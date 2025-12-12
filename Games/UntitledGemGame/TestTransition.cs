
using Apos.Tweens;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using MonoGame.Extended.Screens.Transitions;
using UntitledGemGame;

public class TestTransition : Transition
{
  private readonly GraphicsDevice _graphicsDevice;

  private readonly SpriteBatch m_spriteBatch;
  private OrthographicCamera m_camera;

  public Color Color { get; }

  private FloatTween a;

  public TestTransition(GraphicsDevice graphicsDevice, Color color, OrthographicCamera camera, float duration = 1f)
      : base(duration)
  {
    Color = color;
    _graphicsDevice = graphicsDevice;
    m_spriteBatch = new SpriteBatch(graphicsDevice);
    m_camera = camera;

    // var position = new Vector2Tween(new Vector2(50, 50), new Vector2(200, 200), 2000, Easing.SineIn)
    //     .Wait(1000)
    //     .Offset(new Vector2(-100, 0), 500, Easing.BounceOut)
    //     .Yoyo()
    //     .Loop();

    a = new FloatTween(m_camera.Zoom, UpgradeManager.UG.CameraZoomScale, (long)(duration * 1000 * 0.5f), Easing.CubeIn);
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

    // m_spriteBatch.Begin();
    // FontManager.RenderFieldFont(() => ContentDirectory.Fonts.Roboto_Regular_ttf, "Transitioning...", new Vector2(10, 10), Color.Gold, Color.Black, 128);
    // m_spriteBatch.End();
  }
}
