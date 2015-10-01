using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using CalculationManagerApplication.CMWebServiceReference;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ServiceModel;

namespace CalculationManagerApplication.Additional
{
    public class CMWebServiceData : INotifyPropertyChanged
    {
        private ObservableCollection<FileCalculationInfo> fileCalculation = new ObservableCollection<FileCalculationInfo>();
        public ObservableCollection<FileCalculationInfo> FileCalculation { get { return fileCalculation; } }

        public async void ReloadCalculation()
        {
            if (IsProxyActive)
                await App.Current.Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                            var items = proxy.GetLog();
                            fileCalculation.Clear();
                            foreach (var i in items.Where(item => item != null))
                            {
                                i.LoadLogContentCommand = new DelegateCommand((id) => { LoadLog(Guid.ParseExact((string)id, "N"), i); });
                                fileCalculation.Add(i);
                            }
                    }
                    catch (Exception ex)
                    {
                        ErrorSet(ex.Message);
                    }
                });
            else
                ErrorSet("Подключение к сервису отсутствует");
        }

        private async void LoadLog(Guid id, FileCalculationInfo item)
        {
            if (IsProxyActive)
            try
            {
                item.LogContent = await proxy.GetLogFileAsync(id);
            }
            catch(Exception ex)
            {
                ErrorSet(string.Format("Ошибка при получении лог-файла: {0}", ex.Message));
            }
            else
                ErrorSet("Подключение к сервису отсутствует");
        }

        private ObservableCollection<FileQueueElement> fileQueue = new ObservableCollection<FileQueueElement>();
        public ObservableCollection<FileQueueElement> FileQueue { get { return fileQueue; } }

        public async void ReloadFileQueue()
        {
            if (IsProxyActive)
            try
            {
                var items = await proxy.GetQueueAsync();
                await App.Current.Dispatcher.InvokeAsync(() =>
                {
                    fileQueue.Clear();
                    foreach (var i in items)
                        fileQueue.Add(i);
                });
            }
            catch (Exception ex)
            {
                ErrorSet(ex.Message);
            }
            else
                ErrorSet("Подключение к сервису отсутствует");
        }

        private bool? lastIsActive = null;
        public bool IsActive
        {
            get 
            {
                if (!lastIsActive.HasValue)
                {
                    lastIsActive = (IsProxyActive ? proxy.GetIsCalculationActive() : false);
                } 
                return lastIsActive.Value;
            }
            set
            {
                if (!lastIsActive.HasValue || lastIsActive != value)
                    SetIsActive(value);
            }
        }

        private bool isConnected = false;
        public bool IsConnected
        {
            get { return isConnected; }
            private set
            {
                if (isConnected != value)
                {
                    isConnected = value;
                    RaisePropertyChanged("IsConnected");
                }
            }
        }

        private bool isError = false;
        public bool IsError
        {
            get { return isError; }
            set
            {
                if (isError != value)
                {
                    isError = value;
                    RaisePropertyChanged("IsError");
                }
            }
        }

        private string errorText = string.Empty;
        public string ErrorText
        {
            get { return errorText; }
            set
            {
                if (errorText != value)
                {
                    errorText = value;
                    RaisePropertyChanged("ErrorText");
                }
            }
        }

        public CMWebServiceData()
        {
            Init();
        }

        private Guid clientId = Guid.NewGuid();
        private ControlServiceClient proxy = null;
        private CMWebServiceSubscriber subscriber = null;

        private bool IsProxyActive 
        { 
            get 
            { 
                return proxy != null && proxy.State == CommunicationState.Opened; 
            }
        }

        private System.Threading.Timer connectTimer = null;
        
        private void Init()
        {
            #region Subscriber
            if (subscriber == null)
            {
                subscriber = new CMWebServiceSubscriber();
                subscriber.IsCalculationActiveEvent += (s, e) =>
                {
                    try
                    {
                        lastIsActive = e.IsActive;
                        RaisePropertyChanged("IsActive");
                    }
                    catch (Exception ex)
                    {
                        ErrorSet(string.Format("[subscriber.IsCalculationActiveEvent] exception: {0}", ex.Message));
                    }
                };
                subscriber.LogChangedEvent += (s, e) =>
                {
                    if (e != null)
                        try
                        {
                            App.Current.Dispatcher.Invoke(() =>
                            {
                                if (e != null)
                                    e.LoadLogContentCommand = new DelegateCommand((id) => { LoadLog(Guid.ParseExact((string)id, "N"), e); });

                                var oldItem = FileCalculation.FirstOrDefault(i => i.Id == e.Id);
                                if (oldItem != null)
                                {
                                    var index = FileCalculation.IndexOf(oldItem);
                                    FileCalculation.RemoveAt(index);
                                    FileCalculation.Insert(index, e);
                                }
                                else
                                    FileCalculation.Add(e);
                            });
                        }
                        catch (Exception ex)
                        {
                            ErrorSet(string.Format("[subscriber.LogChangedEvent] exception: {0}", ex.Message));
                        }
                };
                subscriber.QueueChangedEvent += (s, e) =>
                {
                    if (e != null && e.Element != null)
                        try
                        {
                            App.Current.Dispatcher.Invoke(() =>
                            {
                                if (e.Action == FileQueueAction.Pop)
                                {
                                    var oldItem = FileQueue.FirstOrDefault(i => i.File == e.Element.File);
                                    if (oldItem != null)
                                        FileQueue.Remove(oldItem);
                                }
                                else if (e.Action == FileQueueAction.Push)
                                {
                                    FileQueue.Add(e.Element);
                                }
                            });
                        }
                        catch (Exception ex)
                        {
                            ErrorSet(string.Format("[subscriber.QueueChangedEvent] exception: {0}", ex.Message));
                        }
                };
            }
            #endregion
            connectTimer = new System.Threading.Timer(TimmerConnectProc, null, new TimeSpan(0, 0, 0), new TimeSpan(0, 0, 10));
        }

        private void TimmerConnectProc(object state)
        {
            try
            { 
                if (!IsProxyActive)
                    TryToConnect();
                else
                    Ping();
            }
            catch(Exception)
            {
                IsConnected = false;
                ErrorSet("Соединение с сервером потеряно");
            }
        }

        private async void TryToConnect()
        {
            try
            {
#if !DEBUG
                var binding = new NetTcpBinding(SecurityMode.Transport);
                var endPoint = new EndpointAddress("net.tcp://S01-ASTRA.OFFICE.GTT.GAZPROM.RU:9929/CalculationManager/ControlService");
                //var endPoint = new EndpointAddress("net.tcp://WS27SYCHSS.OFFICE.GTT.GAZPROM.RU:9929/CalculationManager/ControlService");
                proxy = new ControlServiceClient(new InstanceContext(subscriber as IControlServiceCallback), binding, endPoint);
#else
                proxy = new ControlServiceClient(new InstanceContext(subscriber as IControlServiceCallback));
#endif
                proxy.ClientCredentials.Windows.ClientCredential = System.Net.CredentialCache.DefaultNetworkCredentials;
                proxy.Open();
                ErrorClear();
                
                proxy.SubscribeEvents(clientId);

                ReloadFileQueue();
                ReloadCalculation();

                await Task.Factory.StartNew(() => 
                {
                    lastIsActive = proxy.GetIsCalculationActive();
                    RaisePropertyChanged("IsActive");
                });

                IsConnected = true;
                //lastIsActive = await proxy.GetIsCalculationActiveAsync();
                //RaisePropertyChanged("IsActive");
            }
            catch (Exception ex)
            {
                ErrorSet(ex.Message);
            }
        }

        private async void Ping()
        {
            await Task.Factory.StartNew(() => 
            {
                if (IsProxyActive)
                    try
                    {
                        proxy.Ping(clientId);
                        IsConnected = true;
                    }
                    catch (Exception)
                    {
                        IsConnected = false;
                    }
                else
                    IsConnected = false;
            });
        }

        private async void SetIsActive(bool value)
        {
            await Task.Factory.StartNew(() =>
            {
                try
                {
                    proxy.SetIsCalculationActive(value);
                }
                catch(Exception ex)
                {
                    ErrorSet(ex.Message);
                }
            });
        }

        private System.Threading.Timer errorClearTimer = null;

        private void TimmerClearErrorProc(object state)
        {
            ErrorClear();
        }

        private void ErrorSet(string errorText)
        {
            ErrorText = errorText == null
                ? string.Empty
                : string.Format("[{0}] {1}", DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss"), errorText);
            IsError = !string.IsNullOrWhiteSpace(ErrorText);

            if (errorClearTimer != null)
            { 
                errorClearTimer.Dispose();
                errorClearTimer = null;
            }

            if (IsError && IsActive)
            {
                errorClearTimer = new System.Threading.Timer(TimmerClearErrorProc, null, -1, (int)new TimeSpan(0, 0, 10).TotalMilliseconds);
            }

            if (!IsProxyActive)
                IsConnected = false;
        }

        private void ErrorClear()
        {
            ErrorSet(null);
        }

        #region PropertyChanged
        private void RaisePropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
        public event PropertyChangedEventHandler PropertyChanged;
        #endregion

        private DelegateCommand activateCommand = null;
        public DelegateCommand ActivateCommand
        {
            get 
            { 
                return activateCommand ?? (activateCommand = new DelegateCommand((o) => 
                    {
                        IsActive = true;
                    }));
            }
        }

        private DelegateCommand deactivateCommand = null;
        public DelegateCommand DeactivateCommand
        {
            get
            {
                return deactivateCommand ?? (deactivateCommand = new DelegateCommand((o) =>
                {
                    IsActive = false;
                }));
            }
        }

        private DelegateCommand clearLogCommand = null;
        public DelegateCommand ClearLogCommand
        {
            get
            {
                return clearLogCommand ?? (clearLogCommand = new DelegateCommand((o) =>
                {
                    fileCalculation.Clear();
                }));
            }
        }

        private DelegateCommand reloadLogCommand = null;
        public DelegateCommand ReloadLogCommand
        {
            get
            {
                return reloadLogCommand 
                    ?? (reloadLogCommand = new DelegateCommand((o) => { ReloadCalculation(); }));
            }
        }

        private DelegateCommand reloadQueueCommand = null;
        public DelegateCommand ReloadQueueCommand
        {
            get
            {
                return reloadQueueCommand ?? 
                    (reloadQueueCommand = new DelegateCommand((o) => { ReloadFileQueue(); }));
            }
        }
    }
}
