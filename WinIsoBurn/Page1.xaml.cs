using System.Windows;
using System.Windows.Controls;
using System;
using System.Runtime.InteropServices;
using IMAPI2.Interop;



namespace WinIsoBurn
{
    /// <summary>
    /// Page1.xaml の相互作用ロジック
    /// </summary>
    public partial class Page1 : Page
    {
        MainWindow mainWindow;
        
        public Page1(MainWindow _mainWindow)
        {
            mainWindow = _mainWindow;
            InitializeComponent();
            this.WindowTitle = Properties.Resources.Page1_WindowTitle; //"書き込むファイルの選択";
        }

        private void NextPage_Click( object sender, RoutedEventArgs e )
        {
            if (checkISO()) this.NavigationService.Navigate(new Page2(mainWindow));
        }

        private void Cancel_Click( object sender, RoutedEventArgs e )
        {
            Application.Current.Shutdown();
        }

        private void File_Select_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.OpenFileDialog dlg = new System.Windows.Forms.OpenFileDialog();
            dlg.Title = Properties.Resources.Page1_OpenFileDialogTitle;
            dlg.Filter = "ISO files (*.iso;*.img)|*.iso;*.img|All files (*.*)|*.*";
            dlg.FilterIndex = 1;
            dlg.ShowDialog();
            if (dlg.FileName != string.Empty)
            {
                Application.Current.Properties["sourceFile"] = dlg.FileName;
                selectedFileNameBox.Text = dlg.FileName;
                NextPage.IsEnabled = true;
            }
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {

        }

        private bool checkISO()
        {
            if (selectedFileNameBox.Text == string.Empty) return false;
            
            IIsoImageManager isoManager = new MsftIsoImageManager();
            String isoPath = (String)Application.Current.Properties["sourceFile"];
            isoManager.SetPath(isoPath);
            String s = "";
            try
            {
                isoManager.Validate();
            }
            catch (System.Runtime.InteropServices.COMException e2)
            {
                NextPage.IsEnabled = false;
                switch ((uint)e2.ErrorCode)
                {
                    case 0xC0AAB200: //IMAPI_E_IMAGEMANAGER_IMAGE_NOT_ALIGNED : The image is not aligned on a 2kb sector boundary.
                    s = Properties.Resources.Page1_ISO_Error_InvalidISO;
                    break;

                    case 0xC0AAB202: // IMAPI_E_IMAGEMANAGER_NO_IMAGE)
                    return false;

                    case 0xC0AAB201: //IMAPI_E_IMAGEMANAGER_NO_VALID_VD_FOUND : The image does not contain a valid volume descriptor.
                    s = Properties.Resources.Page1_ISO_Error_InvalidISO;
                    break;

                    case 0xC0AAB203: //IMAPI_E_IMAGEMANAGER_IMAGE_TOO_BIG : The provided image is too large to be validated as the size exceeds MAXLONG.
                    s = Properties.Resources.Page1_ISO_Error_FileSize;
                    break;
                }
                System.Windows.Forms.DialogResult messageBoxResult =
                   System.Windows.Forms.MessageBox.Show(s, "ISO check result", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                return false;
            }
            return true;
        }

   
    }
}

