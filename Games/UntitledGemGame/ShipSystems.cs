using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace UntitledGemGame;

// Players know the home base abilities as Ship Systems, powered by Power Cells (the
// "blue" currency). Each system has its own small talent tab, laid out as a fixed grid
// like the prestige tree: a node needs the node above it in its column, and every row
// needs a number of cells spent in the rows above it in the same system. Cells and
// talents last one run. The whole feature is unlocked by the Auxiliary Power talent.
public static class ShipSystems
{
  public const string Name = "Ship Systems";
  public const string NavigationLabel = "Systems";
  public const string PointName = "power cell";
  public const string PointsName = "power cells";
  public const string UnlockTalent = "SSU1";
  public const int MaxEquipped = 4;

  // Cells spent in a system's earlier rows that each row requires.
  public static readonly int[] RowRequirements = [0, 1, 4, 7, 10, 14, 19];

  public sealed record Tab(string Name, string Root, Color Accent, string Description, string[][] Rows);

  // Rows hold three columns; "" leaves a gap. New systems only need a tab here and their
  // nodes in upgrades_abilities_buttons.json.
  public static readonly Tab[] Tabs =
  [
    new("Drone Swarm", "Drones1", new Color(123, 255, 248),
      "Launches a swarm of temporary drones that collect gems and fly them home.",
      [
        ["", "Drones1", ""],
        ["DroneSpeed1", "IDF1", "DroneCollectionRange1"],
        ["DroneAfterburners1", "DroneR1", "DroneFinalSweep1"],
        ["IDC1", "DroneCapacity1", "DSE1"],
        ["DronesCooldown1", "DroneDeliveryValue1", ""],
        ["DroneRelay1", "DroneOvercharge1", "DroneLightning1"],
        ["", "", "DroneLightningCount1"],
      ]),
    new("Graviton Cascade", "CM1", new Color(170, 150, 255),
      "Chains of gravity latch onto distant gems and pull them into the home base.",
      [
        ["", "CM1", ""],
        ["CMNC1", "CMC1", "CMRC1"],
        ["CMCR1", "CMA1", "CMConstellation1"],
        ["CMReach1", "CMAC1", "CMCapacity1"],
        ["CMVelocity1", "", "CMNetReach1"],
        ["CMAvalanche1", "CMSC1", "CMHorizon1"],
      ]),
    new("Genesis Pulse", "GS1", new Color(255, 215, 120),
      "Seeds rings of fresh gems around the home base.",
      [
        ["", "GS1", ""],
        ["GSNG1", "GSCD1", "GSRV1"],
        ["GSSpiral1", "GSBloom1", "GSMidas1"],
        ["GSNOR1", "GSSeeds1", "GSMidasReach1"],
        ["GSRR1", "", "GSMidasCapacity1"],
        ["GSCosmic1", "GSWorldseed1", "GSGoldenAge1"],
      ]),
    new("Core Drill", "CoreDrill1", new Color(255, 135, 85),
      "Bores out deep gems and exposes the planet's core so every weapon hits harder while the drill is active.",
      [
        ["", "CoreDrill1", ""],
        ["CDR1", "CDD1", "CDC1"],
        ["CDF1", "CDP1", "CDS1"],
        ["CDFC1", "CDT1", "CDRD1"],
        ["CDFL1", "CDRO1", "CDL1"],
        ["CDRupture1", "CDTap1", "CDHollow1"],
      ]),
    // Replaces Drone Swarm while the Kamikaze Drones talent is owned (IsTabAvailable).
    new("Kamikaze Wing", "KW1", new Color(255, 95, 70),
      "Launches a wing of bomber drones that dive into the planet and detonate.",
      [
        ["", "KW1", ""],
        ["KWS1", "KWD1", "KWC1"],
        ["KWB1", "KWX1", "KWR1"],
        ["KWBC1", "KWXC1", "KWRC1"],
        ["KWDP1", "KWXP1", "KWSP1"],
        ["KWFire1", "KWNuke1", "KWHive1"],
      ]),
  ];

