using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Serilog;
using UntitledGemGame;

// Source-controlled event assignments. Audio files are compiled by both content builders.
public static class SoundEffectDefaults
{
  public const string ManifestPath = "SFX/EventDefaults.json";
  public const string ImportedDirectory = "SFX/EventOverrides";

  public static Dictionary<string, SoundCue[]> Read(Stream stream)
  {
    var defaults = JsonSerializer.Deserialize(stream, SoundEffectDefaultsJson.Default.DictionaryStringSoundCueArray)
      ?? throw new InvalidDataException("Sound defaults must be a JSON object.");
    foreach (var (name, cues) in defaults)
    {
      if (!Enum.TryParse<SoundEvent>(name, out var soundEvent) || !Enum.IsDefined(soundEvent)
        || cues == null || cues.Length == 0)
        throw new InvalidDataException($"Invalid sound default: {name}");
      foreach (var cue in cues)
      {
        ValidatePath(cue.Path);
        ValidatePath(cue.FallbackPath);
        if (!float.IsFinite(cue.Volume) || cue.Volume < 0f
          || !float.IsFinite(cue.FallbackVolume) || cue.FallbackVolume < 0f
          || !float.IsFinite(cue.Pitch) || Math.Abs(cue.Pitch) > 1f
          || !float.IsFinite(cue.PitchJitter) || cue.PitchJitter < 0f || cue.PitchJitter > 1f
          || !float.IsFinite(cue.Pan) || Math.Abs(cue.Pan) > 1f
          || !float.IsFinite(cue.Priority) || cue.Priority < 0f || !Enum.IsDefined(cue.Type))
          throw new InvalidDataException($"Invalid sound parameters: {name}");
      }
    }
    return defaults;
  }

  private static void ValidatePath(string path)
  {
    if (path != null && (!path.StartsWith("SFX/", StringComparison.Ordinal) || path.Contains("..", StringComparison.Ordinal)
      || path.Contains('\\') || path.Contains(':')))
      throw new InvalidDataException($"Invalid sound path: {path}");
  }

  public static void Write(string contentDirectory, Dictionary<string, SoundCue[]> defaults)
  {
    string path = Path.Combine(contentDirectory, ManifestPath);
    Directory.CreateDirectory(Path.GetDirectoryName(path));
    string temporary = path + ".tmp";
    try
    {
      File.WriteAllText(temporary, JsonSerializer.Serialize(defaults, SoundEffectDefaultsJson.Default.DictionaryStringSoundCueArray) + Environment.NewLine);
      File.Move(temporary, path, overwrite: true);
    }
    finally { if (File.Exists(temporary)) File.Delete(temporary); }
  }
}

[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(Dictionary<string, SoundCue[]>))]
internal partial class SoundEffectDefaultsJson : JsonSerializerContext;

public partial class AudioManager
{
  private void LoadSoundDefaults()
  {
    try
    {
      Dictionary<string, SoundCue[]> defaults;
#if !KNI_WEB
      string sourceContent = Demo.IsDev ? DebugSoundEffects.FindSourceContentDirectory() : null;
      if (sourceContent != null)
      {
        using var stream = File.OpenRead(Path.Combine(sourceContent, SoundEffectDefaults.ManifestPath));
        defaults = SoundEffectDefaults.Read(stream);
      }
      else
#endif
      {
        using var stream = TitleContainer.OpenStream("Content/" + SoundEffectDefaults.ManifestPath);
        defaults = SoundEffectDefaults.Read(stream);
      }

      foreach (var (name, cues) in defaults)
      {
        var soundEvent = Enum.Parse<SoundEvent>(name);
#if !KNI_WEB
        // A saved assignment also works when launching an existing binary without rebuilding content.
        if (sourceContent != null)
        {
          try
          {
            foreach (var cue in cues)
            {
              string path = cue.Path;
              if (path?.StartsWith(SoundEffectDefaults.ImportedDirectory + "/", StringComparison.Ordinal) != true
                || _sounds.ContainsKey(path)) continue;
              using var wave = File.OpenRead(Path.Combine(sourceContent, path));
              var effect = Named(SoundEffect.FromStream(wave), path);
              _sounds[path] = effect;
              _debugStartupSounds.Add(effect);
            }
          }
          catch (Exception error)
          {
            Log.Warning(error, "Could not load saved sound for {Event}; using built-in assignment", name);
            continue;
          }
        }
#endif
        _eventCues[soundEvent] = cues;
      }
    }
    catch (Exception error)
    {
      Log.Warning(error, "Could not read sound event defaults; using built-in assignments");
    }
#if !KNI_WEB
    foreach (var pair in _eventCues) _startupCues[pair.Key] = pair.Value;
#endif
  }
}
