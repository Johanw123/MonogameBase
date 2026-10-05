using System.Reflection;
using Gum;
using Gum.DataTypes;
using Gum.GueDeriving;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using UntitledGemGame;
using UntitledGemGame.Screens;

internal sealed partial class MenuRenderChecks
{
  private static readonly Type skin = typeof(GameMain).Assembly.GetType("UntitledGemGame.OrbitSkin")!;

  private static void Skin(string method, params object[] args) =>
    skin.GetMethod(method, BindingFlags.Static | BindingFlags.Public)!.Invoke(null, args);

  private TextRuntime SkinLabel(string text, Rectangle bounds, float scale = .5f)
  {
    var template = project.GetComponentSave("Controls/ButtonMainMenu").ToGraphicalUiElement();
    var font = ((RenderingLibrary.Graphics.Text)template.GetChildByNameRecursively("TextInstance").RenderableComponent).BitmapFont;
    return new TextRuntime
    {
      Text = text, X = bounds.X, Y = bounds.Y, Width = bounds.Width, Height = bounds.Height,
      WidthUnits = DimensionUnitType.Absolute, HeightUnits = DimensionUnitType.Absolute,
      BitmapFont = font, FontScale = scale, Color = new Color(150, 244, 239),
      HorizontalAlignment = RenderingLibrary.Graphics.HorizontalAlignment.Center,
      VerticalAlignment = RenderingLibrary.Graphics.VerticalAlignment.Center
    };
  }

  private void CheckOrbitHud()
  {
    GumService.Default.Root.Children.Clear();
    GumService.Default.PopupRoot.Children.Clear();
    using var batch = new SpriteBatch(GraphicsDevice);
    using var target = new RenderTarget2D(GraphicsDevice, 1920, 1080);
    void Begin()
    {
      GraphicsDevice.SetRenderTarget(target);
      GraphicsDevice.Clear(new Color(7, 12, 20));
      batch.Begin(transformMatrix: Matrix.CreateScale(.5f));
    }
    void Finish(string name)
    {
      batch.End();
      GumService.Default.Draw();
      GraphicsDevice.SetRenderTarget(null);
      using var file = File.Create(Path.Combine(output, name + ".png"));
      target.SaveAsPng(file, 1920, 1080);
    }
    void Button(Rectangle bounds, string label, bool selected = false, bool tab = false, bool confirm = false)
    {
      Skin("Button", batch, bounds, selected, 0f, tab, null!, confirm);
      GumService.Default.Root.Children.Add(SkinLabel(label, bounds, .4f));
    }

    Begin();
    Skin("Panel", batch, new Rectangle(0, 0, 3840, 132), true);
    GumService.Default.Root.Children.Add(SkinLabel("Orbit HUD / in-game surfaces", new Rectangle(1100, 10, 1350, 100), .65f));
    Skin("Slider", batch, new Rectangle(2940, 62, 420, 8), .5f);
    Button(new Rectangle(3480, 36, 250, 60), "Pop out");
    Skin("Panel", batch, new Rectangle(500, 220, 1450, 1560), true);
    GumService.Default.Root.Children.Add(SkinLabel("SHIPYARD", new Rectangle(550, 220, 1350, 98)));
    for (int i = 0; i < 5; i++)
      Button(new Rectangle(64, 220 + i * 156, 400, 144), new[] { "Drifter", "Seeker", "Prospector", "Trove Hunter", "Rimrunner" }[i], i == 0, true);
    for (int i = 0; i < 4; i++)
      Button(new Rectangle(560 + i % 2 * 650, 400 + i / 2 * 300, 600, 240), "Module slot", i == 0);
    Skin("Panel", batch, new Rectangle(560, 1080, 1320, 600), false);
    GumService.Default.Root.Children.Add(SkinLabel("MODULE INVENTORY", new Rectangle(560, 1080, 1320, 80)));
    for (int i = 0; i < 5; i++) Button(new Rectangle(610 + i * 244, 1210, 180, 180), (i + 1).ToString());
    Skin("Panel", batch, new Rectangle(2020, 220, 1750, 1560), true);
    GumService.Default.Root.Children.Add(SkinLabel("DEEP SPACE ARRAY", new Rectangle(2060, 220, 1670, 98)));
    for (int i = 0; i < 3; i++)
      Button(new Rectangle(2090 + i * 540, 430, 490, 920), "Discovery " + (i + 1), i == 1);
    Button(new Rectangle(2660, 1540, 450, 100), "Scan", true, confirm: true);
    Skin("Panel", batch, new Rectangle(0, 2028, 3840, 132), false);
    string[] tabs = { "Upgrades", "Abilities", "Shipyard", "Signals" };
    for (int i = 0; i < tabs.Length; i++) Button(new Rectangle(1020 + i * 246, 2064, 230, 60), tabs[i], i == 2, true);
    Button(new Rectangle(32, 2050, 420, 76), "Extract Core: +12");
    Button(new Rectangle(490, 2050, 460, 76), "Buy +1 ability point", true, confirm: true);
    Skin("Progress", batch, new Rectangle(46, 2120, 390, 6), .65f);
    Finish("OrbitHudSurfaces");
  }
}
