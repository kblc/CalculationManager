using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CalculationManagerFileWriter
{
    public interface IParameters
    {
        string GetParameter(string key);
        void SetParameter(string key, string value);
        Dictionary<string, string> GetParameters();
        event EventHandler<KeyValuePair<string, string>> Changed;
    }

    public class ParametersContainer : IParameters
    {
        private static object parametersLock = new Object();

        public ParametersContainer()
        {
            StaticChanged += (s, e) => { if (Changed != null) Changed(s, e); };
        }

        public string GetParameter(string key)
        {
            lock (parametersLock)
            {
                if (System.Configuration.ConfigurationManager.AppSettings.AllKeys.Contains(key))
                    return System.Configuration.ConfigurationManager.AppSettings[key];
                else
                    return null;
            }
        }

        public void SetParameter(string key, string value)
        {
            KeyValuePair<string, string> changed = new KeyValuePair<string, string>();
            lock (parametersLock)
            {
                var configFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                var settings = configFile.AppSettings.Settings;

                if (settings.AllKeys.Contains(key))
                    settings[key].Value = value;
                else
                    settings.Add(key, value);
                configFile.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection(configFile.AppSettings.SectionInformation.Name);

                changed = new KeyValuePair<string, string>(key, value);
            }

            if (StaticChanged != null && changed.Key.Length > 0)
                StaticChanged(this, changed);
        }

        public Dictionary<string, string> GetParameters()
        {
            try
            {
                var res = new Dictionary<string, string>();
                lock (parametersLock)
                {
                    foreach (var key in System.Configuration.ConfigurationManager.AppSettings.AllKeys.Cast<string>())
                        res.Add(key, System.Configuration.ConfigurationManager.AppSettings[key]);
                }
                return res;
            }
            catch (Exception ex)
            {
                throw new Exception("Can't load application configuration", ex);
            }
        }

        public event EventHandler<KeyValuePair<string, string>> Changed;

        private static EventHandler<KeyValuePair<string, string>> StaticChanged;
    }
}
