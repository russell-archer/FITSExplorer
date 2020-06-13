using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Windows;

namespace FITSExplorer
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static List<string> CommandLineArgs = new List<string>();

        public void AppStartup(object sender, StartupEventArgs e)
        {
        }
    }
}
