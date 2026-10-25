using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Apos.Shapes;
using AsyncContent;
using Gum.Forms.Controls;
using Gum.Forms.DefaultVisuals;
using Gum.Wireframe;
using JapeFramework;
using JapeFramework.Aseprite;
using JapeFramework.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using MonoGame.Extended.Graphics;
using MonoGame.Extended.Input;
using MonoGame.Extended.Screens;
using MonoGame.Extended.Screens.Transitions;
using MonoGameGum;
using MonoGameGum.GueDeriving;
using RenderingLibrary;
using RenderingLibrary.Graphics;
using UntitledGemGame.Entities;

namespace UntitledGemGame.Screens
{
  public class HarvesterStruct
  {
    public MonoGame.Extended.Graphics.Sprite Sprite;
    public AnimatedSprite AnimatedSprite;
    public Transform2 Transform;
    public Vector2 TargetPosition = Vector2.Zero;
    public Harvester.HarvesterType Type = Harvester.HarvesterType.Harvester;
    public int Id;
    public bool ReturningToBase;
    // The title screen's warp-in (MainMenu.UpdateArrival): seconds until the ship drops in,
    // its off-screen start and how far along it is (1 = arrived at TargetPosition).
    public float ArrivalDelay;
    public Vector2 ArrivalStart;
    public float ArrivalProgress = 1f;
    // Length of the light streak behind the ship while it is faster than cruising.
    public float ArrivalTrail;
  }

  public class MainMenu : GameScreen
  {
    // Tight non-transparent bounding box of Content/Textures/logo_4k.png (already left-aligned).
    public static readonly Rectangle LogoSourceRect = new(472, 106, 3168, 1012);

    public static Rectangle GetLogoDestinationRect(int screenWidth, int screenHeight)
    {
      float sx = screenWidth / 3840f;
      float sy = screenHeight / 2160f;
      int x = (int)MathF.Round(180f * sx);
      int y = (int)MathF.Round(140f * sy);
      int w = (int)MathF.Round(820f * sx);
      int h = (int)MathF.Round(w * (LogoSourceRect.Height / (float)LogoSourceRect.Width));
      return new Rectangle(x, y, w, h);
    }

    public static void DrawLogo(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, Color tint)
    {
      var sprite = TextureCache.Logo;
      if (sprite?.IsLoaded != true || sprite.IsFailed || tint.A == 0) return;

      int screenWidth = graphicsDevice.Viewport.Width;
      int screenHeight = graphicsDevice.Viewport.Height;
      var destinationRect = GetLogoDestinationRect(screenWidth, screenHeight);
      spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.NonPremultiplied, SamplerState.LinearClamp);
      spriteBatch.Draw(sprite.Value, destinationRect, LogoSourceRect, tint);
      spriteBatch.End();
    }

    private SpriteBatch m_spriteBatch;
    private GraphicalUiElement m_menuScreen;
    private OrthographicCamera m_camera;
    private OrthographicCamera m_camera_background;

    private GraphicalUiElement newGameDialog;
    private const uint StoreAppId = 5084070;
    private Gum.Forms.DefaultFromFileVisuals.DefaultFromFileButtonRuntime wishlistButton;
    private Gum.Forms.DefaultFromFileVisuals.DefaultFromFileButtonRuntime feedbackButton;

    public MainMenu(Game game, GraphicalUiElement menuScreen)
    : base(game)
    {
      m_menuScreen = menuScreen;
      game.IsMouseVisible = true;

      // m_camera = JapeFramework.BaseGame.Camera;
      m_camera = new OrthographicCamera(GameMain.BoxingViewportAdapter);
      m_camera.Zoom = RenderQuality.WorldZoom(3.5f);

      m_camera_background = new OrthographicCamera(GameMain.BoxingViewportAdapter);
      m_camera_background.Zoom = RenderQuality.WorldZoom(1.5f);

      Init();
      UpdateVersionLabel();
      Loc.Changed += OnLanguageChanged;
      hasSave = new GameSaveStore(GameSaveStore.DefaultPath).Load() != null;
      m_menuScreen.GetChildByNameRecursively("ButtonContinue").Visible = hasSave;

      GumService.Default.Root.Children.Clear();
      GumService.Default.Root.Children.Add(m_menuScreen);
      //
      // GumService.Default.CanvasWidth = 1920 * 2;
      // GumService.Default.CanvasHeight = 1080 * 2;
      // GumService.Default.Root.UpdateLayout();

      var camera = SystemManagers.Default.Renderer.Camera;

      camera.Zoom = 1.0f;
      camera.Position = System.Numerics.Vector2.Zero;

      SystemManagers.Default.Renderer.Camera.CameraCenterOnScreen = CameraCenterOnScreen.TopLeft;
      Renderer.UseBasicEffectRendering = true;

      GumService.Default.CanvasWidth = 3840;
      GumService.Default.CanvasHeight = 2160;
      GumService.Default.Root.UpdateLayout();
    }

    private void UpdateVersionLabel()
    {
      var label = m_menuScreen.GetChildByNameRecursively("VersionLabel") as Gum.GueDeriving.TextRuntime;
      if (label == null)
      {
        label = MenuText(0.46f);
        label.Name = "VersionLabel";
        label.WidthUnits = Gum.DataTypes.DimensionUnitType.Absolute;
        label.HeightUnits = Gum.DataTypes.DimensionUnitType.Absolute;
        label.Width = 520;
        label.Height = 60;
        label.Color = HudLayout.ButtonTextColor;
        label.HorizontalAlignment = HorizontalAlignment.Left;
        label.VerticalAlignment = VerticalAlignment.Center;
        m_menuScreen.Children.Add(label);
        label.Anchor(Anchor.BottomLeft);
        label.X = 216;
        label.Y = -36;
      }
      label.Text = Demo.VersionLabel;
    }

    // The version label holds already translated text, which Gum cannot translate again.
    private void OnLanguageChanged() => UpdateVersionLabel();

    // Text in the menu buttons' font, scaled. It takes the font settings rather than the loaded
    // bitmap font, so it follows the language's font (GumMenuFonts).
    private Gum.GueDeriving.TextRuntime MenuText(float scale)
    {
      var source = (Gum.GueDeriving.TextRuntime)m_menuScreen
        .GetChildByNameRecursively("ButtonNewGame")
        .GetChildByNameRecursively("TextInstance");
      return new Gum.GueDeriving.TextRuntime
      {
        Font = source.Font,
        FontSize = source.FontSize,
        IsBold = source.IsBold,
        OutlineThickness = source.OutlineThickness,
        FontScale = scale
      };
    }

