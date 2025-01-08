using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Collections.Concurrent;

using DatabaseAPI.Collections;

namespace DatabaseAPI.IO;

public class DatabaseTable : IDisposable
{
    private volatile string _name;
    
    private volatile DatabaseFile _file;
    private volatile ConcurrentDictionary<string, DatabaseCollectionData> _collections =
        new ConcurrentDictionary<string, DatabaseCollectionData>();

    public string Name => _name;
    
    public DatabaseFile File => _file;
    public DatabaseMonitor Monitor => _file.Monitor;

    public IReadOnlyDictionary<string, DatabaseCollectionData> Collections => _collections;

    internal DatabaseTable(string name, DatabaseFile file)
    {
        _name = name;
        _file = file;
    }

    public DatabaseCollection<T> GetCollection<T>(string collectionName) where T : DatabaseObject
    {
        if (string.IsNullOrWhiteSpace(collectionName))
            throw new ArgumentNullException(nameof(collectionName));
        
        if (!_collections.TryGetValue(collectionName, out var collectionData))
        {
            collectionData = new DatabaseCollectionData(collectionName, null, typeof(T), typeof(DatabaseCollection<>).MakeGenericType(typeof(T)));
            collectionData.Wrapper = new DatabaseCollection<T>();

            InitializeWrapper(collectionData.Wrapper, collectionData);
            return (DatabaseCollection<T>)collectionData.Wrapper;
        }
        else
        {
            return (DatabaseCollection<T>)collectionData.Wrapper;
        }
    }
    
    public void WriteSelf(BinaryWriter writer)
    {
        writer.Write(_collections.Count);

        foreach (var collection in _collections)
        {
            // uninitialized collection
            if (collection.Value.Type is null || collection.Value.Wrapper is null)
                continue;
            
            var nameBytes = Encoding.UTF32.GetBytes(collection.Key);

            writer.Write(nameBytes.Length);
            writer.Write(nameBytes);

            writer.Write(collection.Value.Type.AssemblyQualifiedName);

            collection.Value.Write(collection.Value.Wrapper.WriteSelf);

            writer.Write(collection.Value.Data.Length);
            writer.Write(collection.Value.Data);
        }
    }

    public void ReadSelf(BinaryReader reader, bool isUpdate)
    {
        var size = reader.ReadInt32();
        var found = new HashSet<string>(size);

        for (int i = 0; i < size; i++)
        {
            var nameLen = reader.ReadInt32();
            var nameBytes = reader.ReadBytes(nameLen);
            var nameValue = Encoding.UTF32.GetString(nameBytes);

            var typeName = reader.ReadString();
            var type = Type.GetType(typeName, true);

            var wrapperType = typeof(DatabaseCollection<>).MakeGenericType(type);
            
            var dataLen = reader.ReadInt32();
            var dataValue = reader.ReadBytes(dataLen);

            found.Add(nameValue);

            if (!_collections.TryGetValue(nameValue, out var collectionData))
            {
                collectionData = new DatabaseCollectionData(nameValue, dataValue, type, wrapperType);

                _collections.TryAdd(nameValue, collectionData);
            }

            if (isUpdate && collectionData.Wrapper != null)
            {
                InitializeWrapper(collectionData.Wrapper, collectionData);
                
                collectionData.Wrapper.ReadSelf(reader, true);
            }
            else
            {
                collectionData.Wrapper ??= Activator.CreateInstance(wrapperType) as DatabaseCollectionBase;

                InitializeWrapper(collectionData.Wrapper, collectionData);
                
                collectionData.Wrapper.ReadSelf(reader, false);
            }
        }

        foreach (var collection in _collections)
        {
            if (!found.Contains(collection.Key))
            {
                collection.Value.Dispose();

                _collections.TryRemove(collection.Key, out _);
            }
        }

        found.Clear();
    }

    public void Dispose()
    {
        foreach (var collection in _collections)
            collection.Value.Dispose();

        _collections.Clear();
        _collections = null;
    }

    private void InitializeWrapper(DatabaseCollectionBase wrapper, DatabaseCollectionData data)
    {
        wrapper.Data = data;
        wrapper.Name = data.Name;
        
        wrapper.Table = this;
    }
}