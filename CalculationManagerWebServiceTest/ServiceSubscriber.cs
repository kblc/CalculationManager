using CalculationManagerWebServiceTest.CMWebServiceReference;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CalculationManagerServiceTest
{
    public class ServiceSubscriber : IControlServiceCallback
    {
        public event EventHandler<IsActivePropertyChangedEventArgs> IsCalculationActiveEvent;
        public event EventHandler<FileCalculationInfo> LogChangedEvent;
        public event EventHandler<FileQueueChangedArgs> QueueChangedEvent;

        public void IsCalculationActiveChanged(IsActivePropertyChangedEventArgs value)
        {
            if (IsCalculationActiveEvent != null)
                IsCalculationActiveEvent(this, value);
        }

        public void LogChanged(FileCalculationInfo element)
        {
            if (LogChangedEvent != null)
                LogChangedEvent(this, element);
        }

        public void QueueChanged(FileQueueChangedArgs element)
        {
            if (QueueChangedEvent != null)
                QueueChangedEvent(this, element);
        }
    }
}
