using Assimp;
using AsyncContent;
using JapeFramework.JsonClasses;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.Graphics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JapeFramework.Aseprite
{
  public class AsepriteHelper
  {
    static Dictionary<string, SpriteSheet> m_cache = new();

    public static AnimatedSprite LoadAnimation(string pngPath, bool loop, int numFrames, float animationSpeedMs)
    {
      if(m_cache.TryGetValue(pngPath, out var spriteSheet))
      {
        var w = (float)spriteSheet.TextureAtlas.Texture.Width / numFrames;
        var origin = new Vector2(w / 2.0f, spriteSheet.TextureAtlas.Texture.Height / 2.0f);
        return new AnimatedSprite(spriteSheet, "animName") { Origin = origin };
      }
      else
      {
        var img = AssetManager.Load<Texture2D>(pngPath);

        var fileName = Path.GetFileNameWithoutExtension(pngPath);

        var dudeAtlas = Texture2DAtlas.Create($"TextureAtlas//{fileName}", img, img.Width, img.Height);
        spriteSheet = new SpriteSheet($"SpriteSheet//{fileName}", dudeAtlas);

        var w = (float)spriteSheet.TextureAtlas.Texture.Width / numFrames;

        dudeAtlas.ClearRegions();

        for (int i = 0; i < numFrames; i++)
        {
          dudeAtlas.CreateRegion((int)(i * w), 0, (int)w, img.Height, "regionName" + i);
        }

        spriteSheet.DefineAnimation("animName", builder =>
        {
          builder.IsLooping(loop);
          for (int i = 0; i < numFrames; i++)
          {
            builder.AddFrame("regionName" + i, TimeSpan.FromMilliseconds(animationSpeedMs));
          }
        });

        m_cache.TryAdd(pngPath, spriteSheet);

        var origin = new Vector2(w / 2.0f, spriteSheet.TextureAtlas.Texture.Height / 2.0f);
        return new AnimatedSprite(spriteSheet, "animName") { Origin = origin };
      }
    }

    public static AnimatedSprite LoadTaggedAnimations(string pngPath, string jsonPath, string initialAnim = "")
    {
      if (m_cache.TryGetValue(pngPath, out var spriteSheet))
      {
        //var w = (float)spriteSheet.TextureAtlas.Texture.Width / numFrames;
        //var origin = new Vector2(w / 2.0f, spriteSheet.TextureAtlas.Texture.Height / 2.0f);
        //var origin = new Vector2(frames[0].Frame.W / 2.0f, frames[0].Frame.H / 2.0f);

        //TODO: origin not calculated here
        return new AnimatedSprite(spriteSheet, "animName") { /*Origin = origin*/ };
      }
      else
      {
        var img = AssetManager.Load<Texture2D>(pngPath);
        var jsonText = AssetManager.Load<string>(jsonPath);

        var json = JsonConvert.DeserializeObject<Root>(jsonText);

        var framesObject = JObject.Parse(json.Frames.ToString());

        List<Root2> frames = new List<Root2>();

        foreach (var framePair in framesObject)
        {
          var val = framePair.Value;

          var frameData = JsonConvert.DeserializeObject<Root2>(val.ToString());
          frames.Add(frameData);
        }

        var fileName = Path.GetFileNameWithoutExtension(json.Meta.Image);

        var dudeAtlas = Texture2DAtlas.Create($"TextureAtlas//{fileName}", img, frames[0].Frame.W, frames[0].Frame.H);
        spriteSheet = new SpriteSheet($"SpriteSheet//{fileName}", dudeAtlas);

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

        //TODO: cache not active here since origin not being calculated otherwise for now
        //m_cache.TryAdd(pngPath, spriteSheet);

        var animSprite = !string.IsNullOrWhiteSpace(initialAnim) ? new AnimatedSprite(spriteSheet, initialAnim) : new AnimatedSprite(spriteSheet);
        animSprite.Origin = new Vector2(frames[0].Frame.W / 2.0f, frames[0].Frame.H / 2.0f);
        return animSprite;
      }
    }
  }
}
