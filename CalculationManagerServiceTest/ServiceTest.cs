using System;
using System.Data;
using System.Data.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using CalculationManager.Additional;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;
using System.ServiceModel;
using CalculationManager;
using System.ServiceModel.Description;
using System.Threading.Tasks;
using CalculationManager.WebService;
using System.Text;

namespace CalculationManagerServiceTest
{
    [TestClass]
    public class ServiceTest
    {
        [TestMethod]
        public void ServiceTest_QueueAccess()
        {
            FileQueue fileQueue = new FileQueue();
            for (int i = 0; i < 10; i++)
            {
                Thread th = new Thread(x => 
                {
                    for (int n = 0; n < 10; n++)
                    { 
                        fileQueue.Push(new FileQueueElement("file"+x.ToString()+n.ToString(), "cmd"+n.ToString(), "utf-8"));
                        Debug.WriteLine("Add item (file{0}{1},cmd{1})", (int)x, n);
                    }
                });
                th.Start(i);
            }
            Thread.Sleep(5000);
            Assert.AreEqual(100, fileQueue.Count);
        }

        [TestMethod]
        public void ServiceTest_Queue_SimilarFiles()
        {
            FileQueue fileQueue = new FileQueue();
            for (int i = 0; i < 5; i++)
                fileQueue.Push(new FileQueueElement("file0", "cmd" + i.ToString(), "utf-8"));
            Assert.AreEqual(1, fileQueue.Count);
        }

        [TestMethod]
        public void ServiceTest_Watcher()
        {
            FileQueue fileQueue = new FileQueue();
            using(FileWatcher fileWatcher = new FileWatcher(fileQueue))
            {
                int errCount = 0;
                fileWatcher.Log = (msg, ex) => 
                {
                    if (ex != null)
                        errCount++;
                };

                if (!System.IO.Directory.Exists(@"D:\test"))
                    System.IO.Directory.CreateDirectory(@"D:\test");

                fileWatcher.Start(new FileWatcherParam[] 
                { 
                    new FileWatcherParam(@"D:\test", "*.txt", "cmd", "utf-8"),
                    new FileWatcherParam(@"D:\test", "*.txt", "cmd", "utf-8"),
                    new FileWatcherParam(@"D:\err" + Guid.NewGuid().ToString("N"), "*.txt", "cmd", "utf-8")
                });

                for (int i = 0; i < 5; i++)
                { 
                    System.IO.File.WriteAllText(string.Format(@"D:\test\{0}.txt", i), "test");
                    System.IO.File.WriteAllText(string.Format(@"D:\test\{0}.xtx", i), "test");
                }
                Thread.Sleep(1000);
                for (int i = 0; i < 5; i++)
                {
                    System.IO.File.Delete(string.Format(@"D:\test\{0}.txt", i));
                    System.IO.File.Delete(string.Format(@"D:\test\{0}.xtx", i));
                }

                Assert.AreEqual(1, errCount, "Error count must equals 1 (load bad folder)");
                Assert.AreEqual(5, fileQueue.Count);

                System.IO.Directory.Delete(@"D:\test", true);
            }
        }

        [TestMethod]
        public void ServiceTest_WatcherAndCommand()
        {
            string folderName = @"D:\test2";
            FileQueue fileQueue = new FileQueue();
            using (FileWatcher fileWatcher = new FileWatcher(fileQueue))
            {
                if (!System.IO.Directory.Exists(folderName))
                    System.IO.Directory.CreateDirectory(folderName);

                fileWatcher.Start(new FileWatcherParam[] { new FileWatcherParam(folderName, "*.d??", "cmd ??", "utf-8") });

                for (int i = 0; i < 5; i++)
                    System.IO.File.WriteAllText(string.Format(@"{0}\test.d{1}", folderName, i.ToString("00")), "test");
                Thread.Sleep(1000);
                for (int i = 0; i < 5; i++)
                    System.IO.File.Delete(string.Format(@"{0}\test.d{1}", folderName, i.ToString("00")));

                Assert.AreEqual(5, fileQueue.Count);

                FileQueueElement item = null;
                while ((item = fileQueue.Pop()) != null)
                {
                    Debug.WriteLine("file: '{0}' and command for it: '{1}'", item.File, item.Command);
                }

                System.IO.Directory.Delete(folderName, true);
            }
        }

