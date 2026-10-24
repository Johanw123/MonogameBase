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
      GuiPalette.Set(GuiColor.Upgrade, new Color(100, 230, 80));
      Check(GuiPalette.Get(GuiColor.Prestige) != GuiPalette.Get(GuiColor.Upgrade), "Panel accents and prestige currency must be independent");
      Check(GuiPalette.Base(blue) != blue && GuiPalette.Base(blue).A == blue.A, "Base tint must change blue and preserve alpha");
      Check(GuiPalette.Base(Color.White) == Color.White && GuiPalette.Base(Color.Orange) == Color.Orange,
        "Base tint must preserve neutral highlights and unrelated colors");
      GuiPalette.ResetOriginal();
      var cyanBorder = new Color(0, 195, 255, 220);
      var darkInterior = new Color(10, 26, 48, 240);
      var greenBorder = new Color(62, 232, 130, 220);
      GuiPalette.Set(GuiColor.Accent, new Color(255, 90, 180));
      Check(GuiPalette.Surface(cyanBorder) != cyanBorder && GuiPalette.Surface(cyanBorder).A == cyanBorder.A,
        "Accent tint must recolor cyan Synty borders and preserve alpha");
      Check(GuiPalette.Surface(darkInterior) == darkInterior,
        "Accent tint alone must keep dark Synty panel interiors unchanged");
      GuiPalette.Set(GuiColor.Confirm, new Color(255, 200, 50));
      Check(GuiPalette.ConfirmSurface(greenBorder) != greenBorder && GuiPalette.ConfirmSurface(greenBorder).A == greenBorder.A,
        "Confirm tint must recolor green Synty confirm borders and preserve alpha");
      GuiPalette.ResetOriginal();
      GuiPalette.Apply(changed);
      GuiPalette.Set(GuiColor.Prestige, new Color(240, 100, 75));
      GuiPalette.Set(GuiColor.Upgrade, new Color(100, 230, 80));
      GuiPalette.Save(path);
      Check(!GuiPalette.HasChanges, "Save must establish the new revert point");
      using (var stream = File.OpenRead(path)) changed = GuiPalette.Read(stream);
      GuiPalette.ResetOriginal();
      Check(GuiPalette.HasChanges, "Original preview must not overwrite saved colors");
      GuiPalette.Revert();
      Check(!GuiPalette.HasChanges && GuiPalette.Get(GuiColor.Prestige) == new Color(240, 100, 75), "Revert must restore the last saved palette");
      GuiPalette.ResetOriginal();
      GuiPalette.Apply(changed);
      Check(GuiPalette.Get(GuiColor.Upgrade) == new Color(100, 230, 80), "JSON round trip must preserve independent accents");
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

      RunSpriteSlotChecks();
      Console.WriteLine("GUI palette & sprite slots: tint, independent accents, JSON, save/revert, sprite overrides, auto mask detection and validation passed.");
    }
    finally
    {
      GuiPalette.Apply(initial);
      GuiSkin.DebugResetAllOriginals();
      Directory.Delete(folder, true);
    }
  }

  private static void RunSpriteSlotChecks()
  {
    GuiSkin.DebugResetAllOriginals();
    Check(!GuiSkin.HasAnyOverride, "All GUI sprite slots must start at built-in defaults");
    Check(GuiSkin.AllSlotInfos.Count >= 24, "Expected full set of GUI sprite slots");
    var builtIns = GuiSkin.GetBuiltInAssetNames();
    Check(builtIns.Contains("modal_info_complete") && builtIns.Contains("synty_hud_box_02"),
      "Built-in sprite list must include Content/Menu assets");

    // Switching a panel background to a white Synty mask must auto-select FillTint
    Check(GuiSkin.DebugSetBuiltInAsset(GuiSpriteSlot.PanelBackground, "synty_hud_box_04"),
      "Switching PanelBackground to synty_hud_box_04 must succeed");
    Check(!GuiSkin.IsBuiltIn(GuiSpriteSlot.PanelBackground) && GuiSkin.IsSlotReplaced(GuiSpriteSlot.PanelBackground),
      "PanelBackground must report replaced after switching built-in asset");
    Check(GuiSkin.GetSlotDimensions(GuiSpriteSlot.PanelBackground).IsWhiteMask,
      "synty_hud_box_04 must be detected as a white/tint mask");
    Check(GuiSkin.ResolveEffectiveColorMode(GuiSpriteSlot.PanelBackground) == GuiSpriteColorMode.FillTint,
      "Auto color mode on white mask in background slot must resolve to FillTint");

    // Switching a border slot to a white Synty frame must auto-select AccentTint
    Check(GuiSkin.DebugSetBuiltInAsset(GuiSpriteSlot.PanelFrameOverlay, "synty_hud_frame_02"),
      "Switching PanelFrameOverlay to synty_hud_frame_02 must succeed");
    Check(GuiSkin.ResolveEffectiveColorMode(GuiSpriteSlot.PanelFrameOverlay) == GuiSpriteColorMode.AccentTint,
      "Auto color mode on white mask in border slot must resolve to AccentTint");

    // Parameter tuning & hidden toggle
    GuiSkin.DebugSetParameters(GuiSpriteSlot.PanelBackground, frameWidth: 32, cornerScale: 1.5f, opacity: 0.75f,
      colorMode: GuiSpriteColorMode.AccentTint, hidden: true);
    Check(GuiSkin.GetSlotFrameWidth(GuiSpriteSlot.PanelBackground) == 32
      && Math.Abs(GuiSkin.GetSlotCornerScale(GuiSpriteSlot.PanelBackground) - 1.5f) < 0.01f
      && Math.Abs(GuiSkin.GetSlotOpacity(GuiSpriteSlot.PanelBackground) - 0.75f) < 0.01f
      && GuiSkin.GetSlotColorMode(GuiSpriteSlot.PanelBackground) == GuiSpriteColorMode.AccentTint
      && GuiSkin.GetSlotHidden(GuiSpriteSlot.PanelBackground),
      "Slot parameters must update live");

    // Custom PNG bytes replacement
    byte[] samplePng = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Content", "Menu", "button_active_green.png"));
    Check(GuiSkin.DebugReplace(GuiSpriteSlot.HudButtonIdle, samplePng, "custom_button.png", out string err) && err == null,
      "DebugReplace with valid PNG bytes must succeed");
    Check(GuiSkin.GetSlotSourceName(GuiSpriteSlot.HudButtonIdle) == "custom_button.png",
      "DebugReplace must record custom source name");

    GuiSkin.DebugRevert(GuiSpriteSlot.HudButtonIdle);
    Check(GuiSkin.IsBuiltIn(GuiSpriteSlot.HudButtonIdle), "DebugRevert must restore saved default");

    GuiSkin.DebugResetAllOriginals();
    Check(!GuiSkin.HasAnyOverride, "DebugResetAllOriginals must restore all slots to built-in defaults");
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
  private static readonly Type skin = game.GetType("UntitledGemGame.GuiSkin")!;
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
  private Texture2D originalGumTextureRef = null!;
  private float? originalGumFrameWidth;
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
    GuiSkin.DebugResetAllOriginals();
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
    originalGumTextureRef = background.Texture;
    originalGumFrameWidth = background.CustomFrameTextureCoordinateWidth;
    originalTexture = Pixels(background.Texture);
    originalHud = Pixels(HudTexture());
    GuiPalette.Set(GuiColor.Accent, new Color(255, 90, 180));
    Runtime("Refresh");
    if (originalTexture.SequenceEqual(Pixels(background.Texture))) throw new Exception("Accent change alone did not recolor Gum texture");
    if (originalHud.SequenceEqual(Pixels(HudTexture()))) throw new Exception("Accent change alone did not recolor HUD texture");
    GuiPalette.ResetOriginal();
    Runtime("Refresh");
    if (!originalTexture.SequenceEqual(Pixels(background.Texture))) throw new Exception("Reset after Accent change did not restore exact Gum pixels");
    if (!originalHud.SequenceEqual(Pixels(HudTexture()))) throw new Exception("Reset after Accent change did not restore exact HUD pixels");
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
      GuiPalette.Set(GuiColor.Accent, new Color(255, 110, 190));
      GuiPalette.Set(GuiColor.Upgrade, new Color(255, 160, 90));
      GuiPalette.Set(GuiColor.Text, new Color(238, 245, 190));
    }
    if (frames == 5) GuiPalette.ResetOriginal();
    if (frames == 7)
    {
      // Simulate dropping a sprite file onto PanelBackground drop target and switching button/border slots
      var dropRect = DebugGuiSprites.GetDropTargetBounds(GuiSpriteSlot.PanelBackground);
      if (dropRect.Width <= 0 || dropRect.Height <= 0)
        throw new Exception("DebugGuiSprites did not record drop target bounds for PanelBackground");
      string dropFile = Path.Combine(Content.RootDirectory, "Menu", "synty_box_techy_01.png");
      if (!DebugGuiSprites.HandleFileDropAt(dropRect.Center, dropFile))
        throw new Exception("Simulated file drop onto PanelBackground drop box was not accepted");
      // Wait for async import task and poll
      for (int spin = 0; spin < 50 && !GuiSkin.IsSlotReplaced(GuiSpriteSlot.PanelBackground); spin++)
      {
        Thread.Sleep(10);
        DebugGuiSprites.PollImports();
      }
      if (!GuiSkin.IsSlotReplaced(GuiSpriteSlot.PanelBackground))
        throw new Exception("Dropped PNG did not replace PanelBackground slot");

      GuiSkin.DebugSetBuiltInAsset(GuiSpriteSlot.HudBarBackground, "synty_box_steel_01");
      GuiSkin.DebugSetBuiltInAsset(GuiSpriteSlot.PanelFrameOverlay, "synty_hud_frame_08");
      GuiSkin.DebugSetBuiltInAsset(GuiSpriteSlot.HudButtonIdle, "synty_hud_box_04");
      GuiSkin.DebugSetBuiltInAsset(GuiSpriteSlot.HudButtonActive, "button_active_green");
      GuiSkin.DebugSetParameters(GuiSpriteSlot.HudButtonIdle, frameWidth: 28, cornerScale: 1.25f);
    }
    if (frames == 9)
    {
      GuiSkin.DebugResetAllOriginals();
    }

    Runtime("Refresh");
    if (slider.FormsControl.Value != 37) throw new Exception("Live recoloring reset a slider");
    if (text.Color != GuiPalette.Get(GuiColor.Text)) throw new Exception("Existing Gum text did not update live");
    if (frames == 3 && originalTexture.SequenceEqual(Pixels(background.Texture))) throw new Exception("Baked blue Gum artwork did not recolor");
    if (frames == 3 && originalHud.SequenceEqual(Pixels(HudTexture()))) throw new Exception("Baked blue HUD artwork did not recolor");
    if (frames == 5 && !originalHud.SequenceEqual(Pixels(HudTexture()))) throw new Exception("HUD texture revert must restore exact premultiplied pixels");
    if (frames == 5 && !originalTexture.SequenceEqual(Pixels(background.Texture))) throw new Exception("Reverting must restore exact texture pixels");
    if (frames == 7)
    {
      if (ReferenceEquals(background.Texture, originalGumTextureRef))
        throw new Exception("Gum button background did not switch to overridden slot texture");
      if (background.CustomFrameTextureCoordinateWidth == originalGumFrameWidth)
        throw new Exception("Gum button background did not update 9-slice frame width");
    }
    if (frames == 9)
    {
      if (!ReferenceEquals(background.Texture, originalGumTextureRef))
        throw new Exception("Resetting GUI sprite slots did not restore original Gum texture reference");
      if (background.CustomFrameTextureCoordinateWidth != originalGumFrameWidth)
        throw new Exception("Resetting GUI sprite slots did not restore original Gum 9-slice frame width");
    }

    GraphicsDevice.SetRenderTarget(target);
    GraphicsDevice.Clear(new Color(5, 8, 14));
    batch.Begin();
    Skin("TopBar", batch, new Rectangle(16, 30, 735, 110), Palette("PanelAccent"));
    Skin("TitleBadge", batch, new Rectangle(165, 40, 430, 88), Palette("PanelAccent"));
    Skin("HudBar", batch, new Rectangle(16, 180, 735, 230));
    for (int i = 0; i < 3; i++) Skin("HudButton", batch, new Rectangle(32 + i * 240, 204, 220, 80),
      Palette("AbilityAccent"), i == 1, i == 1, 0f, null!, false, 1f);
    Skin("Progress", batch, new Rectangle(40, 365, 680, 12), .65f);
    Skin("TechCard", batch, new Rectangle(32, 460, 690, 240), Palette("SurfaceColor"),
      Palette("PanelAccent"), false, true, true, true);
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
    if (frames is 2 or 4 or 6 or 8 or 10)
    {
      Directory.CreateDirectory(output);
      string fileName = frames switch
      {
        2 => "original.png",
        4 => "tinted.png",
        6 => "reverted.png",
        8 => "sprites_overridden.png",
        _ => "sprites_reverted.png",
      };
      using var stream = File.Create(Path.Combine(output, fileName));
      target.SaveAsPng(stream, target.Width, target.Height);
    }
    if (frames == 10)
    {
      Console.WriteLine("GUI runtime recoloring, sprite slot overrides, drag-and-drop import, texture restoration, slider state and editor rendering passed: " + output);
      Exit();
    }
  }

  protected override void UnloadContent()
  {
    target?.Dispose();
    batch?.Dispose();
    GuiPalette.ResetOriginal();
    GuiSkin.DebugResetAllOriginals();
  }
}