    private void Init()
    {
      var newGame = m_menuScreen.GetChildByNameRecursively("ButtonNewGame") as Gum.Forms.DefaultFromFileVisuals.DefaultFromFileButtonRuntime;
      var continueGame = m_menuScreen.GetChildByNameRecursively("ButtonContinue") as Gum.Forms.DefaultFromFileVisuals.DefaultFromFileButtonRuntime;
      var exit = m_menuScreen.GetChildByNameRecursively("ButtonExit") as Gum.Forms.DefaultFromFileVisuals.DefaultFromFileButtonRuntime;
      var settings = m_menuScreen.GetChildByNameRecursively("ButtonSettings") as Gum.Forms.DefaultFromFileVisuals.DefaultFromFileButtonRuntime;
      var credits = m_menuScreen.GetChildByNameRecursively("ButtonCredits") as Gum.Forms.DefaultFromFileVisuals.DefaultFromFileButtonRuntime;
      wishlistButton = (Gum.Forms.DefaultFromFileVisuals.DefaultFromFileButtonRuntime)m_menuScreen.GetChildByNameRecursively("ButtonWishlist");
      wishlistButton.Anchor(Anchor.BottomLeft);
      wishlistButton.X = 180;
      wishlistButton.Y = -124;
      wishlistButton.FormsControl.IsEnabled = !OperatingSystem.IsBrowser();
      wishlistButton.Click += WishlistClicked;

      feedbackButton = m_menuScreen.GetChildByNameRecursively("ButtonFeedback") as Gum.Forms.DefaultFromFileVisuals.DefaultFromFileButtonRuntime;
      if (feedbackButton == null && GameMain.GumProject != null)
      {
        feedbackButton = (Gum.Forms.DefaultFromFileVisuals.DefaultFromFileButtonRuntime)
          GameMain.GumProject.GetComponentSave("Controls/ButtonMainMenu").ToGraphicalUiElement();
        feedbackButton.Name = "ButtonFeedback";
        feedbackButton.SetProperty("Text", Loc.N("Leave Feedback"));
        feedbackButton.WidthUnits = feedbackButton.HeightUnits = Gum.DataTypes.DimensionUnitType.Absolute;
        feedbackButton.Width = 308;
        feedbackButton.Height = 80;
        if (feedbackButton.GetChildByNameRecursively("TextInstance") is Gum.GueDeriving.TextRuntime feedbackText)
          feedbackText.FontScale = 0.52f;
        m_menuScreen.Children.Add(feedbackButton);
        GuiThemeRuntime.TrackMenu(feedbackButton);
        GumText.Track(feedbackButton);
      }
      if (feedbackButton != null)
      {
        feedbackButton.Anchor(Anchor.BottomLeft);
        feedbackButton.X = 552;
        feedbackButton.Y = -124;
        feedbackButton.Click += FeedbackClicked;
      }

      newGame.Click += NewGameClicked;
      continueGame.Click += ContinueClicked;
      settings.Click += SettingsClicked;
      credits.Click += CreditsClicked;
      exit.Visible = !OperatingSystem.IsBrowser();
      exit.Click += ExitClicked;
    }

    private void FeedbackClicked(object sender, EventArgs args)
    {
      AudioManager.Instance.PlaySound(SoundEvent.MenuClick);
      PlaytestFeedback.OpenMainMenuSurvey();
    }

    private void NewGameClicked(object sender, EventArgs args)
    {
      AudioManager.Instance.PlaySound(SoundEvent.MenuClick);
      if (File.Exists(GameSaveStore.DefaultPath) || File.Exists(GameSaveStore.DefaultPath + ".bak"))
        ShowNewGameConfirmation();
      else
        StartGame(newGame: true);
    }

    private void ContinueClicked(object sender, EventArgs args)
    {
      AudioManager.Instance.PlaySound(SoundEvent.MenuClick);
      StartGame(newGame: false);
    }

    private void SettingsClicked(object sender, EventArgs args)
    {
      AudioManager.Instance.PlaySound(SoundEvent.MenuClick);
      GameMain.SwapMenu("SettingsMenu");
    }

    private void CreditsClicked(object sender, EventArgs args)
    {
      AudioManager.Instance.PlaySound(SoundEvent.MenuClick);
      GameMain.SwapMenu("CreditsMenu");
    }

    private void WishlistClicked(object sender, EventArgs args)
    {
      AudioManager.Instance.PlaySound(SoundEvent.MenuClick);
      if (Platform.GameServices.Platform.OpenStorePage(StoreAppId)) return;
#if !KNI_WEB
      Platform.DesktopBrowser.Open($"https://store.steampowered.com/app/{StoreAppId}/");
#endif
    }

    private void ExitClicked(object sender, EventArgs args)
    {
      AudioManager.Instance.PlaySound(SoundEvent.MenuClick);
      Game.Exit();
    }

    private bool m_savedContinueVisible;

    private void SetMainMenuControlsVisible(bool visible)
    {
      if (m_menuScreen == null) return;
      if (!visible && m_menuScreen.GetChildByNameRecursively("ButtonContinue") is GraphicalUiElement contBefore)
        m_savedContinueVisible = contBefore.Visible;

      foreach (string name in new[]
      {
        "PanelInstance", "ButtonNewGame", "ButtonSettings", "ButtonCredits",
        "ButtonWishlist", "ButtonFeedback", "GuiDecorationBottomRule", "GuiDecorationCalibrationTicks"
      })
      {
        if (m_menuScreen.GetChildByNameRecursively(name) is GraphicalUiElement element)
          element.Visible = visible;
      }
      if (m_menuScreen.GetChildByNameRecursively("ButtonContinue") is GraphicalUiElement cont)
        cont.Visible = visible && m_savedContinueVisible;
      if (m_menuScreen.GetChildByNameRecursively("ButtonExit") is GraphicalUiElement exit)
        exit.Visible = visible && !OperatingSystem.IsBrowser();
    }

