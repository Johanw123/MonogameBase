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
  private Layer menuPopupLayer;
  private Layer originalPopupLayer;
  private readonly List<GraphicalUiElement> pauseInputItems = new();

  public Layer m_combinedLayer;


  public Layer m_popupLayer;
  public Layer PrestigeDialogLayer { get; private set; }

  // private BasicEffect _simpleEffect;

  public bool drawUpgradesGui = false;
  public bool IsOverlayVisible => drawUpgradesGui && !IsDetached;
  public bool IsDetached { get; private set; }
  public bool DrawingPopout { get; private set; }
#if KNI_WEB
  public float DockedDimming { get; private set; } = 1f;
#else
  public float DockedDimming { get; private set; } = 0.5f;
#endif
  public float PopoutOpacity { get; private set; } = 1f;
  private bool draggingTransparency;
  private static Rectangle TransparencySlider => new(HudLayout.Width - 1050, 30, 710, 72);
  private static Rectangle TransparencyTrack => new(TransparencySlider.X + 270, 62, 420, 8);
  public bool IsPopoutFocused =>
#if !KNI_WEB
    popout?.Focused == true;
#else
    false;
#endif
  public bool HasInputFocus => GameMain.Instance.IsActive
#if !KNI_WEB
    || popout?.Focused == true
#endif
    ;
#if !KNI_WEB
  private UpgradePopoutWindow popout;
  private bool popoutHadFocus;
  private string popoutError;
  private TimeSpan previousInactiveSleep;
  private static Rectangle PopoutButton => new(HudLayout.Width - 300, 36, 250, 60);

  public void DockUpgrades()
  {
    popout?.Dispose();
    popout = null;
    if (IsDetached) GameMain.Instance.InactiveSleepTime = previousInactiveSleep;
    IsDetached = false;
    draggingTransparency = false;
  }

  private void TogglePopout()
  {
    if (IsDetached) { DockUpgrades(); return; }
    try
    {
      popout = new UpgradePopoutWindow();
      popout.SetOpacity(PopoutOpacity);
      popoutError = null;
      previousInactiveSleep = GameMain.Instance.InactiveSleepTime;
      GameMain.Instance.InactiveSleepTime = TimeSpan.Zero;
      IsDetached = true;
    }
    catch (Exception error)
    {
      DockUpgrades();
      popoutError = "Window unavailable";
      Serilog.Log.Error(error, "Could not open upgrade window");
    }
  }

  public void DrawDetachedHud(Action drawHud, GraphicsDevice graphics, SpriteBatch batch)
  {
    if (popout == null) return;
    DrawingPopout = true;
    try
    {
      // Only the backdrop fades; UI draws at its normal opacity on top.
      graphics.Clear(popout.BackgroundColor);
      drawHud();
      popout.Present(graphics, batch, BaseGame._renderTargetHud);
    }
    catch (Exception error)
    {
      // A failed frame must never leave the only upgrade view in an invisible window.
      DockUpgrades();
      popoutError = "Window unavailable";
      Serilog.Log.Error(error, "Could not present upgrade window; restored in-game tree");
    }
    finally
    {
      DrawingPopout = false;
      graphics.SetRenderTarget(BaseGame._renderTargetHud);
      graphics.Clear(Color.Transparent);
    }
  }
