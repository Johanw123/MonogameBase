using AsyncContent;
using JapeFramework.JsonClasses;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.Graphics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;

namespace JapeFramework.Aseprite
{
  public class AsepriteHelper
  {
    public static AnimatedSprite LoadAnimations(string pngPath, string jsonPath, string initialAnim)
    {
      var img = AssetManager.Load<Texture2D>(pngPath);
      var jsonText = AssetManager.Load<string>(jsonPath);

      var json = JsonConvert.DeserializeObject<Root>(jsonText);

      var framesObject = JObject.Parse(json.Frames.ToString());

      List<Root2> frames = new List<Root2>();

      foreach (var framePair in framesObject)
      {
        var key = framePair.Key;
        var val = framePair.Value;

        var frameData = JsonConvert.DeserializeObject<Root2>(val.ToString());
        frames.Add(frameData);
      }

      var fileName = Path.GetFileNameWithoutExtension(json.Meta.Image);

      var dudeAtlas = Texture2DAtlas.Create($"TextureAtlas//{fileName}", img, frames[0].Frame.W, frames[0].Frame.H);
      var spriteSheet = new SpriteSheet($"SpriteSheet//{fileName}", dudeAtlas);

      foreach (var frameTag in json.Meta.FrameTags)
      {
        var animName = frameTag.Name;
        var from = frameTag.From;
        var to = frameTag.To;

        for (int i = from; i < to; i++)
        {
          dudeAtlas.CreateRegion(frames[i].Frame.X, frames[i].Frame.Y, frames[i].Frame.W, frames[i].Frame.H, animName + i);
        }

        spriteSheet.DefineAnimation(animName, builder =>
        {
          builder.IsLooping(frameTag.Repeat == 0);
          for (int i = from; i < to; i++)
          {
            builder.AddFrame(animName + i, TimeSpan.FromMilliseconds(frames[i].Duration));
          }
        });
      }

      var animSprite = new AnimatedSprite(spriteSheet, initialAnim);
      animSprite.Origin = new Vector2(frames[0].Frame.W / 2.0f, frames[0].Frame.H / 2.0f);
      return animSprite;
    }
  }
}
