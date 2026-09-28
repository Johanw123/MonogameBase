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
      if (!iconPreloadRequested || System.Threading.Volatile.Read(ref iconLoadInFlight) != 0) return;
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
      RefuelButtonBackground = AssetManager.LoadAsync<Texture2D>("Textures/GUI/WenrexaAssetsUI_SciFI/PNG/Button03.png");
      RefuelButtonBackgroundHighlight =
        AssetManager.LoadAsync<Texture2D>("Textures/GUI/WenrexaAssetsUI_SciFI/PNG/Button02.png");

      TooltipBackground = AssetManager.LoadAsync<Texture2D>("Textures/GUI/WenrexaAssetsUI_SciFI/PNG/SelectPanel02_fix.png");
      TooltipTitleBackground = AssetManager.LoadAsync<Texture2D>("Textures/GUI/WenrexaAssetsUI_SciFI/PNG/test.png");

      //SpaceBackground = AssetManager.LoadAsync<Texture2D>("Textures/ScifiSpaceAssetsNAv1/Custom");

      //SpaceBackground2 = AssetManager.LoadAsync<Texture2D>("Textures/ScifiSpaceAssetsNAv1/Custom2");
      //SpaceBackground3 = AssetManager.LoadAsync<Texture2D>(ContentDirectory.Textures.ScifiSpaceAssetsNAv1.PremadeParallax.PremadeParallax3.bg4_png);

      SpaceBackground = AssetManager.LoadAsync<Texture2D>("Textures/space4k.png");
      SpaceBackground2 = AssetManager.LoadAsync<Texture2D>("Textures/space4kclouds.png");
      SpaceBackground3 = AssetManager.LoadAsync<Texture2D>("Textures/space4kstars.png");


      SpaceBackground4 = AssetManager.LoadAsync<Texture2D>(ContentDirectory.Textures.ScifiSpaceAssetsNAv1.PremadeParallax.PremadeParallax3.bg5_png);
      SpaceBackground5 = AssetManager.LoadAsync<Texture2D>(ContentDirectory.Textures.ScifiSpaceAssetsNAv1.PremadeParallax.PremadeParallax3.bg6_png);

      HarvesterShip = AssetManager.LoadAsync<Texture2D>("Textures/Foozle_2DS0013_Void_EnemyFleet_2/Nairan/Designs - Base/PNGs/Nairan - Scout - Base.png");
      AssetManager.LoadAsync<Texture2D>("Textures/Foozle_2DS0013_Void_EnemyFleet_2/Nairan/Engine Effects/PNGs/Nairan - Scout - Engine.png");

      AdvancedHarvesterShip = AssetManager.LoadAsync<Texture2D>("Textures/Foozle_2DS0013_Void_EnemyFleet_2/Nairan/Designs - Base/PNGs/Nairan - Fighter - Base.png");
      PerimeterHarvesterShip = AssetManager.LoadAsync<Texture2D>("Textures/Foozle_2DS0013_Void_EnemyFleet_2/Nairan/Designs - Base/PNGs/Nairan - Torpedo Ship - Base.png");
      ExpertHarvesterShip = AssetManager.LoadAsync<Texture2D>("Textures/Foozle_2DS0013_Void_EnemyFleet_2/Nairan/Designs - Base/PNGs/Nairan - Bomber - Base.png");
      UltimateHarvesterShip = AssetManager.LoadAsync<Texture2D>("Textures/Foozle_2DS0013_Void_EnemyFleet_2/Nairan/Designs - Base/PNGs/Nairan - Frigate - Base.png");

      DroneShip = AssetManager.LoadAsync<Texture2D>("Textures/Foozle_2DS0013_Void_EnemyFleet_2/Nairan/Designs - Base/PNGs/Nairan - Support Ship - Base.png");
      AssetManager.LoadAsync<Texture2D>("Textures/Foozle_2DS0013_Void_EnemyFleet_2/Nairan/Engine Effects/PNGs/Nairan - Support Ship - Engine.png");

      HomeBase = AssetManager.LoadAsync<Texture2D>("Textures/Foozle_2DS0013_Void_EnemyFleet_2/Nairan/Designs - Base/PNGs/Nairan - Battlecruiser - Base.png");

      BlackHole = AssetManager.LoadAsync<Texture2D>("Textures/black_hole.png");

      // SpaceBackground = AssetManager.LoadAsync<Texture2D>(ContentDirectory.Textures.purple_nebula.PurpleNebula2_1024x1024_png);
      // SpaceBackgroundDepth = AssetManager.LoadAsync<Texture2D>(ContentDirectory.Textures.result_upscaled_png);

      HudRedGem = AssetManager.LoadAsync<Texture2D>(ContentDirectory.Textures.Gems.GemGrayStatic_png);
      HudBlueGem = AssetManager.LoadAsync<Texture2D>("Textures/Gems/Gem2GrayStatic.png");

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
      ShapeFx = AssetManager.LoadAsync<Effect>("Shaders/Shapes/apos-shapes.fx");
      LineSdfFx = AssetManager.LoadAsync<Effect>("Shaders/LineSDF.fx");
      RectangleSdfFx = AssetManager.LoadAsync<Effect>("Shaders/JuicySDFRect.fx");
      // BlurFx = AssetManager.LoadAsync<Effect>("Shaders/BlurShader.fx");

      HarvesterEffect = AssetManager.LoadAsync<Effect>(ContentDirectory.Shaders.HarvesterShader_fx);
      BackgroundEffect = AssetManager.LoadAsync<Effect>(ContentDirectory.Shaders.BackgroundShader_fx);

      GemEffect = AssetManager.LoadAsync<Effect>(ContentDirectory.Shaders.GemShader_fx);
    }
  }
}
