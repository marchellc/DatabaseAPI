using System;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace DatabaseAPI.Extensions;

public static class CollectionExtensions
{
    public static bool TryRemove<T>(this T[] array, int index)
    {
        if (array is null) throw new ArgumentNullException(nameof(array));
        if (!TryGet(array, index, out var value)) return false;
        if (value is IDisposable disposable) disposable.Dispose();
        
        array[index] = default;
        return true;
    }
    
    public static bool TryGet<T>(this T[] array, int index, out T result)
    {
        if (array is null) throw new ArgumentNullException(nameof(array));

        result = default;
        
        if (index < 0 || index >= array.Length) return false;
        if ((result = array[index]) is null) return false;

        return true;
    }
    
    public static bool Remove<T>(this ConcurrentStack<T> stack, T item)
    {
        var items = new List<T>(stack.Count);
        var removed = false;

        while (stack.TryPop(out var poppedItem))
            items.Add(poppedItem);

        for (int i = 0; i < items.Count; i++)
        {
            var curItem = items[i];

            if (item.Equals(curItem))
            {
                removed = true;
                continue;
            }

            stack.Push(curItem);
        }

        return removed;
    }
}