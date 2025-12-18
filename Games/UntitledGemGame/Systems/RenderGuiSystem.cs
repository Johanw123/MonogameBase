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

  public static bool drawUpgradesGui = false;
  public static bool DrawBlurEffect = true;

  // public static List<GraphicalUiElement> itemsToUpdate = new();

  public static List<GraphicalUiElement> rootItems = new();
  public static List<GraphicalUiElement> skillTreeItems = new();
  public static List<GraphicalUiElement> hudItems = new();

  // private Effect m_blurEffect;
  // private Texture2D spaceBackground;

  public RenderGuiSystem(SpriteBatch spriteBatch, ShapeBatch shapeBatch,
      GraphicsDevice graphicsDevice, OrthographicCamera camera, GumService gumService)
  {
    _spriteBatch = spriteBatch;
    m_shapeBatch = shapeBatch;
    _graphicsDevice = graphicsDevice;
    m_camera = camera;
    // m_blurEffect = blurEffect;

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

  public void Draw()
  {
    BaseGame.DimmingFactor = drawUpgradesGui ? 0.5f : 0f;
    BaseGame.DrawBlurFilter = drawUpgradesGui;

    if (drawUpgradesGui)
    {
      //TODO: draw button connections
      // Example draw for lines:
      // Fix automatic stystem for when to draw and different states: hidden/unlocked/available etc
      var camera = SystemManagers.Default.Renderer.Camera;
      var m = camera.GetTransformationMatrix(true);
      m_shapeBatch.Begin(m);

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
