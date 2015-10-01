using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace CalculationManagerApplication
{
    /// <summary>
    /// Логика взаимодействия для App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs eventArgs)
        {
            base.OnStartup(eventArgs);
            this.DispatcherUnhandledException += (s, e) =>
                {
                    string errorMessage = string.Format("Необработанное исключение: {0}", e.Exception.Message);
                    e.Handled = true;
                    errorMessage += Environment.NewLine + "Вы хотите продолжить выполнение программы?";
                    if (MessageBox.Show(errorMessage, "Exception", MessageBoxButton.YesNo, MessageBoxImage.Error) == MessageBoxResult.No)
                        Shutdown();
                };
        }
    }
}
