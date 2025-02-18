using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DatabaseAPI.Extensions;
using DatabaseAPI.IO.Interfaces;
using DatabaseAPI.Logging;

namespace DatabaseAPI.Collections;

using IO;

public class DatabaseCollection<T> : DatabaseCollectionBase, IEnumerable<T> where T : class
{
    private static volatile Func<T> constructor = Activator.CreateInstance<T>;

    public static Func<T> Constructor
    {
        get => constructor;
        set => constructor = value;
    }

    private volatile T[] array;
    
    private volatile bool isDisposed = false;
    private volatile bool isReady = false;

    public int Size => Data.Size;
    public int Multiplier => File.ArrayResizeMultiplier;

    public bool IsDisposed => isDisposed;
    public bool IsReady => isReady;

    public List<T> Where(Func<T, bool> predicate)
    {
        if (!isReady) throw new Exception("Collection cannot be accessed while not ready");
        if (isDisposed) throw new ObjectDisposedException("Collection is disposed.");
        
        if (predicate is null) throw new ArgumentNullException(nameof(predicate));

        var list = new List<T>();
        
        for (int i = 0; i < array.Length; i++)
        {
            if (!array.TryGet(i, out var item)) continue;
            if (!predicate(item)) continue;
            
            list.Add(item);
        }
        
        return list;
    }

    public T Get(Func<T, bool> predicate)
    {        
        if (!isReady) throw new Exception("Collection cannot be accessed while not ready");
        if (isDisposed) throw new ObjectDisposedException("Collection is disposed.");
        
        if (predicate is null) throw new ArgumentNullException(nameof(predicate));
        
        for (int i = 0; i < array.Length; i++)
        {
            if (!array.TryGet(i, out var heldItem)) continue;
            if (!predicate(heldItem)) continue;

            return heldItem;
        }

        throw new Exception("Item could not be found");
    }

    public T GetOrAdd(Func<T, bool> predicate, out bool isNew, Action<T> newSetup = null)
    {
        if (!isReady) throw new Exception("Collection cannot be accessed while not ready");
        if (isDisposed) throw new ObjectDisposedException("Collection is disposed.");

        if (predicate is null) throw new ArgumentNullException(nameof(predicate));

        isNew = false;

        for (int i = 0; i < array.Length; i++)
        {
            if (!array.TryGet(i, out var heldItem)) continue;
            if (!predicate(heldItem)) continue;

            return heldItem;
        }

        var newItem = Constructor();
        var newId = Data.NextObjectId;

        isNew = true;

        CheckArray(newId + 1);

        array[GetNewIndex()] = newItem;

        Manipulator.SetObjectId(newItem, newId);
        Data.IncrementSize(1);

        try
        {
            newSetup?.Invoke(newItem);
        }
        catch (Exception ex)
        {
            Log.Error("Database Collection", ex);
        }

        Monitor.Register();
        return newItem;
    }

    public void UpdateOrAdd(Func<T, bool> predicate, Func<T, bool, bool> update)
    {
        if (!isReady) throw new Exception("Collection cannot be accessed while not ready");
        if (isDisposed) throw new ObjectDisposedException("Collection is disposed.");
        
        if (predicate is null) throw new ArgumentNullException(nameof(predicate));
        if (update is null) throw new ArgumentNullException(nameof(update));

        if (TryGet(predicate, out var item))
        {
            if (!update(item, true)) 
                return;
            
            Monitor.Register();
            return;
        }
        
        var newItem = Constructor();
        var newId = Data.NextObjectId;
        
        Manipulator.SetObjectId(newItem, newId);

        CheckArray(array.Length + 1);
        
        array[GetNewIndex()] = newItem;
        
        Data.IncrementSize(1);
        
        try
        {
            update(newItem, false);
        }
        catch (Exception ex)
        {
            Log.Error("Database Collection", ex);
        }
        
        Monitor.Register();
    }

