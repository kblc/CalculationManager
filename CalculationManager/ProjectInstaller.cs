using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.Linq;
using System.ServiceProcess;
using System.Threading.Tasks;

namespace CalculationManager
{
    [RunInstaller(true)]
    public partial class ProjectInstaller : System.Configuration.Install.Installer
    {
        public ProjectInstaller()
        {
            InitializeComponent();
        }

        private void SetServiceStatus(bool startService)
        {
            ServiceControllerStatus setStatus = startService ? ServiceControllerStatus.Running : ServiceControllerStatus.Stopped;
            try
            {
                var controller = new ServiceController(typeof(CalculationManagerService).Name);
                if (controller != null && controller.Status != setStatus)
                {
                    if (startService)
                        controller.Start();
                    else
                        controller.Stop();
                    controller.WaitForStatus(setStatus, new TimeSpan(0, 0, 30));
                }
            }
            catch { }
        }

        private void serviceInstaller1_BeforeInstall(object sender, InstallEventArgs e)
        {
            SetServiceStatus(false);
        }

        private void serviceInstaller1_AfterInstall(object sender, InstallEventArgs e)
        {
            SetServiceStatus(true);
        }

        private void serviceInstaller1_BeforeUninstall(object sender, InstallEventArgs e)
        {
            SetServiceStatus(false);
        }
    }
}
