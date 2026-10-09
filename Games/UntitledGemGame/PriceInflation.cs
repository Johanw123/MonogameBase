using System;

namespace UntitledGemGame;

// Prices past the early game climb faster than the income they buy, so a single run levels
// off and the talents' Gem Lore (CoreExtraction) carries the economy further. Above Threshold
// a price is multiplied by price / Threshold, at most MaxMultiplier, so a player deep into the
// loops, whose gems are worth thousands of times more, can finish the tree.
// The regular tree's prices in upgrades_buttons.json were inflated this way on 2026-10-09
// (rounded to two significant digits); power cells and signal scans apply it to their curves.
public static class PriceInflation
{
  public const double Threshold = 20_000;
  public const double MaxMultiplier = 300;

  public static double Apply(double price)
    => price <= Threshold ? price : price * Math.Min(price / Threshold, MaxMultiplier);
}
