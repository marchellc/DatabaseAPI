using System;
using System.IO;
using System.Threading.Tasks;

using DatabaseAPI.Logging;

namespace DatabaseAPI;

public static class FileUtils
{
    public static void CopyIfExists(string path, string postfix)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentNullException(nameof(path));
        if (string.IsNullOrWhiteSpace(postfix)) throw new ArgumentNullException(nameof(postfix));
        
        if (!File.Exists(path)) return;

        var extension = Path.GetExtension(path);
        var name = Path.GetFileNameWithoutExtension(path);
        var directory = Path.GetDirectoryName(path);

        name += postfix;

        if (!string.IsNullOrWhiteSpace(extension) && !extension.StartsWith(".")) extension = "." + extension;
        if (!string.IsNullOrWhiteSpace(extension)) name += extension;

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
        if (filePath is null) throw new ArgumentNullException(nameof(filePath));
        
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
        if (filePath is null) throw new ArgumentNullException(nameof(filePath));
        
        try
        {
            if (!File.Exists(filePath)) return true;
            
            File.Delete(filePath);
            return true;
        }
        catch (Exception ex)
        {
            Log.Debug("File Utils", $"TryCreate() :: Could not delete file '{filePath}':\n{ex}");
            return false;
        }
    }
    
    public static async Task ReadFileAsync(string filePath, Func<StreamReader, Task> reader)
    {
        if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentNullException(nameof(filePath));
        if (reader is null) throw new ArgumentNullException(nameof(reader));
        
        if (!File.Exists(filePath))
        {
            Log.Debug("File Utils", $"ReadFileAsync() :: File '{filePath}' does not exist");
            return;
        }

        try
        {
            Log.Debug("File Utils", $"ReadFileAsync() :: Reading file '{filePath}'");
            
            using (var stream = File.Open(filePath, FileMode.OpenOrCreate, FileAccess.Read, FileShare.Read))
            using (var sr = new StreamReader(stream)) await reader(sr);
        }
        catch (Exception ex)
        {
            Log.Debug("File Utils", $"ReadFileAsync() :: Could not read file '{filePath}' due to an exception:\n{ex}");
        }
    }
    
    public static async Task WriteFileAsync(string filePath, Func<StreamWriter, Task> writer)
    {
        if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentNullException(nameof(filePath));
        if (writer is null) throw new ArgumentNullException(nameof(writer));

        try
        {
            Log.Debug("File Utils", $"WriteFileAsync() :: Writing file '{filePath}'");
            
            using (var stream = File.Open(filePath, FileMode.Truncate, FileAccess.Write, FileShare.ReadWrite))
            using (var sw = new StreamWriter(stream)) await writer(sw);
        }
        catch (Exception ex)
        {
            Log.Debug("File Utils", $"WriteFileAsync() :: Could not write file '{filePath}' due to an exception:\n{ex}");
        }
    }
}