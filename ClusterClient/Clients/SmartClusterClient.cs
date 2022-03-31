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
        var sw = new Stopwatch();
        var mainSw = new Stopwatch();
        mainSw.Start();
        var t = timeout / ReplicaAddresses.Length;
        var tasksList = new List<Task<string>>(ReplicaAddresses.Length);

        for (var i = 0; i < ReplicaAddresses.Length; i++)
        {
            if (TryGetSuccessTask(tasksList, out var result)) return await result;

            var uri = ReplicaAddresses[i];
            var rst = CreateRequest($"{uri}?query={query}");
            var task = ProcessRequestAsync(rst);


            sw.Start();
            await Task.WhenAny(Task.Delay(t), task);
            sw.Stop();


            if (!task.IsCompleted || task.IsFaulted)
            {
                t = (timeout - mainSw.Elapsed) / Math.Max(1, ReplicaAddresses.Length - i - 1);
                if (!task.IsFaulted)
                    tasksList.Add(task);

                sw.Reset();
                continue;
            }

            return await task;
        }

        mainSw.Stop();

        var delta = timeout - mainSw.Elapsed;

        if (delta > TimeSpan.Zero)
            await Task.WhenAny(tasksList).WaitAsync(timeout - mainSw.Elapsed);

        if (TryGetSuccessTask(tasksList, out var r)) return await r;
        throw new TimeoutException();
    }

    protected override ILog Log => LogManager.GetLogger(typeof(SmartClusterClient));

    private bool TryGetSuccessTask<T>(List<Task<T>> tasks, out Task<T> task)
    {
        var success = tasks.Where(t => t.IsCompletedSuccessfully).ToArray();

        if (success.Any())
        {
            task = success[0];
            return true;
        }

        task = default;
        return false;
    }
}