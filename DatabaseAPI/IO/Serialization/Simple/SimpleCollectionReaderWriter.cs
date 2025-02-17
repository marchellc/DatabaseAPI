using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using DatabaseAPI.Collections;
using DatabaseAPI.IO.Interfaces;

namespace DatabaseAPI.IO.Serialization.Simple;

public class SimpleCollectionReaderWriter : ICollectionReaderWriter
{
    public static volatile SimpleCollectionReaderWriter Instance = new();
    
    public async Task WriteAsync(StreamWriter writer, DatabaseCollectionData data, IStreamSerializer serializer)
    {
        await serializer.WriteIntAsync(writer, data.Size);
        await serializer.WriteIntAsync(writer, data.IdClock);
        await serializer.WriteIntAsync(writer, data.IdQueue.Count);
        
        foreach (var id in data.IdQueue)
            await serializer.WriteIntAsync(writer, id);

        using (var enumerator = data.Wrapper.EnumerateItems())
        {
            while (enumerator.MoveNext())
            {
                await data.ReaderWriter.SerializeAsync(writer, enumerator.Current, data, serializer);
            }
        }
    }
    
    public async Task ReadAsync(StreamReader reader, DatabaseCollectionData data, IStreamDeserializer deserializer, bool isInitialRead)
    {
        var size = await deserializer.ReadIntAsync(reader);
        var id = await deserializer.ReadIntAsync(reader);
        var queueSize = await deserializer.ReadIntAsync(reader);
        var foundList = PoolUtils<int>.List;

        data.Size = size;
        data.IdClock = id;
        
        for (int i = 0; i < queueSize; i++)
        {
            var queueId = await deserializer.ReadIntAsync(reader);
            
            if (isInitialRead || !data.IdQueue.Contains(queueId))
                data.IdQueue.Enqueue(queueId);
        }

        if (isInitialRead)
            data.Wrapper.ClearArray();
        
        for (int i = 0; i < data.Size; i++)
        {
            var objectId = await deserializer.ReadIntAsync(reader);
            
            foundList.Add(objectId);

            if (!isInitialRead && data.Wrapper.TryGetItem(objectId, out var item))
            {
                await data.ReaderWriter.DeserializeAsync(reader, item, data, deserializer);
            }
            else
            {
                if (Activator.CreateInstance(data.Type) is not object instance)
                    throw new Exception($"Could not instantiate type {data.Type.FullName}");
                
                await data.ReaderWriter.DeserializeAsync(reader, instance, data, deserializer);

                data.Wrapper.SetIndex(objectId, instance);
            }
        }
        
        if (!isInitialRead)
            data.Wrapper.RemoveMissing(foundList);
        
        data.SetReady();
        
        PoolUtils<int>.Return(foundList);
    }
}