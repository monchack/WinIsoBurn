using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

using System;
using System.Management;
using System.Collections;
using System.Windows.Interop;
using IMAPI2.Interop;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

using System.Windows.Threading;

namespace WinIsoBurn
{
    /// <summary>
    /// Page2.xaml の相互作用ロジック
    /// </summary>
    public partial class Page2 : Page
    {
        MainWindow mainWindow;
        public ManagementEventWatcher eventWatcherDriveArrival;
		public ManagementEventWatcher eventWatcherDriveRemoval;
		public ManagementEventWatcher eventWatcherOpticalDisc;
		
        String [] driveId;
        bool isBD = false;
        bool isCD = false;
        bool isSupportedMediaReady = false;
        bool isRewritableMedia = false;
        int [] writingSpeed;
        String selectedDrive;
        int driveCount = -1;
        int selectedSpeed = 0;

        public Page2(MainWindow _mainWindow)
        {
            mainWindow = _mainWindow;
            InitializeComponent();
            this.WindowTitle = Properties.Resources.Page2_WindowTitle;

            UpdateDriveList();
            UpdateMediaStatus();
            UpdateWritingSpeedList();
        }
		
		public void UpdateWritingSpeedList()
		{
			if (selectedSpeed != 0) return;
			if (!writingSpeedListBox.Items.IsEmpty)
			{
				this.writingSpeedListBox.Items.Clear();
			}
			if (!isSupportedMediaReady)
			{
				writingSpeedListBox.Items.Add("");
				writingSpeedListBox.SelectedIndex = 0;
				writingSpeedListBox.IsEnabled = false;
				return;
			}

 			IDiscRecorder2 recorder = new MsftDiscRecorder2();
			recorder.InitializeDiscRecorder(selectedDrive);

            IDiscFormat2Data dataWriterImage = new MsftDiscFormat2Data();
            object[] speeds = null;
            try
            {
                dataWriterImage.Recorder = recorder;
                speeds = dataWriterImage.SupportedWriteSpeeds;
            }
            catch (System.Runtime.InteropServices.COMException e)
            {
                if ((uint)e.ErrorCode == 0xC0AA0407) //E_IMAPI_DF2DATA_RECORDER_NOT_SUPPORTED
                {
                }
            }
			if (speeds != null)
			{
				writingSpeedListBox.IsEnabled = true;
				writingSpeed = new int[speeds.Length];
				for (int i = 0; i < speeds.Length; ++i)
				{
					int n = writingSpeed[i] = (int)speeds[i];
                    if (isCD)
                    {
                        n = ((n + 1) * 10) / 75; // n = (n * 2) / 150 * 10;
                        // CD 1x: 150 KB per sec
                        // Example  GH15L    300(4x) 599(8x)  1199(16x) 1800(24x) 2400(32x) 3000(40x) 3599(48x) 3600(max)
                        //          DVR-212L 300(4x) 750(10x) 1200(16x) 1800(24x) 2400(32x) 3000(40x)
                    }
                    else if (isBD)
                    {
                        n = ((n + 1) * 10) / 2195; // n = (n * 2) / 4390 * 10;
                        // BD 1x: 4390 KB per sec
                        // Example BRP-U6X(Optiarc BD-5740L)  4390(2x) 8780(4x) 13170(6x)

                    }
                    else n = ((n + 1) * 8) / 541; // n = (n * 2) / 1352.5 * 10;
                    // DVD 1x:  1352.5 KB per sec
                    // Example GH15L    2705(4x)           5410(8x)             10820(16x)
                    //         DVR-212L 2705(4x)  4057(6x) 5410(8x)  8115(12x)  10820(16x)                                   

                    if (i == 0 && speeds.Length > 1)
                    {
                        if ((int)speeds[1] + 1 >= (int)speeds[0])
                        {
                            // avoid duplication and show as "Max"
                            writingSpeedListBox.Items.Add(Properties.Resources.Page2_SpeedMax);
                            continue;
                        }
                    }
                    double f = (double)n / 10.0;
                    writingSpeedListBox.Items.Add(f.ToString("0.#") + Properties.Resources.Page2_SpeedSuffix);
				}
				writingSpeedListBox.SelectedIndex = 0;
				selectedSpeed = (int)speeds[0];
			}
			else
			{
				writingSpeedListBox.IsEnabled = false;
				selectedSpeed = 0;
			}
        }

