#if !KNI_WEB
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace UntitledGemGame;

internal static class SoundEffectImport
{
  private const int MaxWaveBytes = 64 * 1024 * 1024;

  // File IO and format conversion happen off the game thread. OpenAL objects are created on it.
  internal static async Task<byte[]> ReadWaveAsync(string path)
  {
    path = Path.GetFullPath(path.Trim().Trim('"'));
    if (!File.Exists(path)) throw new FileNotFoundException("Sound file not found.", path);
    if (new FileInfo(path).Length > MaxWaveBytes) throw new InvalidDataException("Choose a sound file smaller than 64 MB.");
    var bytes = await File.ReadAllBytesAsync(path).ConfigureAwait(false);
    if (IsPcmWave(bytes)) return bytes;

    string temporary = Path.Combine(Path.GetTempPath(), "btb-sfx-" + Guid.NewGuid().ToString("N") + ".wav");
    try
    {
      var start = new ProcessStartInfo("ffmpeg")
      {
        UseShellExecute = false, CreateNoWindow = true,
        RedirectStandardError = true, RedirectStandardOutput = true
      };
      foreach (string argument in new[] { "-nostdin", "-hide_banner", "-loglevel", "error", "-y", "-i", path,
        "-vn", "-map", "0:a:0", "-acodec", "pcm_s16le", "-ar", "44100", "-ac", "2", temporary })
        start.ArgumentList.Add(argument);
      using var process = Process.Start(start) ?? throw new IOException("Could not start FFmpeg.");
      var errors = process.StandardError.ReadToEndAsync();
      var output = process.StandardOutput.ReadToEndAsync();
      using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
      try { await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false); }
      catch (OperationCanceledException)
      {
        process.Kill(entireProcessTree: true);
        await process.WaitForExitAsync().ConfigureAwait(false);
        throw new IOException("Sound conversion took longer than 30 seconds.");
      }
      string detail = await errors.ConfigureAwait(false);
      await output.ConfigureAwait(false);
      if (process.ExitCode != 0) throw new InvalidDataException("FFmpeg could not decode this sound: " + detail.Trim());
      if (new FileInfo(temporary).Length > MaxWaveBytes) throw new InvalidDataException("Decoded sound exceeds 64 MB.");
      return await File.ReadAllBytesAsync(temporary).ConfigureAwait(false);
    }
    catch (Win32Exception error)
    {
      throw new IOException("This format needs FFmpeg on PATH. Install FFmpeg or use a mono/stereo 16-bit PCM WAV.", error);
    }
    finally { if (File.Exists(temporary)) File.Delete(temporary); }
  }

  private static bool IsPcmWave(byte[] bytes)
  {
    if (bytes.Length < 44 || !bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8)
      || !bytes.AsSpan(8, 4).SequenceEqual("WAVE"u8)) return false;
    for (int offset = 12; offset <= bytes.Length - 8;)
    {
      uint length = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset + 4));
      if (length > bytes.Length - offset - 8) return false;
      if (bytes.AsSpan(offset, 4).SequenceEqual("fmt "u8) && length >= 16)
      {
        var format = bytes.AsSpan(offset + 8);
        int encoding = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(format);
        int channels = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(format[2..]);
        int rate = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(format[4..]);
        int bits = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(format[14..]);
        return encoding == 1 && channels is 1 or 2 && rate is >= 8000 and <= 48000 && bits == 16;
      }
      long next = offset + 8L + length + (length & 1);
      if (next > bytes.Length) return false;
      offset = (int)next;
    }
    return false;
  }
}
#endif
