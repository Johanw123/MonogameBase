using Base;
using FrogFight.Screens;
using Gum.Forms;
using Gum.Forms.Controls;
using MonoGame.Extended.Screens;
using MonoGameGum;
using System;
using System.IO;
using System.Linq;
using FrogFight.Scenes;
using Gum.DataTypes;

//https://badecho.com/index.php/2023/09/29/msdf-fonts-2/
//https://github.com/craftworkgames/MonoGame.Squid
//https://github.com/rive-app/rive-sharp
//https://docs.flatredball.com/gum/code/monogame
//Monogame extended uses GUM gui

namespace FrogFight
{
  public class GameMain() : BaseGame("FrogFight", 1280, 720)
  {
    //private StreamWriter writer;

    //var file = new FileStream("C:\\Users\\Johan\\source\\test.txt", FileMode.OpenOrCreate);
    //writer = new StreamWriter(file);
    //writer.AutoFlush = true;
    //Console.SetOut(writer);
    GumService GumUI => GumService.Default;

    public static GumProjectSave GumProject;

    protected override void LoadInitialScreen(ScreenManager screenManager)
    {
      //_screenManager.LoadScreen(new MainMenu(this));
      _screenManager.LoadScreen(new TestScene(this));

      base.LoadInitialScreen(screenManager);
    }

    protected override void Initialize()
    {
      base.Initialize();

      GumProject = GumUI.Initialize(
        this,
        "GumProject/GumProjectTest.gumx");


      var screen = GumProject.GetScreenSave("MainMenu");
      var screenRuntime = screen.ToGraphicalUiElement();
      screenRuntime.AddToRoot();

      
      //GumUI.Initialize(this, DefaultVisualsVersion.V2);

      //var button = new Button();
      //button.AddToRoot();
      //button.Click += (_, _) =>
      //  button.Text = "Clicked at\n" + DateTime.Now;
    }
  }
}
