using System;
using System.Collections.Concurrent;

using DatabaseAPI.Collections;

namespace DatabaseAPI.IO;

public class DatabaseTable : IDisposable
{
    private volatile string name;
    
    private volatile DatabaseFile file;
    private volatile ConcurrentDictionary<string, DatabaseCollectionData> collections = new();

    public string Name => name;
    
    public DatabaseFile File => file;
    public DatabaseMonitor Monitor => file.Monitor;

    public ConcurrentDictionary<string, DatabaseCollectionData> Collections => collections;

    public DatabaseTable(string name, DatabaseFile file)
    {
        this.name = name;
        this.file = file;
    }

    public DatabaseCollection<T> GetCollection<T>(string collectionName) where T : class
    {
        if (string.IsNullOrWhiteSpace(collectionName)) throw new ArgumentNullException(nameof(collectionName));
        
        if (!collections.TryGetValue(collectionName, out var collectionData))
        {
            collectionData = CreateCollection(collectionName, typeof(T));
            
            collections.TryAdd(collectionName, collectionData);
            return (DatabaseCollection<T>)collectionData.Wrapper;
        }

        return (DatabaseCollection<T>)collectionData.Wrapper;
    }

    public DatabaseCollectionData GetCollectionData(string collectionName, Type objectType)
    {
        if (string.IsNullOrWhiteSpace(collectionName)) throw new ArgumentNullException(nameof(collectionName));
        if (objectType is null) throw new ArgumentNullException(nameof(objectType));
        
        if (!collections.TryGetValue(collectionName, out var collectionData))
        {
            collectionData = CreateCollection(collectionName, objectType);
            
            collections.TryAdd(collectionName, collectionData);
            return collectionData;
        }

        return collectionData;
    }

    public DatabaseCollectionData CreateCollection(string collectionName, Type objectType)
    {
        if (string.IsNullOrWhiteSpace(collectionName)) throw new ArgumentNullException(nameof(collectionName));
        if (objectType == null) throw new ArgumentNullException(nameof(objectType));
        
        if (!DatabaseFile.TryGetReaderWriter(objectType, out var readerWriter)) throw new Exception($"Missing object reader for type {objectType.FullName}");
        if (!DatabaseFile.TryGetManipulator(objectType, out var manipulator)) throw new Exception($"Missing object manipulator for type {objectType.FullName}");
        
        var data = new DatabaseCollectionData(collectionName, this, objectType, typeof(DatabaseCollection<>).MakeGenericType(objectType));

        data.ReaderWriter = readerWriter;
        data.Manipulator = manipulator;
        
        data.SetNotReady();
        return data;
    }

    public void Dispose()
    {
        if (collections != null)
        {
            foreach (var collection in collections) collection.Value.Dispose();

            collections.Clear();
            collections = null;
        }
    }
}