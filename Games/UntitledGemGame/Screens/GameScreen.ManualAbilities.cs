using System;
using AsyncContent;
using JapeFramework;
using JapeFramework.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using MonoGame.Extended.Input;
using UntitledGemGame.Entities;
using UntitledGemGame.Systems;

namespace UntitledGemGame.Screens;

public partial class UntitledGemGameGameScreen
{
  private Vector2? manualCrystalPosition;
  private readonly GemSpawnData[] manualCrystalShards = new GemSpawnData[ManualFleetAbilities.CrystalShardCount];
  private int manualShardNext, manualShardCount;
  public bool ManualWorldClickConsumed { get; private set; }

  private bool ManualAbilityInputEnabled => GameStarted && GameMain.Instance.IsActive
    && !GameMain.IsPaused && !m_prestiging && !m_postPrestige && !IsPrestigeConfirmationOpen
    && preGameTween.IsComplete && !_renderGuiSystem.IsOverlayVisible
    && !_renderGuiSystem.IsPopoutFocused && !_renderGuiSystem.SalvageInputCaptured
    && !m_upgradeManager.UpdatingButtons && !m_upgradeManager.UpgradeGuiEditMode;

  private void UpdateManualAbilities(float seconds)
  {
    ManualWorldClickConsumed = false;
    if (m_postPrestige) return;
    ManualAbilities.UpdateUnlocks(PrestigeProgression.AddSaturating(m_gameState.RedGemsEarnedThisRun, DeliveredUncounted));
    ManualAbilities.Update(seconds);
    FlushManualCrystalShards();
    if (!ManualAbilityInputEnabled) return;

    var keyboard = KeyboardExtended.GetState();
    var mouse = MouseExtended.GetState();
    var cursor = Gum.GumService.Default.Cursor;
    var point = new Point((int)cursor.X, (int)cursor.Y);
    if (manualCrystalPosition is Vector2 crystal && cursor.Y < HudLayout.ContentBottom
      && mouse.WasButtonPressed(MouseButton.Left)
      && !mouse.IsButtonDown(MouseButton.Right)
      && Vector2.DistanceSquared(m_camera.ScreenToWorld(mouse.Position.ToVector2()), crystal)
        <= MathF.Pow(48f / Math.Max(0.01f, m_camera.Zoom), 2f))
    {
      manualCrystalPosition = null;
      ManualWorldClickConsumed = true;
      AudioManager.Instance.PlaySound(AudioManager.Instance.GemClickSoundEffect);
      SpawnerEffects.Add(null, crystal, Color.Gold, 10f / m_camera.Zoom, 120f / m_camera.Zoom, 0.6f);
      manualShardNext = 0;
      FlushManualCrystalShards();
    }
    for (int i = 0; i < ManualFleetAbilities.Definitions.Length; i++)
    {
      bool pressed = keyboard.WasKeyPressed((Keys)((int)Keys.D1 + i))
        || keyboard.WasKeyPressed((Keys)((int)Keys.NumPad1 + i))
        || mouse.WasButtonPressed(MouseButton.Left) && HudLayout.ManualAbilityButton(i).Contains(point);
      if (i == ManualFleetAbilities.CrystalShatterSlot && (manualCrystalPosition.HasValue || manualShardNext < manualShardCount)) continue;
      if (pressed && ManualAbilities.TryActivate(i, ActivateManualEffect))
        AudioManager.Instance.PlaySound(AudioManager.Instance.MenuHoverButtonSoundEffect);
    }
  }

