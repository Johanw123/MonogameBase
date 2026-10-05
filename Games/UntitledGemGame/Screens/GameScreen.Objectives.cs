using System;
using System.Collections.Generic;
using JapeFramework;
using Microsoft.Xna.Framework;

namespace UntitledGemGame.Screens;

public partial class UntitledGemGameGameScreen
{
  private const float ObjectivePopupDuration = 3.5f;
  private const float ObjectivePopupBacklogDuration = 1.2f;
  private readonly Queue<RunObjective> _objectivePopups = new();
  private float _objectivePopupTime;

  public RunObjectiveStats ObjectiveStats { get; private set; }
  // Several completions at once (such as after loading) flip past instead of queueing for a minute.
  private float CurrentObjectivePopupDuration
    => _objectivePopups.Count > 1 ? ObjectivePopupBacklogDuration : ObjectivePopupDuration;

  private void UpdateObjectives(float dt)
  {
    if (!progressReady || m_upgradeManager.UpdatingButtons || m_upgradeManager.UpgradeGuiEditMode)
      return;

    // Measure counted earnings, so a completion never runs ahead of the HUD.
    ObjectiveStats = CoreShards.Measure(m_upgradeManager.UG, m_gameState.RedGemsEarnedThisRun,
      m_gameState.PeakGemsPerMinute);
    var completed = m_gameState.CompleteObjectives(ObjectiveStats);
    if (completed.Count > 0)
    {
      foreach (var objective in completed)
        _objectivePopups.Enqueue(objective);
      AudioManager.Instance.PlaySound(AudioManager.Instance.UpgradeDoneEffect);
      m_upgradeManager.UpdateTooltipContent();
      SaveProgress();
    }

    if (_objectivePopups.Count > 0 && (_objectivePopupTime += dt) >= CurrentObjectivePopupDuration)
    {
      _objectivePopups.Dequeue();
      _objectivePopupTime = 0f;
    }
  }

  private void ClearObjectivePopups()
  {
    _objectivePopups.Clear();
    _objectivePopupTime = 0f;
  }

  private void DrawObjectiveNotification()
  {
    if (GameMain.IsPaused || RenderGuiSystem.Instance.DrawingPopout
      || !_objectivePopups.TryPeek(out var objective))
      return;

    float fadeIn = Math.Clamp(_objectivePopupTime / 0.2f, 0f, 1f);
    float fadeOut = Math.Clamp((CurrentObjectivePopupDuration - _objectivePopupTime) / 0.25f, 0f, 1f);
    float alpha = Math.Min(fadeIn, fadeOut);
    float centerX = HudLayout.Width / 2f;
    float y = 230f + (1f - fadeIn) * -24f;
    var panel = new Rectangle((int)(centerX - 460), (int)y - 52, 920, 196);

    m_spriteBatch.Begin();
    m_spriteBatch.Draw(AsyncContent.AssetManager.DefaultTexture, panel, OrbitSkin.PanelBackgroundTint * (0.92f * alpha));
    OrbitSkin.NineSlice(m_spriteBatch, "modal_info_complete", panel, 8, alpha);
    m_spriteBatch.End();

    DrawCenteredNotification("OBJECTIVE COMPLETE", centerX, y, 28f,
      CoreShards.Color * alpha, Color.Black * alpha);
    DrawCenteredNotification(objective.Title, centerX, y + 48f, 42f,
      OrbitSkin.StatHeadingColor * alpha, Color.Black * alpha);
    DrawCenteredNotification($"+{objective.Reward} {(objective.Reward == 1 ? "Core Shard" : CoreShards.Name)}",
      centerX, y + 100f, 32f, CoreShards.Color * alpha, Color.Black * alpha);
  }
}
