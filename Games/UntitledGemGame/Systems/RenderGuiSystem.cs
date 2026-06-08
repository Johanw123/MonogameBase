using Apos.Shapes;
using AsyncContent;
using Gum.Forms.Controls;
using Gum.Forms.DefaultVisuals;
using Gum.Managers;
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
  private GumService Gum => GumService.Default;

  public Layer m_upgradesLayer;
  public Layer m_gameMenuLayer;

  public Layer m_combinedLayer;

  // private BasicEffect _simpleEffect;

  public bool drawUpgradesGui = false;
  public bool DrawBlurEffect = true;

  // public static List<GraphicalUiElement> itemsToUpdate = new();

  public List<GraphicalUiElement> rootItems = new();
  public List<GraphicalUiElement> skillTreeItems = new();
  public List<GraphicalUiElement> hudItems = new();
  public List<GraphicalUiElement> gameMenuItems = new();
  public List<GraphicalUiElement> combinedItems = new();


  public static RenderGuiSystem Instance;

  // private Effect m_blurEffect;
  // private Texture2D spaceBackground;

  public RenderGuiSystem(SpriteBatch spriteBatch, ShapeBatch shapeBatch,
      GraphicsDevice graphicsDevice, OrthographicCamera camera, GumService gumService)
  {
    _spriteBatch = spriteBatch;
    m_shapeBatch = shapeBatch;
    _graphicsDevice = graphicsDevice;
    Instance = this;
    // m_blurEffect = blurEffect;

    // blurEffect = AssetManager.LoadAsync<Effect>("Shaders/BlurShader.fx");
    // spaceBackground = AssetManager.Load<Texture2D>(ContentDirectory.Textures.MarkIII_Woods_png);
    // spaceBackgroundDepth = AssetManager.Load<Texture2D>(ContentDirectory.Textures.result_upscaled_png);

    // _simpleEffect = new BasicEffect(_graphicsDevice);
    // _simpleEffect.TextureEnabled = true;

    rootItems.Add(Gum.Root);
    rootItems.Add(Gum.ModalRoot);

    GumService.Default.CanvasWidth = 3840;
    GumService.Default.CanvasHeight = 2160;
    GumService.Default.Root.UpdateLayout();
    GumService.Default.ModalRoot.UpdateLayout();
    GumService.Default.PopupRoot.UpdateLayout();

    m_upgradesLayer = new Layer()
    {
      Name = "UpgradesLayer",
    };

    m_gameMenuLayer = new Layer()
    {
      Name = "GameMenuLayer",
    };

    m_combinedLayer = new Layer()
    {
      Name = "CombinedLayer",
      LayerCameraSettings = new LayerCameraSettings()
      {
        IsInScreenSpace = true,
        Position = System.Numerics.Vector2.Zero,
        Zoom = 1.0f
      }
    };

    Gum.Renderer.AddLayer(m_upgradesLayer);
    Gum.Renderer.AddLayer(m_gameMenuLayer);
    Gum.Renderer.AddLayer(m_combinedLayer);

    targetZoom = SystemManagers.Default.Renderer.Camera.Zoom;

    origZoom = SystemManagers.Default.Renderer.Camera.Zoom;
    origPosition = System.Numerics.Vector2.Zero;

    upgradesZoom = 1.0f;
    upgradesPosition = new System.Numerics.Vector2(2000, 1000);

    SystemManagers.Default.Renderer.Camera.CameraCenterOnScreen = CameraCenterOnScreen.TopLeft;
  }

  public void Finish()
  {
    Gum.Renderer.RemoveLayer(m_upgradesLayer);
    Gum.Renderer.RemoveLayer(m_gameMenuLayer);
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
      var camera = SystemManagers.Default.Renderer.Camera;
      upgradesPosition = camera.Position;
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
      //Renderer.UseCustomEffectRendering = true;
    }
  }

  public void SetRenderUpgradesGui(bool value)
  {
    if (drawUpgradesGui != value)
    {
      ToggleUpgradesGui();
    }
  }

  public float targetZoom = 1.0f;

  public void Update(GameTime gameTime)
  {
    var state = MouseExtended.GetState();
    var keyboardState = KeyboardExtended.GetState();

    var camera = SystemManagers.Default.Renderer.Camera;

    if (keyboardState.WasKeyPressed(Microsoft.Xna.Framework.Input.Keys.F1) && !GameMain.IsPaused)
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
      }
    }

    var vp = BaseGame.BoxingViewportAdapterGui.Viewport;
    var scale = BaseGame.BoxingViewportAdapterGui.GetScaleMatrix();
    Matrix.Invert(ref scale, out var scale2);
    GumService.Default.Cursor.TransformMatrix = Matrix.CreateTranslation(-vp.X, -vp.Y, 0) * scale2;

    // camera.ScreenToWorld(0, 0, out var worldX, out var worldY);
    // m_refuelButton.X = worldX;
    // m_refuelButton.Y = worldY;

    if (GameMain.IsPaused)
    {
      Gum.Update(GameMain.Instance, gameTime, gameMenuItems);
    }
    else if (drawUpgradesGui)
    {
      // camera.ScreenToWorld(0, vp.Height - 50, out var worldX, out var worldY);
      // m_refuelButton.X = worldX;
      // m_refuelButton.Y = worldY;
      // m_refuelButton2.X = camera.Position.X + (vp.Width / 2.0f) - (m_refuelButton.Width / 2.0f);
      // m_refuelButton2.Y = camera.Position.Y + (vp.Height / 2.0f) - (m_refuelButton.Height / 2.0f);
      //
      // m_refuelButton2.Width = 200 / camera.Zoom;
      // m_refuelButton2.Height = 50 / camera.Zoom;

      // var curOverButtonName = GumService.Default.Cursor.WindowOver?.Name ?? "null";
      // Console.WriteLine(curOverButtonName);
      Gum.Update(GameMain.Instance, gameTime, rootItems.Concat(skillTreeItems).Concat(combinedItems));
    }
    else
    {
      Gum.Update(GameMain.Instance, gameTime, rootItems.Concat(hudItems).Concat(combinedItems));
    }
  }

  public void Draw()
  {
    BaseGame.DimmingFactor = (drawUpgradesGui || GameMain.IsPaused) ? 0.5f : 0f;
    BaseGame.DrawBlurFilter = drawUpgradesGui || GameMain.IsPaused;

    if (GameMain.IsPaused)
    {
      SystemManagers.Default.Draw(m_gameMenuLayer);
      return;
    }

    if (!Upgrades.JsonUpgradesAsset.IsLoaded)
      return;

    if (!Upgrades.JsonUpgradeButtonsAsset.IsLoaded)
      return;

    if (drawUpgradesGui)
    {
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

        // float xStart = joint.Value.StartButton.Button.X + buttonHalfSize + joint.Value.StartOffset.X;
        // float yStart = joint.Value.StartButton.Button.Y + buttonHalfSize + joint.Value.StartOffset.Y;
        // float xEnd = joint.Value.EndButton.Button.X + buttonHalfSize + joint.Value.EndOffset.X;
        // float yEnd = joint.Value.EndButton.Button.Y + buttonHalfSize + joint.Value.EndOffset.Y;
        // var color = Color.White;
        //
        // if (joint.Value.State == UpgradeJoint.JointState.Unlocked)
        // {
        //   // color = Color.Green;
        // }
        // else if (joint.Value.State == UpgradeJoint.JointState.Purchased)
        // {
        //   color = new Color(75, 128, 177, 255);
        // }
        //
        // var curX = xStart;
        // var curY = yStart;
        //
        // foreach (var point in joint.Value.MidwayPoints)
        // {
        //   float midX = point.X + buttonHalfSize;
        //   float midY = point.Y + buttonHalfSize;
        //   m_shapeBatch.FillLine(new Vector2(curX, curY), new Vector2(midX, midY), 1, color, 1);
        //   curX = midX;
        //   curY = midY;
        // }
        //
        // m_shapeBatch.FillLine(new Vector2(curX, curY), new Vector2(xEnd, yEnd), 1, color, 1.5f);

        // float progress = 0.5f; // Draw 50% of the entire joint line

        float xStart = joint.Value.StartButton.Button.X + buttonHalfSize + joint.Value.StartOffset.X;
        float yStart = joint.Value.StartButton.Button.Y + buttonHalfSize + joint.Value.StartOffset.Y;
        float xEnd = joint.Value.EndButton.Button.X + buttonHalfSize + joint.Value.EndOffset.X;
        float yEnd = joint.Value.EndButton.Button.Y + buttonHalfSize + joint.Value.EndOffset.Y;
        var color = Color.White;

        if (joint.Value.State == UpgradeJoint.JointState.Unlocked)
        {
          joint.Value.UnlockingTime = 1.0f;
          // color = Color.Green;
        }
        else if(joint.Value.State == UpgradeJoint.JointState.Unlocking)
        {
          if(joint.Value.UnlockingTime >= 1.0f)
          {
            joint.Value.State = UpgradeJoint.JointState.Unlocked;
          }
          else
          {
            joint.Value.UnlockingTime += BaseGame.Time.GetElapsedSeconds() * 5.0f;
          }
        }
        else if (joint.Value.State == UpgradeJoint.JointState.Purchased)
        {
          color = new Color(75, 128, 177, 255);
        }

        // 1. Build a complete list of all points in the path
        var pathPoints = new List<Vector2>();
        pathPoints.Add(new Vector2(xStart, yStart));
        foreach (var point in joint.Value.MidwayPoints)
        {
          pathPoints.Add(new Vector2(point.X + buttonHalfSize, point.Y + buttonHalfSize));
        }
        pathPoints.Add(new Vector2(xEnd, yEnd));

        // 2. Calculate total distance of the entire path
        float totalDistance = 0f;
        for (int i = 0; i < pathPoints.Count - 1; i++)
        {
          totalDistance += Vector2.Distance(pathPoints[i], pathPoints[i + 1]);
        }

        // 3. Determine how much distance we are actually allowed to draw
        float allowedDistance = totalDistance * MathHelper.Clamp(joint.Value.UnlockingTime, 0f, 1f);
        float currentDistanceAccumulator = 0f;

        // 4. Draw segments until we run out of allowed distance
        for (int i = 0; i < pathPoints.Count - 1; i++)
        {
          Vector2 startPt = pathPoints[i];
          Vector2 endPt = pathPoints[i + 1];
          float segmentLength = Vector2.Distance(startPt, endPt);

          // If adding this segment exceeds our limit, we cut it short and stop
          if (currentDistanceAccumulator + segmentLength >= allowedDistance)
          {
            float remainingDistance = allowedDistance - currentDistanceAccumulator;
            float segmentPercent = remainingDistance / segmentLength;

            // Find the exact cutoff point using Vector2.Lerp
            Vector2 cutOffPt = Vector2.Lerp(startPt, endPt, segmentPercent);

            // Draw the final partial segment
            m_shapeBatch.FillLine(startPt, cutOffPt, 1, color, 1);
            break; // We're done!
          }
          else
          {
            // Draw the full segment
            m_shapeBatch.FillLine(startPt, endPt, 1, color, 1);
            currentDistanceAccumulator += segmentLength;
          }
        }
      }

      m_shapeBatch.End();

      SystemManagers.Default.Draw([m_upgradesLayer, m_combinedLayer]);

      // ToggleUpgradesGui();
      // SystemManagers.Default.Renderer.Draw(SystemManagers.Default, Gum.Renderer.MainLayer);
      // ToggleUpgradesGui();
    }
    else
    {
      // SystemManagers.Default.Renderer.Camera.Zoom = 1.0f;
      // origPosition = System.Numerics.Vector2.Zero;

      SystemManagers.Default.Draw([Gum.Renderer.MainLayer, m_combinedLayer]);
    }

    // ToggleUpgradesGui();
    // SystemManagers.Default.Renderer.Draw(SystemManagers.Default, m_combinedLayer);
    // ToggleUpgradesGui();
  }
}
