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
        AudioManager.Instance.PlaySound(SoundEvent.ManualAbilityActivate);
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
    m_spriteBatch.Begin(transformMatrix: HudLayout.RenderTransform);
    for (int i = 0; i < ManualFleetAbilities.Definitions.Length; i++)
    {
      var definition = ManualFleetAbilities.Definitions[i];
      var panel = HudLayout.ManualAbilityButton(i);
      bool unlocked = ManualAbilities.IsUnlocked(i);
      bool active = ManualAbilities.IsActive(i);
      var accent = ManualAbilityAccent(i);
      bool hover = enabled && unlocked && panel.Contains(point);
      OrbitSkin.FlatPanel(m_spriteBatch, panel,
        hover ? new Color(18, 46, 72, 242) : new Color(6, 20, 38, 236),
        hover ? OrbitSkin.Accent : active ? OrbitSkin.ConfirmAccent : OrbitSkin.BorderColor * 0.85f, 1);

      // Dedicated tactical keycap badge on the left
      var keyBox = new Rectangle(panel.X + 14, panel.Y + 14, 38, 38);
      OrbitSkin.FlatPanel(m_spriteBatch, keyBox,
        active ? new Color(16, 62, 52, 245) : new Color(12, 36, 62, 240),
        unlocked ? accent * 0.85f : OrbitSkin.BorderColor * 0.45f, 1);

      float progress = !unlocked ? (float)Math.Clamp((double)ManualAbilities.RunEarnings / definition.UnlockEarnings, 0d, 1d)
        : active ? ManualAbilities.RemainingDuration(i) / ManualAbilities.CastDuration(i)
        : 1f - ManualAbilities.RemainingCooldown(i) / definition.Cooldown;
      var trackRect = new Rectangle(panel.X + 14, panel.Bottom - 9, panel.Width - 28, 3);
      m_spriteBatch.Draw(AssetManager.DefaultTexture, trackRect, OrbitSkin.BorderColor * 0.25f);
      if (progress > 0f)
        m_spriteBatch.Draw(AssetManager.DefaultTexture,
          new Rectangle(trackRect.X, trackRect.Y, Math.Max(2, (int)(trackRect.Width * progress)), trackRect.Height), accent);
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

      // Keycap number centered inside keyBox (panel.X + 14, panel.Y + 14, 38, 38)
      string keyLabel = (i + 1).ToString();
      var keyMeasure = Measure2(keyLabel, Vector2.Zero, 24f);
      FontManager.LayoutFieldFont(HudFont, keyLabel,
        new Vector2(panel.X + 33 - keyMeasure.X / 2f, panel.Y + 33 - keyMeasure.Y / 2f),
        enabled && unlocked ? Color.White : OrbitSkin.MutedTextColor, Color.Black, 24f);

      string status = !unlocked ? Loc.T("LOCKED")
        : active ? Loc.F("ACTIVE · {0:0.0}s", ManualAbilities.RemainingDuration(i))
        : ready ? Loc.F("READY · {0:0}s", definition.Cooldown)
        : Loc.F("{0:0}s cooldown", Math.Ceiling(ManualAbilities.RemainingCooldown(i)));
      float statusWidth = Measure2(status, Vector2.Zero, 20f).X;

      float textLeft = panel.X + 64;
      float titleMaxWidth = panel.Right - 16 - statusWidth - 12 - textLeft;
      LayoutFittedHudText(Loc.T(definition.Name), new Vector2(textLeft, panel.Y + 11),
        Math.Max(60f, titleMaxWidth), 27f, enabled && unlocked ? OrbitSkin.ButtonTextColor : OrbitSkin.MutedTextColor);
      LayoutFittedHudText(status, new Vector2(panel.Right - 16 - statusWidth, panel.Y + 15),
        statusWidth + 2, 20f, !unlocked ? OrbitSkin.LockedTextColor : accent);

      LayoutFittedHudText(!unlocked ? Loc.F("Earn {0} gems this run", NumberFormatter.AbbreviateBigNumber(definition.UnlockEarnings))
        : i == ManualFleetAbilities.PlanetCrackerSlot
          ? Loc.F("Beam rips gems loose · {0:0}/s", PlanetCrackerGemsPerSecond(ManualAbilities.PlanetCrackerMultiplier))
        : i == ManualFleetAbilities.AbilitySurgeSlot ? Loc.F("Auto recharge · {0:0.##}x for {1:0}s",
            active ? ManualAbilities.AutomaticRechargeMultiplier : ManualAbilities.AbilitySurgeMultiplier, definition.Duration)
        : Loc.T(definition.Effect), new Vector2(textLeft, panel.Y + 46),
        panel.Right - 16 - textLeft, 21f, OrbitSkin.MutedTextColor);
    }
    FontManager.EndFieldFonts(HudFont);
  }

  private Color ManualAbilityAccent(int slot)
    => ManualAbilities.IsActive(slot) ? OrbitSkin.ConfirmAccent
      : ManualAbilities.IsReady(slot) ? HudLayout.AbilityAccent : OrbitSkin.MutedTextColor;
}
