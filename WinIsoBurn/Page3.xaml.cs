using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

using System;
using System.Collections;
using System.Windows.Interop;
using IMAPI2.Interop;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;
using System.IO;
using System.Windows.Threading;

namespace WinIsoBurn
{
    /// <summary>
    /// Page3.xaml の相互作用ロジック
    /// </summary>
    public partial class Page3 : Page
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct POWER_REQUEST_CONTEXT
        {
            public UInt32 Version;
            public UInt32 Flags;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string SimpleReasonString;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct PowerRequestContextDetailedInformation
        {
            public IntPtr LocalizedReasonModule;
            public UInt32 LocalizedReasonId;
            public UInt32 ReasonStringCount;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string[] ReasonStrings;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct POWER_REQUEST_CONTEXT_DETAILED
        {
            public UInt32 Version;
            public UInt32 Flags;
            public PowerRequestContextDetailedInformation DetailedInformation;
        }

        internal enum PowerRequestType
        {
            PowerRequestDisplayRequired = 0,
            PowerRequestSystemRequired,
            PowerRequestAwayModeRequired,
            PowerRequestMaximum
        }

        private const int POWER_REQUEST_CONTEXT_VERSION = 0;
        private const int POWER_REQUEST_CONTEXT_SIMPLE_STRING = 0x1;
        private const int POWER_REQUEST_CONTEXT_DETAILED_STRING = 0x2;        

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr PowerCreateRequest(ref POWER_REQUEST_CONTEXT Context);

        //PowerCreateRequest
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool PowerSetRequest(IntPtr hPowerRequest, PowerRequestType RequestType);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool CloseHandle(IntPtr handle);
                   
        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true, PreserveSig = false, EntryPoint = "SHCreateStreamOnFileW")]
        static extern void SHCreateStreamOnFile(string fileName, uint mode, ref IStream stream);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool PowerClearRequest(IntPtr hPowerRequest, PowerRequestType RequestType);
        
        private bool isCancelRequested = false;
        private MainWindow mainWindow;
        
        public Page3(MainWindow _mainWindow) 
        {
            mainWindow = _mainWindow;
            InitializeComponent();
            this.WindowTitle = Properties.Resources.Page3_WindowTitle;
        }

        [DllImport("USER32.DLL")]
        private static extern IntPtr GetSystemMenu(IntPtr hWnd, UInt32 bRevert);

