using System;
using System.IO;
using System.Net;
using System.Text;
using System.Collections.Generic;
using DatabaseAPI.Collections;
using DatabaseAPI.IO;

namespace DatabaseAPI.Extensions;

public static class ReaderExtensions
{
    public static byte[] ReadBytes(this BinaryReader reader)
    {
        if (reader is null)
            throw new ArgumentNullException(nameof(reader));

        return reader.ReadBytes(reader.ReadInt32());
    }

    public static DateTime ReadDate(this BinaryReader reader)
    {
        if (reader is null)
            throw new ArgumentNullException(nameof(reader));

        return DateTime.FromBinary(reader.ReadInt64());
    }

    public static DateTimeOffset ReadOffset(this BinaryReader reader)
    {
        if (reader is null)
            throw new ArgumentNullException(nameof(reader));

        return DateTimeOffset.FromUnixTimeMilliseconds(reader.ReadInt64());
    }

    public static TimeSpan ReadSpan(this BinaryReader reader)
    {
        if (reader is null)
            throw new ArgumentNullException(nameof(reader));

        return TimeSpan.FromTicks(reader.ReadInt64());
    }

    public static IPAddress ReadIpAddress(this BinaryReader reader)
    {
        if (reader is null)
            throw new ArgumentNullException(nameof(reader));

        return new IPAddress(reader.ReadBytes());
    }

    public static IPEndPoint ReadIpEndPoint(this BinaryReader reader)
    {
        if (reader is null)
            throw new ArgumentNullException(nameof(reader));

        return new IPEndPoint(reader.ReadIpAddress(), reader.ReadUInt16());
    }

    public static Type ReadType(this BinaryReader reader)
    {
        if (reader is null)
            throw new ArgumentNullException(nameof(reader));

        return Type.GetType(reader.ReadString(), true);
    }

    public static T[] ReadArray<T>(this BinaryReader reader, Func<T> converter)
    {
        if (reader is null)
            throw new ArgumentNullException(nameof(reader));

        if (converter is null)
            throw new ArgumentNullException(nameof(converter));
        
        var size = reader.ReadInt32();
        var array = new T[size];

        for (int i = 0; i < size; i++)
            array[i] = converter();

        return array;
    }

    public static List<T> ReadList<T>(this BinaryReader reader, Func<T> converter)
    {
        if (reader is null)
            throw new ArgumentNullException(nameof(reader));

        if (converter is null)
            throw new ArgumentNullException(nameof(converter));
        
        var size = reader.ReadInt32();
        var list = new List<T>(size);

        for (int i = 0; i < size; i++)
            list.Add(converter());

        return list;
    }

    public static HashSet<T> ReadHashSet<T>(this BinaryReader reader, Func<T> converter)
    {
        if (reader is null)
            throw new ArgumentNullException(nameof(reader));

        if (converter is null)
            throw new ArgumentNullException(nameof(converter));
        
        var size = reader.ReadInt32();
        var set = new HashSet<T>(size);

        for (int i = 0; i < size; i++)
            set.Add(converter());

        return set;
    }

    public static Dictionary<TKey, TValue> ReadDictionary<TKey, TValue>(this BinaryReader reader, Func<TKey> keyConverter, Func<TKey, TValue> valueConverter)
    {
        if (reader is null)
            throw new ArgumentNullException(nameof(reader));

        if (keyConverter is null)
            throw new ArgumentNullException(nameof(keyConverter));

        if (valueConverter is null)
            throw new ArgumentNullException(nameof(valueConverter));
        
        var size = reader.ReadInt32();
        var dict = new Dictionary<TKey, TValue>(size);

        for (int i = 0; i < size; i++)
        {
            var key = keyConverter();
            dict.Add(key, valueConverter(key));
        }

        return dict;
    }
    
    public static Dictionary<TKey, TValue> ReadDictionary<TKey, TValue>(this BinaryReader reader, Func<TKey> keyConverter, Func<TValue> valueConverter)
    {
        if (reader is null)
            throw new ArgumentNullException(nameof(reader));

        if (keyConverter is null)
            throw new ArgumentNullException(nameof(keyConverter));

        if (valueConverter is null)
            throw new ArgumentNullException(nameof(valueConverter));
        
        var size = reader.ReadInt32();
        var dict = new Dictionary<TKey, TValue>(size);

        for (int i = 0; i < size; i++)
            dict.Add(keyConverter(), valueConverter());

        return dict;
    }

    public static string ReadStringEncoding(this BinaryReader reader, Encoding encoding = null)
    {
        if (reader is null)
            throw new ArgumentNullException(nameof(reader));

        encoding ??= Encoding.UTF32;

        var size = reader.ReadInt32();
        var bytes = reader.ReadBytes(size);

        return encoding.GetString(bytes);
    }
}