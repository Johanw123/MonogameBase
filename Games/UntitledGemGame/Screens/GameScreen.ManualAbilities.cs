using System;
using AsyncContent;
using JapeFramework;
using JapeFramework.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.Input;
using UntitledGemGame.Entities;
using UntitledGemGame.Systems;

namespace UntitledGemGame.Screens;

public partial class UntitledGemGameGameScreen
{
  private bool ManualAbilityInputEnabled => GameStarted && GameMain.Instance.IsActive
    && !GameMain.IsPaused && !m_prestiging && !m_postPrestige && !IsPrestigeConfirmationOpen
    && preGameTween.IsComplete && !_renderGuiSystem.IsOverlayVisible
    && !_renderGuiSystem.IsPopoutFocused && !_renderGuiSystem.SalvageInputCaptured
    && !m_upgradeManager.UpdatingButtons && !m_upgradeManager.UpgradeGuiEditMode;

  private void UpdateManualAbilities(float seconds)
  {
    if (m_postPrestige) return;
    ManualAbilities.Update(seconds);
    if (!ManualAbilityInputEnabled) return;

    var keyboard = KeyboardExtended.GetState();
    var mouse = MouseExtended.GetState();
    var cursor = Gum.GumService.Default.Cursor;
    var point = new Point((int)cursor.X, (int)cursor.Y);
    for (int i = 0; i < ManualFleetAbilities.Definitions.Length; i++)
    {
      bool pressed = keyboard.WasKeyPressed((Keys)((int)Keys.D1 + i))
        || keyboard.WasKeyPressed((Keys)((int)Keys.NumPad1 + i))
        || mouse.WasButtonPressed(MouseButton.Left) && HudLayout.ManualAbilityButton(i).Contains(point);
      if (pressed && ManualAbilities.TryActivate(i, ActivateManualInstantEffect))
        AudioManager.Instance.PlaySound(AudioManager.Instance.MenuHoverButtonSoundEffect);
    }
  }

  private void ActivateManualInstantEffect(int slot)
  {
    if (slot == 4)
    {
      HarvesterCollectionSystem.Instance.EmergencyRefuelFleet();
      return;
    }
    if (slot != 3) return;

    int count = Math.Min(80, Math.Max(0, SignalStats.GemLimit
      - HarvesterCollectionSystem.Instance.flatSpatialHash.NumActiveGems - m_entityFactory.PendingGemSpawnCount));
    float radius = BaseStats.GetHarvesterCollectionRange(m_homeBaseEntity.Get<Harvester>()) + 100f;
    var bounds = PlayAreaBounds.ForCamera(m_camera);
    for (int i = 0; i < count; i++)
    {
      float angle = i * MathHelper.TwoPi / count;
      var position = bounds.Clamp(HomeBasePos + new Vector2(MathF.Cos(angle), MathF.Sin(angle))
        * (radius + Random.Shared.NextSingle() * 80f));
      var spawn = GemQualityTable.RollCurrent();
      m_entityFactory.QueueGemSpawn(position, spawn.Type, spawn.BaseValue, spawn.IsLucky);
    }
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
      bool active = ManualAbilities.IsActive(i);
      bool ready = ManualAbilities.IsReady(i);
      var accent = active ? OrbitSkin.ConfirmAccent : ready ? HudLayout.AbilityAccent : OrbitSkin.MutedTextColor;
      bool hover = enabled && panel.Contains(point);
      m_spriteBatch.Begin();
      m_spriteBatch.Draw(AssetManager.DefaultTexture, panel,
        hover ? HudLayout.ButtonHoverColor : HudLayout.ButtonColor);
      OrbitSkin.NineSlice(m_spriteBatch, "button_idle_blue", panel, 8);
      m_spriteBatch.Draw(AssetManager.DefaultTexture, new Rectangle(panel.X, panel.Y, panel.Width, 2), accent);
      float progress = active ? ManualAbilities.RemainingDuration(i) / definition.Duration
        : 1f - ManualAbilities.RemainingCooldown(i) / definition.Cooldown;
      m_spriteBatch.Draw(AssetManager.DefaultTexture,
        new Rectangle(panel.X + 10, panel.Bottom - 8, (int)((panel.Width - 20) * progress), 3), accent);
      m_spriteBatch.End();

      DrawFittedHudText($"{i + 1}  {definition.Name}", new Vector2(panel.X + 10, panel.Y + 1),
        panel.Width - 20, 40f, enabled ? accent : OrbitSkin.MutedTextColor);
      string status = active ? $"ACTIVE · {ManualAbilities.RemainingDuration(i):0.0}s"
        : ready ? $"READY · {definition.Cooldown:0}s"
        : $"{Math.Ceiling(ManualAbilities.RemainingCooldown(i)):0}s cooldown";
      float statusWidth = Measure2(status, Vector2.Zero, 30f).X;
      DrawFittedHudText(definition.Effect, new Vector2(panel.X + 10, panel.Y + 44),
        panel.Width - 44 - statusWidth, 30f, OrbitSkin.MutedTextColor);
      DrawFittedHudText(status, new Vector2(panel.Right - 10 - statusWidth, panel.Y + 44),
        statusWidth, 30f, accent);
    }
  }
}
