using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.IO;

namespace DatabaseAPI.IO.Interfaces;

public interface IFileReaderWriter
{
    Task<bool> WriteAsync(StreamWriter writer, DatabaseFile file, IStreamSerializer serializer);
    Task<bool> ReadAsync(StreamReader reader, DatabaseFile file, IStreamDeserializer deserializer, bool isInitialRead);
}