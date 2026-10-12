#if !KNI_WEB
using System;
using System.ComponentModel;
using System.Diagnostics;

namespace UntitledGemGame.Platform;

internal static class DesktopBrowser
{
  public static void Open(string url)
  {
    try
    {
      using var browser = Process.Start(new ProcessStartInfo(url)
      {
        UseShellExecute = true,
        WindowStyle = ProcessWindowStyle.Normal
      });
    }
    catch (Exception error) when (IsLaunchError(error))
    {
      Console.WriteLine($"[Wishlist] Could not open the Steam store in a browser: {error.Message}");
    }
  }

  private static bool IsLaunchError(Exception error)
    => error is Win32Exception or InvalidOperationException or PlatformNotSupportedException;
}
#endif
