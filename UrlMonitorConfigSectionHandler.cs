#region Imports

using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;

#endregion Imports

namespace UrlMonitor
{
    public class UrlMonitorConfigSectionHandler : IConfigurationSectionHandler
    {
        private const string sectionName = "UrlMonitorConfig";

        public object Create(object parent, object configContext, XmlNode section)
        {
            string config = section.SelectSingleNode("//" + sectionName).OuterXml;

            if (!string.IsNullOrWhiteSpace(config))
            {
                XmlSerializer serializer = new XmlSerializer(typeof(UrlMonitorConfig));
                MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(config));
                ms.Position = 0;
                return (UrlMonitorConfig)serializer.Deserialize(ms);
            }

            return null;
        }
    }
}
