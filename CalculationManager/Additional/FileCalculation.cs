using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using Helpers.Serialization;
using System.Xml.Serialization;

namespace CalculationManager.Additional
{
    [DataContract(Name = "FileCalculationStatus")]
    public enum FileCalculationStatus 
    {
        [EnumMember]
        InAction = 0,
        [EnumMember]
        Done = 1,
        [EnumMember]
        Error = 2
    }

    [Serializable]
    [XmlInclude(typeof(FileCalculationStatus))]
    [DataContract]
    public class FileCalculationInfo
    {
        [DataMember]
        public string Id { get; set; }
        [DataMember]
        public string LogFilePath { get; set; }
        [DataMember]
        public string LogEncoding { get; set; }
        [DataMember]
        public FileCalculationStatus Status { get; set; }
        [DataMember]
        public DateTime DateStart { get; set; }
        [DataMember]
        public DateTime? DateEnd { get; set; }
        [DataMember]
        public string File { get; set; }
        [DataMember]
        public string Error { get; set; }

        public FileCalculationInfo()
        {
            Id = Guid.NewGuid().ToString("N");
            DateStart = DateTime.UtcNow;
            DateEnd = null;
        }

        public FileCalculationInfo(string logDir) : this()
        {
            LogFilePath = System.IO.Path.Combine(logDir ?? string.Empty, Id + ".log");
        }
    }

    [DataContract]
    public class IsActivePropertyChangedEventArgs : EventArgs
    {
        [DataMember]
        public bool IsActive { get; set; }
        [DataMember]
        public string UserName { get; set; }
        public IsActivePropertyChangedEventArgs() { }
        public IsActivePropertyChangedEventArgs(bool isActive, string userName)
        {
            IsActive = isActive;
            UserName = userName;
        }
    }

    public class FileCalculation : IDisposable
    {
        private object infoLogLock = new Object();
        private List<FileCalculationInfo> infoLog = new List<FileCalculationInfo>();

        public FileQueue Queue { get; private set; }

        public IParameters Parameters { get; private set; }

        private bool isActive = false;
        public bool IsActive 
        { 
            get { return isActive; }
        }

        private void SetIsActive(bool isActive, string userName)
        {
            if (IsActive != isActive)
            { 
                this.isActive = isActive;
                if (IsActiveChanged != null)
                    IsActiveChanged(this, new IsActivePropertyChangedEventArgs(isActive, userName));
            }
        }

        public event EventHandler<IsActivePropertyChangedEventArgs> IsActiveChanged;

        public LogMessage Log { get; set; }

        private void LogMessage(string message, Exception ex = null)
        {
            if (Log != null)
                Log(message, ex);
        }

        public FileCalculation(FileQueue fileQueue, IParameters parameters)
        {
            if (fileQueue == null)
                throw new ArgumentNullException("fileQueue", "Queue can't be null");
            if (parameters == null)
                throw new ArgumentNullException("parameters", "Parameters can't be null");

            Queue = fileQueue;
            Parameters = parameters;

            var statisticLog = System.IO.Path.Combine(Parameters.GetParameter("CalculationLogs"), "log.xml");
            if (System.IO.File.Exists(statisticLog))
            {
                List<FileCalculationInfo> res = null;
                typeof(List<FileCalculationInfo>).DeserializeFromXML(System.IO.File.ReadAllText(statisticLog), out res);
                if (res != null)
                    infoLog.AddRange(res);
                infoLog.ForEach(il => 
                {
                    if (il.Status == FileCalculationStatus.InAction)
                    {
                        il.Error = "Задание прервано";
                        il.Status = FileCalculationStatus.Error;
                    }
                });
            }
        }

        private Thread calculationThread = null;

        private void SetInfo(FileCalculationInfo info)
        {
            lock(infoLogLock)
            {
                if (!infoLog.Contains(info) && info != null)
                    infoLog.Add(info);

                while (infoLog.Count > 100)
                {
                    var item = infoLog.FirstOrDefault();
                    if(item != null)
                    { 
                        if (System.IO.File.Exists(item.LogFilePath))
                        try
                        {
                            System.IO.File.Delete(item.LogFilePath);
                        }
                        catch { }
                        infoLog.Remove(item);
                    }
                }

                var statisticLog = System.IO.Path.Combine(Parameters.GetParameter("CalculationLogs"), "log.xml");
                System.IO.File.WriteAllText(statisticLog, infoLog.SerializeToXML(true));
            }
            RaiseInfoChanged(info);
        }

        private void RaiseInfoChanged(FileCalculationInfo info)
        {
            if (InfoChanged != null)
                InfoChanged(this, info);
        }

        public List<FileCalculationInfo> GetLog()
        {
            lock (infoLogLock)
            {
                return new List<FileCalculationInfo>(infoLog);
            }
        }

