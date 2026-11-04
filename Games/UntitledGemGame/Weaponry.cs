using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace UntitledGemGame;

// Homebase weapon blueprints, hull hardpoint slots, and per-weapon upgrade grids.
// The Primary Cannon sits in the fixed forward bow mount and fires manually (or automatically
// once Plasma Repeater is purchased). The 4 secondary weapon blueprints (Mining Laser,
// Arc Harpoon, Rocket Pods, Railgun) can be unlocked in any order and fitted into any of the
// 4 customizable hardpoint slots on the homebase cruiser.
public static class Weaponry
{
  public static string Name => Loc.T("Weaponry");
  public static string NavigationLabel => Loc.T("Weaponry");

  public const int MaxCustomSlots = 4;

  public sealed record HardpointSlot(int Index, string Name, string Subtitle, Vector2 HullMount);

  public static string PrimarySlotName => Loc.N("Bow Twin Mount");
  public static string PrimarySlotSubtitle => Loc.N("Primary Manual / Auto Mount");
  public static readonly Vector2 PrimaryBowMountLeft = new(-17f, -30f);
  public static readonly Vector2 PrimaryBowMountRight = new(17f, -30f);
  public static readonly Vector2 PrimaryHullMarker = new(0f, -30f);

  // Local coordinates in homebase ship space (matching GameScreen.Turrets.cs).
  // Note: (-14.5, 1.5) is the physical ship terminal on the port deck.
  public static readonly HardpointSlot[] HardpointSlots =
  [
    new(0, Loc.N("Hardpoint I"), Loc.N("Forward Spine"), new Vector2(0f, -17f)),
    new(1, Loc.N("Hardpoint II"), Loc.N("Upper Starboard"), new Vector2(15f, -6f)),
    new(2, Loc.N("Hardpoint III"), Loc.N("Aft Port Wing"), new Vector2(-13f, 30f)),
    new(3, Loc.N("Hardpoint IV"), Loc.N("Aft Wing Bay"), new Vector2(13f, 30f)),
  ];

  public static int DefaultSlotForWeapon(MainShipWeapon weapon) => weapon switch
  {
    MainShipWeapon.Laser => 0,
    MainShipWeapon.Harpoon => 1,
    MainShipWeapon.Rockets => 2,
    MainShipWeapon.Railgun => 3,
    MainShipWeapon.Flak => -1,
    _ => -1,
  };

  public static Vector2 DefaultMount(MainShipWeapon weapon)
  {
    int slot = DefaultSlotForWeapon(weapon);
    return slot >= 0 && slot < HardpointSlots.Length ? HardpointSlots[slot].HullMount : Vector2.Zero;
  }

  public sealed record Tab(
    MainShipWeapon Weapon,
    string Name,
    string Root,
    string Description,
    string[][] Rows)
  {
    public Color Accent => Weaponry.Accent;
  }

  public static readonly Color Accent = new(95, 220, 255);

  public static readonly Tab[] Tabs =
  [
    new(
      MainShipWeapon.Cannon,
      Loc.N("Plasma Cannon"),
      "AC1",
      Loc.N("Attached to the bow mount by default. Fires manually on click, or continuously with Plasma Repeater."),
      [
        ["", "AC1", ""],
        ["CFR1", "CRB1", "CFP1"],
        ["CCR1", "CRN1", "CSS1"],
        ["CCP1", "CRS1", "CBR1"],
        ["CCD1", "GTC1", "CAF1"],
        ["CCS1", "", "CSE1"],
      ]),
    new(
      MainShipWeapon.Laser,
      Loc.N("Mining Laser"),
      "LZ1",
      Loc.N("Locks a steady cutting beam onto the planet, streaming gems continuously and carving magma scars."),
      [
        ["", "LZ1", ""],
        ["LZR1", "LZM1", "LZP1"],
        ["LZH1", "LZB1", "LZT1"],
        ["LZS1", "LMR1", "LZV1"],
        ["ALC1", "", "LZD1"],
        ["", "", "LZQ1"],
      ]),
    new(
      MainShipWeapon.Harpoon,
      Loc.N("Arc Harpoon"),
      "AH1",
      Loc.N("Anchors into the planet crust and sends high-voltage pulses down the cable to crack gem seams."),
      [
        ["", "AH1", ""],
        ["AHR1", "AHDX1", "AHP1"],
        ["AHF1", "THP1", "AHB1"],
        ["AHFC1", "", "AHD1"],
        ["TC1", "", "AHW1"],
        ["", "", "AHT1"],
        ["", "", "AHE1"],
      ]),
    new(
      MainShipWeapon.Rockets,
      Loc.N("Rocket Pods"),
      "RP1",
      Loc.N("Fires guided rocket salvos that arc around the planet and blast clusters of gems loose."),
      [
        ["", "RP1", ""],
        ["RPR1", "RPC1", "RPP1"],
        ["ROS1", "RSW1", "RCW1"],
        ["ROB1", "", "RCS1"],
        ["ROBZ1", "", "IW1"],
      ]),
    new(
      MainShipWeapon.Railgun,
      Loc.N("Railgun"),
      "RG1",
      Loc.N("Charges a massive hypervelocity slug that shatters deep crust layers into rich gem fragments."),
      [
        ["", "RG1", ""],
        ["RGR1", "RGF1", "RGP1"],
        ["RCN1", "RGM1", "BTS1"],
        ["RGC1", "BDS1", "RGS1"],
        ["", "", "RCB1"],
      ]),
    new(
      MainShipWeapon.Flak,
      Loc.N("Flak Battery"),
      "FK1",
      Loc.N("Fires wide shotgun bursts of shrapnel pellets across the planet surface to knock gems loose."),
      [
        ["", "FK1", ""],
        ["FKR1", "FKC1", "FKP1"],
        ["FKW1", "FKS1", "FKF1"],
      ]),
  ];

