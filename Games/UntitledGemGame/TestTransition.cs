
using System;
using System.Collections.Generic;
using Apos.Tweens;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using MonoGame.Extended.Graphics;
using MonoGame.Extended.Screens.Transitions;
using UntitledGemGame;
using UntitledGemGame.Screens;

public class TestTransition : Transition
{
  private readonly GraphicsDevice _graphicsDevice;

  private readonly SpriteBatch m_spriteBatch;
  private OrthographicCamera m_camera;

  public Color Color { get; }

  private List<HarvesterStruct> m_harvesters = new List<HarvesterStruct>();

  private FloatTween a;
  private FloatTween b;

  public TestTransition(GraphicsDevice graphicsDevice, Color color, OrthographicCamera camera, List<HarvesterStruct> harvesters, float duration = 1f)
      : base(duration)
  {
    Color = color;
    _graphicsDevice = graphicsDevice;
    m_spriteBatch = new SpriteBatch(graphicsDevice);
    m_camera = camera;

    m_harvesters = harvesters;

    // var position = new Vector2Tween(new Vector2(50, 50), new Vector2(200, 200), 2000, Easing.SineIn)
    //     .Wait(1000)
    //     .Offset(new Vector2(-100, 0), 500, Easing.BounceOut)
    //     .Yoyo()
    //     .Loop();

    long tweenDuration = (long)(duration * 1000 * 0.5f);
    a = new FloatTween(m_camera.Zoom, UpgradeManager.UG.CameraZoomScale, tweenDuration, Easing.CubeIn);
    b = new FloatTween(1.0f, 0.0f, tweenDuration, Easing.CircOut);

    StateChanged += (s, e) =>
    {
      AudioManager.Instance.ShipEngineDyingSoundEffect.Play();
    };

    Completed += (s, e) =>
    {
      m_harvesters.Clear();
    };
  }

  public override void Dispose()
  {
    m_spriteBatch.Dispose();
  }

  public override void Draw(GameTime gameTime)
  {
    var effect = EffectCache.BackgroundEffect.Value;

    m_camera.Zoom = a.Value;

    var zoom = m_camera.Zoom;
    m_camera.Zoom = 0.5f * zoom;
    effect.Parameters["view_projection"]?.SetValue(m_camera.GetBoundingFrustum().Matrix);
    m_camera.Zoom = zoom;


    Console.WriteLine($"Zoom: {m_camera.Zoom}, a: {a.Value}, b: {b.Value}");

    var bkg = TextureCache.SpaceBackground.Value;
    var bounds = new Rectangle(TextureCache.SpaceBackground.Value.Bounds.X, TextureCache.SpaceBackground.Value.Bounds.Y,
      TextureCache.SpaceBackground.Value.Bounds.Width * 5, TextureCache.SpaceBackground.Value.Bounds.Height * 5);

    Rectangle size = new Rectangle(-bkg.Width * 5, -bkg.Height * 5, bkg.Width * 10, bkg.Height * 10);

    m_spriteBatch.Begin(effect: effect, depthStencilState: DepthStencilState.Default, samplerState: SamplerState.AnisotropicWrap);
    m_spriteBatch.Draw(TextureCache.SpaceBackground, size, bounds,
        Color.White, 0, new Vector2(0, 0), SpriteEffects.None, 0);
    m_spriteBatch.Draw(TextureCache.SpaceBackground2, size, bounds,
        Color.White, 0, new Vector2(0, 0), SpriteEffects.None, 0);
    m_spriteBatch.Draw(TextureCache.SpaceBackground3, size, bounds,
        Color.White, 0, new Vector2(0, 0), SpriteEffects.None, 0);
    m_spriteBatch.End();

    // Console.WriteLine(a.Value);
    foreach (var harvester in m_harvesters)
    {
      harvester.Transform.Scale = new Vector2(b.Value, b.Value);
      m_spriteBatch.Begin(transformMatrix: m_camera.GetViewMatrix());
      m_spriteBatch.Draw(harvester.AnimatedSprite, harvester.Transform);
      m_spriteBatch.Draw(harvester.Sprite, harvester.Transform);
      m_spriteBatch.End();
    }

    // m_spriteBatch.Begin();
    // FontManager.RenderFieldFont(() => ContentDirectory.Fonts.Roboto_Regular_ttf, "Transitioning...", new Vector2(10, 10), Color.Gold, Color.Black, 128);
    // m_spriteBatch.End();
  }
}
