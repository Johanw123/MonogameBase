using Apos.Shapes;
using AsyncContent;
using GUI.Shared.Helpers;
using Gum;
using Gum.Wireframe;
using JapeFramework;
using JapeFramework.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content.Pipeline.Graphics;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using MonoGame.Extended.Input;
using MonoGame.Extended.Screens;
using MonoGame.Extended.Tweening;
using MonoGameGum.Input;
using RenderingLibrary;
using RenderingLibrary.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using UntitledGemGame;
using UntitledGemGame.Screens;


public class RenderGuiSystem
{
  public enum UpgradeTypes : int
  {
    None,
    Upgrades,
    Abilities,
    Meta
  }

  // private readonly GraphicsDevice _graphicsDevice;

  public Layer m_upgradesLayer;
  public Layer m_upgradesAbilitiesLayer;
  public Layer m_upgradesMetaLayer;
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

    m_upgradesAbilitiesLayer = new Layer()
    {
      Name = "m_upgradesAbilitiesLayer",
    };

    m_upgradesMetaLayer = new Layer()
    {
      Name = "UpgradesMetaLayer",
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
    Gum.GumService.Default.Renderer.AddLayer(m_upgradesAbilitiesLayer);
    Gum.GumService.Default.Renderer.AddLayer(m_upgradesMetaLayer);
    Gum.GumService.Default.Renderer.AddLayer(m_gameMenuLayer);
    Gum.GumService.Default.Renderer.AddLayer(m_combinedLayer);
    Gum.GumService.Default.Renderer.AddLayer(m_popupLayer);

    targetZoom = SystemManagers.Default.Renderer.Camera.Zoom;

    origZoom = SystemManagers.Default.Renderer.Camera.Zoom;
    origPosition = System.Numerics.Vector2.Zero;


    SystemManagers.Default.Renderer.Camera.CameraCenterOnScreen = CameraCenterOnScreen.TopLeft;
  }

  public void Finish()
  {
    Gum.GumService.Default.Renderer.RemoveLayer(m_upgradesLayer);
    Gum.GumService.Default.Renderer.RemoveLayer(m_upgradesAbilitiesLayer);
    Gum.GumService.Default.Renderer.RemoveLayer(m_upgradesMetaLayer);
    Gum.GumService.Default.Renderer.RemoveLayer(m_gameMenuLayer);
    Gum.GumService.Default.Renderer.RemoveLayer(m_combinedLayer);
    Gum.GumService.Default.Renderer.RemoveLayer(m_popupLayer);
  }

  private float origZoom;
  private System.Numerics.Vector2 origPosition;

  private readonly Dictionary<UpgradeTypes, (float Zoom, System.Numerics.Vector2 Position)> upgradeViews = new();

  public UpgradeTypes m_upgradeWindowType = UpgradeTypes.None;

  public void SetUpgradeType(UpgradeTypes type)
  {
    var camera = SystemManagers.Default.Renderer.Camera;
    // Capture only an open tree; the gameplay camera is in a different coordinate space.
    if (m_upgradeWindowType != UpgradeTypes.None)
      upgradeViews[m_upgradeWindowType] = (targetZoom, camera.Position);

    m_upgradeWindowType = type;

    drawUpgradesGui = type != UpgradeTypes.None;

    switch (type)
    {
      case UpgradeTypes.None:
        UpgradeManager.Instance.m_upgradesWindow.IsVisible = false;
        UpgradeManager.Instance.m_upgradesWindowAbilities.IsVisible = false;
        UpgradeManager.Instance.m_upgradesWindowMeta.IsVisible = false;
        break;
      case UpgradeTypes.Upgrades:
        UpgradeManager.Instance.m_upgradesWindow.IsVisible = true;
        UpgradeManager.Instance.m_upgradesWindowAbilities.IsVisible = false;
        UpgradeManager.Instance.m_upgradesWindowMeta.IsVisible = false;
        break;
      case UpgradeTypes.Abilities:
        UpgradeManager.Instance.m_upgradesWindow.IsVisible = false;
        UpgradeManager.Instance.m_upgradesWindowAbilities.IsVisible = true;
        UpgradeManager.Instance.m_upgradesWindowMeta.IsVisible = false;
        break;
      case UpgradeTypes.Meta:
        UpgradeManager.Instance.m_upgradesWindow.IsVisible = false;
        UpgradeManager.Instance.m_upgradesWindowAbilities.IsVisible = false;
        UpgradeManager.Instance.m_upgradesWindowMeta.IsVisible = true;
        break;
    }

    if (drawUpgradesGui)
    {
      var view = upgradeViews.TryGetValue(type, out var savedView)
        ? savedView
        : (Zoom: 1.0f, Position: new System.Numerics.Vector2(2000, 1000));
      targetZoom = view.Zoom;
      camera.Zoom = view.Zoom;
      camera.Position = view.Position;

      camera.CameraCenterOnScreen = CameraCenterOnScreen.Center;
      Renderer.UseBasicEffectRendering = false;
    }
    else
    {
      camera.Zoom = origZoom;
      camera.Position = origPosition;

      SystemManagers.Default.Renderer.Camera.CameraCenterOnScreen = CameraCenterOnScreen.TopLeft;
      Renderer.UseBasicEffectRendering = true;
      //Renderer.UseCustomEffectRendering = true;
    }
  }

