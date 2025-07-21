using System.Collections.Generic;
using System.IO;
using System.Linq;
using FontExtension;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoMSDF.Text
{
  public sealed class TextRenderer
  {
    private const string LargeTextTechnique = "LargeText";
    private const string SmallTextTechnique = "SmallText";

    private readonly Effect Effect;
    private readonly FieldFont Font;
    private readonly GraphicsDevice Device;
    private readonly Quad Quad;
    private readonly Dictionary<char, GlyphRenderInfo> Cache;

    public TextRenderer(Effect effect, FieldFont font, GraphicsDevice device)
    {
      this.Effect = effect;
      this.Font = font;
      this.Device = device;

      this.Quad = new Quad();
      this.Cache = new Dictionary<char, GlyphRenderInfo>();

      this.ForegroundColor = Color.White;
      this.EnableKerning = true;
      this.OptimizeForTinyText = false;
    }


    public class DrawOptions
    {
      public Vector2 RenderTransformOrigin = Vector2.Zero;





    }

    public Color ForegroundColor { get; set; }
    public bool EnableKerning { get; set; }


    public float GetTextHeight(string text, float scale)
    {
      var sequence = text.Select(GetRenderInfo).ToArray();

      return sequence.Max(s => s.Texture.Height * (1.0f / s.Metrics.Scale));
    }

    public Vector2 GetScreenCoords(Vector2 pos)
    {
      var view = Matrix.CreateLookAt(Vector3.Backward, Vector3.Forward, Vector3.Up);
      //Viewport viewport = this._viewportAdapter.Viewport;
      return Vector2.Transform(pos + new Vector2(Device.Viewport.X, Device.Viewport.Y), view);
    }

    //public Vector2 GetScreenCoords(Vector2 pos)
    //{
    //  Matrix projectionMatrix = Matrix.CreateOrthographic(Device.Viewport.Width, Device.Viewport.Height, 0.01f, 1000.0f);
    //  var viewMatrix = Matrix.CreateLookAt(Vector3.Backward, Vector3.Forward, Vector3.Up);
    //  var worldPos = new Vector4(pos.X, pos.Y, 1.0f, 1.0f);
      
    //  var clipSpacePos = projectionMatrix * (viewMatrix * worldPos);
    //}


    public float GetTextWidth(string text, float scale)
    {
      if (string.IsNullOrEmpty(text))
        return 0.0f;

      var sequence = text.Select(GetRenderInfo).ToArray();
      var textureWidth = sequence[0].Texture.Width;
      var textureHeight = sequence[0].Texture.Height;


      var stringLength = 0.0f;

      var pen = Vector2.Zero /** (1.0f / scale)*/;

      float lastRight = 0;

      for (var i = 0; i < sequence.Length; i++)
      {
        var current = sequence[i];

        this.Effect.Parameters["GlyphTexture"].SetValue(current.Texture);
        this.Effect.CurrentTechnique.Passes[0].Apply();

        var glyphHeight = textureHeight * (1.0f / current.Metrics.Scale);
        var glyphWidth = textureWidth * (1.0f / current.Metrics.Scale);

        var height = GetTextHeight("aiwudhiawdhwiaud", scale);

        //if (pen.Y == 0)
        //  pen.Y -= height * (scale);

        var left = pen.X - current.Metrics.Translation.X * scale;
        var bottom = pen.Y - current.Metrics.Translation.Y * scale;

        var right = left + glyphWidth * scale;
        var top = bottom + glyphHeight * scale;

        if (!char.IsWhiteSpace(current.Character))
        {
          //this.Quad.Render(this.Device, new Vector2(left, bottom), new Vector2(right, top));
        }

        lastRight = right;

        pen.X += current.Metrics.Advance * scale;

        stringLength += glyphWidth;

        if (this.EnableKerning && i < sequence.Length - 1)
        {
          var next = sequence[i + 1];

          var pair = this.Font.KerningPairs.FirstOrDefault(
            x => x.Left == current.Character && x.Right == next.Character);

          if (pair != null)
          {
            pen.X += pair.Advance * scale;
          }
        }
      }

      return lastRight;
      //return GetScreenCoords(GetScreenCoords(new Vector2(lastRight, 0))).X;
    }

    //public float GetTextWidth(string text, Matrix worldViewProjection)
    //{
    //  var sequence = text.Select(GetRenderInfo).ToArray();

    //  return sequence.Max(s => s.Texture.Width);
    //}

    /// <summary>
    /// Disables text anti-aliasing which might cause blurry text when the text is rendered tiny
    /// </summary>
    public bool OptimizeForTinyText { get; set; }

    public float Render(string text, Matrix worldViewProjection, float scale, Vector2 offset, Color textColor)
    {
      if (string.IsNullOrEmpty(text))
        return 0.0f;

      var sequence = text.Select(GetRenderInfo).ToArray();
      var textureWidth = sequence[0].Texture.Width;
      var textureHeight = sequence[0].Texture.Height;

      this.Effect.Parameters["WorldViewProjection"].SetValue(worldViewProjection);
      this.Effect.Parameters["PxRange"].SetValue(this.Font.PxRange);
      this.Effect.Parameters["TextureSize"].SetValue(new Vector2(textureWidth, textureHeight));
      //this.Effect.Parameters["ForegroundColor"].SetValue(textColor.ToVector4());

      if (this.OptimizeForTinyText)
      {
        this.Effect.CurrentTechnique = this.Effect.Techniques[SmallTextTechnique];
      }
      else
      {
        this.Effect.CurrentTechnique = this.Effect.Techniques[LargeTextTechnique];
      }

      var stringLength = 0.0f;

      var pen = Vector2.Zero + offset /** (1.0f / scale)*/;

      float lastRight = 0;

      for (var i = 0; i < sequence.Length; i++)
      {
        var current = sequence[i];

        this.Effect.Parameters["GlyphTexture"].SetValue(current.Texture);
        this.Effect.CurrentTechnique.Passes[0].Apply();

        var glyphHeight = textureHeight * (1.0f / current.Metrics.Scale);
        var glyphWidth = textureWidth * (1.0f / current.Metrics.Scale);

        var height = GetTextHeight("aiwudhiawdhwiaud", scale);

        //if (pen.Y == 0)
        //  pen.Y -= height * (scale);

        var hej = textureWidth - glyphWidth;

        var left = pen.X - current.Metrics.Translation.X * scale;
        var bottom = pen.Y - current.Metrics.Translation.Y * scale;

        var right = left + glyphWidth * scale;
        var top = bottom + glyphHeight * scale;
        
        if (!char.IsWhiteSpace(current.Character))
        {
          this.Quad.Render(this.Device, new Vector2(left, bottom), new Vector2(right, top));
          //this.Quad.Render(this.Device, new Vector2(-Device.Viewport.Width / 2.0f, -Device.Viewport.Height / 2.0f), new Vector2(Device.Viewport.Width / 2.0f, Device.Viewport.Height / 2.0f));
        }

        lastRight = right * scale;

        pen.X += current.Metrics.Advance * scale;
        
        stringLength += glyphWidth;

        if (this.EnableKerning && i < sequence.Length - 1)
        {
          var next = sequence[i + 1];

          var pair = this.Font.KerningPairs.FirstOrDefault(
            x => x.Left == current.Character && x.Right == next.Character);

          if (pair != null)
          {
            pen.X += pair.Advance * scale;
          }
        }
      }

      return lastRight;
    }

    private GlyphRenderInfo GetRenderInfo(char c)
    {
      if (this.Cache.TryGetValue(c, out var value))
      {
        return value;
      }

      var unit = LoadRenderInfo(c);
      this.Cache.Add(c, unit);
      return unit;
    }

    private GlyphRenderInfo LoadRenderInfo(char c)
    {
      var glyph = this.Font.GetGlyph(c);
      using (var stream = new MemoryStream(glyph.Bitmap))
      {
        var texture = Texture2D.FromStream(this.Device, stream);
        var unit = new GlyphRenderInfo(c, texture, glyph.Metrics);


        return unit;
      }
    }
  }
}
