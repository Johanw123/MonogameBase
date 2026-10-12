using System.IO.Compression;

static class ItchPackage
{
  public static void Create(string directory, string archive)
  {
    directory = Path.GetFullPath(directory);
    archive = Path.GetFullPath(archive);
    if (!File.Exists(Path.Combine(directory, "index.html")))
      throw new InvalidDataException("Expected the published wwwroot folder with index.html.");
    var bundled = new HashSet<string>(StringComparer.Ordinal);
    string bundlePath = Path.Combine(directory, "Content/assets.zip");
    if (File.Exists(bundlePath))
      using (var bundle = ZipFile.OpenRead(bundlePath))
        foreach (var entry in bundle.Entries) bundled.Add(entry.FullName);
    var allFiles = Directory.GetFiles(directory, "*", SearchOption.AllDirectories);
    // Blazor publishes precompressed copies for servers that negotiate content
    // encoding. The standard browser loader also accepts the original files.
    // Keep standalone .gz/.br assets; omit only copies with an original present.
    var files = allFiles.Where(path => path != archive && !IsCompressionCopy(path)
      && !bundled.Contains(Path.GetRelativePath(directory, path).Replace('\\', '/')))
      .OrderBy(path => path, StringComparer.Ordinal).ToArray();
    if (files.Length > 1000)
      throw new InvalidDataException($"Itch ZIP would contain {files.Length} files; the limit is 1000.");
    Directory.CreateDirectory(Path.GetDirectoryName(archive)!);
    string temporary = archive + "." + Guid.NewGuid().ToString("N") + ".tmp";
    try
    {
      using (var zip = ZipFile.Open(temporary, ZipArchiveMode.Create))
        foreach (string path in files)
          zip.CreateEntryFromFile(path, Path.GetRelativePath(directory, path).Replace('\\', '/'), CompressionLevel.Optimal);
      File.Move(temporary, archive, overwrite: true);
    }
    finally
    {
      if (File.Exists(temporary)) File.Delete(temporary);
    }
    Console.WriteLine($"Itch ZIP: {archive} ({files.Length} files; omitted {allFiles.Count(IsCompressionCopy)} compression copies).");
  }

  private static bool IsCompressionCopy(string path)
    => Path.GetExtension(path) is ".gz" or ".br" && File.Exists(Path.ChangeExtension(path, null));
}
