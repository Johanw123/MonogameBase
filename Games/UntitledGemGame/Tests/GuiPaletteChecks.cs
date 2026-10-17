using System.Reflection;
using System.Text;
using Gum;
using Gum.GueDeriving;
using Gum.Wireframe;
using ImGuiNET;
using JapeFramework.ImGUI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using UntitledGemGame;

internal static class GuiPaletteChecks
{
  public static void Run()
  {
    string folder = Path.Combine(Path.GetTempPath(), "gui-palette-" + Guid.NewGuid());
    Directory.CreateDirectory(folder);
    string path = Path.Combine(folder, "palette.json");
    var initial = GuiPalette.Snapshot();
    try
    {
      GuiPalette.ResetOriginal();
      var blue = new Color(10, 80, 200, 137);
      Check(GuiPalette.Base(blue) == blue, "Original palette must preserve exact RGBA");
      var changed = GuiPalette.Snapshot();
      changed.BaseHueShift = 120;
      changed.BaseSaturation = 0.7f;
      changed.BaseBrightness = 1.2f;
      GuiPalette.Apply(changed);
      GuiPalette.Set(GuiColor.Prestige, new Color(240, 100, 75));
      GuiPalette.Set(GuiColor.Signal, new Color(100, 230, 80));
      Check(GuiPalette.Get(GuiColor.Prestige) != GuiPalette.Get(GuiColor.Signal), "Signal and prestige accents must be independent");
      Check(GuiPalette.Base(blue) != blue && GuiPalette.Base(blue).A == blue.A, "Base tint must change blue and preserve alpha");
      Check(GuiPalette.Base(Color.White) == Color.White && GuiPalette.Base(Color.Orange) == Color.Orange,
        "Base tint must preserve neutral highlights and unrelated colors");
      GuiPalette.Save(path);
      Check(!GuiPalette.HasChanges, "Save must establish the new revert point");
      using (var stream = File.OpenRead(path)) changed = GuiPalette.Read(stream);
      GuiPalette.ResetOriginal();
      Check(GuiPalette.HasChanges, "Original preview must not overwrite saved colors");
      GuiPalette.Revert();
      Check(!GuiPalette.HasChanges && GuiPalette.Get(GuiColor.Prestige) == new Color(240, 100, 75), "Revert must restore the last saved palette");
      GuiPalette.ResetOriginal();
      GuiPalette.Apply(changed);
      Check(GuiPalette.Get(GuiColor.Signal) == new Color(100, 230, 80), "JSON round trip must preserve independent accents");
      changed.Colors[GuiColor.Prestige.ToString()] = "#000000";
      Check(GuiPalette.Get(GuiColor.Prestige) != Color.Black, "Applying a palette must copy mutable data");
      changed = GuiPalette.Snapshot();
      changed.BaseHueShift = float.NaN;
      Reject(() => GuiPalette.Apply(changed));
      using var invalid = new MemoryStream(Encoding.UTF8.GetBytes("{\"Colors\":{\"Accent\":\"bad\"}}"));
      Reject(() => GuiPalette.Read(invalid));
      GuiPalette.Set(GuiColor.Ability, Color.HotPink);
      try { GuiPalette.Save(Path.Combine(folder, "missing", "palette.json")); throw new Exception("Save unexpectedly succeeded"); }
      catch (DirectoryNotFoundException) { }
      Check(GuiPalette.HasChanges, "A failed save must leave the revert point unchanged");
      GuiPalette.Revert();
      Check(GuiPalette.Get(GuiColor.Ability) != Color.HotPink, "Revert after a failed save must use the successful save");
      Console.WriteLine("GUI palette: tint, independent accents, JSON, save/revert, reset and validation passed.");
    }
    finally
    {
      GuiPalette.Apply(initial);
      Directory.Delete(folder, true);
    }
  }

  private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
  private static void Reject(Action action)
  {
    try { action(); }
    catch (InvalidDataException) { return; }
    throw new Exception("Invalid palette was accepted");
  }
}

// Uses the same skin, file-defined menu controls and ImGui editor as the game.
internal sealed class GuiPaletteRenderChecks : Game
{
  private readonly string output;
  private static readonly Assembly game = typeof(GameMain).Assembly;
  private static readonly Type skin = game.GetType("UntitledGemGame.OrbitSkin")!;
  private static readonly Type runtime = game.GetType("UntitledGemGame.GuiThemeRuntime")!;
  private static readonly Type theme = game.GetType("UntitledGemGame.MenuTheme")!;
  private readonly Type editorType = game.GetType("UntitledGemGame.DebugGuiColors")!;
  private object editor = null!;
  private ImGuiRenderer renderer = null!;
  private SpriteBatch batch = null!;
  private RenderTarget2D target = null!;
  private NineSliceRuntime background = null!;
  private TextRuntime text = null!;
  private Gum.Forms.DefaultFromFileVisuals.DefaultFromFileSliderRuntime slider = null!;
  private Color[] originalTexture = null!;
  private Color[] originalHud = null!;
  private int frames;

  public GuiPaletteRenderChecks(string output)
  {
    this.output = output;
    Content.RootDirectory = Path.GetFullPath("bin/Debug/net10.0/Content");
    _ = new GraphicsDeviceManager(this) { PreferredBackBufferWidth = 1500, PreferredBackBufferHeight = 950 };
    IsFixedTimeStep = false;
  }

