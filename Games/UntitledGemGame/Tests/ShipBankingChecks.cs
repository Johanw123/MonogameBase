using Microsoft.Xna.Framework;
using UntitledGemGame;

internal static class ShipBankingChecks
{
  public static void Run()
  {
    void Check(bool condition, string message)
    {
      if (!condition) throw new Exception(message);
    }
    float bank = 0f;
    float? previous = null;
    ShipBanking.Update(ref bank, ref previous, 6.27f, 1f / 60f);
    Check(bank == 0f, "Spawning must not cause an artificial bank.");
    ShipBanking.Update(ref bank, ref previous, 0.02f, 1f / 60f);
    Check(bank > 0f, "Crossing the heading wrap clockwise must bank right.");
    bank = 0f; previous = 0.02f;
    ShipBanking.Update(ref bank, ref previous, 6.27f, 1f / 60f);
    Check(bank < 0f, "Crossing the heading wrap counterclockwise must bank left.");
    bank = 0.8f; previous = 1f;
    ShipBanking.Update(ref bank, ref previous, 2f, 0f);
    Check(bank == 0.8f && previous == 1f, "Pausing must freeze banking state.");

    float Turn(int fps)
    {
      float result = 0f;
      float? angle = 0f;
      for (int i = 1; i <= fps; i++)
        ShipBanking.Update(ref result, ref angle, i * 1.2f / fps, 1f / fps);
      return result;
    }
    Check(MathF.Abs(Turn(30) - Turn(120)) < 0.001f,
      "The same turn must bank consistently at different simulation rates.");
    bank = 1f; previous = 0f;
    for (int i = 0; i < 120; i++) ShipBanking.Update(ref bank, ref previous, 0f, 1f / 60f);
    Check(bank == 0f, "Straight flight must settle fully back to level.");
    for (int i = -100; i <= 100; i++)
    {
      float amount = i / 100f;
      Check(IsometricVisuals.ShipBankIndex(amount) + IsometricVisuals.ShipBankIndex(-amount) == 4,
        "Left/right pose selection must be symmetric.");
    }
    for (int heading = 0; heading < 128; heading++)
    {
      float angle = heading * MathHelper.TwoPi / 128;
      Check(IsometricVisuals.ShipFrame(angle) == heading, "All heading frames must remain reachable.");
      Check(IsometricVisuals.ShipFrame(angle + MathHelper.TwoPi) == heading,
        "Heading wrap must preserve the selected frame.");
    }
    Console.WriteLine("Ship banking checks passed: turn direction, wrapping, pause, timing and recovery.");
  }
}
