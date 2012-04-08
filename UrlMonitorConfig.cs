using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace UrlMonitor
{
    public class Header
    {
        public string Name;
        public string Value;
    }

    public class MonitoredUrl : IComparable<MonitoredUrl>
    {
        public string Path;
        public string Method;
        public string PostText;
        public byte[] PostBytes;
        public string StatusCodeRegex;
        public string HeadersRegex;
        public string BodyRegex;
        public string MismatchMessage;
        public bool AlertIfChanged;
        public string EmailAddresses;

        [XmlArray("Headers")]
        [XmlArrayItem("Header")]
        public Header[] Headers;

        [XmlIgnore]
        public DateTime LastCheck;

        [XmlIgnore]
        public byte[] PostDataBytes;

        [XmlIgnore]
        public long MD51;

        [XmlIgnore]
        public long MD52;

        [XmlIgnore]
        public bool InProcess;

        [XmlIgnore]
        public TimeSpan Frequency;

        [XmlElement("Frequency")]
        public string FrequencyString
        {
            get { return Frequency.ToString(); }
            set { Frequency = TimeSpan.Parse(value); }
        }

        [XmlIgnore]
        public TimeSpan MaxTime;

        [XmlElement("MaxTime")]
        public string MaxTimeString
        {
            get { return MaxTime.ToString(); }
            set { MaxTime = TimeSpan.Parse(value); }
        }

        public override int GetHashCode()
        {
            return Path.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            MonitoredUrl other = (obj as MonitoredUrl);
            if (other == null)
            {
                return false;
            }

            return (Path == other.Path);
        }

        public override string ToString()
        {
            return Path;
        }

        public int CompareTo(MonitoredUrl other)
        {
            int result = LastCheck.CompareTo(other.LastCheck);

            if (result == 0)
            {
                result = Path.CompareTo(other.Path);
            }

            return result;
        }
    }

    public class Email
    {
        public string Host;
        public string UserName;
        public string Password;
        public int Port;
        public bool Ssl;
        public string Subject;
        public string From;
    }

    public class UrlMonitorConfig
    {
        [XmlArray("Urls")]
        [XmlArrayItem("Url")]
        public MonitoredUrl[] Urls;

        [XmlIgnore]
        public SortedSet<MonitoredUrl> UrlSet;

        public Email Email;
        public int MaxThreads;

        [XmlIgnore]
        public TimeSpan SleepTimeUrl;
        [XmlElement("SleepTimeUrl")]
        public string SleepTimeUrlString
        {
            get { return SleepTimeUrl.ToString(); }
            set { SleepTimeUrl = TimeSpan.Parse(value); }
        }

        [XmlIgnore]
        public TimeSpan SleepTimeBatch;
        [XmlElement("SleepTimeBatch")]
        public string SleepTimeBatchString
        {
            get { return SleepTimeBatch.ToString(); }
            set { SleepTimeBatch = TimeSpan.Parse(value); }
        }
    }
}
