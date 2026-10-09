using System;
using System.Collections.Generic;
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
using UntitledGemGame.Platform;
using UntitledGemGame.Systems;
using Vector4 = System.Numerics.Vector4;


//https://github.com/cpt-max/MonoGame-Shader-Samples?tab=readme-ov-file
//https://github.com/Amrik19/Monogame-Spritesheet-Instancing

namespace UntitledGemGame.Screens
{
  public partial class UntitledGemGameGameScreen : GameScreen
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
    public GameState State => m_gameState;
    private UpgradeManager m_upgradeManager;

    private readonly GameSaveStore saveStore = new(GameSaveStore.DefaultPath);
    private readonly bool startNewGame;
    private bool progressReady;
    private float autosaveTimer;

    private bool showDebugGUI = false;
    private int? pendingDebugPreset;

    private const float GemCountBaseFontSize = 56f;
    private const float GemCountMaxFontSize = 64f;
    public float gemCountFontSize { get; set; } = GemCountBaseFontSize;
    private readonly Tweener _tweener = new();
    private readonly Tweener _tweenerPreGame = new();
    private Tween? _gemCountTween;

    private MonoGame.Extended.Graphics.AnimatedSprite gemSpriteRedHud;
    private MonoGame.Extended.Graphics.AnimatedSprite coreShardSpriteHud;
    private MonoGame.Extended.Graphics.AnimatedSprite gemSpritePurpleHud;

    // private Texture2D buttonTexture;
    // private Texture2D buttonTexture;

    public UntitledGemGameGameScreen(Game game, bool newGame = false) : base(game)
    {
      startNewGame = newGame;
    }

    public static UntitledGemGameGameScreen Instance;

    public static Vector2 HomeBasePos = Vector2.Zero;
    private RenderGuiSystem _renderGuiSystem;
    private Entity m_homeBaseEntity;

    private bool m_postInitialized = false;
    private bool m_createdInitialGems = false;
    private int gemsPendingRestore;

    private const float JackpotPopupDuration = 1.35f;
    private const float ResonancePopupDuration = 1.5f;
    private const int MaxJackpotPopups = 12;

    private struct JackpotPopup
    {
      public bool Active;
      public Vector2 WorldPosition;
      public float TimeRemaining;
      public float HorizontalOffset;
      public bool IsMegaJackpot;
      public string Text;
    }

    private readonly JackpotPopup[] _jackpotPopups = new JackpotPopup[MaxJackpotPopups];
    private int _nextJackpotPopup;
    private float _resonancePopupTimeRemaining;


    private const float MulticastPopupDuration = 1.35f;
    private struct MulticastPopup
    {
      public IHomeBaseAbility Ability;
      public int CastCount;
      public float TimeRemaining;
      public string Text;
    }

    private readonly MulticastPopup[] _multicastPopups = new MulticastPopup[16];
    private int _nextMulticastPopup;

    public void ShowMulticast(IHomeBaseAbility ability, int castCount)
    {
      if (castCount < 2 || ability == null)
        return;

      _multicastPopups[_nextMulticastPopup] = new MulticastPopup
      {
        Ability = ability,
        CastCount = castCount,
        TimeRemaining = MulticastPopupDuration,
        Text = castCount == 2 ? Loc.T("ECHO!") : Loc.F("ECHO x{0}!", castCount)
      };
      _nextMulticastPopup = (_nextMulticastPopup + 1) % _multicastPopups.Length;
    }

    public void ShowJackpotHaul(Vector2 worldPosition, ulong value, bool isMegaJackpot)
      => ShowWorldPopup(worldPosition,
        (isMegaJackpot ? Loc.T("MEGA JACKPOT!") : Loc.T("JACKPOT!")) + " +" + NumberFormatter.AbbreviateBigNumber(value), isMegaJackpot);

    // Gold text that pops up from a point in the world and drifts up.
    public void ShowWorldPopup(Vector2 worldPosition, string text, bool large)
    {
      int popupIndex = _nextJackpotPopup;
      _nextJackpotPopup = (_nextJackpotPopup + 1) % _jackpotPopups.Length;

      ref JackpotPopup popup = ref _jackpotPopups[popupIndex];
      popup.Active = true;
      popup.WorldPosition = worldPosition;
      popup.TimeRemaining = JackpotPopupDuration;
      popup.HorizontalOffset = ((popupIndex % 5) - 2) * 14f;
      popup.IsMegaJackpot = large;
      popup.Text = text;
    }

    public void ShowResonanceCascade()
    {
      _resonancePopupTimeRemaining = ResonancePopupDuration;
    }

    public override void LoadContent()
    {

      base.LoadContent();

      PostInit();
    }

    public override void UnloadContent()
    {
      AudioManager.Instance.StopRefuelSounds();
      SaveProgress();
      ClearTransientEffects();
      // Seed markers reference gems in this world and must not survive into the next session.
      SpawnerEffects.Seeds.Clear();
      progressReady = false;
      Game.Exiting -= SaveOnLifecycleEvent;
      Game.Deactivated -= SaveOnLifecycleEvent;
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

      RenderGemSystem.Instance?.DisposeBuffers();
      base.UnloadContent();
    }

    // public Button m_upgradesButton;



    public void PostInit()
    {
      if (m_postInitialized) return;
      m_postInitialized = true;

      // ReplaceScreen constructs the incoming screen before unloading the outgoing one.
      // Keep the outgoing session's singletons intact until its teardown has finished.
      Game.IsMouseVisible = true;
      Instance = this;
      m_upgradeManager = new UpgradeManager();

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

      m_upgradeManager.OnUpgrade += (s) =>
      {
        m_homeBaseEntity.Get<HomeBase>().ActivateAbility(s);
      };

      // HomeBasePos = m_camera.ScreenToWorld(new Vector2(width / 2.0f, height / 2.0f));
      PlaceHomeBaseAndPlanet();
      m_homeBaseEntity = m_entityFactory.CreateHomeBase(HomeBasePos, Vector2.Zero);

      Delivered = Collected = DeliveredUncounted = 0;
      _incomeTracker.Reset();
      m_upgradeManager.Init(m_gameState);
      m_prestiging = m_postPrestige = false;
      m_prestigeTime = 0f;
      RenderGuiSystem.Instance.SetUpgradeType(RenderGuiSystem.UpgradeTypes.None);
      var save = startNewGame ? null : saveStore.Load();
      if (save != null)
      {
        m_gameState.Signals = save.Signals;
        m_gameState.Modules = save.Modules;
        // Expand Space depends on extractions, so restore the count before the trees.
        m_gameState.CoreExtractions = save.CoreExtractions;
        m_gameState.CoreDrillTunnels = Math.Clamp(save.CoreDrillTunnels, 0, CoreDrill.MaxTunnels);
        m_upgradeManager.RestoreProgress(save);
        m_gameState.Restore(save.RedGems, save.BlueGems, save.PurpleGems, save.RedGemsEarnedThisRun,
          save.AbilityPointsPurchased);
        m_gameState.RestorePrestige(save.PrestigePointsEarned, save.PendingPrestigePoints,
          save.PrestigeEcho, save.BestPrestigeProgress);
        m_gameState.RestoreCoreShards(save.CoreShards, save.CoreFractures, save.ShellDamage);
        m_gameState.Damage.RestoreRunTotals(save.DamageThisRun);
        ManualAbilities.UpdateUnlocks(m_gameState.RedGemsEarnedThisRun);
        m_createdInitialGems = save.CreatedInitialGems;
        gemsPendingRestore = Math.Clamp(save.ActiveGemCount ?? 0, 0,
          HarvesterCollectionSystem.Instance.flatSpatialHash.MaxCapacity);
        foreach (var button in UpgradeManager.CurrentUpgrades.UpgradeButtonsAbilities.Values)
          if (button.CurrentLevel > 0)
            m_homeBaseEntity.Get<HomeBase>().ActivateAbility(button.Data.ShortName);
        m_homeBaseEntity.Get<HomeBase>().RestoreEquippedAbilities(save.EquippedAbilities);
      }
      m_camera.Zoom = m_upgradeManager.UG.CameraZoomScale;
      // Loading already pauses the game: create the gem entities this run will use now, then
      // collect once, so play starts without that garbage or its promotion. A field already in
      // the thousands, or a player past their first extraction, fills up to the cap; an early
      // save does not pay for the whole cap.
      bool fillsField = gemsPendingRestore >= 5_000 || m_gameState.CoreExtractions > 0;
      m_entityFactory.WarmGemPool(fillsField ? SignalStats.GemLimit
        : Math.Min(SignalStats.GemLimit, gemsPendingRestore + 2_000));
      OrbitSkin.Preload(GraphicsDevice);
      GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
      // Position the whole hull below the viewport after restoring zoom and ship size.
      m_homeBaseEntity.Get<Transform2>().Position = HomeBaseIntroStart();
      progressReady = true;
      if (startNewGame)
        SaveProgress();
      Game.Exiting += SaveOnLifecycleEvent;
      Game.Deactivated += SaveOnLifecycleEvent;


      // AudioManager.Instance.ShipEngineDyingSoundEffect.Play();
      preGameTween = _tweenerPreGame.TweenTo(m_homeBaseEntity.Get<Transform2>(), t => t.Position, HomeBasePos, duration: 3.0f).OnEnd((a) =>
      {
        GameStart();
      }).Easing(EasingFunctions.CubicOut);

      // preGameTweenLogo = _tweenerPreGame.TweenTo(LogoAlpha, LogoAlpha, 0.0f, 2.0f);
      preGameTweenLogo = _tweenerPreGame.TweenTo(target: this, expression: t => t.LogoAlpha, toValue: 0.0f, duration: 2.0f);
    }

