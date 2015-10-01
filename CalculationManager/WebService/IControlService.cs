using CalculationManager.Additional;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;

namespace CalculationManager.WebService
{
    [ServiceContract]
    public interface IContolServiceEvents
    {
        [OperationContract(IsOneWay=true)]
        void IsCalculationActiveChanged(IsActivePropertyChangedEventArgs value);

        [OperationContract(IsOneWay = true)]
        void QueueChanged(FileQueueChangedArgs elements);

        [OperationContract(IsOneWay = true)]
        void LogChanged(FileCalculationInfo element);
    }

    [ServiceContract(CallbackContract = typeof(IContolServiceEvents))]
    public interface IControlService
    {
        [OperationContract]
        bool GetIsCalculationActive();

        [OperationContract]
        void SetIsCalculationActive(bool value);

        [OperationContract]
        IEnumerable<FileQueueElement> GetQueue();

        [OperationContract]
        IEnumerable<FileCalculationInfo> GetLog();

        [OperationContract]
        bool SubscribeEvents(Guid clientId);

        [OperationContract]
        bool UnsubscribeEvents(Guid clientId);

        [OperationContract]
        bool Ping(Guid clientId);

        [OperationContract]
        string GetLogFile(string fileCalculationInfoId);
    }
}