    public void TryGetBatched(Func<T, bool> predicate, Action<T, bool> onCompleted)
    {
        if (!isReady) throw new Exception("Collection cannot be accessed while not ready");
        if (isDisposed) throw new ObjectDisposedException("Collection is disposed.");
        
        if (predicate is null) throw new ArgumentNullException(nameof(predicate));
        if (onCompleted is null) throw new ArgumentNullException(nameof(onCompleted));

        if (File.BatchSize < 1 || Data.Size < File.BatchSize)
        {
            var hasFound = TryGet(predicate, out var foundItem);

            onCompleted(foundItem, hasFound);
        }
        else
        {
            var batchCount = Data.Size / File.BatchSize;
            var batchIndex = 0;
            var batchStatus = false;
            
            void BatchWorker(int batchStartIndex, int batchSize)
            {
                for (int i = 0; i < batchSize; i++)
                {
                    if (batchStatus || batchStartIndex >= array.Length) 
                        break;
                    
                    if (!array.TryGet(batchStartIndex++, out var heldItem)) continue;
                    if (!predicate(heldItem)) continue;

                    batchStatus = true;
                    
                    ThreadUtils.RunOnMain(() => onCompleted(heldItem, true));
                    break;
                }
            }
            
            for (int i = 0; i < batchCount; i++)
            {
                Task.Run(() => { BatchWorker(batchIndex, File.BatchSize); });

                if (batchStatus) 
                    break;
                
                batchIndex += File.BatchSize - 1;
            }
        }
    }

    public bool TryGet(Func<T, bool> predicate, out T item)
    {
        if (!isReady) throw new Exception("Collection cannot be accessed while not ready");
        if (isDisposed) throw new ObjectDisposedException("Collection is disposed.");
        
        if (predicate is null) throw new ArgumentNullException(nameof(predicate));
        
        for (int i = 0; i < array.Length; i++)
        {
            if (!array.TryGet(i, out var heldItem)) continue;
            if (!predicate(heldItem)) continue;
            
            item = heldItem;
            return true;
        }
        
        item = null;
        return false;
    }

    public bool TryGet(int itemId, out T item)
    {
        if (!isReady) throw new Exception("Collection cannot be accessed while not ready");
        if (isDisposed) throw new ObjectDisposedException("Collection is disposed.");
        
        if (itemId < 0) throw new ArgumentException("ItemId cannot be negative.");
        
        item = null;

        for (int i = 0; i < array.Length; i++)
        {
            if (!array.TryGet(i, out var heldItem)) 
                continue;

            var heldId = Manipulator.GetObjectId(heldItem);
            
            if (!heldId.HasValue || heldId.Value != itemId) 
                continue;
            
            item = heldItem;
            return true;
        }

        return false;
    }
    
    public int TryRemoveWhere(Func<T, bool> predicate)
    {
        if (!isReady) throw new Exception("Collection cannot be accessed while not ready");
        if (isDisposed) throw new ObjectDisposedException("Collection is disposed.");
        if (predicate is null) throw new ArgumentNullException(nameof(predicate));
        
        var removedCount = 0;

        for (int i = 0; i < array.Length; i++)
        {
            if (!array.TryGet(i, out var heldItem)) continue;
            if (!predicate(heldItem)) continue;

            array[i] = null;
            
            if (Manipulator.RemoveObjectId(heldItem, out var id)) 
                Data.IdQueue.Enqueue(id);

            removedCount++;
        }

        if (removedCount > 0)
        {
            Data.DecrementSize(removedCount);
            Monitor.Register();
        }
        
        return removedCount;
    }

    public int TryRemoveRange(IEnumerable<T> items)
    {
        if (!isReady) throw new Exception("Collection cannot be accessed while not ready");
        if (isDisposed) throw new ObjectDisposedException("Collection is disposed.");
        if (items is null) throw new ArgumentNullException(nameof(items));

        var count = items.Count();
        var removedCount = 0;
        
        if (count < 1) return 0;

        for (int i = 0; i < array.Length; i++)
        {
            if (!array.TryGet(i, out var item)) continue;
            if (!items.Contains(item)) continue;

            array[i] = null;
            
            if (Manipulator.RemoveObjectId(item, out var id)) 
                Data.IdQueue.Enqueue(id);

            removedCount++;
        }

        if (removedCount > 0)
        {
            Data.DecrementSize(removedCount);
            Monitor.Register();
        }
        
        return removedCount;
    }

