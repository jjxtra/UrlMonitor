#region Imports

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.ServiceProcess;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Security.Cryptography;

#endregion Imports

namespace UrlMonitor
{
    public class UrlMonitorService : ServiceBase
    {
        private class UrlResponse
        {
            public HttpStatusCode StatusCode;
            public string[] Headers;
            public string Body;
            public TimeSpan Duration;
            public long MD51;
            public long MD52;
        }

        private UrlMonitorConfig config;
        private Thread serviceThread;
        private bool run = true;
        private int threadCount;

        private UrlResponse GetResponseForUrl(MonitoredUrl url)
        {
            DateTime start = DateTime.UtcNow;
            HttpWebRequest request = HttpWebRequest.Create(url.Path) as HttpWebRequest;
            if (string.IsNullOrWhiteSpace(url.Method))
            {
                url.Method = "GET";
            }

            request.Method = url.Method;
            foreach (Header header in url.Headers)
            {
                switch (header.Name.ToUpperInvariant())
                {
                    case "USER-AGENT":
                        request.UserAgent = header.Value;
                        break;

                    default:
                        request.Headers.Add(header.Name, header.Value);
                        break;
                }
            }

            if (request.Method.Equals("GET", StringComparison.OrdinalIgnoreCase))
            {
                // do nothing, we'll just get a response right away
            }
            else if (request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase) || request.Method.Equals("PUT", StringComparison.OrdinalIgnoreCase))
            {
                byte[] bytes = (url.PostBytes == null ? Encoding.UTF8.GetBytes(url.PostText) : url.PostBytes);
                request.ContentLength = bytes.Length;
                using (Stream requestStream = request.GetRequestStream())
                {
                    requestStream.Write(bytes, 0, bytes.Length);
                }
            }

