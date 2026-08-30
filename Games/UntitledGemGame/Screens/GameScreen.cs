using System;
using Apos.Shapes;
using Apos.Tweens;
using AsyncContent;
using GUI.Shared.Helpers;
using Gum.Converters;
using Gum.Forms.Controls;
using Gum.Forms.DefaultVisuals;
using Gum.GueDeriving;
using Gum.Wireframe;
using ImGuiNET;
using JapeFramework;
using JapeFramework.Aseprite;
using JapeFramework.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Aseprite;
using MonoGame.Extended;
using MonoGame.Extended.ECS;
using MonoGame.Extended.Graphics;
using MonoGame.Extended.Input;
using MonoGame.Extended.Screens;
using MonoGame.Extended.Tweening;
using MonoGame.Extended.ViewportAdapters;
using MonoGameGum;
using RenderingLibrary;
using RenderingLibrary.Graphics;
using Serilog;
using UntitledGemGame.Entities;
using UntitledGemGame.Systems;
using Vector4 = System.Numerics.Vector4;


//https://github.com/cpt-max/MonoGame-Shader-Samples?tab=readme-ov-file
//https://github.com/Amrik19/Monogame-Spritesheet-Instancing

namespace UntitledGemGame.Screens
{
  public class UntitledGemGameGameScreen : GameScreen
  {
    private SpriteBatch m_spriteBatch;
    private ShapeBatch m_shapeBatch;

    private World m_escWorld;
    private EntityFactory m_entityFactory;
    public OrthographicCamera m_camera;
    private OrthographicCamera m_camera_background;
    public OrthographicCamera m_gui_camera;


    public static ulong Collected;
    public static ulong Delivered;
    public static ulong DeliveredUncounted;

    Tween preGameTween;
    Tween preGameTweenLogo;
    private bool GameStarted = false;

    private GameState m_gameState = new GameState();
    private UpgradeManager m_upgradeManager = new UpgradeManager();

    private bool showDebugGUI = false;

    public float gemCountFontSize { get; set; } = 55f;
    private readonly Tweener _tweener = new();
    private readonly Tweener _tweenerPreGame = new();
    private Tween? _gemCountTween;

    private MonoGame.Extended.Graphics.AnimatedSprite gemSpriteRedHud;
    private MonoGame.Extended.Graphics.AnimatedSprite gemSpriteBlueHud;
    private MonoGame.Extended.Graphics.AnimatedSprite gemSpritePurpleHud;

    // private Texture2D buttonTexture;
    // private Texture2D buttonTexture;

    public UntitledGemGameGameScreen(Game game) : base(game)
    {
      game.IsMouseVisible = true;
      Instance = this;
    }

    public static UntitledGemGameGameScreen Instance;

    public static Vector2 HomeBasePos = Vector2.Zero;
    private RenderGuiSystem _renderGuiSystem;
    private Entity m_homeBaseEntity;

    private bool m_postInitialized = false;
    private bool m_createdInitialGems = false;

    public override void LoadContent()
    {

      base.LoadContent();

      PostInit();
    }

    public override void UnloadContent()
    {
      GameStarted = false;

      GameMain.RemoveCustomImGuiContent(DrawImGUIContent);
      GameMain.RemoveCustomHudContent(DrawHudContent);

      // m_upgradesButton.Visual.RemoveFromManagers();

      foreach (var h in _renderGuiSystem.hudItems)
      {
        h.RemoveFromManagers();
        h.RemoveFromRoot();
      }

      foreach (var h in _renderGuiSystem.skillTreeItems)
      {
        h.RemoveFromManagers();
        h.RemoveFromRoot();
      }

      foreach (var h in _renderGuiSystem.gameMenuItems)
      {
        h.RemoveFromManagers();
        h.RemoveFromRoot();
      }

      m_upgradeManager.Finish();
      _renderGuiSystem.SetUpgradeType(RenderGuiSystem.UpgradeTypes.None);
      _renderGuiSystem.rootItems.Clear();
      _renderGuiSystem.hudItems.Clear();
      _renderGuiSystem.skillTreeItems.Clear();
      _renderGuiSystem.gameMenuItems.Clear();
      _renderGuiSystem.Finish();

      UpgradeManager.CurrentUpgrades.UpgradeButtons.Clear();
      UpgradeManager.CurrentUpgrades.UpgradeButtonsAbilities.Clear();
      UpgradeManager.CurrentUpgrades.UpgradeButtonsMeta.Clear();
      UpgradeManager.CurrentUpgrades.UpgradeJoints.Clear();
      UpgradeManager.CurrentUpgrades.UpgradeJointsAbilities.Clear();
      UpgradeManager.CurrentUpgrades.UpgradeJointsMeta.Clear();
      UpgradeManager.CurrentUpgrades.UpgradeDefinitions.Clear();
      UpgradeManager.CurrentUpgrades.UpgradeDefinitionsAbilities.Clear();
      UpgradeManager.CurrentUpgrades.UpgradeDefinitionsMeta.Clear();

      // RenderGuiSystem.Instance.hudItems.Remove(m_refuelButton.Visual);

      base.UnloadContent();
    }

