using Apos.Shapes;
using AsyncContent;
using Gum.Wireframe;
using JapeFramework;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using MonoGame.Extended.Input;
using MonoGameGum;
using RenderingLibrary;
using RenderingLibrary.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using UntitledGemGame;

public class RenderGuiSystem
{
  private readonly SpriteBatch _spriteBatch;
  private readonly ShapeBatch m_shapeBatch;
  private readonly GraphicsDevice _graphicsDevice;
  private OrthographicCamera m_camera;
  private GumService Gum => GumService.Default;

  public static Layer m_upgradesLayer;

  private BasicEffect _simpleEffect;

  public bool drawUpgradesGui = false;
  public static bool DrawBlurEffect = true;

  // public static List<GraphicalUiElement> itemsToUpdate = new();

  public static List<GraphicalUiElement> rootItems = new();
  public static List<GraphicalUiElement> skillTreeItems = new();
  public static List<GraphicalUiElement> hudItems = new();

  private Effect m_blurEffect;
  // private Texture2D spaceBackground;

  public RenderGuiSystem(SpriteBatch spriteBatch, ShapeBatch shapeBatch,
      GraphicsDevice graphicsDevice, OrthographicCamera camera, GumService gumService,
      Effect blurEffect)
  {
    _spriteBatch = spriteBatch;
    m_shapeBatch = shapeBatch;
    _graphicsDevice = graphicsDevice;
    m_camera = camera;
    m_blurEffect = blurEffect;

    // blurEffect = AssetManager.LoadAsync<Effect>("Shaders/BlurShader.fx");
    // spaceBackground = AssetManager.Load<Texture2D>(ContentDirectory.Textures.MarkIII_Woods_png);
    // spaceBackgroundDepth = AssetManager.Load<Texture2D>(ContentDirectory.Textures.result_upscaled_png);

    _simpleEffect = new BasicEffect(_graphicsDevice);
    _simpleEffect.TextureEnabled = true;

    rootItems.Add(Gum.Root);
    rootItems.Add(Gum.ModalRoot);

    m_upgradesLayer = new Layer()
    {
      Name = "UpgradesLayer",
    };

    Gum.Renderer.AddLayer(m_upgradesLayer);

    targetZoom = SystemManagers.Default.Renderer.Camera.Zoom;

    origZoom = SystemManagers.Default.Renderer.Camera.Zoom;
    origPosition = System.Numerics.Vector2.Zero;

    upgradesZoom = 1.0f;
    upgradesPosition = origPosition;
  }

  private float origZoom;
  private System.Numerics.Vector2 origPosition;

  private float upgradesZoom;
  private System.Numerics.Vector2 upgradesPosition;

  public void ToggleUpgradesGui()
  {
    drawUpgradesGui = !drawUpgradesGui;

    if (upgradesPosition == System.Numerics.Vector2.Zero)
    {
      // var p =
      var camera = SystemManagers.Default.Renderer.Camera;
      upgradesPosition = camera.Position;
      Console.WriteLine($"Setting upgrades position to {upgradesPosition.X}, {upgradesPosition.Y}");
    }

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

      // if (state.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed
      if (state.MiddleButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed
        || state.RightButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed)
      {
        var delta = state.DeltaPosition;
        camera.Position = new System.Numerics.Vector2(
          Math.Clamp(camera.Position.X + delta.X / camera.Zoom, -5000, 5000),
          Math.Clamp(camera.Position.Y + delta.Y / camera.Zoom, -5000, 5000)
        );

        Console.WriteLine($"Camera position: {camera.Position.X}, {camera.Position.Y}");
      }
    }

    var vp = BaseGame.BoxingViewportAdapter.Viewport;
    var scale = BaseGame.BoxingViewportAdapter.GetScaleMatrix();
    Matrix.Invert(ref scale, out scale);
    GumService.Default.Cursor.TransformMatrix = Matrix.CreateTranslation(-vp.X, -vp.Y, 0) * scale;

    if (drawUpgradesGui)
    {
      Gum.Update(GameMain.Instance, gameTime, rootItems.Concat(skillTreeItems));
    }
    else
    {
      Gum.Update(GameMain.Instance, gameTime, rootItems.Concat(hudItems));
    }
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

    weightsParameter = m_blurEffect.Parameters["SampleWeights"];
    offsetsParameter = m_blurEffect.Parameters["SampleOffsets"];

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
    // var width = GameMain.Instance.Window.ClientBounds.Width;
    // var height = GameMain.Instance.Window.ClientBounds.Height;

    var width = GameMain.Instance.GraphicsDevice.Viewport.Width;
    var height = GameMain.Instance.GraphicsDevice.Viewport.Height;