  // public void ToggleUpgradesGui()
  // {
  //   drawUpgradesGui = !drawUpgradesGui;
  //
  //   if (upgradesPosition == System.Numerics.Vector2.Zero)
  //   {
  //     var camera = SystemManagers.Default.Renderer.Camera;
  //     upgradesPosition = camera.Position;
  //   }
  //
  //   if (drawUpgradesGui)
  //   {
  //     var camera = SystemManagers.Default.Renderer.Camera;
  //     camera.Zoom = upgradesZoom;
  //     camera.Position = upgradesPosition;
  //
  //     SystemManagers.Default.Renderer.Camera.CameraCenterOnScreen = CameraCenterOnScreen.Center;
  //     Renderer.UseBasicEffectRendering = false;
  //   }
  //   else
  //   {
  //     var camera = SystemManagers.Default.Renderer.Camera;
  //     upgradesZoom = targetZoom;
  //     upgradesPosition = camera.Position;
  //
  //     camera.Zoom = origZoom;
  //     camera.Position = origPosition;
  //
  //     SystemManagers.Default.Renderer.Camera.CameraCenterOnScreen = CameraCenterOnScreen.TopLeft;
  //     Renderer.UseBasicEffectRendering = true;
  //     //Renderer.UseCustomEffectRendering = true;
  //   }
  // }

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

    // if (keyboardState.WasKeyPressed(Microsoft.Xna.Framework.Input.Keys.F1) && !GameMain.IsPaused)
    // {
    //   // ToggleUpgradesGui();
    // }

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

  private void DrawButtonBorders(Dictionary<string, UpgradeButton> buttons, Matrix viewProjection, float timeInSeconds)
  {
    m_rectangleRender.Begin(viewProjection, timeInSeconds);

    foreach (var ub in buttons)
    {
      var button = ub.Value.Button;
      var buttonVis = button.Visual;

      bool isHovered = buttonVis.HasCursorOver(GumService.Default.Cursor, m_upgradesLayer);

      if (buttonVis.Visible && button.IsVisible && ub.Value.State >= UpgradeButton.UnlockState.Revealed && buttonVis.Children.Count > 3)
      {
        var r2 = new RectangleF(button.Visual.AbsoluteLeft - 2, button.Visual.AbsoluteTop - 2, button.Visual.Width + 4, button.Visual.Height + 4);

        if (ub.Value.State == UpgradeButton.UnlockState.MaxedOut)
        {
          m_rectangleRender.DrawRect(r2.ToRectangle(), 0.8f, 2.0f, Color.White, Color.Black, ub.Value.ClickedTime, isHovered);
          m_rectangleRender.DrawRect(r2.ToRectangle(), 3.0f, 2.0f, ub.Value.BorderColor, ub.Value.BorderColor, ub.Value.ClickedTime, isHovered);
        }
        else if (UpgradeManager.Instance.IsExpandSpaceLocked(ub.Value))
        {
          var lockedColor = new Color(204, 62, 62, 255);
          m_rectangleRender.DrawRect(r2.ToRectangle(), 2.0f, 2.0f, lockedColor, lockedColor, 0, isHovered);
        }
        else if (ub.Value.State == UpgradeButton.UnlockState.Revealed)
        {
          var c = ub.Value.BorderColor * 0.2f;
          c.A = 255;
          m_rectangleRender.DrawRect(r2.ToRectangle(), 2.0f, 2.0f, new Color(60, 60, 60, 255), new Color(60, 60, 60, 255), 0, isHovered);
        }
        else
        {
          if (ub.Value.CanAfford)
          {
            var c = ub.Value.BorderColor;
            c.A = 255;
            m_rectangleRender.DrawRect(r2.ToRectangle(), 0.2f, 2.0f, Color.White, Color.Black, ub.Value.ClickedTime, isHovered);
            m_rectangleRender.DrawRect(r2.ToRectangle(), 2.0f, 2.0f, c, c, ub.Value.ClickedTime, isHovered);
          }
          else
          {
            var c = new Color(200, 25, 10, 255) * 0.3f;
            c.A = 255;
            m_rectangleRender.DrawRect(r2.ToRectangle(), 1.0f, 2.0f, c, c, ub.Value.ClickedTime, isHovered);
          }
        }
      }
    }

    m_rectangleRender.End();
  }

