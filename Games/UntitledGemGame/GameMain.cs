using Base;
using UntitledGemGame.Screens;
using MonoGame.Extended.Screens;

//https://badecho.com/index.php/2023/09/29/msdf-fonts-2/
//https://github.com/craftworkgames/MonoGame.Squid
//https://github.com/rive-app/rive-sharp
//https://docs.flatredball.com/gum/code/monogame
//Monogame extended uses GUM gui

namespace UntitledGemGame
{
  public class GameMain() : BaseGame("UntitledGemGame", targetFps: 165.0f, fixedTimeStep: true)
  {
    protected override void LoadInitialScreen(ScreenManager screenManager)
    {
      _screenManager.LoadScreen(new MainMenu(this));

      base.LoadInitialScreen(screenManager);
    }
  }
}
