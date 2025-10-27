
using AsyncContent;
using Gum.Forms;
using Gum.Wireframe;
using ImGuiNET;
using JapeFramework;
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

    targetZoom = SystemManagers.Default.Renderer.Camera.Zoom;

    origZoom = SystemManagers.Default.Renderer.Camera.Zoom;
    origPosition = SystemManagers.Default.Renderer.Camera.Position;

    upgradesZoom = 1.0f;
    upgradesPosition = new System.Numerics.Vector2(1500, 1500);
  }

  private float origZoom;
  private System.Numerics.Vector2 origPosition;


  private float upgradesZoom;
  private System.Numerics.Vector2 upgradesPosition;

  public void ToggleUpgradesGui()
  {
    drawUpgradesGui = !drawUpgradesGui;

    if (drawUpgradesGui)
    {
      var camera = SystemManagers.Default.Renderer.Camera;
      camera.Zoom = upgradesZoom;
      camera.Position = upgradesPosition;

      SystemManagers.Default.Renderer.Camera.CameraCenterOnScreen = CameraCenterOnScreen.Center;
      Renderer.UseBasicEffectRendering = false;
    }
    else
    {
      var camera = SystemManagers.Default.Renderer.Camera;
      upgradesZoom = targetZoom;
      upgradesPosition = camera.Position;

      camera.Zoom = origZoom;
      camera.Position = origPosition;

      SystemManagers.Default.Renderer.Camera.CameraCenterOnScreen = CameraCenterOnScreen.TopLeft;
      Renderer.UseBasicEffectRendering = true;
    }
  }

  private float targetZoom = 1.0f;

  public void Update(GameTime gameTime)
  {
    var state = MouseExtended.GetState();
    var keyboardState = KeyboardExtended.GetState();

    var camera = SystemManagers.Default.Renderer.Camera;

    if (keyboardState.WasKeyPressed(Microsoft.Xna.Framework.Input.Keys.F1))
    {
      ToggleUpgradesGui();
    }

    if (drawUpgradesGui)
    {
      if (state.DeltaScrollWheelValue > 10)
      {
        targetZoom -= state.DeltaScrollWheelValue * 0.0005f;
      }
      else if (state.DeltaScrollWheelValue < -10)
      {
        targetZoom -= state.DeltaScrollWheelValue * 0.0005f;
      }

      camera.Zoom = MathHelper.Lerp(camera.Zoom, targetZoom, (float)gameTime.ElapsedGameTime.TotalSeconds * 5.0f);

      if (state.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed)
      {
        var delta = state.DeltaPosition;
        camera.Position = new System.Numerics.Vector2(
          Math.Clamp(camera.Position.X + delta.X / camera.Zoom, -3000, 3000),
          Math.Clamp(camera.Position.Y + delta.Y / camera.Zoom, -3000, 3000)
        );
      }
    }

    // Renderer.ApplyCameraZoomOnWorldTranslation = true;
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

  public void DrawLineBetween(
      SpriteBatch spriteBatch,
      Vector2 startPos,
      Vector2 endPos,
      int thickness,
      Color color)
  {
    // Create a texture as wide as the distance between two points and as high as
    // the desired thickness of the line.
    var distance = (int)Vector2.Distance(startPos, endPos);
    var texture = new Texture2D(spriteBatch.GraphicsDevice, distance, thickness);

    // Fill texture with given color.
    var data = new Color[distance * thickness];
    for (int i = 0; i < data.Length; i++)
    {
      data[i] = color;
    }
    texture.SetData(data);

    // Rotate about the beginning middle of the line.
    var rotation = (float)Math.Atan2(endPos.Y - startPos.Y, endPos.X - startPos.X);
    var origin = new Vector2(0, thickness / 2);

    spriteBatch.Draw(
        texture,
        startPos,
        null,
        Color.White,
        rotation,
        origin,
        1.0f,
        SpriteEffects.None,
        1.0f);
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

      //TODO: draw button connections
      // Example draw for lines:
      // Fix automatic stystem for when to draw and different states: hidden/unlocked/available etc
      var camera = SystemManagers.Default.Renderer.Camera;
      var m = camera.GetTransformationMatrix(true);
      _spriteBatch.Begin(transformMatrix: m);
      // var fb = UpgradeManager.CurrentUpgrades.UpgradeButtons.FirstOrDefault();
      // var sb = UpgradeManager.CurrentUpgrades.UpgradeButtons.LastOrDefault();
      // int x = (int)fb.Value.Button.X;
      // int y = (int)fb.Value.Button.Y;
      // int x2 = (int)sb.Value.Button.X;
      // int y2 = (int)sb.Value.Button.Y;
      // DrawLineBetween(_spriteBatch, new Vector2(x, y), new Vector2(x2, y2), 10, Color.Red);

      foreach (var joint in UpgradeManager.CurrentUpgrades.UpgradeJoints)
      {
        if (joint.Value.State == UpgradeJoint.JointState.Hidden)
        {
          continue;
        }

        var fromButton = joint.Value.Start;
        var toButton = joint.Value.End;
        int xStart = (int)fromButton.X;
        int yStart = (int)fromButton.Y;
        int xEnd = (int)toButton.X;
        int yEnd = (int)toButton.Y;
        var color = Color.White;

        if (joint.Value.State == UpgradeJoint.JointState.Unlocked)
        {
          color = Color.White;
        }
        else if (joint.Value.State == UpgradeJoint.JointState.Purchased)
        {
          color = Color.Green;
        }

        var curX = xStart;
        var curY = yStart;

        foreach (var point in joint.Value.MidwayPoints)
        {
          int midX = (int)point.X;
          int midY = (int)point.Y;
          DrawLineBetween(_spriteBatch, new Vector2(curX, curY), new Vector2(midX, midY), 5, color);
          curX = midX;
          curY = midY;
        }

        DrawLineBetween(_spriteBatch, new Vector2(curX, curY), new Vector2(xEnd, yEnd), 5, color);
      }

      // _spriteBatch.Draw(AssetManager.DefaultTexture, new Rectangle(x, y, w, h), Color.Red);
      _spriteBatch.End();
      SystemManagers.Default.Renderer.Draw(SystemManagers.Default, m_upgradesLayer);
    }
    else
    {
      SystemManagers.Default.Renderer.Draw(SystemManagers.Default, Gum.Renderer.MainLayer);
    }
  }
}
