using CalculationManagerApplication.Additional;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace CalculationManagerApplication
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            SourceInitialized += (s, e) => { hwndSource = (System.Windows.Interop.HwndSource)PresentationSource.FromVisual(this); };
            InitializeComponent();
            listBox.SelectionChanged += (s, e) => { listBox.SelectedItem = null; };
            ((CMWebServiceData)Resources["serviceData"]).PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == "IsError")
                    {
                        App.Current.Dispatcher.Invoke(() =>
                        {
                            TaskbarItemInfo.Overlay = ((CMWebServiceData)Resources["serviceData"]).IsError
                                ? (ImageSource)Resources["overlayImage"]
                                : null;
                        });
                    }
                };
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            var minBtn = GetTemplateChild("PART_minimizeButton") as System.Windows.Controls.Primitives.ButtonBase;
            var closeBtn = GetTemplateChild("PART_closeButton") as System.Windows.Controls.Primitives.ButtonBase;
            var maxBtn = GetTemplateChild("PART_restoreButton") as System.Windows.Controls.Primitives.ButtonBase;
            var moveRect = GetTemplateChild("PART_MoveRectangle") as UIElement;
            var resize = GetTemplateChild("PART_ReseizeGrips") as Grid;
            var captionBorder = GetTemplateChild("PART_Caption") as Border;

            if (minBtn != null)
                minBtn.Click += (s, e) => { this.WindowState = System.Windows.WindowState.Minimized; };
            if (closeBtn != null)
                closeBtn.Click += (s,e) => { this.Close(); };
            if (maxBtn != null)
                maxBtn.Click += (s, e) => { this.WindowState = (this.WindowState == System.Windows.WindowState.Normal) ? System.Windows.WindowState.Maximized : System.Windows.WindowState.Normal; };
            if (moveRect != null)
                moveRect.PreviewMouseMove += (s, e) => { if (Mouse.LeftButton == MouseButtonState.Pressed) DragMove(); };
            if (captionBorder != null)
                captionBorder.MouseDown += (s, e) => 
                { 
                    if (e.ClickCount == 2)
                    {
                        if (this.WindowState == System.Windows.WindowState.Normal)
                            this.WindowState = System.Windows.WindowState.Maximized; 
                        else if (this.WindowState == System.Windows.WindowState.Maximized)
                            this.WindowState = System.Windows.WindowState.Normal;
                    }
                };
            
            if (resize != null)
                foreach (System.Windows.Controls.Primitives.Thumb thumb in resize.Children.OfType<System.Windows.Controls.Primitives.Thumb>())
                {
                    var direction = ResizeDirection.Top;
                    if (thumb.VerticalAlignment == System.Windows.VerticalAlignment.Top && thumb.HorizontalAlignment == System.Windows.HorizontalAlignment.Stretch)
                        direction = ResizeDirection.Top;
                    else if (thumb.VerticalAlignment == System.Windows.VerticalAlignment.Top && thumb.HorizontalAlignment == System.Windows.HorizontalAlignment.Left)
                        direction = ResizeDirection.TopLeft;
                    else if (thumb.VerticalAlignment == System.Windows.VerticalAlignment.Top && thumb.HorizontalAlignment == System.Windows.HorizontalAlignment.Right)
                        direction = ResizeDirection.TopRight;
                    else if (thumb.VerticalAlignment == System.Windows.VerticalAlignment.Bottom && thumb.HorizontalAlignment == System.Windows.HorizontalAlignment.Stretch)
                        direction = ResizeDirection.Bottom;
                    else if (thumb.VerticalAlignment == System.Windows.VerticalAlignment.Bottom && thumb.HorizontalAlignment == System.Windows.HorizontalAlignment.Left)
                        direction = ResizeDirection.BottomLeft;
                    else if (thumb.VerticalAlignment == System.Windows.VerticalAlignment.Bottom && thumb.HorizontalAlignment == System.Windows.HorizontalAlignment.Right)
                        direction = ResizeDirection.BottomRight;
                    else if (thumb.VerticalAlignment == System.Windows.VerticalAlignment.Stretch && thumb.HorizontalAlignment == System.Windows.HorizontalAlignment.Left)
                        direction = ResizeDirection.Left;
                    else if (thumb.VerticalAlignment == System.Windows.VerticalAlignment.Stretch && thumb.HorizontalAlignment == System.Windows.HorizontalAlignment.Right)
                        direction = ResizeDirection.Right;

                    thumb.PreviewMouseDown += (s, e) => { ResizeWindow(direction); };
                }
        }

        #region Resize

        private enum ResizeDirection
        {
            Left = 1,
            Right = 2,
            Top = 3,
            TopLeft = 4,
            TopRight = 5,
            Bottom = 6,
            BottomLeft = 7,
            BottomRight = 8,
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, UInt32 msg, IntPtr wParam, IntPtr lParam);

        private System.Windows.Interop.HwndSource hwndSource;
        private void ResizeWindow(ResizeDirection direction)
        {
            SendMessage(hwndSource.Handle, 0x112, (IntPtr)(61440 + direction), IntPtr.Zero);
        }

        #endregion
    }
}