    public bool TryScanSignals(Random random)
    {
      if (GameMain.IsPaused || m_prestiging || m_postPrestige || !progressReady
        || !m_upgradeManager.UGM.SignalsUnlocked || !m_gameState.Signals.TryScan(m_gameState, random, SignalCatalog.IsAvailable)) return false;
      SaveProgress();
      return true;
    }

    public bool TryChooseSignal(int index)
    {
      if (GameMain.IsPaused || m_prestiging || m_postPrestige || !progressReady
        || !m_upgradeManager.UGM.SignalsUnlocked) return false;
      var abilities = m_homeBaseEntity.Get<HomeBase>().Abilities;
      var previousCooldowns = new int[abilities.Count];
      for (int i = 0; i < abilities.Count; i++) previousCooldowns[i] = abilities[i].MaxCooldownTime;
      if (!m_gameState.Signals.TryChoose(index)) return false;
      for (int i = 0; i < abilities.Count; i++)
        if (abilities[i].MaxCooldownTime < previousCooldowns[i])
          abilities[i].CooldownTime = Math.Max(1, (int)(abilities[i].CooldownTime
            * (double)abilities[i].MaxCooldownTime / previousCooldowns[i]));
      SaveProgress();
      return true;
    }

    private void SaveOnLifecycleEvent(object sender, EventArgs e) => SaveProgress();

    public void SaveProgress()
    {
      if (!progressReady || m_upgradeManager.UpdatingButtons || m_upgradeManager.UpgradeGuiEditMode)
        return;

      var save = new GameSave
      {
        RedGems = PrestigeProgression.AddSaturating(m_gameState.CurrentRedGemCount, DeliveredUncounted),
        Signals = m_gameState.Signals,
        Modules = m_gameState.Modules,
        BlueGems = m_gameState.CurrentBlueGemCount,
        AbilityPointsPurchased = m_gameState.AbilityPointsPurchased,
        PurpleGems = m_gameState.CurrentPurpleGemCount,
        // A shard still hovering (or about to fly out) is already earned.
        CoreShards = PrestigeProgression.AddSaturating(m_gameState.CurrentCoreShardCount, OwedCoreShards),
        CoreFractures = m_gameState.CoreFractures,
        ShellDamage = m_gameState.ShellDamage,
        DamageThisRun = m_gameState.Damage.RunTotals(),
        CoreExtractions = m_gameState.CoreExtractions,
        PrestigePointsEarned = m_gameState.PrestigePointsEarned,
        PendingPrestigePoints = m_gameState.PendingPrestigePoints,
        PrestigeEcho = m_gameState.PrestigeEcho,
        BestPrestigeProgress = m_gameState.BestPrestigeProgress,
        CoreDrillTunnels = m_gameState.CoreDrillTunnels,
        RedGemsEarnedThisRun = PrestigeProgression.AddSaturating(m_gameState.RedGemsEarnedThisRun, DeliveredUncounted),
        CreatedInitialGems = m_createdInitialGems,
        ActiveGemCount = (int)Math.Min(HarvesterCollectionSystem.Instance.flatSpatialHash.MaxCapacity,
          (long)HarvesterCollectionSystem.Instance.flatSpatialHash.NumActiveGems
          + gemsPendingRestore + m_entityFactory.PendingGemSpawnCount + pendingPlanetGems + deferredDebris.Count),
        EquippedAbilities = m_homeBaseEntity.Get<HomeBase>().GetEquippedAbilities()
      };
      m_upgradeManager.CaptureProgress(save);
      if (m_prestiging)
      {
        // Persist the completed transaction even if the player quits during its animation.
        save.RedGems = save.RedGemsEarnedThisRun = 0;
        save.PurpleGems = PrestigeProgression.AddSaturating(save.PurpleGems, m_gameState.PendingPrestigePoints);
        save.PendingPrestigePoints = 0;
        save.PrestigeEcho = m_gameState.EchoAfterExtraction;
        save.BestPrestigeProgress = 0;
        save.CoreShards = 0;
        save.CoreFractures = 0;
        save.ShellDamage = 0;
        save.DamageThisRun = new();
        // Power cells and Hollow World tunnels last one run (GameState.CompletePrestige).
        save.BlueGems = save.AbilityPointsPurchased = 0;
        save.CoreDrillTunnels = 0;
        save.CoreExtractions = PrestigeProgression.AddSaturating(save.CoreExtractions, 1);
        save.CreatedInitialGems = false;
        save.ActiveGemCount = 0;
      }
      saveStore.Save(save);
      autosaveTimer = 0;
    }

