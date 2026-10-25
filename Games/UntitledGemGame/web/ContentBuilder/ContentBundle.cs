using System.IO.Compression;

// Everything loaded through ContentManager/Gum is local before the game starts.
// Legacy Song assets remain URLs. Radio stems are decoded locally in sync.
static class ContentBundle
{
  public static void Create(string directory)
  {
    directory = Path.GetFullPath(directory);
    string output = Path.Combine(directory, "assets.zip");
    var files = Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
      .Where(path => path != output && path != output + ".tmp"
        && (Path.GetExtension(path) is not (".mp3" or ".ogg")
          || Path.GetRelativePath(directory, path).Replace('\\', '/').StartsWith("Music/Radio/", StringComparison.Ordinal)))
      .OrderBy(path => path, StringComparer.Ordinal).ToArray();
    if (!files.Any(path => Path.GetExtension(path) == ".xnb"))
      throw new InvalidDataException("Compile web content before creating the asset bundle.");
    string temporary = output + ".tmp";
    try
    {
      using (var zip = ZipFile.Open(temporary, ZipArchiveMode.Create))
        foreach (string path in files)
          zip.CreateEntryFromFile(path, "Content/" + Path.GetRelativePath(directory, path).Replace('\\', '/'), CompressionLevel.Optimal);
      File.Move(temporary, output, overwrite: true);
    }
    finally { if (File.Exists(temporary)) File.Delete(temporary); }
    Console.WriteLine($"Web asset bundle: {files.Length} files, {new FileInfo(output).Length / 1048576.0:0.0} MB.");
  }
}
