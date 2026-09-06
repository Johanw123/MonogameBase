using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;

namespace UntitledGemGame
{
  public sealed class GameSave
  {
    [JsonRequired] public int Version { get; set; } = 1;
    [JsonRequired] public Dictionary<string, int> Upgrades { get; set; } = new();
    [JsonRequired] public Dictionary<string, int> Abilities { get; set; } = new();
    [JsonRequired] public Dictionary<string, int> Meta { get; set; } = new();
    [JsonRequired] public ulong RedGems { get; set; }
    [JsonRequired] public ulong BlueGems { get; set; }
    [JsonRequired] public ulong PurpleGems { get; set; }
    [JsonRequired] public ulong RedGemsEarnedThisRun { get; set; }
    public bool PostPrestige { get; set; }
    public bool CreatedInitialGems { get; set; }
    public List<string> EquippedAbilities { get; set; } = new();
  }

  [JsonSourceGenerationOptions(WriteIndented = true)]
  [JsonSerializable(typeof(GameSave))]
  internal partial class GameSaveContext : JsonSerializerContext { }

  public sealed class GameSaveStore
  {
    public static string DefaultPath => Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
      "UntitledGemGame", "progress.json");

    public string SavePath { get; }
    public bool CanSave { get; private set; } = true;
    public string Error { get; private set; }
    private bool preserveBackup;

    public GameSaveStore(string path) => SavePath = path;

    public GameSave Load()
    {
      CanSave = true;
      Error = null;
      preserveBackup = false;
      foreach (string path in new[] { SavePath, SavePath + ".bak" })
      {
        if (!File.Exists(path)) continue;
        try
        {
          string json = File.ReadAllText(path);
          using var document = JsonDocument.Parse(json);
          if (document.RootElement.ValueKind == JsonValueKind.Object
            && document.RootElement.TryGetProperty("Version", out var version)
            && version.ValueKind == JsonValueKind.Number && version.TryGetInt32(out int versionNumber) && versionNumber != 1)
          {
            // Check the version before decoding fields that a newer schema may have changed.
            CanSave = false;
            Error = "This save needs a different game version. Progress saving is disabled.";
            Log.Error("Unsupported save version {Version} at {Path}; saving disabled", versionNumber, path);
            return null;
          }
          var save = JsonSerializer.Deserialize(json, GameSaveContext.Default.GameSave);
          if (save == null || save.Upgrades == null || save.Abilities == null || save.Meta == null
            || save.EquippedAbilities == null)
            throw new InvalidDataException("The save is missing progress data.");
          preserveBackup = path != SavePath;
          Log.Information("Loaded progress from {Path}", path);
          return save;
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is JsonException || e is InvalidDataException)
        {
          Log.Warning(e, "Could not load progress from {Path}", path);
        }
      }

      // Keep unreadable files available for recovery instead of overwriting them.
      CanSave = !File.Exists(SavePath) && !File.Exists(SavePath + ".bak");
      if (!CanSave)
        Error = "Saved progress could not be loaded. Saving is disabled to protect your save files.";
      return null;
    }

    public bool Save(GameSave save)
    {
      if (!CanSave) return false;
      try
      {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(SavePath))!);
        string temporaryPath = SavePath + ".tmp";
        using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
          JsonSerializer.Serialize(stream, save, GameSaveContext.Default.GameSave);
          stream.Flush(flushToDisk: true);
        }

        // Keep the last complete save and replace the primary file atomically.
        if (File.Exists(SavePath) && !preserveBackup)
          File.Replace(temporaryPath, SavePath, SavePath + ".bak");
        else
          File.Move(temporaryPath, SavePath, overwrite: true);
        preserveBackup = false;
        Error = null;
        return true;
      }
      catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
      {
        Error = "Progress could not be saved. Check disk space and folder permissions.";
        Log.Error(e, "Could not save progress to {Path}", SavePath);
        return false;
      }
    }
  }
}
