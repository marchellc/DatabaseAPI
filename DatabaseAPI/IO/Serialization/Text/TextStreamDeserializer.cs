using System;
using System.IO;
using System.Threading.Tasks;

using DatabaseAPI.IO.Interfaces;

namespace DatabaseAPI.IO.Serialization.Text;

public class TextStreamDeserializer : IStreamDeserializer
{
    public static volatile TextStreamDeserializer Instance = new();

    public async Task<byte> ReadByteAsync(StreamReader reader) => byte.Parse(await reader.ReadLineAsync());
    public async Task<sbyte> ReadSByteAsync(StreamReader reader) => sbyte.Parse(await reader.ReadLineAsync());
    
    public async Task<short> ReadShortAsync(StreamReader reader) => short.Parse(await reader.ReadLineAsync());
    public async Task<ushort> ReadUShortAsync(StreamReader reader) => ushort.Parse(await reader.ReadLineAsync());
    
    public async Task<int> ReadIntAsync(StreamReader reader) => int.Parse(await reader.ReadLineAsync());
    public async Task<uint> ReadUIntAsync(StreamReader reader) => uint.Parse(await reader.ReadLineAsync());
    
    public async Task<long> ReadLongAsync(StreamReader reader) => long.Parse(await reader.ReadLineAsync());
    public async Task<ulong> ReadULongAsync(StreamReader reader) => ulong.Parse(await reader.ReadLineAsync());


    public async Task<float> ReadFloatAsync(StreamReader reader) => float.Parse(await reader.ReadLineAsync());
    public async Task<double> ReadDoubleAsync(StreamReader reader) => double.Parse(await reader.ReadLineAsync());
    public async Task<decimal> ReadDecimalAsync(StreamReader reader) => decimal.Parse(await reader.ReadLineAsync());

    public async Task<string> ReadStringAsync(StreamReader reader) => await reader.ReadLineAsync();
    public async Task<char> ReadCharAsync(StreamReader reader) => (await reader.ReadLineAsync())[0];

    public async Task<T[]> ReadArrayAsync<T>(StreamReader reader, Func<StreamReader, Task<T>> deserializer)
    {
        if (reader is null) throw new ArgumentNullException(nameof(reader));
        if (deserializer is null) throw new ArgumentNullException(nameof(deserializer));
        
        var count = await ReadIntAsync(reader);
        var array = new T[count];
        
        for (int i = 0; i < count; i++)
            array[i] = await deserializer(reader);

        return array;
    }
}