  public static Tab ForWeapon(MainShipWeapon weapon) => Tabs[(int)weapon];

  public static MainShipWeapon? BlueprintWeapon(string shortName) => shortName switch
  {
    "LZ1" => MainShipWeapon.Laser,
    "AH1" => MainShipWeapon.Harpoon,
    "RP1" => MainShipWeapon.Rockets,
    "RG1" => MainShipWeapon.Railgun,
    "FK1" => MainShipWeapon.Flak,
    _ => null,
  };

  // Weapons that are implemented but held back from this version (still being balanced).
  // Outside dev builds their tab shows in Weaponry with no unlock button, every node reads
  // "Not available in this version" and the blueprint never counts as unlocked. Dev builds
  // (Demo.IsDev) can unlock and use them as normal. Remove a weapon here to release it.
  public static readonly HashSet<MainShipWeapon> UnreleasedWeapons = [MainShipWeapon.Flak];

  // Shipped in this version (what players get; the progression simulator models this).
  public static bool IsReleased(MainShipWeapon weapon) => !UnreleasedWeapons.Contains(weapon);

  // Usable in this build: released weapons, plus unreleased ones in dev builds.
  public static bool IsAvailable(MainShipWeapon weapon) => IsReleased(weapon) || Demo.IsDev;

  // True for any node in an unreleased weapon's tree.
  public static bool IsUnreleasedNode(string shortName)
  {
    int tab = TabOf(shortName);
    return tab >= 0 && !IsReleased(Tabs[tab].Weapon);
  }

  // True for any node this build locks away (an unreleased weapon outside dev builds).
  public static bool IsUnavailableNode(string shortName)
  {
    int tab = TabOf(shortName);
    return tab >= 0 && !IsAvailable(Tabs[tab].Weapon);
  }

  // Tabs sit side by side in tree space; the Weaponry layer offset selects which weapon's tree is shown.
  public const int TabStride = 5000;
  public static Rectangle Panel => PanelNavigationLayout.Panel;
  // Medallions, heading and module inventory share the left of the showcase, like the Shipyard.
  public static Rectangle ShowcaseColumn => new(Panel.X + 40, 316, 2300, Panel.Bottom - 352);
  public static Rectangle TreeColumn => new(ShowcaseColumn.Right + 40, 316, Panel.Right - 40 - (ShowcaseColumn.Right + 40), Panel.Bottom - 352);

  public static int TreeCenterX => TreeColumn.Center.X;
  public const int ColumnSpacing = 290;
  public const int FirstRowY = 580;
  public const int RowSpacing = 205;

  private static readonly Dictionary<string, (int Tab, int Row, int Column)> positions = Tabs
    .SelectMany((tab, index) => tab.Rows.SelectMany((row, r) => row
      .Select((id, column) => (id, Value: (index, r, column)))))
    .Where(node => node.id != "")
    .ToDictionary(node => node.id, node => node.Value);

  public static bool IsInTree(string id) => positions.ContainsKey(id);

  public static (int Tab, int Row, int Column)? Locate(string id)
    => positions.TryGetValue(id, out var position) ? position : null;

