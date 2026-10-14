using Apos.Shapes;
using AsyncContent;
using GUI.Shared.Helpers;
using Gum;
using Gum.Wireframe;
using JapeFramework;
using JapeFramework.Helpers;
using Microsoft.Xna.Framework;
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


public partial class RenderGuiSystem
{
  public enum UpgradeTypes : int
  {
    None,
    Upgrades,
    Abilities,
    Meta,
    Shipyard,
    Signals
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

  // private BasicEffect _simpleEffect;

  public bool drawUpgradesGui = false;
  public bool IsOverlayVisible => drawUpgradesGui && !IsDetached;
  public bool IsDetached { get; private set; }
  public bool DrawingPopout { get; private set; }
  public float DockedDimming { get; private set; } = 0.5f;
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
    || popout?.Focused == true || UntitledGemGame.Capture.CaptureSession.Active
#endif
    ;
#if !KNI_WEB
  private UpgradePopoutWindow popout;
  private bool preferPopout;
  private bool popoutHadFocus;
  private string popoutError;
  private TimeSpan previousInactiveSleep;
  private static Rectangle PopoutButton => new(HudLayout.Width - 300, 36, 250, 60);

  public void DockUpgrades()
  {
    preferPopout = false;
    ClosePopout();
  }

  private void ClosePopout()
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
    preferPopout = true;
    OpenPopout();
  }

  private void OpenPopout()
  {
    if (IsDetached) return;
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
      ClosePopout();
      popoutError = Loc.N("Window unavailable");
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
      ClosePopout();
      popoutError = Loc.N("Window unavailable");
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

    m_lineRenderer = new SdfLineRenderer(graphicsDevice, EffectCache.LineSdfFx)
    {
      WobbleAmount = 0f, ThicknessPulseAmount = 0.25f,
      PulseThicknessBoost = 1.5f, BaseGlowSpread = 3f, PulseGlowSpread = 5f,
      BaseGlowPadding = 6.5f, PulseExtraPadding = 12f
    };
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
  }

  private float origZoom;
  private System.Numerics.Vector2 origPosition;
  private const float MinUpgradeZoom = 0.5f;
  private const float MaxUpgradeZoom = 2.0f;
  private const float UpgradePanPadding = 150f;

  private readonly Dictionary<UpgradeTypes, (float Zoom, System.Numerics.Vector2 Position)> upgradeViews = new();

  // A new run starts with a small tree again, so its saved camera view no longer fits.
  public void ForgetView(UpgradeTypes type) => upgradeViews.Remove(type);

  public UpgradeTypes m_upgradeWindowType = UpgradeTypes.None;

