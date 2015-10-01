using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ServiceModel;
using System.ServiceModel.Dispatcher;
using System.ServiceModel.Description;

namespace CalculationManager.Additional
{
    public class ErrorHandler : IErrorHandler
    {
        public event EventHandler<Exception> OnException;

        public bool HandleError(Exception error)
        {
            if (OnException != null)
                OnException(this, error);
            return false;
        }

        public void ProvideFault(Exception error, System.ServiceModel.Channels.MessageVersion version, ref System.ServiceModel.Channels.Message fault)
        {
        }
    }

    public class ErrorServiceBehavior : IServiceBehavior
    {
        public event EventHandler<Exception> OnException;

        public void AddBindingParameters(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase, System.Collections.ObjectModel.Collection<ServiceEndpoint> endpoints, System.ServiceModel.Channels.BindingParameterCollection bindingParameters)
        {
        }

        public void ApplyDispatchBehavior(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase)
        {
            ErrorHandler handler = new ErrorHandler();
            handler.OnException += (s, e) => 
            {
                if (OnException != null)
                    OnException(this, e);
            };
            foreach (ChannelDispatcher dispatcher in serviceHostBase.ChannelDispatchers)
                dispatcher.ErrorHandlers.Add(handler);
        }

        public void Validate(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase)
        {
        }
    }
}
