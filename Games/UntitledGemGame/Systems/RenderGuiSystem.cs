using Apos.Shapes;
using Gum;
using Gum.Wireframe;
using JapeFramework;
using JapeFramework.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using MonoGame.Extended.Input;
using MonoGame.Extended.Tweening;
using MonoGameGum.Input;
using RenderingLibrary;
using RenderingLibrary.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using UntitledGemGame;

public class RenderGuiSystem
{
  // private readonly GraphicsDevice _graphicsDevice;

  public Layer m_upgradesLayer;
  public Layer m_gameMenuLayer;

  public Layer m_combinedLayer;


  public Layer m_popupLayer;

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

  private SdfLineRenderer m_lineRenderer;
  private SdfRectRenderer m_rectangleRender;

  // private Effect m_blurEffect;
  // private Texture2D spaceBackground;

  public RenderGuiSystem(SpriteBatch spriteBatch, ShapeBatch shapeBatch,
      GraphicsDevice graphicsDevice, OrthographicCamera camera, GumService gumService)
  {
    // _graphicsDevice = graphicsDevice;
    Instance = this;
    // m_blurEffect = blurEffect;


    m_lineRenderer = new SdfLineRenderer(graphicsDevice, EffectCache.LineSdfFx);
    m_rectangleRender = new SdfRectRenderer(graphicsDevice, EffectCache.RectangleSdfFx);
    // blurEffect = AssetManager.LoadAsync<Effect>("Shaders/BlurShader.fx");
    // spaceBackground = AssetManager.Load<Texture2D>(ContentDirectory.Textures.MarkIII_Woods_png);
    // spaceBackgroundDepth = AssetManager.Load<Texture2D>(ContentDirectory.Textures.result_upscaled_png);

    // _simpleEffect = new BasicEffect(_graphicsDevice);
    // _simpleEffect.TextureEnabled = true;

    rootItems.Add(Gum.GumService.Default.Root);
    rootItems.Add(Gum.GumService.Default.ModalRoot);

    Gum.GumService.Default.CanvasWidth = 3840;
    Gum.GumService.Default.CanvasHeight = 2160;
    Gum.GumService.Default.Root.UpdateLayout();
    Gum.GumService.Default.ModalRoot.UpdateLayout();
    Gum.GumService.Default.PopupRoot.UpdateLayout();



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

    m_popupLayer = new Layer()
    {
      Name = "PopupLayer",
      // LayerCameraSettings = new LayerCameraSettings()
      // {
      //   IsInScreenSpace = true,
      //   Position = System.Numerics.Vector2.Zero,
      //   Zoom = 1.0f
      // }
    };


    Gum.GumService.Default.Renderer.AddLayer(m_upgradesLayer);
    Gum.GumService.Default.Renderer.AddLayer(m_gameMenuLayer);
    Gum.GumService.Default.Renderer.AddLayer(m_combinedLayer);
    Gum.GumService.Default.Renderer.AddLayer(m_popupLayer);

    targetZoom = SystemManagers.Default.Renderer.Camera.Zoom;

    origZoom = SystemManagers.Default.Renderer.Camera.Zoom;
    origPosition = System.Numerics.Vector2.Zero;

    upgradesZoom = 1.0f;
    upgradesPosition = new System.Numerics.Vector2(2000, 1000);

    SystemManagers.Default.Renderer.Camera.CameraCenterOnScreen = CameraCenterOnScreen.TopLeft;
  }

