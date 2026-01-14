using AsyncContent;
using Bloom_Sample;
using BracketHouse.FontExtension;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.Input;
using MonoGame.Extended.Screens;
using Serilog;
using Serilog.Sinks.Console.LogThemes;
using BloomPostprocess;
using Color = Microsoft.Xna.Framework.Color;
using System.Runtime.InteropServices;
using JapeFramework.ImGUI;
using MonoGame.Extended.ViewportAdapters;
using MonoGame.Extended;
using RenderingLibrary;

// https://badecho.com/index.php/2023/09/29/msdf-fonts-2/
//https://github.com/craftworkgames/MonoGame.Squid
//https://github.com/rive-app/rive-sharp
//https://docs.flatredball.com/gum/code/monogame
//Monogame extended uses GUM gui

namespace JapeFramework
{
  public class BaseGame : Game
  {
    protected GraphicsDeviceManager _graphics;
    protected SpriteBatch _spriteBatch;
    protected readonly ScreenManager _screenManager;
    protected bool showLoadingScreen = false;
    private ImGuiRenderer _imGuiRenderer;

    //public static GraphicsDevice Graphics;

    public static RenderTarget2D renderTarget1, renderTarget2;

    public static RenderTarget2D _renderTargetImgui;
    public static RenderTarget2D _renderTargetHud;

    // private BloomFilter _bloomFilter;
    private Bloom bloom;

    private int VirtualWidth = 1280;
    private int VirtualHeight = 720;


    private int VirtualWidthGui = 1280;
    private int VirtualHeightGui = 720;

    public static int HudScaler = 1;

    private Matrix _scaleMatrix; // Scaling matrix for the SpriteBatch
                                 //
    private Rectangle _finalDestinationRectangle;

    // The delay time (e.g., 200ms is usually enough)
    private const float ResizeDelaySeconds = 0.2f;

    // Flag to track if the graphics settings have been updated 
    private bool _resizeNeedsApplying = false;

    // The time we last received a resize event
    private float _lastResizeTime = 0f;

    protected bool UseLoadingscreen = true;

    public static BoxingViewportAdapter BoxingViewportAdapter;
    public static BoxingViewportAdapter BoxingViewportAdapterGui;
    private Viewport m_fullWindowViewport;

    private OrthographicCamera Camera;
    private OrthographicCamera HudCamera;

    private BlurFilter m_blurFilter;

    public static bool DrawBlurFilter = false;
    public static float DimmingFactor = 0.0f;

    public BaseGame(string gameName, int bufferWidht = 1920, int bufferHeight = 1080, float targetFps = 60.0f, bool fixedTimeStep = true, bool fullscreen = false)
    {
      SetupLogger(gameName);

      VirtualWidth = bufferWidht;
      VirtualHeight = bufferHeight;

      VirtualWidthGui = bufferWidht * HudScaler;
      VirtualHeightGui = bufferHeight * HudScaler;
      // VirtualWidthGui = bufferWidht;
      // VirtualHeightGui = bufferHeight;
      if (fullscreen)
      {
        DisplayMode displayMode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
        bufferWidht = displayMode.Width;
        bufferHeight = displayMode.Height;
      }

      _graphics = new GraphicsDeviceManager(this)
      {
        PreferredBackBufferWidth = bufferWidht,
        PreferredBackBufferHeight = bufferHeight,
        SynchronizeWithVerticalRetrace = false,
        IsFullScreen = fullscreen,
        GraphicsProfile = GraphicsProfile.HiDef,
        PreferredBackBufferFormat = SurfaceFormat
      };

      m_fullWindowViewport = new Viewport(0, 0, bufferWidht, bufferHeight);

      Window.AllowUserResizing = true;

      Content.RootDirectory = "Content";

      IsFixedTimeStep = fixedTimeStep;
      TargetElapsedTime = TimeSpan.FromSeconds(1f / targetFps);

      _screenManager = new ScreenManager();
      Components.Add(_screenManager);
    }

    private bool _isResizing = false;
    private bool _resizePending = false; // The flag to prevent re-entrancy