  private void DrawJointLines(Dictionary<string, UpgradeJoint> joints, Matrix viewProjection, float timeInSeconds)
  {

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


    m_lineRenderer.Begin(viewProjection, timeInSeconds);
#endif
    foreach (var joint in joints)
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
      else if (joint.Value.State == UpgradeJoint.JointState.MaxedOut)
      {
        joint.Value.PurchasingTime = 1.0f;
        color = purchasedColor;
      }
      else if (joint.Value.State == UpgradeJoint.JointState.Purchased)
      {
        // joint.Value.PurchasingTime = 1.0f;
        color = purchasedColor;
      }

      if (joint.Value.State == UpgradeJoint.JointState.MaxedOut)
      {

      }
      else if (joint.Value.State == UpgradeJoint.JointState.Purchased)
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

    m_lineRenderer.End();
  }

  public void Draw(SpriteBatch spriteBatch, Action drawHudBackground)
  {
    BaseGame.DimmingFactor = (drawUpgradesGui || GameMain.IsPaused) ? 0.5f : 0f;
    BaseGame.DrawBlurFilter = drawUpgradesGui || GameMain.IsPaused;

    if (m_upgradeWindowType == UpgradeTypes.Meta)
      BaseGame.DimmingFactor = 1.0f;

    // Gameplay controls must stay above the bar; the upgrade tree must stay below it.
    if (!drawUpgradesGui || GameMain.IsPaused)
      drawHudBackground();

    if (GameMain.IsPaused)
    {
      SystemManagers.Default.Draw(m_gameMenuLayer);
      return;
    }

    if (!Upgrades.JsonUpgradesAsset.IsLoaded)
      return;

    if (!Upgrades.JsonUpgradeButtonsAsset.IsLoaded)
      return;

    var bc = new Color(255, 186, 21, 255);

    var camera = SystemManagers.Default.Renderer.Camera;
    var m = camera.GetTransformationMatrix(true).ToXNA();
    var timeInSeconds = (float)BaseGame.Time.TotalGameTime.TotalSeconds;

    var vp = BaseGame.BoxingViewportAdapterGui.Viewport;
    Matrix projectionMatrix = Matrix.CreateOrthographicOffCenter(0, vp.Width, vp.Height, 0, 0f, -1f);
    Matrix viewProjection = m * projectionMatrix;

    // DrawToggleButton();

    if (drawUpgradesGui)
    {

      // #if KNI_WEB
      //       _spriteBatch.Begin(SpriteSortMode.Immediate, effect: EffectCache.RectangleSdfFx, transformMatrix: m);
      // #else
      //       _spriteBatch.Begin(SpriteSortMode.Immediate, blendState, effect: EffectCache.RectangleSdfFx, transformMatrix: m);
      // #endif

      // m_lineRenderer.Begin(m, timeInSeconds);
      //       var vp = BaseGame.BoxingViewportAdapterGui.Viewport;
      // Matrix projectionMatrix = Matrix.CreateOrthographicOffCenter(0, vp.Width, vp.Height, 0, 0f, -1f);
      // Matrix viewProjection = m * projectionMatrix;
      // var mouseState = Mouse.GetState();


      switch (m_upgradeWindowType)
      {
        case UpgradeTypes.None:
          return;
        case UpgradeTypes.Upgrades:
          DrawJointLines(UpgradeManager.CurrentUpgrades.UpgradeJoints, viewProjection, timeInSeconds);
          DrawButtonBorders(UpgradeManager.CurrentUpgrades.UpgradeButtons, viewProjection, timeInSeconds);
          SystemManagers.Default.Draw([m_upgradesLayer, m_combinedLayer]);
          break;
        case UpgradeTypes.Abilities:
          DrawJointLines(UpgradeManager.CurrentUpgrades.UpgradeJointsAbilities, viewProjection, timeInSeconds);
          DrawButtonBorders(UpgradeManager.CurrentUpgrades.UpgradeButtonsAbilities, viewProjection, timeInSeconds);
          SystemManagers.Default.Draw([m_upgradesAbilitiesLayer, m_combinedLayer]);
          break;
        case UpgradeTypes.Meta:
          DrawJointLines(UpgradeManager.CurrentUpgrades.UpgradeJointsMeta, viewProjection, timeInSeconds);
          DrawButtonBorders(UpgradeManager.CurrentUpgrades.UpgradeButtonsMeta, viewProjection, timeInSeconds);
          SystemManagers.Default.Draw([m_upgradesMetaLayer]);
          break;
      }

      SystemManagers.Default.Draw(m_popupLayer);

      m_rectangleRender.Begin(viewProjection, timeInSeconds);

      var tooltipWindow = UpgradeManager.Instance.m_tooltipWindow;
      if (tooltipWindow != null && tooltipWindow.IsVisible)
      {
        var borderRect = new RectangleF(tooltipWindow.AbsoluteLeft, tooltipWindow.AbsoluteTop, tooltipWindow.Width, tooltipWindow.Height);
        m_rectangleRender.DrawRect(borderRect.ToRectangle(), 1.0f, 5.0f, bc, bc, 0.8f, true);

        var newRect = new RectangleF(borderRect.Left + 50, borderRect.Top + 60, borderRect.Width - 100, 4);
        m_rectangleRender.DrawRect(newRect.ToRectangle(), 1.0f, 5.0f, bc, bc, 0.0f, false);

        var tooltipExtraWindow = UpgradeManager.Instance.m_tooltipExtraWindow;
        if (tooltipExtraWindow.IsVisible)
        {
          borderRect = new RectangleF(tooltipExtraWindow.AbsoluteLeft, tooltipExtraWindow.AbsoluteTop, tooltipExtraWindow.Width, tooltipExtraWindow.Height);
          m_rectangleRender.DrawRect(borderRect.ToRectangle(), 0.5f, 5.0f, bc, bc, 0.0f, true);
        }
      }

      DrawTitleBanner(spriteBatch);

      // var upgradesButton = UntitledGemGameGameScreen.Instance.m_upgradesButton;
      // if(upgradesButton != null && upgradesButton.IsVisible)
      // {
      //   var borderRect = new RectangleF(upgradesButton.AbsoluteLeft, upgradesButton.AbsoluteTop, upgradesButton.Width, upgradesButton.Height);
      //   m_rectangleRender.DrawRect(borderRect.ToRectangle(), 1.0f, 5.0f, bc, bc, 0.8f, true);
      // }





      // var tooltipHeader = UpgradeManager.Instance.m_toolTipTitleBackground;
      // if (tooltipHeader != null && tooltipWindow.IsVisible)
      // {
      //   var borderRect = new RectangleF(tooltipHeader.AbsoluteLeft, tooltipHeader.AbsoluteTop, tooltipHeader.Width, tooltipHeader.Height);
      //   m_rectangleRender.DrawRect(borderRect.ToRectangle(), 1.0f, 5.0f, Color.Yellow, Color.Yellow, 0.8f, false);
      // }

      m_rectangleRender.End();

      // m_rectangleRender.Begin(projectionMatrix, timeInSeconds);

      // var upgradesButton = UntitledGemGameGameScreen.Instance.m_upgradesButton;
      // if(upgradesButton != null && upgradesButton.IsVisible)
      {
        // var borderRect = new RectangleF(upgradesButton.AbsoluteLeft, upgradesButton.AbsoluteTop, upgradesButton.Width, upgradesButton.Height);
        // var borderRect = new RectangleF(0, 0, 200, 200);
        // m_rectangleRender.DrawRect(borderRect.ToRectangle(), 1.0f, 5.0f, bc, bc, 0.8f, true);
      }

      // m_rectangleRender.End();


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


      SystemManagers.Default.Draw([GumService.Default.Renderer.MainLayer, m_combinedLayer]);
    }

    if (drawUpgradesGui)
      drawHudBackground();

    if (m_upgradeWindowType == UpgradeTypes.Meta)
    {
      DrawToggleButtonApplyMeta(spriteBatch);
    }
    else if (!UntitledGemGameGameScreen.Instance.m_prestiging)
    {
      DrawToggleButtonUpgrades(spriteBatch);
      DrawToggleButtonAbilities(spriteBatch);
      DrawToggleButtonUpgradeCheapest(spriteBatch);
    }
  }

