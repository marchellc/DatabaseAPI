using System.IO;
using System.Threading.Tasks;

using DatabaseAPI.Collections;

namespace DatabaseAPI.IO.Interfaces;

public interface ICollectionReaderWriter
{
    Task WriteAsync(StreamWriter writer, DatabaseCollectionData data, IStreamSerializer serializer);
    Task ReadAsync(StreamReader reader, DatabaseCollectionData data, IStreamDeserializer deserializer, bool isInitialRead);
}