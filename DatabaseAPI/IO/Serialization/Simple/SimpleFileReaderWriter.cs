using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.IO;

using DatabaseAPI.IO.Interfaces;

namespace DatabaseAPI.IO.Serialization.Simple;

public class SimpleFileReaderWriter : IFileReaderWriter
{
    public static volatile SimpleFileReaderWriter Instance = new();
    
    public async Task<bool> WriteAsync(StreamWriter writer, DatabaseFile file, IStreamSerializer serializer)
    {
        await serializer.WriteIntAsync(writer, file.Tables.Count);

        foreach (var table in file.Tables)
        {
            await serializer.WriteStringAsync(writer, table.Key);
            await file.TableReaderWriter?.WriteAsync(writer, table.Value, serializer);
        }

        return true;
    }

    public async Task<bool> ReadAsync(StreamReader reader, DatabaseFile file, IStreamDeserializer deserializer, bool isInitialRead)
    {
        var count = await deserializer.ReadIntAsync(reader);

        for (int i = 0; i < count; i++)
        {
            if (isInitialRead)
            {
                var name = await deserializer.ReadStringAsync(reader);
                var table = file.InstantiateTable(name);

                file.Tables.TryAdd(name, table);

                await file.TableReaderWriter?.ReadAsync(reader, table, deserializer, isInitialRead);
            }
            else
            {
                var name = await reader.ReadLineAsync();

                if (!file.Tables.TryGetValue(name, out var table))
                {
                    table = file.InstantiateTable(name);
                    
                    file.Tables.TryAdd(name, table);
                }

                await file.TableReaderWriter?.ReadAsync(reader, table, deserializer, isInitialRead);
            }
        }

        return true;
    }
}