using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FITSFramework
{
    class FrameworkExtensions
    {
    }

    public static class ListOfFITSHeaderItemExtensions
    {
        public static bool ContainsHeaderKey(this List<FITSHeaderItem> list, string key)
        {
            foreach (FITSHeaderItem fhi in list)
            {
                if (key.ToUpper().CompareTo(fhi.Key) == 0)
                    return true;
            }

            return false;
        }

        public static FITSHeaderItem GetHeaderItem(this List<FITSHeaderItem> list, string key)
        {
            foreach (FITSHeaderItem fhi in list)
            {
                if (key.ToUpper().CompareTo(fhi.Key) == 0)
                    return fhi;
            }

            return null;
        }

        public static int GetHeaderIntItem(this List<FITSHeaderItem> list, string key)
        {
            foreach (FITSHeaderItem fhi in list)
            {
                if (key.ToUpper().CompareTo(fhi.Key) == 0)
                {
                    int numericResult;
                    if (int.TryParse(fhi.Value, out numericResult))
                        return numericResult;
                    else
                        return -1;
                }
            }

            return -1;
        }

        public static float GetHeaderFloatItem(this List<FITSHeaderItem> list, string key)
        {
            foreach (FITSHeaderItem fhi in list)
            {
                if (key.ToUpper().CompareTo(fhi.Key) == 0)
                {
                    float numericResult;
                    if (float.TryParse(fhi.Value, out numericResult))
                        return numericResult;
                    else
                        return (float)-1.0;
                }
            }

            return (float)-1.0;
        }

        public static void SetHeaderItem(this List<FITSHeaderItem> list, string key, object itemValue)
        {
            foreach (FITSHeaderItem fhi in list)
            {
                if (key.ToUpper().CompareTo(fhi.Key) == 0)
                {
                    fhi.ValueType = itemValue.GetType();
                    fhi.Value = itemValue.ToString();
                    return;
                }
            }
        }
    }
}
