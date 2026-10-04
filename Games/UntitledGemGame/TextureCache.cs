using AsepriteDotNet;
using AsyncContent;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Graphics;

namespace UntitledGemGame
{
  public static class TextureCache
  {
    public static AsyncAsset<Texture2D> RefuelButtonBackground;
    public static AsyncAsset<Texture2D> RefuelButtonBackgroundHighlight;
    public static AsyncAsset<Texture2D> SpaceBackground;
    public static AsyncAsset<Texture2D> SpaceBackground2;
    public static AsyncAsset<Texture2D> SpaceBackground3;
    public static AsyncAsset<Texture2D> SpaceBackground4;
    public static AsyncAsset<Texture2D> SpaceBackground5;
    // public static AsyncAsset<Texture2D> SpaceBackgroundDepth;

    public static AsyncAsset<Texture2D> TooltipBackground;
    public static AsyncAsset<Texture2D> TooltipTitleBackground;

    public static AsyncAsset<Texture2D> FleetTexture;
    private static FleetAtlas fleet;
    public static FleetAtlas Fleet => FleetTexture?.IsLoaded == true && !FleetTexture.IsFailed
      ? fleet ??= new FleetAtlas(FleetTexture.Value, AssetManager.Load<string>("Atlases/fleet.json"))
      : null;
    public static Texture2DRegion HarvesterShip => Fleet?.Region(FleetAtlas.ScoutHull);
    public static Texture2DRegion AdvancedHarvesterShip => Fleet?.Region(FleetAtlas.FighterHull);
    public static Texture2DRegion PerimeterHarvesterShip => Fleet?.Region(FleetAtlas.TorpedoHull);
    public static Texture2DRegion ExpertHarvesterShip => Fleet?.Region(FleetAtlas.BomberHull);
    public static Texture2DRegion UltimateHarvesterShip => Fleet?.Region(FleetAtlas.FrigateHull);
    public static Texture2DRegion DroneShip => Fleet?.Region(FleetAtlas.DroneHull);
    public static Texture2DRegion HomeBase => Fleet?.Region(FleetAtlas.HomeBaseHull);

    public static AsyncAsset<Texture2D> Planet;
    public static AsyncAsset<Texture2D> PlanetCannonBullet;
    public static AsyncAsset<Texture2D> RocketProjectile;
    public static AsyncAsset<Texture2D> BigSpaceGunShell;
    public static AsyncAsset<Texture2D> PlanetExplosion;

    public static AsyncAsset<Texture2D> HudRedGem;
    public static AsyncAsset<Texture2D> HudBlueGem;
    

    public static AsyncAsset<Texture2D> Logo;
    public static AsyncAsset<Texture2D> IconAtlas;
    public static readonly Rectangle[] ModuleIcons = new Rectangle[ModuleCatalog.Icons.Length];
    public static readonly Rectangle[] SignalIcons = new Rectangle[SignalCatalog.Definitions.Length];
    private static bool iconPreloadRequested;

    public static void RequestIconPreload()
    {
      if (iconPreloadRequested) return;
      iconPreloadRequested = true;
      GameplayPreloader.Queue<Texture2D>("Atlases/icons.png", asset =>
      {
        LoadIconRegions();
        IconAtlas = asset;
      });
    }

    // Pump after the menu has rendered; readiness includes the icon atlas upload.
    public static void UpdateIconPreload()
    {
      if (iconPreloadRequested) GameplayPreloader.Update();
    }

    private static void LoadIconRegions()
    {
      using var metadata = System.Text.Json.JsonDocument.Parse(
        AssetManager.Load<string>("Atlases/icons.json"));
      Rectangle Region(string path)
      {
        var frame = metadata.RootElement.GetProperty(path);
        return new Rectangle(frame[0].GetInt32(), frame[1].GetInt32(),
          frame[2].GetInt32(), frame[3].GetInt32());
      }
      for (int i = 1; i < ModuleIcons.Length; i++) ModuleIcons[i] = Region(ModuleCatalog.Icons[i]);
      for (int i = 0; i < SignalIcons.Length; i++) SignalIcons[i] = Region(SignalCatalog.Definitions[i].Icon);
    }

    private static bool initialized = false;
    // gemTextureRed = AssetManager.Load<Texture2D>(ContentDirectory.Textures.Gems.GemGrayStatic_png);
    // gemTextureRegionRed = new Texture2DRegion(gemTextureRed);
    //
    // gemTextureBlue = AssetManager.Load<Texture2D>("Textures/Gems/Gem2GrayStatic.png");
    // gemTextureRegionBlue = new Texture2DRegion(gemTextureBlue);

