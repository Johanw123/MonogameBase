/// <summary>
/// Entry point for the Content Builder project, 
/// which when executed will build content according to the "Content Collection Strategy" defined in the Builder class.
/// </summary>
/// <remarks>
/// Make sure to validate the directory paths in the "ContentBuilderParams" for your specific project.
/// For more details regarding the Content Builder, see the MonoGame documentation: <tbc.>
/// </remarks>

using Microsoft.Toolkit.HighPerformance;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content.Pipeline;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Media;
using MonoGame.Framework.Content.Pipeline.Builder;

var contentCollectionArgs = new ContentBuilderParams()
{
  Mode = ContentBuilderMode.Builder,
  WorkingDirectory = $"{AppContext.BaseDirectory}../../", // path to where your content folder can be located
  SourceDirectory = "Assets", // Not actually needed as this is the default, but added for reference
  Platform = TargetPlatform.DesktopGL
};
var builder = new Builder();

if (args is not null && args.Length > 0)
{
  builder.Run(args);
}
else
{
  builder.Run(contentCollectionArgs);
}

return builder.FailedToBuild > 0 ? -1 : 0;

public class Builder : ContentBuilder
{
  public override IContentCollection GetContentCollection()
  {
    var contentCollection = new ContentCollection();

    // By default, no content will be imported from the Assets folder using the default importer for their file type.
    // Please define your content collection rules here.

    /* Examples

    // Import all content in the Assets folder using the default importer for their file type.
    contentCollection.Include<WildcardRule>("*");

    // Only copy content from the assets folder rather than build it with the pipeline.
    contentCollection.IncludeCopy<WildcardRule>("*.json");

    // Exclude assets that match the pattern., only required overriding a default import behaviour.
    contentCollection.Exclude<WildcardRule>("Font/*.txt");

    // Include a specific asset with processor parameters.
    contentCollection.Include("Models/character.glb", new FbxImporter(),
        new MeshAnimatedModelProcessor()
        {
            Scale = 100.0f
        }
    );
    */

    contentCollection.IncludeCopy<WildcardRule>("Data/*.json");
    contentCollection.IncludeCopy<WildcardRule>("GumProject/*.*");
    contentCollection.IncludeCopy<WildcardRule>("Fonts/*.ttf");
    contentCollection.IncludeCopy<WildcardRule>("Fonts/GeneratedFonts/*.*");

    // contentCollection.Include<WildcardRule>("Shaders/*.fx", );

    // Music Files
    contentCollection.Include<WildcardRule>("Music/Holizna/Greys.ogg");
    contentCollection.Include<WildcardRule>("Music/Holizna/Hopkinsville Goblins.ogg");
    contentCollection.Include<WildcardRule>("Music/Holizna/Pleiadeans.ogg");
    contentCollection.Include<WildcardRule>("Music/Holizna/Sky Fish.ogg");

    // SFX Files
    contentCollection.Include<WildcardRule>("SFX/Menu/Soundpack/Minimalist7.wav");
    contentCollection.Include<WildcardRule>("SFX/Menu/Soundpack/Minimalist10.wav");
    contentCollection.Include<WildcardRule>("SFX/Ship.wav");
    contentCollection.Include<WildcardRule>("SFX/gem.wav");
    contentCollection.Include<WildcardRule>("SFX/Impact_test2.wav");
    contentCollection.Include<WildcardRule>("SFX/blip.wav");
    contentCollection.Include<WildcardRule>("SFX/Menu/swoosh_4.wav");
    contentCollection.Include<WildcardRule>("SFX/Menu/test3.wav");
    contentCollection.Include<WildcardRule>("SFX/Menu/hover_tooltip.wav");

    // GUI Textures
    contentCollection.Include<WildcardRule>("Textures/GUI/WenrexaAssetsUI_SciFI/PNG/Button02.png");
    contentCollection.Include<WildcardRule>("Textures/GUI/WenrexaAssetsUI_SciFI/PNG/Button03.png");
    contentCollection.Include<WildcardRule>("Textures/GUI/WenrexaAssetsUI_SciFI/PNG/Button04.png");
    contentCollection.Include<WildcardRule>("Textures/GUI/WenrexaAssetsUI_SciFI/PNG/Button11.png");
    contentCollection.Include<WildcardRule>("Textures/GUI/WenrexaAssetsUI_SciFI/PNG/Switch03.png");
    contentCollection.Include<WildcardRule>("Textures/GUI/WenrexaAssetsUI_SciFI/PNG/Switch04.png");
    contentCollection.Include<WildcardRule>("Textures/GUI/WenrexaAssetsUI_SciFI/PNG/TitlePanel02.png");
    contentCollection.Include<WildcardRule>("Textures/GUI/WenrexaAssetsUI_SciFI/PNG/SelectPanel02_fix.png");
    contentCollection.Include<WildcardRule>("Textures/GUI/WenrexaAssetsUI_SciFI/PNG/test.png");
    contentCollection.Include<WildcardRule>("Textures/GUI/Button Normal.png");
    contentCollection.Include<WildcardRule>("Textures/GUI/icon.png");
    //
    //
    //

    contentCollection.Include<WildcardRule>("Textures/GUI/border.png");
    contentCollection.Include<WildcardRule>("Textures/GUI/iconHidden.png");
    contentCollection.Include<WildcardRule>("Textures/GUI/icon_background.png");

    contentCollection.Include<WildcardRule>("Textures/red_pixel.png");
    contentCollection.Include<WildcardRule>("Textures/blue_pixel.png");

    // Space Backgrounds
    contentCollection.Include<WildcardRule>("Textures/space4k.png");
    contentCollection.Include<WildcardRule>("Textures/space4kclouds.png");
    contentCollection.Include<WildcardRule>("Textures/space4kstars.png");

    contentCollection.Include<WildcardRule>("Textures/ScifiSpaceAssetsNAv1/PremadeParallax/PremadeParallax3/bg5.png");
    contentCollection.Include<WildcardRule>("Textures/ScifiSpaceAssetsNAv1/PremadeParallax/PremadeParallax3/bg6.png");

    // Ship & Entity Textures
    contentCollection.Include<WildcardRule>("Textures/Foozle_2DS0013_Void_EnemyFleet_2/Nairan/Designs - Base/PNGs/Nairan - Scout - Base.png");
    contentCollection.Include<WildcardRule>("Textures/Foozle_2DS0013_Void_EnemyFleet_2/Nairan/Engine Effects/PNGs/Nairan - Scout - Engine.png");
    contentCollection.Include<WildcardRule>("Textures/Foozle_2DS0013_Void_EnemyFleet_2/Nairan/Designs - Base/PNGs/Nairan - Support Ship - Base.png");
    contentCollection.Include<WildcardRule>("Textures/Foozle_2DS0013_Void_EnemyFleet_2/Nairan/Engine Effects/PNGs/Nairan - Support Ship - Engine.png");
    contentCollection.Include<WildcardRule>("Textures/Foozle_2DS0013_Void_EnemyFleet_2/Nairan/Designs - Base/PNGs/Nairan - Battlecruiser - Base.png");
    //"Textures/Foozle_2DS0013_Void_EnemyFleet_2/Nairan/Engine Effects/PNGs/Nairan - Scout - Engine.png",

    contentCollection.Include<WildcardRule>("Textures/black_hole.png");

    // HUD & General Textures
    contentCollection.Include<WildcardRule>("Textures/Gems/GemGrayStatic.png");
    contentCollection.Include<WildcardRule>("Textures/Gems/Gem2GrayStatic.png");
    contentCollection.Include<WildcardRule>("Textures/logo_4k.png");


    contentCollection.Include<WildcardRule>("Textures/Gems/Gem1/GEM 1 - RED - Spritesheet.png");
    contentCollection.Include<WildcardRule>("Textures/Gems/Gem3/GEM 3 - BLUE - Spritesheet.png");
    contentCollection.Include<WildcardRule>("Textures/Gems/Gem5/GEM 5 - LILAC - Spritesheet.png");
    contentCollection.Include<WildcardRule>("Textures/icons_set/icons_128/arrow_right.png");
    

    contentCollection.Include<WildcardRule>("Textures/scifi_icons/*.png");


    //contentCollection.Include<WildcardRule>("../../../../JapeFramework/JFContent/*.*");

    //contentCollection.Include<WildcardRule>("Textures/*.png");

    return contentCollection;
  }
}
