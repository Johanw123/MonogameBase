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
using Microsoft.Xna.Framework.Media;
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
    private SpriteBatch m_spriteBatch;
    private GraphicalUiElement m_menuScreen;
    private OrthographicCamera m_camera;
    private OrthographicCamera m_camera_background;

    private GraphicalUiElement newGameDialog;

    public MainMenu(Game game, GraphicalUiElement menuScreen)
    : base(game)
    {
      m_menuScreen = menuScreen;
      game.IsMouseVisible = true;

      // m_camera = JapeFramework.BaseGame.Camera;
      m_camera = new OrthographicCamera(GameMain.BoxingViewportAdapter);
      m_camera.Zoom = 1.5f;

      m_camera_background = new OrthographicCamera(GameMain.BoxingViewportAdapter);
      m_camera_background.Zoom = 1.5f;

      Init();
      UpdateVersionLabel();
      Loc.Changed += OnLanguageChanged;
      m_menuScreen.GetChildByNameRecursively("ButtonContinue").Visible =
        new GameSaveStore(GameSaveStore.DefaultPath).Load() != null;

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
        label = MenuText(0.5f);
        label.Name = "VersionLabel";
        label.WidthUnits = Gum.DataTypes.DimensionUnitType.Absolute;
        label.HeightUnits = Gum.DataTypes.DimensionUnitType.Absolute;
        label.Width = 500;
        label.Height = 70;
        label.Color = HudLayout.ButtonTextColor;
        label.HorizontalAlignment = HorizontalAlignment.Right;
        label.VerticalAlignment = VerticalAlignment.Center;
        m_menuScreen.Children.Add(label);
        label.Anchor(Anchor.BottomRight);
        label.X = -48;
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

      newGame.Click += NewGameClicked;
      continueGame.Click += ContinueClicked;
      settings.Click += SettingsClicked;
      credits.Click += CreditsClicked;
      exit.Click += ExitClicked;
    }

    private void NewGameClicked(object sender, EventArgs args)
    {
      AudioManager.Instance.PlaySound(AudioManager.Instance.MenuClickButtonSoundEffect);
      if (File.Exists(GameSaveStore.DefaultPath) || File.Exists(GameSaveStore.DefaultPath + ".bak"))
        ShowNewGameConfirmation();
      else
        StartGame(newGame: true);
    }

    private void ContinueClicked(object sender, EventArgs args)
    {
      AudioManager.Instance.PlaySound(AudioManager.Instance.MenuClickButtonSoundEffect);
      StartGame(newGame: false);
    }

    private void SettingsClicked(object sender, EventArgs args)
    {
      AudioManager.Instance.PlaySound(AudioManager.Instance.MenuClickButtonSoundEffect);
      GameMain.SwapMenu("SettingsMenu");
    }

    private void CreditsClicked(object sender, EventArgs args)
    {
      AudioManager.Instance.PlaySound(AudioManager.Instance.MenuClickButtonSoundEffect);
      GameMain.SwapMenu("CreditsMenu");
    }

    private void ExitClicked(object sender, EventArgs args)
    {
      AudioManager.Instance.PlaySound(AudioManager.Instance.MenuClickButtonSoundEffect);
      Game.Exit();
    }

    private void ShowNewGameConfirmation()
    {
      if (newGameDialog != null)
        return;

      // ModalRoot does not necessarily have the same dimensions as the menu root.
      var overlay = new Gum.GueDeriving.ContainerRuntime
      {
        WidthUnits = Gum.DataTypes.DimensionUnitType.Absolute,
        HeightUnits = Gum.DataTypes.DimensionUnitType.Absolute,
        Width = GumService.Default.CanvasWidth,
        Height = GumService.Default.CanvasHeight
      };
      newGameDialog = overlay;

      var shade = new Gum.GueDeriving.RectangleRuntime
      {
        WidthUnits = Gum.DataTypes.DimensionUnitType.RelativeToParent,
        HeightUnits = Gum.DataTypes.DimensionUnitType.RelativeToParent,
        Width = 0, Height = 0, IsFilled = true,
        FillColor = new Color(0, 0, 0, 180), StrokeWidth = 0
      };
      overlay.Children.Add(shade);

      var panel = new Gum.GueDeriving.ContainerRuntime { Width = 1400, Height = 540 };
      overlay.Children.Add(panel);
      panel.Anchor(Anchor.Center);
      panel.Children.Add(new Gum.GueDeriving.RectangleRuntime
      {
        Width = 1400, Height = 540, IsFilled = true,
        FillColor = OrbitSkin.PanelBackground,
        StrokeWidth = 0, CornerRadius = 0
      });
      panel.Children.Add(OrbitSkin.GumSurface("modal_info_complete", 1400, 540));
      // The 70px menu font, downscaled instead of enlarging the default font.
      var message = MenuText(0.75f);
      message.Text = Loc.N("Start a new game?\nYour existing progress will be replaced.");
      message.WidthUnits = Gum.DataTypes.DimensionUnitType.Absolute;
      message.HeightUnits = Gum.DataTypes.DimensionUnitType.Absolute;
      message.X = 80;
      message.Y = 60;
      message.Width = 1240;
      message.Height = 250;
      message.Color = OrbitSkin.ButtonTextColor;
      message.HorizontalAlignment = HorizontalAlignment.Center;
      message.VerticalAlignment = VerticalAlignment.Center;
      panel.Children.Add(message);

      var cancel = CreateDialogButton(Loc.N("Cancel"), 80);
      var confirm = CreateDialogButton(Loc.N("New Game"), 720);
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
        GameMain.GumProject.GetComponentSave("Controls/ButtonMainMenu").ToGraphicalUiElement();
      button.SetProperty("Text", text);
      button.WidthUnits = button.HeightUnits = Gum.DataTypes.DimensionUnitType.Absolute;
      button.Width = 600;
      button.Height = 100;
      button.X = x;
      button.Y = 360;
      return button;
    }

    private void CloseNewGameConfirmation()
    {
      if (newGameDialog == null)
        return;
      GumService.Default.ModalRoot.Children.Remove(newGameDialog);
      newGameDialog = null;
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

#if !KNI_WEB
      FontManager.InitFieldFont("Roboto_Regular_ttf", "Fonts/Roboto-Regular.ttf");

      var width = GameMain.Instance.GraphicsDevice.Viewport.Width;
      var height = GameMain.Instance.GraphicsDevice.Viewport.Height;

      var textRenderer = FontManager.GetTextRenderer(nameof(ContentDirectory.Fonts.Roboto_Regular_ttf));
      textRenderer.SetOrtographicProjection(width, height);
      textRenderer.UseScreenSpace = false;
#endif

      GameMain.AddCustomHudContent(DrawMenu);
    }

    // The fleet arrives in squadrons instead of appearing at once: each streaks in from off
    // screen along a shared heading, brakes to cruising speed in a V at its landing spot,
    // then breaks formation to wander.
    private const int FleetSize = 100;
    private const float ArrivalSeconds = 1.8f;
    // The last squadron drops in this long after the first.
    private const float ArrivalWindowSeconds = 2.6f;
    // Up and to the right, so the fleet sweeps in from the lower left.
    private const float ArrivalHeading = -0.35f;
    private const float FormationSpacing = 44f;
    // Exponential braking rate over the arrival; higher arrives faster and brakes harder.
    private const float ArrivalBraking = 6f;
    private static readonly Color WarpTint = new(150, 210, 255);

    private static float CruiseSpeed => 100.0f * HomeBase.BonusMoveSpeed;

    void SpawnHarvesters()
    {
      var vp = BaseGame.BoxingViewportAdapter.Viewport;

      var p0 = m_camera.ScreenToWorld(new Vector2(vp.X, vp.Y));
      var p1 = m_camera.ScreenToWorld(new Vector2(vp.X + vp.Width, vp.Y + vp.Height));

      AudioManager.Instance.FadeInSong("Greys", 3f);
      MediaPlayer.IsRepeating = true;

      var squadrons = new List<int>();
      for (int left = FleetSize; left > 0; left -= squadrons[^1])
        squadrons.Add(Math.Min(left, RandomHelper.Int(5, 10)));

      var inset = new Vector2(FormationSpacing * 3);
      for (int s = 0; s < squadrons.Count; ++s)
      {
        float heading = ArrivalHeading + RandomHelper.Float(-0.3f, 0.3f);
        var forward = new Vector2(MathF.Cos(heading), MathF.Sin(heading));
        var right = new Vector2(-forward.Y, forward.X);
        var lead = RandomHelper.Vector2(p0 + inset, p1 - inset);
        // Far enough back that the whole formation starts off screen.
        float distance = ExitDistance(lead, -forward, p0, p1) + FormationSpacing * 6;
        // A few squadrons lead, then the rest pour in.
        float delay = ArrivalWindowSeconds * MathF.Pow(s / (float)Math.Max(1, squadrons.Count - 1), 0.7f);

        for (int i = 0; i < squadrons[s]; ++i)
        {
          int row = (i + 1) / 2;
          float side = i % 2 == 0 ? 1f : -1f;
          var slot = lead + (right * side - forward * 0.8f) * row * FormationSpacing;
          var harvester = CreateHarvester(slot - forward * distance);
          harvester.Transform.Rotation = heading + MathF.PI / 2;
          harvester.TargetPosition = slot;
          harvester.ArrivalStart = harvester.Transform.Position;
          harvester.ArrivalProgress = 0f;
          // Wingmen drop in just after their leader.
          harvester.ArrivalDelay = delay + row * 0.06f;
        }
      }
    }

    // How far a ray from inside the rectangle travels before leaving it.
    private static float ExitDistance(Vector2 from, Vector2 direction, Vector2 min, Vector2 max)
    {
      float x = direction.X > 0 ? (max.X - from.X) / direction.X : direction.X < 0 ? (min.X - from.X) / direction.X : float.MaxValue;
      float y = direction.Y > 0 ? (max.Y - from.Y) / direction.Y : direction.Y < 0 ? (min.Y - from.Y) / direction.Y : float.MaxValue;
      return Math.Min(x, y);
    }

    private void UpdateArrival(HarvesterStruct harvester, float dt)
    {
      if (harvester.ArrivalDelay > 0f)
      {
        harvester.ArrivalDelay -= dt;
        return;
      }

      // Exponential braking, blended with a constant speed so the ship lands at cruising
      // speed and carries on wandering without a jolt.
      float t = harvester.ArrivalProgress = Math.Min(1f, harvester.ArrivalProgress + dt / ArrivalSeconds);
      float k = ArrivalBraking, norm = 1f - MathF.Exp(-k);
      float brakeSlopeAtEnd = k * MathF.Exp(-k) / norm;
      var path = harvester.TargetPosition - harvester.ArrivalStart;
      float landingSlope = CruiseSpeed * ArrivalSeconds / path.Length();
      float blend = MathHelper.Clamp((landingSlope - brakeSlopeAtEnd) / (1f - brakeSlopeAtEnd), 0f, 1f);
      float progress = (1f - blend) * (1f - MathF.Exp(-k * t)) / norm + blend * t;
      float slope = (1f - blend) * k * MathF.Exp(-k * t) / norm + blend;
      harvester.Transform.Position = harvester.ArrivalStart + path * progress;

      // Trailing light and tinted while much faster than cruising, as if just out of warp.
      float speed = slope * path.Length() / ArrivalSeconds;
      float warp = MathHelper.Clamp((speed / CruiseSpeed - 1f) / 20f, 0f, 1f);
      harvester.ArrivalTrail = Math.Min(speed * 0.08f, 500f) * warp;
      harvester.Sprite.Color = harvester.AnimatedSprite.Color = Color.Lerp(Color.White, WarpTint, warp);
      harvester.Sprite.Alpha = harvester.AnimatedSprite.Alpha = Math.Min(1f, t / 0.05f);
    }

    private ShapeBatch m_shapeBatch;

    private void DrawArrivalTrails()
    {
      m_shapeBatch ??= new ShapeBatch(GraphicsDevice, Content, EffectCache.ShapeFx);
      var viewport = BaseGame.BoxingViewportAdapter;
      m_shapeBatch.Begin(ClickUtility.RenderView(m_camera.GetViewMatrix(), viewport.GetScaleMatrix()),
        Matrix.CreateOrthographicOffCenter(0, viewport.VirtualWidth, viewport.VirtualHeight, 0, 0, 1),
        blendState: BlendState.Additive);
      foreach (var harvester in m_harvesters)
      {
        if (harvester.ArrivalProgress >= 1f || harvester.ArrivalTrail < 1f)
          continue;
        var head = harvester.Transform.Position;
        var tail = head - Vector2.Normalize(harvester.TargetPosition - harvester.ArrivalStart) * harvester.ArrivalTrail;
        // Short streaks are the last of the braking; fade them out rather than shrink to a dot.
        float strength = Math.Min(1f, harvester.ArrivalTrail / 120f) * harvester.Sprite.Alpha;
        m_shapeBatch.FillLine(tail, head, 6f, new Gradient(tail, Color.Transparent, head, WarpTint * (0.6f * strength)), 8f);
        m_shapeBatch.FillLine(tail, head, 1.4f, new Gradient(tail, Color.Transparent, head, new Color(225, 245, 255) * (0.9f * strength)), 1.5f);
      }
      m_shapeBatch.End();
    }

    public override void UnloadContent()
    {
      Loc.Changed -= OnLanguageChanged;
      CloseNewGameConfirmation();
      // The Gum screen is reused; clicks must target the current MainMenu instance.
      ((Gum.Forms.DefaultFromFileVisuals.DefaultFromFileButtonRuntime)m_menuScreen.GetChildByNameRecursively("ButtonNewGame")).Click -= NewGameClicked;
      ((Gum.Forms.DefaultFromFileVisuals.DefaultFromFileButtonRuntime)m_menuScreen.GetChildByNameRecursively("ButtonContinue")).Click -= ContinueClicked;
      ((Gum.Forms.DefaultFromFileVisuals.DefaultFromFileButtonRuntime)m_menuScreen.GetChildByNameRecursively("ButtonSettings")).Click -= SettingsClicked;
      ((Gum.Forms.DefaultFromFileVisuals.DefaultFromFileButtonRuntime)m_menuScreen.GetChildByNameRecursively("ButtonCredits")).Click -= CreditsClicked;
      ((Gum.Forms.DefaultFromFileVisuals.DefaultFromFileButtonRuntime)m_menuScreen.GetChildByNameRecursively("ButtonExit")).Click -= ExitClicked;
      GameMain.RemoveCustomHudContent(DrawMenu);
      base.UnloadContent();
    }

    private List<HarvesterStruct> m_harvesters = new List<HarvesterStruct>();

    public HarvesterStruct CreateHarvester(Vector2 position)
    {
      var animatedSprite = TextureCache.Fleet.CreateEngine(FleetAtlas.ScoutEngine);

      var sprite = new MonoGame.Extended.Graphics.Sprite(TextureCache.HarvesterShip);
      sprite.Origin = new Vector2(sprite.TextureRegion.Width / 2.0f, sprite.TextureRegion.Height / 2.0f);
      // Hidden until its arrival starts, also when the game's transition draws the fleet.
      sprite.Alpha = animatedSprite.Alpha = 0f;

      var harvester = new HarvesterStruct { Sprite = sprite, AnimatedSprite = animatedSprite, Transform = new Transform2(position) };
      m_harvesters.Add(harvester);
      return harvester;
    }

    private void DrawMenu()
    {
      var sprite = TextureCache.Logo;

      int screenWidth = GraphicsDevice.Viewport.Width;

      float topMarginPercent = 0.1f;
      int topMargin = (int)(GraphicsDevice.Viewport.Height * topMarginPercent);

      float aspectRatio = (float)sprite.Value.Width / sprite.Value.Height;
      int logoWidth = (int)(screenWidth * 0.6f);
      int logoHeight = (int)(logoWidth / aspectRatio);
      int xPosition = (screenWidth - logoWidth) / 2;

      var destinationRect = new Rectangle(xPosition, topMargin, logoWidth, logoHeight);

      m_spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.NonPremultiplied);
      if (GumService.Default.Root.Children.Contains(m_menuScreen))
        m_spriteBatch.Draw(sprite, destinationRect, Color.White);
      m_spriteBatch.End();

      Gum.GumService.Default.Draw();
      TextureCache.RequestIconPreload();
    }

    private float LerpAngle(float currentAngle, float targetAngle, float amount)
    {
      float difference = targetAngle - currentAngle;

      // Wrap the difference to ensure it is between -PI and PI
      while (difference < -MathHelper.Pi) difference += MathHelper.TwoPi;
      while (difference > MathHelper.Pi) difference -= MathHelper.TwoPi;

      // Apply the interpolated difference to the current angle
      return currentAngle + difference * amount;
    }

    private string previousButtonName = "null";
    private bool? pendingStart;
    private bool fleetSpawned;
    public override void Update(GameTime gameTime)
    {
      if (!fleetSpawned && GameplayPreloader.Ready && TextureCache.FleetTexture?.IsLoaded == true
        && !TextureCache.FleetTexture.IsFailed)
      {
        fleetSpawned = true;
        SpawnHarvesters();
      }
      if (pendingStart.HasValue && (GameplayPreloader.Error != null
        || !GumService.Default.Root.Children.Contains(m_menuScreen)
        || KeyboardExtended.GetState().WasKeyPressed(Keys.Escape)))
      {
        pendingStart = null;
        m_menuScreen.GetChildByNameRecursively("ButtonNewGame").SetProperty("Text", Loc.N("New Game"));
        m_menuScreen.GetChildByNameRecursively("ButtonContinue").SetProperty("Text", Loc.N("Continue"));
        if (GameplayPreloader.Error != null)
          m_menuScreen.GetChildByNameRecursively("VersionLabel").SetProperty("Text", Loc.N("Unable to load game assets. Please restart the game."));
      }
      if (pendingStart is bool startNew && GameplayPreloader.Ready)
      {
        pendingStart = null;
        EnterGame(startNew);
        return;
      }
      var vp = BaseGame.BoxingViewportAdapterGui.Viewport;
      var scale = BaseGame.BoxingViewportAdapterGui.GetScaleMatrix();
      Matrix.Invert(ref scale, out scale);
      GumService.Default.Cursor.TransformMatrix = Matrix.CreateTranslation(-vp.X, -vp.Y, 0) * scale;
      GumService.Default.Update(gameTime);
      if (newGameDialog != null && KeyboardExtended.GetState().WasKeyPressed(Keys.Escape))
        CloseNewGameConfirmation();
      // GumService.Default.Draw();


      // Console.WriteLine("zoom: " + m_camera.Zoom);

      var curOverButtonName = GumService.Default.Cursor.WindowOver?.Name ?? "null";

      if (curOverButtonName != previousButtonName && curOverButtonName.Contains("Button"))
      {
        if (curOverButtonName != "null")
        {
          // Console.WriteLine("Hovering over button: " + curOverButtonName);
          // AudioManager.Instance.PlaySound("MenuHover");
          AudioManager.Instance.PlaySound(AudioManager.Instance.MenuHoverButtonSoundEffect);
        }
      }

      previousButtonName = curOverButtonName;

      var p0 = m_camera.ScreenToWorld(new Vector2(vp.X, vp.Y));
      var p1 = m_camera.ScreenToWorld(new Vector2(vp.X + vp.Width, vp.Y + vp.Height));

      foreach (var harvester in m_harvesters)
      {
        if (harvester.ArrivalProgress < 1f)
        {
          UpdateArrival(harvester, (float)gameTime.ElapsedGameTime.TotalSeconds);
          harvester.AnimatedSprite.Update(gameTime);
          continue;
        }

        Vector2 spriteSize = new Vector2(harvester.Sprite.TextureRegion.Width, harvester.Sprite.TextureRegion.Height);
        Vector2 halfSpriteSize = spriteSize / 2.0f;

        if (harvester.TargetPosition == Vector2.Zero || Vector2.Distance(harvester.Transform.Position, harvester.TargetPosition) < 1.0f)
        {
          var position = RandomHelper.Vector2(p0 + halfSpriteSize, p1 - halfSpriteSize);
          harvester.TargetPosition = position;
        }
        else
        {
          var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

          var dir = harvester.TargetPosition - harvester.Transform.Position;
          dir.Normalize();
          var movement = dir * dt * CruiseSpeed;

          float radians = (float)Math.Atan2(dir.Y, dir.X);
          harvester.Transform.Rotation = LerpAngle(harvester.Transform.Rotation, radians + (float)Math.PI / 2, dt * 20.0f);
          harvester.Transform.Position += movement;
        }

        harvester.AnimatedSprite.Update(gameTime);

        // harvester.Transform.Position = p1 - halfSpriteSize;
      }

      AudioManager.Instance.Update(gameTime, false);
    }

    private void StartGame(bool newGame)
    {
      // Change screens from Update, after Gum has finished dispatching the click.
      pendingStart = newGame;
      m_menuScreen.GetChildByNameRecursively("ButtonNewGame").SetProperty("Text", Loc.N("New Game"));
      m_menuScreen.GetChildByNameRecursively("ButtonContinue").SetProperty("Text", Loc.N("Continue"));
      var button = m_menuScreen.GetChildByNameRecursively(newGame ? "ButtonNewGame" : "ButtonContinue");
      button.SetProperty("Text", Loc.N("Loading..."));
    }

    private void EnterGame(bool newGame)
    {
      m_menuScreen.GetChildByNameRecursively("ButtonNewGame").SetProperty("Text", Loc.N("New Game"));
      m_menuScreen.GetChildByNameRecursively("ButtonContinue").SetProperty("Text", Loc.N("Continue"));
      MediaPlayer.IsRepeating = false;
      MediaPlayer.Stop();
      GumService.Default.Root.Children.Clear();
      GumService.Default.ModalRoot.Children.Clear();

      // var camera = SystemManagers.Default.Renderer.Camera;
      // Renderer.UseBasicEffectRendering = true;
      // camera.Zoom = 1.0f;
      // camera.Position = System.Numerics.Vector2.Zero;

      GameMain.RemoveCustomHudContent(DrawMenu);

      var gameScreen = new UntitledGemGameGameScreen(Game, newGame);
      gameScreen.Initialize();
      gameScreen.PostInit();
      var transition = new TestTransition(GraphicsDevice, Color.Black, m_camera, m_camera_background, m_harvesters, 1.5f);
      // The transition paints over the new screen after it becomes active.
      // Keep the arrival offscreen until that cover is removed.
      gameScreen.IntroTransitionPending = true;
      transition.Completed += (_, _) => gameScreen.IntroTransitionPending = false;

      GameMain.CurrentMenu = "GameMenu";
      // ScreenManager.LoadScreen(gameScreen, transition);
      ScreenManager.ReplaceScreen(gameScreen, transition);
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
    // private void DrawText(SpriteBatch spriteBatch, string text)
    // {
    //   var font = FontManager.GetDefaultFont(150);
    //   var text_size = font.MeasureString(text);
    //   var pos_x = GraphicsDevice.Viewport.Width / 2.0f - text_size.X / 2.0f;
    //   var pos_y = GraphicsDevice.Viewport.Height / 2.0f - text_size.Y / 2.0f;
    //   spriteBatch.DrawString(font, text, new Vector2(pos_x, pos_y), Color.Yellow);
    // }
    // 

    float map(float x, float in_min, float in_max, float out_min, float out_max)
    {
      return (x - in_min) * (out_max - out_min) / (in_max - in_min) + out_min;
    }

    public override void Draw(GameTime gameTime)
    {
      var effect = EffectCache.BackgroundEffect.Value;

      m_camera_background.Zoom = map(m_camera.Zoom, 0, 3.0f, 0.3f, 1.0f);
      effect.Parameters["view_projection"]?.SetValue(m_camera_background.ViewProjection());

      var bkg = TextureCache.SpaceBackground.Value;
      var bounds = new Rectangle(TextureCache.SpaceBackground.Value.Bounds.X, TextureCache.SpaceBackground.Value.Bounds.Y,
        TextureCache.SpaceBackground.Value.Bounds.Width * 5, TextureCache.SpaceBackground.Value.Bounds.Height * 5);

      Rectangle size = new Rectangle(-bkg.Width * 5, -bkg.Height * 5, bkg.Width * 10, bkg.Height * 10);

      m_spriteBatch.Begin(effect: effect, depthStencilState: DepthStencilState.Default, samplerState: SamplerState.AnisotropicWrap);
      m_spriteBatch.Draw(TextureCache.SpaceBackground, size, bounds,
          Color.White, 0, new Vector2(0, 0), SpriteEffects.None, 0);
      m_spriteBatch.Draw(TextureCache.SpaceBackground2, size, bounds,
          Color.White, 0, new Vector2(0, 0), SpriteEffects.None, 0);
      m_spriteBatch.Draw(TextureCache.SpaceBackground3, size, bounds,
          Color.White, 0, new Vector2(0, 0), SpriteEffects.None, 0);
      m_spriteBatch.End();

      // The last ship spawned is the last to arrive.
      if (m_harvesters.Count > 0 && m_harvesters[^1].ArrivalProgress < 1f)
        DrawArrivalTrails();

      m_spriteBatch.Begin(transformMatrix: m_camera.GetViewMatrix());
      foreach (var harvester in m_harvesters)
      {
        if (harvester.ArrivalDelay > 0f)
          continue;
        m_spriteBatch.Draw(harvester.AnimatedSprite, harvester.Transform);
        m_spriteBatch.Draw(harvester.Sprite, harvester.Transform);
      }
      m_spriteBatch.End();

      var width = GameMain.Instance.GraphicsDevice.Viewport.Width;
      var height = GameMain.Instance.GraphicsDevice.Viewport.Height;



#if !KNI_WEB
      // string title = "Beyond the Belt";
      // float scale = 128;
      // var textSize = Measure2(title, Vector2.Zero, scale);

      //FIXME: This needs to be here or text gets wonky, timing issue so could probably be moved but gotta be the correct timing
      var textRenderer = FontManager.GetTextRenderer(nameof(ContentDirectory.Fonts.Roboto_Regular_ttf));
      textRenderer.SetOrtographicProjection(width, height);
      textRenderer.UseScreenSpace = false;

      // textRenderer.ResetLayout();
      // textRenderer.SimpleLayoutText(title, position, color, strokeColor, scale, -1, wrap, wrapAt);
      // textRenderer.RenderStroke();
      // textRenderer.RenderText();


      // m_spriteBatch.Begin();
      // FontManager.RenderFieldFont(() => ContentDirectory.Fonts.Roboto_Regular_ttf, title, new Vector2(width / 2.0f - textSize.X / 2.0f, 25), Color.Gold, Color.Black, scale);
      // m_spriteBatch.End();
#endif
    }
  }
}