            HttpWebResponse response = request.GetResponse() as HttpWebResponse;
            Stream responseStream = response.GetResponseStream();
            MemoryStream ms = new MemoryStream();
            byte[] buffer = new byte[8192];
            int count;
            while ((count = responseStream.Read(buffer, 0, buffer.Length)) > 0)
            {
                ms.Write(buffer, 0, count);
            }
            string responseBody = Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length);
            TimeSpan duration = (DateTime.UtcNow - start);
            string[] headers = new string[response.Headers.Count];
            int i = 0;
            foreach (string key in response.Headers.AllKeys)
            {
                headers[i++] = key + ":" + response.Headers.Get(key);
            }
            byte[] md5 = new MD5CryptoServiceProvider().ComputeHash(ms.GetBuffer(), 0, (int)ms.Length);
            return new UrlResponse
            {
                MD51 = md5[0] | md5[1] << 8 | md5[2] << 16 | md5[3] << 24,
                MD52 = md5[4] | md5[5] << 8 | md5[6] << 16 | md5[7] << 24,
                Body = responseBody,
                Duration = duration,
                Headers = headers,
                StatusCode = response.StatusCode
            };
        }

        private void SendEmail(MonitoredUrl url, string emailBody)
        {
            SmtpClient client = new SmtpClient();
            client.Host = config.Email.Host;
            client.Port = config.Email.Port;
            client.UseDefaultCredentials = false;
            client.Credentials = new NetworkCredential(config.Email.UserName, config.Email.Password);
            client.EnableSsl = config.Email.Ssl;

            MailMessage msg = new MailMessage
            {
                Body = emailBody,
                BodyEncoding = Encoding.UTF8,
                From = new MailAddress(config.Email.UserName, config.Email.From),
                IsBodyHtml = true,
                Sender = new MailAddress(config.Email.UserName, config.Email.From),
                Subject = config.Email.Subject
            };

            foreach (string address in url.EmailAddresses.Split(',', ';', '|'))
            {
                string trimmedAddress = address.Trim();
                if (trimmedAddress.Length != 0)
                {
                    msg.To.Add(trimmedAddress);
                }
            }

            client.Send(msg);
        }

        private string CheckUrlResponse(UrlResponse response, MonitoredUrl url)
        {
            string message = string.Empty;

            if (!string.IsNullOrWhiteSpace(url.BodyRegex))
            {
                if (!Regex.IsMatch(response.Body, url.BodyRegex, RegexOptions.IgnoreCase | RegexOptions.Singleline))
                {
                    message += "- Failed to match body regex" + Environment.NewLine;
                }
            }
            if (!string.IsNullOrWhiteSpace(url.HeadersRegex))
            {
                bool foundOne = false;

                foreach (string header in response.Headers)
                {
                    if (Regex.IsMatch(header, url.HeadersRegex, RegexOptions.IgnoreCase | RegexOptions.Singleline))
                    {
                        foundOne = true;
                        break;
                    }
                }

                if (!foundOne)
                {
                    message += "- Failed to match headers regex" + Environment.NewLine;
                }
            }
            if (!string.IsNullOrWhiteSpace(url.StatusCodeRegex))
            {
                string statusCodeString = response.StatusCode.ToString("D");

                if (!Regex.IsMatch(statusCodeString, url.StatusCodeRegex, RegexOptions.IgnoreCase))
                {
                    message += "- Failed to match status code regex, got " + statusCodeString;
                }
            }
            if (url.AlertIfChanged && (url.MD51 != 0 || url.MD52 != 0) && (url.MD51 != response.MD51 || url.MD52 != response.MD52))
            {
                message += "- MD5 Changed, body contents are new" + Environment.NewLine;
            }
            if (url.MaxTime.TotalSeconds > 0.0d && response.Duration > url.MaxTime)
            {
                message += "- URL took " + response.Duration.TotalSeconds.ToString("0.00") + " seconds, too long";
            }
            url.MD51 = response.MD51;
            url.MD52 = response.MD52;

            return message;
        }

        private void ProcessUrl(object state)
        {
            MonitoredUrl url = state as MonitoredUrl;

            try
            {
                UrlResponse response = GetResponseForUrl(url);
                string emailBody = CheckUrlResponse(response, url);
                if (emailBody.Length != 0)
                {
                    SendEmail(url, emailBody);
                }
            }
            finally
            {
                url.InProcess = false;
                Interlocked.Decrement(ref threadCount);
            }        
        }

        private void ServiceThread()
        {
            while (run)
            {
                MonitoredUrl[] urls = config.UrlSet.Where(u => !u.InProcess && (DateTime.UtcNow - u.LastCheck) > u.Frequency).ToArray();
                foreach (MonitoredUrl url in urls)
                {
                    Interlocked.Increment(ref threadCount);
                    url.InProcess = true;
                    config.UrlSet.Remove(url);
                    url.LastCheck = DateTime.UtcNow;
                    config.UrlSet.Add(url);
                    ThreadPool.QueueUserWorkItem(new WaitCallback(ProcessUrl), url);

                    while (run && threadCount >= config.MaxThreads)
                    {
                        Thread.Sleep(20);
                    }

                    Thread.Sleep(config.SleepTimeUrl);
                }

                if (!run)
                {
                    break;
                }

                Thread.Sleep(config.SleepTimeBatch);
            }

            while (threadCount != 0)
            {
                Thread.Sleep(20);
            }
        }

        internal void Start(string[] args)
        {
            ReloadConfig();
            run = true;
            serviceThread = new Thread(new ThreadStart(ServiceThread));
            serviceThread.Start();
        }

        protected override void OnStart(string[] args)
        {
            base.OnStart(args);

            Start(args);
        }

        protected override void OnStop()
        {
            base.OnStop();

            run = false;
            serviceThread = null;
        }

        public void ReloadConfig()
        {
            UrlMonitorConfig _config = (UrlMonitorConfig)System.Configuration.ConfigurationManager.GetSection("UrlMonitorConfig");
            _config.UrlSet = new SortedSet<MonitoredUrl>();
            foreach (MonitoredUrl url in _config.Urls)
            {
                _config.UrlSet.Add(url);
            }
            config = _config;
            ThreadPool.SetMaxThreads(config.MaxThreads, config.MaxThreads * 2);
        }
    }
}
