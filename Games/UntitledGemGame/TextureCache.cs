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
    public static Texture2D RefuelButtonBackground;
    public static Texture2D RefuelButtonBackgroundHighlight;

    public static void PreloadTextures()
    {
      RefuelButtonBackground = AssetManager.Load<Texture2D>(ContentDirectory.Textures.ButtonBackground_png);
      RefuelButtonBackgroundHighlight =
        AssetManager.Load<Texture2D>(ContentDirectory.Textures.ButtonBackgroundHighlight_png);
    }
  }
}