    private void ShowNewGameConfirmation()
    {
      if (newGameDialog != null)
        return;

      SetMainMenuControlsVisible(false);

      // ModalRoot does not necessarily have the same dimensions as the menu root.
      var overlay = new Gum.GueDeriving.ContainerRuntime
      {
        WidthUnits = Gum.DataTypes.DimensionUnitType.Absolute,
        HeightUnits = Gum.DataTypes.DimensionUnitType.Absolute,
        Width = GumService.Default.CanvasWidth,
        Height = GumService.Default.CanvasHeight
      };
      newGameDialog = overlay;

      var panel = new Gum.GueDeriving.ContainerRuntime
      {
        WidthUnits = Gum.DataTypes.DimensionUnitType.Absolute,
        HeightUnits = Gum.DataTypes.DimensionUnitType.Absolute,
        Width = 960,
        Height = 500,
        XOrigin = HorizontalAlignment.Left,
        XUnits = Gum.Converters.GeneralUnitType.PixelsFromSmall,
        YOrigin = VerticalAlignment.Top,
        YUnits = Gum.Converters.GeneralUnitType.PixelsFromSmall,
        X = 180,
        Y = 1360
      };
      overlay.Children.Add(panel);
      panel.Children.Add(new Gum.GueDeriving.RectangleRuntime
      {
        Width = 960, Height = 500, IsFilled = true,
        FillColor = GuiSkin.PanelBackground,
        StrokeWidth = 0, CornerRadius = 0
      });
      panel.Children.Add(GuiSkin.GumSurface("modal_info_complete", 960, 500));
      var launchEmblem = GuiSkin.GumLaunchEmblem();
      launchEmblem.X = 560;
      launchEmblem.Y = 24;
      panel.Children.Add(launchEmblem);
      // The 70px menu font, downscaled instead of enlarging the default font.
      var message = MenuText(0.68f);
      message.Text = Loc.N("Start a new game?\nYour existing progress will be replaced.");
      message.WidthUnits = Gum.DataTypes.DimensionUnitType.Absolute;
      message.HeightUnits = Gum.DataTypes.DimensionUnitType.Absolute;
      message.X = 60;
      message.Y = 50;
      message.Width = 840;
      message.Height = 240;
      message.Color = GuiSkin.ButtonTextColor;
      message.HorizontalAlignment = HorizontalAlignment.Center;
      message.VerticalAlignment = VerticalAlignment.Center;
      panel.Children.Add(message);

      var cancel = CreateDialogButton(Loc.N("Cancel"), 60);
      var confirm = CreateDialogButton(Loc.N("New Game"), 500);
      panel.Children.Add(cancel);
      panel.Children.Add(confirm);
      cancel.Click += (s, e) => CloseNewGameConfirmation();
      confirm.Click += (s, e) =>
      {
        CloseNewGameConfirmation();
        StartGame(newGame: true);
      };
      GumService.Default.ModalRoot.Children.Add(overlay);
      overlay.UpdateLayout();
    }

    private Gum.Forms.DefaultFromFileVisuals.DefaultFromFileButtonRuntime CreateDialogButton(string text, float x)
    {
      var button = (Gum.Forms.DefaultFromFileVisuals.DefaultFromFileButtonRuntime)
        GameMain.GumProject.GetComponentSave("Controls/ButtonSettingsMenu").ToGraphicalUiElement();
      button.SetProperty("Text", text);
      button.WidthUnits = button.HeightUnits = Gum.DataTypes.DimensionUnitType.Absolute;
      button.Width = 400;
      button.Height = 92;
      button.X = x;
      button.Y = 348;
      return button;
    }

    private void CloseNewGameConfirmation()
    {
      if (newGameDialog == null)
        return;
      GumService.Default.ModalRoot.Children.Remove(newGameDialog);
      newGameDialog = null;
      SetMainMenuControlsVisible(true);
    }

    // private void InitGumService()
    // {
    // }

    public override void LoadContent()
    {
      base.LoadContent();
      Console.WriteLine("MainMenu LoadContent");

      m_spriteBatch = new SpriteBatch(GraphicsDevice);

      TextureCache.PreloadTextures();
      EffectCache.PreloadEffects();

      AudioManager.Instance.LoadContent(Content);

      FontManager.InitFieldFont("Roboto_Regular_ttf", "Fonts/Roboto-Regular.ttf");

      var width = GameMain.Instance.GraphicsDevice.Viewport.Width;
      var height = GameMain.Instance.GraphicsDevice.Viewport.Height;

      var textRenderer = FontManager.GetTextRenderer(nameof(ContentDirectory.Fonts.Roboto_Regular_ttf));
      textRenderer.SetOrtographicProjection(width, height);
      textRenderer.UseScreenSpace = false;

      GameMain.AddCustomHudContent(DrawMenu);
    }

    public const float MenuScrollSpeed = 24f;
    private const float MenuHomeHullScale = 1.0f;
    private const float MenuTurretTexel = 0.19f;
    private static readonly Vector2 MenuShipTerminalMount = new(-14.5f, 1.5f);
    private const float MenuShipTerminalScaleMultiplier = 0.88f;

    private const int MenuPlanetFrameSize = 96;
    private const int MenuPlanetSheetFrameSize = 288;
    private const int MenuPlanetSheetColumns = 9;
    private const int MenuPlanetFrameCount = 77;
    private const float MenuPlanetFrameSeconds = 0.14f;

    public readonly struct MenuPlanetStyle
    {
      public readonly Color SurfaceTint;
      public readonly float SurfaceBrightness;
      public readonly Color GlowTint;
      public readonly float GlowIntensity;

      public MenuPlanetStyle(Color surfaceTint, float surfaceBrightness, Color glowTint, float glowIntensity)
      {
        SurfaceTint = surfaceTint;
        SurfaceBrightness = surfaceBrightness;
        GlowTint = glowTint;
        GlowIntensity = glowIntensity;
      }
    }

    public static readonly MenuPlanetStyle[] PassingPlanetStyles =
    [
      new(new Color(235, 128, 92),  0.72f, new Color(255, 145, 45),  0.26f), // Terracotta / Rust World
      new(new Color(175, 220, 255), 0.74f, new Color(80, 225, 255),  0.44f), // Deep Cryo / Glacial World
      new(new Color(130, 215, 160), 0.68f, new Color(65, 255, 140),  0.34f), // Emerald / Bio World
      new(new Color(195, 150, 245), 0.68f, new Color(220, 95, 255),  0.40f), // Amethyst / Twilight World
      new(new Color(240, 200, 135), 0.75f, new Color(255, 210, 95),  0.18f), // Golden Dune / Sandstone Moon
      new(new Color(155, 125, 130), 0.62f, new Color(255, 80, 30),   0.48f), // Volcanic / Basalt World
    ];

    public struct MenuPassingPlanet
    {
      public Vector2 Position;
      public float Scale;
      public int StyleIndex;
      public int FrameOffset;
      public float SpinSpeed;
      public float Orientation;
      public SpriteEffects Flip;
      public int LaneIndex;

