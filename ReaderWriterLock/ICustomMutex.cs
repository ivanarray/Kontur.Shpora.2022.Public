namespace ReaderWriterLock;

public interface ICustomMutex
{
    void Lock();
    void Release();
}