#endif
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

    rootItems.Add(GumService.Default.Root);
    rootItems.Add(GumService.Default.ModalRoot);

    GumService.Default.CanvasWidth = 3840;
    GumService.Default.CanvasHeight = 2160;
    GumService.Default.Root.UpdateLayout();
    GumService.Default.ModalRoot.UpdateLayout();
    GumService.Default.PopupRoot.UpdateLayout();

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


    GumService.Default.Renderer.AddLayer(m_upgradesLayer);
    GumService.Default.Renderer.AddLayer(m_upgradesAbilitiesLayer);
    GumService.Default.Renderer.AddLayer(m_upgradesMetaLayer);
    GumService.Default.Renderer.AddLayer(m_gameMenuLayer);
    menuPopupLayer = new Layer { Name = "GameMenuPopupLayer" };
    GumService.Default.Renderer.AddLayer(menuPopupLayer);
    originalPopupLayer = GumService.Default.PopupRoot.Layer;
    GumService.Default.PopupRoot.MoveToLayer(menuPopupLayer);
    GumService.Default.Renderer.AddLayer(m_combinedLayer);
    GumService.Default.Renderer.AddLayer(m_popupLayer);
    PrestigeDialogLayer = new Layer
    {
      Name = "PrestigeDialogLayer"
    };
    GumService.Default.Renderer.AddLayer(PrestigeDialogLayer);

    targetZoom = SystemManagers.Default.Renderer.Camera.Zoom;

    origZoom = SystemManagers.Default.Renderer.Camera.Zoom;
    origPosition = System.Numerics.Vector2.Zero;


    SystemManagers.Default.Renderer.Camera.CameraCenterOnScreen = CameraCenterOnScreen.TopLeft;
  }

  public void Finish()
  {
#if !KNI_WEB
    DockUpgrades();
#endif
    // Gum roots survive the game screen; leave main-menu popups on their original layer.
    GumService.Default.PopupRoot.MoveToLayer(originalPopupLayer);
    GumService.Default.Renderer.RemoveLayer(menuPopupLayer);
    GumService.Default.Renderer.RemoveLayer(m_upgradesLayer);
    GumService.Default.Renderer.RemoveLayer(m_upgradesAbilitiesLayer);
    GumService.Default.Renderer.RemoveLayer(m_upgradesMetaLayer);
    GumService.Default.Renderer.RemoveLayer(m_gameMenuLayer);
    GumService.Default.Renderer.RemoveLayer(m_combinedLayer);
    GumService.Default.Renderer.RemoveLayer(m_popupLayer);
    GumService.Default.Renderer.RemoveLayer(PrestigeDialogLayer);
  }

  private float origZoom;
  private System.Numerics.Vector2 origPosition;
  private const float MinUpgradeZoom = 0.5f;
  private const float MaxUpgradeZoom = 2.0f;
  private const float UpgradePanPadding = 150f;

  private readonly Dictionary<UpgradeTypes, (float Zoom, System.Numerics.Vector2 Position)> upgradeViews = new();

  public UpgradeTypes m_upgradeWindowType = UpgradeTypes.None;

  public void SetUpgradeType(UpgradeTypes type, bool resetPreviousView = false)
  {
#if !KNI_WEB
    if (type == UpgradeTypes.None) DockUpgrades();
#endif
    var camera = SystemManagers.Default.Renderer.Camera;
    // Capture only an open tree; the gameplay camera is in a different coordinate space.
    if (m_upgradeWindowType != UpgradeTypes.None)
    {
      if (resetPreviousView)
        upgradeViews.Remove(m_upgradeWindowType);
      else
        upgradeViews[m_upgradeWindowType] = (targetZoom, camera.Position);
    }

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
        : type == UpgradeTypes.Abilities
          ? (Zoom: 0.5f, Position: new System.Numerics.Vector2(3800, 1860))
          : (Zoom: 1.0f, Position: new System.Numerics.Vector2(2000, 1000));
      targetZoom = Math.Clamp(view.Zoom, MinUpgradeZoom, MaxUpgradeZoom);
      camera.Zoom = targetZoom;
      camera.Position = view.Position;

      camera.CameraCenterOnScreen = CameraCenterOnScreen.Center;
      ClampUpgradeCameraPosition();
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

  private void ClampUpgradeCameraPosition()
  {
    float left = float.PositiveInfinity, top = float.PositiveInfinity;
    float right = float.NegativeInfinity, bottom = float.NegativeInfinity;
    foreach (var upgrade in UpgradeManager.CurrentUpgrades.GetCurrentButtons().Values)
    {
      var visual = upgrade.Button?.Visual;
      if (visual == null || !visual.Visible)
        continue;

      left = Math.Min(left, visual.AbsoluteLeft);
      top = Math.Min(top, visual.AbsoluteTop);
      right = Math.Max(right, visual.AbsoluteRight);
      bottom = Math.Max(bottom, visual.AbsoluteBottom);
    }

    if (float.IsPositiveInfinity(left))
      return;

    var camera = SystemManagers.Default.Renderer.Camera;
    // Keep the camera center near the tree, with a consistent screen-space margin.
    float padding = UpgradePanPadding / camera.Zoom;
    camera.Position = new System.Numerics.Vector2(
      Math.Clamp(camera.Position.X, left - padding, right + padding),
      Math.Clamp(camera.Position.Y, top - padding, bottom + padding));
  }

  private void UpdateMenuInput(GameTime gameTime, IEnumerable<GraphicalUiElement> roots)
  {
#if !KNI_WEB
    if (popout?.Focused == true)
    {
      GumService.Default.Cursor.TransformMatrix = popout.InputTransform();
      Gum.Forms.FormsUtilities.Update(null, gameTime, roots);
      return;
    }
#endif
    GumService.Default.Update(gameTime, roots);
  }

  public void Update(GameTime gameTime)
  {
#if KNI_WEB
    using var timing = new WebMenuTiming.Sample(WebMenuTiming.Phase.Input);
#endif
#if !KNI_WEB
    if (popout?.CloseRequested == true) DockUpgrades();
    bool popoutFocused = popout?.Focused == true;
    bool focusChanged = popoutFocused != popoutHadFocus;
    if (focusChanged)
    {
      draggingTransparency = false;
      GumService.Default.Cursor.ClearInputValues();
      GumService.Default.Cursor.VisualPushed = null;
      GumService.Default.Cursor.VisualOver = null;
      popoutHadFocus = popoutFocused;
    }
#endif
    if (!HasInputFocus)
    {
      draggingTransparency = false;
      GumService.Default.Cursor.ClearInputValues();
      GumService.Default.Cursor.VisualOver = null;
      GumService.Default.Cursor.VisualPushed = null;
      return;
    }
    if (UntitledGemGameGameScreen.Instance?.IsPrestigeConfirmationOpen == true)
    {
      var modalViewport = BaseGame.BoxingViewportAdapterGui.Viewport;
      var modalScale = Matrix.Invert(BaseGame.BoxingViewportAdapterGui.GetScaleMatrix());
      GumService.Default.Cursor.TransformMatrix =
        Matrix.CreateTranslation(-modalViewport.X, -modalViewport.Y, 0) * modalScale;
      WithPrestigeDialogCamera(() =>
        UpdateMenuInput(gameTime, new[] { GumService.Default.ModalRoot }));
      return;
    }

    if (GameMain.IsPaused)
    {
      var viewport = BaseGame.BoxingViewportAdapterGui.Viewport;
      var inverseScale = Matrix.Invert(BaseGame.BoxingViewportAdapterGui.GetScaleMatrix());
      GumService.Default.Cursor.TransformMatrix =
        Matrix.CreateTranslation(-viewport.X, -viewport.Y, 0) * inverseScale;
      pauseInputItems.Clear();
      pauseInputItems.AddRange(gameMenuItems);
      // The empty PopupRoot is itself a hit target. Only open popups should
      // receive input above the menu, never the invisible root container.
      foreach (var child in GumService.Default.PopupRoot.Children)
        if (child is GraphicalUiElement popup)
          pauseInputItems.Add(popup);
      WithPrestigeDialogCamera(() => UpdateMenuInput(gameTime, pauseInputItems));
      return;
    }

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

    bool treeInput = drawUpgradesGui;
#if !KNI_WEB
    treeInput &= !IsDetached || (popout.Focused && popout.PointerOver && !focusChanged);
#endif
    if (treeInput && !draggingTransparency)
    {
      if (state.DeltaScrollWheelValue > 10)
      {
        targetZoom -= state.DeltaScrollWheelValue * 0.0005f;
      }
      else if (state.DeltaScrollWheelValue < -10)
      {
        targetZoom -= state.DeltaScrollWheelValue * 0.0005f;
      }

      targetZoom = Math.Clamp(targetZoom, MinUpgradeZoom, MaxUpgradeZoom);
      camera.Zoom = Math.Clamp(
        MathHelper.Lerp(camera.Zoom, targetZoom, Math.Clamp(dt * 5.0f, 0f, 1f)),
        MinUpgradeZoom, MaxUpgradeZoom);

      if (state.MiddleButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed
        // || state.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed
        || state.RightButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed)
      {
        var delta = state.DeltaPosition;
        float panScale = 1.5f;
#if !KNI_WEB
        if (IsDetached) panScale = popout.InputTransform().M11;
#endif
        camera.Position = new System.Numerics.Vector2(
          camera.Position.X + delta.X * panScale / camera.Zoom,
          camera.Position.Y + delta.Y * panScale / camera.Zoom
        );
      }
      ClampUpgradeCameraPosition();
    }

    var vp = BaseGame.BoxingViewportAdapterGui.Viewport;
    var scale = BaseGame.BoxingViewportAdapterGui.GetScaleMatrix();
    Matrix.Invert(ref scale, out var scale2);
    GumService.Default.Cursor.TransformMatrix = Matrix.CreateTranslation(-vp.X, -vp.Y, 0) * scale2;

    // camera.ScreenToWorld(0, 0, out var worldX, out var worldY);
    // m_refuelButton.X = worldX;
    // m_refuelButton.Y = worldY;

#if !KNI_WEB
    if (IsDetached)
    {
      if (popout.Focused)
      {
        GumService.Default.Cursor.TransformMatrix = popout.InputTransform();
        if (UpdateTransparencySlider(GumService.Default.Cursor.TransformMatrix)) return;
        Gum.Forms.FormsUtilities.Update(null, gameTime, rootItems.Concat(skillTreeItems).Concat(combinedItems));
        SystemManagers.Default.Activity(gameTime.TotalGameTime.TotalSeconds);
      }
      else
      {
        WithPrestigeDialogCamera(() => GumService.Default.Update(gameTime, rootItems.Concat(hudItems).Concat(combinedItems)));
      }
      UpdateNavigationButtons(dt);
      if (popout?.Focused == true && PopoutButton.Contains(GumService.Default.Cursor.X, GumService.Default.Cursor.Y)
        && state.WasButtonPressed(MouseButton.Left)) TogglePopout();
      return;
    }
#endif
    if (drawUpgradesGui)
    {
      if (UpdateTransparencySlider(GumService.Default.Cursor.TransformMatrix)) return;
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
      GumService.Default.Update(gameTime, rootItems.Concat(skillTreeItems).Concat(combinedItems));
    }
    else
    {
      GumService.Default.Update(gameTime, rootItems.Concat(hudItems).Concat(combinedItems));
    }

    if (UntitledGemGameGameScreen.Instance?.IsPrestigeConfirmationOpen != true)
      UpdateNavigationButtons(dt);
#if !KNI_WEB
    if (drawUpgradesGui && PopoutButton.Contains(GumService.Default.Cursor.X, GumService.Default.Cursor.Y)
      && state.WasButtonPressed(MouseButton.Left)) TogglePopout();
#endif
  }

  private void SetTransparencyValue(float value)
  {
    if (IsDetached)
    {
      value = Math.Clamp(value, 0f, 1f);
#if !KNI_WEB
      if (popout.SetOpacity(value)) PopoutOpacity = value;
#endif
    }
    else DockedDimming = Math.Clamp(value, 0f, 1f);
  }

  private bool UpdateTransparencySlider(Matrix transform)
  {
    var mouse = MouseExtended.GetState();
    var point = Vector2.Transform(mouse.Position.ToVector2(), transform);
    bool over = TransparencySlider.Contains(point);
    var trackHit = new Rectangle(TransparencyTrack.Left - 18, TransparencySlider.Top,
      TransparencyTrack.Width + 36, TransparencySlider.Height);
    if (trackHit.Contains(point) && mouse.WasButtonPressed(MouseButton.Left)) draggingTransparency = true;
    bool captured = draggingTransparency;
    if (captured)
    {
      var track = TransparencyTrack;
      float fraction = Math.Clamp((point.X - track.Left) / track.Width, 0f, 1f);
      SetTransparencyValue(fraction);
      if (mouse.LeftButton == ButtonState.Released) draggingTransparency = false;
    }
    if (!over && !captured) return false;
    // Header controls consume presses/releases before Gum can click a node beneath them.
    GumService.Default.Cursor.ClearInputValues();
    GumService.Default.Cursor.VisualPushed = null;
    GumService.Default.Cursor.VisualOver = null;
    UpgradeManager.Instance.HideTooltip();
    return true;
  }

  private void DrawTransparencySlider(SpriteBatch batch)
  {
    var box = TransparencySlider;
    var track = TransparencyTrack;
    float value = IsDetached ? PopoutOpacity : DockedDimming;
    float fraction = value;
    int knob = track.Left + (int)(track.Width * fraction);
    string label = $"{(IsDetached ? "Background" : "Dimming")} {(int)MathF.Round(value * 100)}%";
#if !KNI_WEB
    if (IsDetached && !popout.OpacitySupported) label = "Unavailable";
#endif
    batch.Begin();
    batch.Draw(AssetManager.DefaultTexture, track, HudLayout.ButtonBorderColor);
    batch.Draw(AssetManager.DefaultTexture, new Rectangle(track.Left, track.Top, Math.Max(1, knob - track.Left), track.Height), HudLayout.UpgradeAccent);
    batch.Draw(AssetManager.DefaultTexture, new Rectangle(knob - 8, track.Center.Y - 17, 16, 34), HudLayout.UpgradeAccent);
    batch.End();
    const float size = 28;
    var measured = Measure2(label, Vector2.Zero, size);
    FontManager.RenderFieldFont(() => ContentDirectory.Fonts.Roboto_Regular_ttf, label,
      new Vector2(box.Left, box.Center.Y - measured.Y / 2), HudLayout.ButtonTextColor, Color.Black, size);
  }

  // Handle press edges on every update: catch-up updates can run without a draw.
  private void UpdateNavigationButtons(float dt)
  {
    if (GameMain.IsPaused || !Upgrades.JsonUpgradesAsset.IsLoaded
      || !Upgrades.JsonUpgradeButtonsAsset.IsLoaded)
      return;

    AdvanceButtonAnimation(ref m_animateButtonClickUpgrades, dt);
    AdvanceButtonAnimation(ref m_animateButtonClickAbilities, dt);
    AdvanceButtonAnimation(ref m_animateButtonClickCheapestUpgrade, dt);

    if (m_upgradeWindowType == UpgradeTypes.Meta)
    {
      UpdateButtonApplyMeta();
    }
    else if (!UntitledGemGameGameScreen.Instance.m_prestiging)
    {
      UpdateButtonUpgrades();
      UpdateButtonAbilities();
      if (UpgradeManager.Instance.ExpandSpaceLevel > 0)
      {
        UpdateButtonUpgradeCheapest();
        UpdateButtonUpgradeCheapest2();
      }
    }
  }

  private static void AdvanceButtonAnimation(ref float animation, float dt)
  {
    if (animation <= 0f) return;
    animation += dt * 5f;
    if (animation >= 1f) animation = 0f;
  }

  private void DrawButtonBorders(Dictionary<string, UpgradeButton> buttons, Matrix viewProjection, float timeInSeconds)
  {
#if KNI_WEB
    using var timing = new WebMenuTiming.Sample(WebMenuTiming.Phase.Borders);
#endif
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
    using var timing = new WebMenuTiming.Sample(WebMenuTiming.Phase.Lines);
#endif

    m_lineRenderer.Begin(viewProjection, timeInSeconds);
    foreach (var joint in joints)
    {
      if (joint.Value.State == UpgradeJoint.JointState.Hidden
        || joint.Value.StartButton.State == UpgradeButton.UnlockState.Invisible)
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
    var upgrades = UpgradeManager.Instance;
    bool hideTooltip = IsDetached && upgrades.TooltipBelongsToPopout != DrawingPopout;
    var tooltip = upgrades.m_tooltipWindow?.Visual;
    var extra = upgrades.m_tooltipExtraWindow?.Visual;
    bool tooltipVisible = tooltip?.Visible == true;
    bool extraVisible = extra?.Visible == true;
    try
    {
      // These windows are also children of Gum's shared root. Suppress them for
      // the other window's entire draw, without clearing hover/purchase state.
      if (hideTooltip)
      {
        if (tooltip != null) tooltip.Visible = false;
        if (extra != null) extra.Visible = false;
      }
      DrawWindowContents(spriteBatch, drawHudBackground);
    }
    finally
    {
      if (hideTooltip)
      {
        if (tooltip != null) tooltip.Visible = tooltipVisible;
        if (extra != null) extra.Visible = extraVisible;
      }
    }
  }

  private void DrawWindowContents(SpriteBatch spriteBatch, Action drawHudBackground)
  {
    if (IsDetached && !DrawingPopout && !GameMain.IsPaused)
    {
      // Render gameplay HUD with its own camera, leaving the detached tree view intact.
      drawUpgradesGui = false;
      try { WithPrestigeDialogCamera(() => DrawContents(spriteBatch, drawHudBackground)); }
      finally { drawUpgradesGui = true; }
      return;
    }
    DrawContents(spriteBatch, drawHudBackground);
  }

  private void DrawContents(SpriteBatch spriteBatch, Action drawHudBackground)
  {
#if KNI_WEB
    using var timing = new WebMenuTiming.Sample(WebMenuTiming.Phase.Draw);
#endif
    BaseGame.DimmingFactor = GameMain.IsPaused ? 0.5f : IsOverlayVisible ? DockedDimming : 0f;
    BaseGame.DrawBlurFilter = IsOverlayVisible || GameMain.IsPaused;


    // Gameplay controls must stay above the bar; the upgrade tree must stay below it.
    if (!drawUpgradesGui || GameMain.IsPaused)
      drawHudBackground();

    if (GameMain.IsPaused)
    {
      WithPrestigeDialogCamera(() =>
      {
        SystemManagers.Default.Draw(m_gameMenuLayer);
        SystemManagers.Default.Draw(menuPopupLayer);
      });
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

    // Project into the active HUD target (or detached window), just as Gum does.
    var vp = spriteBatch.GraphicsDevice.Viewport;
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
      // Matrix projectionMatrix = Matrix.CreateOrthographicOffCenter(0, HudLayout.Width, HudLayout.Height, 0, 0f, -1f);
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
      DrawTransparencySlider(spriteBatch);
#if !KNI_WEB
      DrawHudButton(spriteBatch, PopoutButton, IsDetached ? "Dock" : popoutError ?? "Pop out",
        HudLayout.UpgradeAccent, false, PopoutButton.Contains(GumService.Default.Cursor.X, GumService.Default.Cursor.Y), 0);
#endif

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

      if(UpgradeManager.Instance.ExpandSpaceLevel > 0)
      {
        DrawToggleButtonUpgradeCheapest(spriteBatch);
        DrawToggleButtonUpgradeCheapest2(spriteBatch);
      }
    }

    if (UntitledGemGameGameScreen.Instance.IsPrestigeConfirmationOpen)
    {
      WithPrestigeDialogCamera(() => SystemManagers.Default.Draw(PrestigeDialogLayer));
      UntitledGemGameGameScreen.Instance.DrawPrestigeDialogButtons(spriteBatch);
    }
  }

  // Use the same explicit canvas transform for drawing and pointer hit-testing.
  private static void WithPrestigeDialogCamera(Action action)
  {
    var camera = SystemManagers.Default.Renderer.Camera;
    var position = camera.Position;
    var zoom = camera.Zoom;
    var center = camera.CameraCenterOnScreen;
    try
    {
      camera.CameraCenterOnScreen = CameraCenterOnScreen.TopLeft;
      camera.Position = System.Numerics.Vector2.Zero;
      camera.Zoom = 1f;
      action();
    }
    finally
    {
      camera.CameraCenterOnScreen = center;
      camera.Position = position;
      camera.Zoom = zoom;
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
    var mousePos = new Vector2(GumService.Default.Cursor.X, GumService.Default.Cursor.Y);
    var layout = HudLayout.NavigationButton(0);
    bool contains = new RectangleF(layout.X, layout.Y, layout.Width, layout.Height).Contains(mousePos);
    DrawHudButton(m_spriteBatch, layout, "Apply",
      new Color(210, 170, 255), true, contains, m_animateButtonClickUpgrades);
  }

  public void DrawToggleButtonUpgrades(SpriteBatch m_spriteBatch)
  {
    if (m_upgradeWindowType == UpgradeTypes.Meta) return;

    var mousePos = new Vector2(GumService.Default.Cursor.X, GumService.Default.Cursor.Y);
    var layout = HudLayout.NavigationButton(0);
    bool contains = new RectangleF(layout.X, layout.Y, layout.Width, layout.Height).Contains(mousePos);
    DrawHudButton(m_spriteBatch, layout, m_upgradeWindowType == UpgradeTypes.Upgrades ? "Hide" : "Upgrades",
      HudLayout.UpgradeAccent, m_upgradeWindowType == UpgradeTypes.Upgrades, contains, m_animateButtonClickUpgrades);
  }

  public void DrawToggleButtonAbilities(SpriteBatch m_spriteBatch)
  {
    if (m_upgradeWindowType == UpgradeTypes.Meta) return;

    var mousePos = new Vector2(GumService.Default.Cursor.X, GumService.Default.Cursor.Y);
    var layout = HudLayout.NavigationButton(1);
    bool contains = new RectangleF(layout.X, layout.Y, layout.Width, layout.Height).Contains(mousePos);
    DrawHudButton(m_spriteBatch, layout, m_upgradeWindowType == UpgradeTypes.Abilities ? "Hide" : "Abilities",
      HudLayout.AbilityAccent, m_upgradeWindowType == UpgradeTypes.Abilities, contains, m_animateButtonClickAbilities);
  }

  public void DrawToggleButtonUpgradeCheapest(SpriteBatch m_spriteBatch)
  {
    if (m_upgradeWindowType != UpgradeTypes.Upgrades) return;

    var mousePos = new Vector2(GumService.Default.Cursor.X, GumService.Default.Cursor.Y);
    var layout = HudLayout.NavigationButton(2);
    bool contains = new RectangleF(layout.X, layout.Y, layout.Width, layout.Height).Contains(mousePos);
    DrawHudButton(m_spriteBatch, layout, "Upgrade Cheapest",
      HudLayout.UpgradeAccent, false, contains, m_animateButtonClickCheapestUpgrade);
  }

  public void DrawToggleButtonUpgradeCheapest2(SpriteBatch m_spriteBatch)
  {
    if (m_upgradeWindowType != UpgradeTypes.Upgrades) return;

    var mousePos = new Vector2(GumService.Default.Cursor.X, GumService.Default.Cursor.Y);
    var layout = HudLayout.NavigationButton(3);
    bool contains = new RectangleF(layout.X, layout.Y, layout.Width, layout.Height).Contains(mousePos);
    DrawHudButton(m_spriteBatch, layout, "Spend All",
      HudLayout.UpgradeAccent, false, contains, m_animateButtonClickCheapestUpgrade);
  }


  private void UpdateButtonApplyMeta()
  {
    var mouse = MouseExtended.GetState();
    bool isMouseClicked = mouse.WasButtonPressed(MouseButton.Left);
    var mousePos = new Vector2(GumService.Default.Cursor.X, GumService.Default.Cursor.Y);
    var layout = HudLayout.NavigationButton(0);
    bool contains = new RectangleF(layout.X, layout.Y, layout.Width, layout.Height).Contains(mousePos);
    if (contains && isMouseClicked)
    {
      SetUpgradeType(UpgradeTypes.None);
      m_animateButtonClickUpgrades = 0.001f;

      TimerHelper.DoAfter(() =>
          {
            UntitledGemGameGameScreen.Instance.m_prestiging = false;
            UntitledGemGameGameScreen.Instance.m_postPrestige = false;
            UntitledGemGameGameScreen.Instance.m_prestigeTime = 0.0f;
            UntitledGemGameGameScreen.Instance.SaveProgress();
          }, 350, true);
    }
  }

  private void UpdateButtonUpgrades()
  {
    if (m_upgradeWindowType == UpgradeTypes.Meta) return;

    var mouse = MouseExtended.GetState();
    bool isMouseClicked = mouse.WasButtonPressed(MouseButton.Left);
    var mousePos = new Vector2(GumService.Default.Cursor.X, GumService.Default.Cursor.Y);
    var layout = HudLayout.NavigationButton(0);
    bool contains = new RectangleF(layout.X, layout.Y, layout.Width, layout.Height).Contains(mousePos);
    if (contains && isMouseClicked)
    {
      if (m_upgradeWindowType == UpgradeTypes.Upgrades)
        SetUpgradeType(UpgradeTypes.None);
      else
        SetUpgradeType(UpgradeTypes.Upgrades);
      m_animateButtonClickUpgrades = 0.001f;
    }
  }

  private void UpdateButtonAbilities()
  {
    if (m_upgradeWindowType == UpgradeTypes.Meta) return;

    var mouse = MouseExtended.GetState();
    bool isMouseClicked = mouse.WasButtonPressed(MouseButton.Left);
    var mousePos = new Vector2(GumService.Default.Cursor.X, GumService.Default.Cursor.Y);
    var layout = HudLayout.NavigationButton(1);
    bool contains = new RectangleF(layout.X, layout.Y, layout.Width, layout.Height).Contains(mousePos);
    if (contains && isMouseClicked)
    {
      if (m_upgradeWindowType == UpgradeTypes.Abilities)
        SetUpgradeType(UpgradeTypes.None);
      else
        SetUpgradeType(UpgradeTypes.Abilities);
      m_animateButtonClickAbilities = 0.001f;
    }
  }

  private void UpdateButtonUpgradeCheapest()
  {
    if (m_upgradeWindowType != UpgradeTypes.Upgrades) return;

    var mouse = MouseExtended.GetState();
    bool isMouseClicked = mouse.WasButtonPressed(MouseButton.Left);
    var mousePos = new Vector2(GumService.Default.Cursor.X, GumService.Default.Cursor.Y);
    var layout = HudLayout.NavigationButton(2);
    bool contains = new RectangleF(layout.X, layout.Y, layout.Width, layout.Height).Contains(mousePos);
    if (contains && isMouseClicked)
    {
      m_animateButtonClickCheapestUpgrade = 0.001f;
      UpgradeCheapest();
    }
  }

  private void UpdateButtonUpgradeCheapest2()
  {
    if (m_upgradeWindowType != UpgradeTypes.Upgrades) return;

    var mouse = MouseExtended.GetState();
    bool isMouseClicked = mouse.WasButtonPressed(MouseButton.Left);
    var mousePos = new Vector2(GumService.Default.Cursor.X, GumService.Default.Cursor.Y);
    var layout = HudLayout.NavigationButton(3);
    bool contains = new RectangleF(layout.X, layout.Y, layout.Width, layout.Height).Contains(mousePos);
    if (contains && isMouseClicked)
    {
      m_animateButtonClickCheapestUpgrade = 0.001f;

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

      if (cost < cheapest && button.CurrentLevel < button.Data.NumLevels && button.CanAfford && button.Button.IsEnabled && button.Data.UpgradeDefinition.ShortName != "CZS" && button.Data.UpgradeDefinition.ShortName != "P")
      {
        cheapestButton = button;
        cheapest = cost;
      }
    }

    if (cheapestButton != null)
    {
      UpgradeManager.Instance.Upgrade(cheapestButton);
      UpgradeManager.Instance.HideTooltip();
      return true;
    }

    return false;
  }

  public void DrawHudButton(SpriteBatch spriteBatch, Rectangle bounds, string text,
    Color accent, bool selected, bool hovered, float clickAnimation)
  {
    float pulse = clickAnimation > 0 ? MathF.Sin(Math.Clamp(clickAnimation, 0f, 1f) * MathHelper.Pi) : 0;
    Color fill = Color.Lerp(hovered ? HudLayout.ButtonHoverColor : HudLayout.ButtonColor, accent, pulse * 0.16f);
    Color border = selected || hovered ? accent : HudLayout.ButtonBorderColor;
    int borderThickness = Math.Min(HudLayout.ButtonBorderThickness,
      Math.Max(1, Math.Min(bounds.Width, bounds.Height) / 2));
    spriteBatch.Begin();
    spriteBatch.Draw(AssetManager.DefaultTexture, bounds, border);
    spriteBatch.Draw(AssetManager.DefaultTexture,
      new Rectangle(bounds.X + borderThickness, bounds.Y + borderThickness,
        bounds.Width - borderThickness * 2, bounds.Height - borderThickness * 2), fill);
    if (selected || hovered)
      spriteBatch.Draw(AssetManager.DefaultTexture,
        new Rectangle(bounds.X + 16, bounds.Bottom - borderThickness,
          bounds.Width - 32, borderThickness), accent);
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
#if KNI_WEB
      return FontManager.MeasureBrowserText(Text, FontSize);
#else
    var r = FontManager.GetTextRenderer("Roboto_Regular_ttf");
    r.PositiveYIsDown = true;
    r.ResetLayout();

    var fontSize = FontSize;
    var measure = r.MeasureText(Text, position, 1, 1.171875f, fontSize, Color.Transparent, Color.Transparent, r.EnableKerning, r.PositiveYIsDown, r.PositionByBaseline, 0, new Vector2(0, 0), true, -1);
    return measure;
#endif
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