      public float Radius => 31f * Scale;
    }

    public sealed class MenuFlightHandoff
    {
      public Vector2 ShipWorldPosition;
      public float ShipRotation;
      public float BackgroundScroll;
      public float FlightTime;
      public List<MenuPassingPlanet> PassingPlanets = new();
    }

    private sealed class MenuHullTurret
    {
      public readonly string Head, Base;
      public readonly Vector2 Mount;
      public float Aim;

      public MenuHullTurret(string head, string @base, Vector2 mount, float aim)
      {
        Head = head;
        Base = @base;
        Mount = mount;
        Aim = aim;
      }
    }

    // Starter twin front cannon turrets aimed straight ahead (upward along the flight path)
    private readonly MenuHullTurret m_cannonTurretLeft =
      new(FleetAtlas.TurretCannon, FleetAtlas.TurretCannonBase, new(-17f, -30f), -MathHelper.PiOver2);
    private readonly MenuHullTurret m_cannonTurretRight =
      new(FleetAtlas.TurretCannon, FleetAtlas.TurretCannonBase, new(17f, -30f), -MathHelper.PiOver2);

    private readonly List<MenuPassingPlanet> m_passingPlanets = new();
    private bool m_flightSceneSeeded;
    private float m_flightTime;
    private float m_backgroundScroll;
    private Vector2 m_shipPosition;
    private float m_shipRotation;
    private int m_nextPlanetStyleIndex;
    private int m_lastPlanetLane = -1;

    private static Vector2 GetMenuShipBasePosition(Vector2 p0, Vector2 p1)
      => new(MathHelper.Lerp(p0.X, p1.X, 0.43f), MathHelper.Lerp(p0.Y, p1.Y, 0.56f));

    private static float GetLaneWorldX(Vector2 p0, Vector2 p1, int laneIndex)
    {
      float t = laneIndex switch
      {
        0 => 0.61f,
        1 => 0.85f,
        _ => 0.73f,
      };
      return MathHelper.Lerp(p0.X, p1.X, t);
    }

    private MenuPassingPlanet CreatePassingPlanet(Vector2 p0, Vector2 p1, float y, int laneIndex, float scale)
    {
      int styleIdx = m_nextPlanetStyleIndex++ % PassingPlanetStyles.Length;
      m_lastPlanetLane = laneIndex;
      SpriteEffects flip = (styleIdx % 3) switch
      {
        1 => SpriteEffects.FlipHorizontally,
        2 => SpriteEffects.FlipVertically,
        _ => SpriteEffects.None,
      };
      return new MenuPassingPlanet
      {
        Position = new Vector2(GetLaneWorldX(p0, p1, laneIndex), y),
        Scale = scale,
        StyleIndex = styleIdx,
        FrameOffset = (styleIdx * 19 + 7) % MenuPlanetFrameCount,
        SpinSpeed = 0.75f + (styleIdx % 3) * 0.18f,
        Orientation = styleIdx * 1.05f,
        Flip = flip,
        LaneIndex = laneIndex,
      };
    }

    private void EnsureFlightSceneInitialized(Vector2 p0, Vector2 p1)
    {
      if (m_flightSceneSeeded || p1.X <= p0.X + 10f) return;
      m_flightSceneSeeded = true;

      m_shipPosition = GetMenuShipBasePosition(p0, p1);
      m_shipRotation = 0f;

      m_passingPlanets.Clear();
      m_passingPlanets.Add(CreatePassingPlanet(p0, p1, MathHelper.Lerp(p0.Y, p1.Y, 0.24f), laneIndex: 1, scale: 1.95f));
      m_passingPlanets.Add(CreatePassingPlanet(p0, p1, MathHelper.Lerp(p0.Y, p1.Y, 0.78f), laneIndex: 0, scale: 1.32f));
      m_passingPlanets.Add(CreatePassingPlanet(p0, p1, p0.Y - 110f, laneIndex: 2, scale: 1.62f));
    }

    private void UpdateFlightScene(float dt, Vector2 p0, Vector2 p1)
    {
      EnsureFlightSceneInitialized(p0, p1);
      m_flightTime += dt;
      m_backgroundScroll += dt * MenuScrollSpeed;

      var baseShipPos = GetMenuShipBasePosition(p0, p1);
      m_shipPosition = baseShipPos + new Vector2(
        MathF.Sin(m_flightTime * 0.72f) * 3.5f,
        MathF.Cos(m_flightTime * 0.52f) * 2.5f);
      m_shipRotation = MathF.Cos(m_flightTime * 0.72f) * 0.014f;

      float highestTopEdge = p0.Y;
      for (int i = 0; i < m_passingPlanets.Count; i++)
      {
        float topEdge = m_passingPlanets[i].Position.Y - m_passingPlanets[i].Radius;
        if (topEdge < highestTopEdge) highestTopEdge = topEdge;
      }

      for (int i = 0; i < m_passingPlanets.Count; i++)
      {
        var planet = m_passingPlanets[i];
        planet.Position.X = GetLaneWorldX(p0, p1, planet.LaneIndex);
        planet.Position.Y += dt * MenuScrollSpeed;

        if (planet.Position.Y - planet.Radius > p1.Y + 28f)
        {
          int nextLane = (m_lastPlanetLane + 1 + RandomHelper.Int(0, 1)) % 3;
          float nextScale = nextLane switch
          {
            0 => RandomHelper.Float(1.20f, 1.50f),
            1 => RandomHelper.Float(1.75f, 2.05f),
            _ => RandomHelper.Float(1.45f, 1.75f),
          };
          float nextRadius = 31f * nextScale;
          float spawnY = Math.Min(p0.Y, highestTopEdge) - nextRadius - RandomHelper.Float(65f, 100f);
          planet = CreatePassingPlanet(p0, p1, spawnY, nextLane, nextScale);
          highestTopEdge = spawnY - nextRadius;
        }
        m_passingPlanets[i] = planet;
      }
    }

