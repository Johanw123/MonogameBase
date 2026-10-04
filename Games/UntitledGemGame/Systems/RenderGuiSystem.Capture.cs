#if !KNI_WEB
using System;
using System.Linq;
using Microsoft.Xna.Framework;
using UntitledGemGame;

// Named HUD targets for capture scenes ("ui" on pointer/click/hold actions), resolved with the same
// layout rectangles the panels draw and hit-test with, so scripted clicks land on real buttons.
public partial class RenderGuiSystem
{
  internal static readonly string[] CaptureTargetNames =
    ["nav:upgrades", "nav:abilities", "nav:shipyard", "nav:signals", "discovery", "inspect", "reveal_skip",
     "reveal_continue", "reveal_shipyard", "ship:<name>", "slot:<0-3>", "module:<name>", "scan", "card:<0-2>"];

  internal Rectangle? CaptureTarget(string name)
  {
    string[] parts = name.Split(':', 2);
    string arg = parts.Length > 1 ? parts[1] : null;
    string Slug(string s) => new(s.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
    switch (parts[0])
    {
      case "nav":
        int nav = Array.IndexOf(new[] { "upgrades", "abilities", "shipyard", "signals" }, arg);
        return nav >= 0 ? HudLayout.NavigationButton(nav) : null;
      case "discovery": return DiscoveryTab;
      case "inspect": return InspectModuleButton;
      case "reveal_skip": return RevealSkip;
      case "reveal_continue": return RevealContinue;
      case "reveal_shipyard": return RevealShipyard;
      case "ship":
        int ship = int.TryParse(arg, out int number) ? number : Array.FindIndex(ShipyardNames, n => Slug(n) == Slug(arg ?? ""));
        return ship >= 0 && ship < ShipyardNames.Length ? ShipyardTab(ship) : null;
      case "slot":
        return int.TryParse(arg, out int slot) && slot >= 0 && slot < ModuleCatalog.MaxSlotsPerType ? ModuleSlot(slot) : null;
      case "module":
        var available = ModuleInventory.GetAvailableModules().ToArray();
        int tile = Array.FindIndex(available, m => Slug(ModuleCatalog.Names[(int)m]) == Slug(arg ?? ""));
        return tile >= 0 ? ModuleTile(tile) : null;
      case "scan": return SignalScanButton;
      case "card":
        return int.TryParse(arg, out int card) && card >= 0 && card < 3 ? SignalCard(card) : null;
      default: return null;
    }
  }
}
#endif