    // public Button m_upgradesButton;



    public void PostInit()
    {
      if (m_postInitialized) return;
      m_postInitialized = true;

      Log.Information("UntitledGemGameGameScreen PostInit");

      m_camera = new OrthographicCamera(BaseGame.BoxingViewportAdapter);
      m_camera_background = new OrthographicCamera(BaseGame.BoxingViewportAdapter);
      m_gui_camera = new OrthographicCamera(BaseGame.BoxingViewportAdapterGui);

      FontStashSharpText.m_camera = m_camera;

      m_camera.Zoom = UpgradeManager.Instance.UG.CameraZoomScale;

      // m_shapeBatch = new ShapeBatch(GraphicsDevice, Content, EffectCache.ShapeFx);
      m_shapeBatch = new ShapeBatch(GraphicsDevice, Content, EffectCache.ShapeFx);
      _renderGuiSystem = new RenderGuiSystem(m_spriteBatch, m_shapeBatch, GraphicsDevice,
          m_gui_camera, GameMain.GumServiceUpgrades);

      m_escWorld = new WorldBuilder()
        .AddSystem(new HarvesterCollectionSystem(m_camera, m_shapeBatch))
        .AddSystem(new UpdateSystem2(m_camera))
        .AddSystem(new RenderGemSystem(m_spriteBatch, m_shapeBatch, GraphicsDevice, m_camera))
        .AddSystem(new RenderSystem(m_spriteBatch, m_shapeBatch, GraphicsDevice, m_camera))
        // .AddSystem(new RenderGuiSystem(m_spriteBatch, GraphicsDevice, m_gui_camera, GameMain.GumServiceUpgrades))
        .Build();

      m_entityFactory = new EntityFactory(m_escWorld, GraphicsDevice, m_camera);

      // InitImGuiContent();
      // InitHudContent();

      GameMain.AddCustomImGuiContent(DrawImGUIContent);
      GameMain.AddCustomHudContent(DrawHudContent);

      // m_camera.Zoom = 1.0f;

      // var width = GameMain.Instance.Window.ClientBounds.Width;
      // var height = GameMain.Instance.Window.ClientBounds.Height;
      // var width = GraphicsDevice.PresentationParameters.BackBufferWidth;
      // var height = GraphicsDevice.PresentationParameters.BackBufferHeight;
      var width = GraphicsDevice.Viewport.Width;
      var height = GraphicsDevice.Viewport.Height;

      m_upgradeManager.OnUpgradeRoot += () =>
      {
        UpgradeManager.Instance.UG.HarvesterCount += 1;
      };

      m_upgradeManager.OnUpgrade += (s) =>
      {
        m_homeBaseEntity.Get<HomeBase>().ActivateAbility(s);
      };

      // HomeBasePos = m_camera.ScreenToWorld(new Vector2(width / 2.0f, height / 2.0f));
      HomeBasePos = m_camera.ScreenToWorld(BaseGame.ViewportCenter);
      // m_homeBaseEntity = m_entityFactory.CreateHomeBase(new Vector2(HomeBasePos.X, m_camera.ScreenToWorld(new Vector2(0, height + 300)).Y));
      m_homeBaseEntity = m_entityFactory.CreateHomeBase(new Vector2(HomeBasePos.X, HomeBasePos.Y), new Vector2(0, 1000));

      m_upgradeManager.Init(m_gameState);
      // time = UpgradeManager.Instance.UG.GemSpawnCooldown;


      m_homeBaseEntity.Get<HomeBase>().StartShake(3.5f, 3.0f);
      // AudioManager.Instance.ShipEngineDyingSoundEffect.Play();
      preGameTween = _tweenerPreGame.TweenTo(m_homeBaseEntity.Get<Transform2>(), t => t.Position, HomeBasePos, duration: 3.0f).OnEnd((a) =>
      {
        GameStart();
      }).Easing(EasingFunctions.CubicOut);

      // preGameTweenLogo = _tweenerPreGame.TweenTo(LogoAlpha, LogoAlpha, 0.0f, 2.0f);
      preGameTweenLogo = _tweenerPreGame.TweenTo(target: this, expression: t => t.LogoAlpha, toValue: 0.0f, duration: 2.0f);
    }