    public static void DrawScrollingSpaceBackground(SpriteBatch spriteBatch, OrthographicCamera backgroundCamera, float scrollDistance)
    {
      if (EffectCache.BackgroundEffect?.IsLoaded != true || EffectCache.BackgroundEffect.IsFailed) return;
      if (TextureCache.SpaceBackground?.IsLoaded != true || TextureCache.SpaceBackground.IsFailed) return;

      var effect = EffectCache.BackgroundEffect.Value;
      effect.Parameters["view_projection"]?.SetValue(backgroundCamera.ViewProjection());

      var bkg = TextureCache.SpaceBackground.Value;
      var bounds = new Rectangle(bkg.Bounds.X, bkg.Bounds.Y, bkg.Bounds.Width * 5, bkg.Bounds.Height * 5);
      var basePos = new Vector2(-bkg.Width * 5, -bkg.Height * 5);
      // 1 source texel = 2 world units (size is 10x, bounds is 5x), so one full repeat is bkg.Height * 2f world units.
      float repeatHeight = bkg.Height * 2f;
      float y1 = (scrollDistance * 0.45f) % repeatHeight;
      float y2 = (scrollDistance * 0.72f) % repeatHeight;
      float y3 = (scrollDistance * 1.00f) % repeatHeight;

      spriteBatch.Begin(effect: effect, depthStencilState: DepthStencilState.Default, samplerState: SamplerState.AnisotropicWrap);
      spriteBatch.Draw(TextureCache.SpaceBackground, basePos + new Vector2(0f, y1), bounds,
        Color.White, 0f, Vector2.Zero, new Vector2(2f), SpriteEffects.None, 0f);
      spriteBatch.Draw(TextureCache.SpaceBackground2, basePos + new Vector2(0f, y2), bounds,
        Color.White, 0f, Vector2.Zero, new Vector2(2f), SpriteEffects.None, 0f);
      spriteBatch.Draw(TextureCache.SpaceBackground3, basePos + new Vector2(0f, y3), bounds,
        Color.White, 0f, Vector2.Zero, new Vector2(2f), SpriteEffects.None, 0f);
      spriteBatch.End();
    }

    public static void DrawPassingPlanets(SpriteBatch spriteBatch, OrthographicCamera camera, IReadOnlyList<MenuPassingPlanet> planets, float time)
    {
      if (planets == null || planets.Count == 0) return;
      if (TextureCache.Planet?.IsLoaded != true || TextureCache.Planet.IsFailed) return;

      var texture = TextureCache.Planet.Value;
      var sheetOrigin = new Vector2(MenuPlanetSheetFrameSize * 0.5f);

      spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
        transformMatrix: camera.GetViewMatrix());
      for (int i = 0; i < planets.Count; i++)
      {
        var planet = planets[i];
        var style = PassingPlanetStyles[planet.StyleIndex % PassingPlanetStyles.Length];
        int frame = ((int)(time * planet.SpinSpeed / MenuPlanetFrameSeconds) + planet.FrameOffset) % MenuPlanetFrameCount;
        var source = new Rectangle(
          frame % MenuPlanetSheetColumns * MenuPlanetSheetFrameSize,
          frame / MenuPlanetSheetColumns * MenuPlanetSheetFrameSize,
          MenuPlanetSheetFrameSize, MenuPlanetSheetFrameSize);
        float sheetScale = planet.Scale * MenuPlanetFrameSize / MenuPlanetSheetFrameSize;
        Color baseTint = style.SurfaceTint * style.SurfaceBrightness;
        baseTint.A = 255;
        spriteBatch.Draw(texture, planet.Position, source, baseTint, planet.Orientation,
          sheetOrigin, sheetScale, planet.Flip, 0f);
      }
      spriteBatch.End();

      spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
        transformMatrix: camera.GetViewMatrix());
      for (int i = 0; i < planets.Count; i++)
      {
        var planet = planets[i];
        var style = PassingPlanetStyles[planet.StyleIndex % PassingPlanetStyles.Length];
        float glow = style.GlowIntensity * (0.90f + 0.10f * MathF.Sin(time * 2.1f + i * 1.7f));
        if (glow <= 0.001f) continue;
        int frame = ((int)(time * planet.SpinSpeed / MenuPlanetFrameSeconds) + planet.FrameOffset) % MenuPlanetFrameCount;
        var source = new Rectangle(
          frame % MenuPlanetSheetColumns * MenuPlanetSheetFrameSize,
          frame / MenuPlanetSheetColumns * MenuPlanetSheetFrameSize,
          MenuPlanetSheetFrameSize, MenuPlanetSheetFrameSize);
        float sheetScale = planet.Scale * MenuPlanetFrameSize / MenuPlanetSheetFrameSize;
        spriteBatch.Draw(texture, planet.Position, source, style.GlowTint * glow, planet.Orientation,
          sheetOrigin, sheetScale, planet.Flip, 0f);
      }
      spriteBatch.End();
    }

    public override void UnloadContent()
    {
      Loc.Changed -= OnLanguageChanged;
      wishlistButton.Click -= WishlistClicked;
      if (feedbackButton != null)
        feedbackButton.Click -= FeedbackClicked;
      PlaytestFeedback.Close();
      CloseNewGameConfirmation();
      // The Gum screen is reused; clicks must target the current MainMenu instance.
      ((Gum.Forms.DefaultFromFileVisuals.DefaultFromFileButtonRuntime)m_menuScreen.GetChildByNameRecursively("ButtonNewGame")).Click -= NewGameClicked;
      ((Gum.Forms.DefaultFromFileVisuals.DefaultFromFileButtonRuntime)m_menuScreen.GetChildByNameRecursively("ButtonContinue")).Click -= ContinueClicked;
      ((Gum.Forms.DefaultFromFileVisuals.DefaultFromFileButtonRuntime)m_menuScreen.GetChildByNameRecursively("ButtonSettings")).Click -= SettingsClicked;
      ((Gum.Forms.DefaultFromFileVisuals.DefaultFromFileButtonRuntime)m_menuScreen.GetChildByNameRecursively("ButtonCredits")).Click -= CreditsClicked;
      ((Gum.Forms.DefaultFromFileVisuals.DefaultFromFileButtonRuntime)m_menuScreen.GetChildByNameRecursively("ButtonExit")).Click -= ExitClicked;
      m_gpuParticles?.Dispose();
      m_gpuParticles = null;
      GameMain.RemoveCustomHudContent(DrawMenu);
      preparationSteps?.Dispose();
      preparationSteps = null;
      preparingGame?.UnloadContent();
      preparingGame = null;
      base.UnloadContent();
    }

    private GpuParticleSystem m_gpuParticles;
    private Effect m_bloomExtractEffect;
    private float m_thrusterParticleClock;
    private int m_thrusterParticleBeat;

    private GpuParticleSystem GetParticleSystem()
    {
#if KNI_WEB
      return null;
#else
      if (m_gpuParticles != null) return m_gpuParticles;
      if (EffectCache.GpuParticlesFx?.IsLoaded != true || EffectCache.GpuParticlesFx.IsFailed) return null;
      return m_gpuParticles = new GpuParticleSystem(GraphicsDevice, EffectCache.GpuParticlesFx.Value, 256);
#endif
    }

    private void UpdateThrusterParticles(float dt)
    {
      if (!m_flightSceneSeeded) return;
      m_thrusterParticleClock += dt;
      if (m_thrusterParticleClock >= 1f / 60f)
      {
        m_thrusterParticleClock %= 1f / 60f;
        var particles = GetParticleSystem();
        if (particles != null)
        {
          m_thrusterParticleBeat++;
          int maxBursts = Math.Min(8, GpuParticleSystem.MaxPendingBursts - particles.PendingBursts);
          for (int nozzle = 0; nozzle < maxBursts; nozzle++)
          {
            if (!PrerenderedShips.TryCreateThrusterBurst(Harvester.HarvesterType.HomeBase, m_shipPosition,
              m_shipRotation, new Vector2(MenuHomeHullScale), nozzle, m_thrusterParticleBeat,
              1f, boosted: true, out var burst, menuScale: true))
              break;
            particles.Emit(burst);
          }
        }
      }
      m_gpuParticles?.Update(dt);
    }

    private void DrawMenu()
    {
      GumService.Default.Draw();

      if (GameMain.CurrentMenu == "MainMenu")
        DrawLogo(m_spriteBatch, GraphicsDevice, Color.White);

      PlaytestFeedback.Draw(m_spriteBatch);
      menuFrameDrawn = true;
      TextureCache.RequestIconPreload();
    }

    private bool m_prevCaptureLeft;
    private string previousButtonName = "null";
    private bool? pendingStart;
    private readonly bool hasSave;
    private bool menuFrameDrawn;
    private bool preparationFrameAvailable;
    private bool preparedNewGame;
    private bool preparedFrameDrawn;
    private bool newGameCommitted;
    private bool newGameCommitFrameDrawn;
    private UntitledGemGameGameScreen preparingGame;
    private IEnumerator<string> preparationSteps;
    internal bool GameplayReady => GameplayPreloader.Ready && preparingGame != null
      && preparationSteps == null && preparedFrameDrawn;

    private void UpdatePreparation()
    {
      // Draw grants one slice, so fixed-step catch-up cannot drain the load before a paint.
      if (!menuFrameDrawn || !preparationFrameAvailable) return;
      preparationFrameAvailable = false;
      GameplayPreloader.Update();
      if (!GameplayPreloader.Ready) return;
#if !KNI_WEB
      // Capture stages its own world after the asset batch; leave its singletons alone.
      if (Capture.CaptureSession.Active) return;
#endif
      bool wantedNewGame = pendingStart ?? !hasSave;
      if (preparingGame != null && preparationSteps == null && preparedNewGame != wantedNewGame)
      {
        // A confirmed New Game can replace a world prepared for Continue. Never save it.
        preparingGame.UnloadContent();
        preparingGame = null;
        preparedFrameDrawn = false;
      }
      if (preparingGame == null)
      {
        preparedNewGame = wantedNewGame;
        preparingGame = new UntitledGemGameGameScreen(Game, preparedNewGame);
        preparingGame.IntroTransitionPending = true;
        preparingGame.Initialize();
        preparationSteps = preparingGame.PrepareGameSteps(deferActivation: true).GetEnumerator();
      }
      if (preparationSteps == null) return;
      long started = System.Diagnostics.Stopwatch.GetTimestamp();
      do
      {
        if (!preparationSteps.MoveNext())
        {
          preparationSteps.Dispose();
          preparationSteps = null;
          break;
        }
#if KNI_WEB
        if (pendingStart.HasValue) UntitledGemGame.Web.WebBootstrap.SetLoading(preparationSteps.Current);
#endif
      } while (System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds < 2);
    }

    private static Vector2 HullMount(Vector2 homePos, float hullRotation, float x, float y)
    {
      var offset = new Vector2(x, y) * MenuHomeHullScale;
      if (hullRotation != 0f)
        offset = Vector2.Transform(offset, Matrix.CreateRotationZ(hullRotation));
      return homePos + offset;
    }

    private void DrawFlyingHomeBase(Vector2 homePos, float hullRotation, float totalTime)
    {
      var shipTransform = new Transform2(homePos, hullRotation, new Vector2(MenuHomeHullScale));
      if (!PrerenderedShips.DrawMenuShip(m_spriteBatch, Harvester.HarvesterType.HomeBase, 0,
        shipTransform, Color.White, 1f, totalTime, boost: 1.08f))
      {
        if (PrerenderedShips.TryGet(Harvester.HarvesterType.HomeBase, hullRotation, out var prerendered,
          out var prerenderedPivot, out float prerenderedRotation, out float prerenderedScale))
        {
          m_spriteBatch.Draw(prerendered.Texture, homePos, prerendered.Bounds, Color.White,
            prerenderedRotation, prerenderedPivot, new Vector2(MenuHomeHullScale * prerenderedScale),
            SpriteEffects.None, 0f);
        }
      }

      var fleet = TextureCache.Fleet;
      if (fleet == null) return;

      var termRegion = fleet.Region(FleetAtlas.HullTerminal);
      float termScale = MenuTurretTexel * MenuHomeHullScale * MenuShipTerminalScaleMultiplier;
      m_spriteBatch.Draw(fleet.Texture, HullMount(homePos, hullRotation, MenuShipTerminalMount.X, MenuShipTerminalMount.Y),
        termRegion.Bounds, Color.White, hullRotation, fleet.Pivot(FleetAtlas.HullTerminal), termScale, SpriteEffects.None, 0f);

      foreach (var turret in new[] { m_cannonTurretLeft, m_cannonTurretRight })
      {
        var pivot = HullMount(homePos, hullRotation, turret.Mount.X, turret.Mount.Y);
        float scale = MenuTurretTexel * MenuHomeHullScale;
        float aim = turret.Aim + hullRotation;
        var baseRegion = fleet.Region(turret.Base);
        m_spriteBatch.Draw(fleet.Texture, pivot, baseRegion.Bounds, Color.White, hullRotation,
          fleet.Pivot(turret.Base), scale, SpriteEffects.None, 0f);
        var head = PrerenderedShips.Frame(fleet, turret.Head, aim + MathHelper.PiOver2, out var origin, out float rotation);
        m_spriteBatch.Draw(fleet.Texture, pivot, head.Bounds, Color.White, rotation, origin, scale,
          SpriteEffects.None, 0f);
      }
    }

    public override void Update(GameTime gameTime)
    {
      if (!GameplayPreloader.MenuReady) return;
      if (PlaytestFeedback.IsOpen)
      {
        PlaytestFeedback.Update(gameTime);
        GumService.Default.Cursor.ClearInputValues();
        GumService.Default.Cursor.VisualPushed = null;
        GumService.Default.Cursor.VisualOver = null;
      }
      if (pendingStart.HasValue && !newGameCommitted && (GameplayPreloader.Error != null
        || GameMain.ActiveMenuScreen != "MainMenu"
        || (!PlaytestFeedback.IsOpen && KeyboardExtended.GetState().WasKeyPressed(Keys.Escape))))
      {
        pendingStart = null;
#if KNI_WEB
        UntitledGemGame.Web.WebBootstrap.SetLoading(null);
#endif
        m_menuScreen.GetChildByNameRecursively("ButtonNewGame").SetProperty("Text", Loc.N("New Game"));
        m_menuScreen.GetChildByNameRecursively("ButtonContinue").SetProperty("Text", Loc.N("Continue"));
        if (GameplayPreloader.Error != null)
          m_menuScreen.GetChildByNameRecursively("VersionLabel").SetProperty("Text", Loc.N("Unable to load game assets. Please restart the game."));
      }
      UpdatePreparation();
#if KNI_WEB
      if (pendingStart.HasValue && !GameplayPreloader.Ready)
        UntitledGemGame.Web.WebBootstrap.SetLoading("Preparing game assets…", GameplayPreloader.Progress);
#endif
      if (pendingStart is bool startNew && GameplayReady && preparedNewGame == startNew)
      {
        if (startNew && !newGameCommitted)
        {
          preparingGame.CommitPreparedNewGame();
          newGameCommitted = true;
          return;
        }
        if (startNew && !newGameCommitFrameDrawn) return;
        pendingStart = null;
        CompleteEnterGame(preparingGame);
#if KNI_WEB
        UntitledGemGame.Web.WebBootstrap.SetLoading(null);
#endif
        return;
      }
      var vp = BaseGame.BoxingViewportAdapterGui.Viewport;
      var scale = BaseGame.BoxingViewportAdapterGui.GetScaleMatrix();
      Matrix.Invert(ref scale, out scale);
      GumService.Default.Cursor.TransformMatrix = Matrix.CreateTranslation(-vp.X, -vp.Y, 0) * scale;
      if (!GameMain.SoundEffectsLabCapturesInput && !PlaytestFeedback.IsOpen) GumService.Default.Update(gameTime);
      else
      {
        GumService.Default.Cursor.ClearInputValues();
        GumService.Default.Cursor.VisualPushed = null;
        GumService.Default.Cursor.VisualOver = null;
      }
#if !KNI_WEB
      if (Capture.CaptureSession.Active && Capture.CaptureSession.Pointer.Visible)
      {
        bool clicked = Capture.CaptureSession.Pointer.Left && !m_prevCaptureLeft;
        m_prevCaptureLeft = Capture.CaptureSession.Pointer.Left;
        float cx = Capture.CaptureSession.Pointer.Position.X * 3840f;
        float cy = Capture.CaptureSession.Pointer.Position.Y * 2160f;
        if (GameMain.ActiveMenuScreen == "MainMenu")
        {
          foreach (string btnName in new[] { "ButtonContinue", "ButtonNewGame", "ButtonSettings", "ButtonCredits", "ButtonExit", "ButtonFeedback", "ButtonWishlist" })
          {
            if (m_menuScreen.GetChildByNameRecursively(btnName) is GraphicalUiElement btn && btn.Visible)
            {
              float bx = btn.GetAbsoluteLeft();
              float by = btn.GetAbsoluteTop();
              float bw = btn.GetAbsoluteWidth();
              float bh = btn.GetAbsoluteHeight();
              bool inside = cx >= bx && cx <= bx + bw && cy >= by && cy <= by + bh;
              btn.SetProperty("ButtonCategoryState", inside ? (Capture.CaptureSession.Pointer.Left ? "Pushed" : "Highlighted") : "Enabled");
              if (clicked && inside)
              {
                if (btnName == "ButtonContinue") ContinueClicked(btn, EventArgs.Empty);
                else if (btnName == "ButtonNewGame") NewGameClicked(btn, EventArgs.Empty);
                else if (btnName == "ButtonSettings") SettingsClicked(btn, EventArgs.Empty);
                else if (btnName == "ButtonCredits") CreditsClicked(btn, EventArgs.Empty);
              }
            }
          }
        }
        else if (GameMain.ActiveMenuScreen == "SettingsMenu" && GameMain.SettingsMenuScreen is GraphicalUiElement settingsScreen)
        {
          if (settingsScreen.GetChildByNameRecursively("ButtonBack") is GraphicalUiElement backBtn)
          {
            float bx = backBtn.GetAbsoluteLeft();
            float by = backBtn.GetAbsoluteTop();
            float bw = backBtn.GetAbsoluteWidth();
            float bh = backBtn.GetAbsoluteHeight();
            bool inside = cx >= bx && cx <= bx + bw && cy >= by && cy <= by + bh;
            backBtn.SetProperty("ButtonCategoryState", inside ? (Capture.CaptureSession.Pointer.Left ? "Pushed" : "Highlighted") : "Enabled");
            if (clicked && inside)
            {
              AudioManager.Instance.PlaySound(SoundEvent.MenuClick);
              GameMain.SwapMenu("MainMenu");
            }
          }
        }
      }
#endif
      if (newGameDialog != null && !PlaytestFeedback.IsOpen && KeyboardExtended.GetState().WasKeyPressed(Keys.Escape))
        CloseNewGameConfirmation();
      else if (newGameDialog == null && GameMain.ActiveMenuScreen != "MainMenu" && !PlaytestFeedback.IsOpen
        && KeyboardExtended.GetState().WasKeyPressed(Keys.Escape))
      {
        AudioManager.Instance.PlaySound(SoundEvent.MenuClick);
        GameMain.SwapMenu("MainMenu");
      }

      var curOverButtonName = GumService.Default.Cursor.WindowOver?.Name ?? "null";

      if (curOverButtonName != previousButtonName && curOverButtonName.Contains("Button"))
      {
        if (curOverButtonName != "null")
        {
          AudioManager.Instance.PlaySound(SoundEvent.MenuHover);
        }
      }

      previousButtonName = curOverButtonName;

      float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
      var worldVp = BaseGame.BoxingViewportAdapter.Viewport;
      var p0 = m_camera.ScreenToWorld(new Vector2(worldVp.X, worldVp.Y));
      var p1 = m_camera.ScreenToWorld(new Vector2(worldVp.X + worldVp.Width, worldVp.Y + worldVp.Height));

      UpdateFlightScene(dt, p0, p1);
      UpdateThrusterParticles(dt);
      AudioManager.Instance.Update(gameTime, musicInMenu: true);
    }

    private void StartGame(bool newGame)
    {
      if (newGameCommitted) return;
      // Change screens from Update, after Gum has finished dispatching the click.
      pendingStart = newGame;
      m_menuScreen.GetChildByNameRecursively("ButtonNewGame").SetProperty("Text", Loc.N("New Game"));
      m_menuScreen.GetChildByNameRecursively("ButtonContinue").SetProperty("Text", Loc.N("Continue"));
      var button = m_menuScreen.GetChildByNameRecursively(newGame ? "ButtonNewGame" : "ButtonContinue");
      button.SetProperty("Text", Loc.N("Loading..."));
    }

    private MenuFlightHandoff CaptureFlightHandoff()
    {
      var worldVp = BaseGame.BoxingViewportAdapter.Viewport;
      var p0 = m_camera.ScreenToWorld(new Vector2(worldVp.X, worldVp.Y));
      var p1 = m_camera.ScreenToWorld(new Vector2(worldVp.X + worldVp.Width, worldVp.Y + worldVp.Height));
      EnsureFlightSceneInitialized(p0, p1);

      var handoff = new MenuFlightHandoff
      {
        ShipWorldPosition = m_shipPosition,
        ShipRotation = m_shipRotation,
        BackgroundScroll = m_backgroundScroll,
        FlightTime = m_flightTime,
      };
      foreach (var planet in m_passingPlanets)
      {
        // Only hand off planets that have already entered the top of the screen;
        // our destination gameplay planet takes the slot above the top edge.
        if (planet.Position.Y + planet.Radius > p0.Y + 6f)
          handoff.PassingPlanets.Add(planet);
      }
      return handoff;
    }

    private void CompleteEnterGame(UntitledGemGameGameScreen gameScreen)
    {
      m_menuScreen.GetChildByNameRecursively("ButtonNewGame").SetProperty("Text", Loc.N("New Game"));
      m_menuScreen.GetChildByNameRecursively("ButtonContinue").SetProperty("Text", Loc.N("Continue"));
      gameScreen.SetMenuFlightHandoff(CaptureFlightHandoff());
      gameScreen.ActivatePreparedGame(saveNewGame: false);
      preparingGame = null;
      GumService.Default.Root.Children.Clear();
      GumService.Default.ModalRoot.Children.Clear();
      GameMain.RemoveCustomHudContent(DrawMenu);
      var particles = m_gpuParticles;
      m_gpuParticles = null;
      gameScreen.AdoptMenuParticles(particles);
      gameScreen.IntroTransitionPending = false;
      AudioManager.Instance.PlaySound(SoundEvent.ShipEngineDying);

      GameMain.CurrentMenu = "GameMenu";
      ScreenManager.ReplaceScreen(gameScreen);
    }

