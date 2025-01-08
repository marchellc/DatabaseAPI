using System;
using System.IO;
using System.Linq;
using System.Collections.Concurrent;

using DatabaseAPI.Extensions;

namespace DatabaseAPI.Collections;

public class DatabaseCollectionData : IDisposable
{
    private const string _randomChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    private static volatile Random _random = new Random();
    
    private volatile byte[] _data;
    private volatile string _name;

    private volatile Type _type;
    private volatile Type _wrapperType;

    private volatile DatabaseCollectionBase _wrapper;
    private volatile ConcurrentStack<string> _occupiedIds;

    public byte[] Data => _data;
    
    public string Name => _name;

    public Type Type => _type;
    public Type WrapperType => _wrapperType;

    public DatabaseCollectionBase Wrapper
    {
        get => _wrapper;
        internal set => _wrapper = value;
    }

    internal DatabaseCollectionData(string name, byte[] data, Type type, Type wrapperType)
    {
        _data = data;
        _name = name;
        _type = type;
        _wrapperType = wrapperType;

        _occupiedIds = new ConcurrentStack<string>();
    }

    public void Read(Func<BinaryReader, DatabaseCollectionBase> reader)
    {
        using (var stream = new MemoryStream(_data))
        using (var binary = new BinaryReader(stream))
        {
            _wrapper = reader(binary);
        }
    }

    public void Write(Action<BinaryWriter> writer)
    {
        using (var stream = new MemoryStream())
        using (var binary = new BinaryWriter(stream))
        {
            writer(binary);

            _data = stream.ToArray();
        }
    }

    public void Dispose()
    {
        _data = null;
        _name = null;

        _wrapper?.Dispose();
        _wrapper = null;
    }
    
    public string GenerateObjectId(int idSize)
    {
        if (idSize < 1)
            throw new ArgumentOutOfRangeException(nameof(idSize));

        string Generate()
        {
            var str = string.Empty;

            for (int i = 0; i < idSize; i++)
            {
                var generated = _randomChars[_random.Next(0, _randomChars.Length)];

                if (!char.IsNumber(generated) && _random.Next(0, 1) == 1)
                    generated = char.ToLower(generated);

                str += generated;
            }

            return str;
        }

        var generated = Generate();

        while (_occupiedIds.Contains(generated))
            generated = Generate();
        
        return generated;
    }

    public void RemoveObjectId(string objectId)
    {
        if (string.IsNullOrWhiteSpace(objectId))
            throw new ArgumentNullException(nameof(objectId));

        if (_occupiedIds.Contains(objectId))
            _occupiedIds.Remove(objectId);
    }

    public void OccupyId(string objectId)
    {
        if (string.IsNullOrWhiteSpace(objectId))
            throw new ArgumentNullException(nameof(objectId));

        if (!_occupiedIds.Contains(objectId))
            _occupiedIds.Push(objectId);
    }
}