        public void UpdateMediaStatus()
        {
            int i;
            IDiscRecorder2 recorder = new MsftDiscRecorder2();

            isSupportedMediaReady = false;
            isRewritableMedia = false;
            textSupportedMedia.Text = "";

            if (!driveListBox.IsEnabled)
            {
                insertedMedia.Text = "";
                NextPage.IsEnabled = false;
                return;
            }

            recorder.InitializeDiscRecorder(selectedDrive);
            IDiscFormat2Data dataWriterImage = new MsftDiscFormat2Data();
            try
            {
                dataWriterImage.Recorder = recorder;
            }
            catch (System.Runtime.InteropServices.COMException e)
            {
                if ((uint)e.ErrorCode == 0xC0AA0407) //E_IMAPI_DF2DATA_RECORDER_NOT_SUPPORTED
                {
                    insertedMedia.Text = "";
                    NextPage.IsEnabled = false;
                    return;
                }
            }

			int nf, nsec;
			try{
				nf = dataWriterImage.FreeSectorsOnMedia;
				nsec = dataWriterImage.TotalSectorsOnMedia;
			}
			catch (System.Runtime.InteropServices.COMException)
			{
			}

            IDiscRecorder2Ex rc2 = recorder as IDiscRecorder2Ex;

            byte[] resp = new byte[1024];
            uint sb = 1024;
            IntPtr respPtr;
            rc2.GetFeaturePage(IMAPI_FEATURE_PAGE_TYPE.IMAPI_FEATURE_PAGE_TYPE_PROFILE_LIST, true, out respPtr, ref sb);
            if (sb > 1024) sb = 1024;
            Marshal.Copy(respPtr, resp, 0, (int)sb);

            for (i = 4; i < (int)sb; i += 4)
            {
                if (resp[i + 2] != 0)
                {
                    textSupportedMedia.Text += "*";
                    isSupportedMediaReady = true;
                }
                switch (resp[i + 1])
                {
                    case 9:
                    textSupportedMedia.Text += "CD-R ";
                    break;

                    case 10:
                    textSupportedMedia.Text += "CD-RW ";
                    break;
                    
                    case 17:
                    textSupportedMedia.Text += "DVD-R ";
                    break;
                    
                    case 18:
                    textSupportedMedia.Text += "DVD-RAM ";
                    break;

                    case 19:
                    textSupportedMedia.Text += "DVD-RW ";
                    break;

                    case 21:
                    textSupportedMedia.Text += "DVD-R_DL ";
                    break;

                    case 26:
                    textSupportedMedia.Text += "DVD+RW ";
                    break;

                    case 27:
                    textSupportedMedia.Text += "DVD+R ";
                    break;

                    case 43:
                    textSupportedMedia.Text += "DVD+R_DL ";
                    break;

                    case 65:
                    textSupportedMedia.Text += "BD-R ";
                    break;

                    case 67:
                    textSupportedMedia.Text += "BD-RE ";
                    break;
                }
                
            }
            
            IMAPI_MEDIA_PHYSICAL_TYPE n;
            try
            {
                n = dataWriterImage.CurrentPhysicalMediaType;
                switch (n)
                {
                    case IMAPI_MEDIA_PHYSICAL_TYPE.IMAPI_MEDIA_TYPE_CDROM:
					insertedMedia.Text = "CD-ROM or Audio CD";
                    isCD = true;
					isBD = false;
					break;

                    case IMAPI_MEDIA_PHYSICAL_TYPE.IMAPI_MEDIA_TYPE_CDR:
                    insertedMedia.Text = Properties.Resources.MediaType_BlankCDR;//"Blank CD-R";
					isSupportedMediaReady = true;
                    isCD = true;
                    isBD = false;
                    break;

                    case IMAPI_MEDIA_PHYSICAL_TYPE.IMAPI_MEDIA_TYPE_CDRW:
                    insertedMedia.Text = "CD-RW";
					isSupportedMediaReady = true;
					isRewritableMedia = true;
                    isCD = true;
                    isBD = false;
                    break;                   
                                        
                    case IMAPI_MEDIA_PHYSICAL_TYPE.IMAPI_MEDIA_TYPE_DVDDASHRW:
                    insertedMedia.Text = Properties.Resources.MediaType_DVDRW;
					isSupportedMediaReady = true;
					isRewritableMedia = true;
                    isCD = false;
                    isBD = false;
					break;
                    
                    case IMAPI_MEDIA_PHYSICAL_TYPE.IMAPI_MEDIA_TYPE_DVDDASHR:
					insertedMedia.Text = Properties.Resources.MediaType_BlankDVDR;//"Blank DVD-R";
					isSupportedMediaReady = true;
                    isCD = false;
                    isBD = false;
					break;
                    
					case IMAPI_MEDIA_PHYSICAL_TYPE.IMAPI_MEDIA_TYPE_DVDROM:
                    insertedMedia.Text = Properties.Resources.MediaType_DVDROM;
                    isCD = false;
                    isBD = false;
					break;

                    case IMAPI_MEDIA_PHYSICAL_TYPE.IMAPI_MEDIA_TYPE_DVDPLUSR:
                    insertedMedia.Text = Properties.Resources.MediaType_BlankDVDPlusR;//"Blank DVD+R";
					isSupportedMediaReady = true;
                    isCD = false;
                    isBD = false;
                    break;
                    
                    case IMAPI_MEDIA_PHYSICAL_TYPE.IMAPI_MEDIA_TYPE_DVDPLUSRW:
                    insertedMedia.Text = Properties.Resources.MediaType_DVDPlusRW;
					isSupportedMediaReady = true;
					isRewritableMedia = true;
                    isCD = false;
                    isBD = false;
					break;                  

                    case IMAPI_MEDIA_PHYSICAL_TYPE.IMAPI_MEDIA_TYPE_DVDRAM: //IMAPI_PROFILE_TYPE_DVD_RAM:
                    insertedMedia.Text = Properties.Resources.MediaType_DVDRAM;
					isSupportedMediaReady = true;
                    isCD = false;
                    isBD = false;
					isRewritableMedia = true;
                    break;

                    case IMAPI_MEDIA_PHYSICAL_TYPE.IMAPI_MEDIA_TYPE_BDR:
                    insertedMedia.Text = Properties.Resources.MediaType_BlankBDR;
                    isSupportedMediaReady = true;
                    isCD = false;
                    isBD = true;
                    break;

                    case IMAPI_MEDIA_PHYSICAL_TYPE.IMAPI_MEDIA_TYPE_BDRE:
                    insertedMedia.Text = "BD-RE";
                    isSupportedMediaReady = true;
                    isRewritableMedia = true;
                    isCD = false;
                    isBD = true;
                    break;
                    
                    case IMAPI_MEDIA_PHYSICAL_TYPE.IMAPI_MEDIA_TYPE_DVDDASHR_DUALLAYER:
                    insertedMedia.Text = Properties.Resources.MediaType_BlankDVDR_DL;
					isSupportedMediaReady = true;
                    isCD = false;
					isBD = false;
					break;     					

					case IMAPI_MEDIA_PHYSICAL_TYPE.IMAPI_MEDIA_TYPE_DVDPLUSR_DUALLAYER:
                    insertedMedia.Text = Properties.Resources.MediaType_BlankDVDPlusR_DL;
					isSupportedMediaReady = true;
                    isCD = false;
					isBD = false;
					break;                    
                
                    default:
                    insertedMedia.Text = Properties.Resources.MediaType_UnknownMedia;
                    break;
                }
            }
            catch (System.Runtime.InteropServices.COMException e)
            {
				if ((uint)e.ErrorCode == 0xC0AA0202) //E_IMAPI_RECORDER_MEDIA_NO_MEDIA
				{
                    insertedMedia.Text = Properties.Resources.MediaTypeError_NoMediaInserted; //"No media inserted.";
				}
                else if ((uint)e.ErrorCode == 0xC0AA02FF) //E_IMAPI_RECORDER_INVALID_RESPONSE_FROM_DEVICE
                {
                    insertedMedia.Text = Properties.Resources.MediaTypeError_InvalidResponseFromDevice;
                }
                else
                {
                    insertedMedia.Text = "No media detected due to internal error.";
                }
            }
            if (isSupportedMediaReady) NextPage.IsEnabled = true;
            else NextPage.IsEnabled = false;
            
            if (isBD)
            {
				//aaa = dataWriterImage.TotalSectorsOnMedia;
				//aaa = dataWriterImage.FreeSectorsOnMedia;
			}
        }

