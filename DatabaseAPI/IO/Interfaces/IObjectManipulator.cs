using System;

namespace DatabaseAPI.IO.Interfaces;

public interface IObjectManipulator
{
    Type Type { get; }

    bool SetObjectId(object obj, int objectId);
    bool RemoveObjectId(object obj, out int removedId);
    
    int? GetObjectId(object obj);
}