    private void GameStart()
    {
      GameStarted = true;

      var camera = SystemManagers.Default.Renderer.Camera;
      Renderer.UseBasicEffectRendering = true;
      camera.Zoom = 1.0f;
      camera.Position = System.Numerics.Vector2.Zero;

      AudioManager.Instance.PlaySound(AudioManager.Instance.ImpactSoundEffect);
    }

    private bool m_initialized = false;
    public override void Initialize()
    {
      if (m_initialized) return;

      m_initialized = true;
      m_spriteBatch = new SpriteBatch(GraphicsDevice);
      // m_camera = new OrthographicCamera(GraphicsDevice);
      // m_camera = new OrthographicCamera(new BoxingViewportAdapter();
      // m_camera = new OrthographicCamera(JapeFramework.BaseGame.BoxingViewportAdapter);
      // m_gui_camera = new OrthographicCamera(GraphicsDevice);
      // m_gui_camera = new OrthographicCamera(JapeFramework.BaseGame.BoxingViewportAdapter);

      // m_camera = JapeFramework.BaseGame.Camera;
      // m_gui_camera = JapeFramework.BaseGame.HudCamera;


      base.Initialize();
    }

    // private int time;
    private float spawnTimer;
    private float passiveIncomeTimer = 0;
    private string previousButtonName = "null";
    public bool m_prestiging = false;
    public bool m_postPrestige = false;
    public float m_prestigeTime = 0;