    // This event handler still only sets the flag and records the time.
    private void Window_ClientSizeChanged(object sender, EventArgs e)
    {
      if (Time == null)
      {
        _resizeNeedsApplying = true;
        return;
      }

      _resizeNeedsApplying = true;
      _lastResizeTime = (float)Time.TotalGameTime.TotalSeconds;

      // IMPORTANT: We update the target size here so it is always 
      // synchronized with the current window bounds.
      // _graphics.PreferredBackBufferWidth = Window.ClientBounds.Width;
      // _graphics.PreferredBackBufferHeight = Window.ClientBounds.Height;
    }

    protected void RefreshedSize()
    {
      Console.WriteLine("Applying delayed resize changes...");

      int rtWidth = _graphics.GraphicsDevice.PresentationParameters.BackBufferWidth;
      int rtHeight = _graphics.GraphicsDevice.PresentationParameters.BackBufferHeight;

      m_fullWindowViewport = new Viewport(0, 0, rtWidth, rtHeight);

      _renderTargetImgui = new RenderTarget2D(GraphicsDevice, rtWidth, rtHeight, true, SurfaceFormat, DepthFormat);
    }

    private void SetupLogger(string gameName)
    {
      var appdata = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
      var rollingFolder = $"{appdata}/{gameName}/Rolling/";

      if (!Directory.Exists(rollingFolder))
        Directory.CreateDirectory(rollingFolder);

      Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Verbose()
        .Enrich.FromLogContext()
        .WriteTo.Console(theme: LogThemes.UseAnsiTheme<JFLoggerTheme>())
        .WriteTo.Debug()
        .WriteTo.File($"{rollingFolder}/rolling_log.txt", rollingInterval: RollingInterval.Day)
        .CreateLogger();

      Log.Information("Logger initialized");
      Log.Information($"------- Launching game: {gameName} -------");
    }

    public static SurfaceFormat SurfaceFormat = SurfaceFormat.Color;
    public static DepthFormat DepthFormat = DepthFormat.None;

    protected override void Initialize()
    {
      AssetManager.Initialize(Content, GraphicsDevice);
      TextRenderer.Initialize(_graphics, Window, Content);

      // bool isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
      // bool isMac = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
      // bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
      // bool isArm = RuntimeInformation.OSArchitecture == Architecture.Arm64;

      // bool supportImGui = !(isLinux && isArm);

      BoxingViewportAdapter = new BoxingViewportAdapter(
        Window,
        GraphicsDevice,
        VirtualWidth,
        VirtualHeight
      );

      BoxingViewportAdapterGui = new BoxingViewportAdapter(
        Window,
        GraphicsDevice,
        VirtualWidthGui,
        VirtualHeightGui
      );

      Camera = new OrthographicCamera(BoxingViewportAdapter);
      HudCamera = new OrthographicCamera(BoxingViewportAdapterGui);

      // HudCamera.Zoom = 2.0f;

      // if (supportImGui)
      {
        _imGuiRenderer = new ImGuiRenderer(this);
        _imGuiRenderer.RebuildFontAtlas();
      }

      var rtWidth = VirtualWidth;
      var rtHeight = VirtualHeight;

      // _renderTarget = new RenderTarget2D(GraphicsDevice, _graphics.PreferredBackBufferWidth, _graphics.PreferredBackBufferHeight, true, SurfaceFormat, DepthFormat);
      _renderTargetImgui = new RenderTarget2D(GraphicsDevice, rtWidth, rtHeight, true, SurfaceFormat, DepthFormat);
      _renderTargetHud = new RenderTarget2D(GraphicsDevice, VirtualWidthGui, VirtualHeightGui, true, SurfaceFormat, DepthFormat);

      renderTarget1 = new RenderTarget2D(GraphicsDevice, rtWidth, rtHeight, true, SurfaceFormat, DepthFormat);
      renderTarget2 = new RenderTarget2D(GraphicsDevice, rtWidth, rtHeight, true, SurfaceFormat, DepthFormat);

      //https://www.alienscribbleinteractive.com/Tutorials/bloom_tutorial.html
      // _bloomFilter = new BloomFilter();
      // _bloomFilter.Load(GraphicsDevice, Content, rtWidth, rtHeight, SurfaceFormat);
      // _bloomFilter.BloomPreset = BloomFilter.BloomPresets.Focussed;
      // _bloomFilter.BloomUseLuminance = true;
      // _bloomFilter.BloomStreakLength = 3;
      // _bloomFilter.BloomThreshold = 0.6f;

      // GoToFullscreen();

      Window.ClientSizeChanged += Window_ClientSizeChanged;

      base.Initialize();
    }