  private void DrawTitleBanner(SpriteBatch spriteBatch)
  {
    string title = m_upgradeWindowType switch
    {
      UpgradeTypes.Abilities => "Ability Upgrades",
      UpgradeTypes.Meta => "Prestige Upgrades",
      _ => "Upgrades"
    };
    Color accent = m_upgradeWindowType switch
    {
      UpgradeTypes.Abilities => new Color(145, 210, 255),
      UpgradeTypes.Meta => new Color(210, 170, 255),
      _ => new Color(255, 215, 150)
    };
    const int height = 132;
    const float fontSize = 46f;
    float centerX = HudLayout.Width / 2f;
    var titleSize = Measure2(title, Vector2.Zero, fontSize);
    // var labelSize = Measure2("PROGRESSION", Vector2.Zero, 15f);
    int ruleWidth = (int)Math.Min(180, HudLayout.Width * 0.08f);
    int ruleGap = (int)(titleSize.X / 2) + 32;

    spriteBatch.Begin();
    spriteBatch.Draw(AssetManager.DefaultTexture,
      new Rectangle(0, 0, HudLayout.Width, height), HudLayout.PanelColor);
    spriteBatch.Draw(AssetManager.DefaultTexture,
      new Rectangle(0, height - 2, HudLayout.Width, 2), HudLayout.BorderColor);
    spriteBatch.Draw(AssetManager.DefaultTexture,
      new Rectangle((int)centerX - ruleGap - ruleWidth, 69, ruleWidth, 1), HudLayout.BorderColor);
    spriteBatch.Draw(AssetManager.DefaultTexture,
      new Rectangle((int)centerX + ruleGap, 69, ruleWidth, 1), HudLayout.BorderColor);
    spriteBatch.Draw(AssetManager.DefaultTexture,
      new Rectangle((int)centerX - 30, 109, 60, 3), accent);
    spriteBatch.End();

    // FontManager.RenderFieldFont(() => ContentDirectory.Fonts.Roboto_Regular_ttf,
    //   "PROGRESSION", new Vector2(centerX - labelSize.X / 2, 22),
    //   HudLayout.MutedTextColor, Color.Black, 15f);
    FontManager.RenderFieldFont(() => ContentDirectory.Fonts.Roboto_Regular_ttf,
      title, new Vector2(centerX - titleSize.X / 2, 69 - titleSize.Y / 2),
      accent, Color.Black, fontSize);
  }

