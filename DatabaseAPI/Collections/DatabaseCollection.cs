using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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

    private volatile DatabaseCollectionItem<T>[] array;
    
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
            if (!array[i].TryGetValue(out var item)) continue;
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
            if (!array[i].TryGetValue(out var heldItem)) continue;
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
            if (!array[i].TryGetValue(out var heldItem)) continue;
            if (!predicate(heldItem)) continue;

            return heldItem;
        }

        var newItem = Constructor();
        var newId = Data.NextObjectId;

        isNew = true;

        try
        {
            newSetup?.Invoke(newItem);
        }
        catch (Exception ex)
        {
            Log.Error("Database Collection", ex);
        }
        
        CheckArray(newId + 1);

        if (array[newId].HoldItem(newItem))
        {
            Manipulator.SetObjectId(newItem, newId);
            Data.IncrementSize(1);
            Monitor.Register();
        }
        else
        {
            Data.IdQueue.Enqueue(newId);
        }
        
        
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

        try
        {
            update(newItem, false);
        }
        catch (Exception ex)
        {
            Log.Error("Database Collection", ex);
        }
        
        CheckArray(newId + 1);

        if (array[newId].HoldItem(newItem))
        {
            Manipulator.SetObjectId(newItem, newId);
            Data.IncrementSize(1);
        }
        else
        {
            Data.IdQueue.Enqueue(newId);
            return;
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
                    
                    if (!array[batchStartIndex++].TryGetValue(out var heldItem)) continue;
                    if (!predicate(heldItem)) continue;

                    batchStatus = true;
                    
                    onCompleted(heldItem, true);
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
            if (!array[i].TryGetValue(out var heldItem)) continue;
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
        
        if (itemId >= array.Length) 
            return false;

        return array[itemId].TryGetValue(out item);
    }
    
    public int TryRemoveWhere(Func<T, bool> predicate)
    {
        if (!isReady) throw new Exception("Collection cannot be accessed while not ready");
        if (isDisposed) throw new ObjectDisposedException("Collection is disposed.");
        if (predicate is null) throw new ArgumentNullException(nameof(predicate));
        
        var removedCount = 0;

        for (int i = 0; i < array.Length; i++)
        {
            if (!array[i].TryGetValue(out var heldItem)) continue;
            if (!predicate(heldItem)) continue;
            if (!array[i].ReleaseItem()) continue;
            if (Manipulator.RemoveObjectId(heldItem, out var id)) Data.IdQueue.Enqueue(id);

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

        foreach (var item in items)
        {
            var currentItemId = Manipulator.GetObjectId(item);
            
            if (!currentItemId.HasValue) continue;
            if (currentItemId.Value >= array.Length) continue;
            if (!array[currentItemId.Value].ReleaseItem()) continue;
            if (Manipulator.RemoveObjectId(item, out var id)) Data.IdQueue.Enqueue(id);

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
        
        var currentItemId = Manipulator.GetObjectId(item);
        if (!currentItemId.HasValue) return false;
        
        CheckArray(currentItemId.Value + 1);

        if (array[currentItemId.Value].ReleaseItem())
        {
            if (Manipulator.RemoveObjectId(item, out var id))
                Data.IdQueue.Enqueue(id);
            
            Data.DecrementSize(1);
            
            Monitor.Register();
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
        
        CheckArray(count);

        foreach (var item in items)
        {
            var currentItemId = Manipulator.GetObjectId(item);
            if (currentItemId.HasValue) continue;

            var newItemId = Data.NextObjectId;
            
            CheckArray(newItemId + 1);

            if (array[newItemId].HoldItem(item))
                addedCount++;
        }

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

        var currentItemId = Manipulator.GetObjectId(item);
        if (currentItemId.HasValue) return false;

        var newItemId = Data.NextObjectId;

        Manipulator.SetObjectId(item, newItemId);

        CheckArray(newItemId + 1);

        if (array[newItemId].HoldItem(item))
        {
            Data.IncrementSize(1);
            
            Monitor.Register();
            return true;
        }

        return false;
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

            if (!item.TryGetValue(out var value)) 
                continue;

            yield return value;
        }
    }
    
    public IEnumerator<T> EnumerateWhere(Func<T, bool> predicate)
    {
        if (!isReady) throw new Exception("Collection cannot be accessed while not ready");
        if (isDisposed) throw new ObjectDisposedException("Collection is disposed.");
        
        if (predicate is null) throw new ArgumentNullException(nameof(predicate));

        for (int i = 0; i < array.Length; i++)
        {
            if (!array[i].TryGetValue(out var item)) continue;
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
            if (!array[i].TryGetValue(out var item))
                continue;
            
            yield return item;
        }
    }
    #endregion

    private void CheckArray(int count)
    {
        if (array is null) InitializeArray(count < 1 ? null : count);
        if (array.Length < count) ResizeArray(count < 1 ? null : count);
    }

    private void InitializeArray(int? requiredSize)
    {
        if (array is null)
        {
            var size = File.ArrayInitialSize;
            
            if (requiredSize.HasValue && requiredSize.Value > size)
                size += requiredSize.Value;
            
            array = new DatabaseCollectionItem<T>[size];
            
            for (int i = 0; i < File.ArrayInitialSize; i++)
                array[i] = new DatabaseCollectionItem<T>();
        }
    }
    
    private void ResizeArray(int? requiredSize)
    {
        var size = array.Length * File.ArrayResizeMultiplier;
            
        if (requiredSize.HasValue && requiredSize.Value > size)
            size += requiredSize.Value;
        
        var newArray = new DatabaseCollectionItem<T>[size];
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
                newArray[i] = new DatabaseCollectionItem<T>();
            }
        }
    }

    internal override void ClearArray()
    {
        if (isDisposed) throw new ObjectDisposedException("Collection is disposed.");
        
        if (array is null) 
            return;
        
        for (int i = 0; i < array.Length; i++)
            array[i].ReleaseItem();
    }
    
    internal override void SetIndex(int index, object item)
    {
        if (isReady) throw new Exception("Collection is in ready mode.");
        if (isDisposed) throw new ObjectDisposedException("Collection is disposed.");
        
        CheckArray(index + 1);

        array[index].HoldItem((T)item);
    }

    internal override void RemoveIndex(int index)
    {
        if (isReady) throw new Exception("Collection is in ready mode.");
        if (isDisposed) throw new ObjectDisposedException("Collection is disposed.");
        
        CheckArray(index + 1);
        
        if (array[index].ReleaseItem())
            Data.DecrementSize(1);
    }
    
    internal override bool TryGetItem(int itemId, out object item)
    {
        if (!isReady) throw new Exception("Collection cannot be accessed while not ready");
        if (isDisposed) throw new ObjectDisposedException("Collection is disposed.");

        item = null;
        
        if (itemId < 0 || itemId >= array.Length || !array[itemId].TryGetValue(out var heldItem)) 
            return false;

        item = heldItem;
        return true;
    }

    internal override void RemoveMissing(List<int> foundIds)
    {
        if (isDisposed) throw new ObjectDisposedException("Collection is disposed.");
        if (array is null) return;
        
        for (int i = 0; i < array.Length; i++)
        {
            if (!array[i].TryGetValue(out var item)) continue;
            
            var id = Manipulator.GetObjectId(item);
            
            if (!id.HasValue) continue;
            if (!foundIds.Contains(id.Value)) continue;
            
            Manipulator.RemoveObjectId(item, out _);
            
            array[i].ReleaseItem();
        }
    }

    internal override void MakeReady() => isReady = true;
    internal override void MakeUnReady() => isReady = false;
}