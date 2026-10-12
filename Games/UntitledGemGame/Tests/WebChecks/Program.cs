using System.Net;
using System.IO.Compression;
using System.Text.Json;
using Microsoft.JSInterop;
using UntitledGemGame;
using UntitledGemGame.Web;

if (args.Length == 2 && args[0] == "--menu-rendering")
{
  MenuRenderingChecks.Run(args[1]);
  return;
}

if (args.Length == 1 && args[0] == "--preloader")
{
  PreloaderChecks.Run();
  return;
}

if (args.Length == 3 && args[0] == "--shader-profiles")
{
  ShaderProfileChecks.Run(args[1], args[2]);
  return;
}
if (args.Length == 3 && args[0] == "--field-fonts")
{
  FieldFontChecks.Run(args[1], args[2]);
  return;
}
if (args.Length == 2 && args[0] == "--blend-state")
{
  BlendStateChecks.Run(args[1]);
  return;
}

string originalDirectory = Environment.CurrentDirectory;
string scratch = Path.Combine(Path.GetTempPath(), "btb-web-check-" + Guid.NewGuid());
Directory.CreateDirectory(scratch);
Environment.CurrentDirectory = scratch;
int checks = 0;
void Check(bool result, string name) { if (!result) throw new Exception(name); checks++; }
try
{
  var browser = new Browser();
  var handler = new Files();
  using var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/game/") };
  await WebBootstrap.PrepareAsync(http, browser);
  Check(File.ReadAllText("Content/Fonts/Test Font.ttf") == "asset", "Stages raw assets for synchronous game loaders");
  Check(handler.Requests.SequenceEqual(new[] { "/game/Content/assets.zip" }), "Downloads one bundle using the hosting base path");
  Check(File.ReadAllText("Content/Textures/Test.xnb") == "compiled", "Stages compiled assets before game ticks");
  Check(browser.ProgressUpdates > 0, "Reports download and unpacking progress");
  Check(File.ReadAllText(GameSaveStore.DefaultPath) == "progress-json", "Restores browser progress before game startup");
  Check(File.ReadAllText(GameMain.GetPath("Settings.json")) == "settings-json", "Restores browser preferences");
  WebBootstrap.Persist(browser);
  Check(browser.Writes == 0, "Does not repeatedly write unchanged saves");
  File.WriteAllText(GameSaveStore.DefaultPath, "updated");
  WebBootstrap.Persist(browser);
  Check(browser.Values["progress"] == "updated" && browser.Writes == 1, "Persists updated progress");
  browser.AcceptWrites = false;
  File.WriteAllText(GameSaveStore.DefaultPath, "retry");
  WebBootstrap.Persist(browser);
  browser.AcceptWrites = true;
  WebBootstrap.Persist(browser);
  Check(browser.Values["progress"] == "retry", "Retries failed browser storage writes");
  File.Delete(GameSaveStore.DefaultPath);
  WebBootstrap.Persist(browser);
  Check(!browser.Values.ContainsKey("progress"), "Persists save deletion");
  handler.FailAssets = true;
  bool failed = false;
  try { await WebBootstrap.PrepareAsync(http, browser); } catch (HttpRequestException) { failed = true; }
  Check(failed, "Missing asset bundle stops startup with an error");
  handler.FailAssets = false;
  handler.BadPath = true;
  failed = false;
  try { await WebBootstrap.PrepareAsync(http, browser); } catch (InvalidDataException) { failed = true; }
  Check(failed && !File.Exists("outside.txt"), "Bundle entries cannot escape Content");
  Console.WriteLine($"Passed {checks} browser startup and persistence checks.");
}
finally { Environment.CurrentDirectory = originalDirectory; Directory.Delete(scratch, true); }

sealed class Files : HttpMessageHandler
{
  public readonly List<string> Requests = new();
  public bool FailAssets;
  public bool BadPath;
  protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
  {
    Requests.Add(request.RequestUri.PathAndQuery);
    using var bytes = new MemoryStream();
    using (var zip = new ZipArchive(bytes, ZipArchiveMode.Create, leaveOpen: true))
    {
      void Add(string path, string value)
      {
        using var writer = new StreamWriter(zip.CreateEntry(path).Open());
        writer.Write(value);
      }
      Add(BadPath ? "Content/../outside.txt" : "Content/Fonts/Test Font.ttf", "asset");
      Add("Content/Textures/Test.xnb", "compiled");
    }
    return Task.FromResult(new HttpResponseMessage(FailAssets ? HttpStatusCode.NotFound : HttpStatusCode.OK)
    {
      Content = new ByteArrayContent(bytes.ToArray())
    });
  }
}
sealed class Browser : IJSInProcessRuntime
{
  public readonly Dictionary<string, string> Values = new() { ["progress"] = "progress-json", ["settings"] = "settings-json" };
  public bool AcceptWrites = true;
  public int Writes;
  public int ProgressUpdates;
  public TValue Invoke<TValue>(string identifier, params object[] args)
  {
    if (identifier == "btbLoading") { ProgressUpdates++; return default; }
    if (identifier == "btbStorage.read") return (TValue)(object)new Dictionary<string, string>(Values);
    Writes++;
    if (AcceptWrites) { if (args[1] == null) Values.Remove((string)args[0]); else Values[(string)args[0]] = (string)args[1]; }
    return (TValue)(object)AcceptWrites;
  }
  public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object[] args) => new(Invoke<TValue>(identifier, args));
  public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken token, object[] args) => InvokeAsync<TValue>(identifier, args);
}
namespace UntitledGemGame
{
  static class GameSaveStore { public static string DefaultPath => Path.Combine(Environment.CurrentDirectory, "saves/progress.json"); }
  static class GameMain { public static string GetPath(string name) => Path.Combine(Environment.CurrentDirectory, name); }
}
