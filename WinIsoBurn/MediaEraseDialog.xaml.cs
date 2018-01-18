using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

using System.Windows.Navigation;
using System.Management;
using System.Collections;
using System.Windows.Interop;
using IMAPI2.Interop;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;
using System.IO;
using System.Windows.Threading;

//using System.Drawing;
using System.ComponentModel;

namespace WinIsoBurn
{
    /// <summary>
    /// MediaEraseDialog.xaml の相互作用ロジック
    /// </summary>
    public partial class MediaEraseDialog : Window
    {
        // Win32 APIのインポート
        [DllImport("USER32.DLL")]
        private static extern IntPtr GetSystemMenu(IntPtr hWnd, UInt32 bRevert);
        [DllImport("USER32.DLL")]
        private static extern UInt32 RemoveMenu(IntPtr hMenu, UInt32 nPosition, UInt32 wFlags);
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        private static extern int SetClassLong(IntPtr hWnd, int nIndex, int dwNewLong);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetClassLong(IntPtr hWnd, int nIndex);    
                
        private const UInt32 SC_CLOSE = 0x0000F060;
        private const UInt32 MF_BYCOMMAND = 0x00000000;
        
        private const int GWL_STYLE = -16;
        private const int WS_SYSMENU = 0x80000;
        private const int CS_NOCLOSE = 0x00000200;
        private const int GCL_STYLE = -26;
        
        public MediaEraseDialog()
        {
            InitializeComponent();
        }
        
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            
            WindowInteropHelper wih = new WindowInteropHelper(this);
            IntPtr hMenu = GetSystemMenu(wih.Handle, 0);
            RemoveMenu(hMenu, SC_CLOSE, MF_BYCOMMAND);
            SetWindowLong(wih.Handle, GWL_STYLE, GetWindowLong(wih.Handle, GWL_STYLE) & ~WS_SYSMENU); // remove "close" entry
            SetClassLong(wih.Handle, GCL_STYLE, GetClassLong(wih.Handle, GCL_STYLE) | CS_NOCLOSE); // remove close button
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            WindowInteropHelper wih = new WindowInteropHelper(this);
            this.DialogResult = false;
            this.Close();
        }

        public void OnEraseFinished(object sender, System.EventArgs a)
        {
            this.DialogResult = true;
            this.Close();
        }

        private void OnUpdateProgress(int elapsedTime, int estimatedTotalSeconds)
        {
            int total = estimatedTotalSeconds;
            int last = elapsedTime;

            float f = (float)elapsedTime / estimatedTotalSeconds;
            int n = (elapsedTime / (estimatedTotalSeconds / 100));
            eraseProgress.Value = n;
            percentProgress.Text = f.ToString("P0");
        }
        
        private void OnEraseProgress(object o1, int elapsedTime, int estimatedTotalSeconds)
        {
            Dispatcher.Invoke(
            DispatcherPriority.Normal,
            (Action)delegate() { OnUpdateProgress(elapsedTime, estimatedTotalSeconds); }
            );
        }
        
        private void EraseStart_Click(object sender, RoutedEventArgs e)
        {
            CancelButton.IsEnabled = false;
            EraseStartButton.IsEnabled = false;
            eraseProgress.IsEnabled = true;
            checkQuickErase.IsEnabled = false;
            String driveId = (String)Application.Current.Properties["driveId"];
            
            eraseStatusText.Text = Properties.Resources.MediaErase_Erasing;
            
            ThreadStart start = delegate()
            {
                IDiscRecorder2 recorder = new MsftDiscRecorder2();
                recorder.InitializeDiscRecorder(driveId);
                recorder.AcquireExclusiveAccess(true, "imapi_erase_test");
                IDiscFormat2Erase discErase = new MsftDiscFormat2Erase();

                discErase.Recorder = recorder;
                discErase.ClientName = "imapi_erase_test";
                
                try
                {
                    discErase.FullErase = (bool)checkQuickErase.IsChecked;
                }
                catch (System.InvalidOperationException)
                {
                    discErase.FullErase = false;
                }
                
                DiscFormat2Erase_Events eraseEvent = discErase as DiscFormat2Erase_Events; //DiscFormat2Data_Events burnProgress = dataWriterImage as DiscFormat2Data_Events;
                if (discErase.FullErase) eraseEvent.Update += new DiscFormat2Erase_EventsHandler(OnEraseProgress);
                
                try{
                    discErase.EraseMedia();
                }
                catch
                {
                    //this.DialogResult = true;
                }
                
                if (discErase.FullErase)
                {
                    eraseEvent.Update -= new DiscFormat2Erase_EventsHandler(OnEraseProgress);
                }
                recorder.ReleaseExclusiveAccess();
                Dispatcher.Invoke(new System.EventHandler(OnEraseFinished), this, null);
            };
            
            Thread thread = new Thread(start);
            thread.IsBackground = true;
            thread.Start();
        }
    }
}
