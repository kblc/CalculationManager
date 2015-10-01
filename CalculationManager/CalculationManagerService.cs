using System;
using System.Configuration;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using CalculationManager.Additional;
using System.Threading;
using System.ServiceModel;
using CalculationManager.WebService;

namespace CalculationManager
{
    public partial class CalculationManagerService : ServiceBase, IServiceData
    {
        public static IServiceData ServiceInstance { get; set; }

        private IParameters parameters = null;
        public IParameters Parameters
        { 
            get
            {
                return parameters ?? (parameters = new ParametersContainer());
            }
        }

        private FileQueue fileQueue = null;
        public FileQueue Queue
        {
            get
            {
                return fileQueue ?? (fileQueue = new FileQueue());
            }
        }

        private FileWatcher fileWatcher = null;
        public FileWatcher Watcher
        {
            get 
            {
                if (fileWatcher == null)
                {
                    fileWatcher = new FileWatcher(Queue);
                    fileWatcher.Log = (message, ex) => 
                    {
                        if (ex == null)
                            eventLog.WriteEntry(string.Format("Service '{0}' FileWatcher: '{1}'", ServiceName, message), EventLogEntryType.Information);
                        else
                            eventLog.WriteEntry(string.Format("Service '{0}' FileWatcher exception: '{1}'{2}{3}", ServiceName, message, Environment.NewLine, ex.GetFullText()), EventLogEntryType.Error, (int)ErrorCodes.CalculationManagerService_Error, (short)Subcategoryes.InAction);
                    };
                }
                return fileWatcher;
            }
        }

        private FileCalculation fileCalculation = null;
        public FileCalculation Calculation 
        {
            get
            {
                if (fileCalculation == null)
                {
                    fileCalculation = new FileCalculation(Queue, Parameters);
                    fileCalculation.IsActiveChanged += (s,e) =>
                    {
                        eventLog.WriteEntry(string.Format("Service '{0}' FileCalculation: user '{1}' change active to '{2}'", ServiceName, e.UserName, e.IsActive), EventLogEntryType.Information);
                    };
                    fileCalculation.Log = (message, ex) =>
                    {
                        if (ex == null)
                            eventLog.WriteEntry(string.Format("Service '{0}' FileCalculation: '{1}'", ServiceName, message), EventLogEntryType.Information);
                        else
                            eventLog.WriteEntry(string.Format("Service '{0}' FileCalculation exception: '{1}'{2}{3}", ServiceName, message, Environment.NewLine, ex.GetFullText()), EventLogEntryType.Error, (int)ErrorCodes.CalculationManagerService_Error, (short)Subcategoryes.InAction);
                    };
                    fileCalculation.CheckUser = (userName) =>
                        {
                            var allowedUsers = Parameters.GetParameter("AllowedUsers");
                            if (!string.IsNullOrWhiteSpace(allowedUsers))
                                return allowedUsers
                                    .Split(new char[] {'|'}, StringSplitOptions.RemoveEmptyEntries)
                                    .Any(u => string.Compare(userName, u, true) == 0);

                            eventLog.WriteEntry(string.Format("Service '{0}' FileCalculation audit error: user '{1}' can't start/stop calculation", ServiceName, userName), EventLogEntryType.Error, (int)ErrorCodes.CalculationManagerService_Error, (short)Subcategoryes.InAction);
                            return false;
                        };
                }
                return fileCalculation;
            }
        }

        private ServiceHost controlServiceHost = null;

        public CalculationManagerService()
        {
            ServiceInstance = this;
            string log = ServiceName + "Log";

            InitializeComponent();
            if (!System.Diagnostics.EventLog.SourceExists(ServiceName))
                System.Diagnostics.EventLog.CreateEventSource(ServiceName, log);
            eventLog.Source = ServiceName;
            eventLog.Log = log;
        }

        private IEnumerable<FileWatcherParam> GetWatcherParameters()
        {
            return GetWatcherParameters(Parameters);
        }