  public void DrawToggleButtonApplyMeta(SpriteBatch m_spriteBatch)
  {
    var mouse = MouseExtended.GetState();
    bool isMouseClicked = mouse.WasButtonPressed(MouseButton.Left);
    var mousePos = new Vector2(GumService.Default.Cursor.X, GumService.Default.Cursor.Y);
    var layout = HudLayout.NavigationButton(0);
    bool contains = new RectangleF(layout.X, layout.Y, layout.Width, layout.Height).Contains(mousePos);
    DrawHudButton(m_spriteBatch, layout, "Apply",
      new Color(210, 170, 255), true, contains, m_animateButtonClickUpgrades);

    const float animSpeed = 5.0f;
    float dt = (float)BaseGame.Time.ElapsedGameTime.TotalSeconds;
    if (m_animateButtonClickUpgrades > 1.0f)
    {
      m_animateButtonClickUpgrades = 0.0f;
    }
    else if (m_animateButtonClickUpgrades > 0.0f)
    {
      m_animateButtonClickUpgrades += dt * animSpeed;
    }

    if (contains && isMouseClicked)
    {
      SetUpgradeType(UpgradeTypes.None);
      m_animateButtonClickUpgrades = dt * animSpeed;

      TimerHelper.DoAfter(() =>
          {
            UntitledGemGameGameScreen.Instance.m_prestiging = false;
            UntitledGemGameGameScreen.Instance.m_postPrestige = false;
            UntitledGemGameGameScreen.Instance.m_prestigeTime = 0.0f;
            UntitledGemGameGameScreen.Instance.SaveProgress();
          }, 350, true);
    }
  }

