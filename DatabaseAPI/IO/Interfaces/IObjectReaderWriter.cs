using System;
using System.IO;
using System.Threading.Tasks;

using DatabaseAPI.Collections;

namespace DatabaseAPI.IO.Interfaces;

public interface IObjectReaderWriter
{ 
    Type Type { get; }
    
    Task DeserializeAsync(StreamReader reader, object obj, DatabaseCollectionData collection, IStreamDeserializer deserializer);
    Task SerializeAsync(StreamWriter writer, object? obj, DatabaseCollectionData collection, IStreamSerializer serializer);
}