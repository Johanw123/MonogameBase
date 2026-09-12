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
