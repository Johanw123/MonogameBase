using System.Collections.Generic;
using AsyncContent;
using JapeFramework;
using JapeFramework.Aseprite;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.Graphics;
using UntitledGemGame;
using UntitledGemGame.Screens;

public partial class RenderGuiSystem
{
  private const int ObjectiveRows = 5;
  private const int ObjectiveRowHeight = 92;
  private const int ObjectiveListTop = 196;
  private static Rectangle ObjectivePanel
    => new(40, 172, 700, ObjectiveListTop + ObjectiveRows * ObjectiveRowHeight + 16);
  private readonly List<RunObjective> pendingObjectives = new();
  private (Texture2D Texture, Texture2DRegion Region)? coreShardIcon;

  // The shard balance and the objectives closest to completion, beside the tree that spends them.
  private void DrawObjectivesPanel(SpriteBatch batch)
  {
    var screen = UntitledGemGameGameScreen.Instance;
    if (screen == null) return;
    var state = screen.State;
    var stats = screen.ObjectiveStats;

    pendingObjectives.Clear();
    foreach (var objective in CoreShards.Objectives)
      if (!state.CompletedObjectives.Contains(objective.Id)) pendingObjectives.Add(objective);
    // Closest to completion first; ties keep the catalog order.
    pendingObjectives.Sort((a, b) =>
    {
      int order = CoreShards.Progress(b, stats).CompareTo(CoreShards.Progress(a, stats));
      return order != 0 ? order
        : System.Array.IndexOf(CoreShards.Objectives, a).CompareTo(System.Array.IndexOf(CoreShards.Objectives, b));
    });
    int rows = System.Math.Min(ObjectiveRows, pendingObjectives.Count);

    var panel = ObjectivePanel;
    if (rows < ObjectiveRows)
      panel.Height -= (ObjectiveRows - System.Math.Max(1, rows)) * ObjectiveRowHeight;
    int left = panel.X + 28, right = panel.Right - 28;

    coreShardIcon ??= AsepriteHelper.LoadTextureFromAnimationFrame(CoreShards.IconPath, 0, CoreShards.IconFrames);
    var icon = coreShardIcon.Value;
    batch.Begin();
    OrbitSkin.Panel(batch, panel);
    batch.Draw(AssetManager.DefaultTexture,
      new Rectangle(panel.X, panel.Y, HudLayout.ButtonBorderThickness, panel.Height), CoreShards.Color * 0.8f);
    batch.Draw(icon.Texture, new Rectangle(left, panel.Y + 66, icon.Region.Width * 2, icon.Region.Height * 2),
      icon.Region.Bounds, Color.White);
    for (int i = 0; i < rows; i++)
    {
      int y = panel.Y + ObjectiveListTop + i * ObjectiveRowHeight;
      OrbitSkin.Progress(batch, new Rectangle(left, y + 52, right - left - 230, 8),
        CoreShards.Progress(pendingObjectives[i], stats));
    }
    batch.End();

    ulong shards = state.CurrentCoreShardCount;
    DrawObjectiveText("RUN OBJECTIVES", left, panel.Y + 20, 26f, OrbitSkin.MutedTextColor);
    DrawObjectiveTextRight($"{state.CompletedObjectives.Count} / {CoreShards.Objectives.Length} done",
      right, panel.Y + 20, 26f, OrbitSkin.MutedTextColor);
    DrawObjectiveText($"{shards} {(shards == 1 ? "Core Shard" : CoreShards.Name)}", left + 60, panel.Y + 70, 44f,
      CoreShards.Color);
    DrawObjectiveText("Spend on gold-ringed upgrades. Lost on prestige.", left, panel.Y + 136, 22f,
      OrbitSkin.MutedTextColor);

    if (rows == 0)
      DrawObjectiveText("Every objective complete this run!", left, panel.Y + ObjectiveListTop + 20, 28f,
        OrbitSkin.ButtonTextColor);
    for (int i = 0; i < rows; i++)
    {
      var objective = pendingObjectives[i];
      int y = panel.Y + ObjectiveListTop + i * ObjectiveRowHeight;
      DrawObjectiveText(objective.Title, left, y, 30f, OrbitSkin.ButtonTextColor);
      DrawObjectiveTextRight($"+{objective.Reward}", right, y, 30f, CoreShards.Color);
      DrawObjectiveTextRight($"{FormatObjectiveValue(objective, stats.Value(objective.Metric))} / "
        + FormatObjectiveValue(objective, objective.Target), right, y + 40, 22f, OrbitSkin.MutedTextColor);
    }
  }

  private static string FormatObjectiveValue(RunObjective objective, double value)
  {
    ulong whole = value >= ulong.MaxValue ? ulong.MaxValue : (ulong)System.Math.Max(0, value);
    return objective.Metric is ObjectiveMetric.GemsEarned or ObjectiveMetric.PeakGemsPerMinute
      ? NumberFormatter.AbbreviateBigNumber(whole, true) : whole.ToString();
  }

  private static void DrawObjectiveText(string text, float x, float y, float size, Color color)
    => FontManager.RenderFieldFont(() => ContentDirectory.Fonts.Roboto_Regular_ttf,
      text, new Vector2(x, y), color, Color.Black, size);

  private void DrawObjectiveTextRight(string text, float right, float y, float size, Color color)
    => DrawObjectiveText(text, right - Measure2(text, Vector2.Zero, size).X, y, size, color);
}
