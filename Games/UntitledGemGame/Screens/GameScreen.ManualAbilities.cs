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
    if (slot == ManualFleetAbilities.FaultScanSlot)
      ScanFaults();
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

  // Panels first in one sprite batch, then every label in one stroke and one fill pass:
  // the slots never overlap, so the result matches drawing each slot in turn.
  private void DrawManualAbilities()
  {
    if (!ManualAbilities.CommandsEnabled) return;
    var cursor = GameInput.UiCursor;
    var point = new Point((int)cursor.X, (int)cursor.Y);
    bool enabled = ManualAbilityInputEnabled;
    m_spriteBatch.Begin();
    for (int i = 0; i < ManualFleetAbilities.Definitions.Length; i++)
    {
      var definition = ManualFleetAbilities.Definitions[i];
      var panel = HudLayout.ManualAbilityButton(i);
      bool unlocked = ManualAbilities.IsUnlocked(i);
      bool active = ManualAbilities.IsActive(i);
      var accent = ManualAbilityAccent(i);
      bool hover = enabled && unlocked && panel.Contains(point);
      m_spriteBatch.Draw(AssetManager.DefaultTexture, panel,
        hover ? HudLayout.ButtonHoverColor : HudLayout.ButtonColor);
      OrbitSkin.NineSlice(m_spriteBatch, "button_idle_blue", panel, 8);
      m_spriteBatch.Draw(AssetManager.DefaultTexture, new Rectangle(panel.X, panel.Y, panel.Width, 2), accent);
      float progress = !unlocked ? (float)Math.Clamp((double)ManualAbilities.RunEarnings / definition.UnlockEarnings, 0d, 1d)
        : active ? ManualAbilities.RemainingDuration(i) / ManualAbilities.CastDuration(i)
        : 1f - ManualAbilities.RemainingCooldown(i) / definition.Cooldown;
      m_spriteBatch.Draw(AssetManager.DefaultTexture,
        new Rectangle(panel.X + 10, panel.Bottom - 8, (int)((panel.Width - 20) * progress), 3), accent);
    }
    m_spriteBatch.End();

    FontManager.BeginFieldFonts(HudFont);
    for (int i = 0; i < ManualFleetAbilities.Definitions.Length; i++)
    {
      var definition = ManualFleetAbilities.Definitions[i];
      var panel = HudLayout.ManualAbilityButton(i);
      bool unlocked = ManualAbilities.IsUnlocked(i);
      bool active = ManualAbilities.IsActive(i);
      bool ready = ManualAbilities.IsReady(i);
      var accent = ManualAbilityAccent(i);
      LayoutFittedHudText($"{i + 1}  {Loc.T(definition.Name)}", new Vector2(panel.X + 10, panel.Y + 1),
        panel.Width - 20, 40f, enabled && unlocked ? accent : OrbitSkin.MutedTextColor);
      string status = !unlocked ? Loc.T("LOCKED")
        : active ? Loc.F("ACTIVE · {0:0.0}s", ManualAbilities.RemainingDuration(i))
        : ready ? Loc.F("READY · {0:0}s", definition.Cooldown)
        : Loc.F("{0:0}s cooldown", Math.Ceiling(ManualAbilities.RemainingCooldown(i)));
      float statusWidth = Measure2(status, Vector2.Zero, 30f).X;
      LayoutFittedHudText(!unlocked ? Loc.F("Earn {0} gems this run", NumberFormatter.AbbreviateBigNumber(definition.UnlockEarnings))
        : i == ManualFleetAbilities.PlanetCrackerSlot
          ? Loc.F("Beam rips gems loose · {0:0}/s", PlanetCrackerGemsPerSecond(ManualAbilities.PlanetCrackerMultiplier))
        : i == ManualFleetAbilities.AbilitySurgeSlot ? Loc.F("Auto recharge · {0:0.##}x for {1:0}s",
            active ? ManualAbilities.AutomaticRechargeMultiplier : ManualAbilities.AbilitySurgeMultiplier, definition.Duration)
        : Loc.T(definition.Effect), new Vector2(panel.X + 10, panel.Y + 44),
        panel.Width - 44 - statusWidth, 30f, OrbitSkin.MutedTextColor);
      LayoutFittedHudText(status, new Vector2(panel.Right - 10 - statusWidth, panel.Y + 44),
        statusWidth, 30f, !unlocked ? OrbitSkin.LockedTextColor : accent);
    }
    FontManager.EndFieldFonts(HudFont);
  }

  private Color ManualAbilityAccent(int slot)
    => ManualAbilities.IsActive(slot) ? OrbitSkin.ConfirmAccent
      : ManualAbilities.IsReady(slot) ? HudLayout.AbilityAccent : OrbitSkin.MutedTextColor;
}
