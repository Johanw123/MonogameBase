
using System;
using System.Collections.Generic;
using Apos.Tweens;
using AsyncContent;
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
  private OrthographicCamera m_camera_background;
  private GpuParticleSystem m_gpuParticles;
  private Effect m_bloomExtractEffect;

  public Color Color { get; }

  private List<HarvesterStruct> m_harvesters = new List<HarvesterStruct>();

  private FloatTween a;
  private FloatTween b;

  public TestTransition(GraphicsDevice graphicsDevice, Color color,
      OrthographicCamera camera, OrthographicCamera camera_background, List<HarvesterStruct> harvesters,
      float duration = 1f, GpuParticleSystem gpuParticles = null)
      : base(duration)
  {
    Color = color;
    _graphicsDevice = graphicsDevice;
    m_spriteBatch = new SpriteBatch(graphicsDevice);
    m_camera = camera;
    m_camera_background = camera_background;
    m_gpuParticles = gpuParticles;

    m_camera.Zoom = 1.5f;
    m_camera_background.Zoom = 1.5f;

    m_harvesters = harvesters;

    long tweenDuration = (long)(duration * 1000 * 0.5f);
    a = new FloatTween(m_camera.Zoom, UpgradeManager.Instance.UG.CameraZoomScale, tweenDuration, Easing.CubeIn);
    b = new FloatTween(1.0f, 0.0f, tweenDuration, Easing.CircOut);

    StateChanged += (s, e) =>
    {
      AudioManager.Instance.PlaySound(SoundEvent.ShipEngineDying);
    };

    Completed += (s, e) =>
    {
      m_harvesters.Clear();

      GameMain.RemoveCustomHudContent(DrawTitle);
    };

    GameMain.AddCustomHudContent(DrawTitle);
  }

  public override void Dispose()
  {
    m_gpuParticles?.Dispose();
    m_gpuParticles = null;
    m_spriteBatch.Dispose();
  }

  float map(float x, float in_min, float in_max, float out_min, float out_max)
  {
    return (x - in_min) * (out_max - out_min) / (in_max - in_min) + out_min;
  }

  private void DrawTitle()
  {
    MainMenu.DrawLogo(m_spriteBatch, _graphicsDevice, Color.White);
  }

  public override void Draw(GameTime gameTime)
  {
    m_gpuParticles?.Update((float)gameTime.ElapsedGameTime.TotalSeconds);
    m_gpuParticles?.Simulate();
    m_bloomExtractEffect ??= AssetManager.Load<Effect>("Shaders/BloomExtract.fx");
    m_bloomExtractEffect?.Parameters["ShipOrangeBloom"]?.SetValue(UntitledGemGameGameScreen.ShipOrangeBloom);

    AudioManager.Instance.Update(gameTime, false);
    m_camera.Zoom = a.Value;
    m_camera_background.Zoom = map(m_camera.Zoom, 0, 3.0f, 0.3f, 1.0f);
    MainMenu.DrawScrollingSpaceBackground(m_spriteBatch, m_camera_background, 0);

    float totalTime = (float)gameTime.TotalGameTime.TotalSeconds;
    // Keep engine/hull order while submitting the fleet as one texture batch.
    m_spriteBatch.Begin(transformMatrix: m_camera.GetViewMatrix(), samplerState: SamplerState.LinearClamp);
    foreach (var harvester in m_harvesters)
    {
      harvester.Transform.Scale = new Vector2(b.Value, b.Value);
      if (!PrerenderedShips.DrawMenuShip(m_spriteBatch, harvester.Type, harvester.Id, harvester.Transform,
        harvester.Sprite.Color, harvester.Sprite.Alpha, totalTime))
      {
        m_spriteBatch.Draw(harvester.AnimatedSprite, harvester.Transform);
        m_spriteBatch.Draw(harvester.Sprite, harvester.Transform);
      }
    }
    m_spriteBatch.End();
    m_gpuParticles?.Draw(m_camera.ViewProjection());

    // var width = GameMain.Instance.GraphicsDevice.Viewport.Width;
    // var height = GameMain.Instance.GraphicsDevice.Viewport.Height;
    //
    // var sprite = TextureCache.Logo;
    //
    // int screenWidth = GameMain.Instance.GraphicsDevice.Viewport.Width;
    //
    // float topMarginPercent = 0.1f;
    // int topMargin = (int)(GameMain.Instance.GraphicsDevice.Viewport.Height * topMarginPercent);
    //
    // float aspectRatio = (float)sprite.Value.Width / sprite.Value.Height;
    // int logoWidth = (int)(screenWidth * 0.6f);
    // int logoHeight = (int)(logoWidth / aspectRatio);
    // int xPosition = (screenWidth - logoWidth) / 2;
    //
    // var destinationRect = new Rectangle(xPosition, topMargin, logoWidth, logoHeight);
    //
    // m_spriteBatch.Begin();
    // m_spriteBatch.Draw(sprite, destinationRect, Color.White);
    // m_spriteBatch.End();



    // m_spriteBatch.Begin();
    // FontManager.RenderFieldFont(() => ContentDirectory.Fonts.Roboto_Regular_ttf, "Transitioning...", new Vector2(10, 10), Color.Gold, Color.Black, 128);
    // m_spriteBatch.End();
  }
}