    private void GameStart()
    {
      GameStarted = true;
      FinishCrashIntro();

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
    public ClickUtility ClickUtility { get; } = new();
    public CursorGravityWell CursorGravity { get; } = new();
    private float passiveIncomeTimer = 0;
    private string previousButtonName = "null";
    public bool m_prestiging = false;

    // Prestige points stop coming in here: the extraction pays what the panel showed.
    public void BeginPrestige()
    {
      if (m_prestiging || m_postPrestige) return;
      ClearTransientEffects();
      m_prestiging = true;
      StartPrestigeCollapse();
    }

    private void ClearTransientEffects()
    {
      ManualAbilities.Reset();
      ClickUtility.Reset();
      CursorGravity.Reset();
      m_homeBaseEntity?.Get<HomeBase>()?.CancelAbilityEffects();
      m_entityFactory?.ClearPendingGemSpawns();
      ClearPlanetShots();
      ClearCoreDrills();
      Array.Clear(_jackpotPopups);
      Array.Clear(_multicastPopups);
      _resonancePopupTimeRemaining = 0f;
      ClearCoreFracture();
      ClearPlanetShell();
      ClearPrestigePoints();
    }
    public bool m_postPrestige = false;
    public float m_prestigeTime = 0;
    private readonly IncomeTracker _incomeTracker = new IncomeTracker(windowDuration: 30.0f);
    public ManualFleetAbilities ManualAbilities { get; } = new();
    private double passiveIncomeRemainder;
    private bool HasGemCapacity()
    {
      return HarvesterCollectionSystem.Instance.flatSpatialHash.NumActiveGems
        + m_entityFactory.PendingGemSpawnCount + pendingPlanetGems + deferredDebris.Count < SignalStats.GemLimit;
    }

    // A huge hit can knock tens of thousands of gems loose at once. Past this many in a frame
    // they wait, in order, for the next frames: the burst pours out over a few frames instead
    // of stalling one. Waiting gems count against the field limit and are saved.
    private const int DebrisSpawnsPerFrame = 3000;
    private readonly Queue<GemSpawnData> deferredDebris = new();
    private int debrisSpawnedThisFrame;

    private void SpawnDebris(in GemSpawnData data)
    {
      if (deferredDebris.Count == 0 && debrisSpawnedThisFrame < DebrisSpawnsPerFrame)
      {
        ++debrisSpawnedThisFrame;
        m_entityFactory.CreateGem(data.Position, data.Type, data.BaseValue, data.IsLucky, launchVelocity: data.LaunchVelocity);
      }
      else
        deferredDebris.Enqueue(data);
    }

    // End of each frame: spawn waiting debris with what is left of this frame's budget.
    private void SpawnDeferredDebris()
    {
      var grid = HarvesterCollectionSystem.Instance.flatSpatialHash;
      while (deferredDebris.Count > 0 && debrisSpawnedThisFrame < DebrisSpawnsPerFrame)
      {
        var data = deferredDebris.Dequeue();
        if (grid.NumActiveGems >= Math.Min(SignalStats.GemLimit, grid.MaxCapacity)) continue;
        ++debrisSpawnedThisFrame;
        m_entityFactory.CreateGem(data.Position, data.Type, data.BaseValue, data.IsLucky, launchVelocity: data.LaunchVelocity);
      }
      debrisSpawnedThisFrame = 0;
    }

    // Every gem gets its color from the fire power of what knocked it loose.
    private void SpawnRolledGem(Vector2 position, int firePower, float valueMultiplier = 1.0f, bool fromPlanet = false,
      int bonusPercent = 0)
    {
      if (fromPlanet)
        firePower = PrestigeTalentEffects.DeepCoreQualityPower(firePower);
      GemSpawnData gemSpawn = GemQualityTable.Roll(firePower, valueMultiplier);
      if (bonusPercent > 0)
        gemSpawn.BaseValue = AbilityGemValue.AddBonus(gemSpawn.BaseValue, bonusPercent);
      position = MoveOffPlanet(position);
      var data = new GemSpawnData { Position = position, Type = gemSpawn.Type, BaseValue = gemSpawn.BaseValue, IsLucky = gemSpawn.IsLucky };
      if (fromPlanet)
        (data.Position, data.LaunchVelocity) = PlanetLaunch(position);
      SpawnDebris(data);
    }

    private static Vector2 GetNormalGemSpawnPosition(Vector2 minimumPosition, Vector2 maximumPosition)
    {
      if (PlanetMiningEnabled)
        return SamplePlanetDebris(new PlayAreaBounds(minimumPosition, maximumPosition),
          SignalStats.FirePower(MainShipWeapon.Cannon));

      int samples = Random.Shared.NextSingle() < Math.Clamp(BaseStats.GemSpawnCenterBias, 0f, 1f)
        ? Math.Max(1, BaseStats.GemSpawnCenterSamples)
        : 1;

      // Averaging independent uniform samples favors the play-area center while
      // still allowing positions throughout the rectangle, including its corners.
      Vector2 position = Vector2.Zero;
      for (int i = 0; i < samples; i++)
        position += RandomHelper.Vector2(minimumPosition, maximumPosition);
      return position / samples;
    }

    public override void Update(GameTime gameTime)
    {
      WorldClickTriggered = false;
      if (pendingDebugFeature is { } featureAction)
      {
        pendingDebugFeature = null;
        featureAction();
        SaveProgress();
      }
      if (pendingPlanetToggle)
      {
        pendingPlanetToggle = false;
        SaveProgress();
        PlanetMiningEnabled = !PlanetMiningEnabled;
        GameMain.IsPaused = false;
        ScreenManager.ReplaceScreen(new UntitledGemGameGameScreen(Game) { showDebugGUI = true });
        return;
      }
      if (pendingDebugPreset is int stage)
      {
        pendingDebugPreset = null;
        var preset = stage >= 0 ? DebugProgressionPresets.Create(stage, UpgradeManager.CurrentUpgrades)
          : DebugProgressionPresets.CreateFeature(-stage - 1, UpgradeManager.CurrentUpgrades);
        if (saveStore.Save(preset))
        {
          // Prevent teardown from overwriting the preset with the previous session.
          progressReady = false;
          GameMain.IsPaused = false;
          ScreenManager.ReplaceScreen(new UntitledGemGameGameScreen(Game) { showDebugGUI = true });
          return;
        }
      }
      AudioManager.Instance.UpdateRefuelSounds(gameTime,
        m_escWorld == null || GameMain.IsPaused || m_prestiging || m_postPrestige || !preGameTween.IsComplete);
      var deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

      if (m_escWorld == null)
        return;

      // ScreenManager updates gameplay while the opaque menu transition covers it.
      // Also wait out loading/skipped draws so the first visible frame starts offscreen.
      if (!GameStarted && (IntroTransitionPending || !introFrameDrawn))
        return;

      if (!GemClickInputEnabled) ClickUtility.CancelHold();

      if (!UpgradeManager.Instance.UpdatingButtons)
        _renderGuiSystem?.Update(gameTime);

      autosaveTimer += deltaTime;
      if (autosaveTimer >= 5.0f)
        SaveProgress();

      if (GameMain.IsPaused)
        return;

      UpdateTimeLoop(deltaTime);
      UpdateCrashIntro(deltaTime);

      // Complete the intro before entering the normal gameplay update branches.
      if (!preGameTween.IsComplete)
      {
        _tweenerPreGame.Update(deltaTime);
        // Ability cooldowns and casts start with the world simulation, after landing.
        return;
      }

      float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

      if (GameStarted && !m_prestiging && !m_postPrestige && ShipSystems.Online
        && _renderGuiSystem.HasInputFocus
        && !m_upgradeManager.UpdatingButtons && !m_upgradeManager.UpgradeGuiEditMode
        && HudLayout.AbilityPointPanel.Contains(new Point(
          (int)GumService.Default.Cursor.X, (int)GumService.Default.Cursor.Y))
        && MouseExtended.GetState().WasButtonPressed(MouseButton.Left)
        && m_gameState.TryBuyAbilityPoint())
        SaveProgress();
      UpdateExtractHold(dt);
      UpdateDamagePanel();

      for (int i = 0; i < _jackpotPopups.Length; ++i)
      {
        if (!_jackpotPopups[i].Active)
          continue;

        _jackpotPopups[i].TimeRemaining -= dt;
        if (_jackpotPopups[i].TimeRemaining <= 0f)
          _jackpotPopups[i].Active = false;
      }
      _resonancePopupTimeRemaining = Math.Max(0f, _resonancePopupTimeRemaining - dt);
      for (int i = 0; i < _multicastPopups.Length; ++i)
      {
        ref MulticastPopup popup = ref _multicastPopups[i];
        popup.TimeRemaining = Math.Max(0f, popup.TimeRemaining - dt);
        if (popup.TimeRemaining <= 0f)
          popup = default;
      }

      gemSpriteRedHud?.Update(gameTime);
      coreShardSpriteHud?.Update(gameTime);
      gemSpritePurpleHud?.Update(gameTime);

      m_camera.Zoom = MathHelper.Lerp(m_camera.Zoom, UpgradeManager.Instance.UG.CameraZoomScale, (float)gameTime.ElapsedGameTime.TotalSeconds);

      if (m_prestiging)
      {
        m_prestigeTime += dt;
        DeliverGems(gameTime);
        m_escWorld.Update(gameTime);

        UpgradeManager.Instance.UG.HarvesterCount = 0;
        UpdatePrestigeCollapse(dt);

        if (m_prestigeTime > PrestigeCollapseSeconds)
        {
          m_gameState.CompletePrestige();
          // The first extraction reaches the first talent tier and its free Expand Space.
          m_upgradeManager.ApplyExpandSpace();
          UpdateSystem2.Instance.FinishPrestigeCollection();
          HarvesterCollectionSystem.Instance.ClearCargoForPrestige();
          m_homeBaseEntity?.Get<Harvester>()?.ClearCargoForPrestige();
          m_entityFactory.ClearPendingGemSpawns();
          ClearPlanetShots();
          spawnTimer = passiveIncomeTimer = 0f;
          passiveIncomeRemainder = 0;
          _incomeTracker.Reset();
          m_prestiging = false;
          m_postPrestige = true;
          m_prestigeTime = 0.0f;
          Delivered = 0;
          Collected = 0;
          DeliveredUncounted = 0;
          m_createdInitialGems = false;
          gemsPendingRestore = 0;
          RenderGuiSystem.Instance.SetUpgradeType(RenderGuiSystem.UpgradeTypes.Meta);
          HarvesterCollectionSystem.Instance.flatSpatialHash.PrepareQueries();
          SaveProgress();
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

      m_upgradeManager.Update(gameTime);
      if (m_prestiging)
        return;
      UpdateWorldClickInput(dt);
      // A core fracture holds commands and ship systems still until its shard is out.
      if (!FracturePaused)
      {
        UpdateManualAbilities(dt);
        m_homeBaseEntity?.Get<HomeBase>()?.Update(gameTime);
      }
      var keyboardState = KeyboardExtended.GetState();

      var spawnBounds = PlayAreaBounds.ForCamera(m_camera);
      var minimumSpawnPosition = spawnBounds.Minimum;
      var maximumSpawnPosition = spawnBounds.Maximum;

      if (gemsPendingRestore > 0)
      {
        for (int i = 0; i < gemsPendingRestore; i++)
        {
          var position = GetNormalGemSpawnPosition(minimumSpawnPosition, maximumSpawnPosition);
          var gemSpawn = GemQualityTable.Roll(SignalStats.FirePower(MainShipWeapon.Cannon));
          m_entityFactory.QueueGemSpawn(position, gemSpawn.Type, gemSpawn.BaseValue, gemSpawn.IsLucky);
        }
        gemsPendingRestore = 0;
      }

      if (!m_createdInitialGems)
      {
        m_createdInitialGems = true;
        Console.WriteLine("Creating initial gems: " + BaseStats.StartingGemCount);
        // A mined planet starts with an empty field: the player clicks the planet to fire.
        for (int i = 0; i < BaseStats.StartingGemCount && !PlanetMiningEnabled; i++)
        {
          var a = GetNormalGemSpawnPosition(minimumSpawnPosition, maximumSpawnPosition);
          var gemSpawn = GemQualityTable.Roll(SignalStats.FirePower(MainShipWeapon.Cannon));
          m_entityFactory.QueueGemSpawn(a, gemSpawn.Type, gemSpawn.BaseValue, gemSpawn.IsLucky);
        }
      }
      else
      {
        // The cannon's timer: once automated it fires on it (the old ambient spawn
        // timer still drives spawning when planet mining is off).
        float currentCooldown = MainShipWeapons.CannonShotInterval(
          PrestigeTalentEffects.AutomaticWeaponFireRate(SignalStats.FireRate(MainShipWeapon.Cannon)));
        int gemsPerSpawn = SignalStats.FirePower(MainShipWeapon.Cannon);
        if (!FracturePaused) spawnTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;

        if (spawnTimer >= currentCooldown)
        {
          int burstsToTrigger = (int)(spawnTimer / currentCooldown);

          int totalGemsToSpawn = burstsToTrigger * gemsPerSpawn;

          if (PlanetMiningEnabled && UpgradeManager.Instance.UG.AutoCannon)
            FirePlanetCannon(burstsToTrigger, gemsPerSpawn);
          for (int i = 0; i < totalGemsToSpawn && !PlanetMiningEnabled; ++i)
          {
            if (!HasGemCapacity())
              break;

            SpawnRolledGem(GetNormalGemSpawnPosition(minimumSpawnPosition, maximumSpawnPosition), gemsPerSpawn);
          }

          spawnTimer -= burstsToTrigger * currentCooldown;
        }
      }

      ClickUtility.Update(deltaTime);
      UpdatePlanet(deltaTime, minimumSpawnPosition, maximumSpawnPosition);

      if (UpgradeManager.Instance.UG.PassiveIncome > 0)
      {
        float currentInterval = UntitledGemGame.ClickUtility.PassiveInterval(UpgradeManager.Instance.UG);

        passiveIncomeTimer += deltaTime + ClickUtility.TakePassiveCredit();

        if (passiveIncomeTimer >= currentInterval)
        {
          int ticks = (int)(passiveIncomeTimer / currentInterval);
          double income = ticks * SignalStats.PassiveIncome + passiveIncomeRemainder;
          ulong earned = income >= ulong.MaxValue ? ulong.MaxValue : (ulong)income;
          passiveIncomeRemainder = income >= ulong.MaxValue ? 0 : income - earned;
          DeliveredUncounted = PrestigeProgression.AddSaturating(DeliveredUncounted, earned);
          passiveIncomeTimer -= ticks * currentInterval;
        }
      }

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
      if (keyboardState.WasKeyPressed(Keys.F6))
      {
        RenderGuiSystem.Instance.SetUpgradeType(RenderGuiSystem.UpgradeTypes.Signals);
      }
      if (keyboardState.WasKeyPressed(Keys.F5))
      {
        RenderGuiSystem.Instance.SetUpgradeType(RenderGuiSystem.UpgradeTypes.Shipyard);
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
        else if (RenderGuiSystem.Instance.m_upgradeWindowType is RenderGuiSystem.UpgradeTypes.Upgrades or RenderGuiSystem.UpgradeTypes.Abilities or RenderGuiSystem.UpgradeTypes.Shipyard or RenderGuiSystem.UpgradeTypes.Signals)
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
      _incomeTracker.Update(dt, Delivered);
      UpdatePrestigePoints(dt);
      UpdatePlanetShell(dt);
      UpdateCoreFracture(dt);

      // m_camera.Zoom = UpgradeManager.Instance.UG.CameraZoomScale;
      // m_camera.Zoom = MathHelper.Lerp(m_camera.Zoom, UpgradeManager.Instance.UG.CameraZoomScale, (float)gameTime.ElapsedGameTime.TotalSeconds);
      //TODO: find better lerp or an easing function
      // m_camera.Zoom = MathHelper.Lerp(m_camera.Zoom, 1.0f, (float)gameTime.ElapsedGameTime.TotalSeconds);

      m_escWorld.Update(gameTime);
      if (progressReady && !m_upgradeManager.UpdatingButtons && !m_upgradeManager.UpgradeGuiEditMode
        && m_upgradeManager.UGM.ShipyardUnlocked && m_gameState.Modules.AdvanceSalvage(dt, Random.Shared))
      {
        SaveProgress();
        AudioManager.Instance.PlaySound(AudioManager.Instance.BlipSoundEffect);
      }

      // 1. Calculate how far we are from the target scale (1.0f)
      float displacement = 1.0f - CurrentScale;

      // 2. Spring force pulls toward target, damping resists the velocity
      float springForce = displacement * SpringTension;
      float dampingForce = -ScaleVelocity * SpringDamping;

      // 3. Apply forces to velocity, and velocity to scale
      float acceleration = springForce + dampingForce;
      ScaleVelocity += acceleration * dt;
      CurrentScale += ScaleVelocity * dt;

      // 4. Prevent it from inverting or blowing up wildly
      CurrentScale = Math.Clamp(CurrentScale, 0.5f, 10.0f);

      if (HomeBase.Instance != null)
      {
        HomeBase.Instance.Entity.Get<Transform2>().Scale = new Vector2(CurrentScale, CurrentScale);
      }

      // The home base is a world-space sprite and can handle a much larger scale
      // multiplier. Applying that multiplier directly to screen-space HUD text
      // could produce a font size of 550px, pushing the gem count off-screen.
      gemCountFontSize = Math.Clamp(
        GemCountBaseFontSize * CurrentScale,
        GemCountBaseFontSize,
        GemCountMaxFontSize);


      SpawnAndRemoveHarvesters();

      TimerHelper.PumpEndOfFrameObjects();
      SpawnDeferredDebris();
      EntityFactory.Instance.Update();

      _tweener?.Update((float)gameTime.ElapsedGameTime.TotalSeconds);
    }

    // State
    public float CurrentScale = 1.0f;
    public float ScaleVelocity = 0.0f;

    // Tuning (Tweak these for the perfect juice)
    public float SpringTension = 250f; // How hard it snaps back to 1.0
    public float SpringDamping = 18f;  // How fast the bounciness settles

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

        // if (HomeBase.Instance != null)
        // {
        //   HomeBase.Instance.Entity.Get<Transform2>().Scale += scale;
        //   //Clamp scale for HomeBase
        //   HomeBase.Instance.Entity.Get<Transform2>().Scale =
        //     Vector2.Clamp(HomeBase.Instance.Entity.Get<Transform2>().Scale, new Vector2(1.0f, 1.0f), new Vector2(10.0f, 10.0f));
        // }

        Delivered += toDeliver;
        DeliveredUncounted -= toDeliver;

        m_gameState.EarnRedGems(toDeliver);

        // Add VELOCITY instead of raw scale. 
        // This stacks naturally if gems stream in over multiple frames.
        float logDelivery = MathF.Log10(Math.Max(1, toDeliver));
        ScaleVelocity += logDelivery * 0.35f;

        // gemCountFontSize = MathHelper.Clamp(gemCountFontSize, 55f, 100f);
        // var diff = gemCountFontSize - 55f;
        // gemCountFontSize = MathHelper.Lerp(gemCountFontSize, 55f, (float)gameTime.ElapsedGameTime.TotalSeconds * diff);
        // Console.WriteLine($"ToDeliver: {toDeliver}");

        m_upgradeManager.UpdateTooltipContent();
      }
    }

    private void SpawnAndRemoveHarvesters()
    {
      var curHarvesters = m_entityFactory.Harvesters.Count;
      int wantedHarvesters = PrestigeTalentEffects.FleetCount(UpgradeManager.Instance.UG.HarvesterCount);
      if (curHarvesters < wantedHarvesters)
      {
        m_entityFactory.CreateHarvester(HomeBasePos + RandomHelper.Vector2(new Vector2(-25, -25), new Vector2(25, 25)));
        Console.WriteLine("Added harvester due to upgrade.");
      }
      else if (curHarvesters > wantedHarvesters)
      {
        m_entityFactory.RemoveRandomHarvester(EntityFactory.Instance.Harvesters);
        Console.WriteLine("Removed excess harvester due to downgrade.");
      }

      curHarvesters = m_entityFactory.AdvancedHarvesters.Count;
      wantedHarvesters = PrestigeTalentEffects.FleetCount(UpgradeManager.Instance.UG.AdvancedHarvesterCount);
      if (curHarvesters < wantedHarvesters)
      {
        m_entityFactory.CreateAdvancedHarvester(HomeBasePos + RandomHelper.Vector2(new Vector2(-25, -25), new Vector2(25, 25)));
        Console.WriteLine("Added advanced harvester due to upgrade.");
      }
      else if (curHarvesters > wantedHarvesters)
      {
        m_entityFactory.RemoveRandomHarvester(EntityFactory.Instance.AdvancedHarvesters);
        Console.WriteLine("Removed excess advanced harvester due to downgrade.");
      }



      curHarvesters = m_entityFactory.PerimeterHarvesters.Count;
      wantedHarvesters = PrestigeTalentEffects.FleetCount(UpgradeManager.Instance.UG.PerimeterHarvesterCount);
      if (curHarvesters < wantedHarvesters)
      {
        m_entityFactory.CreatePerimeterHarvester(HomeBasePos + RandomHelper.Vector2(new Vector2(-25, -25), new Vector2(25, 25)));
        Console.WriteLine("Added perimeter harvester due to upgrade.");
      }
      else if (curHarvesters > wantedHarvesters)
      {
        m_entityFactory.RemoveRandomHarvester(EntityFactory.Instance.PerimeterHarvesters);
        Console.WriteLine("Removed excess perimeter harvester due to downgrade.");
      }



      curHarvesters = m_entityFactory.ExpertHarvesters.Count;
      wantedHarvesters = PrestigeTalentEffects.FleetCount(UpgradeManager.Instance.UG.ExpertHarvesterCount);
      if (curHarvesters < wantedHarvesters)
      {
        m_entityFactory.CreateExpertHarvester(HomeBasePos + RandomHelper.Vector2(new Vector2(-25, -25), new Vector2(25, 25)));
        Console.WriteLine("Added advanced harvester due to upgrade.");
      }
      else if (curHarvesters > wantedHarvesters)
      {
        m_entityFactory.RemoveRandomHarvester(EntityFactory.Instance.ExpertHarvesters);
        Console.WriteLine("Removed excess advanced harvester due to downgrade.");
      }



      curHarvesters = m_entityFactory.UltimateHarvesters.Count;
      wantedHarvesters = PrestigeTalentEffects.FleetCount(UpgradeManager.Instance.UG.UltimateHarvesterCount);
      if (curHarvesters < wantedHarvesters)
      {
        m_entityFactory.CreateUltimateHarvester(HomeBasePos + RandomHelper.Vector2(new Vector2(-25, -25), new Vector2(25, 25)));
        Console.WriteLine("Added advanced harvester due to upgrade.");
      }
      else if (curHarvesters > wantedHarvesters)
      {
        m_entityFactory.RemoveRandomHarvester(EntityFactory.Instance.UltimateHarvesters);
        Console.WriteLine("Removed excess advanced harvester due to downgrade.");
      }


    }

    private void DrawHudContent()
    {
      if (!string.IsNullOrEmpty(saveStore.Error))
        FontManager.RenderFieldFont(nameof(ContentDirectory.Fonts.Roboto_Regular_ttf),
          Loc.T(saveStore.Error), new Vector2(30, 330), Color.OrangeRed, Color.Black, 24f);

      if (!GameStarted)
        return;

      if (!UpgradeManager.Instance.UpdatingButtons && _renderGuiSystem != null)
        _renderGuiSystem.Draw(m_spriteBatch, DrawHudBackground);
      else
        DrawHudBackground();

      if (coreShardSpriteHud == null)
      {
        coreShardSpriteHud = AsepriteHelper.LoadAnimation(
          CoreShards.IconPath,
          true,
          CoreShards.IconFrames,
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


      m_spriteBatch.Begin();
      for (int i = 0; i < 4; i++)
      {
        var card = HudLayout.ResourcePanel(i);
        m_spriteBatch.Draw(AssetManager.DefaultTexture, card, HudLayout.ButtonColor);
        m_spriteBatch.Draw(AssetManager.DefaultTexture,
          new Rectangle(card.X + 12, card.Bottom - 2, card.Width - 24, 2), OrbitSkin.BorderColor);
      }
      var gemsPanel = HudLayout.ResourcePanel(0);
      var coreShardPanel = HudLayout.ResourcePanel(2);
      var prestigeResourcePanel = HudLayout.ResourcePanel(3);
      gemSpriteRedHud ??= AsepriteHelper.LoadAnimation(
        "Textures/Gems/Gem1/GEM 1 - RED - Spritesheet.png", true, 10, 150);
      gemSpriteRedHud.Draw(m_spriteBatch, new Vector2(gemsPanel.X + 28, gemsPanel.Y + 67), 0, Vector2.One * 1.5f);
      coreShardSpriteHud.Draw(m_spriteBatch, new Vector2(coreShardPanel.X + 28, coreShardPanel.Y + 67), 0, Vector2.One * 1.5f);
      gemSpritePurpleHud.Draw(m_spriteBatch, new Vector2(prestigeResourcePanel.X + 28, prestigeResourcePanel.Y + 67), 0, Vector2.One * 1.5f);
      m_spriteBatch.End();

#if !KNI_WEB
      DrawHudResource(Loc.T("GEMS"), NumberFormatter.AbbreviateBigNumber(m_gameState.CurrentRedGemCount),
        HudLayout.ResourcePanel(0), gemCountFontSize, new Color(255, 215, 150));
      DrawHudResource(CoreShards.Name, NumberFormatter.AbbreviateBigNumber(m_gameState.CurrentCoreShardCount),
        HudLayout.ResourcePanel(2), 56f, CoreShards.Color);
      DrawHudResource(Loc.T("Prestige points"), NumberFormatter.AbbreviateBigNumber(m_gameState.CurrentPurpleGemCount),
        HudLayout.ResourcePanel(3), 56f, new Color(210, 170, 255));
      DrawHudResource(Loc.T("GEMS / MIN"), NumberFormatter.AbbreviateBigNumber((ulong)_incomeTracker.GemsPerMinute),
        HudLayout.ResourcePanel(1), 56f, new Color(235, 230, 215), false);

      if (ClickUtility.Combo > 0 && !RenderGuiSystem.Instance.IsOverlayVisible
        && (UpgradeManager.Instance.UG.ClickComboBonus > 0 || ClickUtility.LastCritical
          || UpgradeManager.Instance.UGM.ClickComboSupernova))
        DrawFittedHudText((ClickUtility.LastCritical ? Loc.T("CRITICAL!") + "  " : "")
          + Loc.F("CLICK x{0}", ClickUtility.Combo) + "  |  " + Loc.F("{0:0.##}x VALUE", ClickUtility.LastMultiplier)
          + (UpgradeManager.Instance.UGM.ClickComboSupernova ? "  |  " + Loc.F("SUPERNOVA {0}/5", ClickUtility.SupernovaProgress) : ""),
          new Vector2(20, 16), 660, 28f, ClickUtility.LastCritical ? Color.Gold : Color.Aquamarine);
      DrawExtractPanel(HudLayout.PrestigePanel);
      DrawPrestigePoints();
      DrawAbilityPointProgress();
      DrawMetaUpgradeNotifications();
      DrawMulticastNotifications();
      DrawCoreFractureHud();
      DrawDamagePanel();
      DrawExtractionCaptions();
      DrawExtractTooltip();
#endif
      DrawManualAbilities();
    }

    private void DrawHudBackground()
    {
      m_spriteBatch.Begin();
      m_spriteBatch.Draw(AssetManager.DefaultTexture,
        new Rectangle(0, HudLayout.ContentBottom, HudLayout.Width, HudLayout.Height), OrbitSkin.PanelBackgroundTint);
      OrbitSkin.NineSlice(m_spriteBatch, "modal_title_background",
        new Rectangle(0, HudLayout.ContentBottom, HudLayout.Width, HudLayout.Height), 8);
      m_spriteBatch.Draw(AssetManager.DefaultTexture,
        new Rectangle(0, HudLayout.ContentBottom, HudLayout.Width, 2), OrbitSkin.BorderColor);
      m_spriteBatch.End();
    }

    public void DrawHudRedGem(SpriteBatch batch, Vector2 position)
    {
      gemSpriteRedHud ??= AsepriteHelper.LoadAnimation(
        "Textures/Gems/Gem1/GEM 1 - RED - Spritesheet.png", true, 10, 150);
      gemSpriteRedHud.Draw(batch, position, 0, Vector2.One);
    }

    private void DrawHudResource(string label, string value, Rectangle panel,
      float fontSize, Color color, bool hasIcon = true)
    {
      DrawFittedHudText(label, new Vector2(panel.X + 16, panel.Y + 6), panel.Width - 32,
        36f, OrbitSkin.MutedTextColor);
      float textX = panel.X + (hasIcon ? 56 : 16);
      float availableWidth = panel.Right - 16 - textX;
      // Keep the animated balance inside its card even at maximum currency.
      var measure = Measure2(value, Vector2.Zero, fontSize);
      fontSize *= Math.Min(1f, availableWidth / Math.Max(1f, measure.X));
      measure = Measure2(value, Vector2.Zero, fontSize);
      FontManager.RenderFieldFont(nameof(ContentDirectory.Fonts.Roboto_Regular_ttf),
        value, new Vector2(textX, panel.Y + 69 - measure.Y / 2), color, Color.Black, fontSize);
    }

    private const string HudFont = nameof(ContentDirectory.Fonts.Roboto_Regular_ttf);

    private void DrawFittedHudText(string text, Vector2 position, float width, float fontSize, Color color)
    {
      var measure = Measure2(text, Vector2.Zero, fontSize);
      fontSize *= Math.Min(1f, width / Math.Max(1f, measure.X));
      FontManager.RenderFieldFont(HudFont, text, position, color, Color.Black, fontSize);
    }

    // DrawFittedHudText inside FontManager.BeginFieldFonts/EndFieldFonts.
    private void LayoutFittedHudText(string text, Vector2 position, float width, float fontSize, Color color)
    {
      var measure = Measure2(text, Vector2.Zero, fontSize);
      fontSize *= Math.Min(1f, width / Math.Max(1f, measure.X));
      FontManager.LayoutFieldFont(HudFont, text, position, color, Color.Black, fontSize);
    }

    private void DrawAbilityPointProgress()
    {
      if (GameMain.IsPaused || m_prestiging || m_postPrestige || !ShipSystems.Online)
        return;

      var panel = HudLayout.AbilityPointPanel;
      ulong? price = m_gameState.NextAbilityPointPrice;
      ulong balance = m_gameState.CurrentRedGemCount;
      bool available = price is ulong cost && balance >= cost
        && m_gameState.CurrentBlueGemCount < ulong.MaxValue
        && !m_upgradeManager.UpdatingButtons && !m_upgradeManager.UpgradeGuiEditMode;
      bool hovered = panel.Contains(new Point((int)GumService.Default.Cursor.X,
        (int)GumService.Default.Cursor.Y));
      float progress = price is ulong target ? (float)Math.Min(1d, (double)balance / target) : 1f;
      int padding = HudLayout.ProgressPanelPadding;
      int contentWidth = panel.Width - padding * 2;
      var bar = new Rectangle(panel.X + padding, panel.Y + HudLayout.ProgressBarTop, contentWidth, 8);

      m_spriteBatch.Begin();
      OrbitSkin.Button(m_spriteBatch, panel, available && hovered, confirm: available);
      OrbitSkin.Progress(m_spriteBatch, bar, progress);
      m_spriteBatch.End();

      DrawFittedHudText(Loc.T("Buy +1 power cell"),
        new Vector2(panel.X + padding, panel.Y + HudLayout.ProgressTitleTop), contentWidth, 36f,
        available ? Color.White : HudLayout.AbilityAccent);
      string status = price is ulong next
        ? (available
          ? Loc.F("Ready · {0} gems", NumberFormatter.AbbreviateBigNumber(next))
          : Loc.F("Cost: {0} gems", NumberFormatter.AbbreviateBigNumber(next)))
        : Loc.T("Maximum reached");
      DrawFittedHudText(status, new Vector2(panel.X + padding, panel.Y + HudLayout.ProgressStatusTop),
        contentWidth, 32f, available ? Color.White : OrbitSkin.MutedTextColor);
    }

    private void DrawMetaUpgradeNotifications()
    {
      if (GameMain.IsPaused || RenderGuiSystem.Instance.IsOverlayVisible
        || RenderGuiSystem.Instance.DrawingPopout)
        return;

      for (int i = 0; i < _jackpotPopups.Length; ++i)
      {
        ref JackpotPopup popup = ref _jackpotPopups[i];
        if (!popup.Active)
          continue;

        float progress = 1.0f - popup.TimeRemaining / JackpotPopupDuration;
        float fade = Math.Clamp(popup.TimeRemaining / 0.28f, 0f, 1f);
        float popProgress = Math.Clamp(progress / 0.14f, 0f, 1f);
        float popScale = 1.0f + MathF.Sin(popProgress * MathHelper.Pi) * 0.18f;
        Vector2 screenPosition = m_camera.WorldToScreen(popup.WorldPosition);
        screenPosition.X += popup.HorizontalOffset;
        screenPosition.Y -= 58f + progress * 48f;

        float fontSize = (popup.IsMegaJackpot ? 38f : 32f) * popScale;
        Color color = popup.IsMegaJackpot ? new Color(255, 225, 90) : Color.Gold;
        DrawCenteredNotification(popup.Text, screenPosition.X, screenPosition.Y,
          fontSize, color * fade, Color.Black * fade);
      }

      if (_resonancePopupTimeRemaining > 0f)
      {
        float progress = 1.0f - _resonancePopupTimeRemaining / ResonancePopupDuration;
        float fadeIn = Math.Clamp(progress / 0.12f, 0f, 1f);
        float fadeOut = Math.Clamp(_resonancePopupTimeRemaining / 0.35f, 0f, 1f);
        float alpha = Math.Min(fadeIn, fadeOut);
        Vector2 screenPosition = m_camera.WorldToScreen(HomeBasePos);
        screenPosition.Y -= 105f + progress * 24f;

        DrawCenteredNotification(Loc.T("RESONANCE CASCADE!"), screenPosition.X, screenPosition.Y,
          30f, new Color(80, 255, 235) * alpha, Color.Black * alpha);
        DrawCenteredNotification(Loc.T("FLEET OVERDRIVE"), screenPosition.X, screenPosition.Y + 29f,
          20f, Color.Gold * alpha, Color.Black * alpha);
      }
    }

    private void DrawMulticastNotifications()
    {
      if (GameMain.IsPaused || RenderGuiSystem.Instance.IsOverlayVisible
        || RenderGuiSystem.Instance.DrawingPopout
        || UpgradeManager.Instance.UpdatingButtons || HomeBase.Instance == null)
        return;

      var camera = SystemManagers.Default.Renderer.Camera;
      for (int i = 0; i < _multicastPopups.Length; ++i)
      {
        ref MulticastPopup popup = ref _multicastPopups[i];
        if (popup.TimeRemaining <= 0f
          || !HomeBase.Instance.AbilityButtons.TryGetValue(popup.Ability, out var button)
          || !button.IsVisible)
          continue;

        var visual = button.Visual;
        float centerX = visual.AbsoluteLeft + visual.Width * 0.5f;
        float topY = visual.AbsoluteTop;
        // The detached main HUD is drawn at origin with zoom 1. The shared
        // Gum camera has already reverted to the upgrade tree camera here.
        float scale = 1f;
        if (!RenderGuiSystem.Instance.IsDetached)
        {
          camera.WorldToScreen(centerX, topY, out centerX, out topY);
          scale = camera.Zoom;
        }

        float progress = 1f - popup.TimeRemaining / MulticastPopupDuration;
        float fade = Math.Clamp(popup.TimeRemaining / 0.3f, 0f, 1f);
        float popProgress = Math.Clamp(progress / 0.16f, 0f, 1f);
        float popScale = 1f + MathF.Sin(popProgress * MathHelper.Pi) * 0.25f;
        float fontSize = (22f + (popup.CastCount - 2) * 2f) * popScale * scale;
        float y = topY - (26f + progress * 48f) * scale;
        Color color = popup.CastCount switch
        {
          2 => new Color(90, 225, 255),
          3 => new Color(190, 130, 255),
          4 => new Color(255, 170, 65),
          _ => new Color(255, 225, 90)
        };

        DrawCenteredNotification(popup.Text, centerX, y,
          fontSize, color * fade, Color.Black * fade);
      }
    }

    private void DrawCenteredNotification(string text, float centerX, float y,
      float fontSize, Color color, Color outlineColor)
    {
      var measure = Measure2(text, Vector2.Zero, fontSize);
      var position = new Vector2(centerX - measure.X * 0.5f, y - measure.Y * 0.5f);
      FontManager.RenderFieldFont(nameof(ContentDirectory.Fonts.Roboto_Regular_ttf),
        text, position, color, outlineColor, fontSize);
    }

    public Vector2 Measure2(string Text, Vector2 position, float FontSize)
    {
      var r = FontManager.GetTextRenderer("Roboto_Regular_ttf");
      r.PositiveYIsDown = true;
      // MeasureText leaves the layout alone, so texts batched with BeginFieldFonts survive.

      var fontSize = FontSize;
      var measure = r.MeasureText(Text, position, 1, 1.171875f, fontSize, Color.Transparent, Color.Transparent, r.EnableKerning, r.PositiveYIsDown, r.PositionByBaseline, 0, new Vector2(0, 0), true, -1);
      return measure;
    }

    private readonly DebugStatEditor debugStatEditor = new();
    private readonly DebugFeatureTools debugFeatureTools = new();
    private Action pendingDebugFeature;

    // Everything currently marking the planet, for tuning the molten, lightning and crit builds.
    private void DrawPlanetDebuffsDebug()
    {
      if (!ImGui.CollapsingHeader("Planet debuffs", ImGuiTreeNodeFlags.DefaultOpen)) return;
      ImGui.Text($"Molten craters: {craters.Count} / {CraterCap}");
      ImGui.Text($"Magma scars: {magmaScars.Count} / {MagmaScarCap}");
      ImGui.Text($"Shock: {ShockStacks} / {PrestigeTalentEffects.MaxShockStacks} stacks, "
        + $"+{(PrestigeTalentEffects.ShockMultiplier(ShockStacks) - 1f) * 100f:0}% damage taken");
      ImGui.Text($"Conductor slugs: {conductorSlugs.Count} / {MainShipWeapons.ConductorSlugs}");
      ImGui.Text($"Weak points: {weakPoints.Count} / {PrestigeTalentEffects.MaxWeakPoints}");
      ImGui.Text(m_gameState.ShellBroken ? "Shell: broken"
        : $"Shell: {PlanetShell.Wear(m_gameState.ShellDamage) * 100f:0}% cracked");
      ImGui.Text($"Weapon damage bonus (all sources): +{weaponYieldBonus * 100f:0}%");
      ImGui.Text($"Crits: +{PrestigeTalentEffects.WeaponCritChance * 100f:0}% chance on every weapon, "
        + $"hot streak {hotStreak.Count} / {PrestigeTalentEffects.HotStreakMaxStacks}, critical mass queued {criticalMassTargets.Count}, "
        + $"x{PrestigeTalentEffects.CritMultiplier} per crit");
      ImGui.Text($"Railgun: charge {railgunCharge * 100f:0}%, bank {railgunBank} / {MainShipWeapons.CapacitorRounds - 1}, "
        + $"battery +{kineticBattery * 100f:0}%, recoil harvest {recoilHarvest:0.0}s");
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
        ImGui.Text($"Active gems: {HarvesterCollectionSystem.Instance.flatSpatialHash.NumActiveGems} / {UpgradeManager.Instance.UG.MaxGemCount}");
        ImGui.Text($"Queryable gems: {HarvesterCollectionSystem.Instance.flatSpatialHash.AvailableCount}, updating: {UpdateSystem2.Instance.UpdatingGemCount}");
        ImGui.Text($"Gem quads rebuilt: {RenderGemSystem.Instance.RebuiltQuadsLastFrame}, pages uploaded: {RenderGemSystem.Instance.UploadedPagesLastFrame}");
        ImGui.Text($"Picked Up: {Collected}");
        ImGui.Text($"Delivered: {Delivered}");
        ImGui.Text($"Prestige: {m_gameState.PrestigePointsEarned} earned, {m_gameState.PendingPrestigePoints} this run, next at {m_gameState.Income.PerMinute:N0} / {PrestigeProgression.Threshold(m_gameState.PrestigePointsEarned):N0} gems/min");
        ImGui.Text($"Prestige bar: echo {m_gameState.PrestigeEcho:P0} + live {m_gameState.PrestigeProgress:P0}, best {m_gameState.BestPrestigeProgress:P0}, next echo {m_gameState.EchoAfterExtraction:P0}");
        DrawPlanetDebuffsDebug();

        ImGui.Separator();
        ImGui.TextWrapped("Presets replace and save your progress. Feature scenarios are debug sandboxes.");
        ImGui.BeginDisabled(!progressReady || m_prestiging || m_postPrestige || m_upgradeManager.UpdatingButtons);
        if (ImGui.CollapsingHeader("Gameplay progression"))
          for (int stage = 0; stage < DebugProgressionPresets.Names.Length; stage++)
          {
            if (ImGui.Button(DebugProgressionPresets.Names[stage])) pendingDebugPreset = stage;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(DebugProgressionPresets.Descriptions[stage]);
          }
        if (ImGui.CollapsingHeader("Feature scenarios"))
          for (int feature = 0; feature < DebugProgressionPresets.FeatureNames.Length; feature++)
            if (ImGui.Button(DebugProgressionPresets.FeatureNames[feature])) pendingDebugPreset = -feature - 1;
        if (ImGui.CollapsingHeader("Current session tools"))
        {
          ImGui.TextWrapped("These actions keep your current build and save the result.");
          bool planetMining = PlanetMiningEnabled;
          if (ImGui.Checkbox("Planet mining prototype (reloads)", ref planetMining))
            pendingPlanetToggle = true;
          if (ImGui.Button("Add 10,000 red gems"))
          {
            m_gameState.CurrentRedGemCount = PrestigeProgression.AddSaturating(m_gameState.CurrentRedGemCount, 10_000);
            SaveProgress();
          }
          if (ImGui.Button($"Add 5 {CoreShards.Name}"))
          {
            m_gameState.CurrentCoreShardCount = PrestigeProgression.AddSaturating(m_gameState.CurrentCoreShardCount, 5);
            SaveProgress();
          }
          ImGui.SameLine();
          ImGui.BeginDisabled(fractureActive || shellShattering);
          if (ImGui.Button(PlanetShelled ? "Shatter planet shell" : "Trigger core fracture"))
            StartCoreFracture();
          ImGui.EndDisabled();
          if (PlanetShelled)
            ImGui.Text($"Shell damage {m_gameState.ShellDamage:N0} / {PlanetShell.Health:N0}");
          else
            ImGui.Text($"Damage/min {planetDamage.PerMinute:N0} / next fracture {NextFractureDamage:N0} ({m_gameState.CoreFractures} this run)");
          ImGui.BeginDisabled(m_upgradeManager.UG.AutoRefuel);
          if (ImGui.Button("Unlock auto refuel"))
          {
            m_upgradeManager.GrantDebugAutoRefuel();
            SaveProgress();
          }
          ImGui.EndDisabled();
          if (ImGui.Button("Discover all modules"))
          {
            m_gameState.Modules.DiscoverAllModules();
            SaveProgress();
          }
          if (ImGui.Button("Queue 3 module discoveries"))
          {
            DebugProgressionPresets.QueueDiscoveries(m_gameState.Modules);
            SaveProgress();
          }
          if (ImGui.Button("Next module discovery in 1 second"))
          {
            var modules = m_gameState.Modules;
            modules.StartSalvage(new Random());
            if (!modules.CollectionComplete)
            {
              modules.DiscoveryProgressSeconds = modules.DiscoveryThresholdSeconds - 1;
              modules.RecordHarvest();
            }
            SaveProgress();
          }
        }
        debugFeatureTools.Draw(m_upgradeManager, m_gameState, action => pendingDebugFeature = action);
        debugStatEditor.Draw(m_upgradeManager);
        ImGui.EndDisabled();
        if (saveStore.Error != null) ImGui.TextWrapped(saveStore.Error);

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

      DrawPlanet();
      DrawCrashIntro();
      m_escWorld.Draw(gameTime);
      if (!IntroTransitionPending && EffectCache.HarvesterEffect.IsLoaded)
        introFrameDrawn = true;
      DrawWeapons();
      // Shapes render into the virtual-sized target, before it is scaled to the window.
      m_shapeBatch.Begin(UntitledGemGame.ClickUtility.RenderView(m_camera.GetViewMatrix(),
          BaseGame.BoxingViewportAdapter.GetScaleMatrix()),
        Matrix.CreateOrthographicOffCenter(0, BaseGame.BoxingViewportAdapter.VirtualWidth,
          BaseGame.BoxingViewportAdapter.VirtualHeight, 0, 0, 1), blendState: BlendState.Additive);
      ClickUtility.Draw(m_shapeBatch, m_camera.Zoom);
      m_shapeBatch.End();
      DrawGemClickRadius();
      DrawManualWorldEffects();
      DrawLoopFlash();
      ApplyPrestigeWarp();
      DrawTimeLoopHole();

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