  public void DrawToggleButtonUpgrades(SpriteBatch m_spriteBatch)
  {
    if (m_upgradeWindowType == UpgradeTypes.Meta) return;

    var mouse = MouseExtended.GetState();
    bool isMouseClicked = mouse.WasButtonPressed(MouseButton.Left);
    var mousePos = new Vector2(GumService.Default.Cursor.X, GumService.Default.Cursor.Y);
    var layout = HudLayout.NavigationButton(0);
    bool contains = new RectangleF(layout.X, layout.Y, layout.Width, layout.Height).Contains(mousePos);
    DrawHudButton(m_spriteBatch, layout, m_upgradeWindowType == UpgradeTypes.Upgrades ? "Hide" : "Upgrades",
      HudLayout.UpgradeAccent, m_upgradeWindowType == UpgradeTypes.Upgrades, contains, m_animateButtonClickUpgrades);

    const float animSpeed = 5.0f;
    float dt = (float)BaseGame.Time.ElapsedGameTime.TotalSeconds;
    if (m_animateButtonClickUpgrades > 1.0f)
    {
      m_animateButtonClickUpgrades = 0.0f;
    }
    else if (m_animateButtonClickUpgrades > 0.0f)
    {
      m_animateButtonClickUpgrades += dt * animSpeed;
    }

    if (contains && isMouseClicked)
    {
      if (m_upgradeWindowType == UpgradeTypes.Upgrades)
        SetUpgradeType(UpgradeTypes.None);
      else
        SetUpgradeType(UpgradeTypes.Upgrades);
      m_animateButtonClickUpgrades = dt * animSpeed;
    }
  }

  public void DrawToggleButtonAbilities(SpriteBatch m_spriteBatch)
  {
    if (m_upgradeWindowType == UpgradeTypes.Meta) return;

    var mouse = MouseExtended.GetState();
    bool isMouseClicked = mouse.WasButtonPressed(MouseButton.Left);
    var mousePos = new Vector2(GumService.Default.Cursor.X, GumService.Default.Cursor.Y);
    var layout = HudLayout.NavigationButton(1);
    bool contains = new RectangleF(layout.X, layout.Y, layout.Width, layout.Height).Contains(mousePos);
    DrawHudButton(m_spriteBatch, layout, m_upgradeWindowType == UpgradeTypes.Abilities ? "Hide" : "Abilities",
      HudLayout.AbilityAccent, m_upgradeWindowType == UpgradeTypes.Abilities, contains, m_animateButtonClickAbilities);

    const float animSpeed = 5.0f;
    float dt = (float)BaseGame.Time.ElapsedGameTime.TotalSeconds;
    if (m_animateButtonClickAbilities > 1.0f)
    {
      m_animateButtonClickAbilities = 0.0f;
    }
    else if (m_animateButtonClickAbilities > 0.0f)
    {
      m_animateButtonClickAbilities += dt * animSpeed;
    }

    if (contains && isMouseClicked)
    {
      if (m_upgradeWindowType == UpgradeTypes.Abilities)
        SetUpgradeType(UpgradeTypes.None);
      else
        SetUpgradeType(UpgradeTypes.Abilities);
      // ToggleUpgradesGui();
      m_animateButtonClickAbilities = dt * animSpeed;
    }
  }