    public static void PreloadTextures()
    {
      if (initialized)
        return;

      initialized = true;
      GameplayPreloader.Queue<Texture2D>("Textures/GUI/WenrexaAssetsUI_SciFI/PNG/Button03.png", asset => RefuelButtonBackground = asset);
      GameplayPreloader.Queue<Texture2D>("Textures/GUI/WenrexaAssetsUI_SciFI/PNG/Button02.png", asset => RefuelButtonBackgroundHighlight = asset);

      GameplayPreloader.Queue<Texture2D>("Textures/GUI/WenrexaAssetsUI_SciFI/PNG/SelectPanel02_fix.png", asset => TooltipBackground = asset);
      GameplayPreloader.Queue<Texture2D>("Textures/GUI/WenrexaAssetsUI_SciFI/PNG/test.png", asset => TooltipTitleBackground = asset);

      //SpaceBackground = AssetManager.LoadAsync<Texture2D>("Textures/ScifiSpaceAssetsNAv1/Custom");

      //SpaceBackground2 = AssetManager.LoadAsync<Texture2D>("Textures/ScifiSpaceAssetsNAv1/Custom2");
      //SpaceBackground3 = AssetManager.LoadAsync<Texture2D>(ContentDirectory.Textures.ScifiSpaceAssetsNAv1.PremadeParallax.PremadeParallax3.bg4_png);

      SpaceBackground = GameplayPreloader.Load<Texture2D>("Textures/space4k.png");
      SpaceBackground2 = GameplayPreloader.Load<Texture2D>("Textures/space4kclouds.png");
      SpaceBackground3 = GameplayPreloader.Load<Texture2D>("Textures/space4kstars.png");


      GameplayPreloader.Queue<Texture2D>(ContentDirectory.Textures.ScifiSpaceAssetsNAv1.PremadeParallax.PremadeParallax3.bg5_png, asset => SpaceBackground4 = asset);
      GameplayPreloader.Queue<Texture2D>(ContentDirectory.Textures.ScifiSpaceAssetsNAv1.PremadeParallax.PremadeParallax3.bg6_png, asset => SpaceBackground5 = asset);

      // Required by the menu fleet as well as gameplay; joins the startup batch.
      FleetTexture = GameplayPreloader.Load<Texture2D>("Atlases/fleet.png");

      GameplayPreloader.Queue<Texture2D>("Textures/Foozle_2DS0015_Void_EnvironmentPack/Planets/PNGs/Earth-Like planet.png",
        asset => Planet = asset);
      GameplayPreloader.Queue<Texture2D>("Textures/Foozle_2DS0011_Void_MainShip/Main ship weapons/PNGs/Main ship weapon - Projectile - Auto cannon bullet.png",
        asset => PlanetCannonBullet = asset);
      GameplayPreloader.Queue<Texture2D>("Textures/Foozle_2DS0013_Void_EnemyFleet_2/Nairan/Weapon Effects - Projectiles/PNGs/Nairan - Rocket.png",
        asset => RocketProjectile = asset);
      GameplayPreloader.Queue<Texture2D>("Textures/Foozle_2DS0011_Void_MainShip/Main ship weapons/PNGs/Main ship weapon - Projectile - Big Space Gun.png",
        asset => BigSpaceGunShell = asset);
      GameplayPreloader.Queue<Texture2D>("Textures/Foozle_2DS0015_Void_EnvironmentPack/Asteroids/PNGs/Asteroid 01 - Explode.png",
        asset => PlanetExplosion = asset);

      // SpaceBackground = AssetManager.LoadAsync<Texture2D>(ContentDirectory.Textures.purple_nebula.PurpleNebula2_1024x1024_png);
      // SpaceBackgroundDepth = AssetManager.LoadAsync<Texture2D>(ContentDirectory.Textures.result_upscaled_png);

      GameplayPreloader.Queue<Texture2D>(ContentDirectory.Textures.Gems.GemGrayStatic_png, asset => HudRedGem = asset);
      GameplayPreloader.Queue<Texture2D>("Textures/Gems/Gem2GrayStatic.png", asset => HudBlueGem = asset);

      Logo = GameplayPreloader.Load<Texture2D>("Textures/logo_4k.png");
    }
  }

  public static class EffectCache
  {
    public static AsyncAsset<Effect> ShapeFx;
    public static AsyncAsset<Effect> LineSdfFx;
    public static AsyncAsset<Effect> LaserBeamFx;
    public static AsyncAsset<Effect> BlackHoleWarpFx;
    public static AsyncAsset<Effect> BlackHoleFx;
    public static AsyncAsset<Effect> RectangleSdfFx;
    // public static AsyncAsset<Effect> BlurFx;
    public static AsyncAsset<Effect> HarvesterEffect;
    public static AsyncAsset<Effect> BackgroundEffect;

    public static AsyncAsset<Effect> GemEffect;

    public static bool initialized = false;

    public static void PreloadEffects()
    {
      if (initialized)
        return;

      initialized = true;
      GameplayPreloader.Queue<Effect>("Shaders/Shapes/apos-shapes.fx", asset => ShapeFx = asset);
      GameplayPreloader.Queue<Effect>("Shaders/LineSDF.fx", asset => LineSdfFx = asset);
      GameplayPreloader.Queue<Effect>("Shaders/LaserBeam.fx", asset => LaserBeamFx = asset);
      GameplayPreloader.Queue<Effect>("Shaders/BlackHoleWarp.fx", asset => BlackHoleWarpFx = asset);
      GameplayPreloader.Queue<Effect>("Shaders/BlackHole.fx", asset => BlackHoleFx = asset);
      GameplayPreloader.Queue<Effect>("Shaders/JuicySDFRect.fx", asset => RectangleSdfFx = asset);
      // BlurFx = AssetManager.LoadAsync<Effect>("Shaders/BlurShader.fx");

      GameplayPreloader.Queue<Effect>(ContentDirectory.Shaders.HarvesterShader_fx, asset => HarvesterEffect = asset);
      BackgroundEffect = GameplayPreloader.Load<Effect>(ContentDirectory.Shaders.BackgroundShader_fx);

      GameplayPreloader.Queue<Effect>(ContentDirectory.Shaders.GemShader_fx, asset => GemEffect = asset);
    }
  }
}