        public void UpdateDriveList()
        {
            IDiscMaster2 discMaster = new MsftDiscMaster2();
          //  IEnumerator ie = discMaster.GetEnumerator();
            int count = discMaster.Count;
			int cursorPosition = -1;
			int i;
            
            if (driveCount == discMaster.Count) return;
            if (!driveListBox.Items.IsEmpty)
			{
				driveListBox.Items.Clear();
            }
			if (discMaster.Count == 0)
			{
				selectedSpeed = 0;
				driveListBox.Items.Add("");
				//driveListBox.SelectedIndex = 0;				
				driveListBox.IsEnabled = false;
				return;
			}            

			driveId = new String[discMaster.Count];
			for (i = 0; i < count; ++i)
            {
				IDiscRecorder2 recorder = new MsftDiscRecorder2();
                String s = discMaster[i];
				if (s == selectedDrive) cursorPosition = i;
                driveId[i] = s;
                recorder.InitializeDiscRecorder(s);
                s = recorder.ProductId;
                this.driveListBox.Items.Add(s);
            }
			if (cursorPosition == -1)
			{
				selectedSpeed = 0;
				cursorPosition = 0;
			}
			driveListBox.SelectedIndex = cursorPosition;
			selectedDrive = discMaster[cursorPosition];
			driveListBox.IsEnabled = true;
        }

