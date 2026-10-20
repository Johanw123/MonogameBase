using JapeFramework;
using Microsoft.Xna.Framework;

namespace UntitledGemGame;

// All HUD drawing and hit targets use the same virtual coordinates as Gum.
public static class HudLayout
{
  // Geometry and input stay in virtual HUD coordinates on both platforms.
  public static Matrix RenderTransform =>
#if KNI_WEB
    Matrix.CreateScale(0.5f);
#else
    Matrix.Identity;
#endif
  public const int SlotPadding = 16;
  public const int MainBarHeight = 100 + SlotPadding * 2;
  public const int ManualBarHeight = 104;
  public const int Height = MainBarHeight + ManualBarHeight;
  public const int Left = 28;
  public const int RowHeight = 92;
  public const int RowGap = 16;
  public const int ProgressPanelHeight = RowHeight;
  public const int ProgressPanelPadding = 20;
  public const int ProgressTitleTop = 12;
  public const int ProgressBarTop = 58;
  public const int ProgressStatusTop = 50;
  // Keep the automatic loadout between navigation and bulk upgrade actions.
  public static float AbilityPointSpace => AbilitySlotsCenterX - Width / 2f;
  public static Color PanelColor => GuiSkin.PanelColor;
  public static Color BorderColor => GuiSkin.BorderColor;
  public static Color MutedTextColor => GuiSkin.MutedTextColor;
  public static Color ButtonColor => GuiSkin.ButtonColor;
  public static Color ButtonHoverColor => GuiSkin.ButtonHoverColor;
  public static Color ButtonBorderColor => GuiSkin.ButtonBorderColor;
  public static Color ButtonTextColor => GuiSkin.ButtonTextColor;
  public static Color AbilityAccent => GuiSkin.AbilityAccent;
  public static Color UpgradeAccent => GuiSkin.UpgradeAccent;
  public static int Width => BaseGame.BoxingViewportAdapterGui?.VirtualWidth ?? 3840;
  public static float AbilitySlotsCenterX => (NavigationButton(SignalsTab).Right + BulkUpgradeButton(0).Left) / 2f;
  public static int Bottom => BaseGame.BoxingViewportAdapterGui?.VirtualHeight ?? 2160;
  public static int Top => Bottom - Height;
  public static int TopRowY => Top + 18;
  public static int BottomRowY => TopRowY + RowHeight + RowGap;
  public static int ManualTop => Bottom - ManualBarHeight;
  // Menus must stop above both rows of the HUD, rather than the lower row alone.
  public static int ContentBottom => Top;
  public static int ResourceWidth => 304;
  public static int ResourcesRight => Left + ResourceWidth * 2 + 16;
  public static Rectangle ResourcePanel(int index) => new Rectangle(
    Left + index % 2 * (ResourceWidth + 16), TopRowY + index / 2 * (RowHeight + RowGap),
    ResourceWidth, RowHeight);
  public static Rectangle ManualAbilityButton(int index)
  {
    int left = ResourcesRight + 28;
    int gap = 16;
    int width = (Width - left - Left - gap * 4) / 5;
    return new Rectangle(left + index * (width + gap), BottomRowY, width, RowHeight);
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
  private static int NavigationWidth => 212;
  private static int ProgressWidth => 416;
  private const int BulkButtonWidth = 272;
  public static Rectangle NavigationButton(int index) => new Rectangle(
    PrestigePanel.Right + 20 + index * (NavigationWidth + 14),
    TopRowY, NavigationWidth, RowHeight);
  public static Rectangle BulkUpgradeButton(int index) => new Rectangle(
    Width - Left - BulkButtonWidth * 2 - 16 + index * (BulkButtonWidth + 16), TopRowY, BulkButtonWidth, RowHeight);
  public static Rectangle PrestigePanel => new Rectangle(
    ResourcesRight + 28, TopRowY, ProgressWidth, RowHeight);
  // The Damage button takes Spend All's slot at the bar's right end, which is free
  // whenever the upgrade tree is closed; the Damage panel opens above it.
  public const int DamagePanelWidth = 660;
  public const int DamageRowHeight = 46;
  public static Rectangle DamageButton => BulkUpgradeButton(1);
  // The Feedback button takes Upgrade Cheapest's slot in the HUD bar next to Damage.
  public static Rectangle FeedbackButton => BulkUpgradeButton(0);
}
