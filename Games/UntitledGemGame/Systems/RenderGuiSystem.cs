
using AsyncContent;
using Base;
using Gum.Forms;
using Gum.Wireframe;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using MonoGame.Extended.ECS;
using MonoGame.Extended.ECS.Systems;
using MonoGame.Extended.Graphics;
using MonoGame.Extended.Input;
using MonoGameGum;
using RenderingLibrary;
using RenderingLibrary.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using UntitledGemGame;
using UntitledGemGame.Entities;

public class RenderGuiSystem
{
  private readonly SpriteBatch _spriteBatch;
  private readonly GraphicsDevice _graphicsDevice;
  private OrthographicCamera m_camera;
  private GumService Gum => GumService.Default;

  public static Layer m_upgradesLayer;

  private BasicEffect _simpleEffect;



  public bool drawUpgradesGui = false;

  public static List<GraphicalUiElement> itemsToUpdate = new List<GraphicalUiElement>();

  private AsyncAsset<Effect> blurEffect;
  // private Texture2D spaceBackground;

  public RenderGuiSystem(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, OrthographicCamera camera, GumService gumService)
  {
    _spriteBatch = spriteBatch;
    _graphicsDevice = graphicsDevice;
    m_camera = camera;

    blurEffect = AssetManager.LoadAsync<Effect>("Shaders/BlurShader.fx");
    // spaceBackground = AssetManager.Load<Texture2D>(ContentDirectory.Textures.MarkIII_Woods_png);
    // spaceBackgroundDepth = AssetManager.Load<Texture2D>(ContentDirectory.Textures.result_upscaled_png);

    _simpleEffect = new BasicEffect(_graphicsDevice);
    _simpleEffect.TextureEnabled = true;

    itemsToUpdate.Add(Gum.Root);
    itemsToUpdate.Add(Gum.ModalRoot);

    m_upgradesLayer = new Layer()
    {
      Name = "UpgradesLayer",
    };

    Gum.Renderer.AddLayer(m_upgradesLayer);
  }

  public void Update(GameTime gameTime)
  {
    var state = MouseExtended.GetState();
    var keyboardState = KeyboardExtended.GetState();

    var camera = SystemManagers.Default.Renderer.Camera;

    if (keyboardState.WasKeyPressed(Microsoft.Xna.Framework.Input.Keys.F1))
    {
      drawUpgradesGui = !drawUpgradesGui;
    }

    if (state.DeltaScrollWheelValue > 10)
    {
      camera.Zoom *= 1.01f;
    }
    else if (state.DeltaScrollWheelValue < -10)
    {
      camera.Zoom *= 0.99f;
    }

    if (state.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed)
    {
      var delta = state.DeltaPosition;
      camera.Position = new System.Numerics.Vector2(
        Math.Clamp(camera.Position.X + delta.X / camera.Zoom, -500, 500),
        Math.Clamp(camera.Position.Y + delta.Y / camera.Zoom, -500, 500)
      );
    }

    // Renderer.ApplyCameraZoomOnWorldTranslation = true;
    // // Renderer.UseBasicEffectRendering = false;
    // Gum.Renderer.Camera.Zoom = m_upgradesLayer.LayerCameraSettings.Zoom.Value;
    // Gum.Renderer.Camera.Position = m_upgradesLayer.LayerCameraSettings.Position.Value;
    //

    // foreach (var item in itemsToUpdate)
    // {
    //   item.UpdateLayout();
    // }

    // GraphicalUiElement.CanvasWidth = GameMain.Instance.GraphicsDevice.PresentationParameters.BackBufferWidth / camera.Zoom;
    // GraphicalUiElement.CanvasHeight = GameMain.Instance.GraphicsDevice.PresentationParameters.BackBufferHeight / camera.Zoom;

    Gum.Update(GameMain.Instance, gameTime, itemsToUpdate);
  }
  float ComputeGaussian(float n)
  {
    float theta = 10;
    return (float)((1.0 / Math.Sqrt(2 * Math.PI * theta)) *
                    Math.Exp(-(n * n) / (2 * theta * theta)));
  }

