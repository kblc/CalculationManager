using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CalculationManagerServiceTest;
using System.Diagnostics;
using System.ServiceModel;
using CalculationManagerWebServiceTest.CMWebServiceReference;
using System.Threading;
using System.Threading.Tasks;

namespace CalculationManagerWebServiceTest
{
    [TestClass]
    public class WebServiceTest
    {
        [TestMethod]
        public void WebServiceTest_Connect()
        {
            var serverThread = new Thread(new ThreadStart(() =>
            {
                var st = new ServiceTest();
                st.ServiceTest_WCFAndCalculationAndWatcherAndCommand(10*60*1000);
            }));
            serverThread.Start();
            try
            { 
                var evnt = new ServiceSubscriber();
                evnt.IsCalculationActiveEvent += (s, e) => 
                {
                    Debug.WriteLine(string.Format("[WebServiceTest] IsActive changed to '{0}' by user '{1}'", e.IsActive, e.UserName)); 
                };
                evnt.LogChangedEvent += (s, e) =>
                {
                    Debug.WriteLine(string.Format("[WebServiceTest] Log changed (id:'{0}')", e != null && e.Id != null ? e.Id : "null"));
                };
                evnt.QueueChangedEvent += (s, e) =>
                {
                    Debug.WriteLine(string.Format("[WebServiceTest] Queue changed (file:'{0}', action:'{1}')", e.Element.File, e.Action));
                };
                InstanceContext evntContext = new InstanceContext(evnt as IControlServiceCallback);

                Thread.Sleep(3000);
                var clientId = Guid.NewGuid();
                using (var proxy = new ControlServiceClient(evntContext))
                {
                    proxy.ClientCredentials.Windows.ClientCredential = System.Net.CredentialCache.DefaultNetworkCredentials;

                    Debug.WriteLine("[WebServiceTest] subscribe events");
                    proxy.SubscribeEvents(clientId);

                    var queueElements = proxy.GetQueue();
                    Assert.AreEqual(3, queueElements.Length);
                
                    string folderName = @"D:\test3";
                    for (int i = 3; i < 5; i++)
                        System.IO.File.WriteAllText(string.Format(@"{0}\test.d{1}", folderName, i.ToString("00")), "test");

                    Thread.Sleep(3000);

                    queueElements = proxy.GetQueue();
                    Assert.AreEqual(5, queueElements.Length);

                    Debug.WriteLine("[WebServiceTest] SetIsCalculationActive(true)");
                    proxy.SetIsCalculationActive(true);
                    Thread.Sleep(1000);

                    while ((queueElements = proxy.GetQueue()).Length > 0 
                        && (proxy.GetIsCalculationActive()))
                        Thread.Sleep(1000);

                    Debug.WriteLine("[WebServiceTest] SetIsCalculationActive(false)");
                    proxy.SetIsCalculationActive(false);
                    Thread.Sleep(1000);

                    Debug.WriteLine("[WebServiceTest] unsubscribe events");
                    proxy.UnsubscribeEvents(clientId);
                }
            }
            finally 
            {
                serverThread.Abort();
            }
        }
    }
}