#if !KNI_WEB
    public Vector2 Measure2(string Text, Vector2 position, float FontSize)
    {
      var r = FontManager.GetTextRenderer("Roboto_Regular_ttf");
      r.PositiveYIsDown = true;
      r.ResetLayout();

      var fontSize = FontSize;
      var measure = r.MeasureText(Text, position, 0, 0, fontSize, Color.Transparent, Color.Transparent, r.EnableKerning, r.PositiveYIsDown, r.PositionByBaseline, 0, new Vector2(0, 0), true, -1);
      return measure;
    }
#endif

    float map(float x, float in_min, float in_max, float out_min, float out_max)
    {
      return (x - in_min) * (out_max - out_min) / (in_max - in_min) + out_min;
    }

    public override void Draw(GameTime gameTime)
    {
      preparationFrameAvailable = true;
      if (newGameCommitted) newGameCommitFrameDrawn = true;
      if (preparingGame != null && preparationSteps == null && !preparedFrameDrawn)
      {
        // Exercise the actual gameplay draw paths before the handoff, so first-use GPU
        // setup belongs to loading. The menu redraws the scene target immediately below.
        preparingGame.Draw(new GameTime());
        GraphicsDevice.Clear(Color.Black);
        preparedFrameDrawn = true;
      }
      m_gpuParticles?.Simulate();
      m_bloomExtractEffect ??= AssetManager.Load<Effect>("Shaders/BloomExtract.fx");
      m_bloomExtractEffect?.Parameters["ShipOrangeBloom"]?.SetValue(UntitledGemGameGameScreen.ShipOrangeBloom);

      m_camera_background.Zoom = RenderQuality.WorldZoom(map(m_camera.Zoom / RenderQuality.WorldScale, 0, 3.0f, 0.3f, 1.0f));
      DrawScrollingSpaceBackground(m_spriteBatch, m_camera_background, m_backgroundScroll);

      var vp = BaseGame.BoxingViewportAdapter.Viewport;
      var p0 = m_camera.ScreenToWorld(new Vector2(vp.X, vp.Y));
      var p1 = m_camera.ScreenToWorld(new Vector2(vp.X + vp.Width, vp.Y + vp.Height));
      EnsureFlightSceneInitialized(p0, p1);

      DrawPassingPlanets(m_spriteBatch, m_camera, m_passingPlanets, m_flightTime);

      // The engines sit beneath the hull, so their particles must stay behind the ship.
      m_gpuParticles?.Draw(m_camera.ViewProjection());

      float totalTime = (float)gameTime.TotalGameTime.TotalSeconds;
      m_spriteBatch.Begin(transformMatrix: m_camera.GetViewMatrix(), samplerState: SamplerState.LinearClamp);
      DrawFlyingHomeBase(m_shipPosition, m_shipRotation, totalTime);
      m_spriteBatch.End();

      var width = GameMain.Instance.GraphicsDevice.Viewport.Width;
      var height = GameMain.Instance.GraphicsDevice.Viewport.Height;

#if !KNI_WEB
      //FIXME: This needs to be here or text gets wonky, timing issue so could probably be moved but gotta be the correct timing
      var textRenderer = FontManager.GetTextRenderer(nameof(ContentDirectory.Fonts.Roboto_Regular_ttf));
      textRenderer.SetOrtographicProjection(width, height);
      textRenderer.UseScreenSpace = false;
#endif
    }
  }
}
