using System.Reflection;
using AsyncContent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using UntitledGemGame;
using UntitledGemGame.Entities;

// Render the real ship/engine draw path without starting gameplay or touching saves.
internal sealed class ThrusterPreview : Game
{
  private readonly string root, output;
  private bool rendered;

  public ThrusterPreview(string root, string output)
  {
    this.root = Path.GetFullPath(root);
    this.output = Path.GetFullPath(output);
    Content.RootDirectory = Path.Combine(this.root, "bin/Debug/net10.0/Content");
    _ = new GraphicsDeviceManager(this) { PreferredBackBufferWidth = 960, PreferredBackBufferHeight = 640 };
  }

  protected override void Draw(GameTime gameTime)
  {
    if (rendered) return;
    rendered = true;
    var texture = Content.Load<Texture2D>("Atlases/fleet");
    var field = typeof(TextureCache).GetField("fleet", BindingFlags.Static | BindingFlags.NonPublic)!;
    var oldFleet = field.GetValue(null);
    var oldTexture = TextureCache.FleetTexture;
    try
    {
      field.SetValue(null, new FleetAtlas(texture, File.ReadAllText(Path.Combine(root, "Content/Atlases/fleet.json"))));
      TextureCache.FleetTexture = new AsyncAsset<Texture2D> { Value = texture, IsLoaded = true };
      using var target = new RenderTarget2D(GraphicsDevice, 960, 640);
      using var batch = new SpriteBatch(GraphicsDevice);
      GraphicsDevice.SetRenderTarget(target);
      GraphicsDevice.Clear(new Color(4, 9, 18));
      batch.Begin(samplerState: SamplerState.LinearClamp);
      for (int i = 0; i < 3; i++)
        if (!PrerenderedShips.DrawMenuShip(batch, Harvester.HarvesterType.HomeBase, 0,
          new Transform2(new Vector2(160 + i * 320, 230), 0, new Vector2(2.4f)),
          Color.White, 1f, i * 0.31f, boost: 1.08f))
          throw new Exception("Thruster preview failed to draw the home base.");
      batch.End();
      GraphicsDevice.SetRenderTarget(null);
      Directory.CreateDirectory(Path.GetDirectoryName(output)!);
      using var file = File.Create(output);
      target.SaveAsPng(file, target.Width, target.Height);
      Console.WriteLine($"Thruster preview saved: {output}");
    }
    finally
    {
      field.SetValue(null, oldFleet);
      TextureCache.FleetTexture = oldTexture;
    }
    Exit();
  }
}
