using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ClusterClient.Clients;
using log4net;

namespace ClusterTests;

public class RoundRobinClusterClient : ClusterClientBase
{
    public RoundRobinClusterClient(string[] replicaAddresses)
        : base(replicaAddresses)
    {
    }

    public override async Task<string> ProcessRequestAsync(string query, TimeSpan timeout)
    {
        var sw = new Stopwatch();
        var t = timeout / ReplicaAddresses.Length;
        for (var i = 0; i < ReplicaAddresses.Length; i++)
        {
            var uri = ReplicaAddresses[i];
            var rst = CreateRequest($"{uri}?query={query}");
            var task = ProcessRequestAsync(rst);


            sw.Start();
            await Task.WhenAny(Task.Delay(t), task);
            sw.Stop();


            if (!task.IsCompleted || task.IsFaulted)
            {
                if (task.IsFaulted)
                {
                    t += TimeSpan.FromMilliseconds((t.TotalMilliseconds - sw.ElapsedMilliseconds) /
                                                   (ReplicaAddresses.Length - i - 1));
                }
                sw.Reset();
                continue;
            }

            return await task;
        }

        throw new TimeoutException();
    }


    protected override ILog Log => LogManager.GetLogger(typeof(RoundRobinClusterClient));
}