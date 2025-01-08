using System;
using System.IO;
using System.Net;
using System.Text;
using System.Linq;
using System.Collections.Generic;

using DatabaseAPI.IO;

namespace DatabaseAPI.Extensions;

public static class WriterExtensions
{
    public static void Write(this BinaryWriter writer, DateTime time)
    {
        if (writer is null)
            throw new ArgumentNullException(nameof(writer));

        writer.Write(time.ToBinary());
    }

    public static void Write(this BinaryWriter writer, DateTimeOffset offset)
    {
        if (writer is null)
            throw new ArgumentNullException(nameof(writer));

        writer.Write(offset.ToUnixTimeMilliseconds());
    }

    public static void Write(this BinaryWriter writer, TimeSpan span)
    {
        if (writer is null)
            throw new ArgumentNullException(nameof(writer));

        writer.Write(span.Ticks);
    }

    public static void Write(this BinaryWriter writer, Type type)
    {
        if (writer is null)
            throw new ArgumentNullException(nameof(writer));

        if (type is null)
            throw new ArgumentNullException(nameof(type));

        writer.Write(type.AssemblyQualifiedName);
    }

    public static void Write(this BinaryWriter writer, IPAddress address)
    {
        if (writer is null)
            throw new ArgumentNullException(nameof(writer));

        if (address is null)
            throw new ArgumentNullException(nameof(address));

        writer.WriteBytes(address.GetAddressBytes());
    }

    public static void Write(this BinaryWriter writer, IPEndPoint endPoint)
    {
        if (writer is null)
            throw new ArgumentNullException(nameof(writer));

        if (endPoint is null)
            throw new ArgumentNullException(nameof(endPoint));

        writer.Write(endPoint.Address);
        writer.Write((ushort)endPoint.Port);
    }

    public static void WriteItems<T>(this BinaryWriter writer, IEnumerable<T> objects, Action<T> serializer)
    {
        if (writer is null)
            throw new ArgumentNullException(nameof(writer));

        if (serializer is null)
            throw new ArgumentNullException(nameof(serializer));

        if (objects is null)
            throw new ArgumentNullException(nameof(objects));
        
        writer.Write(objects.Count());

        foreach (var obj in objects)
            serializer(obj);
    }

    public static void WriteDictionary<TKey, TValue>(this BinaryWriter writer, IDictionary<TKey, TValue> dictionary, Action<TKey> keySerializer, Action<TValue> valueSerializer)
    {
        if (writer is null)
            throw new ArgumentNullException(nameof(writer));

        if (dictionary is null)
            throw new ArgumentNullException(nameof(dictionary));

        if (keySerializer is null)
            throw new ArgumentNullException(nameof(keySerializer));

        if (valueSerializer is null)
            throw new ArgumentNullException(nameof(valueSerializer));
        
        writer.Write(dictionary.Count);

        foreach (var pair in dictionary)
        {
            keySerializer(pair.Key);
            valueSerializer(pair.Value);
        }
    }

    public static void WriteBytes(this BinaryWriter writer, byte[] bytes)
    {
        if (bytes is null)
            throw new ArgumentNullException(nameof(bytes));
        
        if (writer is null)
            throw new ArgumentNullException(nameof(writer));

        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    public static void WriteStringEncoding(this BinaryWriter writer, string value, Encoding encoding = null)
    {
        if (writer is null)
            throw new ArgumentNullException(nameof(writer));

        encoding ??= Encoding.UTF32;

        if (string.IsNullOrWhiteSpace(value))
            value = string.Empty;

        var bytes = encoding.GetBytes(value);

        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    public static void WriteObject(this BinaryWriter writer, DatabaseObject obj)
    {
        if (writer is null)
            throw new ArgumentNullException(nameof(writer));

        if (obj is null)
            throw new ArgumentNullException(nameof(obj));

        obj.Write(writer);
    }
}