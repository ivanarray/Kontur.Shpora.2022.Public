using System;
using System.Linq;
using System.Threading.Tasks;
using ClusterClient.Clients;
using log4net;

namespace ClusterTests;

public class ParallelClusterClient : ClusterClientBase
{
    public ParallelClusterClient(string[] replicaAddresses)
        : base(replicaAddresses)
    {
    }

    public override async Task<string> ProcessRequestAsync(string query, TimeSpan timeout)
    {
        var tasks = ReplicaAddresses.Select(uri =>
        {
            var rst = CreateRequest($"{uri}?query={query}");
            return ProcessRequestAsync(rst);
        }).ToHashSet();

        while (true)
        {
            await Task.WhenAny(tasks).WaitAsync(timeout);
            var res = tasks.First(t => t.IsCompleted);
            tasks.Remove(res);
            if (!(res.IsFaulted && tasks.Count > 0)) return await res;
        }
    }

    protected override ILog Log => LogManager.GetLogger(typeof(ParallelClusterClient));
}