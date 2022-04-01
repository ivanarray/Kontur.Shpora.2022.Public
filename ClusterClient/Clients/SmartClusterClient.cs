using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClusterClient.Clients;
using log4net;

namespace ClusterTests;

public class SmartClusterClient : ClusterClientBase
{
    public SmartClusterClient(string[] replicaAddresses)
        : base(replicaAddresses)
    {
    }

    public override async Task<string> ProcessRequestAsync(string query, TimeSpan timeout)
    {
        var localTimer = new Stopwatch();
        var commonTimer = new Stopwatch();

        commonTimer.Start();
        
        var t = timeout / ReplicaAddresses.Length;
        var tasksWaitingSet = new HashSet<Task<string>>(ReplicaAddresses.Length);

        for (var i = 0; i < ReplicaAddresses.Length; i++)
        {
            var task = ProcessRequestAsync(CreateRequest($"{ReplicaAddresses[i]}?query={query}"));
            
            tasksWaitingSet.Add(task);

            localTimer.Start();
            await Task.WhenAny(Task.Delay(t), Task.WhenAny(tasksWaitingSet));
            localTimer.Stop();

            if (task.IsCompleted && !task.IsFaulted) return await task;
            
            t = (timeout - commonTimer.Elapsed) / Math.Max(1, ReplicaAddresses.Length - i - 1);
            
            if (task.IsFaulted)
                tasksWaitingSet.Remove(task);

            localTimer.Reset();
        }
        commonTimer.Stop();

        var delta = timeout - commonTimer.Elapsed;

        if (delta > TimeSpan.Zero)
            await Task.WhenAny(tasksWaitingSet).WaitAsync(timeout - commonTimer.Elapsed);

        if (TryGetSuccessTask(tasksWaitingSet, out var r)) return await r;

        throw new TimeoutException();
    }

    protected override ILog Log => LogManager.GetLogger(typeof(SmartClusterClient));

    private bool TryGetSuccessTask<T>(HashSet<Task<T>> tasks, out Task<T> task)
    {
        var success = tasks.Where(t => t.IsCompleted).ToArray();

        if (success.Any())
        {
            task = success[0];
            return true;
        }

        task = default;
        return false;
    }
}