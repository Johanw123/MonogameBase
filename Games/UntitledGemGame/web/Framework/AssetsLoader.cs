using System;
using System.IO;
using AsepriteDotNet.Aseprite;
using BracketHouse.FontExtension;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Media;

namespace AsyncContent;

// Browser assets are compiled for BlazorGL; raw UI and font files are staged
// into the virtual filesystem by WebBootstrap before GameMain starts.
public sealed class AssetsLoader(GraphicsDevice graphics, ContentManager content)
{
  private static string AssetName(string path)
  {
    path = path.Replace('\\', '/').TrimStart('/');
    int root = path.IndexOf("Content/", StringComparison.Ordinal);
    if (root >= 0) path = path[(root + "Content/".Length)..];
    string extension = Path.GetExtension(path);
    return extension is ".png" or ".fx" or ".mgfx" or ".xnb" or ".wav" or ".ogg"
      ? path[..^extension.Length] : path;
  }
  public Texture2D LoadTexture(string asset, bool forceReload) => content.Load<Texture2D>(AssetName(asset));
  public Effect LoadEffect(string asset, bool forceReload) => content.Load<Effect>(AssetName(asset));
  public SoundEffect LoadSound(string asset, bool forceReload) => content.Load<SoundEffect>(AssetName(asset));
  public Song LoadSong(string asset, bool forceReload) => content.Load<Song>(AssetName(asset));
  public AsepriteFile LoadAsepriteFile(string asset, bool forceReload) => throw new NotSupportedException("Use the game's prebuilt atlases in the browser.");
  public string LoadTextString(string asset, bool forceReload) => File.ReadAllText("Content/" + AssetName(asset));
  public FieldFont LoadFieldFont(string asset, bool forceReload)
  {
    string path = "Content/" + AssetName(asset);
    string name = Path.GetFileNameWithoutExtension(path);
    string directory = Path.Combine(Path.GetDirectoryName(path)!, "GeneratedFonts");
    return FieldFont.FromJsonAndBitmapBytes(Path.Combine(directory, name + "-layout.json"),
      File.ReadAllBytes(Path.Combine(directory, name + "-atlas.png")));
  }
}