    Console.WriteLine("Viewport w: " + width + " - " + height);

    BaseGame.DimmingFactor = drawUpgradesGui ? 0.7f : 0f;
    //TODO: move to base game for the blur and darken screen as processor steps?
    if (drawUpgradesGui)
    {
      // if (m_blurEffect.IsLoaded && !blurEffect.IsFailed)
      {
        m_blurEffect.Parameters["view_projection"]?.SetValue(m_camera.GetBoundingFrustum().Matrix);
        m_blurEffect.Parameters["xResolution"]?.SetValue(new Vector2(width, height));

        SetBlurEffectParameters(1f / width, 0);
      }

      if (DrawBlurEffect)
        _spriteBatch.Begin(effect: m_blurEffect, blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointClamp);
      else
        _spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointClamp);

      // _spriteBatch.Draw(BaseGame.renderTarget2, new Rectangle(0, 0, width, height), BaseGame.renderTarget2.Bounds, Color.White);
      _spriteBatch.Draw(BaseGame.renderTarget2, Vector2.Zero, Color.White);
      _spriteBatch.End();

      // _spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointClamp);
      // _spriteBatch.Draw(AssetManager.DefaultTexture, new Rectangle(0, 0, width, height)
      //     , Color.Black * 0.7f);
      // _spriteBatch.End();


      // _spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointClamp);
      // _spriteBatch.Draw(BaseGame._renderTargetImgui, new Rectangle(0, 0, width, height)
      //     , Color.White);
      // _spriteBatch.End();

      //TODO: draw button connections
      // Example draw for lines:
      // Fix automatic stystem for when to draw and different states: hidden/unlocked/available etc
      var camera = SystemManagers.Default.Renderer.Camera;
      var m = camera.GetTransformationMatrix(true);
      // _spriteBatch.Begin(transformMatrix: m);
      m_shapeBatch.Begin(m);
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

        float buttonSize = joint.Value.StartButton.Button.Width;
        float buttonHalfSize = buttonSize / 2.0f;

        float xStart = joint.Value.StartButton.Button.X + buttonHalfSize + joint.Value.StartOffset.X;
        float yStart = joint.Value.StartButton.Button.Y + buttonHalfSize + joint.Value.StartOffset.Y;
        float xEnd = joint.Value.EndButton.Button.X + buttonHalfSize + joint.Value.EndOffset.X;
        float yEnd = joint.Value.EndButton.Button.Y + buttonHalfSize + joint.Value.EndOffset.Y;
        var color = Color.White;

        if (joint.Value.State == UpgradeJoint.JointState.Unlocked)
        {
          // color = Color.Green;
        }
        else if (joint.Value.State == UpgradeJoint.JointState.Purchased)
        {
          color = Color.Blue;
        }

        var curX = xStart;
        var curY = yStart;

        foreach (var point in joint.Value.MidwayPoints)
        {
          float midX = point.X + buttonHalfSize;
          float midY = point.Y + buttonHalfSize;
          m_shapeBatch.FillLine(new Vector2(curX, curY), new Vector2(midX, midY), 1, color, 1);
          // m_shapeBatch.BorderLine(new Vector2(curX, curY), new Vector2(midX, midY), 1, color, 13, 1);
          // var w = Math.Abs(midX - curX);
          // var h = Math.Abs(midY - curY);
          // var thiccness = 4;
          // m_shapeBatch.FillRectangle(new Vector2(curX, curY), new Vector2(w + thiccness, h + thiccness), color, 0, 0, 1);
          curX = midX;
          curY = midY;
        }

        m_shapeBatch.FillLine(new Vector2(curX, curY), new Vector2(xEnd, yEnd), 1, color, 1.5f);
        // Console.WriteLine($"Drawing line from {curX},{curY} to {xEnd},{yEnd}");
        // m_shapeBatch.BorderLine(new Vector2(curX, curY), new Vector2(xEnd, yEnd), 1, color, 13, 1);

        // {
        //   var w = Math.Abs(xEnd - curX);
        //   var h = Math.Abs(yEnd - curY);
        //   var thiccness = 4;
        //   m_shapeBatch.FillRectangle(new Vector2(curX, curY), new Vector2(w + thiccness, h + thiccness), color, 0, 0, 1);
        // }
      }

      m_shapeBatch.End();

      SystemManagers.Default.Renderer.Draw(SystemManagers.Default, m_upgradesLayer);
    }
    else
    {
      SystemManagers.Default.Renderer.Draw(SystemManagers.Default, Gum.Renderer.MainLayer);
    }
  }
}
