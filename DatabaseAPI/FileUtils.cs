using System;
using System.IO;
using DatabaseAPI.IO.Locks;
using DatabaseAPI.Logging;

namespace DatabaseAPI;

public static class FileUtils
{
    public static void CopyIfExists(string path, string postfix)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentNullException(nameof(path));

        if (string.IsNullOrWhiteSpace(postfix))
            throw new ArgumentNullException(nameof(postfix));
        
        if (!File.Exists(path))
            return;

        var extension = Path.GetExtension(path);
        var name = Path.GetFileNameWithoutExtension(path);
        var directory = Path.GetDirectoryName(path);

        name += postfix;

        if (!extension.StartsWith("."))
            extension = "." + extension;

        name += extension;

        var copyPath = Path.Combine(directory, name);

        try
        {
            File.Copy(path, copyPath);
        }
        catch (Exception ex)
        {
            Log.Debug("File Utils", $"CreateIfExists() :: Could not copy file '{path}' to '{copyPath}':\n{ex}");
        }
    }
    
    public static bool TryCreate(string filePath)
    {
        if (filePath is null)
            throw new ArgumentNullException(nameof(filePath));
        
        try
        {
            File.Create(filePath).Close();
            return true;
        }
        catch (Exception ex)
        {
            Log.Debug("File Utils", $"TryCreate() :: Could not create file '{filePath}':\n{ex}");
            return false;
        }
    }
    
    public static bool TryDelete(string filePath)
    {
        if (filePath is null)
            throw new ArgumentNullException(nameof(filePath));
        
        try
        {
            if (!File.Exists(filePath))
                return true;
            
            File.Delete(filePath);
            return true;
        }
        catch (Exception ex)
        {
            Log.Debug("File Utils", $"TryCreate() :: Could not delete file '{filePath}':\n{ex}");
            return false;
        }
    }
    
    public static bool ReadBinary(string filePath, int minFileLength, Action<BinaryReader> reader)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentNullException(nameof(filePath));
        
        if (reader is null)
            throw new ArgumentNullException(nameof(reader));
        
        if (!File.Exists(filePath))
        {
            Log.Debug("File Utils", $"ReadBinary() :: File '{filePath}' does not exist");
            return false;
        }

        try
        {
            Log.Debug("File Utils", $"ReadBinary() :: Reading file '{filePath}'");
            
            var fileData = File.ReadAllBytes(filePath);

            if (minFileLength > 0 && fileData.Length < minFileLength)
            {
                Log.Debug("File Utils", $"ReadBinary() :: Read invalid file data (read {filePath.Length} bytes, minimum is {minFileLength} bytes)");
                return false;
            }
            
            using (var stream = new MemoryStream(fileData))
            using (var binary = new BinaryReader(stream))
                reader(binary);

            fileData = null;
            return true;
        }
        catch (Exception ex)
        {
            Log.Debug("File Utils", $"ReadBinary() :: Could not read file '{filePath}' due to an exception:\n{ex}");
            return false;
        }
    }
    
    public static bool WriteBinary(string filePath, Action<BinaryWriter> writer)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentNullException(nameof(filePath));
        
        if (writer is null)
            throw new ArgumentNullException(nameof(writer));

        try
        {
            Log.Debug("File Utils", $"WriteBinary() :: Writing file '{filePath}'");
            
            using (var stream = new MemoryStream())
            using (var binary = new BinaryWriter(stream))
            {
                writer(binary);

                var data = stream.ToArray();
                
                Log.Debug("File Utils", $"WriteBinary() :: Serialized '{data.Length}' bytes");

                File.WriteAllBytes(filePath, data);
            }
            
            return true;
        }
        catch (Exception ex)
        {
            Log.Debug("File Utils", $"WriteBinary() :: Could not write file '{filePath}' due to an exception:\n{ex}");
            return false;
        }
    }
}