  private void ActivateManualEffect(int slot)
  {
    var bounds = PlayAreaBounds.ForCamera(m_camera);
    var home = m_homeBaseEntity.Get<Harvester>();
    float homeRange = BaseStats.GetHarvesterCollectionRange(home);
    if (slot == ManualFleetAbilities.MagnetizerSlot)
      SpawnerEffects.Add(null, HomeBasePos, Color.Cyan, homeRange,
        (bounds.Maximum - bounds.Minimum).Length() * 0.5f, 0.5f);
    else if (slot == ManualFleetAbilities.CashOutSlot)
    {
      HarvesterCollectionSystem.Instance.CashOutFleet(ManualAbilities.CashOutMultiplier);
      SpawnerEffects.Add(null, HomeBasePos, Color.Gold, homeRange * 2f, homeRange, 0.6f);
    }
    else if (slot == ManualFleetAbilities.CrystalShatterSlot)
    {
      float zoom = Math.Max(0.01f, m_camera.Zoom);
      var crystalArea = bounds.Inset(60f / zoom);
      var extent = crystalArea.Maximum - crystalArea.Minimum;
      float homeClearance = Math.Min(homeRange + 80f / zoom, Math.Min(extent.X, extent.Y) * 0.35f);
      var crystal = crystalArea.Minimum;
      float farthestDistance = -1f;
      // Bounded rejection sampling gives each cast a new location, with a fallback
      // for unusually small play areas where home clearance cannot be met.
      for (int attempt = 0; attempt < 12; attempt++)
      {
        var candidate = crystalArea.Minimum + extent * new Vector2(Random.Shared.NextSingle(), Random.Shared.NextSingle());
        float distance = Vector2.DistanceSquared(candidate, HomeBasePos);
        if (distance > farthestDistance)
        {
          crystal = candidate;
          farthestDistance = distance;
        }
        if (distance >= homeClearance * homeClearance)
        {
          crystal = candidate;
          break;
        }
      }
      manualCrystalPosition = crystal;
      manualShardCount = manualCrystalShards.Length;
      manualShardNext = 0;
      float crystalValueMultiplier = ManualAbilities.CrystalShardValueMultiplier(
        HarvesterCollectionSystem.Instance.GetFleetCargoCapacity());
      for (int i = 0; i < manualShardCount; i++)
      {
        float angle = i * MathHelper.TwoPi / manualShardCount;
        var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        var spawn = GemQualityTable.RollCurrent(valueMultiplier: crystalValueMultiplier);
        spawn.Position = bounds.Clamp(crystal + direction * 24f / Math.Max(0.01f, m_camera.Zoom));
        spawn.LaunchVelocity = direction * 160f / Math.Max(0.01f, m_camera.Zoom);
        manualCrystalShards[i] = spawn;
      }
    }
    else if (slot == ManualFleetAbilities.CollectorSwarmSlot)
    {
      HarvesterCollectionSystem.Instance.GetCollectorSwarmStats(out float speed, out float range);
      float collectorValue = HarvesterCollectionSystem.Instance.CollectorSwarmValueMultiplier();
      // Count stays fixed: amplifier and fleet progress strengthen the eight collectors.
      for (int i = 0; i < ManualFleetAbilities.CollectorCount; i++)
      {
        float angle = i * MathHelper.TwoPi / ManualFleetAbilities.CollectorCount;
        var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        var drone = m_entityFactory.CreateDrone(HomeBasePos + direction * 24f).Get<Harvester>();
        drone.IsCommandDrone = true;
        drone.CommandDroneSpeed = speed * ManualAbilities.Power;
        drone.CommandDroneRange = range * 0.25f * (1f + (ManualAbilities.Power - 1f) * 0.5f);
        drone.CommandDroneLifetime = 8f;
        drone.CommandDroneValueMultiplier = collectorValue * ManualAbilities.CollectorValueMultiplier;
        drone.TargetScreenPosition = bounds.Clamp(HomeBasePos + direction * (bounds.Maximum - bounds.Minimum).Length() * 0.4f);
      }
      SpawnerEffects.Add(null, HomeBasePos, Color.LightSkyBlue, homeRange, homeRange * 2f, 0.5f);
    }
  }

  private void FlushManualCrystalShards()
  {
    if (manualCrystalPosition.HasValue) return;
    int room = Math.Max(0, SignalStats.GemLimit - HarvesterCollectionSystem.Instance.flatSpatialHash.NumActiveGems
      - m_entityFactory.PendingGemSpawnCount);
    while (room-- > 0 && manualShardNext < manualShardCount)
    {
      var spawn = manualCrystalShards[manualShardNext++];
      m_entityFactory.QueueGemSpawn(spawn.Position, spawn.Type, spawn.BaseValue, spawn.IsLucky,
        launchVelocity: spawn.LaunchVelocity);
    }
  }

  private void ClearManualCrystal()
  {
    manualCrystalPosition = null;
    manualShardNext = manualShardCount = 0;
    ManualWorldClickConsumed = false;
  }