    public bool TryRemove(T item)
    {
        if (!isReady) throw new Exception("Collection cannot be accessed while not ready");
        if (isDisposed) throw new ObjectDisposedException("Collection is disposed.");
        
        if (item is null) throw new ArgumentNullException(nameof(item));

        for (int i = 0; i < array.Length; i++)
        {
            var indexItem = array[i];
            
            if (indexItem is null) continue;
            if (!indexItem.Equals(item)) continue;

            array[i] = null;
            
            if (Manipulator.RemoveObjectId(indexItem, out var id)) 
                Data.IdQueue.Enqueue(id);

            return true;
        }

        return false;
    }

    public int TryAddRange(IEnumerable<T> items)
    {
        if (!isReady) throw new Exception("Collection cannot be accessed while not ready");
        if (isDisposed) throw new ObjectDisposedException("Collection is disposed.");
        if (items is null) throw new ArgumentNullException(nameof(items));

        var count = items.Count();
        var addedCount = 0;
        
        if (count < 1) return 0;

        var indexes = GetIndexes(count);
        var index = 0;
        
        CheckArray(array.Length + count);

        foreach (var item in items)
        {
            Manipulator.SetObjectId(item, Data.NextObjectId);
            
            array[indexes[index++]] = item;
            
            addedCount++;
        }
        
        PoolUtils<int>.Return(indexes);

        if (addedCount > 0)
        {
            Data.IncrementSize(addedCount);
            Monitor.Register();
        }
        
        return addedCount;
    }

    public bool TryAdd(T item)
    {
        if (!isReady) throw new Exception("Collection cannot be accessed while not ready");
        if (isDisposed) throw new ObjectDisposedException("Collection is disposed.");
        if (item is null) throw new ArgumentNullException(nameof(item));
        
        Manipulator.SetObjectId(item, Data.NextObjectId);
        
        CheckArray(array.Length + 1);
        
        array[GetNewIndex()] = item;
        
        Data.IncrementSize(1);
        
        Monitor.Register();
        return true;
    }

    #region IEnumerable implementation
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    
    public IEnumerator<T> GetEnumerator()
    {
        if (!isReady) throw new Exception("Collection cannot be accessed while not ready");
        if (isDisposed) throw new ObjectDisposedException("Collection is disposed.");
        
        for (int i = 0; i < array.Length; i++)
        {
            var item = array[i];
            if (item is null) continue;

            yield return item;
        }
    }
    
    public IEnumerator<T> EnumerateWhere(Func<T, bool> predicate)
    {
        if (!isReady) throw new Exception("Collection cannot be accessed while not ready");
        if (isDisposed) throw new ObjectDisposedException("Collection is disposed.");
        
        if (predicate is null) throw new ArgumentNullException(nameof(predicate));

        for (int i = 0; i < array.Length; i++)
        {
            if (!array.TryGet(i, out var item)) continue;
            if (!predicate(item)) continue;
            
            yield return item;
        }
    }

    public override IEnumerator<object> EnumerateItems()
    {
        if (!isReady) throw new Exception("Collection cannot be accessed while not ready");
        if (isDisposed) throw new ObjectDisposedException("Collection is disposed.");
        
        for (int i = 0; i < array.Length; i++)
        {
            if (!array.TryGet(i, out var item))
                continue;
            
            yield return item;
        }
    }
    #endregion

    private void CheckArray(int count)
    {
        if (array is null) InitializeArray(count < 1 ? null : count);
        if (array.Length < count) ResizeArray(array.Length + count);
    }

