using System;
using System.Collections.Generic;
using DatabaseAPI.IO;
using DatabaseAPI.IO.Interfaces;

namespace DatabaseAPI.Collections;

public abstract class DatabaseCollectionBase : IDisposable
{
    private volatile string _name;
    private volatile int _idSize = 8;
    
    private volatile DatabaseTable _table;
    private volatile DatabaseCollectionData _data;
    
    public string Name
    {
        get => _name;
        internal set => _name = value;
    }

    public DatabaseTable Table
    {
        get => _table;
        internal set => _table = value;
    }

    public DatabaseCollectionData Data
    {
        get => _data;
        internal set => _data = value;
    }

    public string TableName => Table.Name;

    public IObjectReaderWriter ReaderWriter => Data.ReaderWriter;
    public IObjectManipulator Manipulator => Data.Manipulator;
    
    public DatabaseFile File => Table.File;
    public DatabaseMonitor Monitor => Table.Monitor;
    
    public int? GetObjectId(object obj) => Manipulator.GetObjectId(obj);
    

    public void SaveChanges() => Monitor.Register();
    
    public abstract void Dispose();
    public abstract void AddItems(IEnumerable<object> items);

    public abstract IEnumerator<object> EnumerateItems();

    internal abstract void ClearArray();

    internal abstract void MakeReady();
    internal abstract void MakeUnReady();
    
    internal abstract bool TryGetItem(int itemId, out object item);
    internal abstract void RemoveMissing(List<int> foundIds);
}