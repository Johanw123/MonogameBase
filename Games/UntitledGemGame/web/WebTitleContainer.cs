using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Platform;

namespace UntitledGemGame.Web;

internal sealed class WebTitleContainerFactory : TitleContainerFactory
{
  public override TitleContainerStrategy CreateTitleContainerStrategy() => new WebTitleContainer();
}

// KNI's default Blazor strategy uses synchronous XMLHttpRequest for every asset.
// Assets are already downloaded asynchronously by WebBootstrap; never fetch on a tick.
internal sealed class WebTitleContainer : TitleContainerStrategy
{
  public override string Location => string.Empty;
  public override TitlePlatform Platform => TitlePlatform.BlazorWASM;
  public override Stream PlatformOpenStream(string name)
  {
    string path = name.Replace('\\', '/').TrimStart('/');
    if (!File.Exists(path)) throw new FileNotFoundException("Web asset was not bundled: " + path, path);
    return File.OpenRead(path);
  }
}