    protected override void LoadContent()
    {
      _spriteBatch = new SpriteBatch(GraphicsDevice);

      FontManager.InitFontManager(GraphicsDevice);

      AssetManager.FakeMinimumLoadingTime(1500);

      showLoadingScreen = true;
      //_screenManager.LoadScreen(new MainMenu(this));

      AssetManager.BatchLoaded += () =>
      {
        showLoadingScreen = false;
      };

      LoadInitialScreen(_screenManager);

      var pp = GraphicsDevice.PresentationParameters;
      bloom = new Bloom(GraphicsDevice, _spriteBatch);
      bloom.LoadContent(Content, pp);

      m_blurFilter = new BlurFilter();
      m_blurFilter.LoadContent();
      //_screenManager.LoadScreen(new MainMenu(this), new FadeTransition(GraphicsDevice, Color.Black, 1.5f));
    }

    protected virtual void LoadInitialScreen(ScreenManager screenManager)
    {

    }

    public static float ZoomFactor = 1.0f;

    public static GameTime Time;
    protected override void Update(GameTime gameTime)
    {
      float currentTime = (float)gameTime.TotalGameTime.TotalSeconds;

      if (_resizeNeedsApplying && (currentTime - _lastResizeTime > ResizeDelaySeconds))
      {
        RefreshedSize();
        _resizeNeedsApplying = false;
      }

      // 1. Get current screen dimensions
      int screenWidth = GraphicsDevice.Viewport.Width;
      int screenHeight = GraphicsDevice.Viewport.Height;

      // 2. Define your fixed design height
      const float DesignHeight = 1080f;

      // 3. Calculate the zoom
      ZoomFactor = DesignHeight / screenHeight;

      KeyboardExtended.Update();
      MouseExtended.Update();

      Time = gameTime;

      base.Update(gameTime);
    }

    private void DrawText(SpriteBatch spriteBatch, string text)
    {
      var font = FontManager.GetDefaultFont(150);
      var text_size = font.MeasureString(text);
      var pos_x = GraphicsDevice.Viewport.Width / 2.0f - text_size.X / 2.0f;
      var pos_y = GraphicsDevice.Viewport.Height / 2.0f - text_size.Y / 2.0f;
      spriteBatch.DrawString(font, text, new Vector2(pos_x, pos_y), Color.Yellow);
    }

    protected override void Draw(GameTime gameTime)
    {
      GraphicsDevice.Clear(Color.Black);

      if (_isResizing)
        return;

      if (_resizePending)
        return;

      if (_spriteBatch == null)
        return;

      if (UseLoadingscreen && showLoadingScreen && AssetManager.IsLoadingContent())
      {
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        DrawText(_spriteBatch, "Loading...");
        _spriteBatch.End();
        return;
      }

      var viewMatrix = Camera.GetViewMatrix();
      var viewMatrixHud = HudCamera.GetViewMatrix();
      // var viewport = GraphicsDevice.Viewport;

      //Render to HUD and ImGui to their own render targets
      DrawHud(gameTime);
      DrawImGui(gameTime);

      GraphicsDevice.SetRenderTarget(renderTarget1);
      GraphicsDevice.Clear(Color.Black);

      base.Draw(gameTime);

      bloom.Draw(renderTarget1, renderTarget2);
      bloom.Settings = BloomSettings.PresetSettings[0];

      GraphicsDevice.SetRenderTarget(null);

      BoxingViewportAdapter.Reset();
      GraphicsDevice.Viewport = BoxingViewportAdapter.Viewport;

      _spriteBatch.Begin(0, BlendState.AlphaBlend, samplerState: SamplerState.PointClamp, transformMatrix: viewMatrix);
      _spriteBatch.Draw(renderTarget2, Vector2.Zero, Color.White);
      _spriteBatch.End();

      if (DrawBlurFilter)
      {
        var width = GraphicsDevice.Viewport.Width;
        var height = GraphicsDevice.Viewport.Height;
        m_blurFilter.Draw(_spriteBatch, renderTarget2, Camera, width, height);
      }

      if (DimmingFactor > 0.0f && DimmingFactor <= 1.0f)
      {
        _spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointClamp);
        _spriteBatch.Draw(
            AssetManager.DefaultTexture,
            new Rectangle(0, 0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height),
            Color.Black * DimmingFactor);
        _spriteBatch.End();
      }

