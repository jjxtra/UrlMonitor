#region Imports

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Permissions;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Xml;
using System.Text.RegularExpressions;

#endregion Imports

namespace UrlMonitor
{
    /// <summary>
    /// Main app
    /// </summary>
    public class UrlMonitorApp
    {
        public static void RunService(string[] args)
        {
            System.ServiceProcess.ServiceBase[] ServicesToRun;
            ServicesToRun = new System.ServiceProcess.ServiceBase[] { new UrlMonitorService() };
            System.ServiceProcess.ServiceBase.Run(ServicesToRun);
        }

        public static void RunConsole(string[] args)
        {
            UrlMonitorService svc = new UrlMonitorService();
            svc.Start(args);
            Console.WriteLine("Press ENTER to quit");
            Console.ReadLine();
            svc.Stop();
        }

        public static void Main(string[] args)
        {
            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
            ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;

            if (args.Length != 0 && args[0] == "debug")
            {
                RunConsole(args);
            }
            else
            {
                RunService(args);
            }
        }
    }
}
