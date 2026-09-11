using System.Globalization;

namespace UntitledGemGame;

public static class UpgradeValueFormatter
{
  public static string Format(JsonUpgrade upgrade, double value, bool percentage)
  {
    if (!percentage) return value.ToString("0.##", CultureInfo.CurrentCulture);

    // These stats store percentage points, not fractional multipliers.
    if (upgrade.ShortName is "CMAC" or "GSRR")
      return $"{value:0.##}%";

    // Show the bonus/reduction relative to the unupgraded ability stat.
    if (upgrade.ShortName is "DroneSpeed" or "IDF" or "HDTD")
    {
      double baseline = double.Parse(upgrade.BaseValue, CultureInfo.InvariantCulture);
      double bonus = (value / baseline - 1) * 100;
      return bonus.ToString("+0.##;-0.##;0", CultureInfo.CurrentCulture) + "%";
    }

    return $"+{value * 100:0.##}%";
  }
}
