using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

using DatabaseAPI.IO.Interfaces;

namespace DatabaseAPI.IO.Serialization.Text;

public class TextStreamSerializer : IStreamSerializer
{
    public static volatile TextStreamSerializer Instance = new();
    
    public async Task WriteByteAsync(StreamWriter writer, byte value) => await writer.WriteLineAsync(value.ToString());
    public async Task WriteSByteAsync(StreamWriter writer, sbyte value) => await writer.WriteLineAsync(value.ToString());
    
    public async Task WriteShortAsync(StreamWriter writer, short value) => await writer.WriteLineAsync(value.ToString());
    public async Task WriteUShortAsync(StreamWriter writer, ushort value) => await writer.WriteLineAsync(value.ToString());

    public async Task WriteIntAsync(StreamWriter writer, int value) => await writer.WriteLineAsync(value.ToString());
    public async Task WriteUIntAsync(StreamWriter writer, uint value) => await writer.WriteLineAsync(value.ToString());

    public async Task WriteLongAsync(StreamWriter writer, long value) => await writer.WriteLineAsync(value.ToString());
    public async Task WriteULongAsync(StreamWriter writer, ulong value) => await writer.WriteLineAsync(value.ToString());

    public async Task WriteFloatAsync(StreamWriter writer, float value) => await writer.WriteLineAsync(value.ToString());
    public async Task WriteDoubleAsync(StreamWriter writer, double value) => await writer.WriteLineAsync(value.ToString());
    public async Task WriteDecimalAsync(StreamWriter writer, decimal value) => await writer.WriteLineAsync(value.ToString());
    public async Task WriteStringAsync(StreamWriter writer, string value) => await writer.WriteLineAsync(value);
    public async Task WriteBoolAsync(StreamWriter writer, bool value) => await writer.WriteLineAsync(value.ToString());
    public async Task WriteCharAsync(StreamWriter writer, char value) => await writer.WriteLineAsync(value.ToString());

    public async Task WriteArrayAsync<T>(StreamWriter writer, IEnumerable<T> values, Func<StreamWriter, T, Task> serializer)
    {
        if (writer is null) throw new ArgumentNullException(nameof(writer));
        if (values is null) throw new ArgumentNullException(nameof(values));
        if (serializer is null) throw new ArgumentNullException(nameof(serializer));

        var count = values.Count();
        
        await WriteIntAsync(writer, count);
        
        foreach (var item in values)
            await serializer(writer, item);
    }
}