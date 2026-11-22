#if KNI_WEB
using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using UntitledGemGame.web;

#else
using System.Threading.Tasks;
#endif

namespace UntitledGemGame
{
  internal class Program
  {
    private static async Task Main(string[] args)
    {
#if KNI_WEB
      var builder = WebAssemblyHostBuilder.CreateDefault(args);
      builder.RootComponents.Add<App>("#app");
      builder.RootComponents.Add<HeadOutlet>("head::after");      
      builder.Services.AddScoped(sp => new HttpClient()
      {
        BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
      });
      await builder.Build().RunAsync();
#else
      TweenRuntime.Initialize();
      if (System.Array.IndexOf(args, "--tween-check") >= 0)
      {
        TweenRuntime.Check();
        return;
      }

      // Marketing footage: --capture scene.json [--overwrite] or --capture-list catalog.json.
      // See Marketing/Skills/game-shorts. Runs offscreen without Steam or the player's save.
      if (System.Array.IndexOf(args, "--capture") >= 0 || System.Array.IndexOf(args, "--capture-list") >= 0)
      {
        if (!Demo.IsDev)
        {
          System.Console.Error.WriteLine("Capture mode is only available in development builds.");
          System.Environment.ExitCode = 2;
          return;
        }
        try
        {
          Capture.CaptureSession.Configure(args);
        }
        catch (System.Exception e) when (e is System.ArgumentException or System.IO.IOException or System.Text.Json.JsonException)
        {
          System.Console.Error.WriteLine($"CAPTURE FAILED: {e.Message}");
          System.Environment.ExitCode = 2;
          return;
        }
        using (var captureGame = new UntitledGemGame.GameMain())
          captureGame.Run();
        System.Environment.ExitCode = Capture.CaptureSession.ExitCode;
        return;
      }

      // Logged human playthrough: --record-run <dir>. See Capture/RunRecording.cs and tools/record_run.py.
      // Skips Steam, so its overlay and notifications stay out of the recording.
      int recordRun = System.Array.IndexOf(args, "--record-run");
      if (recordRun >= 0)
      {
        if (recordRun + 1 >= args.Length)
        {
          System.Console.Error.WriteLine("--record-run needs a folder for the run's save and log.");
          System.Environment.ExitCode = 2;
          return;
        }
        try
        {
          Capture.RunRecording.Configure(args[recordRun + 1]);
        }
        catch (System.Exception e) when (e is System.IO.IOException or System.UnauthorizedAccessException)
        {
          System.Console.Error.WriteLine($"RECORD RUN FAILED: {e.Message}");
          System.Environment.ExitCode = 2;
          return;
        }
        try
        {
          using var recordedGame = new UntitledGemGame.GameMain();
          recordedGame.Run();
        }
        finally
        {
          Capture.RunRecording.Close();
        }
        return;
      }

      using var platform = Platform.SteamPlatformServices.Start(out var restartRequested);
      if (restartRequested)
        return;

      Platform.GameServices.Attach(platform);

      if (System.Array.IndexOf(args, "--steam-check") >= 0)
      {
        System.Environment.ExitCode = Platform.SteamDiagnostics.Run(platform) ? 0 : 1;
        return;
      }

      using var game = new UntitledGemGame.GameMain();
      game.PlatformServices = platform;
      game.Run();
#endif
    }
  }
}
