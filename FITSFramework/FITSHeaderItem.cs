using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FITSFramework
{
    public class FITSHeaderItem
    {
        #region Member variables
        private string m_key;
        private string m_value;
        private string m_comment;
        private Type m_valueType;
        #endregion

        #region Properties
        public string Key
        {
            get
            {
                if (!string.IsNullOrEmpty(m_key))
                    return m_key.Trim();
                else
                    return "";
            }
            set { m_key = value; }
        }

        public string Value
        {
            get
            {
                if (!string.IsNullOrEmpty(m_value))
                    return m_value.Trim();
                else
                    return "";
            }
            set { m_value = value; }
        }

        public string Comment
        {
            get
            {
                if (!string.IsNullOrEmpty(m_comment))
                    return m_comment.Trim();
                else
                    return "";
            }
            set { m_comment = value; }
        }

        public Type ValueType       { get { return m_valueType; } set { m_valueType = value; }}
        public bool HasComment      { get { return !string.IsNullOrEmpty(m_comment); }}
        #endregion

        #region Construction
        public FITSHeaderItem() : this("", "", "", typeof(object)) {}
        public FITSHeaderItem(string key, string value, string comment) : this("", "", "", typeof(object)) {}
        public FITSHeaderItem(string key, string value, string comment, Type valueType)
        {
            m_key = key;
            m_value = value;
            m_comment = comment;
            m_valueType = valueType;
        }
        #endregion

        #region Methods
        public static bool IsDisplayableHeaderItem(string key)
        {
            switch (key)
            {
                case "SIMPLE":
                case "END":
                    return false;
            }
            return true;
        }
        #endregion
    }
}
