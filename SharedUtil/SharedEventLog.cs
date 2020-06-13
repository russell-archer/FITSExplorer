using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace SharedUtil
{
    public static class SharedEventLog
    {
        public static void Log(string msg, EventLogEntryType entryType)
        {
            try
            {
                if (!System.Diagnostics.EventLog.SourceExists("FITSExplorer"))
                    System.Diagnostics.EventLog.CreateEventSource("FITSExplorer", "FITSExplorerLog");

                EventLog appEventLog = new EventLog();
                appEventLog.Source = "FITSExplorer";
                appEventLog.Log = "FITSExplorerLog";
                appEventLog.WriteEntry(msg, entryType);
            }
            catch
            {
            }
        }
    }
}
