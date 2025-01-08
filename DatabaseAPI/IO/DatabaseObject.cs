using System.IO;
using System;

namespace DatabaseAPI.IO;

public abstract class DatabaseObject
{
    private volatile string _id;

    public string Id => _id;

    public abstract void Write(BinaryWriter writer);
    public abstract void Read(BinaryReader reader);

    internal void SetId(string id)
        => _id = id;
}