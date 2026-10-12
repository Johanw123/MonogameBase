using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.IO.Compression;
using System.Net.Http;

using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace UntitledGemGame.Web;

internal static class WebBootstrap
{
  public static int CanvasWidth { get; set; } = 800;
  public static int CanvasHeight { get; set; } = 600;
  public static long FrameIndex { get; set; }
  public static bool AudioActivated { get; set; }
  private static readonly Dictionary<string, string> saved = new();
  private static readonly Dictionary<string, string> paths = new()
  {
    ["progress"] = GameSaveStore.DefaultPath,
    ["backup"] = GameSaveStore.DefaultPath + ".bak",
    ["settings"] = GameMain.GetPath("Settings.json")
  };

  public static async Task PrepareAsync(HttpClient http, IJSRuntime js)
  {
    SetLoading("Downloading game assets…");
    await js.InvokeVoidAsync("btbLoading", LoadingMessage, LoadingProgress);
    using var response = await http.GetAsync("Content/assets.zip", HttpCompletionOption.ResponseHeadersRead);
    response.EnsureSuccessStatusCode();
    using var bytes = new MemoryStream();
    using (var stream = await response.Content.ReadAsStreamAsync())
    {
      var buffer = new byte[128 * 1024];
      long total = response.Content.Headers.ContentLength ?? 0;
      int count;
      while ((count = await stream.ReadAsync(buffer)) > 0)
      {
        bytes.Write(buffer, 0, count);
        SetLoading("Downloading game assets…", total > 0 ? (double)bytes.Length / total : 0);
        await js.InvokeVoidAsync("btbLoading", LoadingMessage, LoadingProgress);
        await Task.Delay(1); // Yield even when the HTTP response was buffered.
      }
    }
    bytes.Position = 0;
    using (var bundle = new ZipArchive(bytes, ZipArchiveMode.Read))
    {
      int completed = 0;
      foreach (var entry in bundle.Entries)
      {
        string path = entry.FullName;
        if (!path.StartsWith("Content/", StringComparison.Ordinal) || path.Split('/').Contains(".."))
          throw new InvalidDataException("Invalid bundled asset path: " + path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using (var source = entry.Open())
        using (var destination = File.Create(path))
          await source.CopyToAsync(destination);
        if (++completed % 8 == 0 || completed == bundle.Entries.Count)
        {
          SetLoading("Unpacking game assets…", (double)completed / bundle.Entries.Count);
          await js.InvokeVoidAsync("btbLoading", LoadingMessage, LoadingProgress);
          await Task.Delay(1);
        }
      }
    }
    var stored = await js.InvokeAsync<Dictionary<string, string>>("btbStorage.read");
    foreach (var entry in stored)
    {
      if (!paths.TryGetValue(entry.Key, out var path)) continue;
      Directory.CreateDirectory(Path.GetDirectoryName(path)!);
      File.WriteAllText(path, entry.Value);
      saved[entry.Key] = entry.Value;
    }
    SetLoading("Preparing the main menu…");
  }

  public static string LoadingMessage { get; private set; } = "Loading Beyond the Belt…";
  public static double LoadingProgress { get; private set; }
  public static void SetLoading(string message, double progress = 0)
  {
    LoadingMessage = message;
    LoadingProgress = Math.Clamp(progress, 0, 1);
  }

  // Synchronous JS interop also lets beforeunload flush the latest game save.
  public static void Persist(IJSRuntime js)
  {
    if (js is not IJSInProcessRuntime browser) return;
    foreach (var entry in paths)
    {
      string value = File.Exists(entry.Value) ? File.ReadAllText(entry.Value) : null;
      if (saved.GetValueOrDefault(entry.Key) == value) continue;
      if (browser.Invoke<bool>("btbStorage.write", entry.Key, value)) saved[entry.Key] = value;
    }
  }
}
