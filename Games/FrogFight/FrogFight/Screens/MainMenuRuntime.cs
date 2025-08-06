using Gum.Converters;
using Gum.DataTypes;
using Gum.Managers;
using Gum.Wireframe;
using MonoGameGum;
using MonoGameGum.GueDeriving;
using RenderingLibrary.Graphics;
using System.Linq;
using FrogFight;

partial class MainMenuRuntime
{
    partial void CustomInitialize()
    {
      ButtonPlay.FormsControl.Click += (_, _) =>
      {
        GumService.Default.Root.Children.Clear();

        var screen = GameMain.GumProject.GetScreenSave("LobbyBrowser");
        var screenRuntime = screen.ToGraphicalUiElement();
        screenRuntime.AddToRoot();
      };

      ButtonExit.FormsControl.Click += (_, _) =>
      {
        System.Environment.Exit(0);
      };
  }
}
