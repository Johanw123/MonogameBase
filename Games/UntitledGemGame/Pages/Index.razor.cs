using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Platform;
using Microsoft.Xna.Platform.Audio;
using Microsoft.Xna.Platform.Graphics;
using Microsoft.Xna.Platform.Input;
using Microsoft.Xna.Platform.Media;
using System;
using UntitledGemGame;

namespace UntitledGemGame.web.Pages
{
  public partial class Index
  {
    Game _game;

    protected override void OnAfterRender(bool firstRender)
    {
      base.OnAfterRender(firstRender);

      if (firstRender)
      {
        JsRuntime.InvokeAsync<object>("initRenderJS", DotNetObjectReference.Create(this));
      }
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
          TitleContainerFactory.RegisterTitleContainerFactory(new ConcreteTitleContainerFactory());
          MediaFactory.RegisterMediaFactory(new ConcreteMediaFactory());


          Console.WriteLine("Creating and running GameMain");

          _game = new GameMain();
          _game.Run();
        }

        // run gameloop
        _game.Tick();
      }
      catch (Exception ex)
      {
        Console.WriteLine("CRASH IN TICK: " + ex.Message);
        Console.WriteLine(ex.StackTrace);
        throw;
      }
    }

  }
}
