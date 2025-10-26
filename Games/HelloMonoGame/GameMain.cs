using HelloMonoGame.Screens;
using JapeFramework;
using MonoGame.Extended.Screens;

//https://badecho.com/index.php/2023/09/29/msdf-fonts-2/
//https://github.com/craftworkgames/MonoGame.Squid
//https://github.com/rive-app/rive-sharp
//https://docs.flatredball.com/gum/code/monogame
//Monogame extended uses GUM gui

namespace HelloMonoGame
{
  public class GameMain() : BaseGame("HelloMonoGame")
  {
    protected override void LoadInitialScreen(ScreenManager screenManager)
    {
      _screenManager.LoadScreen(new MainMenu(this));

      base.LoadInitialScreen(screenManager);
    }
  }
}