  public void SetUpgradeType(UpgradeTypes type, bool resetPreviousView = false)
  {
    if (type == UpgradeTypes.Shipyard && !UpgradeManager.Instance.UGM.ShipyardUnlocked
      || type == UpgradeTypes.Signals && !UpgradeManager.Instance.UGM.SignalsUnlocked
      || type == UpgradeTypes.Abilities && !ShipSystems.Online) return;
    // The Talents tab marks its read-only view after this.
    m_talentsReadOnly = false;
#if !KNI_WEB
    if (type == UpgradeTypes.None) ClosePopout();
#endif
    var camera = SystemManagers.Default.Renderer.Camera;
    // Capture only an open tree; the gameplay camera is in a different coordinate space.
    if (m_upgradeWindowType is not (UpgradeTypes.None or UpgradeTypes.Shipyard or UpgradeTypes.Signals
      or UpgradeTypes.Meta or UpgradeTypes.Abilities))
    {
      if (resetPreviousView)
        upgradeViews.Remove(m_upgradeWindowType);
      else
        upgradeViews[m_upgradeWindowType] = (targetZoom, camera.Position);
    }

    CancelModuleDrag();
    revealingModule = ShipModule.None;
    m_upgradeWindowType = type;

    drawUpgradesGui = type != UpgradeTypes.None;
#if !KNI_WEB
    if (drawUpgradesGui && preferPopout) OpenPopout();
#endif

    switch (type)
    {
      case UpgradeTypes.Signals:
      case UpgradeTypes.Shipyard:
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

    UpgradeManager.Instance.HideTooltip();

    if (drawUpgradesGui && type is not (UpgradeTypes.Shipyard or UpgradeTypes.Signals))
    {
      if (type == UpgradeTypes.Meta)
      {
        targetZoom = camera.Zoom = 1f;
        camera.Position = System.Numerics.Vector2.Zero;
        camera.CameraCenterOnScreen = CameraCenterOnScreen.TopLeft;
        Renderer.UseBasicEffectRendering = false;
        return;
      }
      if (type == UpgradeTypes.Abilities)
      {
        FrameSystemTab();
        return;
      }
      var view = upgradeViews.TryGetValue(type, out var savedView)
        ? savedView
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
    SalvageInputCaptured = false;
#if !KNI_WEB
    if (popout?.CloseRequested == true) SetUpgradeType(UpgradeTypes.None);
    bool popoutFocused = popout?.Focused == true;
    bool focusChanged = popoutFocused != popoutHadFocus;
    if (focusChanged)
    {
      CancelModuleDrag();
      draggingTransparency = false;
      GumService.Default.Cursor.ClearInputValues();
      GumService.Default.Cursor.VisualPushed = null;
      GumService.Default.Cursor.VisualOver = null;
      popoutHadFocus = popoutFocused;
    }
#endif
    if (!HasInputFocus)
    {
      CancelModuleDrag();
      draggingTransparency = false;
      GumService.Default.Cursor.ClearInputValues();
      GumService.Default.Cursor.VisualOver = null;
      GumService.Default.Cursor.VisualPushed = null;
      return;
    }
    if (GameMain.IsPaused)
    {
      CancelModuleDrag();
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
      WithScreenCamera(() => UpdateMenuInput(gameTime, pauseInputItems));
      return;
    }

    var state = MouseExtended.GetState();
    var keyboardState = KeyboardExtended.GetState();

    float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
    float time = (float)gameTime.TotalGameTime.Milliseconds;
    // EffectCache.ShapeFx.Value.Parameters["_Time"].SetValue(time);
    //

    _tweener.Update(dt);
    if (UpdateModuleSalvageInput(dt)) return;

    var camera = SystemManagers.Default.Renderer.Camera;

    // if (keyboardState.WasKeyPressed(Microsoft.Xna.Framework.Input.Keys.F1) && !GameMain.IsPaused)
    // {
    //   // ToggleUpgradesGui();
    // }

    bool treeInput = drawUpgradesGui
      && m_upgradeWindowType is not (UpgradeTypes.Shipyard or UpgradeTypes.Signals or UpgradeTypes.Meta or UpgradeTypes.Abilities);
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
        WithScreenCamera(() => GumService.Default.Update(gameTime, rootItems.Concat(hudItems).Concat(combinedItems)));
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
    int percent = (int)MathF.Round(value * 100);
    string label = IsDetached ? Loc.F("Background {0}%", percent) : Loc.F("Dimming {0}%", percent);
#if !KNI_WEB
    if (IsDetached && !popout.OpacitySupported) label = Loc.T("Unavailable");
#endif
    batch.Begin(transformMatrix: HudLayout.RenderTransform);
    OrbitSkin.Slider(batch, track, value);
    batch.End();
    const float size = 28;
    var measured = Measure2(label, Vector2.Zero, size);
    FontManager.RenderFieldFont(nameof(ContentDirectory.Fonts.Roboto_Regular_ttf), label,
      new Vector2(box.Left, box.Center.Y - measured.Y / 2), OrbitSkin.ButtonTextColor, Color.Black, size);
  }

  // Handle press edges on every update: catch-up updates can run without a draw.
  private void UpdateNavigationButtons(float dt)
  {
    if (GameMain.IsPaused || !Upgrades.JsonUpgradesAsset.IsLoaded
      || !Upgrades.JsonUpgradeButtonsAsset.IsLoaded)
      return;

    AdvanceButtonAnimation(ref m_animateButtonClickUpgrades, dt);
    AdvanceButtonAnimation(ref m_animateButtonClickAbilities, dt);
    AdvanceButtonAnimation(ref m_animateButtonClickShipyard, dt);
    AdvanceButtonAnimation(ref m_animateButtonClickSignals, dt);
    AdvanceButtonAnimation(ref m_animateButtonClickCheapestUpgrade, dt);
    AdvanceButtonAnimation(ref m_animateButtonClickTalents, dt);

    if (ChoosingTalents)
    {
      UpdateButtonApplyMeta();
    }
    else if (!UntitledGemGameGameScreen.Instance.m_prestiging)
    {
      UpdateButtonUpgrades();
      UpdateButtonAbilities();
      UpdateShipSystemsInput(dt);
      UpdateShipyardInput(dt);
      UpdateSignalsInput();
      UpdateTalentsNavigation();
      if (BulkUpgradeButtonsShown)
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
    m_rectangleRender.Begin(viewProjection, timeInSeconds);

    foreach (var ub in buttons)
    {
      var button = ub.Value.Button;
      var buttonVis = button.Visual;

      // Skip hidden and offscreen visuals before cursor traversal or border work.
      if (!buttonVis.Visible || !button.IsVisible || ub.Value.State < UpgradeButton.UnlockState.Revealed)
        continue;
      if (!RenderVisibility.Intersects(buttonVis.AbsoluteLeft - 48, buttonVis.AbsoluteTop - 48,
          buttonVis.AbsoluteLeft + buttonVis.Width + 48, buttonVis.AbsoluteTop + buttonVis.Height + 48,
          viewProjection)) continue;
      var layer = m_upgradeWindowType == UpgradeTypes.Meta ? m_upgradesMetaLayer
        : m_upgradeWindowType == UpgradeTypes.Abilities ? m_upgradesAbilitiesLayer : m_upgradesLayer;
      bool isHovered =
#if KNI_WEB
        UntitledGemGame.Web.WebHudRenderScope.HasCursorOver(buttonVis, layer);
#else
        buttonVis.HasCursorOver(GumService.Default.Cursor, layer);
#endif

      if (buttonVis.Visible && button.IsVisible && ub.Value.State >= UpgradeButton.UnlockState.Revealed && buttonVis.Children.Count > 3)
      {
        var r2 = new RectangleF(button.Visual.AbsoluteLeft - 2, button.Visual.AbsoluteTop - 2, button.Visual.Width + 4, button.Visual.Height + 4);

        bool locked = IsUpgradeNodeLocked(ub.Value);
        bool canBuy = CanBuyUpgradeNode(ub.Value);
        var color = locked ? OrbitSkin.BorderColor * 0.65f
          : canBuy ? OrbitSkin.UpgradeAccent
          : ub.Value.IsMaxLevel ? OrbitSkin.ConfirmAccent * 0.85f
          : ub.Value.CurrentLevel > 0 ? OrbitSkin.Accent
          : OrbitSkin.DisabledTextColor * 0.85f;
        float glow = canBuy ? 0.28f : isHovered ? 0.22f : 0.1f;
        // One restrained outline. White double strokes made maxed nodes look actionable.
        m_rectangleRender.DrawRect(r2.ToRectangle(), canBuy ? 1.8f : 1.1f, 2f,
          color, color * glow, locked ? 0 : ub.Value.ClickedTime, isHovered);

        // Currency identity is a quiet outer frame, independent of affordability.
        if (ub.Value.Data.UpgradeDefinition.Currency == CoreShards.Currency)
        {
          var frame = new RectangleF(r2.X - 5, r2.Y - 5, r2.Width + 10, r2.Height + 10);
          var gold = OrbitSkin.UpgradeAccent * 0.4f;
          m_rectangleRender.DrawRect(frame.ToRectangle(), 0.8f, 3f, gold, Color.Transparent, 0, false);
        }
      }
    }

    m_rectangleRender.End();
  }

  private static bool IsUpgradeNodeLocked(UpgradeButton node)
    => node.State is UpgradeButton.UnlockState.Revealed or UpgradeButton.UnlockState.DemoLocked
      || UpgradeManager.Instance.IsExpandSpaceLocked(node);

  private bool CanBuyUpgradeNode(UpgradeButton node)
    => (node.State is UpgradeButton.UnlockState.Unlocked or UpgradeButton.UnlockState.Purchased)
      && !node.IsMaxLevel && !IsUpgradeNodeLocked(node) && node.CanAfford
      && node.Button.Visual.IsEnabled && !TalentsReadOnly;

  private void DrawUpgradeNodeStatus(SpriteBatch batch, Dictionary<string, UpgradeButton> buttons,
    Matrix transform, Matrix viewProjection)
  {
    batch.Begin(transformMatrix: transform, samplerState: SamplerState.LinearClamp);
    foreach (var node in buttons.Values)
    {
      var visual = node.Button.Visual;
      if (!visual.Visible || !node.Button.IsVisible || node.State < UpgradeButton.UnlockState.Revealed) continue;
      int x = (int)visual.AbsoluteLeft, y = (int)visual.AbsoluteTop;
      int width = (int)visual.Width, height = (int)visual.Height;
      if (!RenderVisibility.Intersects(x - 16, y - 16, x + width + 16, y + height + 16, viewProjection)) continue;
      if (IsUpgradeNodeLocked(node)) continue;
      // An empty rail means untouched; a filled portion means purchased ranks.
      // Keep this inside the icon frame so it cannot collide with talent names.
      batch.Draw(AssetManager.DefaultTexture,
        new Rectangle(x + 4, y + height - 14, Math.Max(1, width - 8), 11), OrbitSkin.PanelColor);
      var rail = new Rectangle(x + 10, y + height - 8, Math.Max(1, width - 20), 5);
      batch.Draw(AssetManager.DefaultTexture, rail, OrbitSkin.BorderColor * 0.65f);
      float fraction = Math.Clamp(node.CurrentLevel / (float)Math.Max(1, node.Data.NumLevels), 0, 1);
      if (fraction > 0)
      {
        int minimum = Math.Min(6, rail.Width);
        int filled = node.IsMaxLevel ? rail.Width
          : Math.Clamp((int)(rail.Width * fraction), minimum, Math.Max(minimum, rail.Width - 6));
        batch.Draw(AssetManager.DefaultTexture, new Rectangle(rail.X, rail.Y, filled, rail.Height),
          node.IsMaxLevel ? OrbitSkin.ConfirmAccent : OrbitSkin.Accent);
      }
      bool canBuy = CanBuyUpgradeNode(node);
      if (!canBuy && !node.IsMaxLevel) continue;
      var badge = new Rectangle(x + width - 10, y - 14, 24, 24);
      var ink = canBuy ? OrbitSkin.UpgradeAccent : OrbitSkin.ConfirmAccent;
      batch.Draw(AssetManager.DefaultTexture, badge, ink);
      var inside = new Rectangle(badge.X + 2, badge.Y + 2, badge.Width - 4, badge.Height - 4);
      batch.Draw(AssetManager.DefaultTexture, inside, OrbitSkin.PanelColor);
      var glyph = canBuy ? TextureCache.UpgradeAvailableIcon : TextureCache.UpgradeCompleteIcon;
      if (glyph?.IsLoaded == true && !glyph.IsFailed)
        batch.Draw(glyph.Value, new Rectangle(badge.X + 3, badge.Y + 3, 18, 18), ink);

    }
    batch.End();
  }

  private void DrawJointLines(Dictionary<string, UpgradeJoint> joints, Matrix viewProjection, float timeInSeconds,
    bool dimLocked = false)
  {

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

      var startVisual = joint.Value.StartButton.Button.Visual;
      var endVisual = joint.Value.EndButton.Button.Visual;
      float xStart = startVisual.AbsoluteLeft + buttonHalfSizeStart + joint.Value.StartOffset.X;
      float yStart = startVisual.AbsoluteTop + startVisual.Height / 2f + joint.Value.StartOffset.Y;
      float xEnd = endVisual.AbsoluteLeft + buttonHalfSizeEnd + joint.Value.EndOffset.X;
      float yEnd = endVisual.AbsoluteTop + endVisual.Height / 2f + joint.Value.EndOffset.Y;
      var color = OrbitSkin.MutedTextColor * (dimLocked && joint.Value.EndButton.State == UpgradeButton.UnlockState.Revealed ? 0.35f : 0.75f);
      var purchasedColor = OrbitSkin.AbilityAccent * 0.8f;

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

      bool purchased = joint.Value.State is UpgradeJoint.JointState.Purchased or UpgradeJoint.JointState.MaxedOut;
      bool purchasing = joint.Value.State == UpgradeJoint.JointState.Purchasing;
      D(xStart, yStart, xEnd, yEnd, joint.Value,
        purchased ? purchasedColor : color, (purchased ? purchasedColor : color) * 0.16f, purchased ? 1f : joint.Value.UnlockingTime);
      if (purchasing)
        D(xStart, yStart, xEnd, yEnd, joint.Value, OrbitSkin.Accent * 0.7f,
          OrbitSkin.Accent * 0.1f, joint.Value.PurchasingTime, joint.Value.PurchasingTime);

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
      try { WithScreenCamera(() => DrawContents(spriteBatch, drawHudBackground)); }
      finally { drawUpgradesGui = true; }
      return;
    }
    DrawContents(spriteBatch, drawHudBackground);
  }

  private void DrawContents(SpriteBatch spriteBatch, Action drawHudBackground)
  {
    // Gum's Draw(layer) overloads never reset the renderer's per-frame record of render
    // states (only Draw(SystemManagers) does), so it grew every frame: steady garbage and a
    // slow leak. Reset it once per frame before drawing any layer.
    SystemManagers.Default.Renderer.ClearPerformanceRecordingVariables();
    BaseGame.DimmingFactor = GameMain.IsPaused ? 0.5f : IsOverlayVisible ? DockedDimming : 0f;
    BaseGame.DrawBlurFilter = IsOverlayVisible || GameMain.IsPaused;


    // Gameplay controls must stay above the bar; the upgrade tree must stay below it.
    if (!drawUpgradesGui || GameMain.IsPaused)
      drawHudBackground();

    if (GameMain.IsPaused)
    {
      WithScreenCamera(() =>
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

    var bc = OrbitSkin.ButtonBorderColor;

    var camera = SystemManagers.Default.Renderer.Camera;
    var m = camera.GetTransformationMatrix(true).ToXNA();
    var timeInSeconds = (float)BaseGame.Time.TotalGameTime.TotalSeconds;

    var vp = BaseGame.BoxingViewportAdapterGui.Viewport;
    Matrix projectionMatrix = Matrix.CreateOrthographicOffCenter(0,
#if KNI_WEB
      1920, 1080,
#else
      vp.Width, vp.Height,
#endif
      0, 0f, -1f);
    Matrix viewProjection = m * projectionMatrix;

    // DrawToggleButton();

    if (drawUpgradesGui && m_upgradeWindowType is UpgradeTypes.Shipyard or UpgradeTypes.Signals)
    {
      if (m_upgradeWindowType == UpgradeTypes.Shipyard) DrawShipyard(spriteBatch);
      else DrawSignals(spriteBatch);
      DrawTitleBanner(spriteBatch);
      DrawTransparencySlider(spriteBatch);
    }
    else if (drawUpgradesGui)
    {

      // #if KNI_WEB
      //       _spriteBatch.Begin(SpriteSortMode.Immediate, effect: EffectCache.RectangleSdfFx, transformMatrix: m);
      // #else
      //       _spriteBatch.Begin(SpriteSortMode.Immediate, blendState, effect: EffectCache.RectangleSdfFx, transformMatrix: m);
      // #endif

      // m_lineRenderer.Begin(m, timeInSeconds);
      //       var vp = BaseGame.BoxingViewportAdapterGui.Viewport;
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
          DrawUpgradeNodeStatus(spriteBatch, UpgradeManager.CurrentUpgrades.UpgradeButtons, m, viewProjection);
          break;
        case UpgradeTypes.Abilities:
          DrawShipSystemsPanel(spriteBatch);
          DrawJointLines(UpgradeManager.CurrentUpgrades.UpgradeJointsAbilities, viewProjection, timeInSeconds, true);
          DrawButtonBorders(UpgradeManager.CurrentUpgrades.UpgradeButtonsAbilities, viewProjection, timeInSeconds);
          SystemManagers.Default.Draw([m_upgradesAbilitiesLayer, m_combinedLayer]);
          DrawUpgradeNodeStatus(spriteBatch, UpgradeManager.CurrentUpgrades.UpgradeButtonsAbilities, m, viewProjection);
          DrawShipSystemLabels(spriteBatch);
          break;
        case UpgradeTypes.Meta:
          DrawPrestigeTalentPanel(spriteBatch);
          DrawButtonBorders(UpgradeManager.CurrentUpgrades.UpgradeButtonsMeta, viewProjection, timeInSeconds);
          SystemManagers.Default.Draw([m_upgradesMetaLayer]);
          DrawUpgradeNodeStatus(spriteBatch, UpgradeManager.CurrentUpgrades.UpgradeButtonsMeta, m, viewProjection);
          DrawPrestigeTalentLabels(spriteBatch);
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
    {
#if !KNI_WEB
      DrawHudButton(spriteBatch, PopoutButton, IsDetached ? Loc.T("Dock") : popoutError != null ? Loc.T(popoutError) : Loc.T("Pop out"),
        HudLayout.UpgradeAccent, false, PopoutButton.Contains(GumService.Default.Cursor.X, GumService.Default.Cursor.Y), 0);
#endif
      drawHudBackground();
    }

    if (ChoosingTalents)
    {
      DrawToggleButtonApplyMeta(spriteBatch);
    }
    else if (!UntitledGemGameGameScreen.Instance.m_prestiging)
    {
      DrawToggleButtonUpgrades(spriteBatch);
      DrawToggleButtonAbilities(spriteBatch);
      DrawShipyardNavigation(spriteBatch);
      DrawSignalsNavigation(spriteBatch);
      DrawTalentsNavigation(spriteBatch);

      if (BulkUpgradeButtonsShown)
      {
        DrawToggleButtonUpgradeCheapest(spriteBatch);
        DrawToggleButtonUpgradeCheapest2(spriteBatch);
      }
    }
  }

  // Use the same explicit canvas transform for drawing and pointer hit-testing.
  private static void WithScreenCamera(Action action)
  {
    var camera = SystemManagers.Default.Renderer.Camera;
    var position = camera.Position;
    var zoom = camera.Zoom;
    var center = camera.CameraCenterOnScreen;
    try
    {
      camera.CameraCenterOnScreen = CameraCenterOnScreen.TopLeft;
      camera.Position = System.Numerics.Vector2.Zero;
      camera.Zoom =
#if KNI_WEB
        UntitledGemGame.Web.WebHudRenderScope.IsDrawing ? 0.5f : 1f;
#else
        1f;
#endif
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
      UpgradeTypes.Signals => Loc.T("Deep Space Signal"),
      UpgradeTypes.Shipyard => Loc.T("Shipyard"),
      UpgradeTypes.Abilities => ShipSystems.Name,
      UpgradeTypes.Meta => Loc.T("Prestige Upgrades"),
      _ => Loc.T("Upgrades")
    };
    Color accent = m_upgradeWindowType switch
    {
      UpgradeTypes.Abilities => OrbitSkin.AbilityAccent,
      UpgradeTypes.Shipyard => OrbitSkin.ShipyardAccent,
      UpgradeTypes.Signals => OrbitSkin.SignalAccent,
      UpgradeTypes.Meta => OrbitSkin.EpicRarity,
      _ => OrbitSkin.UpgradeAccent
    };
    const int height = 132;
    const float fontSize = 46f;
    float centerX = HudLayout.Width / 2f;
    var titleSize = Measure2(title, Vector2.Zero, fontSize);
    // var labelSize = Measure2("PROGRESSION", Vector2.Zero, 15f);
    int ruleWidth = (int)Math.Min(180, HudLayout.Width * 0.08f);
    int ruleGap = (int)(titleSize.X / 2) + 32;
    int lineThickness = HudLayout.ButtonBorderThickness;

    spriteBatch.Begin(transformMatrix: HudLayout.RenderTransform);
    spriteBatch.Draw(AssetManager.DefaultTexture,
      new Rectangle(0, 0, HudLayout.Width, height), OrbitSkin.PanelBackgroundTint);
    // Use the borderless artwork so its baked-in edge cannot split from our rule when scaled.
    OrbitSkin.NineSlice(spriteBatch, "modal_title_background", new Rectangle(0, 0, HudLayout.Width, height), 8);
    spriteBatch.Draw(AssetManager.DefaultTexture,
      new Rectangle(0, height - lineThickness, HudLayout.Width, lineThickness), accent * 0.45f);
    spriteBatch.Draw(AssetManager.DefaultTexture,
      new Rectangle((int)centerX - ruleGap - ruleWidth, 69 - lineThickness / 2, ruleWidth, lineThickness), accent * 0.55f);
    spriteBatch.Draw(AssetManager.DefaultTexture,
      new Rectangle((int)centerX + ruleGap, 69 - lineThickness / 2, ruleWidth, lineThickness), accent * 0.55f);
    spriteBatch.End();

    // FontManager.RenderFieldFont(() => ContentDirectory.Fonts.Roboto_Regular_ttf,
    //   "PROGRESSION", new Vector2(centerX - labelSize.X / 2, 22),
    //   OrbitSkin.MutedTextColor, Color.Black, 15f);
    FontManager.RenderFieldFont(nameof(ContentDirectory.Fonts.Roboto_Regular_ttf),
      title, new Vector2(centerX - titleSize.X / 2, 69 - titleSize.Y / 2),
      accent, Color.Black, fontSize);
  }

  public void DrawToggleButtonApplyMeta(SpriteBatch m_spriteBatch)
  {
    var mousePos = new Vector2(GumService.Default.Cursor.X, GumService.Default.Cursor.Y);
    var layout = HudLayout.NavigationButton(0);
    bool contains = new RectangleF(layout.X, layout.Y, layout.Width, layout.Height).Contains(mousePos);
    DrawHudButton(m_spriteBatch, layout, Loc.T("Begin run"),
      OrbitSkin.PrestigeAccent, true, contains, m_animateButtonClickUpgrades);

    var respec = HudLayout.NavigationButton(1);
    bool respecHovered = new RectangleF(respec.X, respec.Y, respec.Width, respec.Height).Contains(mousePos);
    bool enabled = UntitledGemGameGameScreen.Instance?.m_postPrestige == true
      && PrestigeTalentLayout.SpentPoints(UpgradeManager.CurrentUpgrades.UpgradeButtonsMeta) > 0;
    DrawHudButton(m_spriteBatch, respec, enabled ? Loc.T("Refund all") : Loc.T("No points spent"),
      OrbitSkin.EpicRarity * (enabled ? 1f : 0.45f), false, enabled && respecHovered, m_animateButtonClickAbilities);
  }

  public void DrawToggleButtonUpgrades(SpriteBatch m_spriteBatch)
  {
    if (ChoosingTalents) return;

    var mousePos = new Vector2(GumService.Default.Cursor.X, GumService.Default.Cursor.Y);
    var layout = HudLayout.NavigationButton(HudLayout.UpgradesTab);
    bool contains = new RectangleF(layout.X, layout.Y, layout.Width, layout.Height).Contains(mousePos);
    DrawHudButton(m_spriteBatch, layout, m_upgradeWindowType == UpgradeTypes.Upgrades ? Loc.T("Hide") : Loc.T("Upgrades"),
      OrbitSkin.UpgradeAccent, m_upgradeWindowType == UpgradeTypes.Upgrades, contains, m_animateButtonClickUpgrades, tab: true);
  }

  public void DrawToggleButtonAbilities(SpriteBatch m_spriteBatch)
  {
    if (ChoosingTalents || !ShipSystems.Online) return;

    var mousePos = new Vector2(GumService.Default.Cursor.X, GumService.Default.Cursor.Y);
    var layout = HudLayout.NavigationButton(HudLayout.SystemsTab);
    bool contains = new RectangleF(layout.X, layout.Y, layout.Width, layout.Height).Contains(mousePos);
    // Power cells are bought in the Systems window; the button says when one is affordable.
    DrawHudButton(m_spriteBatch, layout, m_upgradeWindowType == UpgradeTypes.Abilities ? Loc.T("Hide")
      : CanBuyPowerCell ? ShipSystems.NavigationLabel + " !" : ShipSystems.NavigationLabel,
      OrbitSkin.AbilityAccent, m_upgradeWindowType == UpgradeTypes.Abilities, contains, m_animateButtonClickAbilities, tab: true);
  }

  // Upgrade Cheapest and Spend All. The HUD's Damage button shares their slot and hides
  // while they show, including in the main window while the tree is popped out.
  public bool BulkUpgradeButtonsShown => m_upgradeWindowType == UpgradeTypes.Upgrades
    && UpgradeManager.Instance.ExpandSpaceLevel > 0;

  public void DrawToggleButtonUpgradeCheapest(SpriteBatch m_spriteBatch)
  {
    if (m_upgradeWindowType != UpgradeTypes.Upgrades) return;

    var mousePos = new Vector2(GumService.Default.Cursor.X, GumService.Default.Cursor.Y);
    var layout = HudLayout.BulkUpgradeButton(0);
    bool contains = new RectangleF(layout.X, layout.Y, layout.Width, layout.Height).Contains(mousePos);
    DrawHudButton(m_spriteBatch, layout, Loc.T("Upgrade Cheapest"),
      HudLayout.UpgradeAccent, false, contains, m_animateButtonClickCheapestUpgrade);
  }

  public void DrawToggleButtonUpgradeCheapest2(SpriteBatch m_spriteBatch)
  {
    if (m_upgradeWindowType != UpgradeTypes.Upgrades) return;

    var mousePos = new Vector2(GumService.Default.Cursor.X, GumService.Default.Cursor.Y);
    var layout = HudLayout.BulkUpgradeButton(1);
    bool contains = new RectangleF(layout.X, layout.Y, layout.Width, layout.Height).Contains(mousePos);
    DrawHudButton(m_spriteBatch, layout, Loc.T("Spend All"),
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
            UntitledGemGameGameScreen.Instance.GrantRunStartRewards();
            UntitledGemGameGameScreen.Instance.SaveProgress();
          }, 350, true);
    }

    var respec = HudLayout.NavigationButton(1);
    bool overRespec = new RectangleF(respec.X, respec.Y, respec.Width, respec.Height).Contains(mousePos);
    if (overRespec && isMouseClicked
      && UntitledGemGameGameScreen.Instance?.m_postPrestige == true
      && PrestigeTalentLayout.SpentPoints(UpgradeManager.CurrentUpgrades.UpgradeButtonsMeta) > 0)
    {
      UpgradeManager.Instance.RespecPrestigeTalents();
      m_animateButtonClickAbilities = 0.001f;
    }
  }

  private void UpdateButtonUpgrades()
  {
    if (ChoosingTalents) return;

    var mouse = GameInput.Mouse;
    bool isMouseClicked = mouse.WasButtonPressed(MouseButton.Left);
    var mousePos = GameInput.UiCursor;
    var layout = HudLayout.NavigationButton(HudLayout.UpgradesTab);
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
    if (ChoosingTalents || !ShipSystems.Online) return;

    var mouse = GameInput.Mouse;
    bool isMouseClicked = mouse.WasButtonPressed(MouseButton.Left);
    var mousePos = GameInput.UiCursor;
    var layout = HudLayout.NavigationButton(HudLayout.SystemsTab);
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
    var layout = HudLayout.BulkUpgradeButton(0);
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
    var layout = HudLayout.BulkUpgradeButton(1);
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
      // Core Shards buy a deliberate build choice; bulk buying must never spend them.
      if (button.Data.UpgradeDefinition.Currency == CoreShards.Currency)
        continue;

      var cost = button.GetNextLevelCost();

      if (cost < cheapest && button.CurrentLevel < button.Data.NumLevels && button.CanAfford && button.Button.IsEnabled)
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
    Color accent, bool selected, bool hovered, float clickAnimation, bool tab = false,
    string modalAsset = null, bool confirm = false)
  {
    float pulse = clickAnimation > 0 ? MathF.Sin(Math.Clamp(clickAnimation, 0f, 1f) * MathHelper.Pi) : 0;
    spriteBatch.Begin(transformMatrix: HudLayout.RenderTransform);
    OrbitSkin.Button(spriteBatch, bounds, selected || hovered, pulse, tab, modalAsset, confirm);
    if (tab)
    {
      int thickness = HudLayout.ButtonBorderThickness;
      // Cover the artwork's teal underline, then mark the actual selected tab.
      spriteBatch.Draw(AssetManager.DefaultTexture,
        new Rectangle(bounds.X, bounds.Bottom - thickness * 3, bounds.Width, thickness * 3), OrbitSkin.PanelColor);
      spriteBatch.Draw(AssetManager.DefaultTexture,
        new Rectangle(bounds.X, bounds.Bottom - thickness, bounds.Width, thickness),
        selected ? accent : hovered ? accent * 0.6f : OrbitSkin.BorderColor);
    }
    spriteBatch.End();

    float fontSize = modalAsset != null ? 40f : bounds.Y >= HudLayout.Top ? 38f : bounds.Height >= 100 ? 32f : 28f;
    var measure = Measure2(text, Vector2.Zero, fontSize);
    float fit = Math.Min(1f, Math.Min(Math.Max(1, bounds.Width - 24) / Math.Max(1f, measure.X),
      Math.Max(1, bounds.Height - 16) / Math.Max(1f, measure.Y)));
    fontSize *= fit;
    measure = Measure2(text, Vector2.Zero, fontSize);
    FontManager.RenderFieldFont(nameof(ContentDirectory.Fonts.Roboto_Regular_ttf), text,
      new Vector2(bounds.Center.X - measure.X / 2, bounds.Center.Y - measure.Y / 2),
      confirm ? OrbitSkin.ConfirmAccent : tab && (selected || hovered) ? accent : OrbitSkin.ButtonTextColor, Color.Black, fontSize);
  }

  private float m_animateButtonClickUpgrades = 0.0f;
  private float m_animateButtonClickAbilities = 0.0f;
  private float m_animateButtonClickShipyard = 0.0f;
  private float m_animateButtonClickSignals = 0.0f;
  private float m_animateButtonClickCheapestUpgrade = 0.0f;

  public Vector2 Measure2(string Text, Vector2 position, float FontSize)
  {
    var r = FontManager.GetTextRenderer("Roboto_Regular_ttf");
    r.PositiveYIsDown = true;
    // MeasureText leaves the layout alone, so texts batched with BeginFieldFonts survive.

    var fontSize = FontSize;
    var measure = r.MeasureText(Text, position, 1, 1.171875f, fontSize, Color.Transparent, Color.Transparent, r.EnableKerning, r.PositiveYIsDown, r.PositionByBaseline, 0, new Vector2(0, 0), true, -1);
    return measure;
  }



  private readonly System.Runtime.CompilerServices.ConditionalWeakTable<UpgradeJoint, JointPath> jointPaths = new();

  private sealed class JointPath
  {
    public Vector2[] Points;
    public Vector2[] RawPoints;
    public RectangleF StartBounds, EndBounds, StartBadge, EndBadge;
    public float[] Lengths;
    public float TotalDistance;
    public JointPath(int count) { Points = new Vector2[count]; RawPoints = new Vector2[count]; Lengths = new float[count - 1]; }
  }

  private RectangleF GetConnectionNodeBounds(UpgradeButton node)
  {
    var visual = node.Button.Visual;
    float frame = node.Data.UpgradeDefinition.Currency == CoreShards.Currency ? 8f : 3f;
    return new RectangleF(visual.AbsoluteLeft - frame, visual.AbsoluteTop - frame,
      visual.Width + frame * 2, visual.Height + frame * 2);
  }

  private RectangleF GetConnectionBadgeBounds(UpgradeButton node)
  {
    if (IsUpgradeNodeLocked(node) || (!node.IsMaxLevel && !CanBuyUpgradeNode(node))) return default;
    var visual = node.Button.Visual;
    return new RectangleF(visual.AbsoluteLeft + visual.Width - 12, visual.AbsoluteTop - 16, 28, 28);
  }

  private JointPath GetJointPath(UpgradeJoint joint, Vector2 start, Vector2 end, Vector2 routeOffset)
  {
    int count = joint.MidwayPoints.Count + 2;
    if (!jointPaths.TryGetValue(joint, out var path) || path.RawPoints.Length != count)
    {
      jointPaths.Remove(joint);
      path = new JointPath(count);
      jointPaths.Add(joint, path);
    }
    var startBounds = GetConnectionNodeBounds(joint.StartButton);
    var endBounds = GetConnectionNodeBounds(joint.EndButton);
    var startBadge = GetConnectionBadgeBounds(joint.StartButton);
    var endBadge = GetConnectionBadgeBounds(joint.EndButton);
    bool changed = !path.StartBadge.Equals(startBadge) || !path.EndBadge.Equals(endBadge) || path.RawPoints[0] != start || path.RawPoints[count - 1] != end
      || !path.StartBounds.Equals(startBounds) || !path.EndBounds.Equals(endBounds);
    path.RawPoints[0] = start;
    path.RawPoints[count - 1] = end;
    for (int i = 0; i < count - 2; i++)
    {
      var point = joint.MidwayPoints[i];
      var value = new Vector2(point.X + routeOffset.X, point.Y + routeOffset.Y);
      changed |= path.RawPoints[i + 1] != value;
      path.RawPoints[i + 1] = value;
    }
    if (changed)
    {
      path.StartBounds = startBounds;
      path.EndBounds = endBounds;
      path.StartBadge = startBadge;
      path.EndBadge = endBadge;
      path.Points = (Vector2[])path.RawPoints.Clone();
      UpgradeConnectionGeometry.TrimEndpoints(path.Points, startBounds, endBounds, 1f);
      if (startBadge.Width > 0) UpgradeConnectionGeometry.TrimBadge(path.Points, startBadge, false);
      if (endBadge.Width > 0) UpgradeConnectionGeometry.TrimBadge(path.Points, endBadge, true);
      path.Points = UpgradeConnectionGeometry.RoundCorners(path.Points, 14f);
      path.Lengths = new float[Math.Max(0, path.Points.Length - 1)];
      path.TotalDistance = 0;
      for (int i = 0; i < path.Lengths.Length; i++)
      {
        path.Lengths[i] = Vector2.Distance(path.Points[i], path.Points[i + 1]);
        path.TotalDistance += path.Lengths[i];
      }
    }
    return path;
  }

  private void D(float xStart, float yStart, float xEnd, float yEnd, UpgradeJoint joint, Color colorCore, Color colorGlow, float d, float pulse = -1f)
  {
    if (d <= 0f) return;
    float buttonSize = joint.StartButton.Button.Width;
    float buttonHalfSize = buttonSize / 2.0f;

    var source = joint.StartButton.Button;
    var routeOffset = new Vector2(source.Visual.AbsoluteLeft - source.X + buttonHalfSize,
      source.Visual.AbsoluteTop - source.Y + source.Height / 2f);
    var path = GetJointPath(joint, new Vector2(xStart, yStart), new Vector2(xEnd, yEnd), routeOffset);
    float allowedDistance = path.TotalDistance * MathHelper.Clamp(d, 0f, 1f);
    DrawConnectionRange(path, 0, allowedDistance, 1.3f, colorCore, colorGlow);
    if (pulse >= 0 && path.TotalDistance > 0)
    {
      float head = path.TotalDistance * MathHelper.Clamp(pulse, 0f, 1f);
      // A short luminous tail and a crisp white tip, measured along the route.
      DrawConnectionRange(path, Math.Max(0, head - 12f), head, 1.5f,
        OrbitSkin.Accent, OrbitSkin.Accent * 0.28f, joined: false);
      DrawConnectionRange(path, Math.Max(0, head - 3f), head, 2f,
        Color.White, OrbitSkin.Accent * 0.6f, joined: false);
    }
  }

  private void DrawConnectionRange(JointPath path, float from, float to, float thickness, Color core, Color glow, bool joined = true)
  {
    float distance = 0;
    for (int i = 0; i < path.Lengths.Length; i++)
    {
      float length = path.Lengths[i];
      float end = distance + length;
      if (length > 0.0001f && end > from && distance < to)
      {
        var startPoint = Vector2.Lerp(path.Points[i], path.Points[i + 1], Math.Clamp((from - distance) / length, 0f, 1f));
        var endPoint = Vector2.Lerp(path.Points[i], path.Points[i + 1], Math.Clamp((to - distance) / length, 0f, 1f));
        float halfWidth = thickness + m_lineRenderer.BaseGlowPadding;
        var normal = Vector2.Normalize(path.Points[i + 1] - path.Points[i]);
        normal = new Vector2(-normal.Y, normal.X) * halfWidth;
        var startJoin = from <= distance ? ConnectionJoin(path, i, halfWidth) : normal;
        var endJoin = to >= end ? ConnectionJoin(path, i + 1, halfWidth) : normal;
        if (joined)
          m_lineRenderer.DrawLine(startPoint, endPoint, thickness, core, glow,
            startJoin: startJoin, endJoin: endJoin);
        else
        {
          // Keep rounded caps on the moving spark's short tail and white tip.
          float visibleLength = Vector2.Distance(startPoint, endPoint);
          m_lineRenderer.DrawLine(startPoint, endPoint, thickness,
            core * Math.Min(1f, visibleLength / (thickness * 2f)),
            glow * Math.Min(1f, visibleLength / 12f));
        }
      }
      distance = end;
      if (distance >= to) break;
    }
  }
  private static Vector2 ConnectionJoin(JointPath path, int index, float halfWidth)
    => UpgradeConnectionGeometry.JoinOffset(path.Points, index, halfWidth);

}