  public const int DroneSwarmTab = 0;
  public const int KamikazeWingTab = 4;

  // Kamikaze Drones turns the drone system into bombers: the Kamikaze Wing takes Drone
  // Swarm's place, and the other one cannot be learned. Talents only change between
  // runs, when every system is reset anyway.
  public static bool IsTabAvailable(int tab) => IsTabAvailable(tab, PrestigeTalentEffects.KamikazeDrones);

  public static bool IsTabAvailable(int tab, bool kamikazeDrones) => tab switch
  {
    DroneSwarmTab => !kamikazeDrones,
    KamikazeWingTab => kamikazeDrones,
    _ => tab >= 0 && tab < Tabs.Length,
  };

  // The drone system's core talent, whichever form it takes.
  public static string DroneSystemRoot(bool kamikazeDrones) => Tabs[kamikazeDrones ? KamikazeWingTab : DroneSwarmTab].Root;

  // The tabs the panel shows, in order: the Kamikaze Wing in Drone Swarm's slot.
  public static int[] VisibleTabs => Enumerable.Range(0, Tabs.Length)
    .Where(tab => tab != KamikazeWingTab)
    .Select(tab => tab == DroneSwarmTab && !IsTabAvailable(tab) ? KamikazeWingTab : tab)
    .ToArray();

  // A tab that can be shown: the replacement if it is the system that was swapped out.
  public static int Resolve(int tab)
  {
    tab = Math.Clamp(tab, 0, Tabs.Length - 1);
    if (IsTabAvailable(tab)) return tab;
    return tab == DroneSwarmTab ? KamikazeWingTab : tab == KamikazeWingTab ? DroneSwarmTab : 0;
  }

  // Tabs sit side by side in tree space; the camera shows one at a time.
  public const int TabStride = 5000;
  public static readonly Rectangle Panel = new(420, 156, 3000, 1740);
  public static readonly Rectangle Readout = new(2220, 330, 1160, 1530);
  public const int TreeCenterX = 1310;
  public const int ColumnSpacing = 400;
  public const int FirstRowY = 470;
  public const int RowSpacing = 200;
  public const int RowLabelX = 480;

  private static readonly Dictionary<string, (int Tab, int Row, int Column)> positions = Tabs
    .SelectMany((tab, index) => tab.Rows.SelectMany((row, r) => row
      .Select((id, column) => (id, Value: (index, r, column)))))
    .Where(node => node.id != "")
    .ToDictionary(node => node.id, node => node.Value);

  public static bool Online => UpgradeManager.Instance?.UGM.ShipSystemsUnlocked == true;

  public static bool IsInTree(string id) => positions.ContainsKey(id);

  public static (int Tab, int Row, int Column)? Locate(string id)
    => positions.TryGetValue(id, out var position) ? position : null;

  public static int TabOf(string id) => positions.TryGetValue(id, out var position) ? position.Tab : -1;

  public static bool IsCapstone(UpgradeButton button) => Locate(button.Data.ShortName) is { Row: >= 5 }
    && button.Data.UpgradeDefinition.Type == "bool";

  public static Vector2 NodeCenter(int row, int column)
    => new(TreeCenterX + (column - 1) * ColumnSpacing, FirstRowY + row * RowSpacing);

  public static float RowCenterY(int row) => FirstRowY + row * RowSpacing;

  public static void ApplyLayout(Dictionary<string, UpgradeButton> buttons)
  {
    foreach (var (id, button) in buttons)
    {
      if (Locate(id) is not var (tab, row, column))
      {
        button.Data.PosX = -10_000;
        button.Data.PosY = -10_000;
        continue;
      }
      bool unlock = button.Data.UpgradeDefinition.Type == "bool";
      button.Data.ButtonSizeScale = row == 0 ? 1.9f : IsCapstone(button) ? 1.95f : unlock ? 1.75f : 1.6f;
      int size = (int)Math.Round(50 * button.Data.ButtonSizeScale);
      var center = NodeCenter(row, column);
      button.Data.PosX = tab * TabStride + (int)center.X - size / 2;
      button.Data.PosY = (int)center.Y - size / 2;
    }
  }

