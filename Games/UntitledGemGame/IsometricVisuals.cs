using System;
using AsyncContent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using UntitledGemGame.Entities;

namespace UntitledGemGame;

// Visual experiment only: simulation, bounds, mouse picking and saves keep their
// current coordinates. Each prerendered heading already includes the camera tilt.
public static class IsometricVisuals
{
  public const int ShipFrameSize = 256;
  public const int ShipFrameCount = 128;
  public const int ShipBankCount = 5;
  public const int ShipPagesPerBank = 2;
  public const int ShipColumns = 8;
  public const int FramesPerPage = 64;
  public const int PlanetFramesPerPage = 16;
  public const int ShipModelCount = 7;
  public const int HomeFrameSize = 512;
  public const int HomeFramesPerPage = 16;
  public const int HomePages = 8;
  public const int FleetEmissionFrameSize = 128;
  public const int PlanetEmissionFrameSize = 256;
  public const float EmissionBrightness = 0.85f;
  private static readonly string[] ShipModels = { "basic", "advanced", "perimeter", "expert", "ultimate", "drone", "home" };
  public const int PlanetFrameSize = 512;
  public const int PlanetFrameCount = 120;
  public const int PlanetColumns = 4;
  public const float PlanetRotationSeconds = 4.8f;
  public const int GemFrameCount = 8;
  public static bool Enabled { get; set; } = Demo.IsDev;
  public static bool BankingEnabled { get; set; } = true;
  // Six banked fleet ships, plus higher-resolution level-only homebase pages.
  public static readonly AsyncAsset<Texture2D>[] ShipPages = new AsyncAsset<Texture2D>[(ShipModelCount - 1) * ShipBankCount * ShipPagesPerBank + HomePages];
  public static readonly AsyncAsset<Texture2D>[] HomeEmissionPages = new AsyncAsset<Texture2D>[HomePages];
  public static readonly AsyncAsset<Texture2D>[] FleetEmissionPages = new AsyncAsset<Texture2D>[(ShipModelCount - 1) * ShipBankCount * ShipPagesPerBank];
  public static readonly AsyncAsset<Texture2D>[] PlanetPages = new AsyncAsset<Texture2D>[8];
  public static readonly AsyncAsset<Texture2D>[] PlanetEmissionPages = new AsyncAsset<Texture2D>[8];
  public static AsyncAsset<Texture2D> Gems;
  private static bool pagesReady;
  // Missing files may resolve to the loader's white placeholder. Do not treat
  // that placeholder as a usable atlas, especially for additive emission.
  private static bool Ready(AsyncAsset<Texture2D> asset, int width, int height)
    => asset?.IsLoaded == true && !asset.IsFailed
      && asset.Value.Width == width && asset.Value.Height == height;
  public static bool Active => Enabled && (pagesReady || CheckPages());

  private static bool CheckPages()
  {
    if (!Ready(Gems, 512, 64)) return false;
    for (int i = 0; i < ShipPages.Length; i++)
      if (!Ready(ShipPages[i], 2048, 2048)) return false;
    for (int i = 0; i < PlanetPages.Length; i++)
      if (!Ready(PlanetPages[i], 2048, i == 7 ? 1024 : 2048)) return false;
    for (int i = 0; i < HomeEmissionPages.Length; i++)
      if (!Ready(HomeEmissionPages[i], 2048, 2048)) return false;
    for (int i = 0; i < FleetEmissionPages.Length; i++)
      if (!Ready(FleetEmissionPages[i], 1024, 1024)) return false;
    for (int i = 0; i < PlanetEmissionPages.Length; i++)
      if (!Ready(PlanetEmissionPages[i], 1024, i == 7 ? 512 : 1024)) return false;
    return pagesReady = true;
  }

