using System;
using System.Threading;

namespace ReaderWriterLock;

public class RwLock : IRwLock
{
    private readonly ICustomMutex mutex = new CustomMutex();

    private readonly object readerLock = new();

    private uint readersCount;

    public void ReadLocked(Action action)
    {
        lock (readerLock)
        {
            readersCount++;
            if (readersCount == 1)
            {
                mutex.Lock();
            }
        }

        action();

        lock (readerLock)
        {
            readersCount--;
            if (readersCount == 0)
            {
                mutex.Release();
            }
        }
    }
    
    public void WriteLocked(Action action)
    {
        mutex.Lock();
        action();
        mutex.Release();
    }
}