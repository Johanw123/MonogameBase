using System;
using AsyncContent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace UntitledGemGame;

// Visual experiment only: simulation, bounds, mouse picking and saves keep their
// current coordinates. Each prerendered heading already includes the camera tilt.
public static class IsometricVisuals
{
  public const int ShipFrameSize = 512;
  public const int ShipFrameCount = 128;
  public const int ShipBankCount = 5;
  public const int ShipPagesPerBank = 8;
  public const int ShipColumns = 4;
  public const int FramesPerPage = 16;
  public const int PlanetFrameSize = 512;
  public const int PlanetFrameCount = 120;
  public const int PlanetColumns = 4;
  public const float PlanetRotationSeconds = 4.8f;
  public const int GemFrameCount = 8;
  public static bool Enabled { get; set; } = Demo.IsDev;
  public static bool BankingEnabled { get; set; } = true;
  public static readonly AsyncAsset<Texture2D>[] ShipPages = new AsyncAsset<Texture2D>[ShipPagesPerBank * ShipBankCount];
  public static readonly AsyncAsset<Texture2D>[] PlanetPages = new AsyncAsset<Texture2D>[8];
  public static AsyncAsset<Texture2D> Gems;
  private static bool pagesReady;
  private static bool Ready(AsyncAsset<Texture2D> asset) => asset?.IsLoaded == true && !asset.IsFailed;
  public static bool Active => Enabled && (pagesReady || CheckPages());

  private static bool CheckPages()
  {
    if (!Ready(Gems)) return false;
    for (int i = 0; i < ShipPages.Length; i++)
      if (!Ready(ShipPages[i])) return false;
    for (int i = 0; i < PlanetPages.Length; i++)
      if (!Ready(PlanetPages[i])) return false;
    return pagesReady = true;
  }

  public static void Preload()
  {
    for (int i = 0; i < ShipPages.Length; i++)
    {
      int page = i;
      GameplayPreloader.Queue<Texture2D>($"Textures/Isometric/ship-{page:D2}.png", asset => ShipPages[page] = asset);
    }
    for (int i = 0; i < PlanetPages.Length; i++)
    {
      int page = i;
      GameplayPreloader.Queue<Texture2D>($"Textures/Isometric/planet-{page:D2}.png", asset => PlanetPages[page] = asset);
    }
    GameplayPreloader.Queue<Texture2D>("Textures/Isometric/gem.png", asset => Gems = asset);
  }

  // Clockwise SCREEN headings from up, matching Transform2.Rotation. The
  // exporter undoes foreshortening so the screen directions are equally spaced.
  public static int ShipFrame(float rotation)
  {
    int index = (int)MathF.Floor(rotation * ShipFrameCount / MathHelper.TwoPi + 0.5f);
    return (index % ShipFrameCount + ShipFrameCount) % ShipFrameCount;
  }

  public static Rectangle ShipSource(float rotation)
  {
    int frame = ShipFrame(rotation) % FramesPerPage;
    return new Rectangle(frame % ShipColumns * ShipFrameSize, frame / ShipColumns * ShipFrameSize, ShipFrameSize, ShipFrameSize);
  }

  public static int ShipBankIndex(float bank)
    => Math.Clamp((int)MathF.Round(Math.Clamp(bank, -1f, 1f) * 2f,
      MidpointRounding.AwayFromZero) + 2, 0, ShipBankCount - 1);

  public static Texture2D ShipTexture(float rotation, float bank = 0f)
    => ShipPages[ShipBankIndex(bank) * ShipPagesPerBank + ShipFrame(rotation) / FramesPerPage].Value;

  private static int PlanetFrame(float age)
    => (int)(age % PlanetRotationSeconds / PlanetRotationSeconds * PlanetFrameCount);

  public static Texture2D PlanetTexture(float age) => PlanetPages[PlanetFrame(age) / FramesPerPage].Value;

  public static Rectangle PlanetSource(float age)
  {
    int frame = PlanetFrame(age) % FramesPerPage;
    return new Rectangle(frame % PlanetColumns * PlanetFrameSize, frame / PlanetColumns * PlanetFrameSize, PlanetFrameSize, PlanetFrameSize);
  }
}
