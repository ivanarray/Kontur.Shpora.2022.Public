using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using log4net;

namespace NMAP
{
    public class AsyncScanner : IPScanner
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(AsyncScanner));

        public async Task Scan(IPAddress[] ipAddrs, int[] ports)
        {
            await Task.WhenAll(ipAddrs.Select(
                ipAddr => ProcessIpAddr(ports, ipAddr)
            ));
        }

        private async Task ProcessIpAddr(int[] ports, IPAddress ipAddr)
        {
            var result = await  PingAddr(ipAddr);

            if (result == IPStatus.Success) return;

            await Task.WhenAll(
                ports.Select(
                    port => CheckPort(ipAddr, port)
                ));
        }

        protected async Task<IPStatus> PingAddr(IPAddress ipAddr, int timeout = 3000)
        {
            Log.Info($"Pinging {ipAddr}");
            using var ping = new Ping();
            try
            {
                var status = await ping.SendPingAsync(ipAddr, timeout);
                Log.Info($"Pinged {ipAddr}: {status}");
                return status.Status;
            }
            catch
            {
                return IPStatus.Unknown;
            }
        }

        protected async Task CheckPort(IPAddress ipAddr, int port, int timeout = 3000)
        {
            using (var tcpClient = new TcpClient())
            {
                Log.Info($"Checking {ipAddr}:{port}");

                var connectStatus = await tcpClient.ConnectWithTimeoutAsync(ipAddr, port, timeout);
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

                Log.Info($"Checked {ipAddr}:{port} - {portStatus}");
            }
        }
    }
}