    public override void Update(GameTime gameTime)
    {
      var deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

      if (m_escWorld == null)
        return;

      if (!UpgradeManager.Instance.UpdatingButtons)
        _renderGuiSystem?.Update(gameTime);

      if (GameMain.IsPaused)
        return;

      float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

      gemSpriteRedHud?.Update(gameTime);
      gemSpriteBlueHud?.Update(gameTime);
      gemSpritePurpleHud?.Update(gameTime);

      m_camera.Zoom = MathHelper.Lerp(m_camera.Zoom, UpgradeManager.Instance.UG.CameraZoomScale, (float)gameTime.ElapsedGameTime.TotalSeconds);

      if (m_prestiging)
      {
        m_prestigeTime += dt;
        DeliverGems(gameTime);
        m_escWorld.Update(gameTime);

        UpgradeManager.Instance.UG.HarvesterCount = 0;

        if (m_prestigeTime > 2.0f)
        {
          //TODO: convert currency to prestige points
          m_gameState.CurrentPurpleGemCount += m_gameState.CurrentRedGemCount;
          m_gameState.CurrentRedGemCount = 0;
          m_prestiging = false;
          m_postPrestige = true;
          m_prestigeTime = 0.0f;
          Delivered = 0;
          Collected = 0;
          DeliveredUncounted = 0;
          m_createdInitialGems = false;
          RenderGuiSystem.Instance.SetUpgradeType(RenderGuiSystem.UpgradeTypes.Meta);
          HarvesterCollectionSystem.Instance.flatSpatialHash.RebuildGrid();
        }

        return;
      }
      else if (m_postPrestige)
      {
        m_escWorld.Update(gameTime);
        m_upgradeManager.Update(gameTime);
        return;
      }

      AudioManager.Instance.Update(gameTime, GameStarted);

      // GumService.Default.Update(gameTime);
      var curOverButtonName = Gum.GumService.Default.Cursor.VisualOver?.Name ?? "null";

      if (curOverButtonName != previousButtonName && curOverButtonName.Contains("Button"))
      {
        if (curOverButtonName != "null")
        {
          AudioManager.Instance.PlaySound(AudioManager.Instance.MenuHoverButtonSoundEffect);
        }
      }
      previousButtonName = curOverButtonName;

      if (!preGameTween.IsComplete)
      {
        _tweenerPreGame.Update((float)gameTime.ElapsedGameTime.TotalSeconds);
        m_homeBaseEntity.Get<HomeBase>()?.Update(gameTime);
        return;
      }

      m_upgradeManager.Update(gameTime);
      m_homeBaseEntity?.Get<HomeBase>()?.Update(gameTime);
      var keyboardState = KeyboardExtended.GetState();

      var vp = BaseGame.BoxingViewportAdapter.Viewport;
      var p0 = m_camera.ScreenToWorld(new Vector2(vp.X, vp.Y));
      var p1 = m_camera.ScreenToWorld(new Vector2(vp.X + vp.Width, vp.Y + vp.Height));

      Vector2 spriteSize = new Vector2(32, 32);
      Vector2 halfSpriteSize = spriteSize / 2.0f;

      if (!m_createdInitialGems)
      {
        m_createdInitialGems = true;
        Console.WriteLine("Creating initial gems: " + UpgradeManager.Instance.UGM.StartingGemCount);
        var gemValue = (uint)UpgradeManager.Instance.UG.GemValue;
        for (int i = 0; i < UpgradeManager.Instance.UGM.StartingGemCount; i++)
        {
          var a = RandomHelper.Vector2(p0 + halfSpriteSize, p1 - halfSpriteSize);
          m_entityFactory.QueueGemSpawn(a, GemTypes.Red, gemValue);
        }
      }
      else
      {
        float currentCooldown = BaseStats.GemSpawnCooldownSeconds / UpgradeManager.Instance.UG.GemSpawnCooldown;
        int gemsPerSpawn = UpgradeManager.Instance.UG.GemSpawnRate; // e.g., 1, 2, 5 gems per burst
                                                                    //
        spawnTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;

        if (spawnTimer >= currentCooldown)
        {
          int burstsToTrigger = (int)(spawnTimer / currentCooldown);

          int totalGemsToSpawn = burstsToTrigger * gemsPerSpawn;

          for (int i = 0; i < totalGemsToSpawn; ++i)
          {
            if (HarvesterCollectionSystem.Instance.flatSpatialHash.NumActiveGems >= UpgradeManager.Instance.UG.MaxGemCount)
              break;

            var chance = UpgradeManager.Instance.UG.GemSpawnQuality switch
            {
              1 => 1,
              2 => 10,
              3 => 50,
              _ => 0
            };

            var upgrade = RandomHelper.PercentChance(chance);
            var gemValue = (uint)UpgradeManager.Instance.UG.GemValue;
            var type = upgrade ? GemTypes.LightGreen : GemTypes.Red;
            var a = RandomHelper.Vector2(p0 + halfSpriteSize, p1 - halfSpriteSize);

            m_entityFactory.CreateGem(a, type, gemValue);
          }

          spawnTimer -= burstsToTrigger * currentCooldown;
        }
      }

      if (UpgradeManager.Instance.UG.PassiveIncome > 0)
      {
        //TODO: add inteval reduce cooldown upgrade
        float currentInterval = BaseStats.PassiveIncomeInterval / 1.0f;// / UpgradeManager.Instance.UG.PassiveIncomeFrequencyMultiplier;

        passiveIncomeTimer += deltaTime;

        if (passiveIncomeTimer >= currentInterval)
        {
          int ticks = (int)(passiveIncomeTimer / currentInterval);
          DeliveredUncounted += (ulong)(ticks * UpgradeManager.Instance.UG.PassiveIncome);
          passiveIncomeTimer -= ticks * currentInterval;
        }
      }

      // passiveIncomeTimer -= deltaTime * 1000;
      // if (passiveIncomeTimer <= 0)
      // {
      //   //TODO: add passive income timer reduction upgrade
      //   if (UpgradeManager.Instance.UG.PassiveIncome > 0)
      //   {
      //     DeliveredUncounted += (ulong)(UpgradeManager.Instance.UG.PassiveIncome);
      //   }
      //   passiveIncomeTimer = 1000;
      // }

      if (keyboardState.WasKeyPressed(Keys.F1))
      {
        RenderGuiSystem.Instance.SetUpgradeType(RenderGuiSystem.UpgradeTypes.Upgrades);
      }
      if (keyboardState.WasKeyPressed(Keys.F2))
      {
        RenderGuiSystem.Instance.SetUpgradeType(RenderGuiSystem.UpgradeTypes.Abilities);
      }
      if (keyboardState.WasKeyPressed(Keys.F3))
      {
        RenderGuiSystem.Instance.SetUpgradeType(RenderGuiSystem.UpgradeTypes.Meta);
      }
      if (keyboardState.WasKeyPressed(Keys.F4))
      {
        UpgradeManager.Instance.UpgradeGuiEditMode = !UpgradeManager.Instance.UpgradeGuiEditMode;
      }

      if (keyboardState.WasKeyPressed(Keys.Escape))
      {
        if (UpgradeManager.Instance.UpgradeGuiEditMode)
        {
          UpgradeManager.Instance.UpgradeGuiEditMode = false;
        }
        else if (RenderGuiSystem.Instance.m_upgradeWindowType == RenderGuiSystem.UpgradeTypes.Upgrades || RenderGuiSystem.Instance.m_upgradeWindowType == RenderGuiSystem.UpgradeTypes.Abilities)
        {
          _renderGuiSystem.SetUpgradeType(RenderGuiSystem.UpgradeTypes.None);
        }
        else
        {
          GameMain.TogglePauseGame();
        }
      }

      if (keyboardState.WasKeyPressed(Keys.B))
      {
        //var a = m_camera.ScreenToWorld(RandomHelper.Vector2(Vector2.Zero, new Vector2(1920, 900)));
        //m_entityFactory.CreateHarvester(a);

        // ++Upgrades.HarvesterCount;
        // Upgrades.HarvesterCount.Increment();
      }


      if (keyboardState.WasKeyPressed(Keys.F9))
      {
        UpgradeManager.CurrentUpgrades.SaveToJson();
      }

      if (keyboardState.WasKeyPressed(Keys.F5))
      {
        m_upgradeManager.RefreshButtons();
      }

      if (keyboardState.IsKeyDown(Keys.I))
      {
        //m_camera.ZoomIn(0.01f);

        UpgradeManager.Instance.UG.CameraZoomScale += 0.01f;
      }

      if (keyboardState.IsKeyDown(Keys.O))
      {
        //m_camera.ZoomOut(0.01f);

        UpgradeManager.Instance.UG.CameraZoomScale -= 0.01f;
      }

      //if (keyboardState.IsKeyDown(Keys.R))
      //{
      //}

      DeliverGems(gameTime);

      // m_camera.Zoom = UpgradeManager.Instance.UG.CameraZoomScale;
      // m_camera.Zoom = MathHelper.Lerp(m_camera.Zoom, UpgradeManager.Instance.UG.CameraZoomScale, (float)gameTime.ElapsedGameTime.TotalSeconds);
      //TODO: find better lerp or an easing function
      // m_camera.Zoom = MathHelper.Lerp(m_camera.Zoom, 1.0f, (float)gameTime.ElapsedGameTime.TotalSeconds);

      m_escWorld.Update(gameTime);

      SpawnAndRemoveHarvesters();

      TimerHelper.PumpEndOfFrameObjects();
      EntityFactory.Instance.Update();

      _tweener?.Update((float)gameTime.ElapsedGameTime.TotalSeconds);
    }

