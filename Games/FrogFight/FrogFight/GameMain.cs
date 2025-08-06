using Base;
using FrogFight.Screens;
using MonoGame.Extended.Screens;
using System;
using System.IO;

//https://badecho.com/index.php/2023/09/29/msdf-fonts-2/
//https://github.com/craftworkgames/MonoGame.Squid
//https://github.com/rive-app/rive-sharp
//https://docs.flatredball.com/gum/code/monogame
//Monogame extended uses GUM gui

namespace FrogFight
{
  public class GameMain() : BaseGame("FrogFight")
  {
    //private StreamWriter writer;

    //var file = new FileStream("C:\\Users\\Johan\\source\\test.txt", FileMode.OpenOrCreate);
    //writer = new StreamWriter(file);
    //writer.AutoFlush = true;
    //Console.SetOut(writer);

    protected override void LoadInitialScreen(ScreenManager screenManager)
    {
      _screenManager.LoadScreen(new MainMenu(this));

      base.LoadInitialScreen(screenManager);
    }
  }
}