        [TestMethod]
        public void ServiceTest_CalculationAndWatcherAndCommand()
        {
            string folderName = @"D:\test3";
            FileQueue fileQueue = new FileQueue();
            using (FileWatcher fileWatcher = new FileWatcher(fileQueue))
            {
                if (!System.IO.Directory.Exists(folderName))
                    System.IO.Directory.CreateDirectory(folderName);
                if (!System.IO.Directory.Exists(folderName + @"\logs"))
                    System.IO.Directory.CreateDirectory(folderName + @"\logs");

                System.IO.File.WriteAllText(string.Format(@"{0}\start.bat", folderName), "@echo [+] test %1 completed");

                fileWatcher.Start(new FileWatcherParam[] { new FileWatcherParam(folderName, "*.d??", string.Format("\"{0}\\start.bat\" ??", folderName), "utf-8") });

                for (int i = 0; i < 5; i++)
                    System.IO.File.WriteAllText(string.Format(@"{0}\test.d{1}", folderName, i.ToString("00")), "test");

                Thread.Sleep(1000);

                Assert.AreEqual(5, fileQueue.Count);

                var virtualParameters = new VirtualParameters();
                virtualParameters.SetParameter("CalculationLogs", string.Format(@"{0}\logs", folderName));
                virtualParameters.SetParameter("ProcessTimeout", "10");
                virtualParameters.SetParameter("CalculationLogs", folderName + @"\logs");
                using (var fileCalculation = new FileCalculation(fileQueue, virtualParameters))
                {
                    var logFiles = new List<string>();

                    fileCalculation.Log += (s, e) =>
                        {
                            if (e != null)
                                Assert.Fail("exception: " + e.Message);
                        };

                    fileCalculation.InfoChanged += (s,e) => 
                        {
                            if (e == null)
                            {
                                Debug.WriteLine("end");
                            }
                            else
                            { 
                                logFiles.Add(System.IO.Path.Combine(virtualParameters.GetParameter("CalculationLogs") ?? string.Empty,e.Id + ".log"));
                                Debug.WriteLine(string.Format("File: '{0}'|status:'{1}'|", e.File, e.Status));
                            }
                        };
                    fileCalculation.Start();

                    while (fileQueue.Count > 0 && fileCalculation.IsActive)
                        Thread.Sleep(1000);
                }

                if (System.IO.Directory.Exists(folderName + @"\logs"))
                    foreach (string fl in System.IO.Directory.GetFiles(folderName + @"\logs"))
                        Debug.WriteLine(string.Format("Log '{0}': {1}", fl, System.IO.File.ReadAllText(fl)));

                for (int i = 0; i < 5; i++)
                    System.IO.File.Delete(string.Format(@"{0}\test.d{1}", folderName, i.ToString("00")));

                if (System.IO.Directory.Exists(folderName))
                    System.IO.Directory.Delete(folderName, true);

                Assert.AreEqual(0, fileQueue.Count);
            }
        }

        [TestMethod]
        public void ServiceTest_WCFAndWait2Min()
        {
            //http://WS27SYCHSS.OFFICE.GTT.GAZPROM.RU:9929/CalculationManager/ControlService
            //net.tcp://WS27SYCHSS.OFFICE.GTT.GAZPROM.RU:9929/CalculationManager/ControlService
            var hostName = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName()).HostName;
            using (ServiceHost srvHost = CreateServiceHost(typeof(ControlService), typeof(IControlService), new Uri(string.Format("net.tcp://{0}:9929/CalculationManager/ControlService", hostName), UriKind.Absolute)))
            {
                srvHost.Open();
                Thread.Sleep(2 * 60 * 1000);
            }
        }

        [TestMethod]
        public void ServiceTest_GetParametersCheck()
        {
            var virtualParameters = new VirtualParameters();
            virtualParameters.SetParameter(@"WatchFolder_Day", @"I:\CDS\ASTRA\TP7\TUM1\FILES\pzg.d??");
            virtualParameters.SetParameter(@"WatchFolder_Hour", @"I:\CDS\ASTRA\TP7\TUM1\FILES\pzg.??");
            virtualParameters.SetParameter(@"CommandLine_Day", @"c:\tp7\raschet\tom\balm_ooo.bat ?? 24");
            virtualParameters.SetParameter(@"CommandLine_Hour", @"c:\tp7\raschet\tom\dtch_o.bat ??");

            var param = CalculationManagerService.GetWatcherParameters(virtualParameters);
            Assert.AreEqual(2, new List<FileWatcherParam>(param).Count);
        }

