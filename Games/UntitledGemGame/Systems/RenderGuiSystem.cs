using Apos.Shapes;
using AsyncContent;
using GUI.Shared.Helpers;
using Gum;
using Gum.Wireframe;
using JapeFramework;
using JapeFramework.Aseprite;
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
  public bool HasInputFocus => !PlaytestFeedback.IsOpen && ((GameMain.Instance.IsActive && !GameMain.SoundEffectsLabCapturesInput)
#if !KNI_WEB
    || popout?.Focused == true || UntitledGemGame.Capture.CaptureSession.Active
#endif
    );
#if !KNI_WEB
  private UpgradePopoutWindow popout;
  private bool preferPopout;
  private bool popoutHadFocus;
  private string popoutError;
  private TimeSpan previousInactiveSleep;

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
    DisposeSignalParticles();
    DisposeTerminalRenderTarget();
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
    var previousType = m_upgradeWindowType;
    if (m_upgradeWindowType != type)
      AudioManager.Instance.PlaySound(type == UpgradeTypes.None ? SoundEvent.WindowClose : SoundEvent.WindowOpen);
    m_upgradeWindowType = type;

    drawUpgradesGui = type != UpgradeTypes.None;
#if !KNI_WEB
    if (drawUpgradesGui && preferPopout) OpenPopout();
#endif
    NotifyTerminalTransition(previousType, type);

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

  private void UpdateUpgradeTreeInput(GameTime gameTime)
  {
    bool isUpgradeTree = m_upgradeWindowType == UpgradeTypes.Upgrades;
    bool suppressTreeCursor = (isUpgradeTree && !UpgradesTreeViewport.Contains(SalvageCursor))
      || (TryGetActivePanelBounds(out _, out var closeButton) && closeButton.Contains(SalvageCursor));
    if (suppressTreeCursor)
      ClearGumCursorInput();

    var roots = suppressTreeCursor
      ? rootItems.Concat(combinedItems)
      : rootItems.Concat(skillTreeItems).Concat(combinedItems);
    UpdateMenuInput(gameTime, roots);
  }

  public static Rectangle UpgradesPanel => PanelNavigationLayout.Panel;
  private const int UpgradesHeaderHeight = 140;
  public static int UpgradesHeaderCenterY => UpgradesPanel.Y + UpgradesHeaderHeight / 2;
  public static Rectangle UpgradesExtractButton => new(
    UpgradesPanel.Center.X - 208, UpgradesHeaderCenterY - 46, 416, 92);
  public static Rectangle UpgradesTreeViewport => new(
    UpgradesPanel.X + 20,
    UpgradesPanel.Y + UpgradesHeaderHeight + 4,
    UpgradesPanel.Width - 40,
    UpgradesPanel.Height - UpgradesHeaderHeight - 24);
  public static Rectangle UpgradesCloseButton
    => GuiSkin.PanelCloseButtonBounds(PanelNavigationLayout.Panel, size: 56, marginX: 36, marginY: 24);

  // Return true whenever a panel owns Escape, including mandatory choice screens.
  public bool BackOneLayer()
  {
    if (revealingModule != ShipModule.None)
    {
      if (moduleRevealAge < ModuleRevealDuration)
        moduleRevealAge = ModuleRevealDuration;
      else if (UntitledGemGameGameScreen.Instance.TryAcknowledgeModule(revealingModule))
        revealingModule = ShipModule.None;
      return true;
    }
    if (draggedModule != ShipModule.None)
    {
      CancelModuleDrag();
      return true;
    }
    if (m_upgradeWindowType == UpgradeTypes.None)
      return false;

    ClearGumCursorInput();
    // Mandatory talent/signal choices have no Back button.
    if (TryGetActivePanelBounds(out _, out _))
      SetUpgradeType(UpgradeTypes.None);
    return true;
  }

  public bool PanelDismissInputCaptured { get; private set; }

  public bool TryGetActivePanelBounds(out Rectangle panel, out Rectangle closeButton)
  {
    panel = Rectangle.Empty;
    closeButton = Rectangle.Empty;
    if (ChoosingTalents) return false;
    switch (m_upgradeWindowType)
    {
      case UpgradeTypes.Upgrades:
        panel = UpgradesPanel;
        closeButton = UpgradesCloseButton;
        return true;
      case UpgradeTypes.Meta when TalentsReadOnly:
        panel = PrestigeTalentPanelBounds;
        closeButton = PrestigeTalentCloseButton;
        return true;
      case UpgradeTypes.Abilities:
        panel = ShipSystems.Panel;
        closeButton = ShipSystemsCloseButton;
        return true;
      case UpgradeTypes.Shipyard:
        panel = ShipyardPanel;
        closeButton = ShipyardCloseButton;
        return true;
      case UpgradeTypes.Signals when !signalChoicesVisible && !IsSelectingSignal:
        panel = SignalPanel;
        closeButton = SignalCloseButton;
        return true;
      default:
        return false;
    }
  }

  private void ClearGumCursorInput()
  {
    GumService.Default.Cursor.ClearInputValues();
    GumService.Default.Cursor.VisualPushed = null;
    GumService.Default.Cursor.VisualOver = null;
    UpgradeManager.Instance.HideTooltip();
  }

  private bool UpdateActivePanelDismiss()
  {
    if (IsDetached && !IsPopoutFocused)
      return false;
    if (!TryGetActivePanelBounds(out _, out var closeButton))
      return false;
    if (m_upgradeWindowType == UpgradeTypes.Shipyard && draggedModule != ShipModule.None)
      return false;
    if (!GameInput.Mouse.WasButtonPressed(MouseButton.Left))
      return false;
    var cursor = SalvageCursor;
    if (!closeButton.Contains(cursor))
      return false;

    PanelDismissInputCaptured = true;
    ClearGumCursorInput();
    AudioManager.Instance.PlaySound(SoundEvent.WindowClose);
    SetUpgradeType(UpgradeTypes.None);
    return true;
  }

  public void Update(GameTime gameTime)
  {
    SalvageInputCaptured = false;
    if (PanelDismissInputCaptured && !GameInput.Mouse.IsButtonDown(MouseButton.Left))
      PanelDismissInputCaptured = false;
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
      PanelPointer = new Vector2(-1000, -1000);
      CancelModuleDrag();
      draggingTransparency = false;
      GumService.Default.Cursor.ClearInputValues();
      GumService.Default.Cursor.VisualOver = null;
      GumService.Default.Cursor.VisualPushed = null;
      return;
    }
    PanelPointer = ReadPanelPointer;
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
    UpdateTerminalAnimation(dt);
    if (UpdatePanelNavigation()) return;
    if (UpdateUpgradesHeaderActions()) return;
    if (UpdateModuleSalvageInput(dt)) return;
    if (UpdateActivePanelDismiss()) return;

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
      bool allowZoom = UpgradesTreeViewport.Contains(SalvageCursor);
      if (allowZoom && state.DeltaScrollWheelValue > 10)
      {
        targetZoom -= state.DeltaScrollWheelValue * 0.0005f;
      }
      else if (allowZoom && state.DeltaScrollWheelValue < -10)
      {
        targetZoom -= state.DeltaScrollWheelValue * 0.0005f;
      }

      targetZoom = Math.Clamp(targetZoom, MinUpgradeZoom, MaxUpgradeZoom);
      camera.Zoom = Math.Clamp(
        MathHelper.Lerp(camera.Zoom, targetZoom, Math.Clamp(dt * 5.0f, 0f, 1f)),
        MinUpgradeZoom, MaxUpgradeZoom);

      if (UpgradesTreeViewport.Contains(SalvageCursor)
        && (state.MiddleButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed
          || state.RightButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed))
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
        UpdateUpgradeTreeInput(gameTime);
        SystemManagers.Default.Activity(gameTime.TotalGameTime.TotalSeconds);
      }
      else
      {
        WithScreenCamera(() => GumService.Default.Update(gameTime, rootItems.Concat(hudItems).Concat(combinedItems)));
      }
      UpdateNavigationButtons(dt);
      return;
    }
