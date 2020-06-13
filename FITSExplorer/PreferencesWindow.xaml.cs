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

namespace FITSExplorer
{
    public partial class PreferencesWindow : Window
    {
        private delegate void ReadRegistry();           // Delegate used to run the read registry thread
        private ReadRegistry m_readRegistryOp = null;   // Instance of the read registry delegate
        private bool m_readingRegistry = false;         // Flags if we're running the read registry thread

        public PreferencesWindow()
        {
            InitializeComponent();

            m_readingRegistry = true;
            m_readRegistryOp = new ReadRegistry(DoReadRegistry);
            m_readRegistryOp.BeginInvoke(DoReadRegistryComplete, null);
        }

        private void buttonOK_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void buttonCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void DoReadRegistry()
        {
        }

        private void DoReadRegistryComplete(IAsyncResult result)
        {
            this.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, (Action)(() =>
            {
                MessageBox.Show("Done!");
            }));
        }
    }
}
