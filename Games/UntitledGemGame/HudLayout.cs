using JapeFramework;
using Microsoft.Xna.Framework;

namespace UntitledGemGame;

// All HUD drawing and hit targets use the same virtual coordinates as Gum.
internal static class HudLayout
{
  public const int SlotPadding = 16;
  public const int MainBarHeight = 100 + SlotPadding * 2;
  public const int ManualBarHeight = 104;
  public const int Height = MainBarHeight + ManualBarHeight;
  public const int Left = 24;
  public const int ProgressPanelHeight = 108;
  public const int ProgressPanelPadding = 18;
  public const int ProgressTitleTop = 6;
  public const int ProgressBarTop = 49;
  public const int ProgressStatusTop = 66;
  // Keep the automatic loadout between navigation and bulk upgrade actions.
  public static float AbilityPointSpace => AbilitySlotsCenterX - Width / 2f;
  public static readonly Color PanelColor = new Color(15, 13, 27, 255);
  public static readonly Color BorderColor = new Color(100, 78, 125, 180);
  public static readonly Color MutedTextColor = new Color(210, 203, 222);
  public static readonly Color ButtonColor = new Color(25, 22, 39);
  public static readonly Color ButtonHoverColor = new Color(39, 33, 55);
  public static readonly Color ButtonBorderColor = new Color(86, 70, 106);
  public static readonly Color ButtonTextColor = new Color(225, 218, 233);
  public static readonly Color AbilityAccent = new Color(145, 210, 255);
  public static readonly Color UpgradeAccent = new Color(255, 215, 150);
  public static int Width => BaseGame.BoxingViewportAdapterGui.VirtualWidth;
  public static float AbilitySlotsCenterX => (NavigationButton(SignalsTab).Right + BulkUpgradeButton(0).Left) / 2f;
  public static int Bottom => BaseGame.BoxingViewportAdapterGui.VirtualHeight;
  public static int Top => Bottom - Height;
  public static int ManualTop => Bottom - ManualBarHeight;
  // Menus must stop above both rows of the HUD, rather than the lower row alone.
  public static int ContentBottom => Top;
  public static int ResourceWidth => Width / 12;
  public static int ResourcesRight => Left + ResourceWidth * 2 + 12;
  public static Rectangle ResourcePanel(int index) => new Rectangle(
    Left + index % 2 * (ResourceWidth + 12), Top + 8 + index / 2 * 112,
    ResourceWidth, 108);
  public static Rectangle ManualAbilityButton(int index)
  {
    int left = ResourcesRight + 24;
    int width = (Width - left - Left - 16 * 4) / 5;
    return new Rectangle(left + index * (width + 16), ManualTop + 8, width, 88);
  }
  // Borders are drawn into the virtual HUD texture before it is downscaled.
  // Keep two display pixels of coverage so fractional sampling cannot skip them.
  public static int ButtonBorderThickness
  {
    get
    {
      var scale = BaseGame.BoxingViewportAdapterGui.GetScaleMatrix();
      float pixelsPerUnit = System.Math.Max(0.0001f,
        System.Math.Min(System.Math.Abs(scale.M11), System.Math.Abs(scale.M22)));
      return (int)System.Math.Ceiling(2f / pixelsPerUnit);
    }
  }
  // Navigation tabs, in the order their features arrive: Talents from the first extraction,
  // then Systems, Shipyard and Signals from the talent tiers, so the bar fills without gaps.
  public const int UpgradesTab = 0, TalentsTab = 1, SystemsTab = 2, ShipyardTab = 3, SignalsTab = 4;
  private static int NavigationWidth => Width * 58 / 1000;
  private static int ProgressWidth => Width * 105 / 1000;
  public static Rectangle NavigationButton(int index) => new Rectangle(
    PrestigePanel.Right + 16 + index * (NavigationWidth + 12),
    Top + 12, NavigationWidth, ProgressPanelHeight);
  public static Rectangle BulkUpgradeButton(int index) => new Rectangle(
    Width - Left - 230 * 2 - 16 + index * 246, Top + 12, 230, ProgressPanelHeight);
  public static Rectangle PrestigePanel => new Rectangle(
    ResourcesRight + 24, Top + 12, ProgressWidth, ProgressPanelHeight);
  // The Damage button takes Spend All's slot at the bar's right end, which is free
  // whenever the upgrade tree is closed; the Damage panel opens above it.
  public const int DamagePanelWidth = 660;
  public const int DamageRowHeight = 46;
  public static Rectangle DamageButton => BulkUpgradeButton(1);
}