		protected override void OnInitialized(EventArgs e)
        {
			WqlEventQuery q;
			ConnectionOptions opt;
            
            opt = new ConnectionOptions();
            opt.EnablePrivileges = true; //sets required privilege
            ManagementScope scope = new ManagementScope("root\\CIMV2", opt);

			// drive arrival event
            q = new WqlEventQuery();
			q.EventClassName = "__InstanceCreationEvent";
            q.WithinInterval = new TimeSpan(0, 0, 1);
			q.Condition = @"TargetInstance ISA 'Win32_LogicalDisk' and TargetInstance.DriveType = 5";
            eventWatcherDriveArrival = new ManagementEventWatcher(scope, q);
			eventWatcherDriveArrival.EventArrived += new System.Management.EventArrivedEventHandler(this.OnWMIDriveEvent);

			// drive removal event
			q = new WqlEventQuery();
			q.EventClassName = "__InstanceDeletionEvent";
			q.WithinInterval = new TimeSpan(0, 0, 1);
			q.Condition = @"TargetInstance ISA 'Win32_LogicalDisk' and TargetInstance.DriveType = 5";
			eventWatcherDriveRemoval = new ManagementEventWatcher(scope, q);
			eventWatcherDriveRemoval.EventArrived += new System.Management.EventArrivedEventHandler(this.OnWMIDriveEvent);

			// disc event CDS_DRIVE_NOT_READY
			q = new WqlEventQuery();
			q.EventClassName = "__InstanceModificationEvent";
			//q.EventClassName = "__InstanceCreationEvent";
			q.WithinInterval = new TimeSpan(0, 0, 1);
			q.Condition = @"TargetInstance ISA 'Win32_LogicalDisk' and TargetInstance.DriveType = 5"; //
			//q.Condition = @"TargetInstance ISA 'Win32_DiskDrive'";
			eventWatcherOpticalDisc = new ManagementEventWatcher(scope, q);
			eventWatcherOpticalDisc.EventArrived += new System.Management.EventArrivedEventHandler(this.OnWMIDiskEvent);
			
            this.Dispatcher.ShutdownStarted += Dispatcher_ShutdownStarted;
            
            base.OnInitialized(e);
        }

		void OnWMIDriveEvent(object sender, EventArrivedEventArgs e)
		{
			try
			{
				Dispatcher.Invoke(new System.EventHandler(OnDeviceChanged), this, null);
			}
			catch
			{
			}
		}

