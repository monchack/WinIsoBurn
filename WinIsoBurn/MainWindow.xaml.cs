
using System;
using System.Management;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Navigation;

using System.Runtime.InteropServices;

namespace WinIsoBurn
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : NavigationWindow
    {
        public MainWindow()
        {
            this.Navigate(new Page1(this));
        }
        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
        }
        override protected void OnClosed(EventArgs a)
        {
        }
        protected override void OnSourceInitialized(EventArgs e)
        {
			base.OnSourceInitialized(e);
        }

		private void onClosing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			Page3 p = this.NavigationService.Content as Page3;
			if (p != null)
			{
				p.Cancel_Click(null, null);
				e.Cancel = true;
			}
		}
    }
}