        [DllImport("user32.dll")]
        static extern bool EnableMenuItem(IntPtr hMenu, uint uIDEnableItem, uint uEnable);

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            const UInt32         SC_CLOSE  = 0x0000F060;
            const UInt32        MF_ENABLED = 0x00000000;
            WindowInteropHelper wih        = new WindowInteropHelper(mainWindow);
            IntPtr              hMenu      = GetSystemMenu(wih.Handle, 0);
            EnableMenuItem(hMenu, SC_CLOSE, MF_ENABLED);    
        }

        public void Cancel_Click(object sender, RoutedEventArgs e)
        {
            if (!CancelButton.IsEnabled) return;
            isCancelRequested = true;
            CancelButton.IsEnabled = false;
            percentProgress.Text = Properties.Resources.Page3_Canceling;
        }
        
        public void OnWritingFinished(object sender, System.EventArgs a)
        {
            this.NavigationService.Navigate(new Page4(isCancelRequested));
        }

        private void OnUpdateProgress(IDiscFormat2DataEventArgs ea)
        {
            int total = ea.SectorCount;
            int last = ea.LastWrittenLba;
            
            if (ea.CurrentAction == IMAPI_FORMAT2_DATA_WRITE_ACTION.IMAPI_FORMAT2_DATA_WRITE_ACTION_FINALIZATION)
            {
                mainText.Text = Properties.Resources.Page3_FinalProcessing;
                return;
            }

            if (ea.CurrentAction == IMAPI_FORMAT2_DATA_WRITE_ACTION.IMAPI_FORMAT2_DATA_WRITE_ACTION_VERIFYING)
            {
                mainText.Text = Properties.Resources.Page3_Verifying;
                return;
            }            

            float f = (float)last / total;
            int n = (last / (total / 100));
            WritingProgress.Value = n;
            if (!isCancelRequested) percentProgress.Text = f.ToString("P0");
            
            TimeSpan ts = new TimeSpan(0, 0, ea.RemainingTime);
            remainingText.Text = Properties.Resources.Page3_TimeRemainings + ts.ToString();//("T");

            if (n == 100) CancelButton.IsEnabled = false;
        }        
        
        private void OnBurnProgress(object o1, object o2)
        {
            IDiscFormat2Data          dd = o1 as IDiscFormat2Data;
            IDiscFormat2DataEventArgs ea = o2 as IDiscFormat2DataEventArgs;
            if (ea != null && dd!= null)
            {
                Dispatcher.Invoke(
                DispatcherPriority.Normal,
                (Action)delegate() {OnUpdateProgress(ea); }
                );

                if (isCancelRequested) dd.CancelWrite();
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs rea)
        {
            NavigationService.RemoveBackEntry();
            NavigationService.RemoveBackEntry();

            String driveId       = (String)Application.Current.Properties["driveId"];
            String sourceFile    = (String)Application.Current.Properties["sourceFile"];
            int    writingSpeed  = (int)Application.Current.Properties["writingSpeed"];
            bool   toBeClosed    = (bool)Application.Current.Properties["toBeClosed"];
            bool   toVerify      = (bool)Application.Current.Properties["toBeVerified"];

            ThreadStart startCreate = delegate()
            {
                IDiscRecorder2 recorder = new MsftDiscRecorder2();
                recorder.InitializeDiscRecorder(driveId);
                recorder.DisableMcn();
                recorder.AcquireExclusiveAccess(true, "imapi_test");

                IDiscFormat2Data dataWriterImage = new MsftDiscFormat2Data();
                dataWriterImage.Recorder = recorder;
                dataWriterImage.ForceMediaToBeClosed = toBeClosed;
                dataWriterImage.ClientName = "test_imapi";
                dataWriterImage.SetWriteSpeed(writingSpeed, false);

                DiscFormat2Data_Events burnProgress = dataWriterImage as DiscFormat2Data_Events;
                burnProgress.Update += new DiscFormat2Data_EventsHandler(OnBurnProgress);
                IStream stream = null;
                SHCreateStreamOnFile(sourceFile, 0, ref stream);

                POWER_REQUEST_CONTEXT prc;
                IntPtr hPower = (IntPtr)null;
                try{
                    prc.Version = 0;
                    prc.SimpleReasonString = "imapi_test";
                    prc.Flags = POWER_REQUEST_CONTEXT_SIMPLE_STRING;
                    hPower = PowerCreateRequest(ref prc);
                    PowerSetRequest(hPower, PowerRequestType.PowerRequestDisplayRequired);
                }
                catch{
                }

                IBurnVerification verify = dataWriterImage as IBurnVerification;
                if (toVerify)
                {
                    verify.BurnVerificationLevel = IMAPI_BURN_VERIFICATION_LEVEL.IMAPI_BURN_VERIFICATION_FULL;
                }
                else verify.BurnVerificationLevel = IMAPI_BURN_VERIFICATION_LEVEL.IMAPI_BURN_VERIFICATION_NONE;
        
                try
                {
                    dataWriterImage.Write(stream);
                }
                catch (System.Runtime.InteropServices.COMException e)
                {
                    string s = "IMAPI Error: " + Convert.ToString(e.ErrorCode, 16);
                    if ((uint)e.ErrorCode == 0xC0AA0002) //E_IMAPI_REQUEST_CANCELLED
                    {
                        // canceled
                        s += "a";
                    }
                    else if ((uint)e.ErrorCode == 0xC0AA0404) //E_IMAPI_DF2DATA_STREAM_TOO_LARGE_FOR_CURRENT_MEDIA
                    {
                        // Not enought capacity
                        s = Properties.Resources.Page3_Error_NotEnoughCapacity;
                        System.Windows.Forms.DialogResult messageBoxResult =
                            System.Windows.Forms.MessageBox.Show(s, "IMAPI Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                    }
                    else
                    {
                        System.Windows.Forms.DialogResult messageBoxResult =
                            System.Windows.Forms.MessageBox.Show(s, "IMAPI Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                    }
                    //else return; //throw;
                }
                catch
                {
                    //error
                }

                burnProgress.Update -= new DiscFormat2Data_EventsHandler(OnBurnProgress);

                try
                {
                    recorder.ReleaseExclusiveAccess();
                    recorder.EnableMcn();
                }
                catch
                {
                }

                if (hPower != (IntPtr)null)
                {
                    PowerClearRequest(hPower, PowerRequestType.PowerRequestDisplayRequired);
                    CloseHandle(hPower);
                }

                Dispatcher.Invoke(new System.EventHandler(OnWritingFinished), this, null);
            };

            const UInt32 SC_CLOSE = 0x0000F060;
            const UInt32 MF_GRAYED = 0x00000001;
            WindowInteropHelper wih = new WindowInteropHelper(mainWindow);
            IntPtr hMenu = GetSystemMenu(wih.Handle, 0);
            EnableMenuItem(hMenu, SC_CLOSE, MF_GRAYED);    

            Thread thread = new Thread(startCreate);
            thread.IsBackground = true;
            thread.Start();
        }
    }
}