  public static int TabOf(string id) => positions.TryGetValue(id, out var position) ? position.Tab : -1;

  public static Vector2 NodeCenter(int row, int column)
    => new(TreeCenterX + (column - 1) * ColumnSpacing, FirstRowY + row * RowSpacing);

  public static float RowCenterY(int row) => FirstRowY + row * RowSpacing;

  public static void ApplyLayout(Dictionary<string, UpgradeButton> buttons)
  {
    foreach (var (id, button) in buttons)
    {
      if (Locate(id) is not var (tab, row, column))
        continue;

      bool isRoot = row == 0;
      bool isCoreShard = button.Data.UpgradeDefinition.Currency == CoreShards.Currency;
      bool isBool = button.Data.UpgradeDefinition.Type == "bool";
      button.Data.ButtonSizeScale = isRoot ? 1.85f : isCoreShard ? 1.85f : isBool ? 1.68f : 1.55f;
      int size = (int)Math.Round(50 * button.Data.ButtonSizeScale);
      var center = NodeCenter(row, column);
      button.Data.PosX = tab * TabStride + (int)center.X - size / 2;
      button.Data.PosY = (int)center.Y - size / 2;
    }
  }

  // Route branches between rows and take skipped tiers around intervening nodes.
  public static void RouteLinks(Dictionary<string, UpgradeJoint> joints)
  {
    foreach (var joint in joints.Values)
    {
      var start = joint.StartButton.Data;
      var end = joint.EndButton.Data;
      if (Locate(start.ShortName) is not var (startTab, startRow, startCol)
        || Locate(end.ShortName) is not var (endTab, endRow, endCol)
        || startTab != endTab)
      {
        continue;
      }

      joint.MidwayPoints.Clear();
      // Adjacent upgrades in a branch connect straight down. Longer links
      // leave the column so intervening nodes never appear to be prerequisites.
      if (startCol == endCol && endRow == startRow + 1)
        continue;

      float half = 25f * start.ButtonSizeScale;
      float startX = start.PosX + half;
      float endX = end.PosX + 25f * end.ButtonSizeScale;
      float exitY = RowCenterY(startRow) + RowSpacing * 0.5f;
      float entryY = RowCenterY(endRow) - RowSpacing * 0.5f;
      void Waypoint(float x, float y)
        => joint.MidwayPoints.Add(new Vector2(x - half, y - half));

      Waypoint(startX, exitY);
      if (endRow > startRow + 1)
      {
        // Use the outside of the originating branch; the gutter stays clear
        // of every node even when the destination is in another column.
        float gutterX = startX + (startCol == 0 ? -1 : 1) * ColumnSpacing * 0.38f;
        Waypoint(gutterX, exitY);
        Waypoint(gutterX, entryY);
      }
      Waypoint(endX, entryY);
    }
  }
}

public sealed partial class WeaponryLoadout
{
  public MainShipWeapon?[] Slots { get; set; } = new MainShipWeapon?[Weaponry.MaxCustomSlots];
  public HashSet<MainShipWeapon> Unequipped { get; set; } = new();

  public int FindSlot(MainShipWeapon weapon)
  {
    if (weapon == MainShipWeapon.Cannon) return -1;
    for (int i = 0; i < Slots.Length && i < Weaponry.MaxCustomSlots; i++)
    {
      if (Slots[i] == weapon)
        return i;
    }
    return -1;
  }

  public bool IsFitted(MainShipWeapon weapon)
    => weapon == MainShipWeapon.Cannon || FindSlot(weapon) >= 0;

  public Vector2 GetMount(MainShipWeapon weapon)
  {
    int slot = FindSlot(weapon);
    if (slot >= 0 && slot < Weaponry.HardpointSlots.Length)
      return Weaponry.HardpointSlots[slot].HullMount;
    return Weaponry.DefaultMount(weapon);
  }

  public bool TryEquip(MainShipWeapon weapon, int slotIndex)
  {
    if (weapon == MainShipWeapon.Cannon || slotIndex < 0 || slotIndex >= UnlockedHardpoints)
      return false;

    EnsureSlotsLength();
    int previousSlot = FindSlot(weapon);
    var displaced = Slots[slotIndex];

    if (previousSlot == slotIndex)
    {
      Unequipped.Remove(weapon);
      return true;
    }

    if (previousSlot >= 0)
      Slots[previousSlot] = displaced;

    Slots[slotIndex] = weapon;
    Unequipped.Remove(weapon);
    if (displaced is { } other && other != weapon)
      Unequipped.Remove(other);
    return true;
  }