  public void DrawToggleButtonUpgradeCheapest(SpriteBatch m_spriteBatch)
  {
    if (m_upgradeWindowType == UpgradeTypes.Meta) return;

    var mouse = MouseExtended.GetState();
    bool isMouseClicked = mouse.WasButtonPressed(MouseButton.Left);
    var mousePos = new Vector2(GumService.Default.Cursor.X, GumService.Default.Cursor.Y);
    var layout = HudLayout.NavigationButton(2);
    bool contains = new RectangleF(layout.X, layout.Y, layout.Width, layout.Height).Contains(mousePos);
    DrawHudButton(m_spriteBatch, layout, "Upgrade Cheapest",
      HudLayout.UpgradeAccent, false, contains, m_animateButtonClickCheapestUpgrade);

    const float animSpeed = 5.0f;
    float dt = (float)BaseGame.Time.ElapsedGameTime.TotalSeconds;
    if (m_animateButtonClickCheapestUpgrade > 1.0f)
    {
      m_animateButtonClickCheapestUpgrade = 0.0f;
    }
    else if (m_animateButtonClickCheapestUpgrade > 0.0f)
    {
      m_animateButtonClickCheapestUpgrade += dt * animSpeed;
    }

    if (contains && isMouseClicked)
    {
      m_animateButtonClickCheapestUpgrade = dt * animSpeed;
      // while(UpgradeCheapest())
      // {
      //
      // }

      for (int i = 0; i < 100; ++i)
      {
        UpgradeCheapest();
      }
    }
  }

  private bool UpgradeCheapest()
  {
    ulong cheapest = ulong.MaxValue;
    UpgradeButton cheapestButton = null;
    foreach (var button in UpgradeManager.CurrentUpgrades.GetCurrentButtons().Values)
    {
      var cost = button.GetNextLevelCost();

      if (cost < cheapest && cost > 0 && button.CurrentLevel < button.Data.NumLevels && button.CanAfford && button.Button.IsEnabled)
      {
        cheapestButton = button;
        cheapest = cost;
      }
    }

    if (cheapestButton != null)
    {
      UpgradeManager.Instance.Upgrade(cheapestButton);
      return true;
    }

    return false;
  }

  private void DrawHudButton(SpriteBatch spriteBatch, Rectangle bounds, string text,
    Color accent, bool selected, bool hovered, float clickAnimation)
  {
    float pulse = clickAnimation > 0 ? MathF.Sin(Math.Clamp(clickAnimation, 0f, 1f) * MathHelper.Pi) : 0;
    Color fill = Color.Lerp(hovered ? HudLayout.ButtonHoverColor : HudLayout.ButtonColor, accent, pulse * 0.16f);
    Color border = selected || hovered ? accent : HudLayout.ButtonBorderColor;
    spriteBatch.Begin();
    spriteBatch.Draw(AssetManager.DefaultTexture, bounds, border);
    spriteBatch.Draw(AssetManager.DefaultTexture,
      new Rectangle(bounds.X + 1, bounds.Y + 1, bounds.Width - 2, bounds.Height - 2), fill);
    if (selected || hovered)
      spriteBatch.Draw(AssetManager.DefaultTexture,
        new Rectangle(bounds.X + 16, bounds.Bottom - 4, bounds.Width - 32, 2), accent);
    spriteBatch.End();

    const float fontSize = 24f;
    var measure = Measure2(text, Vector2.Zero, fontSize);
    FontManager.RenderFieldFont(() => ContentDirectory.Fonts.Roboto_Regular_ttf, text,
      new Vector2(bounds.Center.X - measure.X / 2, bounds.Center.Y - measure.Y / 2),
      selected || hovered ? accent : HudLayout.ButtonTextColor, Color.Black, fontSize);
  }

  private float m_animateButtonClickUpgrades = 0.0f;
  private float m_animateButtonClickAbilities = 0.0f;
  private float m_animateButtonClickCheapestUpgrade = 0.0f;

  public Vector2 Measure2(string Text, Vector2 position, float FontSize)
  {
    var r = FontManager.GetTextRenderer("Roboto_Regular_ttf");
    r.PositiveYIsDown = true;
    r.ResetLayout();

    var fontSize = FontSize;
    var measure = r.MeasureText(Text, position, 1, 1.171875f, fontSize, Color.Transparent, Color.Transparent, r.EnableKerning, r.PositiveYIsDown, r.PositionByBaseline, 0, new Vector2(0, 0), true, -1);
    return measure;
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