    private void DeliverGems(GameTime gameTime)
    {
      if (DeliveredUncounted > 0)
      {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        ulong toDeliver = Math.Clamp((uint)(DeliveredUncounted * dt), 1, DeliveredUncounted);

        if (DeliveredUncounted > 100)
        {
          toDeliver = (ulong)(DeliveredUncounted * 0.8f);
        }

        var scale = new Vector2(0.001f, 0.001f) * toDeliver;

        gemCountFontSize += scale.X * 15.0f;

        if (HomeBase.Instance != null)
        {
          HomeBase.Instance.Entity.Get<Transform2>().Scale += scale;
          //Clamp scale for HomeBase
          HomeBase.Instance.Entity.Get<Transform2>().Scale =
            Vector2.Clamp(HomeBase.Instance.Entity.Get<Transform2>().Scale, new Vector2(1.0f, 1.0f), new Vector2(10.0f, 10.0f));
        }

        Delivered += toDeliver;
        DeliveredUncounted -= toDeliver;

        m_gameState.CurrentRedGemCount += toDeliver;

        gemCountFontSize = MathHelper.Clamp(gemCountFontSize, 55f, 100f);
        var diff = gemCountFontSize - 55f;
        gemCountFontSize = MathHelper.Lerp(gemCountFontSize, 55f, (float)gameTime.ElapsedGameTime.TotalSeconds * diff);
        // Console.WriteLine($"ToDeliver: {toDeliver}");

        m_upgradeManager.UpdateTooltipContent();
      }
    }

    private void SpawnAndRemoveHarvesters()
    {
      var curHarvesters = m_entityFactory.Harvesters.Count;
      if (curHarvesters < UpgradeManager.Instance.UG.HarvesterCount)
      {
        m_entityFactory.CreateHarvester(HomeBasePos + RandomHelper.Vector2(new Vector2(-25, -25), new Vector2(25, 25)));
        Console.WriteLine("Added harvester due to upgrade.");
      }
      else if (curHarvesters > UpgradeManager.Instance.UG.HarvesterCount)
      {
        m_entityFactory.RemoveRandomHarvester(EntityFactory.Instance.Harvesters);
        Console.WriteLine("Removed excess harvester due to downgrade.");
      }

      curHarvesters = m_entityFactory.AdvancedHarvesters.Count;
      if (curHarvesters < UpgradeManager.Instance.UG.AdvancedHarvesterCount)
      {
        m_entityFactory.CreateAdvancedHarvester(HomeBasePos + RandomHelper.Vector2(new Vector2(-25, -25), new Vector2(25, 25)));
        Console.WriteLine("Added advanced harvester due to upgrade.");
      }
      else if (curHarvesters > UpgradeManager.Instance.UG.HarvesterCount)
      {
        m_entityFactory.RemoveRandomHarvester(EntityFactory.Instance.AdvancedHarvesters);
        Console.WriteLine("Removed excess advanced harvester due to downgrade.");
      }



      curHarvesters = m_entityFactory.ExpertHarvesters.Count;
      if (curHarvesters < UpgradeManager.Instance.UG.ExpertHarvesterCount)
      {
        m_entityFactory.CreateExpertHarvester(HomeBasePos + RandomHelper.Vector2(new Vector2(-25, -25), new Vector2(25, 25)));
        Console.WriteLine("Added advanced harvester due to upgrade.");
      }
      else if (curHarvesters > UpgradeManager.Instance.UG.ExpertHarvesterCount)
      {
        m_entityFactory.RemoveRandomHarvester(EntityFactory.Instance.ExpertHarvesters);
        Console.WriteLine("Removed excess advanced harvester due to downgrade.");
      }



      curHarvesters = m_entityFactory.UltimateHarvesters.Count;
      if (curHarvesters < UpgradeManager.Instance.UG.UltimateHarvesterCount)
      {
        m_entityFactory.CreateUltimateHarvester(HomeBasePos + RandomHelper.Vector2(new Vector2(-25, -25), new Vector2(25, 25)));
        Console.WriteLine("Added advanced harvester due to upgrade.");
      }
      else if (curHarvesters > UpgradeManager.Instance.UG.UltimateHarvesterCount)
      {
        m_entityFactory.RemoveRandomHarvester(EntityFactory.Instance.UltimateHarvesters);
        Console.WriteLine("Removed excess advanced harvester due to downgrade.");
      }


    }

