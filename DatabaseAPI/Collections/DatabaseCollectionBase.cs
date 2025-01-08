using System;
using System.Collections.Concurrent;
using System.IO;
using DatabaseAPI.IO;

namespace DatabaseAPI.Collections;

public class DatabaseCollectionBase : IDisposable
{
    private volatile string _name;
    private volatile int _idSize = 8;
    
    private volatile DatabaseTable _table;
    private volatile DatabaseCollectionData _data;

    public int IdLength
    {
        get
        {
            if (_idSize < 1)
                _idSize = 8; // default size

            return _idSize;
        }
        set
        {
            if (value < 1)
                throw new ArgumentOutOfRangeException(nameof(value));

            _idSize = value;
        }
    }
    
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

    public DatabaseFile File => Table.File;
    public DatabaseMonitor Monitor => Table.Monitor;
    
    public virtual void Dispose() { }

    public void SaveChanges()
        => Monitor.Register(false);
    
    internal virtual void ReadSelf(BinaryReader reader, bool isUpdate) { }
    internal virtual void WriteSelf(BinaryWriter writer) { }
}