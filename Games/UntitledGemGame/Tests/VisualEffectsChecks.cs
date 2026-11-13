using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using UntitledGemGame;

// Exercise the actual GPU pipeline: neutral identity, post-combine grading,
// bloom disable, temporal fog, opaque output, and portable settings serialization.
internal sealed class VisualEffectsChecks : Game
{
  private readonly string root;
  private bool ran;
  public VisualEffectsChecks(string root)
  {
    this.root = Path.GetFullPath(root);
    _ = new GraphicsDeviceManager(this) { PreferredBackBufferWidth = 128,
      PreferredBackBufferHeight = 96, GraphicsProfile = GraphicsProfile.HiDef };
  }
  protected override void Draw(GameTime gameTime)
  {
    if (ran) return;
    ran = true;
    int count = 0;
    void Check(bool pass, string message)
    {
      if (!pass) throw new Exception(message);
      count++;
    }
    Effect Shader(string name) => new(GraphicsDevice,
      GpuParticleChecks.Compile(Path.Combine(root, "Content/Shaders", name + ".fx")));
    using var combine = Shader("BloomCombine");
    using var extract = Shader("BloomExtract");
    using var background = Shader("BackgroundShader");
    using var scene = new Texture2D(GraphicsDevice, 128, 96);
    using var black = new Texture2D(GraphicsDevice, 128, 96);
    using var target = new RenderTarget2D(GraphicsDevice, 128, 96);
    using var batch = new SpriteBatch(GraphicsDevice);
    var source = Enumerable.Repeat(new Color(80, 120, 180), 128 * 96).ToArray();
    scene.SetData(source);
    black.SetData(Enumerable.Repeat(Color.Black, source.Length).ToArray());
    void Set(Effect e, string key, float value) => e.Parameters[key].SetValue(value);
    Color[] Render(Effect e, Texture2D texture, bool overlay = false)
    {
      GraphicsDevice.SetRenderTarget(target);
      GraphicsDevice.Clear(overlay ? Color.Black : Color.Magenta);
      batch.Begin(blendState: overlay ? BlendState.AlphaBlend : BlendState.Opaque, samplerState: SamplerState.LinearClamp, effect: e);
      batch.Draw(texture, target.Bounds, Color.White);
      batch.End();
      GraphicsDevice.SetRenderTarget(null);
      var result = new Color[source.Length];
      target.GetData(result);
      Check(result.All(c => c.A == 255), "World composition must remain opaque");
      return result;
    }
    combine.Parameters["BaseTexture"].SetValue(scene);
    combine.Parameters["PixelSize"].SetValue(new Vector2(1f / 128, 1f / 96));
    Set(combine, "BaseIntensity", 1); Set(combine, "BaseSaturation", 1);
    Set(combine, "BloomSaturation", 1); Set(combine, "BloomIntensity", 0);
    var identity = Render(combine, black);
    Check(identity.All(c => Math.Abs(c.R - 80) <= 1 && Math.Abs(c.G - 120) <= 1 && Math.Abs(c.B - 180) <= 1),
      "Disabled effects must preserve the original scene");
    // ShadowDusk does not retain HLSL scalar initializers. Set uniforms explicitly,
    // as VisualEffects.Prepare does in the running game.
    Set(combine, "Contrast", 1); Set(combine, "Saturation", 1);
    Set(combine, "VignetteRadius", .35f); Set(combine, "VignetteSoftness", .55f);
    Set(combine, "HaloRadius", 8); Set(combine, "Anamorphic", 1);
    Set(combine, "VisualEnabled", 1);
    var neutral = Render(combine, black);
    Check(neutral.Zip(identity).All(p => Math.Abs(p.First.R - p.Second.R) <= 1
      && Math.Abs(p.First.G - p.Second.G) <= 1 && Math.Abs(p.First.B - p.Second.B) <= 1),
      $"Enabled neutral controls must preserve the scene: {identity[0]} -> {neutral[0]}");
    Set(combine, "VignetteStrength", .7f);
    var vignette = Render(combine, black);
    Check(vignette[0].R < vignette[48 * 128 + 64].R - 20, "Vignette must darken corners, preserving the center");
    Set(combine, "VignetteStrength", 0);
    Set(combine, "Exposure", 1);
    var exposed = Render(combine, black);
    Check(exposed[0].R >= 158, "Exposure must work even with bloom disabled");
    Set(combine, "Exposure", 0);
    Set(combine, "BloomIntensity", 1);
    Set(combine, "HaloMix", .5f);
    Set(combine, "AberrationPixels", 2);
    Set(combine, "SharpenAmount", .5f);
    Set(combine, "LensAmount", -.15f);
    Set(combine, "GrainAmount", .03f);
    _ = Render(combine, scene);
    Set(extract, "BloomThreshold", .34f); Set(extract, "ShipOrangeBloom", 0);
    Set(extract, "SoftBloom", 1); Set(extract, "SoftKnee", .45f);
    var bright = Render(extract, scene);
    Check(bright[0].B > bright[0].G && bright[0].G > bright[0].R && bright[0].R > 0,
      "Soft extraction must preserve color ratios instead of clipping individual channels");
    background.Parameters["view_projection"].SetValue(Matrix.CreateOrthographicOffCenter(0, 128, 96, 0, 0, 1));
    Set(background, "BackgroundBrightness", 1); Set(background, "BackgroundSaturation", 1);
    Set(background, "FogScale", 6); Set(background, "FogSpeed", .035f);
    background.Parameters["FogColor"].SetValue(new Vector3(.1f, .22f, .42f));
    Set(background, "FogDensity", .6f);
    Set(background, "FogCoverage", .8f);
    Check(Render(background, black).All(c => c.R == 0 && c.G == 0 && c.B == 0),
      "Base layers must not bake fog underneath opaque cloud masks");
    background.CurrentTechnique = background.Techniques["FogOverlay"];
    var fog = Render(background, black, overlay: true);
    Check(fog.Max(c => c.B) - fog.Min(c => c.B) > 10, "Fog must produce structured clouds");
    Set(background, "VisualTime", 15);
    var animated = Render(background, black, overlay: true);
    Check(!animated.SequenceEqual(fog), "Fog must animate");
    using var cloudMask = new Texture2D(GraphicsDevice, 128, 96);
    var mask = new Color[source.Length];
    for (int y = 0; y < 96; y++)
      for (int x = 0; x < 128; x++)
        mask[y * 128 + x] = x < 64 ? Color.Transparent : Color.Black;
    cloudMask.SetData(mask);
    Check(Render(background, cloudMask, overlay: true).SequenceEqual(animated),
      "Hard cloud alpha edges must not cut holes in the fog overlay");
    Set(background, "FogDensity", 0);
    Check(Render(background, black, overlay: true).All(c => c.R == 0 && c.G == 0 && c.B == 0), "Disabled fog must leave the background untouched");
    // The railgun's muzzle and channel (RailgunBolt.fx through SdfLineRenderer): both
    // techniques compile, and each lights the pixels its effect should.
    using var railgun = Shader("RailgunBolt");
    Check(railgun.Techniques["RailTrail"] != null && railgun.Techniques["RailMuzzle"] != null,
      "RailgunBolt must offer the trail and muzzle techniques");
    var rail = new SdfLineRenderer(GraphicsDevice, railgun, maxLines: 2) { PulseExtraPadding = 0f, BaseGlowPadding = 30f };
    var railView = Matrix.CreateOrthographicOffCenter(0, 128, 96, 0, 0, 1);
    var railTint = new Color(110, 180, 255);
    Color[] RenderRail(string technique, Action draw)
    {
      railgun.CurrentTechnique = railgun.Techniques[technique];
      GraphicsDevice.SetRenderTarget(target);
      GraphicsDevice.Clear(Color.Black);
      rail.Begin(railView, 1f);
      draw();
      rail.End();
      GraphicsDevice.SetRenderTarget(null);
      var result = new Color[source.Length];
      target.GetData(result);
      return result;
    }
    int Light(Color[] pixels, int x, int y) => pixels[y * 128 + x].R + pixels[y * 128 + x].G + pixels[y * 128 + x].B;
    // A round a tenth of a second into a quarter-second flight from (10, 48) to (120, 48).
    var trail = RenderRail("RailTrail", () => rail.DrawLine(new Vector2(10, 48), new Vector2(120, 48), 1f,
      new Color(0.1f / 2f, 0.3f, 0.25f / 0.5f, 1f), railTint, pulseProgress: 0.4f));
    Check(Light(trail, 30, 48) > 300, "The channel behind the round must flash white");
    Check(Light(trail, 100, 48) < 30, "Nothing may show ahead of the round");
    var windUp = RenderRail("RailMuzzle", () => rail.DrawLine(new Vector2(40, 48), new Vector2(41, 48), 1f,
      new Color(0.9f, 0f, 0f, 1f), railTint));
    Check(Light(windUp, 40, 48) > 300, "The wind-up must gather a star at the muzzle");
    Check(Light(windUp, 110, 48) < 30, "The wind-up stays around the muzzle");
    var shot = RenderRail("RailMuzzle", () => rail.DrawLine(new Vector2(40, 48), new Vector2(41, 48), 1f,
      new Color(0f, 0.02f, 0f, 1f), railTint));
    Check(Light(shot, 40, 48) > 300, "The shot must flash at the muzzle");
    Check(Light(RenderRail("RailMuzzle", () => rail.DrawLine(new Vector2(40, 48), new Vector2(41, 48), 1f,
      new Color(0f, 0f, 0f, 1f), railTint)), 40, 48) == 0, "An idle muzzle draws nothing");
    // No ray or glow is cut off in a straight line: at the height of the flash the light
    // has faded out by the edge of the quad, padded by the reach the game gives it
    // (GameScreen.Railgun.cs RailgunMuzzleReach and RailgunTrailReach), yet still
    // reaches well out from the muzzle and the channel.
    int MaxLight(Color[] pixels, Func<int, int, bool> where)
    {
      int max = 0;
      for (int y = 0; y < 96; y++)
        for (int x = 0; x < 128; x++)
          if (where(x, y)) max = Math.Max(max, Light(pixels, x, y));
      return max;
    }
    const float muzzleScale = 0.25f, trailScale = 0.4f;
    rail.BaseGlowPadding = 180f * muzzleScale;
    float muzzleEdge = muzzleScale + rail.BaseGlowPadding;
    var flash = RenderRail("RailMuzzle", () => rail.DrawLine(new Vector2(64, 48), new Vector2(65, 48), muzzleScale,
      new Color(0f, 0.02f, 0f, 1f), railTint));
    float Square(int x, int y) => Math.Max(Math.Abs(x + 0.5f - 64.5f), Math.Abs(y + 0.5f - 48f));
    Check(MaxLight(flash, (x, y) => Square(x, y) >= muzzleEdge - 2f) <= 1,
      "The shot's star must fade out before the edge of its quad");
    Check(MaxLight(flash, (x, y) => Math.Abs(Square(x, y) - muzzleEdge * 0.5f) < 1f) > 60,
      "The shot's star must still reach out from the muzzle");
    rail.BaseGlowPadding = 56f * trailScale;
    float trailEdge = trailScale + rail.BaseGlowPadding;
    var landed = RenderRail("RailTrail", () => rail.DrawLine(new Vector2(14, 48), new Vector2(114, 48), trailScale,
      new Color(0.05f / 2f, 0.3f, 0.04f / 0.5f, 1f), railTint, pulseProgress: 1f));
    Check(MaxLight(landed, (x, y) => Math.Abs(y + 0.5f - 48f) >= trailEdge - 2f) <= 1,
      "The channel's flash and impact must fade out before the edge of their quad");
    Check(MaxLight(landed, (x, y) => Math.Abs(Math.Abs(y + 0.5f - 48f) - trailEdge * 0.4f) < 1f) > 20,
      "The channel's glow must still reach out from the line of fire");
    VisualEffects.Recommended();
    VisualEffects.Settings.Grain = true;
    VisualEffects.Settings.Exposure = .7f;
    using var json = new MemoryStream(Encoding.UTF8.GetBytes(VisualEffects.ToJson()));
    var restored = VisualEffects.Read(json);
    Check(restored.Grain && restored.Exposure == .7f && restored.FogDensity == .16f,
      "Settings must round-trip without reflection");
    restored.VignetteSoftness = 0;
    bool rejected = false;
    try { restored.Validate(); } catch (InvalidDataException) { rejected = true; }
    Check(rejected, "Settings must reject invalid shader divisors");
    Console.WriteLine($"Visual effects: {count} checks passed.");
    Exit();
  }
}