      _spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.AnisotropicClamp, transformMatrix: viewMatrix);
      // _spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.AnisotropicClamp, transformMatrix: viewMatrixHud);
      // _spriteBatch.Draw(_renderTargetHud, BoxingViewportAdapter.Viewport.Bounds, Color.White);
      // _spriteBatch.Draw(AssetManager.DefaultTexture, BoxingViewportAdapter.Viewport.Bounds, Color.Red);
      // _spriteBatch.Draw(AssetManager.DefaultTexture, new Rectangle(0, 0, VirtualWidth, VirtualHeight), Color.Red);
      _spriteBatch.Draw(_renderTargetHud, new Rectangle(0, 0, VirtualWidthGui, VirtualHeightGui), Color.White);
      _spriteBatch.End();

      // Console.WriteLine(BoxingViewportAdapter.Viewport.Bounds);
      // GraphicsDevice.Viewport = viewport;

      if (ShouldDrawImGui)
      {
        var viewport = GraphicsDevice.Viewport;
        GraphicsDevice.Viewport = m_fullWindowViewport;
        _spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointClamp);
        _spriteBatch.Draw(_renderTargetImgui, Vector2.Zero, Color.White);

        _spriteBatch.End();
        // GraphicsDevice.Viewport = BoxingViewportAdapter.Viewport;
        GraphicsDevice.Viewport = viewport;
      }

      DrawLoadingAssets();
    }

    public bool ShouldDrawImGui => DrawImGuiEnabled && IsImGuiSPlatformSupported;
    public virtual bool DrawImGuiEnabled => true;
    public virtual bool IsImGuiSPlatformSupported => true;

    public void DrawImGui(GameTime gameTime)
    {
      if (ShouldDrawImGui)
      {
        _graphics.GraphicsDevice.SetRenderTarget(_renderTargetImgui);
        GraphicsDevice.Clear(Color.Transparent);
        DrawCustomImGuiContent(_imGuiRenderer, gameTime);
      }
    }

    public void DrawHud(GameTime gameTime)
    {
      _graphics.GraphicsDevice.SetRenderTarget(_renderTargetHud);
      GraphicsDevice.Clear(Color.Transparent);
      DrawHudLayer();
    }

    public static Vector2 ViewportMin => new Vector2(BoxingViewportAdapter.Viewport.X, BoxingViewportAdapter.Viewport.Y);
    public static Vector2 ViewportMax => new Vector2(BoxingViewportAdapter.Viewport.X + BoxingViewportAdapter.Viewport.Width, BoxingViewportAdapter.Viewport.Y + BoxingViewportAdapter.Viewport.Height);
    public static Vector2 ViewportCenter => new Vector2(BoxingViewportAdapter.Viewport.X + BoxingViewportAdapter.Viewport.Width / 2, BoxingViewportAdapter.Viewport.Y + BoxingViewportAdapter.Viewport.Height / 2);

    public virtual void DrawCustomImGuiContent(ImGuiRenderer _imGuiRenderer, GameTime gameTime)
    {

    }

    public virtual void DrawHudLayer()
    {

    }

    private void DrawLoadingAssets()
    {
      if (!showLoadingScreen && AssetManager.IsLoadingContent())
      {
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);

        var font = FontManager.GetDefaultFont(30);

        var text = "Loading Additional Assets...";
        var text_size = font.MeasureString(text);
        var pos_x = 0;
        var pos_y = GraphicsDevice.Viewport.Height - text_size.Y;

        _spriteBatch.DrawString(font, text, new Vector2(pos_x, pos_y), Color.Yellow);

        _spriteBatch.End();
      }
    }
  }
}
