using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.Serialization.Json;
using System.Threading.Tasks;
using log4net;

namespace NMAP
{
    public class SequentialScanner : IPScanner
    {
        protected virtual ILog log => LogManager.GetLogger(typeof(SequentialScanner));

        public virtual Task Scan(IPAddress[] ipAddrs, int[] ports)
        {
            List<Task> tasks = ipAddrs.AsParallel()
                .Select(ipAddr => Task.Factory.StartNew(
                    () => { ProcessIpAddr(ports, ipAddr); }, TaskCreationOptions.AttachedToParent))
                .ToList();
            return Task.WhenAll(tasks);
        }

        private void ProcessIpAddr(int[] ports, IPAddress ipAddr)
        {
            Task.Run(() => PingAddr(ipAddr))
                .ContinueWith((res) =>
                {
                    if (res.Result != IPStatus.Success) return;
                    foreach (var port in ports)
                    {
                        Task.Factory.StartNew(() => { CheckPort(ipAddr, port); }, TaskCreationOptions.AttachedToParent);
                    }
                });
        }

        protected IPStatus PingAddr(IPAddress ipAddr, int timeout = 3000)
        {
            log.Info($"Pinging {ipAddr}");
            using (var ping = new Ping())
            {
                try
                {
                    var status = ping.Send(ipAddr, timeout).Status;
                    log.Info($"Pinged {ipAddr}: {status}");
                    return status;
                }
                catch
                {
                    return IPStatus.Unknown;
                }
            }
        }

        protected void CheckPort(IPAddress ipAddr, int port, int timeout = 3000)
        {
            using (var tcpClient = new TcpClient())
            {
                log.Info($"Checking {ipAddr}:{port}");

                var connectStatus = tcpClient.ConnectWithTimeout(ipAddr, port, timeout);
                PortStatus portStatus;
                switch (connectStatus)
                {
                    case TaskStatus.RanToCompletion:
                        portStatus = PortStatus.OPEN;
                        break;
                    case TaskStatus.Faulted:
                        portStatus = PortStatus.CLOSED;
                        break;
                    default:
                        portStatus = PortStatus.FILTERED;
                        break;
                }

                log.Info($"Checked {ipAddr}:{port} - {portStatus}");
            }
        }
    }
}