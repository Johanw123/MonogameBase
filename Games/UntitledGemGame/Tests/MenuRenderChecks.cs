using System.Reflection;
using Gum;
using Gum.Forms.DefaultFromFileVisuals;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RenderingLibrary;
using UntitledGemGame;

// Loads the real menu and textures without starting GameMain or touching player saves.
internal sealed class MenuRenderChecks : Game
{
  private readonly string output;
  private readonly Type theme = typeof(GameMain).Assembly.GetType("UntitledGemGame.MenuTheme")!;
  private Gum.DataTypes.GumProjectSave project = null!;

  public MenuRenderChecks(string content, string output)
  {
    Content.RootDirectory = Path.GetFullPath(content);
    this.output = Path.GetFullPath(output);
    _ = new GraphicsDeviceManager(this) { PreferredBackBufferWidth = 1920, PreferredBackBufferHeight = 1080 };
  }

  protected override void LoadContent()
  {
    project = GumService.Default.Initialize(this, Path.Combine(Content.RootDirectory, "GumProject/BeyondTheBelt.gumx"));
    theme.GetMethod("Apply", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, new object[] { project });
    GumService.Default.CanvasWidth = 3840;
    GumService.Default.CanvasHeight = 2160;
    SystemManagers.Default.Renderer.Camera.Zoom = .5f;
    SystemManagers.Default.Renderer.Camera.CameraCenterOnScreen = CameraCenterOnScreen.TopLeft;
  }

  protected override void Draw(GameTime gameTime)
  {
    Directory.CreateDirectory(output);
    foreach (string screenName in new[] { "MainMenu", "SettingsMenu", "CreditsMenu" })
    {
      GumService.Default.Root.Children.Clear();
      var screen = project.GetScreenSave(screenName).ToGraphicalUiElement();
      screen.AddToRoot();
      GumService.Default.Root.UpdateLayout();
      if (screenName == "MainMenu")
      {
        var button = screen.GetChildByNameRecursively("ButtonNewGame");
        foreach (var state in project.GetComponentSave("Controls/ButtonMainMenu").Categories.Single(c => c.Name == "ButtonCategory").States)
          button.ApplyState(state);
        button.SetProperty("ButtonCategoryState", "Enabled");
        if (button.GetGraphicalUiElementByName("Background") is not Gum.GueDeriving.NineSliceRuntime background ||
          background.Texture == null || background.CustomFrameTextureCoordinateWidth != 12)
          throw new Exception("Menu button is not using the exported nine-slice texture.");
        Capture("MainMenuContrast", contrastBackground: true);
      }
      if (screenName == "SettingsMenu")
      {
        var music = (DefaultFromFileSliderRuntime)screen.GetChildByNameRecursively("SliderMusicVolume");
        var sfx = (DefaultFromFileSliderRuntime)screen.GetChildByNameRecursively("SliderSfxVolume");
        foreach (var slider in new[] { music, sfx })
        {
          theme.GetMethod("BindOrbitSlider", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, new object[] { slider });
          slider.FormsControl.Minimum = 0;
          slider.FormsControl.Maximum = 100;
          var fill = slider.GetGraphicalUiElementByName("OrbitFill");
          foreach (int value in new[] { 0, 50, 100 })
          {
            slider.FormsControl.Value = value;
            if (Math.Abs(fill.Width - value) > .01 || fill.Visible != (value > 0))
              throw new Exception("Slider fill does not follow value.");
          }
        }
        music.FormsControl.Value = 65;
        sfx.FormsControl.Value = 35;
        foreach (string name in new[] { "CheckBoxFullscreen", "CheckBoxVsync", "CheckBoxBorderless", "CheckBoxFixedTimeStep" })
        {
          var toggle = (DefaultFromFileCheckBoxRuntime)screen.GetChildByNameRecursively(name);
          toggle.FormsControl.IsChecked = true;
          var knob = toggle.GetGraphicalUiElementByName("OrbitKnob");
          if (Math.Abs(knob.X - 72.5f) > .01) throw new Exception("Toggle on state failed.");
          toggle.FormsControl.IsChecked = false;
          if (Math.Abs(knob.X - 31.5f) > .01) throw new Exception("Toggle off state failed.");
          toggle.FormsControl.IsChecked = name is "CheckBoxVsync" or "CheckBoxFixedTimeStep";
        }
        var combo = (DefaultFromFileComboBoxRuntime)screen.GetChildByNameRecursively("ComboBoxResolution");
        combo.FormsControl.ListBox.VisualTemplate = new Gum.Forms.VisualTemplate(() =>
          project.GetComponentSave("Controls/ListBoxItem").ToGraphicalUiElement());
        foreach (var resolution in new[] { "3840 x 2160", "2560 x 1440", "2560 x 1600", "2048 x 1536",
          "1920 x 1080", "1920 x 1200", "1920 x 1440", "1680 x 1050", "1600 x 900", "1440 x 900", "1280 x 720" })
          combo.FormsControl.ListBox.Items.Add(resolution);
        combo.FormsControl.SelectedIndex = 0;
        PositionDropdown(combo);
      }
      Capture(screenName);
      if (screenName == "SettingsMenu")
      {
        var music = (DefaultFromFileSliderRuntime)screen.GetChildByNameRecursively("SliderMusicVolume");
        var sfx = (DefaultFromFileSliderRuntime)screen.GetChildByNameRecursively("SliderSfxVolume");
        music.FormsControl.Value = 1;
        sfx.FormsControl.Value = 100;
        Capture("SettingsSliderEndpoints");
        music.FormsControl.Value = 65;
        sfx.FormsControl.Value = 35;
        var combo = (DefaultFromFileComboBoxRuntime)screen.GetChildByNameRecursively("ComboBoxResolution");
        combo.FormsControl.IsDropDownOpen = true;
        PositionDropdown(combo);
        Capture("SettingsDropdown");
      }
    }
    Console.WriteLine("Menu render checks passed: screens, button states, dropdown, toggle states, slider endpoints and midpoint.");
    Exit();
  }

  private void Capture(string name, bool contrastBackground = false)
  {
      using var target = new RenderTarget2D(GraphicsDevice, 1920, 1080);
      GraphicsDevice.SetRenderTarget(target);
      GraphicsDevice.Clear(new Color(7, 12, 20));
      if (contrastBackground)
      {
        // Deliberately bright detail makes missing/transparent button backings obvious.
        using var pixel = new Texture2D(GraphicsDevice, 1, 1);
        pixel.SetData(new[] { Color.White });
        using var batch = new SpriteBatch(GraphicsDevice);
        batch.Begin();
        for (int y = 0; y < 1080; y += 24)
          for (int x = 0; x < 1920; x += 24)
            batch.Draw(pixel, new Rectangle(x, y, 12, 12), new Color(90, 130, 170));
        batch.End();
      }
      GumService.Default.Draw();
      GraphicsDevice.SetRenderTarget(null);
      using var file = File.Create(Path.Combine(output, name + ".png"));
      target.SaveAsPng(file, target.Width, target.Height);
  }

  private static void PositionDropdown(DefaultFromFileComboBoxRuntime combo) =>
    typeof(GameMain).GetMethod("PositionResolutionDropdown", BindingFlags.NonPublic | BindingFlags.Static)!
      .Invoke(null, new object[] { combo });
}
