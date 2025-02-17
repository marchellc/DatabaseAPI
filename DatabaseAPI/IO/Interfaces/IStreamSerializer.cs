using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace DatabaseAPI.IO.Interfaces;

public interface IStreamSerializer
{ 
    Task WriteByteAsync(StreamWriter writer, byte value);
    Task WriteSByteAsync(StreamWriter writer, sbyte value);
    Task WriteShortAsync(StreamWriter writer, short value);
    Task WriteUShortAsync(StreamWriter writer, ushort value);
    Task WriteIntAsync(StreamWriter writer, int value);
    Task WriteUIntAsync(StreamWriter writer, uint value);
    Task WriteLongAsync(StreamWriter writer, long value);
    Task WriteULongAsync(StreamWriter writer, ulong value);
    Task WriteFloatAsync(StreamWriter writer, float value);
    Task WriteDoubleAsync(StreamWriter writer, double value);
    Task WriteDecimalAsync(StreamWriter writer, decimal value);
    Task WriteStringAsync(StreamWriter writer, string value);
    Task WriteBoolAsync(StreamWriter writer, bool value);
    Task WriteCharAsync(StreamWriter writer, char value);
    Task WriteArrayAsync<T>(StreamWriter writer, IEnumerable<T> values, Func<StreamWriter, T, Task> serializer);
}