namespace MyList.Helpers;

public interface IFileSystemProbe
{
    bool FileExists(string path);
    bool DirectoryExists(string path);
}

public sealed class FileSystemProbe : IFileSystemProbe
{
    public static readonly FileSystemProbe Default = new();

    public bool FileExists(string path)
    {
        try { return System.IO.File.Exists(path); }
        catch (System.IO.IOException) { return false; }
        catch (System.UnauthorizedAccessException) { return false; }
    }

    public bool DirectoryExists(string path)
    {
        try { return System.IO.Directory.Exists(path); }
        catch (System.IO.IOException) { return false; }
        catch (System.UnauthorizedAccessException) { return false; }
    }
}