  void SetBlurEffectParameters(float dx, float dy)
  {
    // Look up the sample weight and offset effect parameters.
    EffectParameter weightsParameter, offsetsParameter;

    weightsParameter = blurEffect.Value.Parameters["SampleWeights"];
    offsetsParameter = blurEffect.Value.Parameters["SampleOffsets"];

    if (weightsParameter == null || offsetsParameter == null)
      return;

    // Look up how many samples our gaussian blur effect supports.
    int sampleCount = weightsParameter.Elements.Count;

    // Create temporary arrays for computing our filter settings.
    float[] sampleWeights = new float[sampleCount];
    Vector2[] sampleOffsets = new Vector2[sampleCount];

    // The first sample always has a zero offset.
    sampleWeights[0] = ComputeGaussian(0);
    sampleOffsets[0] = new Vector2(0);

    // Maintain a sum of all the weighting values.
    float totalWeights = sampleWeights[0];

    // Add pairs of additional sample taps, positioned along a line in both directions from the center.
    for (int i = 0; i < sampleCount / 2; i++)
    {
      // Store weights for the positive and negative taps.
      float weight = ComputeGaussian(i + 1);

      sampleWeights[i * 2 + 1] = weight;
      sampleWeights[i * 2 + 2] = weight;

      totalWeights += weight * 2;

      // To get the maximum amount of blurring from a limited number of pixel shader samples, we take advantage of the bilinear filtering
      // hardware inside the texture fetch unit. If we position our texture coordinates exactly halfway between two texels, the filtering unit
      // will average them for us, giving two samples for the price of one. This allows us to step in units of two texels per sample, rather
      // than just one at a time. The 1.5 offset kicks things off by positioning us nicely in between two texels.
      float sampleOffset = i * 2 + 1.5f;

      Vector2 delta = new Vector2(dx, dy) * sampleOffset;

      // Store texture coordinate offsets for the positive and negative taps.
      sampleOffsets[i * 2 + 1] = delta;
      sampleOffsets[i * 2 + 2] = -delta;
    }

    // Normalize the list of sample weightings, so they will always sum to one.
    for (int i = 0; i < sampleWeights.Length; i++)
    {
      sampleWeights[i] /= totalWeights;
    }

    weightsParameter.SetValue(sampleWeights);  // Tell the effect about our new filter settings.
    offsetsParameter.SetValue(sampleOffsets);
  }

  public void Draw()
  {
    if (drawUpgradesGui)
    {
      if (blurEffect.IsLoaded && !blurEffect.IsFailed)
      {
        blurEffect.Value.Parameters["view_projection"]?.SetValue(m_camera.GetBoundingFrustum().Matrix);
        blurEffect.Value.Parameters["xResolution"]?.SetValue(new Vector2(_graphicsDevice.Viewport.Width, _graphicsDevice.Viewport.Height));

        SetBlurEffectParameters(1f / _graphicsDevice.Viewport.Width, 0);
      }

      _spriteBatch.Begin(effect: blurEffect, blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointClamp);
      _spriteBatch.Draw(BaseGame.renderTarget2, new Rectangle(0, 0, GameMain.Instance.GraphicsDevice.PresentationParameters.BackBufferWidth, GameMain.Instance.GraphicsDevice.PresentationParameters.BackBufferHeight),
          Color.White);
      _spriteBatch.End();

      _spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointClamp);
      _spriteBatch.Draw(AssetManager.DefaultTexture, new Rectangle(0, 0, GameMain.Instance.GraphicsDevice.PresentationParameters.BackBufferWidth, GameMain.Instance.GraphicsDevice.PresentationParameters.BackBufferHeight)
          , Color.Black * 0.7f);
      _spriteBatch.End();

      SystemManagers.Default.Renderer.Draw(SystemManagers.Default, m_upgradesLayer);
    }
    else
    {
      SystemManagers.Default.Renderer.Draw(SystemManagers.Default, Gum.Renderer.MainLayer);
    }
  }
}
