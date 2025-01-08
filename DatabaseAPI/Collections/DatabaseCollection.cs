using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;

namespace DatabaseAPI.Collections;

using IO;

public class DatabaseCollection<T> : DatabaseCollectionBase,
    
    IEnumerable<T>

    where T : DatabaseObject
{
    private static volatile Func<T> _constructor = Activator.CreateInstance<T>;

    public static Func<T> Constructor
    {
        get => _constructor;
        set => _constructor = value;
    }
    
    private volatile ConcurrentDictionary<string, T> _idMap = new ConcurrentDictionary<string, T>();
    private volatile bool _isDisposed = false;

    public int Size => _idMap.Count;

    public bool IsDisposed => _isDisposed;
    
    public List<T> Find(Func<T, bool> predicate)
    {
        if (_isDisposed)
            throw new ObjectDisposedException(typeof(DatabaseCollection<T>).FullName);
        
        if (predicate is null)
            throw new ArgumentNullException(nameof(predicate));
        
        var list = new List<T>();

        foreach (var pair in _idMap)
        {
            if (!predicate(pair.Value)) continue;
            list.Add(pair.Value);
        }

        return list;
    }
    
    public bool TryFindNonAlloc(Func<T, bool> predicate, ICollection<T> target)
    {        
        if (_isDisposed)
            throw new ObjectDisposedException(typeof(DatabaseCollection<T>).FullName);

        if (predicate is null)
            throw new ArgumentNullException(nameof(predicate));

        foreach (var pair in _idMap)
        {
            if (!predicate(pair.Value)) continue;
            target.Add(pair.Value);
        }

        return target.Count > 0;
    }

    public bool TryFind(Func<T, bool> predicate, out List<T> items)
    {        
        if (_isDisposed)
            throw new ObjectDisposedException(typeof(DatabaseCollection<T>).FullName);

        if (predicate is null)
            throw new ArgumentException(nameof(predicate));

        items = new List<T>();

        foreach (var pair in _idMap)
        {
            if (!predicate(pair.Value)) continue;
            items.Add(pair.Value);
        }

        return items.Count > 0;
    }
    
    public T Get(string id)
    {        
        if (_isDisposed)
            throw new ObjectDisposedException(typeof(DatabaseCollection<T>).FullName);

        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentNullException(nameof(id));

        if (!_idMap.TryGetValue(id, out var item))
            throw new KeyNotFoundException($"No item with ID {id} was present");

        return item;
    }

    public T Get(Func<T, bool> predicate)
    {        
        if (_isDisposed)
            throw new ObjectDisposedException(typeof(DatabaseCollection<T>).FullName);

        if (predicate is null)
            throw new ArgumentNullException(nameof(predicate));

        foreach (var pair in _idMap)
        {
            if (!predicate(pair.Value)) continue;
            return pair.Value;
        }

        throw new Exception("Could not find a matching item");
    }
    
    public T GetOrAdd(Func<T, bool> predicate, Func<T> constructor)
    {        
        if (_isDisposed)
            throw new ObjectDisposedException(typeof(DatabaseCollection<T>).FullName);

        if (predicate is null)
            throw new ArgumentNullException(nameof(predicate));
        
        if (constructor is null)
            throw new ArgumentNullException(nameof(constructor));
        
        if (TryGet(predicate, out var item))
            return item;

        item = constructor();
        item.SetId(Data.GenerateObjectId(IdLength));

        _idMap.TryAdd(item.Id, item);

        DatabaseEvents<T>.InvokeOnItemAdded(this, item);

        Monitor.Register(false);
        return item;
    }

    public bool TryUpdateOrAdd(Func<T, bool> predicate, Action<T> update, Func<T> constructor)
    {
        if (_isDisposed)
            throw new ObjectDisposedException(typeof(DatabaseCollection<T>).FullName);

        if (predicate is null)
            throw new ArgumentNullException(nameof(predicate));

        if (update is null)
            throw new ArgumentNullException(nameof(update));

        if (constructor is null)
            throw new ArgumentNullException(nameof(constructor));

        if (TryGet(predicate, out var item))
        {
            update(item);

            DatabaseEvents<T>.InvokeOnItemUpdated(this, item);

            Monitor.Register(false);
            return true;
        }
        else
        {
            item = constructor();
            item.SetId(Data.GenerateObjectId(IdLength));

            _idMap.TryAdd(item.Id, item);

            DatabaseEvents<T>.InvokeOnItemAdded(this, item);

            Monitor.Register(false);
            return true;
        }
    }

    public bool TryUpdate(Func<T, bool> predicate, Action<T> update)
    {
        if (_isDisposed)
            throw new ObjectDisposedException(typeof(DatabaseCollection<T>).FullName);

        if (predicate is null)
            throw new ArgumentNullException(nameof(predicate));

        if (update is null)
            throw new ArgumentNullException(nameof(update));

        if (!TryGet(predicate, out var item))
            return false;

        update(item);
        
        DatabaseEvents<T>.InvokeOnItemUpdated(this, item);

        Monitor.Register(false);
        return true;
    }

    public bool TryUpdate(string id, Action<T> update)
    {        
        if (_isDisposed)
            throw new ObjectDisposedException(typeof(DatabaseCollection<T>).FullName);

        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentNullException(nameof(id));

        if (update is null)
            throw new ArgumentNullException(nameof(update));

        if (!_idMap.TryGetValue(id, out var item))
            return false;

        update(item);
        
        DatabaseEvents<T>.InvokeOnItemUpdated(this, item);

        Monitor.Register(false);
        return true;
    }
    
    public bool TryGet(Func<T, bool> predicate, out T item)
    {        
        if (_isDisposed)
            throw new ObjectDisposedException(typeof(DatabaseCollection<T>).FullName);

        if (predicate is null)
            throw new ArgumentNullException(nameof(predicate));

        foreach (var pair in _idMap)
        {
            if (!predicate(pair.Value)) continue;

            item = pair.Value;
            return true;
        }

        item = default;
        return false;
    }
    
    public bool TryGet(string id, out T item)
    {        
        if (_isDisposed)
            throw new ObjectDisposedException(typeof(DatabaseCollection<T>).FullName);

        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentNullException(nameof(id));

        return _idMap.TryGetValue(id, out item);
    }
    
    public bool TryAdd(T item)
    {        
        if (_isDisposed)
            throw new ObjectDisposedException(typeof(DatabaseCollection<T>).FullName);

        if (item is null)
            throw new ArgumentNullException(nameof(item));

        if (!string.IsNullOrWhiteSpace(item.Id))
        {
            if (_idMap.ContainsKey(item.Id))
            {
                return false;
            }
        }
        else
        {
            item.SetId(Data.GenerateObjectId(IdLength));
        }

        _idMap.TryAdd(item.Id, item);

        DatabaseEvents<T>.InvokeOnItemAdded(this, item);

        Monitor.Register(false);
        return true;
    }

    public bool TrySet(T item, string id)
    {        
        if (_isDisposed)
            throw new ObjectDisposedException(typeof(DatabaseCollection<T>).FullName);

        if (item is null)
            throw new ArgumentNullException(nameof(item));

        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentNullException(nameof(id));

        if (!string.IsNullOrWhiteSpace(item.Id))
        {
            if (_idMap.TryRemove(item.Id, out _))
                DatabaseEvents<T>.InvokeOnItemRemoved(this, item);
            else
                Data.RemoveObjectId(item.Id);
        }

        item.SetId(id);

        Data.OccupyId(id);

        if (_idMap.TryRemove(id, out var curItem))
        {
            DatabaseEvents<T>.InvokeOnItemRemoved(this, curItem);
            
            curItem.SetId(string.Empty);
        }

        _idMap.TryAdd(id, item);

        DatabaseEvents<T>.InvokeOnItemAdded(this, item);

        Monitor.Register(false);
        return true;
    }

    public bool TryRemove(T item)
    {        
        if (_isDisposed)
            throw new ObjectDisposedException(typeof(DatabaseCollection<T>).FullName);

        if (item is null)
            throw new ArgumentNullException(nameof(item));

        if (string.IsNullOrWhiteSpace(item.Id))
            return false;

        if (!_idMap.TryRemove(item.Id, out _))
            return false;

        DatabaseEvents<T>.InvokeOnItemRemoved(this, item);

        Data.RemoveObjectId(item.Id);
        
        item.SetId(string.Empty);
        
        Monitor.Register(false);
        return true;
    }
    
    public bool TryRemove(string id)
    {        
        if (_isDisposed)
            throw new ObjectDisposedException(typeof(DatabaseCollection<T>).FullName);

        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentNullException(nameof(id));

        if (!_idMap.TryRemove(id, out var item))
            return false;

        DatabaseEvents<T>.InvokeOnItemRemoved(this, item);

        Data.RemoveObjectId(id);
        
        item.SetId(string.Empty);

        Monitor.Register(false);
        return true;
    }
    
    public int Remove(Func<T, bool> predicate)
    {        
        if (_isDisposed)
            throw new ObjectDisposedException(typeof(DatabaseCollection<T>).FullName);

        if (predicate is null)
            throw new ArgumentNullException(nameof(predicate));

        var count = 0;

        foreach (var pair in _idMap)
        {
            if (!predicate(pair.Value)) continue;
            if (!_idMap.TryRemove(pair.Key, out _)) continue;

            DatabaseEvents<T>.InvokeOnItemRemoved(this, pair.Value);

            Data.RemoveObjectId(pair.Key);
            
            pair.Value.SetId(string.Empty);
            
            count++;
        }

        if (count > 0)
            Monitor.Register(false);

        return count;
    }

    public void Clear()
    {        
        if (_isDisposed)
            throw new ObjectDisposedException(typeof(DatabaseCollection<T>).FullName);

        foreach (var pair in _idMap)
        {
            DatabaseEvents<T>.InvokeOnItemRemoved(this, pair.Value);

            Data.RemoveObjectId(pair.Key);

            pair.Value.SetId(string.Empty);
        }
        
        _idMap.Clear();

        DatabaseEvents.InvokeOnCleared(this);

        Monitor.Register(true);
    }

    public int Count(Func<T, bool> predicate)
    {        
        if (_isDisposed)
            throw new ObjectDisposedException(typeof(DatabaseCollection<T>).FullName);

        if (predicate is null)
            throw new ArgumentNullException(nameof(predicate));

        var count = 0;

        foreach (var pair in _idMap)
        {
            if (!predicate(pair.Value)) continue;
            count++;
        }

        return count;
    }

    public List<T> GetItems()
    {        
        if (_isDisposed)
            throw new ObjectDisposedException(typeof(DatabaseCollection<T>).FullName);

        var list = new List<T>(_idMap.Count);

        foreach (var pair in _idMap)
            list.Add(pair.Value);

        return list;
    }

    public bool GetItemsNonAlloc(ICollection<T> target)
    {        
        if (_isDisposed)
            throw new ObjectDisposedException(typeof(DatabaseCollection<T>).FullName);

        if (target is null)
            throw new ArgumentNullException(nameof(target));

        foreach (var pair in _idMap)
            target.Add(pair.Value);

        return target.Count > 0;
    }

    public void SaveChanges(Action action)
    {
        if (action is null)
            throw new ArgumentNullException(nameof(action));

        action();

        Monitor.Register(false);
    }
    
    public IEnumerator<T> GetEnumerator()
    {        
        if (_isDisposed)
            throw new ObjectDisposedException(typeof(DatabaseCollection<T>).FullName);

        foreach (var pair in _idMap)
            yield return pair.Value;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public override void Dispose()
    {        
        if (_isDisposed)
            throw new ObjectDisposedException(typeof(DatabaseCollection<T>).FullName);

        base.Dispose();

        _idMap?.Clear();
        _idMap = null;

        _isDisposed = true;

        DatabaseEvents.InvokeOnDisposed(this);
    }

    internal override void ReadSelf(BinaryReader reader, bool isUpdate)
    {        
        if (_isDisposed)
            throw new ObjectDisposedException(typeof(DatabaseCollection<T>).FullName);

        base.ReadSelf(reader, isUpdate);

        var size = reader.ReadInt32();
        var found = new HashSet<string>();

        if (!isUpdate)
            _idMap.Clear();

        for (int i = 0; i < size; i++)
        {
            var instanceId = reader.ReadString();

            found.Add(instanceId);
            
            if (isUpdate && _idMap.TryGetValue(instanceId, out var existingItem))
            {
                existingItem.Read(reader);

                DatabaseEvents<T>.InvokeOnItemUpdated(this, existingItem);
            }
            else
            {
                existingItem = Constructor();
                existingItem.SetId(instanceId);

                Data.OccupyId(instanceId);

                _idMap.TryAdd(instanceId, existingItem);
            }
        }

        var anyMissing = false;
        
        foreach (var pair in _idMap)
        {
            if (!found.Contains(pair.Key) && _idMap.TryRemove(pair.Key, out _))
            {
                DatabaseEvents<T>.InvokeOnItemRemoved(this, pair.Value);
                
                Data.RemoveObjectId(pair.Key);

                pair.Value.SetId(string.Empty);

                anyMissing = true;
            }
        }

        found.Clear();

        if (anyMissing)
            Monitor.Register(false);
    }

    internal override void WriteSelf(BinaryWriter writer)
    {        
        if (_isDisposed)
            throw new ObjectDisposedException(typeof(DatabaseCollection<T>).FullName);

        base.WriteSelf(writer);

        writer.Write(_idMap.Count);

        foreach (var pair in _idMap)
        {
            writer.Write(pair.Key);

            pair.Value.Write(writer);
        }
    }
}