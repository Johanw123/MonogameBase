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

    private const float GemCountBaseFontSize = 55f;
    private const float GemCountMaxFontSize = 78f;
    public float gemCountFontSize { get; set; } = GemCountBaseFontSize;
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

    private ulong _prestigeProgressReward = ulong.MaxValue;
    private ulong _prestigeProgressStart;
    private ulong? _prestigeProgressTarget;

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
        Text = $"{castCount}x MULTICAST!"
      };
      _nextMulticastPopup = (_nextMulticastPopup + 1) % _multicastPopups.Length;
    }

    public void ShowJackpotHaul(Vector2 worldPosition, ulong value, bool isMegaJackpot)
    {
      int popupIndex = _nextJackpotPopup;
      _nextJackpotPopup = (_nextJackpotPopup + 1) % _jackpotPopups.Length;

      ref JackpotPopup popup = ref _jackpotPopups[popupIndex];
      popup.Active = true;
      popup.WorldPosition = worldPosition;
      popup.TimeRemaining = JackpotPopupDuration;
      popup.HorizontalOffset = ((popupIndex % 5) - 2) * 14f;
      popup.IsMegaJackpot = isMegaJackpot;
      popup.Text = $"{(isMegaJackpot ? "MEGA JACKPOT!" : "JACKPOT!")} +{NumberFormatter.AbbreviateBigNumber(value)}";
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
    private ulong _prestigeRewardAtStart;

    public ulong GetPrestigeEarnings()
    {
      ulong delivered = PrestigeProgression.AddSaturating(m_gameState.RedGemsEarnedThisRun, DeliveredUncounted);
      delivered = PrestigeProgression.AddSaturating(delivered, HarvesterCollectionSystem.Instance?.GetCarriedGemValue() ?? 0);
      return PrestigeProgression.AddSaturating(delivered, UpdateSystem2.Instance?.GetUncollectedGemValue() ?? 0);
    }

    public void BeginPrestige()
    {
      if (m_prestiging || m_postPrestige) return;
      _prestigeRewardAtStart = PrestigeProgression.GetReward(GetPrestigeEarnings());
      m_prestiging = true;
    }
    public bool m_postPrestige = false;
    public float m_prestigeTime = 0;
    private readonly IncomeTracker _incomeTracker = new IncomeTracker(windowDuration: 30.0f);
    private float gemShowerTimer;
    private float gemCometTimer;
    private readonly List<SpawnStreakEffect> spawnStreakEffects = new();

    private sealed class SpawnStreakEffect
    {
      public Vector2 Start;
      public Vector2 End;
      public Color Color;
      public float Thickness;
      public float Duration;
      public float Age;
      public List<Vector2> PendingGemPositions;
      public int NextGemIndex;
    }

    private void AddSpawnStreak(
      Vector2 start,
      Vector2 end,
      Color color,
      float thickness,
      float duration,
      List<Vector2> pendingGemPositions = null)
    {
      spawnStreakEffects.Add(new SpawnStreakEffect
      {
        Start = start,
        End = end,
        Color = color,
        Thickness = thickness,
        Duration = duration,
        PendingGemPositions = pendingGemPositions,
      });
    }

    private void UpdateSpawnStreakEffects(float deltaTime)
    {
      for (int i = spawnStreakEffects.Count - 1; i >= 0; i--)
      {
        SpawnStreakEffect effect = spawnStreakEffects[i];
        effect.Age += deltaTime;

        if (effect.PendingGemPositions != null)
        {
          float progress = Math.Clamp(effect.Age / effect.Duration, 0.0f, 1.0f);
          float headProgress = Math.Min(1.0f, progress * 1.55f);

          while (effect.NextGemIndex < effect.PendingGemPositions.Count)
          {
            float gemProgress = (effect.NextGemIndex + 0.35f) / effect.PendingGemPositions.Count;
            if (gemProgress > headProgress)
              break;

            SpawnRolledGem(effect.PendingGemPositions[effect.NextGemIndex]);
            effect.NextGemIndex++;
          }
        }

        if (effect.Age >= effect.Duration)
          spawnStreakEffects.RemoveAt(i);
      }
    }

    private void DrawSpawnStreakEffects()
    {
      if (spawnStreakEffects.Count == 0)
        return;

      m_shapeBatch.Begin(m_camera.GetViewMatrix());
      foreach (SpawnStreakEffect effect in spawnStreakEffects)
      {
        float progress = Math.Clamp(effect.Age / effect.Duration, 0.0f, 1.0f);
        float headProgress = Math.Min(1.0f, progress * 1.55f);
        float tailProgress = Math.Max(0.0f, headProgress - 0.32f);
        Vector2 visibleStart = Vector2.Lerp(effect.Start, effect.End, tailProgress);
        Vector2 visibleEnd = Vector2.Lerp(effect.Start, effect.End, headProgress);
        float alpha = MathF.Sin(progress * MathHelper.Pi);
        m_shapeBatch.FillLine(visibleStart, visibleEnd, 0.01f,
          effect.Color * alpha, effect.Thickness * (0.65f + alpha * 0.35f));
      }
      m_shapeBatch.End();
    }

    private bool HasGemCapacity()
    {
      return HarvesterCollectionSystem.Instance.flatSpatialHash.NumActiveGems
        < UpgradeManager.Instance.UG.MaxGemCount;
    }

    private static uint MultiplyGemValue(uint value, float multiplier)
    {
      double multipliedValue = value * Math.Max(1.0, multiplier);
      return (uint)Math.Min(Math.Round(multipliedValue), uint.MaxValue);
    }

    private GemSpawnData RollAmbientGem(GemSpawnData? sharedQuality = null, float valueMultiplier = 1.0f)
    {
      var upgrades = UpgradeManager.Instance.UG;
      GemSpawnData gemSpawn = sharedQuality
        ?? GemQualityTable.Roll(upgrades.GemSpawnQuality, BaseStats.GetCurrentGemValue());

      if (upgrades.LuckyGems
        && Random.Shared.NextSingle() < Math.Clamp(upgrades.LuckyGemChance, 0.0f, 1.0f))
      {
        valueMultiplier *= upgrades.LuckyGemValue;
        gemSpawn.IsLucky = true;
      }

      gemSpawn.BaseValue = MultiplyGemValue(gemSpawn.BaseValue, valueMultiplier);
      return gemSpawn;
    }

    private void SpawnRolledGem(Vector2 position, GemSpawnData? sharedQuality = null, float valueMultiplier = 1.0f)
    {
      // if (!HasGemCapacity())
      //   return;

      GemSpawnData gemSpawn = RollAmbientGem(sharedQuality, valueMultiplier);
      m_entityFactory.CreateGem(position, gemSpawn.Type, gemSpawn.BaseValue, gemSpawn.IsLucky);
    }

    private void SpawnAmbientGemEvent(Vector2 minimumPosition, Vector2 maximumPosition)
    {
      var upgrades = UpgradeManager.Instance.UG;
      if (!HasGemCapacity())
        return;

      bool spawnCluster = upgrades.ClusterGems
        && Random.Shared.NextSingle() < Math.Clamp(upgrades.ClusterGemsChance, 0.0f, 1.0f);
      Vector2 clusterCenter = RandomHelper.Vector2(minimumPosition, maximumPosition);

      if (!spawnCluster)
      {
        SpawnRolledGem(clusterCenter);
        return;
      }

      int gemsPerCluster = Math.Max(1, upgrades.ClusterSize);
      if (upgrades.Motherlode && Random.Shared.NextSingle() < BaseStats.MotherlodeChance)
        gemsPerCluster *= BaseStats.MotherlodeSizeMultiplier;

      int clusterCount = upgrades.Supercluster && Random.Shared.NextSingle() < BaseStats.SuperclusterChance
        ? BaseStats.SuperclusterCount
        : 1;

      GemSpawnData? sharedQuality = null;
      if (upgrades.MonochromeVein && Random.Shared.NextSingle() < BaseStats.MonochromeVeinChance)
      {
        sharedQuality = GemQualityTable.Roll(upgrades.GemSpawnQuality, BaseStats.GetCurrentGemValue());
      }

      for (int clusterIndex = 0; clusterIndex < clusterCount; clusterIndex++)
      {
        Vector2 currentCenter = clusterCenter;
        if (clusterIndex > 0)
        {
          float centerAngle = MathHelper.TwoPi * clusterIndex / clusterCount;
          currentCenter += new Vector2(MathF.Cos(centerAngle), MathF.Sin(centerAngle))
            * BaseStats.ClusterRadius * 1.65f;
        }

        for (int i = 0; i < gemsPerCluster && HasGemCapacity(); i++)
        {
          Vector2 position = currentCenter;
          if (i > 0)
          {
            float angle = RandomHelper.Float(0.0f, MathHelper.TwoPi);
            // Square root keeps the cluster filled instead of crowding its center.
            float radius = MathF.Sqrt(Random.Shared.NextSingle()) * BaseStats.ClusterRadius;
            position += new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
          }

          float coreMultiplier = upgrades.ClusterCore && i == 0
            ? BaseStats.ClusterCoreValueMultiplier
            : 1.0f;
          SpawnRolledGem(position, sharedQuality, coreMultiplier);
        }
      }
    }

    private void UpdateSpecialGemSpawns(float deltaTime, Vector2 minimumPosition, Vector2 maximumPosition)
    {
      var upgrades = UpgradeManager.Instance.UG;

      if (upgrades.GemShower)
      {
        float showerCooldown = BaseStats.GemShowerCooldownSeconds
          / Math.Max(0.1f, upgrades.GemShowerCooldown);
        gemShowerTimer += deltaTime;
        if (gemShowerTimer >= showerCooldown)
        {
          gemShowerTimer -= showerCooldown;
          SpawnGemShower(minimumPosition, maximumPosition);
        }
      }
      else
      {
        gemShowerTimer = 0.0f;
      }

      if (upgrades.GemComet)
      {
        float cometCooldown = BaseStats.GemCometCooldownSeconds
          / Math.Max(0.1f, upgrades.GemCometCooldown);
        gemCometTimer += deltaTime;
        if (gemCometTimer >= cometCooldown)
        {
          gemCometTimer -= cometCooldown;
          SpawnGemComet(minimumPosition, maximumPosition);
        }
      }
      else
      {
        gemCometTimer = 0.0f;
      }
    }

    private void SpawnGemShower(Vector2 minimumPosition, Vector2 maximumPosition)
    {
      var upgrades = UpgradeManager.Instance.UG;
      const int streakCount = 6;
      const int baseGemCount = 30;
      int gemCount = Math.Max(1, upgrades.GemShowerGemCount);
      // Size upgrades broaden the presentation gently as well as adding gems.
      // Half-strength square-root growth avoids turning extra spread into a drawback.
      float showerWidth = 1.0f
        + (MathF.Sqrt(Math.Max(1.0f, gemCount / (float)baseGemCount)) - 1.0f) * 0.5f;
      int rows = (int)Math.Ceiling(gemCount / (float)streakCount);

      for (int column = 0; column < streakCount; column++)
      {
        float xProgress = (column + 0.5f) / streakCount;
        Vector2 streakStart = new Vector2(
          MathHelper.Lerp(minimumPosition.X, maximumPosition.X, xProgress),
          minimumPosition.Y);
        Vector2 streakEnd = new Vector2(streakStart.X + rows * 11.0f * showerWidth, maximumPosition.Y);
        Color streakColor = column % 2 == 0 ? new Color(90, 220, 255) : new Color(255, 120, 225);
        var gemPositions = new List<Vector2>(rows);

        for (int row = 0; row < rows; row++)
        {
          int gemIndex = row * streakCount + column;
          if (gemIndex >= gemCount)
            break;

          float yProgress = (row + 0.5f) / rows;
          Vector2 position = new Vector2(
            MathHelper.Lerp(minimumPosition.X, maximumPosition.X, xProgress) + row * 11.0f * showerWidth,
            MathHelper.Lerp(minimumPosition.Y, maximumPosition.Y, yProgress));
          position += new Vector2(
            RandomHelper.Float(-12.0f, 12.0f) * showerWidth,
            RandomHelper.Float(-18.0f, 18.0f));
          gemPositions.Add(position);
        }

        // The wide colored streak owns the scheduled gems; the white streak
        // is visual-only and follows the exact same motion.
        AddSpawnStreak(streakStart, streakEnd, streakColor, 13.0f * showerWidth, 1.05f, gemPositions);
        AddSpawnStreak(streakStart, streakEnd, Color.White, 3.0f * MathF.Sqrt(showerWidth), 1.05f);
      }
    }

    private void SpawnGemComet(Vector2 minimumPosition, Vector2 maximumPosition)
    {
      var upgrades = UpgradeManager.Instance.UG;
      const int baseGemCount = 24;
      int gemCount = Math.Max(2, upgrades.GemCometGemCount);
      float cometWidth = 1.0f
        + (MathF.Sqrt(Math.Max(1.0f, gemCount / (float)baseGemCount)) - 1.0f) * 0.5f;
      bool leftToRight = Random.Shared.Next(2) == 0;
      float height = maximumPosition.Y - minimumPosition.Y;
      float startY = RandomHelper.Float(minimumPosition.Y + height * 0.15f, maximumPosition.Y - height * 0.15f);
      float endY = MathHelper.Clamp(
        startY + RandomHelper.Float(-height * 0.35f, height * 0.35f),
        minimumPosition.Y,
        maximumPosition.Y);

      Vector2 start = new Vector2(leftToRight ? minimumPosition.X : maximumPosition.X, startY);
      Vector2 end = new Vector2(leftToRight ? maximumPosition.X : minimumPosition.X, endY);
      Vector2 direction = Vector2.Normalize(end - start);
      Vector2 perpendicular = new Vector2(-direction.Y, direction.X);
      var gemPositions = new List<Vector2>(gemCount);

      for (int i = 0; i < gemCount; i++)
      {
        float progress = i / (float)(gemCount - 1);
        Vector2 position = Vector2.Lerp(start, end, progress);
        position += perpendicular * RandomHelper.Float(-8.0f, 8.0f) * cometWidth;
        position += direction * RandomHelper.Float(-5.0f, 5.0f);
        gemPositions.Add(position);
      }

      AddSpawnStreak(start, end, new Color(70, 195, 255), 22.0f * cometWidth, 1.25f, gemPositions);
      AddSpawnStreak(start, end, new Color(220, 250, 255), 6.0f * MathF.Sqrt(cometWidth), 1.25f);
    }

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
          m_gameState.CompletePrestige(_prestigeRewardAtStart);
          UpdateSystem2.Instance.FinishPrestigeCollection();
          HarvesterCollectionSystem.Instance.ClearCargoForPrestige();
          m_homeBaseEntity?.Get<Harvester>()?.ClearCargoForPrestige();
          m_entityFactory.ClearPendingGemSpawns();
          spawnStreakEffects.Clear();
          spawnTimer = passiveIncomeTimer = gemShowerTimer = gemCometTimer = 0f;
          _incomeTracker.Reset();
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
      if (m_prestiging)
        return;
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
        for (int i = 0; i < UpgradeManager.Instance.UGM.StartingGemCount; i++)
        {
          var a = RandomHelper.Vector2(p0 + halfSpriteSize, p1 - halfSpriteSize);
          var gemSpawn = RollAmbientGem();
          m_entityFactory.QueueGemSpawn(a, gemSpawn.Type, gemSpawn.BaseValue, gemSpawn.IsLucky);
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

            SpawnAmbientGemEvent(p0 + halfSpriteSize, p1 - halfSpriteSize);
          }

          spawnTimer -= burstsToTrigger * currentCooldown;
        }
      }

      UpdateSpawnStreakEffects(deltaTime);
      UpdateSpecialGemSpawns(deltaTime, p0 + halfSpriteSize, p1 - halfSpriteSize);

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
      _incomeTracker.Update(dt, Delivered);

      // m_camera.Zoom = UpgradeManager.Instance.UG.CameraZoomScale;
      // m_camera.Zoom = MathHelper.Lerp(m_camera.Zoom, UpgradeManager.Instance.UG.CameraZoomScale, (float)gameTime.ElapsedGameTime.TotalSeconds);
      //TODO: find better lerp or an easing function
      // m_camera.Zoom = MathHelper.Lerp(m_camera.Zoom, 1.0f, (float)gameTime.ElapsedGameTime.TotalSeconds);

      m_escWorld.Update(gameTime);

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
      else if (curHarvesters > UpgradeManager.Instance.UG.AdvancedHarvesterCount)
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

      float currentGpm = _incomeTracker.GemsPerMinute;

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

      FontManager.RenderFieldFont(() => ContentDirectory.Fonts.Roboto_Regular_ttf, $"gem/m: {NumberFormatter.AbbreviateBigNumber((ulong)currentGpm)}", new Vector2(60, 250), Color.Yellow, Color.Black, 35f);

      DrawPrestigeProgress();

      DrawMetaUpgradeNotifications();
      DrawMulticastNotifications();

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

    // private void DrawPrestigeProgress()
    // {
    //   if (GameMain.IsPaused || RenderGuiSystem.Instance.drawUpgradesGui || m_prestiging || m_postPrestige)
    //     return;
    //
    //   ulong earnings = GetPrestigeEarnings();
    //   ulong reward = PrestigeProgression.GetReward(earnings);
    //   if (reward != _prestigeProgressReward)
    //   {
    //     _prestigeProgressReward = reward;
    //     _prestigeProgressStart = PrestigeProgression.GetRequiredEarnings(reward) ?? earnings;
    //     _prestigeProgressTarget = PrestigeProgression.GetRequiredEarnings(reward + 1);
    //   }
    //
    //   float progress = _prestigeProgressTarget is ulong target
    //     ? (float)Math.Clamp((double)(earnings - _prestigeProgressStart) / (target - _prestigeProgressStart), 0, 1)
    //     : 1f;
    //   var purple = new Color(190, 120, 255);
    //   var bar = new Rectangle(60, 342, 280, 12);
    //
    //   m_spriteBatch.Begin();
    //   m_spriteBatch.Draw(AssetManager.DefaultTexture, new Rectangle(48, 294, 304, 98), new Color(15, 10, 30, 205));
    //   m_spriteBatch.Draw(AssetManager.DefaultTexture, new Rectangle(bar.X - 1, bar.Y - 1, bar.Width + 2, bar.Height + 2), new Color(100, 65, 140));
    //   m_spriteBatch.Draw(AssetManager.DefaultTexture, bar, new Color(40, 25, 60));
    //   int fillWidth = (int)(bar.Width * progress);
    //   if (fillWidth > 0)
    //   {
    //     m_spriteBatch.Draw(AssetManager.DefaultTexture, new Rectangle(bar.X, bar.Y, fillWidth, bar.Height), purple);
    //     m_spriteBatch.Draw(AssetManager.DefaultTexture, new Rectangle(bar.X, bar.Y, fillWidth, 3), new Color(225, 185, 255));
    //   }
    //   m_spriteBatch.End();
    //
    //   FontManager.RenderFieldFont(() => ContentDirectory.Fonts.Roboto_Regular_ttf,
    //     $"Prestige: +{reward:N0} purple", new Vector2(60, 304), purple, Color.Black, 24f);
    //   string nextText = _prestigeProgressTarget is ulong next
    //     ? $"Next: {NumberFormatter.AbbreviateBigNumber(next - earnings)} more red"
    //     : "Maximum prestige reward reached";
    //   FontManager.RenderFieldFont(() => ContentDirectory.Fonts.Roboto_Regular_ttf,
    //     nextText, new Vector2(60, 363), new Color(220, 210, 235), Color.Black, 18f);
    // }
    private void DrawPrestigeProgress()
    {
      if (GameMain.IsPaused || RenderGuiSystem.Instance.drawUpgradesGui || m_prestiging || m_postPrestige)
        return;


      var vp = BaseGame.BoxingViewportAdapterGui.Viewport;


      // --- UI CONFIGURATION ---
      // Change this single variable to move the entire UI block
      // Vector2 basePos = new Vector2(48, 294);
      Vector2 basePos = new Vector2(48, vp.Height - 100);

      // Dimensions & Offsets (relative to basePos)
      Point panelSize = new Point(304, 98);
      Vector2 barOffset = new Vector2(12, 48);
      Point barSize = new Point(280, 12);
      Vector2 titleTextOffset = new Vector2(12, 10);
      Vector2 nextTextOffset = new Vector2(12, 69);

      // Colors
      Color panelBgColor = new Color(15, 10, 30, 205);
      Color barBorderColor = new Color(100, 65, 140);
      Color barBgColor = new Color(40, 25, 60);
      Color barFillColor = new Color(190, 120, 255); // previously "purple"
      Color barHighlightColor = new Color(225, 185, 255);
      Color nextTextColor = new Color(220, 210, 235);
      // ------------------------

      // Logic
      ulong earnings = GetPrestigeEarnings();
      ulong reward = PrestigeProgression.GetReward(earnings);
      if (reward != _prestigeProgressReward)
      {
        _prestigeProgressReward = reward;
        _prestigeProgressStart = PrestigeProgression.GetRequiredEarnings(reward) ?? earnings;
        _prestigeProgressTarget = PrestigeProgression.GetRequiredEarnings(reward + 1);
      }

      float progress = _prestigeProgressTarget is ulong target
          ? (float)Math.Clamp((double)(earnings - _prestigeProgressStart) / (target - _prestigeProgressStart), 0, 1)
          : 1f;

      // Derived Rectangles
      Rectangle panelRect = new Rectangle((int)basePos.X, (int)basePos.Y, panelSize.X, panelSize.Y);
      Rectangle barRect = new Rectangle((int)(basePos.X + barOffset.X), (int)(basePos.Y + barOffset.Y), barSize.X, barSize.Y);

      // Draw Sprites
      m_spriteBatch.Begin();

      // Background Panel
      m_spriteBatch.Draw(AssetManager.DefaultTexture, panelRect, panelBgColor);

      // Bar Border (drawn slightly larger than the bar)
      m_spriteBatch.Draw(AssetManager.DefaultTexture, new Rectangle(barRect.X - 1, barRect.Y - 1, barRect.Width + 2, barRect.Height + 2), barBorderColor);

      // Bar Background
      m_spriteBatch.Draw(AssetManager.DefaultTexture, barRect, barBgColor);

      // Bar Fill
      int fillWidth = (int)(barRect.Width * progress);
      if (fillWidth > 0)
      {
        m_spriteBatch.Draw(AssetManager.DefaultTexture, new Rectangle(barRect.X, barRect.Y, fillWidth, barRect.Height), barFillColor);
        m_spriteBatch.Draw(AssetManager.DefaultTexture, new Rectangle(barRect.X, barRect.Y, fillWidth, 3), barHighlightColor);
      }

      m_spriteBatch.End();

      // Draw Texts
      Vector2 titlePos = basePos + titleTextOffset;
      FontManager.RenderFieldFont(() => ContentDirectory.Fonts.Roboto_Regular_ttf,
          $"Prestige: +{reward:N0} purple", titlePos, barFillColor, Color.Black, 24f);

      Vector2 nextPos = basePos + nextTextOffset;
      string nextText = _prestigeProgressTarget is ulong next
          ? $"Next: {NumberFormatter.AbbreviateBigNumber(next - earnings)} more red"
          : "Maximum prestige reward reached";

      FontManager.RenderFieldFont(() => ContentDirectory.Fonts.Roboto_Regular_ttf,
          nextText, nextPos, nextTextColor, Color.Black, 18f);
    }

    private void DrawMetaUpgradeNotifications()
    {
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

        DrawCenteredNotification("RESONANCE CASCADE!", screenPosition.X, screenPosition.Y,
          30f, new Color(80, 255, 235) * alpha, Color.Black * alpha);
        DrawCenteredNotification("FLEET OVERDRIVE", screenPosition.X, screenPosition.Y + 29f,
          20f, Color.Gold * alpha, Color.Black * alpha);
      }
    }

    private void DrawMulticastNotifications()
    {
      if (GameMain.IsPaused || RenderGuiSystem.Instance.drawUpgradesGui
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
        camera.WorldToScreen(visual.AbsoluteLeft + visual.Width * 0.5f, visual.AbsoluteTop,
          out float centerX, out float topY);

        float progress = 1f - popup.TimeRemaining / MulticastPopupDuration;
        float fade = Math.Clamp(popup.TimeRemaining / 0.3f, 0f, 1f);
        float popProgress = Math.Clamp(progress / 0.16f, 0f, 1f);
        float popScale = 1f + MathF.Sin(popProgress * MathHelper.Pi) * 0.25f;
        float scale = camera.Zoom;
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
      FontManager.RenderFieldFont(() => ContentDirectory.Fonts.Roboto_Regular_ttf,
        text, position, color, outlineColor, fontSize);
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
        ImGui.Text($"Active gems: {HarvesterCollectionSystem.Instance.flatSpatialHash.NumActiveGems} / {UpgradeManager.Instance.UG.MaxGemCount}");
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
      DrawSpawnStreakEffects();

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