  public void Finish()
  {
    Gum.GumService.Default.Renderer.RemoveLayer(m_upgradesLayer);
    Gum.GumService.Default.Renderer.RemoveLayer(m_gameMenuLayer);
    Gum.GumService.Default.Renderer.RemoveLayer(m_combinedLayer);
    Gum.GumService.Default.Renderer.RemoveLayer(m_popupLayer);
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
  private readonly Tweener _tweener = new();

  public void Update(GameTime gameTime)
  {
    var state = MouseExtended.GetState();
    var keyboardState = KeyboardExtended.GetState();

    float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
    float time = (float)gameTime.TotalGameTime.Milliseconds;
    // EffectCache.ShapeFx.Value.Parameters["_Time"].SetValue(time);
    //

    _tweener.Update(dt);

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

      if (state.MiddleButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed
        // || state.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed
        || state.RightButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed)
      {
        var delta = state.DeltaPosition;
        camera.Position = new System.Numerics.Vector2(
          Math.Clamp(camera.Position.X + delta.X * 1.5f / camera.Zoom, -5000, 5000),
          Math.Clamp(camera.Position.Y + delta.Y * 1.5f / camera.Zoom, -5000, 5000)
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
      Gum.GumService.Default.Update(gameTime, gameMenuItems);
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
      Gum.GumService.Default.Update(gameTime, rootItems.Concat(skillTreeItems).Concat(combinedItems));
    }
    else
    {
      Gum.GumService.Default.Update(gameTime, rootItems.Concat(hudItems).Concat(combinedItems));
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
      var m = camera.GetTransformationMatrix(true).ToXNA();
      var timeInSeconds = (float)BaseGame.Time.TotalGameTime.TotalSeconds;

#if KNI_WEB
      _spriteBatch.Begin(SpriteSortMode.Immediate, effect: EffectCache.LineSdfFx, transformMatrix: m);
#else
      var blendState = new Microsoft.Xna.Framework.Graphics.BlendState
      {
        ColorBlendFunction = Microsoft.Xna.Framework.Graphics.BlendFunction.Add,
        AlphaBlendFunction = Microsoft.Xna.Framework.Graphics.BlendFunction.Max,
        ColorSourceBlend = Microsoft.Xna.Framework.Graphics.Blend.One,
        ColorDestinationBlend = Microsoft.Xna.Framework.Graphics.Blend.One,
        AlphaSourceBlend = Microsoft.Xna.Framework.Graphics.Blend.One,
        AlphaDestinationBlend = Microsoft.Xna.Framework.Graphics.Blend.One
      };

      var vp = BaseGame.BoxingViewportAdapterGui.Viewport;
      Matrix projectionMatrix = Matrix.CreateOrthographicOffCenter(0, vp.Width, vp.Height, 0, 0f, -1f);
      Matrix viewProjection = m * projectionMatrix;

      m_lineRenderer.Begin(viewProjection, timeInSeconds);
#endif
      foreach (var joint in UpgradeManager.CurrentUpgrades.UpgradeJoints)
      {
        if (joint.Value.State == UpgradeJoint.JointState.Hidden)
        {
          continue;
        }

        float buttonSizeStart = joint.Value.StartButton.Button.Width;
        float buttonHalfSizeStart = buttonSizeStart / 2.0f;
        float buttonSizeEnd = joint.Value.EndButton.Button.Width;
        float buttonHalfSizeEnd = buttonSizeEnd / 2.0f;

        // float progress = 0.5f; // Draw 50% of the entire joint line

        float xStart = joint.Value.StartButton.Button.X + buttonHalfSizeStart + joint.Value.StartOffset.X;
        float yStart = joint.Value.StartButton.Button.Y + buttonHalfSizeStart + joint.Value.StartOffset.Y;
        float xEnd = joint.Value.EndButton.Button.X + buttonHalfSizeEnd + joint.Value.EndOffset.X;
        float yEnd = joint.Value.EndButton.Button.Y + buttonHalfSizeEnd + joint.Value.EndOffset.Y;
        // var color = Color.White;
        // var color = new Color(255,255,255, 140);
        var color = Color.White;
        var purchasedColor = new Color(75, 128, 177, 255);

        float unlockingSpeed = 5.0f;
        float purchasingSpeed = 5.0f;

        if (joint.Value.State == UpgradeJoint.JointState.Unlocked)
        {
          joint.Value.UnlockingTime = 1.0f;
          // color = Color.Green;
        }
        else if (joint.Value.State == UpgradeJoint.JointState.Unlocking)
        {
          if (joint.Value.UnlockingTime >= 1.0f)
          {
            joint.Value.State = UpgradeJoint.JointState.Unlocked;
          }
          else
          {
            joint.Value.UnlockingTime += BaseGame.Time.GetElapsedSeconds() * unlockingSpeed;
          }
        }
        else if (joint.Value.State == UpgradeJoint.JointState.Purchasing)
        {
          if (joint.Value.PurchasingTime >= 1.0f)
          {
            joint.Value.State = UpgradeJoint.JointState.Purchased;
            joint.Value.EndButton.ClickedTime = 0.0f;
            _tweener.TweenTo(target: joint.Value.EndButton, expression: btn => btn.ClickedTime, toValue: 1.0f, duration: 0.7f)
                .Easing(EasingFunctions.ExponentialOut);
            // AudioManager.Instance.PlaySound(AudioManager.Instance.UpgradeDoneEffect, pitch: RandomHelper.Float(-0.2f, 0.2f));
            AudioManager.Instance.PlaySound(AudioManager.Instance.UpgradeDoneEffect);
          }
          else
          {
            joint.Value.PurchasingTime += BaseGame.Time.GetElapsedSeconds() * purchasingSpeed;
          }
        }
        else if (joint.Value.State == UpgradeJoint.JointState.Purchased)
        {
          joint.Value.PurchasingTime = 1.0f;
          color = purchasedColor;
        }

        if (joint.Value.State == UpgradeJoint.JointState.Purchased)
        {
          D(xStart, yStart, xEnd, yEnd, joint.Value, color, color, joint.Value.UnlockingTime);
          D(xStart, yStart, xEnd, yEnd, joint.Value, purchasedColor, purchasedColor, joint.Value.PurchasingTime);
        }
        else
        {
          D(xStart, yStart, xEnd, yEnd, joint.Value, color * 0.7f, color * 0.1f, joint.Value.UnlockingTime);
          D(xStart, yStart, xEnd, yEnd, joint.Value, purchasedColor, purchasedColor, joint.Value.PurchasingTime);
        }
      }

      // _spriteBatch.End();
      m_lineRenderer.End();

      // #if KNI_WEB
      //       _spriteBatch.Begin(SpriteSortMode.Immediate, effect: EffectCache.RectangleSdfFx, transformMatrix: m);
      // #else
      //       _spriteBatch.Begin(SpriteSortMode.Immediate, blendState, effect: EffectCache.RectangleSdfFx, transformMatrix: m);
      // #endif

      // m_lineRenderer.Begin(m, timeInSeconds);
      //       var vp = BaseGame.BoxingViewportAdapterGui.Viewport;
      // Matrix projectionMatrix = Matrix.CreateOrthographicOffCenter(0, vp.Width, vp.Height, 0, 0f, -1f);
      // Matrix viewProjection = m * projectionMatrix;
      m_rectangleRender.Begin(viewProjection, timeInSeconds);


      var mouseState = Mouse.GetState();

      RectangleF r = new RectangleF(0, 0, 0, 0);
      if (UpgradeManager.Instance.m_tooltipWindow != null && UpgradeManager.Instance.m_tooltipWindow.Visual.Visible)
        r = new RectangleF(UpgradeManager.Instance.m_tooltipWindow.Visual.AbsoluteLeft, UpgradeManager.Instance.m_tooltipWindow.Visual.AbsoluteTop, UpgradeManager.Instance.m_tooltipWindow.Visual.Width, UpgradeManager.Instance.m_tooltipWindow.Visual.Height);

      foreach (var ub in UpgradeManager.CurrentUpgrades.UpgradeButtons)
      {
        var button = ub.Value.Button;
        var buttonVis = button.Visual;

        bool isHovered = buttonVis.HasCursorOver(Gum.GumService.Default.Cursor, m_upgradesLayer);

        if (buttonVis.Visible && button.IsVisible && ub.Value.State >= UpgradeButton.UnlockState.Revealed && buttonVis.Children.Count > 3)
        {
          var r2 = new RectangleF(button.Visual.AbsoluteLeft - 2, button.Visual.AbsoluteTop - 2, button.Visual.Width + 4, button.Visual.Height + 4);
          // if (!r.Intersects(r2))
          // {
          //   m_shapeBatch.DrawRectangle(new Vector2(button.AbsoluteLeft, button.AbsoluteTop), new Vector2(button.ActualWidth, button.ActualHeight), new Color(0, 0, 0, 0), Color.Red, 2);
          // }
          // m_shapeBatch.DrawRectangle(new Vector2(button.AbsoluteLeft - 2, button.AbsoluteTop - 2), new Vector2(button.ActualWidth + 4, button.ActualHeight + 4), new Color(0, 0, 0, 0), borderSprite.Color, 2);

          if (ub.Value.State == UpgradeButton.UnlockState.Purchased)
          {
            m_rectangleRender.DrawRect(r2.ToRectangle(), 0.8f, 2.0f, Color.White, Color.Black, ub.Value.ClickedTime, isHovered);
            m_rectangleRender.DrawRect(r2.ToRectangle(), 2.5f, 2.0f, ub.Value.BorderColor, ub.Value.BorderColor, ub.Value.ClickedTime, isHovered);
          }
          else if (ub.Value.State == UpgradeButton.UnlockState.Revealed)
          {
            var c = ub.Value.BorderColor * 0.2f;
            c.A = 255;
            m_rectangleRender.DrawRect(r2.ToRectangle(), 2.0f, 2.0f, new Color(60, 60, 60, 255), new Color(60, 60, 60, 255), 0, isHovered);
          }
          else
          // else if(ub.Value.State == UpgradeButton.UnlockState.)
          {
            if (ub.Value.CanAfford)
            {
              var c = ub.Value.BorderColor;
              c.A = 255;
              m_rectangleRender.DrawRect(r2.ToRectangle(), 0.2f, 2.0f, Color.White, Color.Black, 0, isHovered);
              m_rectangleRender.DrawRect(r2.ToRectangle(), 2.0f, 2.0f, c, c, 0, isHovered);
            }
            else
            {
              var c = ub.Value.BorderColor * 0.2f;
              c.A = 255;
              m_rectangleRender.DrawRect(r2.ToRectangle(), 2.0f, 2.0f, c, c, 0, isHovered);
            }
          }
        }
      }



      m_rectangleRender.End();
      // _spriteBatch.End();
      // m_lineRenderer.End();


      SystemManagers.Default.Draw([m_upgradesLayer, m_combinedLayer]);


      SystemManagers.Default.Draw(m_popupLayer);

      m_rectangleRender.Begin(viewProjection, timeInSeconds);


      var bc = new Color(255, 186, 21, 255);

      var tooltipWindow = UpgradeManager.Instance.m_tooltipWindow;
      if (tooltipWindow != null && tooltipWindow.IsVisible)
      {
        var borderRect = new RectangleF(tooltipWindow.AbsoluteLeft, tooltipWindow.AbsoluteTop, tooltipWindow.Width, tooltipWindow.Height);
        m_rectangleRender.DrawRect(borderRect.ToRectangle(), 1.0f, 5.0f, bc, bc, 0.8f, true);

        var newRect = new RectangleF(borderRect.Left + 50, borderRect.Top + 60, borderRect.Width - 100, 4);
        m_rectangleRender.DrawRect(newRect.ToRectangle(), 1.0f, 5.0f, bc, bc, 0.0f, false);

        var tooltipExtraWindow = UpgradeManager.Instance.m_tooltipExtraWindow;
        if(tooltipExtraWindow.IsVisible)
        {
          borderRect = new RectangleF(tooltipExtraWindow.AbsoluteLeft, tooltipExtraWindow.AbsoluteTop, tooltipExtraWindow.Width, tooltipExtraWindow.Height);
          m_rectangleRender.DrawRect(borderRect.ToRectangle(), 0.5f, 5.0f, bc, bc, 0.0f, true);
        }
      }


      // var tooltipHeader = UpgradeManager.Instance.m_toolTipTitleBackground;
      // if (tooltipHeader != null && tooltipWindow.IsVisible)
      // {
      //   var borderRect = new RectangleF(tooltipHeader.AbsoluteLeft, tooltipHeader.AbsoluteTop, tooltipHeader.Width, tooltipHeader.Height);
      //   m_rectangleRender.DrawRect(borderRect.ToRectangle(), 1.0f, 5.0f, Color.Yellow, Color.Yellow, 0.8f, false);
      // }

      m_rectangleRender.End();



      // foreach(var child in UpgradeManager.window.Children)
      // {
      //   Console.WriteLine(child.Name + " - " + child.GetType());
      // }
      // ToggleUpgradesGui();
      // SystemManagers.Default.Renderer.Draw(SystemManagers.Default, Gum.Renderer.MainLayer);
      // ToggleUpgradesGui();
    }
    else
    {
      // SystemManagers.Default.Renderer.Camera.Zoom = 1.0f;
      // origPosition = System.Numerics.Vector2.Zero;

      SystemManagers.Default.Draw([Gum.GumService.Default.Renderer.MainLayer, m_combinedLayer]);
    }

    // ToggleUpgradesGui();
    // SystemManagers.Default.Renderer.Draw(SystemManagers.Default, m_combinedLayer);
    // ToggleUpgradesGui();
  }



  private void D(float xStart, float yStart, float xEnd, float yEnd, UpgradeJoint joint, Color colorCore, Color colorGlow, float d)
  {
    float buttonSize = joint.StartButton.Button.Width;
    float buttonHalfSize = buttonSize / 2.0f;

    var timeInSeconds = (float)BaseGame.Time.TotalGameTime.TotalSeconds;

    // 1. Build a complete list of all points in the path
    var pathPoints = new List<Vector2>();
    pathPoints.Add(new Vector2(xStart, yStart));
    foreach (var point in joint.MidwayPoints)
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
    float allowedDistance = totalDistance * MathHelper.Clamp(d, 0f, 1f);
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
        // m_shapeBatch.FillLine(startPt, cutOffPt, 3, color, 1.0f);
        // m_lineRenderer.DrawLine(_spriteBatch, startPt, cutOffPt, 2.3f, color, color * 2);
        // m_shapeBatch.DrawLine(startPt, cutOffPt, 3, color, color, 3, 1.5f);

        // m_lineRenderer.DrawLine(_spriteBatch, timeInSeconds, startPt, cutOffPt, 2.3f, colorCore, colorGlow, d, Color.White);
        m_lineRenderer.DrawLine(startPt, cutOffPt, 2.3f, colorCore, colorGlow, d, Color.White);

        break; // We're done!
      }
      else
      {
        // Draw the full segment
        // m_shapeBatch.FillLine(startPt, endPt, 3, color, 1.0f);
        // m_shapeBatch.DrawLine(startPt, endPt, 3, color, color, 3, 1.5f);
        // m_lineRenderer.DrawLine(_spriteBatch, startPt, endPt, 2.3f, color, color * 2);
        // m_lineRenderer.DrawLine(_spriteBatch, timeInSeconds, startPt, endPt, 2.3f, color, color * 2, d, Color.White);
        m_lineRenderer.DrawLine(startPt, endPt, 2.3f, colorCore, colorGlow, 0, Color.White);
        currentDistanceAccumulator += segmentLength;
      }
    }

  }
}