#endif
    if (drawUpgradesGui)
    {
      UpdateUpgradeTreeInput(gameTime);
    }
    else
    {
      GumService.Default.Update(gameTime, rootItems.Concat(hudItems).Concat(combinedItems));
    }

    UpdateNavigationButtons(dt);
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
    GuiSkin.Slider(batch, track, value);
    batch.End();
    const float size = 28;
    var measured = Measure2(label, Vector2.Zero, size);
    FontManager.RenderFieldFont(nameof(ContentDirectory.Fonts.Roboto_Regular_ttf), label,
      new Vector2(box.Left, box.Center.Y - measured.Y / 2), GuiSkin.ButtonTextColor, Color.Black, size);
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
      if (HudLayout.BottomBarVisible)
      {
        UpdateButtonUpgrades();
        UpdateButtonAbilities();
        UpdateTalentsNavigation();
      }
      UpdateShipSystemsInput(dt);
      UpdateShipyardInput(dt);
      UpdateSignalsInput();
      if (HudLayout.BottomBarVisible && BulkUpgradeButtonsShown)
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
          buttonVis.AbsoluteRight + 48, buttonVis.AbsoluteBottom + 48,
          viewProjection)) continue;
      // Use the result of input traversal, including parent bounds and overlays.
      // Retesting during draw can light a node that cannot receive a click.
      bool isHovered = ReferenceEquals(GumService.Default.Cursor.VisualOver, buttonVis);

      if (buttonVis.Visible && button.IsVisible && ub.Value.State >= UpgradeButton.UnlockState.Revealed && buttonVis.Children.Count > 3)
      {
        var r2 = new RectangleF(button.Visual.AbsoluteLeft - 2, button.Visual.AbsoluteTop - 2, button.Visual.AbsoluteWidth + 4, button.Visual.AbsoluteHeight + 4);

        bool locked = IsUpgradeNodeLocked(ub.Value);
        bool canBuy = CanBuyUpgradeNode(ub.Value);
        var color = locked ? GuiSkin.BorderColor * 0.65f
          : isHovered ? GuiSkin.HoverAccent
          : canBuy ? GuiSkin.UpgradeAccent
          : ub.Value.IsMaxLevel ? GuiSkin.ConfirmAccent * 0.85f
          : ub.Value.CurrentLevel > 0 ? GuiSkin.Accent
          : GuiSkin.DisabledTextColor * 0.85f;
        float glow = canBuy ? 0.28f : isHovered ? 0.22f : 0.1f;
        // One restrained outline. White double strokes made maxed nodes look actionable.
        if (GuiSkin.IsBuiltIn(GuiSpriteSlot.TreeNodeBorder))
          m_rectangleRender.DrawRect(r2.ToRectangle(), canBuy ? 1.8f : 1.1f, 2f,
            color, color * glow, locked ? 0 : ub.Value.ClickedTime, isHovered);

        // The outer frame turns green after purchase; the shard badge keeps the currency identity.
        if (ub.Value.Data.UpgradeDefinition.Currency == CoreShards.Currency)
        {
          var frame = new RectangleF(r2.X - 5, r2.Y - 5, r2.Width + 10, r2.Height + 10);
          var frameColor = CoreShardNodeFrameColor(ub.Value) * (locked ? 0.45f : 0.85f);
          m_rectangleRender.DrawRect(frame.ToRectangle(), 1.2f, 6f, frameColor, frameColor * 0.12f, 0, false);
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

  private static Color CoreShardNodeFrameColor(UpgradeButton node)
    => node.CurrentLevel > 0 ? GuiSkin.ConfirmAccent : CoreShards.Color;

  private void DrawCoreShardNodeMarker(SpriteBatch batch, Rectangle bounds, UpgradeButton node)
  {
    bool locked = IsUpgradeNodeLocked(node);
    var gold = CoreShards.Color * (locked ? 0.45f : 1f);
    var frameColor = CoreShardNodeFrameColor(node) * (locked ? 0.45f : 1f);
    const int arm = 14, thickness = 3;
    var frame = new Rectangle(bounds.X - 8, bounds.Y - 8, bounds.Width + 16, bounds.Height + 16);
    // Corner brackets give these nodes a different silhouette from ordinary upgrades.
    for (int horizontal = 0; horizontal < 2; horizontal++)
      for (int vertical = 0; vertical < 2; vertical++)
      {
        batch.Draw(AssetManager.DefaultTexture, new Rectangle(
          horizontal == 0 ? frame.Left : frame.Right - arm,
          vertical == 0 ? frame.Top : frame.Bottom - thickness, arm, thickness), frameColor);
        batch.Draw(AssetManager.DefaultTexture, new Rectangle(
          horizontal == 0 ? frame.Left : frame.Right - thickness,
          vertical == 0 ? frame.Top : frame.Bottom - arm, thickness, arm), frameColor);
      }

    var badge = new Rectangle(bounds.X - 14, bounds.Y - 14, 28, 28);
    GuiSkin.FlatPanel(batch, badge, GuiSkin.PanelColor, gold, 2);
    var shardTex = GemSurfaceTexture.CoreShardIcon();
    batch.Draw(shardTex, new Rectangle(badge.X + 1, badge.Y + 1, 26, 26), GemSurfaceTexture.CoreShardCrop(shardTex),
      locked ? Color.White * 0.45f : Color.White);
  }

  private static readonly RasterizerState ScissorRasterizer = new()
  {
    CullMode = CullMode.None,
    ScissorTestEnable = true
  };

  private readonly InvisibleRenderable upgradesTreeClip = new() { ClipsChildren = true };
  private readonly Layer clippedUpgradesLayer = new();

  private static Rectangle ToScissorRectangle(GraphicsDevice device, Rectangle hudBounds)
  {
    var transform = HudLayout.RenderTransform;
    var topLeft = Vector2.Transform(new Vector2(hudBounds.Left, hudBounds.Top), transform);
    var bottomRight = Vector2.Transform(new Vector2(hudBounds.Right, hudBounds.Bottom), transform);
    int vpW = device.Viewport.Width;
    int vpH = device.Viewport.Height;
    int x = Math.Clamp((int)MathF.Floor(topLeft.X), 0, vpW);
    int y = Math.Clamp((int)MathF.Floor(topLeft.Y), 0, vpH);
    int r = Math.Clamp((int)MathF.Ceiling(bottomRight.X), x, vpW);
    int b = Math.Clamp((int)MathF.Ceiling(bottomRight.Y), y, vpH);
    return new Rectangle(x, y, Math.Max(0, r - x), Math.Max(0, b - y));
  }

  private void DrawUpgradesPanelBackground(SpriteBatch batch)
  {
    var panel = UpgradesPanel;
    batch.Begin(transformMatrix: HudLayout.RenderTransform);
    // The detached window already supplies the backdrop, including its opacity.
    if (IsDetached)
      GuiSkin.PanelFrame(batch, panel, GuiSkin.PanelAccent);
    else
      DrawNavigationPanelBackground(batch, GuiSkin.PanelAccent);
    batch.End();
  }

  private void DrawUpgradesPanelChrome(SpriteBatch batch)
  {
    var panel = UpgradesPanel;
    var closeBtn = UpgradesCloseButton;
    batch.Begin(transformMatrix: HudLayout.RenderTransform);
    if (IsDetached)
    {
      var headerMask = new Rectangle(panel.X + 12, panel.Y + 12, panel.Width - 24, UpgradesHeaderHeight - 10);
      batch.Draw(GuiSkin.Pixel(batch.GraphicsDevice), headerMask, GuiPalette.Base(new Color(5, 16, 28, 248)));
    }
    DrawCleanHorizDivider(batch, panel.X + 48, panel.Y + UpgradesHeaderHeight, panel.Width - 96, GuiSkin.PanelAccent);
    GuiSkin.CloseButton(batch, closeBtn, GuiSkin.PanelAccent, closeBtn.Contains(PanelPointer));
    batch.End();

    const string font = nameof(ContentDirectory.Fonts.Roboto_Regular_ttf);
    FontManager.RenderFieldFont(font, Loc.Upper(Loc.T("Upgrades")),
      new Vector2(panel.X + 48, panel.Y + 34), GuiSkin.PanelAccent, Color.Black, 38);
    DrawFittedSystemText(Loc.T("Right or middle drag to pan  •  Scroll wheel to zoom"),
      new Vector2(panel.X + 48, panel.Y + 88), Math.Max(400, UpgradesExtractButton.Left - panel.X - 100), 24, GuiSkin.MutedTextColor);
  }

  private void UpdateUpgradesTreeClip(Rectangle scissor)
  {
    var camera = SystemManagers.Default.Renderer.Camera;
    // Gum owns its SpriteBatch state, so setting the device scissor alone does not
    // clip icons. Map its clip container through the exact draw transform, including
    // camera centering and pixel snapping, so it matches the shader passes.
    var inverse = Matrix.Invert(camera.GetTransformationMatrix(true).ToXNA());
    var topLeft = Vector2.Transform(new Vector2(scissor.Left, scissor.Top), inverse);
    var bottomRight = Vector2.Transform(new Vector2(scissor.Right, scissor.Bottom), inverse);
    upgradesTreeClip.X = topLeft.X;
    upgradesTreeClip.Y = topLeft.Y;
    upgradesTreeClip.Width = bottomRight.X - topLeft.X;
    upgradesTreeClip.Height = bottomRight.Y - topLeft.Y;
  }

  private void DrawClippedUpgradeVisuals(Rectangle scissor)
  {
    UpdateUpgradesTreeClip(scissor);
    // Wrap the draw traversal without reparenting the UI: world coordinates and
    // input bounds stay intact, and partially visible nodes keep their visible part.
    foreach (var visual in m_upgradesLayer.Renderables)
      if (visual.Parent == null)
        upgradesTreeClip.Children.Add(visual);
    if (clippedUpgradesLayer.Renderables.Count == 0)
      clippedUpgradesLayer.Add(upgradesTreeClip);
    clippedUpgradesLayer.LayerCameraSettings = m_upgradesLayer.LayerCameraSettings;
    clippedUpgradesLayer.IsLinearFilteringEnabled = m_upgradesLayer.IsLinearFilteringEnabled;
    try
    {
      SystemManagers.Default.Draw([clippedUpgradesLayer, m_combinedLayer]);
    }
    finally
    {
      upgradesTreeClip.Children.Clear();
    }
  }

  private void DrawUpgradesTree(SpriteBatch spriteBatch, Matrix m, Matrix viewProjection, float timeInSeconds)
  {
    var buttons = UpgradeManager.CurrentUpgrades.UpgradeButtons;
    var joints = UpgradeManager.CurrentUpgrades.UpgradeJoints;
    DrawUpgradesPanelBackground(spriteBatch);
    var device = spriteBatch.GraphicsDevice;
    var oldScissor = device.ScissorRectangle;
    var oldRasterizer = device.RasterizerState;
    var scissor = ToScissorRectangle(device, UpgradesTreeViewport);
    try
    {
      device.ScissorRectangle = scissor;
      DrawJointLines(joints, viewProjection, timeInSeconds, rasterizerState: ScissorRasterizer);
      device.RasterizerState = ScissorRasterizer;
      DrawButtonBorders(buttons, viewProjection, timeInSeconds);
      DrawClippedUpgradeVisuals(scissor);
      device.ScissorRectangle = scissor;
      DrawUpgradeNodeStatus(spriteBatch, buttons, m, viewProjection, ScissorRasterizer);
    }
    finally
    {
      device.ScissorRectangle = oldScissor;
      device.RasterizerState = oldRasterizer;
    }
    DrawUpgradesPanelChrome(spriteBatch);
    DrawUpgradesHeaderActions(spriteBatch);
    UntitledGemGameGameScreen.Instance?.DrawUpgradesExtractControl();
  }

  private void DrawUpgradeNodeStatus(SpriteBatch batch, Dictionary<string, UpgradeButton> buttons,
    Matrix transform, Matrix viewProjection, RasterizerState rasterizerState = null)
  {
    batch.Begin(transformMatrix: transform, samplerState: SamplerState.LinearClamp, rasterizerState: rasterizerState);
    foreach (var node in buttons.Values)
    {
      var visual = node.Button.Visual;
      if (!visual.Visible || !node.Button.IsVisible || node.State < UpgradeButton.UnlockState.Revealed) continue;
      int x = (int)visual.AbsoluteLeft, y = (int)visual.AbsoluteTop;
      int width = (int)visual.AbsoluteWidth, height = (int)visual.AbsoluteHeight;
      if (!RenderVisibility.Intersects(x - 16, y - 16, x + width + 16, y + height + 16, viewProjection)) continue;
      bool locked = IsUpgradeNodeLocked(node);
      bool canBuy = CanBuyUpgradeNode(node);
      if (!GuiSkin.IsBuiltIn(GuiSpriteSlot.TreeNodeBorder))
      {
        bool hovered = ReferenceEquals(GumService.Default.Cursor.VisualOver, visual);
        var borderColor = locked ? GuiSkin.BorderColor * 0.65f
          : hovered ? GuiSkin.HoverAccent
          : canBuy ? GuiSkin.UpgradeAccent
          : node.IsMaxLevel ? GuiSkin.ConfirmAccent * 0.85f
          : node.CurrentLevel > 0 ? GuiSkin.Accent
          : GuiSkin.DisabledTextColor * 0.85f;
        GuiSkin.DrawTreeNodeBorder(batch, new Rectangle(x - 2, y - 2, width + 4, height + 4), borderColor);
      }
      if (node.Data.UpgradeDefinition.Currency == CoreShards.Currency)
        DrawCoreShardNodeMarker(batch, new Rectangle(x, y, width, height), node);
      if (locked) continue;
      if (m_upgradeWindowType == UpgradeTypes.Meta)
      {
        bool isFree = PrestigeTalentLayout.IsFreeReward(node.Data.ShortName);
        var pipColor = node.IsMaxLevel ? (isFree ? FreeAccent : GuiSkin.ConfirmAccent)
          : canBuy ? (isFree ? FreeAccent : GuiSkin.UpgradeAccent)
          : GuiSkin.BorderColor * 0.75f;
        GuiSkin.DrawHexPip(batch, new Rectangle(x + width / 2 - 9, y + height - 9, 18, 18),
          pipColor, active: node.CurrentLevel > 0 || canBuy);
        continue;
      }
      // An empty rail means untouched; a filled portion means purchased ranks.
      // Keep this inside the icon frame so it cannot collide with talent names.
      batch.Draw(AssetManager.DefaultTexture,
        new Rectangle(x + 4, y + height - 14, Math.Max(1, width - 8), 11), GuiSkin.PanelColor);
      var rail = new Rectangle(x + 10, y + height - 8, Math.Max(1, width - 20), 5);
      batch.Draw(AssetManager.DefaultTexture, rail, GuiSkin.NodeProgressAccent * 0.28f);
      float fraction = Math.Clamp(node.CurrentLevel / (float)Math.Max(1, node.Data.NumLevels), 0, 1);
      if (fraction > 0)
      {
        int minimum = Math.Min(6, rail.Width);
        int filled = node.IsMaxLevel ? rail.Width
          : Math.Clamp((int)(rail.Width * fraction), minimum, Math.Max(minimum, rail.Width - 6));
        batch.Draw(AssetManager.DefaultTexture, new Rectangle(rail.X, rail.Y, filled, rail.Height),
          GuiSkin.NodeProgressAccent);
      }
      if (m_upgradeWindowType == UpgradeTypes.Abilities || (!canBuy && !node.IsMaxLevel)) continue;
      var badge = new Rectangle(x + width - 10, y - 14, 24, 24);
      var ink = canBuy ? GuiSkin.UpgradeAccent : GuiSkin.ConfirmAccent;
      GuiSkin.FlatPanel(batch, badge, GuiSkin.PanelColor, ink, 2);
      var glyph = canBuy ? TextureCache.UpgradeAvailableIcon : TextureCache.UpgradeCompleteIcon;
      if (glyph?.IsLoaded == true && !glyph.IsFailed)
        batch.Draw(glyph.Value, new Rectangle(badge.X + 3, badge.Y + 3, 18, 18), ink);

    }
    batch.End();
  }

  private void DrawJointLines(Dictionary<string, UpgradeJoint> joints, Matrix viewProjection, float timeInSeconds,
    bool dimLocked = false, RasterizerState rasterizerState = null)
  {

    m_lineRenderer.Begin(viewProjection, timeInSeconds, rasterizerState);
    foreach (var joint in joints)
    {
      if (joint.Value.State == UpgradeJoint.JointState.Hidden
        || joint.Value.StartButton.State == UpgradeButton.UnlockState.Invisible)
      {
        continue;
      }

      float buttonSizeStart = joint.Value.StartButton.Button.Visual.AbsoluteWidth;
      float buttonHalfSizeStart = buttonSizeStart / 2.0f;
      float buttonSizeEnd = joint.Value.EndButton.Button.Visual.AbsoluteWidth;
      float buttonHalfSizeEnd = buttonSizeEnd / 2.0f;

      // float progress = 0.5f; // Draw 50% of the entire joint line

      var startVisual = joint.Value.StartButton.Button.Visual;
      var endVisual = joint.Value.EndButton.Button.Visual;
      float xStart = startVisual.AbsoluteLeft + buttonHalfSizeStart + joint.Value.StartOffset.X;
      float yStart = startVisual.AbsoluteTop + startVisual.AbsoluteHeight / 2f + joint.Value.StartOffset.Y;
      float xEnd = endVisual.AbsoluteLeft + buttonHalfSizeEnd + joint.Value.EndOffset.X;
      float yEnd = endVisual.AbsoluteTop + endVisual.AbsoluteHeight / 2f + joint.Value.EndOffset.Y;
      var color = GuiSkin.MutedTextColor * (dimLocked && joint.Value.EndButton.State == UpgradeButton.UnlockState.Revealed ? 0.35f : 0.75f);
      var purchasedColor = GuiSkin.AbilityAccent * 0.8f;

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
          AudioManager.Instance.PlaySound(SoundEvent.UpgradeComplete);
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
        D(xStart, yStart, xEnd, yEnd, joint.Value, GuiSkin.Accent * 0.7f,
          GuiSkin.Accent * 0.1f, joint.Value.PurchasingTime, joint.Value.PurchasingTime);

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
    float overlayProgress = ChoosingTalents && IsOverlayVisible ? 1f : EffectiveTerminalOverlayProgress;
    BaseGame.DimmingFactor = GameMain.IsPaused ? 0.5f : DockedDimming * overlayProgress;
    BaseGame.DrawBlurFilter = (IsOverlayVisible && overlayProgress > 0.35f) || GameMain.IsPaused;


    // Gameplay controls must stay above the bar; the upgrade tree must stay below it.
    if (!drawUpgradesGui || GameMain.IsPaused)
      drawHudBackground();

    if (GameMain.IsPaused)
    {
      WithScreenCamera(() =>
      {
        if (!MenuTerminalTransition.Composite(spriteBatch.GraphicsDevice))
          SystemManagers.Default.Draw(m_gameMenuLayer);
        SystemManagers.Default.Draw(menuPopupLayer);
      });
      return;
    }

    if (!Upgrades.JsonUpgradesAsset.IsLoaded)
      return;

    if (!Upgrades.JsonUpgradeButtonsAsset.IsLoaded)
      return;

    var camera = SystemManagers.Default.Renderer.Camera;
    var m = camera.GetTransformationMatrix(true).ToXNA();

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
    var timeInSeconds = (float)BaseGame.Time.TotalGameTime.TotalSeconds;
    bool captureTerminal = drawUpgradesGui && !IsDetached && !ChoosingTalents;
    if (captureTerminal)
      BeginTerminalScreenCapture(spriteBatch.GraphicsDevice);

    if (drawUpgradesGui && m_upgradeWindowType is UpgradeTypes.Shipyard or UpgradeTypes.Signals)
    {
      if (m_upgradeWindowType == UpgradeTypes.Shipyard) DrawShipyard(spriteBatch);
      else DrawSignals(spriteBatch);
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
          if (captureTerminal)
            EndTerminalScreenCapture(spriteBatch);
          return;
        case UpgradeTypes.Upgrades:
          DrawUpgradesTree(spriteBatch, m, viewProjection, timeInSeconds);
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
      UntitledGemGameGameScreen.Instance?.DrawUpgradesExtractTooltip();

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

    DrawPanelNavigation(spriteBatch);
    if (captureTerminal)
      EndTerminalScreenCapture(spriteBatch);

    if (drawUpgradesGui)
    {
      drawHudBackground();
    }

    if (ChoosingTalents)
    {
      DrawToggleButtonApplyMeta(spriteBatch);
    }
    else if (HudLayout.BottomBarVisible && !UntitledGemGameGameScreen.Instance.m_prestiging)
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

    if (!captureTerminal && IsTerminalClosingAnimationActive)
      CompositeTerminalScreen(spriteBatch, opening: false);
  }

  // Use the same explicit canvas transform for drawing and pointer hit-testing.
  /// <summary>
  /// Draws the pause menu into the page transition's target while it changes page.
  /// Called before anything is drawn into the HUD target, which binding a target discards.
  /// </summary>
  public void PrepareMenuTransition(GraphicsDevice device)
  {
    if (GameMain.IsPaused && GameMain.CurrentMenu == "GameMenu")
      MenuTerminalTransition.Prepare(device, () => WithScreenCamera(() => SystemManagers.Default.Draw(m_gameMenuLayer)),
        keepBackdrop: false);
  }

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
    Color accent = GuiSkin.PanelAccent;
    const int height = 132;
    const float fontSize = 46f;
    float centerX = HudLayout.Width / 2f;
    var titleSize = Measure2(title, Vector2.Zero, fontSize);
    int ruleWidth = (int)Math.Min(210, HudLayout.Width * 0.08f);
    int badgeW = (int)titleSize.X + 148;
    int ruleGap = badgeW / 2 + 24;

    spriteBatch.Begin(transformMatrix: HudLayout.RenderTransform);
    GuiSkin.TopBar(spriteBatch, new Rectangle(0, 0, HudLayout.Width, height), accent);
    GuiSkin.TitleBadge(spriteBatch, new Rectangle((int)centerX - badgeW / 2, 22, badgeW, 88), accent);
    GuiSkin.TechDivider(spriteBatch, new Rectangle((int)centerX - ruleGap - ruleWidth, 64, ruleWidth, 4), accent * 0.78f);
    GuiSkin.TechDivider(spriteBatch, new Rectangle((int)centerX + ruleGap, 64, ruleWidth, 4), accent * 0.78f);
    GuiSkin.DrawHexPip(spriteBatch, new Rectangle((int)centerX - badgeW / 2 + 22, 57, 18, 18), accent, true);
    GuiSkin.DrawHexPip(spriteBatch, new Rectangle((int)centerX + badgeW / 2 - 40, 57, 18, 18), accent, true);
    spriteBatch.End();

    FontManager.RenderFieldFont(nameof(ContentDirectory.Fonts.Roboto_Regular_ttf),
      title, new Vector2(centerX - titleSize.X / 2, 69 - titleSize.Y / 2),
      accent, Color.Black, fontSize);
  }

  public void DrawToggleButtonApplyMeta(SpriteBatch m_spriteBatch)
  {
    var mousePos = GameInput.UiCursor;
    var layout = PrestigeBeginRunButton;
    bool contains = new RectangleF(layout.X, layout.Y, layout.Width, layout.Height).Contains(mousePos);
    DrawHudButton(m_spriteBatch, layout, Loc.T("Begin run"),
      GuiSkin.PanelAccent, true, contains, m_animateButtonClickUpgrades);

    var respec = PrestigeRefundAllButton;
    bool respecHovered = new RectangleF(respec.X, respec.Y, respec.Width, respec.Height).Contains(mousePos);
    bool enabled = UntitledGemGameGameScreen.Instance?.m_postPrestige == true
      && PrestigeTalentLayout.SpentPoints(UpgradeManager.CurrentUpgrades.UpgradeButtonsMeta) > 0;
    DrawHudButton(m_spriteBatch, respec, enabled ? Loc.T("Refund all") : Loc.T("No points spent"),
      GuiSkin.PanelAccent * (enabled ? 1f : 0.45f), false, enabled && respecHovered, m_animateButtonClickAbilities);
  }

  public void DrawToggleButtonUpgrades(SpriteBatch m_spriteBatch)
  {
    if (ChoosingTalents) return;

    var mousePos = new Vector2(GumService.Default.Cursor.X, GumService.Default.Cursor.Y);
    var layout = HudLayout.NavigationButton(HudLayout.UpgradesTab);
    bool contains = new RectangleF(layout.X, layout.Y, layout.Width, layout.Height).Contains(mousePos);
    DrawHudButton(m_spriteBatch, layout, m_upgradeWindowType == UpgradeTypes.Upgrades ? Loc.T("Hide") : Loc.T("Upgrades"),
      GuiSkin.PanelAccent, m_upgradeWindowType == UpgradeTypes.Upgrades, contains, m_animateButtonClickUpgrades, tab: true);
  }

  public void DrawToggleButtonAbilities(SpriteBatch m_spriteBatch)
  {
    if (ChoosingTalents || !ShipSystems.Online) return;

    var mousePos = new Vector2(GumService.Default.Cursor.X, GumService.Default.Cursor.Y);
    var layout = HudLayout.NavigationButton(HudLayout.SystemsTab);
    bool contains = new RectangleF(layout.X, layout.Y, layout.Width, layout.Height).Contains(mousePos);
    // Power cells are bought in the Systems window; a badge marks when one is affordable.
    bool selected = m_upgradeWindowType == UpgradeTypes.Abilities;
    DrawHudButton(m_spriteBatch, layout, selected ? Loc.T("Hide") : ShipSystems.NavigationLabel,
      GuiSkin.PanelAccent, selected, contains, m_animateButtonClickAbilities, tab: true, badge: !selected && CanBuyPowerCell);
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
    var mouse = GameInput.Mouse;
    bool isMouseClicked = mouse.WasButtonPressed(MouseButton.Left);
    var mousePos = GameInput.UiCursor;
    var layout = PrestigeBeginRunButton;
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

    var respec = PrestigeRefundAllButton;
    bool overRespec = new RectangleF(respec.X, respec.Y, respec.Width, respec.Height).Contains(mousePos);
    if (overRespec && isMouseClicked
      && UntitledGemGameGameScreen.Instance?.m_postPrestige == true
      && PrestigeTalentLayout.SpentPoints(UpgradeManager.CurrentUpgrades.UpgradeButtonsMeta) > 0)
    {
      UpgradeManager.Instance.RespecPrestigeTalents();
      AudioManager.Instance.PlaySound(SoundEvent.UpgradeRefund);
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
    string modalAsset = null, bool confirm = false, bool badge = false)
  {
    float pulse = clickAnimation > 0 ? MathF.Sin(Math.Clamp(clickAnimation, 0f, 1f) * MathHelper.Pi) : 0;
    spriteBatch.Begin(transformMatrix: HudLayout.RenderTransform);
    GuiSkin.HudButton(spriteBatch, bounds, accent, selected, hovered, pulse, modalAsset, confirm);
    if (badge)
    {
      // Glowing notification badge inset cleanly inside the top-right chamfer
      var badgeRect = new Rectangle(bounds.Right - 26, bounds.Y + 12, 10, 10);
      spriteBatch.Draw(AssetManager.DefaultTexture,
        new Rectangle(badgeRect.X - 2, badgeRect.Y - 2, badgeRect.Width + 4, badgeRect.Height + 4), GuiSkin.PanelColor);
      spriteBatch.Draw(AssetManager.DefaultTexture, badgeRect, GuiSkin.StatValueColor);
    }
    spriteBatch.End();

    float fontSize = modalAsset != null ? 40f : bounds.Y >= HudLayout.Top ? 31f : bounds.Height >= 100 ? 32f : 28f;
    var measure = Measure2(text, Vector2.Zero, fontSize);
    float fit = Math.Min(1f, Math.Min(Math.Max(1, bounds.Width - 32) / Math.Max(1f, measure.X),
      Math.Max(1, bounds.Height - 20) / Math.Max(1f, measure.Y)));
    fontSize *= fit;
    measure = Measure2(text, Vector2.Zero, fontSize);
    FontManager.RenderFieldFont(nameof(ContentDirectory.Fonts.Roboto_Regular_ttf), text,
      new Vector2(bounds.Center.X - measure.X / 2, bounds.Center.Y - measure.Y / 2),
      tab && hovered ? GuiSkin.HoverAccent
        : confirm ? GuiSkin.ConfirmAccent : tab && selected ? accent : GuiSkin.ButtonTextColor, Color.Black, fontSize);
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
      visual.AbsoluteWidth + frame * 2, visual.AbsoluteHeight + frame * 2);
  }

  private RectangleF GetConnectionBadgeBounds(UpgradeButton node)
  {
    if (IsUpgradeNodeLocked(node) || (!node.IsMaxLevel && !CanBuyUpgradeNode(node))) return default;
    var visual = node.Button.Visual;
    return new RectangleF(visual.AbsoluteLeft + visual.AbsoluteWidth - 12, visual.AbsoluteTop - 16, 28, 28);
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
    float buttonSize = joint.StartButton.Button.Visual.AbsoluteWidth;
    float buttonHalfSize = buttonSize / 2.0f;

    var source = joint.StartButton.Button;
    var routeOffset = new Vector2(source.Visual.AbsoluteLeft - source.X + buttonHalfSize,
      source.Visual.AbsoluteTop - source.Y + source.Visual.AbsoluteHeight / 2f);
    var path = GetJointPath(joint, new Vector2(xStart, yStart), new Vector2(xEnd, yEnd), routeOffset);
    float allowedDistance = path.TotalDistance * MathHelper.Clamp(d, 0f, 1f);
    DrawConnectionRange(path, 0, allowedDistance, 1.3f, colorCore, colorGlow);
    if (pulse >= 0 && path.TotalDistance > 0)
    {
      float head = path.TotalDistance * MathHelper.Clamp(pulse, 0f, 1f);
      // A short luminous tail and a crisp white tip, measured along the route.
      DrawConnectionRange(path, Math.Max(0, head - 12f), head, 1.5f,
        GuiSkin.Accent, GuiSkin.Accent * 0.28f, joined: false);
      DrawConnectionRange(path, Math.Max(0, head - 3f), head, 2f,
        Color.White, GuiSkin.Accent * 0.6f, joined: false);
    }
  }

  private void DrawConnectionRange(JointPath path, float from, float to, float thickness, Color core, Color glow, bool joined = true)
  {
    bool clipToViewport = m_upgradeWindowType == UpgradeTypes.Upgrades;
    RectangleF worldClip = default;
    if (clipToViewport)
    {
      var camera = SystemManagers.Default.Renderer.Camera;
      var view = UpgradesTreeViewport;
      camera.ScreenToWorld(view.Left, view.Top, out float minX, out float minY);
      camera.ScreenToWorld(view.Right, view.Bottom, out float maxX, out float maxY);
      worldClip = new RectangleF(minX, minY, maxX - minX, maxY - minY);
    }

    float distance = 0;
    for (int i = 0; i < path.Lengths.Length; i++)
    {
      float length = path.Lengths[i];
      float end = distance + length;
      if (length > 0.0001f && end > from && distance < to)
      {
        var startPoint = Vector2.Lerp(path.Points[i], path.Points[i + 1], Math.Clamp((from - distance) / length, 0f, 1f));
        var endPoint = Vector2.Lerp(path.Points[i], path.Points[i + 1], Math.Clamp((to - distance) / length, 0f, 1f));
        bool clippedStart = false, clippedEnd = false;
        if (clipToViewport && !ClipSegmentToRect(ref startPoint, ref endPoint, worldClip, out clippedStart, out clippedEnd))
        {
          distance = end;
          if (distance >= to) break;
          continue;
        }
        float halfWidth = thickness + m_lineRenderer.BaseGlowPadding;
        var normal = Vector2.Normalize(path.Points[i + 1] - path.Points[i]);
        normal = new Vector2(-normal.Y, normal.X) * halfWidth;
        var startJoin = !clippedStart && from <= distance ? ConnectionJoin(path, i, halfWidth) : normal;
        var endJoin = !clippedEnd && to >= end ? ConnectionJoin(path, i + 1, halfWidth) : normal;
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

  private static bool ClipSegmentToRect(ref Vector2 p0, ref Vector2 p1, RectangleF rect,
    out bool clippedStart, out bool clippedEnd)
  {
    clippedStart = false;
    clippedEnd = false;
    float t0 = 0f, t1 = 1f;
    float dx = p1.X - p0.X;
    float dy = p1.Y - p0.Y;
    float minX = rect.X, maxX = rect.X + rect.Width;
    float minY = rect.Y, maxY = rect.Y + rect.Height;

    static bool ClipEdge(float p, float q, ref float t0, ref float t1)
    {
      if (MathF.Abs(p) < 1e-6f) return q >= 0f;
      float r = q / p;
      if (p < 0f)
      {
        if (r > t1) return false;
        if (r > t0) t0 = r;
      }
      else
      {
        if (r < t0) return false;
        if (r < t1) t1 = r;
      }
      return true;
    }

    if (!ClipEdge(-dx, p0.X - minX, ref t0, ref t1)
      || !ClipEdge(dx, maxX - p0.X, ref t0, ref t1)
      || !ClipEdge(-dy, p0.Y - minY, ref t0, ref t1)
      || !ClipEdge(dy, maxY - p0.Y, ref t0, ref t1)
      || t1 - t0 <= 1e-5f)
      return false;

    var orig0 = p0;
    var orig1 = p1;
    if (t0 > 0f)
    {
      p0 = Vector2.Lerp(orig0, orig1, t0);
      clippedStart = true;
    }
    if (t1 < 1f)
    {
      p1 = Vector2.Lerp(orig0, orig1, t1);
      clippedEnd = true;
    }
    return true;
  }

  private static Vector2 ConnectionJoin(JointPath path, int index, float halfWidth)
    => UpgradeConnectionGeometry.JoinOffset(path.Points, index, halfWidth);

}
