using CalculationManager.Additional;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace CalculationManager.WebService
{
    public class ControlServiceClient
    {
        public IContolServiceEvents Events { get; private set; }
        public System.Security.Principal.WindowsIdentity Identity { get; private set; }
        public DateTime LastPing { get; set; }
        public ControlServiceClient(IContolServiceEvents events, System.Security.Principal.WindowsIdentity identity)
        {
            Events = events;
            Identity = identity;
            LastPing = DateTime.UtcNow;
        }
    }

    [ServiceBehavior(InstanceContextMode=InstanceContextMode.Single, ConcurrencyMode=ConcurrencyMode.Multiple)]
    public class ControlService: IControlService
    {
        private static object clientLocker = new Object();
        private static Dictionary<Guid, ControlServiceClient> clients = new Dictionary<Guid, ControlServiceClient>();

        public ControlService()
        {
            if (CalculationManagerService.ServiceInstance != null)
            { 
                CalculationManagerService.ServiceInstance.Calculation.IsActiveChanged += (s, e) => 
                    {
                        Broadcast(item => item.Events.IsCalculationActiveChanged(e));
                    };

                CalculationManagerService.ServiceInstance.Calculation.InfoChanged += (s, e) =>
                    {
                        Broadcast(item => item.Events.LogChanged(e));
                    };

                CalculationManagerService.ServiceInstance.Queue.Changed += (s, e) =>
                    {
                        Broadcast(item => item.Events.QueueChanged(e));
                    };
            }
        }

        public bool GetIsCalculationActive()
        {
            return CalculationManagerService.ServiceInstance.Calculation.IsActive;
        }

        public void SetIsCalculationActive(bool value)
        {
            if (CalculationManagerService.ServiceInstance.Calculation.IsActive != value)
            {
                var windowsInent = ServiceSecurityContext.Current.WindowsIdentity;
                if (windowsInent != null)
                { 
                    if (value)
                        CalculationManagerService.ServiceInstance.Calculation.Start(windowsInent.Name);
                    else
                        CalculationManagerService.ServiceInstance.Calculation.Stop(windowsInent.Name);
                }
            }
        }

        public IEnumerable<FileQueueElement> GetQueue()
        {
            return CalculationManagerService.ServiceInstance.Queue.GetQueue();
        }

        public IEnumerable<FileCalculationInfo> GetLog()
        {
            return CalculationManagerService.ServiceInstance.Calculation.GetLog();
        }

        public bool SubscribeEvents(Guid clientId)
        {
            try
            {
                IContolServiceEvents subscriber = OperationContext.Current.GetCallbackChannel<IContolServiceEvents>();
                lock(clientLocker)
                {
                    if (clients.Keys.Contains(clientId))
                        clients.Remove(clientId);
                    clients.Add(clientId, new ControlServiceClient(subscriber, ServiceSecurityContext.Current.WindowsIdentity));
                }
                return true;
            }
            catch(Exception) 
            {
                return false;
            }
        }

        public bool UnsubscribeEvents(Guid clientId)
        {
            try
            {
                IContolServiceEvents subscriber = OperationContext.Current.GetCallbackChannel<IContolServiceEvents>();
                lock (clientLocker)
                {
                    if (clients.Keys.Contains(clientId))
                        clients.Remove(clientId);
                }
                return true;
            }
            catch (Exception) 
            {
                return false;
            }
        }

        private void Broadcast(Action<ControlServiceClient> actionToDo)
        {
            lock (clientLocker)
            {
                var inactiveClients = clients
                    .AsParallel()
                    .WithDegreeOfParallelism(10)
                    .Select(item =>
                        {
                            try
                            {
                                actionToDo(item.Value);
                                return null;
                            }
                            catch
                            {
                                return (Guid?)item.Key;
                            }
                        }
                    )
                    .Where(i => i.HasValue)
                    .Select(i => i.Value)
                    .ToArray();

                foreach (var c in inactiveClients)
                    clients.Remove(c);
            }

        }

        public bool Ping(Guid clientId)
        {
            lock (clientLocker)
            {
                if (clients.ContainsKey(clientId))
                    clients[clientId].LastPing = DateTime.UtcNow;
            }
            return true;
        }

        public string GetLogFile(string fileCalculationInfoId)
        {
            try
            { 
                return CalculationManagerService.ServiceInstance.Calculation.GetLogFileContent(fileCalculationInfoId);
            }
            catch(Exception ex)
            {
                return string.Format("Ошибка на сервере при получении лог-файла: {0}", ex.Message);
            }
        }
    }
}
