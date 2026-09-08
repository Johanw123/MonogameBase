using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using MonoGame.Extended.Graphics;
using UntitledGemGame;

// Standalone offscreen graphics check. Creates no game state and never reads/writes saves.
internal sealed class RenderChecks : Game
{
  private readonly string _shaderPath;
  private bool _ran;
  public RenderChecks(string shaderPath)
  {
    _shaderPath = shaderPath;
    _ = new GraphicsDeviceManager(this)
    {
      PreferredBackBufferWidth = 256,
      PreferredBackBufferHeight = 256,
      SynchronizeWithVerticalRetrace = false
    };
    IsFixedTimeStep = false;
  }

  protected override void Draw(GameTime gameTime)
  {
    if (_ran) return;
    _ran = true;
    using var texture = new Texture2D(GraphicsDevice, 16, 16);
    var pixels = new Color[256];
    for (int y = 0; y < 16; ++y)
      for (int x = 0; x < 16; ++x)
      {
        byte value = (byte)(40 + (x * 11 + y * 7) % 210);
        byte alpha = x < 2 || y < 2 || x > 13 || y > 13 ? (byte)0 : (byte)255;
        pixels[y * 16 + x] = new Color(value, value, value, alpha);
      }
    texture.SetData(pixels);
    using var effect = new Effect(GraphicsDevice, File.ReadAllBytes(_shaderPath));
    effect.Parameters["view_projection"].SetValue(Matrix.CreateOrthographicOffCenter(0, 256, 256, 0, 0, 1));
    effect.Parameters["TexelSize"]?.SetValue(new Vector2(1f / 16));
    effect.Parameters["_OutlineColor"]?.SetValue(Vector4.One);
    using var spriteBatch = new SpriteBatch(GraphicsDevice);
    using var cache = new GemRenderBatch(GraphicsDevice);
    using var target = new RenderTarget2D(GraphicsDevice, 256, 256);
    var reference = new List<(int Id, Sprite Sprite, Transform2 Transform)>();
    for (int i = 0; i < 4100; ++i)
    {
      var sprite = new Sprite(new Texture2DRegion(texture, 2, 1, 12, 14))
      {
        Origin = new Vector2(5, 7),
        Color = new Color(130, 210, 90, i % 2 == 0 ? 255 : 0),
        Effect = (SpriteEffects)(i % 4)
      };
      bool visible = i < 9 || i >= 4096;
      var transform = new Transform2(visible ? new Vector2(25 + (i % 5) * 45, 30 + (i % 4) * 50)
        : new Vector2(3000, 3000), i * 0.13f, new Vector2(0.5f + i % 3, 0.75f + i % 2));
      reference.Add((i, sprite, transform));
      cache.Add(i, sprite, transform);
    }

    void Compare(string stage)
    {
      GraphicsDevice.SetRenderTarget(target);
      GraphicsDevice.Clear(new Color(10, 15, 25));
      spriteBatch.Begin(effect: effect, samplerState: SamplerState.LinearClamp);
      foreach (var item in reference) spriteBatch.Draw(item.Sprite, item.Transform);
      spriteBatch.End();
      GraphicsDevice.SetRenderTarget(null);
      var expected = new Color[256 * 256];
      target.GetData(expected);

      GraphicsDevice.SetRenderTarget(target);
      GraphicsDevice.Clear(new Color(10, 15, 25));
      cache.Draw(effect, texture);
      GraphicsDevice.SetRenderTarget(null);
      var actual = new Color[expected.Length];
      target.GetData(actual);
      int changed = 0, maximumDifference = 0;
      for (int i = 0; i < actual.Length; ++i)
      {
        int delta = Math.Max(Math.Abs(actual[i].R - expected[i].R),
          Math.Max(Math.Abs(actual[i].G - expected[i].G), Math.Abs(actual[i].B - expected[i].B)));
        maximumDifference = Math.Max(delta, maximumDifference);
        if (delta > 2) ++changed;
      }
      if (changed > 8)
        throw new Exception($"{stage}: cached rendering differs in {changed} pixels (max channel delta {maximumDifference})");
      Console.WriteLine($"Render check {stage}: {changed} pixels differ by >2/255; uploaded pages {cache.UploadedPagesLastFrame}.");
    }

    Compare("initial two-page scene");
    if (cache.UploadedPagesLastFrame != 2) throw new Exception("Initial pages were not uploaded");
    Compare("unchanged frame");
    if (cache.UploadedPagesLastFrame != 0) throw new Exception("Idle gems uploaded geometry");
    reference[0].Transform.Position += new Vector2(7, -3);
    reference[0].Transform.Scale *= 1.5f;
    reference[0].Sprite.Color = new Color(200, 70, 160, 255);
    reference[4096].Transform.Rotation += 0.4f;
    cache.Update(0);
    cache.Update(4096);
    Compare("movement, growth, tint and rotation");
    if (cache.UploadedPagesLastFrame != 2) throw new Exception("Changed pages were not uploaded");
    cache.Remove(reference[2].Id);
    reference.RemoveAt(2);
    Compare("stable removal across page boundaries");
    foreach (var item in reference) cache.Remove(item.Id);
    reference.Clear();
    Compare("empty batch");
    var reused = new Sprite(texture) { Origin = new Vector2(8), Color = Color.White };
    var reusedTransform = new Transform2(new Vector2(128), 0.3f, new Vector2(4));
    reference.Add((7, reused, reusedTransform));
    cache.Add(7, reused, reusedTransform);
    Compare("reused batch");

    cache.Remove(7);
    reference.Clear();
    for (int i = 0; i < 4; ++i)
    {
      var layeredSprite = new Sprite(texture)
      {
        Origin = new Vector2(8),
        Color = i switch { 1 => Color.Red, 2 => Color.Green, _ => Color.Blue }
      };
      var layeredTransform = new Transform2(i == 0 ? new Vector2(3000) : new Vector2(128),
        0, new Vector2(5));
      reference.Add((i, layeredSprite, layeredTransform));
      cache.Add(i, layeredSprite, layeredTransform);
    }
    Compare("overlapping gems before collection");
    // Removing an unrelated gem must not bring a different gem to the front.
    cache.Remove(0);
    reference.RemoveAt(0);
    Compare("unrelated collection preserves overlapping gems");

    for (int frame = 0; frame < 12; ++frame)
    {
      // Exercise several removals before one draw, appending new gems, and
      // reusing the same IDs/sprites while old render slots await compaction.
      for (int pickup = 0; pickup < 2; ++pickup)
      {
        int slot = (frame + pickup) % reference.Count;
        var retired = reference[slot];
        cache.Remove(retired.Id);
        cache.Remove(retired.Id); // Deferred ECS cleanup can repeat a removal.
        reference.RemoveAt(slot);
        retired.Sprite.Color = new Color(30 + frame * 12, 200 - frame * 9, 150, 255);
        var spawnTransform = new Transform2(new Vector2(105 + pickup * 20, 125 + frame),
          frame * 0.1f, new Vector2(0.2f + frame * 0.3f));
        reference.Add((retired.Id, retired.Sprite, spawnTransform));
        cache.Add(retired.Id, retired.Sprite, spawnTransform);
      }
      reference[0].Transform.Position += new Vector2(2, -1);
      cache.Update(reference[0].Id);
      Compare($"collection and pooled respawn {frame + 1}");
      if (cache.Count != reference.Count) throw new Exception("Collection/spawning lost a render entry");
    }
    Console.WriteLine("Offscreen render comparisons passed.");
    Exit();
  }
}