    private void InitializeArray(int? requiredSize)
    {
        if (array is null)
        {
            var size = File.ArrayInitialSize;
            
            if (requiredSize.HasValue && requiredSize.Value > size)
                size += requiredSize.Value;

            array = new T[size];

            for (int i = 0; i < File.ArrayInitialSize; i++)
                array[i] = null;
        }
    }
    
    private void ResizeArray(int? requiredSize)
    {
        var size = array.Length * File.ArrayResizeMultiplier;
            
        if (requiredSize.HasValue && requiredSize.Value > size)
            size += requiredSize.Value;

        var newArray = new T[size];
        var oldArray = array;
        
        array = newArray;

        for (int i = 0; i < size; i++)
        {
            if (i < oldArray.Length)
            {
                newArray[i] = oldArray[i];
                oldArray[i] = null;
            }
            else
            {
                newArray[i] = null;
            }
        }
    }

    private int GetNewIndex()
    {
        for (int i = 0; i < array.Length; i++)
        {
            if (array[i] is null)
                return i;
        }

        return -1;
    }

    public List<int> GetIndexes(int count)
    {
        var list = PoolUtils<int>.List;
        if (list.Capacity < count) list.Capacity = count;
        
        for (int i = 0; i < count; i++)
            list.Add(GetNewIndex());

        return list;
    }

    public int SetNewIndex(T item)
    {
        var index = GetNewIndex();

        if (index < 0)
            throw new Exception("No more free space in array");
        
        array[index] = item;
        return index;
    }

    public override void AddItems(IEnumerable<object> items)
    {
        if (!isReady) throw new Exception("Collection cannot be accessed while not ready");
        if (isDisposed) throw new ObjectDisposedException("Collection is disposed.");
        if (items is null) throw new ArgumentNullException(nameof(items));

        var count = items.Count();
        var addedCount = 0;

        if (count < 1) return;

        var indexes = GetIndexes(count);
        var index = 0;
        
        CheckArray(array.Length + count);

        foreach (var item in items)
        {
            Manipulator.SetObjectId(item, Data.NextObjectId);
            
            array[indexes[index++]] = (T)item;
            
            addedCount++;
        }
        
        PoolUtils<int>.Return(indexes);

        if (addedCount > 0)
        {
            Data.IncrementSize(addedCount);
            Monitor.Register();
        }
    }

    public override void Dispose()
    {
        if (isDisposed) throw new ObjectDisposedException("Collection is disposed.");

        isReady = false;
        isDisposed = true;
        
        ClearArray();
        
        array = null;
    }

    internal override void ClearArray()
    {
        if (isDisposed) throw new ObjectDisposedException("Collection is disposed.");
        
        if (array is null) 
            return;

        for (int i = 0; i < array.Length; i++)
            array[i] = null;
    }
    
    internal override bool TryGetItem(int itemId, out object item)
    {
        if (!isReady) throw new Exception("Collection cannot be accessed while not ready");
        if (isDisposed) throw new ObjectDisposedException("Collection is disposed.");

        item = null;
        
        if (itemId < 0) return false;

        for (int i = 0; i < array.Length; i++)
        {
            if (!array.TryGet(i, out var heldItem)) continue;
            
            var heldId = Manipulator.GetObjectId(heldItem);
            
            if (!heldId.HasValue || heldId.Value != itemId) continue;
            
            item = heldItem;
            return true;
        }

        return false;
    }

    internal override void RemoveMissing(List<int> foundIds)
    {
        if (isDisposed) throw new ObjectDisposedException("Collection is disposed.");
        if (array is null) return;
        
        for (int i = 0; i < array.Length; i++)
        {
            if (!array.TryGet(i, out var item)) continue;
            
            var id = Manipulator.GetObjectId(item);
            
            if (!id.HasValue) continue;
            if (foundIds.Contains(id.Value)) continue;
            
            Manipulator.RemoveObjectId(item, out _);

            array[i] = null;
        }
    }

    internal override void MakeReady() => isReady = true;
    internal override void MakeUnReady() => isReady = false;
}