  public bool TryMoveSlot(int fromSlot, int toSlot)
  {
    EnsureSlotsLength();
    if (fromSlot < 0 || fromSlot >= Weaponry.MaxCustomSlots || toSlot < 0 || toSlot >= Weaponry.MaxCustomSlots)
      return false;
    if (Slots[fromSlot] is not { } weapon)
      return false;
    return TryEquip(weapon, toSlot);
  }

  public bool TryUnequip(MainShipWeapon weapon)
  {
    if (weapon == MainShipWeapon.Cannon)
      return false;
    EnsureSlotsLength();
    int slot = FindSlot(weapon);
    if (slot >= 0)
      Slots[slot] = null;
    Unequipped.Add(weapon);
    return slot >= 0;
  }

  public bool TryUnequipSlot(int slotIndex)
  {
    EnsureSlotsLength();
    if (slotIndex < 0 || slotIndex >= Weaponry.MaxCustomSlots || Slots[slotIndex] is not { } weapon)
      return false;
    Slots[slotIndex] = null;
    Unequipped.Add(weapon);
    return true;
  }

  public bool TryAutoFit(MainShipWeapon weapon)
  {
    if (weapon == MainShipWeapon.Cannon)
      return false;
    EnsureSlotsLength();
    if (FindSlot(weapon) >= 0)
    {
      Unequipped.Remove(weapon);
      return true;
    }

    int slot = OpenSlotFor(weapon);
    if (slot < 0)
      return false;
    Slots[slot] = weapon;
    Unequipped.Remove(weapon);
    return true;
  }

  // The weapon's natural hardpoint if it is empty, otherwise the first empty one, or -1.
  public int OpenSlotFor(MainShipWeapon weapon)
  {
    EnsureSlotsLength();
    int preferred = Weaponry.DefaultSlotForWeapon(weapon);
    if (preferred >= 0 && preferred < UnlockedHardpoints && Slots[preferred] == null)
      return preferred;
    for (int i = 0; i < UnlockedHardpoints; i++)
    {
      if (Slots[i] == null)
        return i;
    }
    return -1;
  }

  // The first bought weapon that could be attached but is not: it fires only from a hardpoint,
  // though its upgrades can still be bought. One the player took off (Unequipped) stays their choice.
  public MainShipWeapon? AwaitingHardpoint(Func<MainShipWeapon, bool> isUnlocked)
  {
    EnsureSlotsLength();
    foreach (var weapon in MainShipWeapons.All)
    {
      if (weapon != MainShipWeapon.Cannon && isUnlocked(weapon) && !IsFitted(weapon)
        && !Unequipped.Contains(weapon) && OpenSlotFor(weapon) >= 0)
        return weapon;
    }
    return null;
  }

  public void SyncWithUnlockedBlueprints(Func<MainShipWeapon, bool> isUnlocked)
  {
    EnsureSlotsLength();
    var seen = new HashSet<MainShipWeapon>();
    for (int i = 0; i < Weaponry.MaxCustomSlots; i++)
    {
      if (Slots[i] is not { } weapon)
        continue;
      if (weapon == MainShipWeapon.Cannon || !isUnlocked(weapon) || !seen.Add(weapon))
        Slots[i] = null;
    }

    Unequipped.RemoveWhere(w => w == MainShipWeapon.Cannon || !isUnlocked(w));
  }

  public WeaponryLoadout Clone()
  {
    EnsureSlotsLength();
    var copy = new WeaponryLoadout();
    Array.Copy(Slots, copy.Slots, Weaponry.MaxCustomSlots);
    foreach (var weapon in Unequipped)
      copy.Unequipped.Add(weapon);
    copy.Supports = (WeaponSupportModule[])Supports.Clone();
    copy.SupportInventory = (int[])SupportInventory.Clone();
    return copy;
  }

  public void ResetRun()
  {
    Slots = new MainShipWeapon?[Weaponry.MaxCustomSlots];
    Unequipped.Clear();
    Array.Clear(Supports);
    Array.Clear(SupportInventory);
  }

  private void EnsureSlotsLength()
  {
    if (Slots == null || Slots.Length != Weaponry.MaxCustomSlots)
    {
      var resized = new MainShipWeapon?[Weaponry.MaxCustomSlots];
      if (Slots != null)
        Array.Copy(Slots, resized, Math.Min(Slots.Length, Weaponry.MaxCustomSlots));
      Slots = resized;
    }
    Unequipped ??= new HashSet<MainShipWeapon>();
  }
}
