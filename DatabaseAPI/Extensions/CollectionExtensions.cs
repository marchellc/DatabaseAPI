using System.Collections.Concurrent;
using System.Collections.Generic;

namespace DatabaseAPI.Extensions;

public static class CollectionExtensions
{
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