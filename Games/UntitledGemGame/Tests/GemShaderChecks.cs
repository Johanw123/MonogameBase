using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using UntitledGemGame;

// Isolated visual comparison and repeatable draw workload; never loads game saves.
internal sealed class GemShaderChecks : Game
{
  private readonly string _beforePath, _afterPath, _texturePath, _output;
  private Texture2D _source = null!, _surface = null!;
  private Effect _before = null!, _after = null!;
  private SpriteBatch _batch = null!;
  private int _frame;

  public GemShaderChecks(string before, string after, string texture, string output)
  {
    (_beforePath, _afterPath, _texturePath, _output) = (before, after, texture, output);
    _ = new GraphicsDeviceManager(this)
    {
      PreferredBackBufferWidth = 1024, PreferredBackBufferHeight = 512,
      SynchronizeWithVerticalRetrace = false
    };
    IsFixedTimeStep = false;
  }

  protected override void LoadContent()
  {
    using var input = File.OpenRead(_texturePath);
    _source = Texture2D.FromStream(GraphicsDevice, input);
    var pixels = new Color[_source.Width * _source.Height];
    _source.GetData(pixels);
    for (int i = 0; i < pixels.Length; ++i)
      pixels[i] = Color.FromNonPremultiplied(pixels[i].ToVector4());
    _source.SetData(pixels);
    var packed = GemSurfaceTexture.Bake(pixels, _source.Width, _source.Height);
    for (int i = 0; i < packed.Length; ++i)
    {
      var p = packed[i];
      if (p.A != pixels[i].A || p.R + p.G > p.A + 1 || p.B > p.A)
        throw new Exception("Surface weights must preserve coverage and premultiplied color.");
      if (p.A == 0 && p != Color.Transparent)
        throw new Exception("Transparent texels must not bleed color or outlines.");
    }
    _surface = GemSurfaceTexture.Create(_source);
    _before = new Effect(GraphicsDevice, File.ReadAllBytes(_beforePath));
    _after = new Effect(GraphicsDevice, File.ReadAllBytes(_afterPath));
    _batch = new SpriteBatch(GraphicsDevice);
    foreach (var effect in new[] { _before, _after })
    {
      effect.Parameters["view_projection"].SetValue(Matrix.CreateOrthographicOffCenter(0, 1024, 512, 0, 0, 1));
      effect.Parameters["TexelSize"]?.SetValue(new Vector2(1f / _source.Width, 1f / _source.Height));
      effect.Parameters["_OutlineColor"]?.SetValue(Vector4.One);
    }
    Directory.CreateDirectory(_output);
    Preview(_before, _source, "before.png");
    Preview(_after, _surface, "after.png");
    Console.WriteLine($"Gem shader previews: {_output}. Rows: scales 0.5, 1, 2, 4; paired columns: ordinary/outlined.");
  }

  private void Preview(Effect effect, Texture2D texture, string name)
  {
    using var target = new RenderTarget2D(GraphicsDevice, 1024, 512);
    GraphicsDevice.SetRenderTarget(target);
    GraphicsDevice.Clear(new Color(12, 16, 24));
    _batch.Begin(effect: effect, samplerState: SamplerState.LinearClamp);
    Color[] colors = [Color.CornflowerBlue, Color.LimeGreen, Color.MediumPurple, Color.Gold];
    float[] scales = [0.5f, 1f, 2f, 4f];
    for (int row = 0; row < scales.Length; ++row)
      for (int column = 0; column < 8; ++column)
      {
        var c = colors[column / 2];
        var tint = new Color(c.R, c.G, c.B, column % 2 == 0 ? (byte)0 : (byte)255);
        _batch.Draw(texture, new Vector2(64 + column * 128, 48 + row * 125), null, tint,
          0, new Vector2(texture.Width, texture.Height) / 2, scales[row], SpriteEffects.None, 0);
      }
    _batch.End();
    GraphicsDevice.SetRenderTarget(null);
    using var file = File.Create(Path.Combine(_output, name));
    target.SaveAsPng(file, target.Width, target.Height);
  }

  protected override void Draw(GameTime gameTime)
  {
    bool optimized = (_frame & 1) != 0;
    GraphicsDevice.Clear(new Color(12, 16, 24));
    _batch.Begin(effect: optimized ? _after : _before, samplerState: SamplerState.LinearClamp);
    for (int i = 0; i < 10000; ++i)
    {
      var tint = new Color(100, 180, 240, i % 5 == 0 ? 255 : 0);
      _batch.Draw(optimized ? _surface : _source,
        new Vector2(i * 37 % 998, i * 71 % 474), tint);
    }
    _batch.End();
    if (++_frame == 40)
    {
      Console.WriteLine("Gem surface checks passed; rendered 20 identical workloads per shader, alternating old/new.");
      Exit();
    }
  }

  protected override void UnloadContent()
  {
    _batch.Dispose(); _before.Dispose(); _after.Dispose();
    _source.Dispose(); _surface.Dispose();
  }
}
