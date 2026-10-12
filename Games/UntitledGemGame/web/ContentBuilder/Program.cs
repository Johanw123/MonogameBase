using System.Diagnostics;
using System.IO.Enumeration;
using System.Text.Json;
using System.Text.RegularExpressions;

if (args.Length == 2 && args[0] == "--bundle-content")
{
  ContentBundle.Create(args[1]);
  return;
}

if (args.Length == 3 && args[0] == "--package-itch")
{
  ItchPackage.Create(args[1], args[2]);
  return;
}

if (args.Length < 3) throw new ArgumentException("Expected game directory, output directory and KNI MGCB.dll path.");
string game = Path.GetFullPath(args[0]), output = Path.GetFullPath(args[1]);
string source = Path.Combine(game, "Content");
Directory.CreateDirectory(output);
string strategy = File.ReadAllText(Path.Combine(game, "ContentBuilder/Builder/Builder.cs"));
strategy = Regex.Replace(strategy, @"/\*.*?\*/|//[^\r\n]*", "", RegexOptions.Singleline);
var build = new SortedSet<string>(StringComparer.Ordinal);
var copy = new SortedSet<string>(StringComparer.Ordinal);
var files = Directory.GetFiles(source, "*", SearchOption.AllDirectories)
  .Select(p => Path.GetRelativePath(source, p).Replace('\\', '/'))
  .Where(p => !p.StartsWith("bin/") && !p.StartsWith("obj/")).ToArray();
foreach (Match rule in Regex.Matches(strategy, "contentCollection\\.(IncludeCopy|Include)<WildcardRule>\\(\"([^\"]+)\"\\)"))
  Add(rule.Groups[2].Value, rule.Groups[1].Value == "IncludeCopy" ? copy : build);
// Browser fonts include the Asian OTF faces and all shipped translations.
Add("Fonts/*.otf", copy);
Add("Fonts/Licenses/*", copy);
Add("Localization/*.json", copy);
Add("Shaders/*.fx", build);
Add("Shaders/Shapes/*.fx", build);

void Add(string pattern, SortedSet<string> destination)
{
  foreach (var path in files)
    if (FileSystemName.MatchesSimpleExpression(pattern, path, ignoreCase: false)) destination.Add(path);
}
foreach (string path in copy)
{
  string destination = Path.Combine(output, path);
  Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
  File.Copy(Path.Combine(source, path), destination, overwrite: true);
}
File.WriteAllText(Path.Combine(output, "raw-assets.json"), JsonSerializer.Serialize(copy.Select(p => "Content/" + p)));
// MGCB resolves /build paths while reading the response file, whose folder
// becomes its default working directory. Set the source root before any /build.
var manifest = new List<string> { "/workingDir:" + source, "/platform:BlazorGL", "/profile:HiDef", "/compress:True" };
foreach (string path in build.Except(copy))
{
  string extension = Path.GetExtension(path);
  string importer = extension switch { ".png" => "TextureImporter", ".fx" => "EffectImporter", ".wav" => "WavImporter", ".ogg" => "OggImporter", _ => throw new InvalidDataException(path) };
  string processor = extension switch { ".png" => "TextureProcessor", ".fx" => "EffectProcessor", ".wav" => "SoundEffectProcessor", ".ogg" => "SongProcessor", _ => throw new InvalidDataException(path) };
  manifest.Add($"/importer:{importer}");
  manifest.Add($"/processor:{processor}");
  if (extension == ".png") manifest.Add("/processorParam:TextureFormat=Color");
  manifest.Add($"/build:{path}");
}
string intermediate = Path.Combine(game, "web/obj/content");
Directory.CreateDirectory(intermediate);
string response = Path.Combine(intermediate, "Content.mgcb");
File.WriteAllLines(response, manifest);
Console.WriteLine($"Web assets: {build.Except(copy).Count()} compiled, {copy.Count} raw files.");
if (args.Contains("--manifest-only")) return;
var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, WorkingDirectory = source };
if (!OperatingSystem.IsWindows())
{
  // KNI's effect/audio compiler uses Windows native libraries. The runtime
  // installed for MonoGame shader compilation can also run KNI's MGCB.
  string? prefix = Environment.GetEnvironmentVariable("MGFXC_WINE_PATH");
  if (string.IsNullOrEmpty(prefix)) throw new PlatformNotSupportedException("Web content compilation needs Windows, or Wine with MGFXC_WINE_PATH pointing to a Windows .NET 8 prefix. See web/README.md.");
  start.FileName = "wine";
  start.Environment["WINEPREFIX"] = prefix;
  start.Environment["WINEDEBUG"] = "-all";
  start.Environment["WINEDLLOVERRIDES"] = "d3dcompiler_47=n,explorer.exe=e,services.exe=f";
  start.ArgumentList.Add(Path.Combine(prefix, "drive_c/Program Files/dotnet/dotnet.exe"));
}
start.ArgumentList.Add(Path.GetFullPath(args[2]));
start.ArgumentList.Add("/@:" + response);
start.ArgumentList.Add("/outputDir:" + output);
start.ArgumentList.Add("/intermediateDir:" + intermediate);
using var compiler = Process.Start(start) ?? throw new InvalidOperationException("Could not start KNI's content compiler.");
compiler.WaitForExit();
if (compiler.ExitCode != 0)
{
  Console.Error.WriteLine($"KNI content compilation failed ({compiler.ExitCode}).");
  Environment.ExitCode = compiler.ExitCode;
}