        public static IEnumerable<FileWatcherParam> GetWatcherParameters(IParameters parameters)
        {
            string startWith = "WatchFolder_";
            var allParameters = parameters.GetParameters();
            var allPrm =
                allParameters
                    .Where(p => p.Key.ToLowerInvariant().StartsWith(startWith.ToLowerInvariant()))
                    .Select(p => new { ParamFolderAndFilter = p.Value, ParamSubKey = p.Key.Substring(startWith.Length) })
                    .Select(p => new 
                    { 
                        ParamFolderAndFilter = p.ParamFolderAndFilter, 
                        ParamCommand = allParameters.ContainsKey("CommandLine_" + p.ParamSubKey) ? allParameters["CommandLine_" + p.ParamSubKey] : string.Empty,
                        LogCodepage = allParameters.ContainsKey("Codepage_" + p.ParamSubKey) ? allParameters["Codepage_" + p.ParamSubKey] : string.Empty,
                    })
                    .Where(p => !string.IsNullOrWhiteSpace(p.ParamCommand))
                    .Where(p => p.ParamFolderAndFilter.LastIndexOf(@"\") > 0)
                    .Select(p => new
                    {
                        Folder = p.ParamFolderAndFilter.Substring(0, p.ParamFolderAndFilter.LastIndexOf(@"\")),
                        Filter = p.ParamFolderAndFilter.Substring(p.ParamFolderAndFilter.LastIndexOf(@"\") + 1),
                        Command = p.ParamCommand,
                        LogCodepage = p.LogCodepage,
                    })
                    .Select(p => new FileWatcherParam(p.Folder, p.Filter, p.Command, p.LogCodepage))
                    .ToArray();
            return allPrm;
        }

        private Timer restartWebserviceTimer = null;
        private void WebserviceStart()
        {
            WebserviceStop();

            var errBehavior = new ErrorServiceBehavior();
            errBehavior.OnException += (s, e) =>
            {
                eventLog.WriteEntry(string.Format("Service '{0}' ControlServiceHost exception:{1}'{2}'", ServiceName, Environment.NewLine, e.GetFullText()), EventLogEntryType.Error, (int)ErrorCodes.CalculationManagerService_Error, (short)Subcategoryes.InAction);
            };

            controlServiceHost = new ServiceHost(typeof(ControlService));
            controlServiceHost.Description.Behaviors.Add(errBehavior);
            controlServiceHost.Faulted += (s, e) =>
            {
                eventLog.WriteEntry(string.Format("Service '{0}' ControlServiceHost faulted", ServiceName), EventLogEntryType.Error, (int)ErrorCodes.CalculationManagerService_Error, (short)Subcategoryes.InAction);
                restartWebserviceTimer = new Timer(new TimerCallback(
                    (o) => 
                    {
                        restartWebserviceTimer.Dispose();
                        restartWebserviceTimer = null;
                        if (controlServiceHost == null || controlServiceHost.State != CommunicationState.Opened)
                            WebserviceStart();
                    }), null, -1, (int)new TimeSpan(0, 1, 0).TotalMilliseconds);
            };
            controlServiceHost.Open();
        }

        private void WebserviceStop()
        {
            if (controlServiceHost != null && controlServiceHost.State != CommunicationState.Closed)
            { 
                controlServiceHost.Abort();
                controlServiceHost = null;
            }
        }

        private void StartupCommand()
        {
            var cmd = Parameters.GetParameter("ServiceStartupCommand");
            if (!string.IsNullOrEmpty(cmd))
                try
                {
                    using(var process = System.Diagnostics.Process.Start(cmd))
                        if (!process.WaitForExit(10 * 1000))
                        {
                            process.Kill();
                            throw new Exception("timeout");
                        }
                }
                catch (Exception ex)
                {
                    throw new Exception(string.Format("startup process {0}", ex.Message), ex);
                }
        }

        protected override void OnStart(string[] args)
        {
            int step = 0;
            base.OnStart(args);
            try
            {
                step++;
                StartupCommand();

                step++;
                Queue.Clear();
                step++;
                var param = GetWatcherParameters();
                step++;
                Watcher.Start(param);
                step++;

                bool autoStart = true;
                if (!bool.TryParse(Parameters.GetParameter("AutoStart"), out autoStart))
                    Parameters.SetParameter("AutoStart", autoStart.ToString());
                step++;

                if (autoStart)
                    Calculation.Start();
                step++;

                WebserviceStart();
                step++;

                eventLog.WriteEntry(string.Format("Service '{0}' started", ServiceName), EventLogEntryType.Information);
            }
            catch(Exception ex)
            {
                eventLog.WriteEntry(string.Format("Service '{0}' can't start (step:{1}, exception: '{2}'):{3}{4}", ServiceName, step, ex.Message, Environment.NewLine, ex.GetFullText()), EventLogEntryType.Error, (int)ErrorCodes.CalculationManagerService_CtyticalError, (short)Subcategoryes.Loading);
                throw ex;
            }
        }

        protected override void OnStop()
        {
            int step = 0;
            base.OnStop();
            try
            {
                step++;
                WebserviceStop();
                step++;
                Calculation.CheckUser = new Func<string, bool>((userName) => true);
                Calculation.Stop();
                step++;
                Watcher.Stop();
                step++;
                Queue.Clear();
            }
            catch(Exception ex)
            {
                eventLog.WriteEntry(string.Format("Service '{0}' can't stop (step:{1}, exception: '{2}'):{3}{4}", ServiceName, step, ex.Message, Environment.NewLine, ex.GetFullText()), EventLogEntryType.Error, (int)ErrorCodes.CalculationManagerService_CtyticalError, (short)Subcategoryes.Unloading);
            }
            eventLog.WriteEntry(string.Format("Service '{0}' stoped", ServiceName), EventLogEntryType.Information);
        }
    }
}
