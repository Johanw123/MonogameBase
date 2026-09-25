using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;

namespace UntitledGemGame;

public sealed class SignalChoice
{
  public int Signal { get; set; }
  public int Rarity { get; set; }
}

public sealed class SignalProgression
{
  public static int SignalCount => SignalCatalog.Definitions.Length;
  public const int RarityCount = 5;
  private static readonly double[] Bonuses = [5, 8, 12, 20, 35];
  private static readonly int[] Weights = [55, 25, 13, 6, 1];
  public long[] Counts { get; set; } = new long[SignalCount * RarityCount];
  public ulong ScansPurchased { get; set; }
  public List<SignalChoice> PendingChoices { get; set; } = new();

  [JsonIgnore] public ulong? ScanCost
  {
    get
    {
      double cost = Math.Ceiling(1000 * Math.Pow(1.25, ScansPurchased));
      return double.IsFinite(cost) && cost < (double)ulong.MaxValue ? (ulong)cost : null;
    }
  }

  public static double BonusForRarity(int rarity) => Bonuses[rarity];
  public long StackCount(int signal)
  {
    long total = 0;
    for (int r = 0; r < RarityCount; r++) total += Counts[signal * RarityCount + r];
    return total;
  }

  public double BonusPercent(int signal)
  {
    double total = 0;
    for (int r = 0; r < RarityCount; r++) total += Counts[signal * RarityCount + r] * Bonuses[r];
    return SignalCatalog.Definitions[signal].Reduction ? (1 - ReductionMultiplier((SignalKind)signal)) * 100 : total;
  }
  public float Multiplier(int signal) => (float)(1 + BonusPercent(signal) / 100);
  public float Multiplier(SignalKind signal) => Multiplier((int)signal);
  public int ScaleCount(SignalKind signal, int count)
    => (int)Math.Min(int.MaxValue, Math.Ceiling(Math.Max(0, count) * (1 + BonusPercent((int)signal) / 100)));
  public double ReductionMultiplier(SignalKind signal)
  {
    double log = 0;
    for (int r = 0; r < RarityCount; r++) log += Counts[(int)signal * RarityCount + r] * Math.Log(1 - Bonuses[r] / 100);
    return Math.Exp(log);
  }
  [JsonIgnore] public double CooldownMultiplier => ReductionMultiplier(SignalKind.AbilityCooldown);

  public bool TryScan(GameState wallet, Random random, Func<int, bool> available = null)
  {
    if (PendingChoices.Count != 0 || ScanCost is not ulong cost || wallet.CurrentRedGemCount < cost) return false;
    var pool = new List<int>();
    for (int id = 0; id < SignalCount; id++)
      if (available == null || available(id)) pool.Add(id);
    if (pool.Count < 3) return false;
    var choices = new List<SignalChoice>(3);
    for (int i = 0; i < 3; i++)
    {
      int pick = random.Next(i, pool.Count);
      (pool[i], pool[pick]) = (pool[pick], pool[i]);
      int roll = random.Next(100), rarity = 0;
      while (roll >= Weights[rarity]) roll -= Weights[rarity++];
      choices.Add(new SignalChoice { Signal = pool[i], Rarity = rarity });
    }
    wallet.CurrentRedGemCount -= cost;
    ScansPurchased++;
    PendingChoices = choices;
    return true;
  }

  public bool TryChoose(int index)
  {
    if (index < 0 || index >= PendingChoices.Count) return false;
    var choice = PendingChoices[index];
    if (StackCount(choice.Signal) == long.MaxValue) return false;
    Counts[choice.Signal * RarityCount + choice.Rarity]++;
    PendingChoices.Clear();
    return true;
  }

  public void Validate()
  {
    if (Counts == null || Counts.Length != SignalCount * RarityCount || PendingChoices == null
      || PendingChoices.Count is not (0 or 3)) throw new InvalidDataException("Invalid signal progress.");
    for (int s = 0; s < SignalCount; s++)
    {
      long total = 0;
      for (int r = 0; r < RarityCount; r++)
      {
        long count = Counts[s * RarityCount + r];
        if (count < 0 || count > long.MaxValue - total) throw new InvalidDataException("Invalid signal count.");
        total += count;
      }
    }
    var seen = new HashSet<int>();
    foreach (var choice in PendingChoices)
      if (choice == null || choice.Signal < 0 || choice.Signal >= SignalCount
        || choice.Rarity < 0 || choice.Rarity >= RarityCount || !seen.Add(choice.Signal))
        throw new InvalidDataException("Invalid pending signals.");
  }
}
