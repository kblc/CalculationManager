using CalculationManagerApplication.Additional;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace CalculationManagerApplication.CMWebServiceReference
{
    public partial class FileCalculationInfo : INotifyPropertyChanged
    {
        public bool HasLogContent
        {
            get { return !string.IsNullOrWhiteSpace(LogContent); }
        }

        private string logContent = string.Empty;
        public string LogContent
        {
            get { return logContent; }
            set { logContent = value; RaisePropertyChanged("LogContent"); RaisePropertyChanged("HasLogContent"); }
        }

        public DelegateCommand LoadLogContentCommand { get; set; }

        private DelegateCommand copyErrorTextToClipboardCommand = null;
        public DelegateCommand CopyErrorTextToClipboardCommand
        {
            get
            {
                return copyErrorTextToClipboardCommand ?? (copyErrorTextToClipboardCommand = new DelegateCommand((o) =>
                {
                    Clipboard.SetText(this.Error);
                }));
            }
        }

        private DelegateCommand copyLogToClipboardCommand = null;
        public DelegateCommand CopyLogToClipboardCommand
        {
            get
            {
                return copyLogToClipboardCommand ?? (copyLogToClipboardCommand = new DelegateCommand((o) =>
                {
                    Clipboard.SetText(this.LogContent);
                }));
            }
        }
    }

}