  private void DrawManualWorldEffects()
  {
    if (!GameStarted || m_prestiging || m_postPrestige) return;
    float zoom = Math.Max(0.01f, m_camera.Zoom);
    if (manualCrystalPosition is Vector2 crystal)
    {
      var texture = TextureCache.HudRedGem.Value;
      m_spriteBatch.Begin(transformMatrix: m_camera.GetViewMatrix(), samplerState: SamplerState.PointClamp);
      m_spriteBatch.Draw(texture, crystal, null, Color.Gold, 0f,
        new Vector2(texture.Width, texture.Height) * 0.5f, 72f / texture.Width / zoom, SpriteEffects.None, 0f);
      m_spriteBatch.End();
    }
    if (!ManualAbilities.IsActive(ManualFleetAbilities.MagnetizerSlot)) return;
    var bounds = PlayAreaBounds.ForCamera(m_camera);
    float radius = (bounds.Maximum - bounds.Minimum).Length() * 0.5f;
    float homeRange = BaseStats.GetHarvesterCollectionRange(m_homeBaseEntity.Get<Harvester>());
    m_shapeBatch.Begin(m_camera.GetViewMatrix(), blendState: BlendState.Additive);
    // Three shared inward rings; draw cost is independent of gem population.
    for (int ring = 0; ring < 3; ring++)
    {
      float phase = (ManualAbilities.MagnetElapsed * 0.7f + ring / 3f) % 1f;
      float r = MathHelper.Lerp(radius, homeRange, phase);
      var previous = HomeBasePos + new Vector2(r, 0f);
      for (int segment = 1; segment <= 64; segment++)
      {
        float angle = segment * MathHelper.TwoPi / 64f;
        var next = HomeBasePos + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * r;
        m_shapeBatch.FillLine(previous, next, 1.2f / zoom, Color.Cyan * (0.4f * (1f - phase)), 1f / zoom);
        previous = next;
      }
    }
    m_shapeBatch.End();
  }

  private void DrawManualAbilities()
  {
    var cursor = Gum.GumService.Default.Cursor;
    var point = new Point((int)cursor.X, (int)cursor.Y);
    bool enabled = ManualAbilityInputEnabled;
    for (int i = 0; i < ManualFleetAbilities.Definitions.Length; i++)
    {
      var definition = ManualFleetAbilities.Definitions[i];
      var panel = HudLayout.ManualAbilityButton(i);
      bool unlocked = ManualAbilities.IsUnlocked(i);
      bool active = ManualAbilities.IsActive(i);
      bool waitingForCrystal = i == ManualFleetAbilities.CrystalShatterSlot && (manualCrystalPosition.HasValue || manualShardNext < manualShardCount);
      bool ready = ManualAbilities.IsReady(i) && !waitingForCrystal;
      var accent = active ? OrbitSkin.ConfirmAccent : ready ? HudLayout.AbilityAccent : OrbitSkin.MutedTextColor;
      bool hover = enabled && unlocked && panel.Contains(point);
      m_spriteBatch.Begin();
      m_spriteBatch.Draw(AssetManager.DefaultTexture, panel,
        hover ? HudLayout.ButtonHoverColor : HudLayout.ButtonColor);
      OrbitSkin.NineSlice(m_spriteBatch, "button_idle_blue", panel, 8);
      m_spriteBatch.Draw(AssetManager.DefaultTexture, new Rectangle(panel.X, panel.Y, panel.Width, 2), accent);
      float progress = !unlocked ? (float)Math.Clamp((double)ManualAbilities.RunEarnings / definition.UnlockEarnings, 0d, 1d)
        : active ? ManualAbilities.RemainingDuration(i) / ManualAbilities.CastDuration(i)
        : 1f - ManualAbilities.RemainingCooldown(i) / definition.Cooldown;
      m_spriteBatch.Draw(AssetManager.DefaultTexture,
        new Rectangle(panel.X + 10, panel.Bottom - 8, (int)((panel.Width - 20) * progress), 3), accent);
      m_spriteBatch.End();

      DrawFittedHudText($"{i + 1}  {definition.Name}", new Vector2(panel.X + 10, panel.Y + 1),
        panel.Width - 20, 40f, enabled && unlocked ? accent : OrbitSkin.MutedTextColor);
      string status = !unlocked ? "LOCKED"
        : waitingForCrystal ? (manualCrystalPosition.HasValue ? "CLICK CRYSTAL" : "WAITING FOR SPACE")
        : active ? $"ACTIVE · {ManualAbilities.RemainingDuration(i):0.0}s"
        : ready ? $"READY · {definition.Cooldown:0}s"
        : $"{Math.Ceiling(ManualAbilities.RemainingCooldown(i)):0}s cooldown";
      float statusWidth = Measure2(status, Vector2.Zero, 30f).X;
      DrawFittedHudText(!unlocked ? $"Earn {NumberFormatter.AbbreviateBigNumber(definition.UnlockEarnings)} gems this run"
        : i == ManualFleetAbilities.CrystalShatterSlot && manualCrystalPosition.HasValue ? "CLICK THE GOLDEN CRYSTAL"
        : i == ManualFleetAbilities.CashOutSlot ? $"Beam cargo home · +{(ManualAbilities.CashOutMultiplier - 1f) * 100f:0}% value" : definition.Effect, new Vector2(panel.X + 10, panel.Y + 44),
        panel.Width - 44 - statusWidth, 30f, OrbitSkin.MutedTextColor);
      DrawFittedHudText(status, new Vector2(panel.Right - 10 - statusWidth, panel.Y + 44),
        statusWidth, 30f, !unlocked ? OrbitSkin.LockedTextColor : accent);
    }
  }
}