		void OnWMIDiskEvent(object sender, EventArrivedEventArgs e)
		{
			try
			{
				Dispatcher.Invoke(new System.EventHandler(OnDiscChanged), this, null);//????
			}
			catch
			{
			}
		}		
       
        private void Dispatcher_ShutdownStarted( object sender, EventArgs e )
        {
			eventWatcherDriveArrival.Stop();
			eventWatcherDriveRemoval.Stop();
			eventWatcherOpticalDisc.Stop();
			this.Dispatcher.ShutdownStarted -= Dispatcher_ShutdownStarted;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
			eventWatcherDriveArrival.Start();
			eventWatcherDriveRemoval.Start();
			eventWatcherOpticalDisc.Start();
        }
        
        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
			eventWatcherDriveArrival.Stop();
			eventWatcherDriveRemoval.Stop();
			eventWatcherOpticalDisc.Stop();
        }

        private void NextPage_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Properties["driveId"] = selectedDrive;
			Application.Current.Properties["writingSpeed"] = selectedSpeed;
			Application.Current.Properties["toBeClosed"] = checkToBeClose.IsChecked;
			Application.Current.Properties["toBeVerified"] = checkToVeirfy.IsChecked;

			if (isRewritableMedia)
			{
				eventWatcherDriveArrival.Stop();
				eventWatcherDriveRemoval.Stop();
				eventWatcherOpticalDisc.Stop();
				
				MediaEraseDialog dlg = new MediaEraseDialog();
				dlg.Owner = mainWindow;
				Nullable<bool> dialogResult = dlg.ShowDialog();
				if (dialogResult == false)
				{
					eventWatcherDriveArrival.Start();
					eventWatcherDriveRemoval.Start();
					eventWatcherOpticalDisc.Start();
					return;
				}
			}			
            this.NavigationService.Navigate(new Page3(mainWindow));
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void OnBackClicked( object sender, RoutedEventArgs e )
        {
            if( NavigationService.CanGoBack )　NavigationService.GoBack();
        }

		public void OnDiscChanged(object sender, System.EventArgs a)
		{
            String s = insertedMedia.Text;
			UpdateMediaStatus();
			if (s != insertedMedia.Text)
			{
				selectedSpeed = 0;
				UpdateWritingSpeedList();
			}
		}

        public void OnDeviceChanged(object sender, System.EventArgs a)
        {
             UpdateDriveList();
             selectedSpeed = 0;
             UpdateMediaStatus();
			 UpdateWritingSpeedList();
        }

        private void OnDriveSelected(object sender, SelectionChangedEventArgs e)
        {
			if (driveListBox.SelectedIndex >= 0)
			{
				selectedDrive = driveId[driveListBox.SelectedIndex];
				selectedSpeed = 0;
				UpdateMediaStatus();
				UpdateWritingSpeedList();
			}
        }
        
        private void OnSpeedSelected(object sender, SelectionChangedEventArgs e)
        {
			if (writingSpeedListBox.SelectedIndex >= 0 && writingSpeed != null)
				selectedSpeed = writingSpeed[writingSpeedListBox.SelectedIndex];
        }

        //trial
        private void button1_Click(object sender, RoutedEventArgs e)
        {
            IDiscRecorder2 recorder = new MsftDiscRecorder2();
            recorder.InitializeDiscRecorder(selectedDrive);
            IDiscRecorder2Ex dr2 = recorder as IDiscRecorder2Ex;
            //recorder.QueryInterface();
            byte[] resp = new byte[32];
            byte[] recv = new byte[18];
            uint sb = 1024;
            IntPtr respPtr;

            resp[0] = 0; resp[1] = 0; resp[2] = 0; resp[3] = 0; resp[4] = 0;
            resp[5] = 0; resp[6] = 0; resp[7] = 0; resp[8] = 0; resp[9] = 0;
            resp[10] = 0; resp[11] = 0; resp[12] = 0; 

            try
            {
                dr2.SendCommandNoData(resp, 6, recv, 60);
                
                dr2.GetDiscInformation(out respPtr, ref sb);
                if (sb > 1024) sb = 1024;
                Marshal.Copy(respPtr, resp, 0, (int)sb);
            }
            catch (System.Runtime.InteropServices.COMException )
            {
            }
         }
    }
}
