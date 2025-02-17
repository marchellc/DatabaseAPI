using System;
using System.IO;
using System.Threading.Tasks;

namespace DatabaseAPI.IO.Interfaces;

public interface IStreamDeserializer
{
    Task<byte> ReadByteAsync(StreamReader reader);
    Task<sbyte> ReadSByteAsync(StreamReader reader);
    
    Task<short> ReadShortAsync(StreamReader reader);
    Task<ushort> ReadUShortAsync(StreamReader reader);
    
    Task<int> ReadIntAsync(StreamReader reader);
    Task<uint> ReadUIntAsync(StreamReader reader);
    
    Task<long> ReadLongAsync(StreamReader reader);
    Task<ulong> ReadULongAsync(StreamReader reader);
    
    Task<float> ReadFloatAsync(StreamReader reader);
    Task<double> ReadDoubleAsync(StreamReader reader);
    Task<decimal> ReadDecimalAsync(StreamReader reader);
    Task<string> ReadStringAsync(StreamReader reader);
    Task<char> ReadCharAsync(StreamReader reader);

    Task<T[]> ReadArrayAsync<T>(StreamReader reader, Func<StreamReader, Task<T>> deserializer);
}