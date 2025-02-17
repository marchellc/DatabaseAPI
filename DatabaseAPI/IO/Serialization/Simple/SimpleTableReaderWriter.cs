using System;
using System.IO;
using System.Threading.Tasks;

using DatabaseAPI.IO.Interfaces;

namespace DatabaseAPI.IO.Serialization.Simple;

public class SimpleTableReaderWriter : ITableReaderWriter
{
    public static volatile SimpleTableReaderWriter Instance = new();
    
    public async Task WriteAsync(StreamWriter writer, DatabaseTable table, IStreamSerializer serializer)
    {
        await serializer.WriteIntAsync(writer, table.Collections.Count);

        foreach (var data in table.Collections)
        {
            await serializer.WriteStringAsync(writer, data.Key);
            await serializer.WriteStringAsync(writer, data.Value.Type.AssemblyQualifiedName);
            
            await table.File.CollectionReaderWriter?.WriteAsync(writer, data.Value, serializer);
        }
    }
    
    public async Task ReadAsync(StreamReader reader, DatabaseTable table, IStreamDeserializer deserializer, bool isInitialRead)
    {
        var count = await deserializer.ReadIntAsync(reader);
        
        for (int i = 0; i < count; i++)
        {
            var name = await deserializer.ReadStringAsync(reader);
            var typeName = await deserializer.ReadStringAsync(reader);
            var type = Type.GetType(typeName, true);

            if (isInitialRead)
            {
                var collection = table.CreateCollection(name, type);
                
                table.Collections.TryAdd(name, collection);

                await table.File.CollectionReaderWriter?.ReadAsync(reader, collection, deserializer, isInitialRead);
            }
            else
            {
                if (!table.Collections.TryGetValue(name, out var collection))
                {
                    collection = table.CreateCollection(name, type);
                    table.Collections.TryAdd(name, collection);
                }

                await table.File.CollectionReaderWriter?.ReadAsync(reader, collection, deserializer, isInitialRead);
            }
        }
    }
}