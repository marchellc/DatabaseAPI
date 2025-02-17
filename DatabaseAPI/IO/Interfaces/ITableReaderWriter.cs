using System.IO;
using System.Threading.Tasks;

namespace DatabaseAPI.IO.Interfaces;

public interface ITableReaderWriter
{
    Task ReadAsync(StreamReader reader, DatabaseTable table, IStreamDeserializer deserializer, bool isInitialRead);
    Task WriteAsync(StreamWriter writer, DatabaseTable table, IStreamSerializer serializer);
}