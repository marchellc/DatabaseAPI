using System;
using System.IO;
using System.Linq;
using System.Collections.Concurrent;
using System.Threading;
using DatabaseAPI.Extensions;
using DatabaseAPI.IO;
using DatabaseAPI.IO.Interfaces;

namespace DatabaseAPI.Collections;

public class DatabaseCollectionData : IDisposable
{
    private volatile string name;
    
    private volatile int id;
    private volatile int size;

    private volatile IObjectReaderWriter readerWriter;
    private volatile IObjectManipulator manipulator;

    private volatile Type type;
    private volatile Type wrapperType;

    private volatile DatabaseTable table;
    private volatile DatabaseCollectionBase wrapper;
    
    private volatile ConcurrentQueue<int> idQueue = new();
    
    public string Name => name;

    public Type Type => type;
    public Type WrapperType => wrapperType;

    public IObjectReaderWriter ReaderWriter
    {
        get => readerWriter;
        set => readerWriter = value;
    }

    public IObjectManipulator Manipulator
    {
        get => manipulator;
        set => manipulator = value;
    }

    public int Size
    {
        get => size;
        set => size = value;
    }

    public int IdClock
    {
        get => id;
        set => id = value;
    }

    public int NextObjectId
    {
        get
        {
            if (idQueue.TryDequeue(out var nextId)) 
                return nextId;
            
            return Interlocked.Increment(ref id);
        }
    }

    public DatabaseCollectionBase Wrapper => wrapper;
    public DatabaseTable Table => table;
    
    public ConcurrentQueue<int> IdQueue => idQueue;

    public DatabaseCollectionData(string name, DatabaseTable table, Type type, Type wrapperType)
    {
        if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
        
        if (type is null) throw new ArgumentNullException(nameof(type));
        if (table is null) throw new ArgumentNullException(nameof(table));
        if (wrapperType is null) throw new ArgumentNullException(nameof(wrapperType));
        
        this.name = name;
        this.type = type;
        this.table = table;
        
        this.wrapperType = wrapperType;
        this.wrapper = Activator.CreateInstance(wrapperType) as DatabaseCollectionBase;

        wrapper.Data = this;
        wrapper.Name = name;
    }
    
    public void IncrementSize(int amount) => Interlocked.Add(ref size, amount);
    public void DecrementSize(int amount) => Interlocked.Exchange(ref size, size - amount);

    public void SetReady() => Wrapper.MakeReady();
    public void SetNotReady() => Wrapper.MakeUnReady();

    public void Dispose()
    {
        name = null;

        wrapper?.Dispose();
        wrapper = null;

        if (idQueue != null)
        {
            while (idQueue.TryDequeue(out _)) 
                continue;
            
            idQueue = null;
        }
    }
}