  public static void Preload()
  {
    for (int model = 0; model < ShipModels.Length; model++)
    {
      int count = model == ShipModelCount - 1 ? HomePages : ShipPagesPerBank * ShipBankCount;
      for (int i = 0; i < count; i++)
      {
        int page = model * ShipPagesPerBank * ShipBankCount + i;
        GameplayPreloader.Queue<Texture2D>($"Textures/Isometric/ship-{ShipModels[model]}-{i:D2}.png", asset => ShipPages[page] = asset);
        if (model != ShipModelCount - 1)
          GameplayPreloader.Queue<Texture2D>($"Textures/Isometric/ship-{ShipModels[model]}-emission-{i:D2}.png", asset => FleetEmissionPages[page] = asset);
      }
    }
    for (int i = 0; i < HomeEmissionPages.Length; i++)
    {
      int page = i;
      GameplayPreloader.Queue<Texture2D>($"Textures/Isometric/ship-home-emission-{page:D2}.png", asset => HomeEmissionPages[page] = asset);
    }
    for (int i = 0; i < PlanetPages.Length; i++)
    {
      int page = i;
      GameplayPreloader.Queue<Texture2D>($"Textures/Isometric/planet-{page:D2}.png", asset => PlanetPages[page] = asset);
      GameplayPreloader.Queue<Texture2D>($"Textures/Isometric/planet-emission-{page:D2}.png", asset => PlanetEmissionPages[page] = asset);
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

  public static int ShipModelIndex(Harvester.HarvesterType type) => type switch
  {
    Harvester.HarvesterType.AdvancedHarvester => 1,
    Harvester.HarvesterType.PerimeterHarvester => 2,
    Harvester.HarvesterType.ExpertHarvester => 3,
    Harvester.HarvesterType.UltimateHarvester => 4,
    Harvester.HarvesterType.Drone => 5,
    Harvester.HarvesterType.HomeBase => 6,
    _ => 0,
  };

  public static int ShipPage(Harvester.HarvesterType type, float rotation, float bank = 0f)
    => ShipModelIndex(type) * ShipPagesPerBank * ShipBankCount
      + (type == Harvester.HarvesterType.HomeBase ? 0 : ShipBankIndex(bank) * ShipPagesPerBank)
      + ShipFrame(rotation) / (type == Harvester.HarvesterType.HomeBase ? HomeFramesPerPage : FramesPerPage);

  public static int FrameSize(Harvester.HarvesterType type)
    => type == Harvester.HarvesterType.HomeBase ? HomeFrameSize : ShipFrameSize;

  public static Rectangle ShipSource(Harvester.HarvesterType type, float rotation)
  {
    if (type != Harvester.HarvesterType.HomeBase) return ShipSource(rotation);
    int frame = ShipFrame(rotation) % HomeFramesPerPage;
    return new Rectangle(frame % 4 * HomeFrameSize, frame / 4 * HomeFrameSize, HomeFrameSize, HomeFrameSize);
  }

  public static Texture2D HomeEmissionTexture(float rotation)
    => HomeEmissionPages[ShipFrame(rotation) / HomeFramesPerPage].Value;

  public static Texture2D ShipEmissionTexture(Harvester.HarvesterType type, float rotation, float bank)
    => type == Harvester.HarvesterType.HomeBase ? HomeEmissionTexture(rotation)
      : FleetEmissionPages[ShipPage(type, rotation, bank)].Value;

  public static int EmissionFrameSize(Harvester.HarvesterType type)
    => type == Harvester.HarvesterType.HomeBase ? HomeFrameSize : FleetEmissionFrameSize;

  public static Rectangle ShipEmissionSource(Harvester.HarvesterType type, float rotation)
  {
    if (type == Harvester.HarvesterType.HomeBase) return ShipSource(type, rotation);
    int frame = ShipFrame(rotation) % FramesPerPage;
    return new Rectangle(frame % ShipColumns * FleetEmissionFrameSize,
      frame / ShipColumns * FleetEmissionFrameSize, FleetEmissionFrameSize, FleetEmissionFrameSize);
  }

  public static Texture2D ShipTexture(Harvester.HarvesterType type, float rotation, float bank = 0f)
    => ShipPages[ShipPage(type, rotation, bank)].Value;

  // Every exported mesh fits a radius-1.3 sphere in a 3.7-wide camera canvas.
  // Use the existing base radius (which already includes entity scale) and
  // the same smoothed pickup bonus as the legacy hull. Never apply scale twice.
  public static float ShipCanvasWorldSize(Harvester harvester)
    => BaseStats.GetHarvesterBaseCollectionRange(harvester)
      * (harvester.VisualCollectionRangeMultiplier ?? 1f) * (3.7f / 1.3f);

  private static int PlanetFrame(float age)
    => (int)(age % PlanetRotationSeconds / PlanetRotationSeconds * PlanetFrameCount);

  public static Texture2D PlanetTexture(float age) => PlanetPages[PlanetFrame(age) / PlanetFramesPerPage].Value;
  public static Texture2D PlanetEmissionTexture(float age) => PlanetEmissionPages[PlanetFrame(age) / PlanetFramesPerPage].Value;

  public static Rectangle PlanetEmissionSource(float age)
  {
    var source = PlanetSource(age);
    int divisor = PlanetFrameSize / PlanetEmissionFrameSize;
    return new Rectangle(source.X / divisor, source.Y / divisor, source.Width / divisor, source.Height / divisor);
  }

  public static Rectangle PlanetSource(float age)
  {
    int frame = PlanetFrame(age) % PlanetFramesPerPage;
    return new Rectangle(frame % PlanetColumns * PlanetFrameSize, frame / PlanetColumns * PlanetFrameSize, PlanetFrameSize, PlanetFrameSize);
  }
}
