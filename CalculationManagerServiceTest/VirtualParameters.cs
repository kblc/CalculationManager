using CalculationManager.Additional;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CalculationManagerServiceTest
{
    public class ServiceData : IServiceData
    {
        public IParameters Parameters { get; set; }

        public FileQueue Queue { get; set; }

        public FileWatcher Watcher { get; set; }

        public FileCalculation Calculation { get; set; }
    }

    public class VirtualParameters : IParameters
    {
        private object parametersLock = new Object();
        private Dictionary<string, string> parameters = new Dictionary<string, string>();

        public string GetParameter(string key)
        {
            lock (parametersLock)
            { 
                if (parameters.Keys.Contains(key))
                    return parameters[key];
                else
                    return null;
            }
        }

        public void SetParameter(string key, string value)
        {
            lock (parametersLock)
            {
                if (parameters.Keys.Contains(key))
                    parameters[key] = value;
                else
                    parameters.Add(key, value);
            }

            if (Changed != null)
                Changed(this, new KeyValuePair<string, string>(key, value));
        }

        public Dictionary<string, string> GetParameters()
        {
            lock (parametersLock)
            { 
                return new Dictionary<string, string>(parameters);
            }
        }

        public event EventHandler<KeyValuePair<string,string>> Changed;
    }
}