    private void DrawHudContent()
    {
      if (!GameStarted)
        return;

      var canvasWidth = Gum.GumService.Default.Root.Width; //3840
      var canvasHeight = Gum.GumService.Default.Root.Height; //2160

      var bounds = BaseGame.BoxingViewportAdapter.Viewport.Bounds;

      int banner_height = 100;
      int banner_mid_pos = bounds.Bottom - banner_height / 2;
      int banner_top_pos = bounds.Bottom - banner_height;

      int banner_mid_x = bounds.Width / 2;

      m_spriteBatch.Begin();
      m_spriteBatch.Draw(TextureCache.TooltipBackground, new Rectangle(0, banner_top_pos, bounds.Width, banner_height), new Color(0, 0, 0, 100));
      m_spriteBatch.End();

      if (!UpgradeManager.Instance.UpdatingButtons)
        _renderGuiSystem?.Draw(m_spriteBatch);

      // if(!m_prestiging)
      // {
      //   _renderGuiSystem.DrawToggleButtonUpgrades(m_spriteBatch);
      //   _renderGuiSystem.DrawToggleButtonAbilities(m_spriteBatch);
      // }

      if (gemSpriteRedHud == null)
      {
        gemSpriteRedHud = AsepriteHelper.LoadAnimation(
          "Textures/Gems/Gem1/GEM 1 - RED - Spritesheet.png",
          true,
          10,
          150);
      }

      if (gemSpriteBlueHud == null)
      {
        gemSpriteBlueHud = AsepriteHelper.LoadAnimation(
          "Textures/Gems/Gem3/GEM 3 - BLUE - Spritesheet.png",
          true,
          11,
          150);
      }

      if (gemSpritePurpleHud == null)
      {
        gemSpritePurpleHud = AsepriteHelper.LoadAnimation(
          "Textures/Gems/Gem5/GEM 5 - LILAC - Spritesheet.png",
          true,
          11,
          150);
      }


      var red = TextureCache.HudRedGem.Value;
      var blue = TextureCache.HudBlueGem.Value;

      m_spriteBatch.Begin();
      // m_spriteBatch.Draw(red, new Rectangle(10, 33, red.Bounds.Width, red.Bounds.Height), Color.White);
      // gemSpriteRedHud.Draw(m_spriteBatch, new Vector2(banner_mid_x + 50, banner_mid_pos), 0, new Vector2(1.5f, 1.5f));
      // gemSpriteBlueHud.Draw(m_spriteBatch, new Vector2(banner_mid_x + 300, banner_mid_pos), 0, new Vector2(1.5f, 1.5f));

      gemSpriteRedHud.Draw(m_spriteBatch, new Vector2(30, 55), 0, new Vector2(1.5f, 1.5f));
      gemSpriteBlueHud.Draw(m_spriteBatch, new Vector2(30, 125), 0, new Vector2(1.5f, 1.5f));

      if (m_gameState.CurrentPurpleGemCount > 0)
        gemSpritePurpleHud.Draw(m_spriteBatch, new Vector2(30, 190), 0, new Vector2(1.5f, 1.5f));

      // m_spriteBatch.Draw(blue, new Rectangle(10, 110, blue.Bounds.Width, blue.Bounds.Height), Color.White);
      m_spriteBatch.End();

      ulong gemCount = m_gameState.CurrentRedGemCount;
      var s = NumberFormatter.AbbreviateBigNumber(gemCount);
#if !KNI_WEB

      var tx = FontManager.GetTextRenderer(() => ContentDirectory.Fonts.Roboto_Regular_ttf);

      var p = new Vector2(60, 55);
      var measure = Measure2(s, p, gemCountFontSize);
      p -= new Vector2(0, measure.Y / 2.0f);

      FontManager.RenderFieldFont(() => ContentDirectory.Fonts.Roboto_Regular_ttf, $"{s}", p, Color.Yellow, Color.Black, gemCountFontSize);
      FontManager.RenderFieldFont(() => ContentDirectory.Fonts.Roboto_Regular_ttf, $"{m_gameState.CurrentBlueGemCount}", new Vector2(60, 95), Color.Yellow, Color.Black, 55f);
      FontManager.RenderFieldFont(() => ContentDirectory.Fonts.Roboto_Regular_ttf, $"{m_gameState.CurrentPurpleGemCount}", new Vector2(60, 160), Color.Yellow, Color.Black, 55f);
#endif
      //FIXE: debug rendering
      // var camera = RenderingLibrary.SystemManagers.Default.Renderer.Camera;
      // m_shapeBatch.Begin();
      // foreach (var item in UpgradeManager.m_tooltipValueElements)
      // {
      //   Console.WriteLine(item.Width);
      //   camera.WorldToScreen(item.AbsoluteX, item.AbsoluteY, out float screenX, out float screenY);
      //   m_shapeBatch.BorderRectangle(new Vector2(screenX, screenY), new Vector2(item.Width, item.Height) * camera.Zoom, Color.AliceBlue);
      // }
      // m_shapeBatch.End();
    }

