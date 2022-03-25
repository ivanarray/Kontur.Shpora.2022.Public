using System.Threading;

namespace ReaderWriterLock;

public class CustomMutex: ICustomMutex
{
    private readonly object locker = new();
    private bool isLock;
    public void Lock()
    {
    Begin:
       lock(locker)
       {
           if (isLock)
           {
               Monitor.Wait(locker);
               goto Begin;
           }
           isLock = true;
       }
    }

    public void Release()
    {
        lock (locker)
        {
            isLock = false;
            Monitor.PulseAll(locker);
        }
    }
}