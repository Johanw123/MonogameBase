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
    new(2, Loc.N("Hardpoint III"), Loc.N("Lower Starboard"), new Vector2(15f, 9f)),
    new(3, Loc.N("Hardpoint IV"), Loc.N("Aft Wing Bay"), new Vector2(13f, 30f)),
  ];

  public static int DefaultSlotForWeapon(MainShipWeapon weapon) => weapon switch
  {
    MainShipWeapon.Laser => 0,
    MainShipWeapon.Harpoon => 1,
    MainShipWeapon.Rockets => 2,
    MainShipWeapon.Railgun => 3,
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
    string Role,
    string Description,
    Color Accent,
    string[][] Rows);

  public static readonly Tab[] Tabs =
  [
    new(
      MainShipWeapon.Cannon,
      Loc.N("Plasma Cannon"),
      "AC1",
      Loc.N("Primary Kinetic Battery"),
      Loc.N("Fitted in the bow mount by default. Fires manually on click, or continuously with Plasma Repeater."),
      new Color(95, 220, 255),
      [
        ["", "AC1", ""],
        ["CFR1", "CRB1", "CFP1"],
        ["CFR2", "CFP2", "CSS1"],
        ["CCR1", "GTC1", ""],
        ["CCS1", "", ""],
      ]),
    new(
      MainShipWeapon.Laser,
      Loc.N("Mining Laser"),
      "LZ1",
      Loc.N("Continuous Thermal Beam"),
      Loc.N("Locks a steady cutting beam onto the planet, streaming gems continuously and carving magma scars."),
      new Color(255, 110, 90),
      [
        ["", "LZ1", ""],
        ["LZR1", "", "LZP1"],
        ["LZM1", "", "LZT1"],
        ["LZH1", "", "LZD1"],
        ["ALC1", "", "LZQ1"],
      ]),
    new(
      MainShipWeapon.Harpoon,
      Loc.N("Arc Harpoon"),
      "AH1",
      Loc.N("Tethered Voltaic Spike"),
      Loc.N("Anchors into the planet crust and sends high-voltage pulses down the cable to crack gem seams."),
      new Color(110, 215, 255),
      [
        ["", "AH1", ""],
        ["AHR1", "", "AHP1"],
        ["AHF1", "", "AHB1"],
        ["AHDX1", "THP1", "AHD1"],
        ["TC1", "", "AHW1"],
      ]),
    new(
      MainShipWeapon.Rockets,
      Loc.N("Rocket Pods"),
      "RP1",
      Loc.N("Salvo Ordinance Launcher"),
      Loc.N("Fires guided rocket salvos that arc around the planet and blast clusters of gems loose."),
      new Color(255, 175, 75),
      [
        ["", "RP1", ""],
        ["RPR1", "", "RPP1"],
        ["ROS1", "", "RPC1"],
        ["", "RSW1", "RCW1"],
        ["", "", "IW1"],
      ]),
    new(
      MainShipWeapon.Railgun,
      Loc.N("Railgun"),
      "RG1",
      Loc.N("Heavy Siege Accelerator"),
      Loc.N("Charges a massive hypervelocity slug that shatters deep crust layers into rich gem fragments."),
      new Color(185, 140, 255),
      [
        ["", "RG1", ""],
        ["RGR1", "", "RGP1"],
        ["RCN1", "", "RGF1"],
        ["", "BDS1", "BTS1"],
        ["", "", "RCB1"],
      ]),
  ];

  public static Tab ForWeapon(MainShipWeapon weapon) => Tabs[(int)weapon];

  public static MainShipWeapon? BlueprintWeapon(string shortName) => shortName switch
  {
    "LZ1" => MainShipWeapon.Laser,
    "AH1" => MainShipWeapon.Harpoon,
    "RP1" => MainShipWeapon.Rockets,
    "RG1" => MainShipWeapon.Railgun,
    _ => null,
  };

  // Tabs sit side by side in tree space; the Weaponry layer offset selects which weapon's tree is shown.
  public const int TabStride = 5000;
  public static Rectangle Panel => PanelNavigationLayout.Panel;
  public static Rectangle BlueprintsColumn => new(Panel.X + 40, 316, 400, Panel.Bottom - 352);
  public static Rectangle ShowcaseColumn => new(BlueprintsColumn.Right + 40, 316, 1860, Panel.Bottom - 352);
  public static Rectangle TreeColumn => new(ShowcaseColumn.Right + 40, 316, Panel.Right - 40 - (ShowcaseColumn.Right + 40), Panel.Bottom - 352);

  public static int TreeCenterX => TreeColumn.Center.X;
  public const int ColumnSpacing = 290;
  public const int FirstRowY = 730;
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

  // Routes non-vertical links through horizontal buses between rows so no link crosses a node or label.
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
      if (startCol == endCol)
        continue;

      float half = 25f * start.ButtonSizeScale;
      float startX = start.PosX + half;
      float endX = end.PosX + 25f * end.ButtonSizeScale;
      float busY = startRow == endRow
        ? RowCenterY(startRow) + RowSpacing * 0.42f
        : (RowCenterY(startRow) + RowCenterY(endRow)) * 0.5f + RowSpacing * 0.04f;
      joint.MidwayPoints.Add(new Vector2(startX - half, busY - half));
      joint.MidwayPoints.Add(new Vector2(endX - half, busY - half));
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

    // Prefer first empty slot; if empty at the weapon's natural slot, use that first.
    int preferred = Weaponry.DefaultSlotForWeapon(weapon);
    if (preferred >= 0 && preferred < UnlockedHardpoints && Slots[preferred] == null)
    {
      Slots[preferred] = weapon;
      Unequipped.Remove(weapon);
      return true;
    }

    for (int i = 0; i < UnlockedHardpoints; i++)
    {
      if (Slots[i] == null)
      {
        Slots[i] = weapon;
        Unequipped.Remove(weapon);
        return true;
      }
    }
    return false;
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
    copy.UnlockedHardpoints = UnlockedHardpoints;
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
