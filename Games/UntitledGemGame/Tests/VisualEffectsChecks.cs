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
    Color[] Render(Effect e, Texture2D texture)
    {
      GraphicsDevice.SetRenderTarget(target);
      GraphicsDevice.Clear(Color.Magenta);
      batch.Begin(blendState: BlendState.Opaque, samplerState: SamplerState.LinearClamp, effect: e);
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
    var fog = Render(background, black);
    Check(fog.Max(c => c.B) - fog.Min(c => c.B) > 10, "Fog must produce structured clouds");
    Set(background, "VisualTime", 15);
    var animated = Render(background, black);
    Check(!animated.SequenceEqual(fog), "Fog must animate");
    Set(background, "FogDensity", 0);
    Check(Render(background, black).All(c => c.R == 0 && c.G == 0 && c.B == 0), "Disabled fog must leave the background untouched");
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
