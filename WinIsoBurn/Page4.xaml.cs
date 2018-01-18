using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace WinIsoBurn
{
    /// <summary>
    /// Page4.xaml の相互作用ロジック
    /// </summary>
    public partial class Page4 : Page
    {
        bool isCanceled;
        public Page4(bool _isCanceled) 
        {
            isCanceled = _isCanceled;
            InitializeComponent();
            this.WindowTitle = Properties.Resources.Page4_WindowTitle;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            NavigationService.RemoveBackEntry();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
        
        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            if (isCanceled) textTitle.Text = Properties.Resources.Page4_Canceled;
        }
    }
}