  // Rules read levels through a lookup so presets can plan builds without live buttons.
  private static Func<string, int> LiveLevels(Dictionary<string, UpgradeButton> buttons)
    => id => buttons.TryGetValue(id, out var button) ? button.CurrentLevel : 0;

  // Links leave a system's core through a bus between the first two rows, so no link
  // crosses a node or its label. Joint midpoints are offset by the start node's half size.
  public static void RouteLinks(Dictionary<string, UpgradeJoint> joints)
  {
    float busY = (RowCenterY(0) + RowCenterY(1)) / 2f;
    foreach (var joint in joints.Values)
    {
      var start = joint.StartButton.Data;
      var end = joint.EndButton.Data;
      if (Locate(start.ShortName) is not { Row: 0 } || Locate(end.ShortName) is not var (_, _, column)) continue;
      joint.MidwayPoints.Clear();
      if (column == 1) continue;
      float half = 25f * start.ButtonSizeScale;
      float startX = start.PosX + half, endX = end.PosX + 25f * end.ButtonSizeScale;
      joint.MidwayPoints.Add(new Vector2(startX - half, busY - half));
      joint.MidwayPoints.Add(new Vector2(endX - half, busY - half));
    }
  }

  public static ulong Spent(Dictionary<string, UpgradeButton> buttons, int tab, int beforeRow = int.MaxValue)
    => Spent(buttons, LiveLevels(buttons), tab, beforeRow);

  public static ulong Spent(Dictionary<string, UpgradeButton> buttons, Func<string, int> levelOf,
    int tab, int beforeRow = int.MaxValue)
  {
    ulong total = 0;
    foreach (var (id, button) in buttons)
    {
      if (Locate(id) is not var (nodeTab, row, _) || nodeTab != tab || row >= beforeRow) continue;
      foreach (var level in button.Data.LevelInfo.Take(levelOf(id)))
        total = PrestigeProgression.AddSaturating(total, level.Cost);
    }
    return total;
  }

  public static int RowRequirement(int row) => RowRequirements[Math.Min(row, RowRequirements.Length - 1)];

  public static bool IsRowOpen(Dictionary<string, UpgradeButton> buttons, int tab, int row)
    => Spent(buttons, tab, row) >= (ulong)RowRequirement(row);

  // Whether the next rank can be learned: the node above it and the row requirement.
  public static bool CanLearn(Dictionary<string, UpgradeButton> buttons, string id)
    => CanLearn(buttons, id, LiveLevels(buttons));

  // Presets plan saves with their own talents, so they pass whether Kamikaze Drones is owned.
  public static bool CanLearn(Dictionary<string, UpgradeButton> buttons, string id, Func<string, int> levelOf,
    bool? kamikazeDrones = null)
  {
    if (Locate(id) is not var (tab, row, _) || !IsTabAvailable(tab, kamikazeDrones ?? PrestigeTalentEffects.KamikazeDrones)
      || !buttons.TryGetValue(id, out var button)) return false;
    bool prerequisite = string.IsNullOrEmpty(button.Data.BlockedBy) || levelOf(button.Data.BlockedBy) > 0;
    return prerequisite && Spent(buttons, levelOf, tab, row) >= (ulong)RowRequirement(row);
  }

  // A rank can be refunded unless another learned talent still depends on it.
  public static bool CanRefund(Dictionary<string, UpgradeButton> buttons, string id)
  {
    int tab = TabOf(id);
    if (tab < 0 || !buttons.TryGetValue(id, out var button) || button.CurrentLevel == 0) return false;
    button.CurrentLevel--;
    try
    {
      return buttons.All(pair => pair.Value.CurrentLevel == 0 || TabOf(pair.Key) != tab
        || CanLearn(buttons, pair.Key, LiveLevels(buttons)));
    }
    finally
    {
      button.CurrentLevel++;
    }
  }
}
