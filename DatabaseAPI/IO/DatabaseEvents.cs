using System;

using DatabaseAPI.Collections;

namespace DatabaseAPI.IO;

public static class DatabaseEvents
{
    public static event Action<DatabaseCollectionBase> OnCleared;
    public static event Action<DatabaseCollectionBase> OnDisposed; 

    public static void InvokeOnCleared(DatabaseCollectionBase databaseCollectionBase)
        => OnCleared?.Invoke(databaseCollectionBase);

    public static void InvokeOnDisposed(DatabaseCollectionBase databaseCollectionBase)
        => OnDisposed?.Invoke(databaseCollectionBase);
}

public static class DatabaseEvents<T> where T : DatabaseObject
{
    public static event Action<DatabaseCollection<T>, T> OnItemAdded;
    public static event Action<DatabaseCollection<T>, T> OnItemRemoved;
    public static event Action<DatabaseCollection<T>, T> OnItemUpdated;  

    public static void InvokeOnItemAdded(DatabaseCollection<T> collection, T item)
        => OnItemAdded?.Invoke(collection, item);
    
    public static void InvokeOnItemRemoved(DatabaseCollection<T> collection, T item)
        => OnItemRemoved?.Invoke(collection, item);

    public static void InvokeOnItemUpdated(DatabaseCollection<T> collection, T item)
        => OnItemUpdated?.Invoke(collection, item);
}