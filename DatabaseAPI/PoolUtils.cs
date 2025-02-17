using System;
using System.Collections.Generic;

namespace DatabaseAPI;

public static class PoolUtils<T>
{
    public static volatile Func<List<T>> RentList;
    public static volatile Func<HashSet<T>> RentHashSet;
    
    public static volatile Action<List<T>> ReturnList;
    public static volatile Action<HashSet<T>> ReturnHashSet;

    public static List<T> List
    {
        get
        {
            if (RentList is null) return new List<T>();
            return RentList();
        }
    }

    public static HashSet<T> HashSet
    {
        get
        {
            if (RentHashSet is null) return new HashSet<T>();
            return RentHashSet();
        }
    }

    public static void Return(List<T> list)
    {
        if (list is null) throw new ArgumentNullException(nameof(list));
        if (ReturnList != null) ReturnList(list);
    }

    public static void Return(HashSet<T> hashSet)
    {
        if (hashSet is null) throw new ArgumentNullException(nameof(hashSet));
        if (ReturnHashSet != null) ReturnHashSet(hashSet);
    }
}