    public Vector2 Measure2(string Text, Vector2 position, float FontSize)
    {
      var r = FontManager.GetTextRenderer("Roboto_Regular_ttf");
      r.PositiveYIsDown = true;
      r.ResetLayout();

      var fontSize = FontSize;
      var measure = r.MeasureText(Text, position, 1, 1.171875f, fontSize, Color.Transparent, Color.Transparent, r.EnableKerning, r.PositiveYIsDown, r.PositionByBaseline, 0, new Vector2(0, 0), true, -1);
      return measure;
    }

    private void DrawImGUIContent()
    {
      if (KeyboardExtended.GetState().WasKeyPressed(Keys.Tab))
      {
        showDebugGUI = !showDebugGUI;
      }

      if (showDebugGUI && !UpgradeManager.Instance.UpgradeGuiEditMode)
      {
        ImGui.SetNextWindowBgAlpha(1.0f);
        // var deltaTime = (float)GameMain.GameInstance.TargetElapsedTime.TotalSeconds;
        // _frameCounter.Update(deltaTime);
        // var fps = string.Format("FPS: {0}", _frameCounter.AverageFramesPerSecond);
        // ImGui.Text(fps);
        ImGui.Text($"Entities: {m_escWorld.EntityCount}");
        ImGui.Text($"Picked Up: {Collected}");
        ImGui.Text($"Delivered: {Delivered}");

        ImGui.SetNextWindowBgAlpha(1.0f);

        ImGui.GetStyle().Colors[(int)ImGuiCol.SliderGrab] = new Vector4(1.0f, 0.0f, 0.0f, 1.0f);
        ImGui.GetStyle().Colors[(int)ImGuiCol.SliderGrabActive] = new Vector4(0.0f, 1.0f, 0.0f, 1.0f);

        //ImGui.GetStyle().Colors[(int)ImGuiCol.WindowBg] = new Vector4(0.0f, 1.0f, 0.0f, 1.0f);
        //ImGui.GetStyle().Colors[(int)ImGuiCol.ChildBg] = new Vector4(0.0f, 1.0f, 0.0f, 1.0f);
        ImGui.GetStyle().Colors[(int)ImGuiCol.FrameBg] = new Vector4(0.2f, 0.2f, 0.2f, 1.0f);
        ImGui.GetStyle().Colors[(int)ImGuiCol.FrameBgActive] = new Vector4(0.4f, 0.4f, 0.4f, 1.0f);
        ImGui.GetStyle().Colors[(int)ImGuiCol.FrameBgHovered] = new Vector4(0.3f, 0.3f, 0.3f, 1.0f);
        //ImGui.GetStyle().Colors[(int)ImGuiCol.ScrollbarBg] = new Vector4(0.0f, 1.0f, 0.0f, 1.0f);

        //ImGui.Begin("adad");
        //ImGui.GetStyle().Alpha = 1.0f;
        ImGui.SliderFloat("HarvesterSpeed", ref UpgradeManager.Instance.UG.HarvesterSpeed, 1.0f, 1000.0f);
        ImGui.SliderFloat("CameraZoomScale", ref UpgradeManager.Instance.UG.CameraZoomScale, 0, 3.0f);


        ImGui.SliderFloat("HarvesterCollectionRange", ref UpgradeManager.Instance.UG.HarvesterCollectionRange, 0, 100);
        ImGui.SliderFloat("HomebaseCollectionRange", ref UpgradeManager.Instance.UG.HomebaseCollectionRange, 0, 100);

        ImGui.SliderInt("HarvesterCapacity", ref UpgradeManager.Instance.UG.HarvesterCapacity, 0, 5000);


        ImGui.SliderInt("MaxGemCount", ref UpgradeManager.Instance.UG.MaxGemCount, 0, 500000);
        ImGui.SliderFloat("GemSpawnCooldown", ref UpgradeManager.Instance.UG.GemSpawnCooldown, 1.0f, 500.0f);

        ImGui.SliderInt("HarvesterCount", ref UpgradeManager.Instance.UG.HarvesterCount, 0, 25);
        ImGui.SliderInt("GemSpawnRate", ref UpgradeManager.Instance.UG.GemSpawnRate, 0, 500);


        ImGui.SliderInt("GemValue", ref UpgradeManager.Instance.UG.GemValue, 0, 5000);


        ImGui.SliderFloat("HarvesterMaximumFuel", ref UpgradeManager.Instance.UG.HarvesterMaxFuel, 0, 10000f);

        ImGui.SliderFloat("HarvesterRefuelSpeed", ref UpgradeManager.Instance.UG.HarvesterRefuelSpeed, 1, 1000f);

        ImGui.Checkbox("HomebaseCollector", ref UpgradeManager.Instance.UG.HomeBaseCollector);

        ImGui.Checkbox("RefuelAtHomebase", ref UpgradeManager.Instance.UGM.RefuelHomebase);
        ImGui.Checkbox("AutoRefuel", ref UpgradeManager.Instance.UGM.AutoRefuel);
        //ImGui.Combo("Test", ref Upgrades.HarvesterCollectionStrategyInt, Enum.GetNames<HarvesterStrategy>(), 10);

        // if (ImGui.BeginCombo("HarvesterCollectionStrategy", Upgrades.HarvesterCollectionStrategy.ToString()))
        // {
        //   for (int i = 0; i < Enum.GetValues(typeof(HarvesterStrategy)).Length; i++)
        //   {
        //     var projType = (HarvesterStrategy)i;
        //     bool isSelected = Upgrades.HarvesterCollectionStrategy == projType;
        //     if (ImGui.Selectable(projType.ToString(), isSelected))
        //     {
        //       Upgrades.HarvesterCollectionStrategy = projType;
        //     }
        //
        //     if (isSelected)
        //       ImGui.SetItemDefaultFocus();
        //   }
        //
        //   ImGui.EndCombo();
        // }
      }
    }

