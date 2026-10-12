using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Platform;
using Microsoft.Xna.Platform.Audio;
using Microsoft.Xna.Platform.Graphics;
using Microsoft.Xna.Platform.Input;
using Microsoft.Xna.Platform.Media;
using System;
using System.Threading.Tasks;
using UntitledGemGame.Web;
using UntitledGemGame;

namespace UntitledGemGame.web.Pages
{
  public partial class Index
  {
    Game _game;
    DateTime lastPersist;
    string lastLoadingMessage;
    double lastLoadingProgress = -1;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
      if (!firstRender) return;
      try
      {
        using var http = new System.Net.Http.HttpClient { BaseAddress = new Uri(Navigation.BaseUri) };
        await WebBootstrap.PrepareAsync(http, JsRuntime);
        await JsRuntime.InvokeVoidAsync("initRenderJS", DotNetObjectReference.Create(this));
      }
      catch (Exception error)
      {
        Console.Error.WriteLine(error);
        await JsRuntime.InvokeVoidAsync("btbShowError", "Could not load the game. Reload to try again.");
      }
    }

    [JSInvokable]
    public void EnableAudio() => WebBootstrap.AudioActivated = true;

    [JSInvokable]
    public void Resize(int width, int height)
    {
      WebBootstrap.CanvasWidth = Math.Max(1, width);
      WebBootstrap.CanvasHeight = Math.Max(1, height);
      if (_game is GameMain game) game.ResizeBrowser(WebBootstrap.CanvasWidth, WebBootstrap.CanvasHeight);
    }

    [JSInvokable]
    public void PersistProgress()
    {
      Screens.UntitledGemGameGameScreen.Instance?.SaveProgress();
      WebBootstrap.Persist(JsRuntime);
    }

    [JSInvokable]
    public void TickDotNet()
    {
      try
      {
        // init game
        if (_game == null)
        {
          // 1. Register the factory BEFORE running the game
          // Note: 'ConcreteGameFactory' should be provided by your KNI/Apos project template.
          // If you don't have it, ensure you have the correct 'using' statement for it.
          GameFactory.RegisterGameFactory(new ConcreteGameFactory());
          InputFactory.RegisterInputFactory(new ConcreteInputFactory());
          GraphicsFactory.RegisterGraphicsFactory(new ConcreteGraphicsFactory());
          AudioFactory.RegisterAudioFactory(new ConcreteAudioFactory());
          TitleContainerFactory.RegisterTitleContainerFactory(new WebTitleContainerFactory());
          MediaFactory.RegisterMediaFactory(new ConcreteMediaFactory());


          Console.WriteLine("Creating and running GameMain");

          _game = new GameMain();
          _game.Run();
          WebBootstrap.SetLoading(null);
        }

        // run gameloop
        WebBootstrap.FrameIndex++;
        _game.Tick();
        if (lastLoadingMessage != WebBootstrap.LoadingMessage || lastLoadingProgress != WebBootstrap.LoadingProgress)
        {
          lastLoadingMessage = WebBootstrap.LoadingMessage;
          lastLoadingProgress = WebBootstrap.LoadingProgress;
          JsRuntime.InvokeVoidAsync("btbLoading", lastLoadingMessage, lastLoadingProgress);
        }
        if ((DateTime.UtcNow - lastPersist).TotalSeconds >= 1)
        {
          WebBootstrap.Persist(JsRuntime);
          lastPersist = DateTime.UtcNow;
        }
      }
      catch (Exception ex)
      {
        JsRuntime.InvokeVoidAsync("btbShowError", "The game stopped unexpectedly. Reload to try again; details are in the browser console.");
        Console.WriteLine("CRASH IN TICK: " + ex.Message);
        Console.WriteLine(ex.StackTrace);
        throw;
      }
    }

  }
}
