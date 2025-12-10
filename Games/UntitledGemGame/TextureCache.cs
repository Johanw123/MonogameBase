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
    public static AsyncAsset<Texture2D> SpaceBackgroundDepth;


    public static AsyncAsset<Texture2D> HudRedGem;
    public static AsyncAsset<Texture2D> HudBlueGem;

    public static void PreloadTextures()
    {
      RefuelButtonBackground = AssetManager.LoadAsync<Texture2D>(ContentDirectory.Textures.ButtonBackground_png);
      RefuelButtonBackgroundHighlight =
        AssetManager.LoadAsync<Texture2D>(ContentDirectory.Textures.ButtonBackgroundHighlight_png);

      SpaceBackground = AssetManager.LoadAsync<Texture2D>(ContentDirectory.Textures.ScifiSpaceAssetsNAv1.PremadeParallax.PremadeParallax3.bg1_png);
      SpaceBackground2 = AssetManager.LoadAsync<Texture2D>(ContentDirectory.Textures.ScifiSpaceAssetsNAv1.PremadeParallax.PremadeParallax3.bg2_png);
      SpaceBackground3 = AssetManager.LoadAsync<Texture2D>(ContentDirectory.Textures.ScifiSpaceAssetsNAv1.PremadeParallax.PremadeParallax3.bg4_png);
      SpaceBackground4 = AssetManager.LoadAsync<Texture2D>(ContentDirectory.Textures.ScifiSpaceAssetsNAv1.PremadeParallax.PremadeParallax3.bg5_png);
      SpaceBackground5 = AssetManager.LoadAsync<Texture2D>(ContentDirectory.Textures.ScifiSpaceAssetsNAv1.PremadeParallax.PremadeParallax3.bg6_png);


      // SpaceBackground = AssetManager.LoadAsync<Texture2D>(ContentDirectory.Textures.purple_nebula.PurpleNebula2_1024x1024_png);
      SpaceBackgroundDepth = AssetManager.LoadAsync<Texture2D>(ContentDirectory.Textures.result_upscaled_png);

      HudRedGem = AssetManager.LoadAsync<Texture2D>(ContentDirectory.Textures.Gems.GemGrayStatic_png);
      HudBlueGem = AssetManager.LoadAsync<Texture2D>("Textures/Gems/Gem2GrayStatic.png");
    }
  }

  public static class EffectCache
  {
    public static AsyncAsset<Effect> ShapeFx;
    public static AsyncAsset<Effect> BlurFx;
    public static AsyncAsset<Effect> HarvesterEffect;
    public static AsyncAsset<Effect> BackgroundEffect;

    public static void PreloadEffects()
    {
      ShapeFx = AssetManager.LoadAsync<Effect>("Shaders/Shapes/apos-shapes.fx");
      BlurFx = AssetManager.LoadAsync<Effect>("Shaders/BlurShader.fx");

      HarvesterEffect = AssetManager.LoadAsync<Effect>(ContentDirectory.Shaders.HarvesterShader_fx);
      BackgroundEffect = AssetManager.LoadAsync<Effect>(ContentDirectory.Shaders.BackgroundShader_fx);
    }
  }
}