        [TestMethod]
        public async void ServiceTest_WCFAndCalculationAndWatcherAndCommand(int timeOut)
        {
            string folderName = @"D:\test3";
            FileQueue fileQueue = new FileQueue();
            using (FileWatcher fileWatcher = new FileWatcher(fileQueue))
            {
                if (System.IO.Directory.Exists(folderName))
                    System.IO.Directory.Delete(folderName, true);

                System.IO.Directory.CreateDirectory(folderName);
                if (!System.IO.Directory.Exists(folderName + @"\logs"))
                    System.IO.Directory.CreateDirectory(folderName + @"\logs");

                System.IO.File.WriteAllText(string.Format(@"{0}\start.bat", folderName), "@echo [+] test %1 completed");

                fileWatcher.Start(new FileWatcherParam[] { new FileWatcherParam(folderName, "*.d??", string.Format("\"{0}\\start.bat\" ??", folderName), "utf-8") });

                for (int i = 0; i < 3; i++)
                    System.IO.File.WriteAllText(string.Format(@"{0}\test.d{1}", folderName, i.ToString("00")), "test");

                Thread.Sleep(1000);

                Assert.AreEqual(3, fileQueue.Count);

                var virtualParameters = new VirtualParameters();
                virtualParameters.SetParameter("CalculationLogs", string.Format(@"{0}\logs", folderName));
                virtualParameters.SetParameter("ProcessTimeout", "10");
                virtualParameters.SetParameter("AutoStart", "0");
                int exceptionsCount = 0;
                using (var fileCalculation = new FileCalculation(fileQueue, virtualParameters))
                {
                    var logFiles = new List<string>();

                    fileCalculation.Log += (s, e) =>
                    {
                        if (e != null)
                        {
                            exceptionsCount++;
                            Assert.Fail("[ServiceTest] exception: " + e.Message);
                        }
                    };

                    fileCalculation.InfoChanged += (s, e) =>
                    {
                        if (e == null)
                        {
                            Debug.WriteLine("[ServiceTest] file calculation end");
                        }
                        else
                        {
                            logFiles.Add(System.IO.Path.Combine(virtualParameters.GetParameter("CalculationLogs") ?? string.Empty, e.Id + ".log"));
                            Debug.WriteLine(string.Format("[ServiceTest] file calculation: '{0}'|status:'{1}'", e.File, e.Status));
                        }
                    };
                    //fileCalculation.Start();

                    var serviceData = new ServiceData() 
                    {
                        Queue = fileQueue,
                        Watcher = fileWatcher,
                        Calculation = fileCalculation,
                        Parameters = virtualParameters 
                    };
                    CalculationManagerService.ServiceInstance = serviceData;

                    //http://WS27SYCHSS.OFFICE.GTT.GAZPROM.RU:9929/CalculationManager/ControlService
                    //net.tcp://WS27SYCHSS.OFFICE.GTT.GAZPROM.RU:9929/CalculationManager/ControlService
                    var hostName = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName()).HostName;
                    using (ServiceHost srvHost = CreateServiceHost(typeof(ControlService), typeof(IControlService), new Uri(string.Format("net.tcp://{0}:9929/CalculationManager/ControlService", hostName), UriKind.Absolute)))
                    {
                        srvHost.Open();
                        //Thread.Sleep(10 * 60 * 1000);
                        await Task.Delay(timeOut);
                    }

                    Assert.AreEqual(false, fileCalculation.IsActive, "Поток подсчёта уже должен быть завершен");
                }

                if (System.IO.Directory.Exists(folderName + @"\logs"))
                    foreach (string fl in System.IO.Directory.GetFiles(folderName + @"\logs"))
                        Debug.WriteLine(string.Format("[ServiceTest] Log '{0}': {1}", fl, System.IO.File.ReadAllText(fl).Trim()));

                if (System.IO.Directory.Exists(folderName))
                    System.IO.Directory.Delete(folderName, true);

                Assert.AreEqual(0, fileQueue.Count);
            }
        }

        [TestMethod]
        public void ServiceTest_ReadDosFile()
        {
            string res = System.IO.File.ReadAllText(@"C:\TP7\RASCHET\TOM\logs\d2e87e5d85b7409982b56c15873474c7.log", Encoding.GetEncoding("cp866"));
        }

        private ServiceHost CreateServiceHost(Type serviceType, Type interfaceServiceType, Uri uri)
        {
            var host = new ServiceHost(serviceType, uri);

            //var mexBehavior = new ServiceMetadataBehavior() { HttpGetEnabled = true, HttpGetUrl = uri };
            host.Description.Behaviors.Add(new ServiceMetadataBehavior());

            if (host.Description.Behaviors.Contains(typeof(ServiceDebugBehavior)))
            {
                ((ServiceDebugBehavior)host.Description.Behaviors[typeof(ServiceDebugBehavior)]).IncludeExceptionDetailInFaults = true;
            } else
                host.Description.Behaviors.Add(new ServiceDebugBehavior() { IncludeExceptionDetailInFaults = true });

            //var mexHttpBinding = MetadataExchangeBindings.CreateMexHttpBinding();
            var mexTcpBinding = MetadataExchangeBindings.CreateMexTcpBinding();
            //var wsHttpBinding = new WSDualHttpBinding();
            var tcpBinding = new NetTcpBinding(SecurityMode.Transport);
            tcpBinding.Security.Transport.ClientCredentialType = TcpClientCredentialType.Windows;

            host.AddServiceEndpoint(typeof(IMetadataExchange), mexTcpBinding, uri.AbsoluteUri + "/mex");
            //host.AddServiceEndpoint(interfaceServiceType, wsHttpBinding, "msec");
            host.AddServiceEndpoint(interfaceServiceType, tcpBinding, "");

            Debug.WriteLine("[ServiceTest] create service host at: " + uri.AbsoluteUri);
            host.Opened += (s, e) => { Debug.WriteLine("[ServiceTest] opened"); };
            host.Opening += (s, e) => { Debug.WriteLine("[ServiceTest] opening"); };
            host.UnknownMessageReceived += (s, e) => { Debug.WriteLine("[ServiceTest] UnknownMessageReceived"); };
            host.Closed += (s, e) => { Debug.WriteLine("[ServiceTest] closed"); };
            host.Closing += (s, e) => { Debug.WriteLine("[ServiceTest] closing"); };
            host.Faulted += (s, e) => { Debug.WriteLine("[ServiceTest] faulted"); };

            return host;
        }
    }
}
