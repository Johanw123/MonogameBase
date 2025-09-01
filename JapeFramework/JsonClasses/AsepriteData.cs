using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using MonoGame.Extended;

namespace JapeFramework.JsonClasses
{
  internal class AsepriteData
  {
  }

  public class Root
  {
    public Root(
      object frames,
      Meta meta
    )
    {
      this.Frames = frames;
      this.Meta = meta;
    }
    //public List<object> Frames { get; set; }
    [JsonProperty("meta")]
    [JsonPropertyName("meta")]
    public Meta Meta { get; }

    [JsonProperty("frames")]
    [JsonPropertyName("frames")]
    public object Frames { get; }
  }

  public class Layer
  {
    public Layer(
      string name,
      int opacity,
      string blendMode
    )
    {
      this.Name = name;
      this.Opacity = opacity;
      this.BlendMode = blendMode;
    }

    [JsonProperty("name")]
    [JsonPropertyName("name")]
    public string Name { get; }

    [JsonProperty("opacity")]
    [JsonPropertyName("opacity")]
    public int Opacity { get; }

    [JsonProperty("blendMode")]
    [JsonPropertyName("blendMode")]
    public string BlendMode { get; }
  }

  public class Meta
  {
    public Meta(
      string app,
      string version,
      string image,
      string format,
      Size size,
      string scale,
      List<FrameTag> frameTags,
      List<Layer> layers,
      List<object> slices
    )
    {
      this.App = app;
      this.Version = version;
      this.Image = image;
      this.Format = format;
      this.Size = size;
      this.Scale = scale;
      this.FrameTags = frameTags;
      this.Layers = layers;
      this.Slices = slices;
    }

    [JsonProperty("app")]
    [JsonPropertyName("app")]
    public string App { get; }

    [JsonProperty("version")]
    [JsonPropertyName("version")]
    public string Version { get; }

    [JsonProperty("image")]
    [JsonPropertyName("image")]
    public string Image { get; }

    [JsonProperty("format")]
    [JsonPropertyName("format")]
    public string Format { get; }

    [JsonProperty("size")]
    [JsonPropertyName("size")]
    public Size Size { get; }

    [JsonProperty("scale")]
    [JsonPropertyName("scale")]
    public string Scale { get; }

    [JsonProperty("frameTags")]
    [JsonPropertyName("frameTags")]
    public IReadOnlyList<FrameTag> FrameTags { get; }

    [JsonProperty("layers")]
    [JsonPropertyName("layers")]
    public IReadOnlyList<Layer> Layers { get; }

    [JsonProperty("slices")]
    [JsonPropertyName("slices")]
    public IReadOnlyList<object> Slices { get; }
  }

  public class FrameTag
  {
    public FrameTag(
      string name,
      int from,
      int to,
      string direction,
      string color
    )
    {
      this.Name = name;
      this.From = from;
      this.To = to;
      this.Direction = direction;
      this.Color = color;
    }

    [JsonProperty("name")]
    [JsonPropertyName("name")]
    public string Name { get; }

    [JsonProperty("from")]
    [JsonPropertyName("from")]
    public int From { get; }

    [JsonProperty("to")]
    [JsonPropertyName("to")]
    public int To { get; }

    [JsonProperty("direction")]
    [JsonPropertyName("direction")]
    public string Direction { get; }

    [JsonProperty("color")]
    [JsonPropertyName("color")]
    public string Color { get; }
  }

  public class SourceSize
  {
    public SourceSize(
      int w,
      int h
    )
    {
      this.W = w;
      this.H = h;
    }

    [JsonProperty("w")]
    [JsonPropertyName("w")]
    public int W { get; }

    [JsonProperty("h")]
    [JsonPropertyName("h")]
    public int H { get; }
  }

  public class SpriteSourceSize
  {
    public SpriteSourceSize(
      int x,
      int y,
      int w,
      int h
    )
    {
      this.X = x;
      this.Y = y;
      this.W = w;
      this.H = h;
    }

    [JsonProperty("x")]
    [JsonPropertyName("x")]
    public int X { get; }

    [JsonProperty("y")]
    [JsonPropertyName("y")]
    public int Y { get; }

    [JsonProperty("w")]
    [JsonPropertyName("w")]
    public int W { get; }

    [JsonProperty("h")]
    [JsonPropertyName("h")]
    public int H { get; }
  }


  public class Frame
  {
    public Frame(
      int x,
      int y,
      int w,
      int h
    )
    {
      this.X = x;
      this.Y = y;
      this.W = w;
      this.H = h;
    }

    [JsonProperty("x")]
    [JsonPropertyName("x")]
    public int X { get; }

    [JsonProperty("y")]
    [JsonPropertyName("y")]
    public int Y { get; }

    [JsonProperty("w")]
    [JsonPropertyName("w")]
    public int W { get; }

    [JsonProperty("h")]
    [JsonPropertyName("h")]
    public int H { get; }
  }

  public class Root2
  {
    public Root2(
      Frame frame,
      bool rotated,
      bool trimmed,
      SpriteSourceSize spriteSourceSize,
      SourceSize sourceSize,
      int duration
    )
    {
      this.Frame = frame;
      this.Rotated = rotated;
      this.Trimmed = trimmed;
      this.SpriteSourceSize = spriteSourceSize;
      this.SourceSize = sourceSize;
      this.Duration = duration;
    }

    [JsonProperty("frame")]
    [JsonPropertyName("frame")]
    public Frame Frame { get; }

    [JsonProperty("rotated")]
    [JsonPropertyName("rotated")]
    public bool Rotated { get; }

    [JsonProperty("trimmed")]
    [JsonPropertyName("trimmed")]
    public bool Trimmed { get; }

    [JsonProperty("spriteSourceSize")]
    [JsonPropertyName("spriteSourceSize")]
    public SpriteSourceSize SpriteSourceSize { get; }

    [JsonProperty("sourceSize")]
    [JsonPropertyName("sourceSize")]
    public SourceSize SourceSize { get; }

    [JsonProperty("duration")]
    [JsonPropertyName("duration")]
    public int Duration { get; }
  }
}