  private static void Runtime(string method, params object[] args) => runtime.GetMethod(method)!.Invoke(null, args);
  private static void Skin(string method, params object[] args) => skin.GetMethods().Single(m => m.Name == method && m.GetParameters().Length == args.Length).Invoke(null, args);
  private static Color Palette(string property) => (Color)skin.GetProperty(property)!.GetValue(null)!;
  private Texture2D HudTexture() => (Texture2D)skin.GetMethod("GetTexture", BindingFlags.Static | BindingFlags.NonPublic)!
    .Invoke(null, [GraphicsDevice, "hud_bar_background"])!;
  private static Color[] Pixels(Texture2D texture) { var pixels = new Color[texture.Width * texture.Height]; texture.GetData(pixels); return pixels; }

  protected override void LoadContent()
  {
    GuiPalette.ResetOriginal();
    var project = GumService.Default.Initialize(this, Path.Combine(Content.RootDirectory, "GumProject/BeyondTheBelt.gumx"));
    theme.GetMethod("Apply")!.Invoke(null, [project]);
    Runtime("CaptureStates", project);
    var menu = project.GetScreenSave("SettingsMenu").ToGraphicalUiElement();
    Runtime("Track", menu);
    slider = (Gum.Forms.DefaultFromFileVisuals.DefaultFromFileSliderRuntime)menu.GetChildByNameRecursively("SliderMusicVolume");
    slider.FormsControl.Value = 37;
    var button = project.GetComponentSave("Controls/ButtonMainMenu").ToGraphicalUiElement();
    Runtime("Track", button);
    background = (NineSliceRuntime)button.GetChildByNameRecursively("Background");
    text = (TextRuntime)button.GetChildByNameRecursively("TextInstance");
    originalTexture = Pixels(background.Texture);
    originalHud = Pixels(HudTexture());
    batch = new SpriteBatch(GraphicsDevice);
    target = new RenderTarget2D(GraphicsDevice, 1500, 950);
    renderer = new ImGuiRenderer(this);
    renderer.RebuildFontAtlas();
    ImGui.GetIO().IniSavingRate = 0;
    editor = Activator.CreateInstance(editorType)!;
  }

  protected override void Draw(GameTime gameTime)
  {
    ++frames;
    if (frames == 3)
    {
      var data = GuiPalette.Snapshot();
      data.BaseHueShift = 125;
      GuiPalette.Apply(data);
      GuiPalette.Set(GuiColor.Prestige, new Color(255, 160, 90));
      GuiPalette.Set(GuiColor.Text, new Color(238, 245, 190));
    }
    if (frames == 5) GuiPalette.ResetOriginal();
    Runtime("Refresh");
    if (slider.FormsControl.Value != 37) throw new Exception("Live recoloring reset a slider");
    if (text.Color != GuiPalette.Get(GuiColor.Text)) throw new Exception("Existing Gum text did not update live");
    if (frames == 3 && originalTexture.SequenceEqual(Pixels(background.Texture))) throw new Exception("Baked blue Gum artwork did not recolor");
    if (frames == 3 && originalHud.SequenceEqual(Pixels(HudTexture()))) throw new Exception("Baked blue HUD artwork did not recolor");
    if (frames == 5 && !originalHud.SequenceEqual(Pixels(HudTexture()))) throw new Exception("HUD texture revert must restore exact premultiplied pixels");
    if (frames == 5 && !originalTexture.SequenceEqual(Pixels(background.Texture))) throw new Exception("Reverting must restore exact texture pixels");
    GraphicsDevice.SetRenderTarget(target);
    GraphicsDevice.Clear(new Color(5, 8, 14));
    batch.Begin();
    Skin("TopBar", batch, new Rectangle(16, 30, 735, 110), Palette("PrestigeAccent"));
    Skin("TitleBadge", batch, new Rectangle(165, 40, 430, 88), Palette("PrestigeAccent"));
    Skin("HudBar", batch, new Rectangle(16, 180, 735, 230));
    for (int i = 0; i < 3; i++) Skin("HudButton", batch, new Rectangle(32 + i * 240, 204, 220, 80),
      Palette("AbilityAccent"), i == 1, i == 1, 0f, null!, false);
    Skin("Progress", batch, new Rectangle(40, 365, 680, 12), .65f);
    Skin("TechCard", batch, new Rectangle(32, 460, 690, 240), Palette("SurfaceColor"),
      Palette("PrestigeAccent"), false, true, true, true);
    batch.End();
    renderer.BeginLayout(gameTime);
    ImGui.SetNextWindowPos(new System.Numerics.Vector2(770, 20), ImGuiCond.Always);
    ImGui.SetNextWindowSize(new System.Numerics.Vector2(710, 910), ImGuiCond.Always);
    ImGui.Begin("Debug palette preview");
    ImGui.SetNextItemOpen(true, ImGuiCond.Always);
    editorType.GetMethod("Draw")!.Invoke(editor, null);
    ImGui.End();
    renderer.EndLayout();
    GraphicsDevice.SetRenderTarget(null);
    if (frames is 2 or 4 or 6)
    {
      Directory.CreateDirectory(output);
      using var stream = File.Create(Path.Combine(output, frames == 4 ? "tinted.png" : frames == 2 ? "original.png" : "reverted.png"));
      target.SaveAsPng(stream, target.Width, target.Height);
    }
    if (frames == 6)
    {
      Console.WriteLine("GUI runtime recoloring, texture restoration, slider state and editor rendering passed: " + output);
      Exit();
    }
  }

  protected override void UnloadContent() { target?.Dispose(); batch?.Dispose(); GuiPalette.ResetOriginal(); }
}
