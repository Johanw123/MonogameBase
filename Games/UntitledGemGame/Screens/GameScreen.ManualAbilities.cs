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
  private bool GameplayInputEnabled => GameStarted && GameInput.WindowActive
    && !GameMain.IsPaused && !m_prestiging && !m_postPrestige
    && preGameTween.IsComplete && !_renderGuiSystem.IsOverlayVisible
    && !_renderGuiSystem.IsPopoutFocused && !_renderGuiSystem.SalvageInputCaptured
    && !m_upgradeManager.UpdatingButtons && !m_upgradeManager.UpgradeGuiEditMode;

  private bool ManualAbilityInputEnabled => GameplayInputEnabled && ManualAbilities.CommandsEnabled;

  private void UpdateManualAbilities(float seconds)
  {
    if (m_postPrestige) return;
    ManualAbilities.UpdateUnlocks(PrestigeProgression.AddSaturating(m_gameState.RedGemsEarnedThisRun, DeliveredUncounted));
    if (!ManualAbilityInputEnabled)
    {
      ManualAbilities.Update(seconds);
      return;
    }

    var keyboard = KeyboardExtended.GetState();
    var mouse = GameInput.Mouse;
    var cursor = GameInput.UiCursor;
    var point = new Point((int)cursor.X, (int)cursor.Y);
    for (int i = 0; i < ManualFleetAbilities.Definitions.Length; i++)
    {
      bool pressed = keyboard.WasKeyPressed((Keys)((int)Keys.D1 + i))
        || keyboard.WasKeyPressed((Keys)((int)Keys.NumPad1 + i))
        || mouse.WasButtonPressed(MouseButton.Left) && HudLayout.ManualAbilityButton(i).Contains(point);
      if (pressed && ManualAbilities.TryActivate(i, ActivateManualEffect))
        AudioManager.Instance.PlaySound(AudioManager.Instance.MenuHoverButtonSoundEffect);
    }
    ManualAbilities.Update(seconds);
  }

  private void ActivateManualEffect(int slot)
  {
    var bounds = PlayAreaBounds.ForCamera(m_camera);
    var home = m_homeBaseEntity.Get<Harvester>();
    float homeRange = BaseStats.GetHarvesterCollectionRange(home);
    if (slot == ManualFleetAbilities.MagnetizerSlot)
      SpawnerEffects.Add(null, HomeBasePos, Color.Cyan, homeRange,
        (bounds.Maximum - bounds.Minimum).Length() * 0.5f, 0.5f);
    else if (slot == ManualFleetAbilities.AbilitySurgeSlot)
    {
      SpawnerEffects.Add(null, HomeBasePos, Color.Violet, homeRange, homeRange * 2f, 0.6f);
    }
    else if (slot == ManualFleetAbilities.PlanetCrackerSlot)
      StartPlanetCracker();
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

  private void DrawManualWorldEffects()
  {
    if (!GameStarted || m_prestiging || m_postPrestige) return;
    float zoom = Math.Max(0.01f, m_camera.Zoom);
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
    if (!ManualAbilities.CommandsEnabled) return;
    var cursor = GameInput.UiCursor;
    var point = new Point((int)cursor.X, (int)cursor.Y);
    bool enabled = ManualAbilityInputEnabled;
    for (int i = 0; i < ManualFleetAbilities.Definitions.Length; i++)
    {
      var definition = ManualFleetAbilities.Definitions[i];
      var panel = HudLayout.ManualAbilityButton(i);
      bool unlocked = ManualAbilities.IsUnlocked(i);
      bool active = ManualAbilities.IsActive(i);
      bool ready = ManualAbilities.IsReady(i);
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
        : active ? $"ACTIVE · {ManualAbilities.RemainingDuration(i):0.0}s"
        : ready ? $"READY · {definition.Cooldown:0}s"
        : $"{Math.Ceiling(ManualAbilities.RemainingCooldown(i)):0}s cooldown";
      float statusWidth = Measure2(status, Vector2.Zero, 30f).X;
      DrawFittedHudText(!unlocked ? $"Earn {NumberFormatter.AbbreviateBigNumber(definition.UnlockEarnings)} gems this run"
        : i == ManualFleetAbilities.PlanetCrackerSlot
          ? $"Beam rips gems loose · {PlanetCrackerGemsPerSecond(ManualAbilities.PlanetCrackerMultiplier):0}/s"
        : i == ManualFleetAbilities.AbilitySurgeSlot ? $"Auto recharge · {(active ? ManualAbilities.AutomaticRechargeMultiplier : ManualAbilities.AbilitySurgeMultiplier):0.##}x for {definition.Duration:0}s" : definition.Effect, new Vector2(panel.X + 10, panel.Y + 44),
        panel.Width - 44 - statusWidth, 30f, OrbitSkin.MutedTextColor);
      DrawFittedHudText(status, new Vector2(panel.Right - 10 - statusWidth, panel.Y + 44),
        statusWidth, 30f, !unlocked ? OrbitSkin.LockedTextColor : accent);
    }
  }
}
