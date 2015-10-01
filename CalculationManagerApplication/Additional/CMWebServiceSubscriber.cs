using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CalculationManagerApplication.CMWebServiceReference;
using System.ServiceModel;

namespace CalculationManagerApplication.Additional
{
    [CallbackBehavior(ConcurrencyMode=ConcurrencyMode.Multiple)]
    public class CMWebServiceSubscriber : IControlServiceCallback
    {
        public event EventHandler<IsActivePropertyChangedEventArgs> IsCalculationActiveEvent;
        public event EventHandler<CMWebServiceReference.FileCalculationInfo> LogChangedEvent;
        public event EventHandler<FileQueueChangedArgs> QueueChangedEvent;

        public void IsCalculationActiveChanged(IsActivePropertyChangedEventArgs value)
        {
            if (IsCalculationActiveEvent != null)
                IsCalculationActiveEvent(this, value);
        }

        public void LogChanged(CMWebServiceReference.FileCalculationInfo element)
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