        private void CalculationThread(object param)
        {
            try
            {
                while (true)
                {
                    DateTime dt;
                    if (!DateTime.TryParse(Parameters.GetParameter("StartFromDate"), out dt))
                        dt = DateTime.Now.AddDays(-30);

                    int timeOut;
                    if (!int.TryParse(Parameters.GetParameter("ProcessTimeout"), out timeOut))
                        timeOut = 180;

                    var el = Queue.Pop();
                    if (el != null)
                    {
                        if (el.Changed >= dt)
                            try
                            {
                                LogMessage(string.Format("Файл '{0}' (дата изменения: {1}) принят в обработку", el.File, el.Changed.ToString("yyyy.MM.dd hh:mm:ss")));
                                dt = el.Changed;
                                var info = new FileCalculationInfo(Parameters.GetParameter("CalculationLogs"))
                                {
                                    Status = FileCalculationStatus.InAction,
                                    File = el.File,
                                    LogEncoding = el.LogEncoding
                                };
                                try
                                {
                                    SetInfo(info);

                                    string command = el.Command;
                                    string arguments = " >" + info.LogFilePath;

                                    if (el.Command.IndexOf(" ") > 0)
                                    {
                                        if (el.Command.FirstOrDefault() == '\"')
                                        {
                                            var ind = el.Command.Substring(1).IndexOf("\"") + 2;
                                            command = el.Command.Substring(0, ind);
                                            arguments = el.Command.Substring(ind + 1) + arguments;
                                        }
                                        else
                                        {
                                            var ind = el.Command.IndexOf(" ");
                                            command = el.Command.Substring(0, ind);
                                            arguments = el.Command.Substring(ind) + arguments;
                                        }
                                    }

                                    LogMessage(string.Format("For file '{0}' start command '{1}' with arguments '{2}'", el.File, command, arguments));

                                    var psi = new ProcessStartInfo(command, arguments);
                                    psi.WindowStyle = ProcessWindowStyle.Hidden;
                                    using(var process = Process.Start(psi))
                                    try
                                    {
                                        if (process.WaitForExit(timeOut * 1000))
                                        {
                                            info.DateEnd = DateTime.UtcNow;
                                            info.Status = FileCalculationStatus.Done;
                                            SetInfo(info);
                                        }
                                        else
                                            throw new Exception("Процесс выполняется слишком долго");
                                    }
                                    catch (ThreadAbortException ex)
                                    {
                                        if (process != null)
                                            try
                                            {
                                                process.Kill();
                                            }
                                            catch { }
                                        throw ex;
                                    }

                                    Parameters.SetParameter("StartFromDate", dt.ToString());
                                }
                                catch (Exception ex)
                                {
                                    info.Error = ex.GetFullText(false);
                                    info.DateEnd = DateTime.UtcNow;
                                    info.Status = FileCalculationStatus.Error;
                                    SetInfo(info);
                                    throw ex;
                                }
                            }
                            catch (ThreadAbortException ex)
                            {
                                Queue.Push(el);
                                throw ex;
                            }
                        else
                            LogMessage(string.Format("Файл '{0}' (дата изменения: {1}) пропущен", el.File, el.Changed.ToString("yyyy.MM.dd hh:mm:ss")));
                    }
                    else
                        Thread.Sleep(1000);
                }
            }
            catch (ThreadAbortException)
            {
                LogMessage("Поток остановлен пользователем");
            }
            catch(Exception ex)
            {
                LogMessage(ex.Message, ex);
            }
            finally
            {
                SetInfo(null);
            }
        }

        public string GetLogFileContent(string fileCalculationInfoId)
        {
            FileCalculationInfo info = null;
            lock(infoLogLock)
                info = infoLog.FirstOrDefault(i => i.Id == fileCalculationInfoId);

            if (info != null)
            {
                var enc = string.IsNullOrWhiteSpace(info.LogEncoding)
                    ? Encoding.Default
                    : (Encoding.GetEncoding(info.LogEncoding) ?? Encoding.Default);

                if (!System.IO.File.Exists(info.LogFilePath))
                    throw new Exception("Файл лога не найден");

                return (System.IO.File.ReadAllText(info.LogFilePath, enc) ?? string.Empty).Trim();
            }
            else
                throw new Exception("Неверный идентификатор очереди");
        }

        private string GetCurrentUser()
        {
            var curr = System.Security.Principal.WindowsIdentity.GetCurrent();
            if (curr != null)
            {
                string result = curr.Name;
                if (curr.User.IsWellKnown(System.Security.Principal.WellKnownSidType.LocalSystemSid))
                    result = "LocalSystem";
                if (curr.User.IsWellKnown(System.Security.Principal.WellKnownSidType.LocalServiceSid))
                    result = "LocalService";
                return result;
            }
            else
                return string.Empty;
        }

        public void Start(string userName = null)
        {
            if (string.IsNullOrWhiteSpace(userName))
            {
                //var cred = System.Net.CredentialCache.DefaultNetworkCredentials;
                //userName = string.Format(@"{0}\{1}", cred.Domain, cred.UserName);
                userName = GetCurrentUser();
            }

            if (CheckUser(userName))
            {
                calculationThread = new Thread(new ParameterizedThreadStart(CalculationThread));
                calculationThread.Start(null);
                SetIsActive(true, userName);
            }
            else
                throw new UnauthorizedAccessException(string.Format("Данному пользователю '{0}' запрещено управлять сервисом", userName));
        }

        public void Stop(string userName = null)
        {
            if (string.IsNullOrWhiteSpace(userName))
            { 
                //var cred = System.Net.CredentialCache.DefaultNetworkCredentials;
                //userName = string.Format(@"{0}\{1}", cred.Domain, cred.UserName);
                userName = GetCurrentUser();
            }

            if (CheckUser(userName))
            {
                if (calculationThread != null && calculationThread.IsAlive)
                    calculationThread.Abort();
                calculationThread = null;
                SetIsActive(false, userName);
            }
            else
                throw new UnauthorizedAccessException(string.Format("Данному пользователю '{0}' запрещено управлять сервисом", userName));
        }

        public Func<string, bool> CheckUser = new Func<string,bool>((s) => true);

        public event EventHandler<FileCalculationInfo> InfoChanged;

        public void Dispose()
        {
            Stop();
        }
    }
}
