using System;
using System.Collections;
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
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using System.Globalization;
using System.Diagnostics;

namespace FITSExplorer
{
    public static class AppEventLog
    {
        public static void Log(string msg, EventLogEntryType entryType)
        {
            if (!System.Diagnostics.EventLog.SourceExists("FITSExplorer"))
                System.Diagnostics.EventLog.CreateEventSource("FITSExplorer", "FITSExplorerLog");

            EventLog appEventLog = new EventLog();
            appEventLog.Source = "FITSExplorer";
            appEventLog.Log = "FITSExplorerLog";
            appEventLog.WriteEntry(msg, entryType);
        }
    }
}
