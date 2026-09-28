using AsepriteDotNet;
using AsyncContent;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;

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

    public static AsyncAsset<Texture2D> HarvesterShip;
    public static AsyncAsset<Texture2D> AdvancedHarvesterShip;
    public static AsyncAsset<Texture2D> PerimeterHarvesterShip;
    public static AsyncAsset<Texture2D> ExpertHarvesterShip;
    public static AsyncAsset<Texture2D> UltimateHarvesterShip;

    public static AsyncAsset<Texture2D> DroneShip;
    public static AsyncAsset<Texture2D> DroneEngine;

    public static AsyncAsset<Texture2D> HomeBase;
    public static AsyncAsset<Texture2D> BlackHole;

    public static AsyncAsset<Texture2D> HudRedGem;
    public static AsyncAsset<Texture2D> HudBlueGem;
    

    public static AsyncAsset<Texture2D> Logo;
    public static readonly AsyncAsset<Texture2D>[] ModuleIcons = new AsyncAsset<Texture2D>[ModuleCatalog.Icons.Length];
    public static readonly AsyncAsset<Texture2D>[] SignalIcons = new AsyncAsset<Texture2D>[SignalCatalog.Definitions.Length];
    private static bool iconPreloadRequested;
    private static int nextModuleIcon = 1;
    private static int nextSignalIcon;
    private static int iconLoadInFlight;

    public static void RequestIconPreload() => iconPreloadRequested = true;

    // Called from the game update, after the menu has rendered. Limit GPU uploads
    // to one outstanding icon and keep progressing if the player leaves the menu.
    public static void UpdateIconPreload()
    {
      if (!iconPreloadRequested) return;
      GameplayPreloader.Update();
      if (!GameplayPreloader.Ready || System.Threading.Volatile.Read(ref iconLoadInFlight) != 0) return;
      if (nextModuleIcon >= ModuleIcons.Length && nextSignalIcon >= SignalIcons.Length) return;
      System.Threading.Interlocked.Exchange(ref iconLoadInFlight, 1);
      if (nextModuleIcon < ModuleIcons.Length)
      {
        int index = nextModuleIcon++;
        ModuleIcons[index] = AssetManager.LoadAsync<Texture2D>(ModuleCatalog.Icons[index],
          callbackDone: _ => System.Threading.Interlocked.Exchange(ref iconLoadInFlight, 0));
      }
      else
      {
        int index = nextSignalIcon++;
        SignalIcons[index] = AssetManager.LoadAsync<Texture2D>(SignalCatalog.Definitions[index].Icon,
          callbackDone: _ => System.Threading.Interlocked.Exchange(ref iconLoadInFlight, 0));
      }
#if KNI_WEB
      // The web loader completes synchronously and does not invoke the callback.
      System.Threading.Interlocked.Exchange(ref iconLoadInFlight, 0);
#endif
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

      SpaceBackground = AssetManager.LoadAsync<Texture2D>("Textures/space4k.png");
      SpaceBackground2 = AssetManager.LoadAsync<Texture2D>("Textures/space4kclouds.png");
      SpaceBackground3 = AssetManager.LoadAsync<Texture2D>("Textures/space4kstars.png");


      GameplayPreloader.Queue<Texture2D>(ContentDirectory.Textures.ScifiSpaceAssetsNAv1.PremadeParallax.PremadeParallax3.bg5_png, asset => SpaceBackground4 = asset);
      GameplayPreloader.Queue<Texture2D>(ContentDirectory.Textures.ScifiSpaceAssetsNAv1.PremadeParallax.PremadeParallax3.bg6_png, asset => SpaceBackground5 = asset);

      HarvesterShip = AssetManager.LoadAsync<Texture2D>("Textures/Foozle_2DS0013_Void_EnemyFleet_2/Nairan/Designs - Base/PNGs/Nairan - Scout - Base.png");
      AssetManager.LoadAsync<Texture2D>("Textures/Foozle_2DS0013_Void_EnemyFleet_2/Nairan/Engine Effects/PNGs/Nairan - Scout - Engine.png");

      GameplayPreloader.Queue<Texture2D>("Textures/Foozle_2DS0013_Void_EnemyFleet_2/Nairan/Designs - Base/PNGs/Nairan - Fighter - Base.png", asset => AdvancedHarvesterShip = asset);
      GameplayPreloader.Queue<Texture2D>("Textures/Foozle_2DS0013_Void_EnemyFleet_2/Nairan/Designs - Base/PNGs/Nairan - Torpedo Ship - Base.png", asset => PerimeterHarvesterShip = asset);
      GameplayPreloader.Queue<Texture2D>("Textures/Foozle_2DS0013_Void_EnemyFleet_2/Nairan/Designs - Base/PNGs/Nairan - Bomber - Base.png", asset => ExpertHarvesterShip = asset);
      GameplayPreloader.Queue<Texture2D>("Textures/Foozle_2DS0013_Void_EnemyFleet_2/Nairan/Designs - Base/PNGs/Nairan - Frigate - Base.png", asset => UltimateHarvesterShip = asset);

      GameplayPreloader.Queue<Texture2D>("Textures/Foozle_2DS0013_Void_EnemyFleet_2/Nairan/Designs - Base/PNGs/Nairan - Support Ship - Base.png", asset => DroneShip = asset);
      GameplayPreloader.Queue<Texture2D>("Textures/Foozle_2DS0013_Void_EnemyFleet_2/Nairan/Engine Effects/PNGs/Nairan - Support Ship - Engine.png", asset => DroneEngine = asset);

      GameplayPreloader.Queue<Texture2D>("Textures/Foozle_2DS0013_Void_EnemyFleet_2/Nairan/Designs - Base/PNGs/Nairan - Battlecruiser - Base.png", asset => HomeBase = asset);

      GameplayPreloader.Queue<Texture2D>("Textures/black_hole.png", asset => BlackHole = asset);

      // SpaceBackground = AssetManager.LoadAsync<Texture2D>(ContentDirectory.Textures.purple_nebula.PurpleNebula2_1024x1024_png);
      // SpaceBackgroundDepth = AssetManager.LoadAsync<Texture2D>(ContentDirectory.Textures.result_upscaled_png);

      GameplayPreloader.Queue<Texture2D>(ContentDirectory.Textures.Gems.GemGrayStatic_png, asset => HudRedGem = asset);
      GameplayPreloader.Queue<Texture2D>("Textures/Gems/Gem2GrayStatic.png", asset => HudBlueGem = asset);

      Logo = AssetManager.LoadAsync<Texture2D>("Textures/logo_4k.png");
    }
  }

  public static class EffectCache
  {
    public static AsyncAsset<Effect> ShapeFx;
    public static AsyncAsset<Effect> LineSdfFx;
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
      GameplayPreloader.Queue<Effect>("Shaders/JuicySDFRect.fx", asset => RectangleSdfFx = asset);
      // BlurFx = AssetManager.LoadAsync<Effect>("Shaders/BlurShader.fx");

      GameplayPreloader.Queue<Effect>(ContentDirectory.Shaders.HarvesterShader_fx, asset => HarvesterEffect = asset);
      BackgroundEffect = AssetManager.LoadAsync<Effect>(ContentDirectory.Shaders.BackgroundShader_fx);

      GameplayPreloader.Queue<Effect>(ContentDirectory.Shaders.GemShader_fx, asset => GemEffect = asset);
    }
  }
}