    float map(float x, float in_min, float in_max, float out_min, float out_max)
    {
      return (x - in_min) * (out_max - out_min) / (in_max - in_min) + out_min;
    }

    public float LogoAlpha { get; set; } = 1.0f;

    public override void Draw(GameTime gameTime)
    {
      if (m_escWorld == null)
        return;

      // if(GameMain.Instance.MaximizeFramefrate && _renderGuiSystem.drawUpgradesGui)
      //   return;

      var effect = EffectCache.BackgroundEffect.Value;
      m_camera_background.Zoom = map(m_camera.Zoom, 0, 3.0f, 0.3f, 1.0f);
      effect.Parameters["view_projection"]?.SetValue(m_camera_background.GetBoundingFrustum().Matrix);

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

      m_escWorld.Draw(gameTime);

      if (!GameStarted)
      {
        var sprite = TextureCache.Logo;

        int screenWidth = GameMain.Instance.GraphicsDevice.Viewport.Width;

        float topMarginPercent = 0.1f;
        int topMargin = (int)(GameMain.Instance.GraphicsDevice.Viewport.Height * topMarginPercent);

        float aspectRatio = (float)sprite.Value.Width / sprite.Value.Height;
        int logoWidth = (int)(screenWidth * 0.6f);
        int logoHeight = (int)(logoWidth / aspectRatio);
        int xPosition = (screenWidth - logoWidth) / 2;

        var destinationRect = new Rectangle(xPosition, topMargin, logoWidth, logoHeight);

        m_spriteBatch.Begin();
        m_spriteBatch.Draw(sprite, destinationRect, Color.White * LogoAlpha);
        m_spriteBatch.End();
      }

      // if (!UpgradeManager.UpdatingButtons)
      //   _renderGuiSystem?.Draw();

      // var deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
      // _frameCounter.Update(deltaTime);
    }
  }
}
