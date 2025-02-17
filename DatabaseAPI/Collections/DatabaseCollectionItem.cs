using System;

namespace DatabaseAPI.Collections;

public class DatabaseCollectionItem<T> where T : class
{
    private volatile bool isHeld = false;
    private volatile T value = null;
    
    public bool IsHeld => isHeld;
    
    public T Value => value;

    public DatabaseCollectionItem()
    {
        isHeld = false;
        value = null;
    }

    public DatabaseCollectionItem(T value)
    {
        this.isHeld = true;
        this.value = value;
    }

    public bool TryGetValue(out T value)
    {
        value = this.value;
        return isHeld && value != null;
    }

    public bool HoldItem(T item)
    {
        if (item is null) throw new ArgumentNullException(nameof(item));
        if (isHeld && value is not null && item.Equals(value)) return false;
        
        isHeld = true;
        value = item;

        return true;
    }

    public bool ReleaseItem()
    {
        if (!isHeld) 
            return false;
        
        isHeld = false;
        value = null;

        return true;
    }
}