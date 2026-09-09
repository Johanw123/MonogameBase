using System;
using System.IO;
using System.Diagnostics;
using Microsoft.Xna.Framework.Input;
using System.Linq.Expressions;
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

using JapeFramework.ImGUI;
using MonoGame.Extended.ViewportAdapters;
using MonoGame.Extended;
using RenderingLibrary;
using JapeFramework.Helpers;

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

    public static RenderTarget2D? renderTarget1, renderTarget2;

    public static RenderTarget2D? _renderTargetImgui;
    public static RenderTarget2D? _renderTargetHud;

    // Allow games with a supersampled HUD to filter thin details during downscaling.
    protected virtual bool UseHudMipMaps => false;

    // private BloomFilter _bloomFilter;
    private Bloom? bloom = null;

    private int VirtualWidth = 1280;
    private int VirtualHeight = 720;

    private int VirtualWidthGui = 1280;
    private int VirtualHeightGui = 720;

    public static int HudScaler = 1;


    // The delay time (e.g., 200ms is usually enough)
    private const float ResizeDelaySeconds = 0.2f;

    // Flag to track if the graphics settings have been updated 
    private bool _resizeNeedsApplying = false;

    // The time we last received a resize event
    private float _lastResizeTime = 0f;

    protected bool UseLoadingscreen = true;
    protected bool m_draw_framerate = true;


    public static BoxingViewportAdapter? BoxingViewportAdapter;
    public static BoxingViewportAdapter? BoxingViewportAdapterGui;
    private Viewport m_fullWindowViewport;

    private OrthographicCamera Camera;
    private OrthographicCamera HudCamera;

    private BlurFilter m_blurFilter;
    //private FastBlurFilter m_fastBlurFilter;

    public static FrameCounter m_frameCounter;
    private long _lastFrameTimestamp;
    private double _updateMilliseconds;
    private int _performanceView = 1; // 0 hidden, 1 compact, 2 detailed
    private readonly int[] _gcBaseline = new int[3];
    private readonly string[] _performanceValues = new string[14];
    private static readonly string[] PerformanceLabels =
    {
      "95th percentile", "99th percentile", "Worst frame", "Over 16.7 ms",
      "CPU update", "CPU draw", "Draw calls", "Primitives",
      "Textures", "Managed heap", "GC 0 / 1 / 2", "History"
    };
    private string _performanceStatus = "";
    private long _nextPerformanceText;
    private KeyboardState _performanceKeys;


    public static bool DrawBlurFilter = false;
    public static float DimmingFactor = 0.0f;

    // https://community.monogame.net/t/solved-right-way-to-use-matrices-to-scale-a-gui-across-different-display-configs/10590/7
    private float _renderScale = 0.5f;
    private const int _renderScreenHeight = 1080;

    public float AspectRatio => (float)GraphicsDevice.PresentationParameters.BackBufferWidth / GraphicsDevice.PresentationParameters.BackBufferHeight;

    public Vector2 GetScaledResolution()
    {
      var scaledHeight = (float)_renderScreenHeight / _renderScale;
      return new Vector2(AspectRatio * scaledHeight, scaledHeight);
    }

    public BaseGame()
    {
      _screenManager = new ScreenManager();
      Components.Add(_screenManager);
    }

    public BaseGame(string gameName, int bufferWidht = 1920, int bufferHeight = 1080, float targetFps = 60.0f, bool fixedTimeStep = true, bool fullscreen = false)
    {
      _screenManager = new ScreenManager();
      Components.Add(_screenManager);
      Init(gameName, bufferWidht, bufferHeight, targetFps, fixedTimeStep, fullscreen);
    }

    public void Init(string gameName, int bufferWidht = 1920, int bufferHeight = 1080, float targetFps = 60.0f, bool fixedTimeStep = true, bool fullscreen = false, bool borderlessIfFullscreen = true)
    {
      SetupLogger(gameName);

      //VirtualWidth = bufferWidht;
      //VirtualHeight = bufferHeight;

      //VirtualWidthGui = bufferWidht * HudScaler;
      //VirtualHeightGui = bufferHeight * HudScaler;

      VirtualWidth = 3840;
      VirtualHeight = 2160;

      // VirtualWidthGui = 3840;
      // VirtualHeightGui = 2160;

      // VirtualWidth = 2880;
      // VirtualHeight = 1620;


      VirtualWidthGui = VirtualWidth;
      VirtualHeightGui = VirtualHeight;


      // if (fullscreen)
      // {
      //   DisplayMode displayMode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
      //   bufferWidht = displayMode.Width;
      //   bufferHeight = displayMode.Height;
      // }

      _graphics = new GraphicsDeviceManager(this)
      {
        PreferredBackBufferWidth = bufferWidht,
        PreferredBackBufferHeight = bufferHeight,
        SynchronizeWithVerticalRetrace = false,
        IsFullScreen = fullscreen,
        HardwareModeSwitch = !borderlessIfFullscreen,
        GraphicsProfile = GraphicsProfile.HiDef,
        PreferredBackBufferFormat = SurfaceFormat
      };

      Log.Information($"Setting back buffer size: {bufferWidht}x{bufferHeight}");

      m_fullWindowViewport = new Viewport(0, 0, bufferWidht, bufferHeight);

      Window.AllowUserResizing = true;
      Content.RootDirectory = "Content";

      m_frameCounter = new FrameCounter();
      for (int i = 0; i < 3; ++i) _gcBaseline[i] = GC.CollectionCount(i);

      IsFixedTimeStep = fixedTimeStep;
      // _graphics.SynchronizeWithVerticalRetrace = fixedTimeStep;
      TargetElapsedTime = TimeSpan.FromSeconds(1f / targetFps);
    }

    public void SetVirtualResolutionGui(int width, int height)
    {
      VirtualWidthGui = width;
      VirtualHeightGui = height;

      BoxingViewportAdapterGui = new BoxingViewportAdapter(
        Window,
        GraphicsDevice,
        VirtualWidthGui,
        VirtualHeightGui
      );

      _renderTargetHud?.Dispose();
      _renderTargetHud = new RenderTarget2D(GraphicsDevice, VirtualWidthGui, VirtualHeightGui, UseHudMipMaps, SurfaceFormat, DepthFormat);

      SetVirtualResolution(width, height);
      // HudCamera = new OrthographicCamera(BoxingViewportAdapterGui);
    }

    public void SetVirtualResolution(int width, int height)
    {
      VirtualWidth = width;
      VirtualHeight = height;

      BoxingViewportAdapter = new BoxingViewportAdapter(
        Window,
        GraphicsDevice,
        VirtualWidth,
        VirtualHeight
      );

      _renderTargetImgui?.Dispose();
      _renderTargetImgui = new RenderTarget2D(GraphicsDevice, VirtualWidth, VirtualHeight, false, SurfaceFormat, DepthFormat);

      renderTarget1?.Dispose();
      renderTarget2?.Dispose();

      renderTarget1 = new RenderTarget2D(GraphicsDevice, VirtualWidth, VirtualHeight, false, SurfaceFormat, DepthFormat);
      renderTarget2 = new RenderTarget2D(GraphicsDevice, VirtualWidth, VirtualHeight, false, SurfaceFormat, DepthFormat);

      // Camera = new OrthographicCamera(BoxingViewportAdapter);
    }

    private bool _isResizing = false;
    private bool _resizePending = false; // The flag to prevent re-entrancy

    // This event handler still only sets the flag and records the time.
    private void Window_ClientSizeChanged(object sender, EventArgs e)
    {
      // Console.WriteLine("Window resize event detected. Marking resize as pending: {}" + Window.ClientBounds);

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

      _renderTargetImgui = new RenderTarget2D(GraphicsDevice, rtWidth, rtHeight, false, SurfaceFormat, DepthFormat);

      // _graphics.ApplyChanges();
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
      FontStashSharpText.Initialize(_graphics.GraphicsDevice);

      // bool isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
      // bool isMac = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
      // bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
      // bool isArm = RuntimeInformation.OSArchitecture == Architecture.Arm64;

      // bool supportImGui = !(isLinux && isArm);


      // VirtualWidth = (int)GetScaledResolution().X;
      // VirtualHeight = (int)GetScaledResolution().Y;
      //
      // VirtualWidthGui = (int)GetScaledResolution().X;
      // VirtualHeightGui = (int)GetScaledResolution().Y;


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
#if !KNI_WEB
        _imGuiRenderer = new ImGuiRenderer(this);
        _imGuiRenderer.RebuildFontAtlas();
#endif
      }

      var rtWidth = VirtualWidth;
      var rtHeight = VirtualHeight;

      // _renderTarget = new RenderTarget2D(GraphicsDevice, _graphics.PreferredBackBufferWidth, _graphics.PreferredBackBufferHeight, true, SurfaceFormat, DepthFormat);
      _renderTargetImgui = new RenderTarget2D(GraphicsDevice, rtWidth, rtHeight, false, SurfaceFormat, DepthFormat);
      _renderTargetHud = new RenderTarget2D(GraphicsDevice, VirtualWidthGui, VirtualHeightGui, UseHudMipMaps, SurfaceFormat, DepthFormat);

      renderTarget1 = new RenderTarget2D(GraphicsDevice, rtWidth, rtHeight, false, SurfaceFormat, DepthFormat);
      renderTarget2 = new RenderTarget2D(GraphicsDevice, rtWidth, rtHeight, false, SurfaceFormat, DepthFormat);

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

#if !KNI_WEB
      FontManager.InitFontManager(GraphicsDevice);
#endif

      AssetManager.FakeMinimumLoadingTime(1500);

      showLoadingScreen = true;
      //_screenManager.LoadScreen(new MainMenu(this));

      AssetManager.BatchLoaded += () =>
      {
        showLoadingScreen = false;
      };

      LoadInitialScreen(_screenManager);

#if !KNI_WEB
      var pp = GraphicsDevice.PresentationParameters;
      bloom = new Bloom(GraphicsDevice, _spriteBatch);
      bloom.LoadContent(Content, pp);
#endif

      // var fx = AssetManager.LoadAsync<Effect>("JFContent/Shaders/Slug/SlugShader.fx", true);

      m_blurFilter = new BlurFilter();
      m_blurFilter.LoadContent();

      //var effect = AssetManager.LoadAsync<Effect>("JFContent/Shaders/FastBlur.fx", true);
      //m_fastBlurFilter = new FastBlurFilter(GraphicsDevice, effect);
      //_screenManager.LoadScreen(new MainMenu(this), new FadeTransition(GraphicsDevice, Color.Black, 1.5f));
    }

    protected virtual void LoadInitialScreen(ScreenManager screenManager)
    {

    }

    public static GameTime Time;
    protected override void Update(GameTime gameTime)
    {
      long updateStart = Stopwatch.GetTimestamp();
      var keys = Keyboard.GetState();
      if (keys.IsKeyDown(Keys.F) && !_performanceKeys.IsKeyDown(Keys.F))
      {
        _performanceView = (_performanceView + 1) % 3;
        _nextPerformanceText = 0;
      }
      if (keys.IsKeyDown(Keys.R) && !_performanceKeys.IsKeyDown(Keys.R))
      {
        m_frameCounter.Reset();
        _lastFrameTimestamp = _nextPerformanceText = 0;
        for (int i = 0; i < 3; ++i) _gcBaseline[i] = GC.CollectionCount(i);
      }
      _performanceKeys = keys;
      float currentTime = (float)gameTime.TotalGameTime.TotalSeconds;

      if (_resizeNeedsApplying && (currentTime - _lastResizeTime > ResizeDelaySeconds))
      {
        RefreshedSize();
        _resizeNeedsApplying = false;
      }

      KeyboardExtended.Update();
      MouseExtended.Update();

      Time = gameTime;
      base.Update(gameTime);
      _updateMilliseconds += (Stopwatch.GetTimestamp() - updateStart) * 1000.0 / Stopwatch.Frequency;

    }

    private void DrawTextCenter(SpriteBatch spriteBatch, string text)
    {
#if !KNI_WEB
      var font = FontManager.GetDefaultFont(150);
      var text_size = font.MeasureString(text);
      var pos_x = GraphicsDevice.Viewport.Width / 2.0f - text_size.X / 2.0f;
      var pos_y = GraphicsDevice.Viewport.Height / 2.0f - text_size.Y / 2.0f;
      spriteBatch.DrawString(font, text, new Vector2(pos_x, pos_y), Color.Yellow);
#endif
    }

    protected override void Draw(GameTime gameTime)
    {
      long drawStart = Stopwatch.GetTimestamp();
      // Real draw-to-draw intervals include presentation and frame limiting.
      // Do not record time spent unfocused, loading, or resizing as gameplay spikes.
      if (IsActive && !_isResizing && !_resizePending && !showLoadingScreen)
      {
        if (_lastFrameTimestamp != 0)
          m_frameCounter.Update((float)((drawStart - _lastFrameTimestamp) / (double)Stopwatch.Frequency));
        _lastFrameTimestamp = drawStart;
      }
      else _lastFrameTimestamp = 0;
      double updateMilliseconds = _updateMilliseconds;
      _updateMilliseconds = 0;
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
        DrawTextCenter(_spriteBatch, "Loading...");
        _spriteBatch.End();
        return;
      }

      var viewMatrix = Camera.GetViewMatrix();
      var viewMatrixHud = HudCamera.GetViewMatrix();
      // var viewport = GraphicsDevice.Viewport;

      GraphicsDevice.Viewport = new Viewport(0, 0, VirtualWidth, VirtualHeight);

      //Render to HUD and ImGui to their own render targets
      DrawHud(gameTime);
      DrawImGui(gameTime);

      GraphicsDevice.SetRenderTarget(renderTarget1);
      GraphicsDevice.Clear(Color.Black);

      base.Draw(gameTime);

      if (bloom != null)
      {
        bloom.Draw(renderTarget1, renderTarget2);
        bloom.Settings = BloomSettings.PresetSettings[0];
      }

      GraphicsDevice.SetRenderTarget(null);

      BoxingViewportAdapter.Reset();
      GraphicsDevice.Viewport = BoxingViewportAdapter.Viewport;

      if (bloom != null)
      {
        _spriteBatch.Begin(0, BlendState.AlphaBlend, samplerState: SamplerState.LinearClamp,
          transformMatrix: viewMatrix);
        _spriteBatch.Draw(renderTarget2, Vector2.Zero, Color.White);
        _spriteBatch.End();
      }
      else
      {
        _spriteBatch.Begin(0, BlendState.AlphaBlend, samplerState: SamplerState.LinearClamp,
          transformMatrix: viewMatrix);
        _spriteBatch.Draw(renderTarget1, Vector2.Zero, Color.White);
        _spriteBatch.End();
      }

#if !KNI_WEB
      if (DrawBlurFilter)
      {
        var width = GraphicsDevice.Viewport.Width;
        var height = GraphicsDevice.Viewport.Height;
        m_blurFilter.Draw(_spriteBatch, renderTarget2, Camera, width, height);
      }
      //if (DrawBlurFilter)
      //{
      //  m_fastBlurFilter.Draw(_spriteBatch, renderTarget2, viewMatrix);
      //}
#endif

      if (DimmingFactor is > 0.0f and <= 1.0f)
      {
        _spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.LinearClamp);
        _spriteBatch.Draw(
            AssetManager.DefaultTexture,
            new Rectangle(0, 0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height),
            Color.Black * DimmingFactor);
        _spriteBatch.End();
      }

      // _spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.AnisotropicClamp, transformMatrix: viewMatrix);
      _spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.LinearClamp, transformMatrix: viewMatrixHud);
      // _spriteBatch.Draw(_renderTargetHud, BoxingViewportAdapter.Viewport.Bounds, Color.White);
      // _spriteBatch.Draw(AssetManager.DefaultTexture, BoxingViewportAdapter.Viewport.Bounds, Color.Red);
      // _spriteBatch.Draw(AssetManager.DefaultTexture, new Rectangle(0, 0, VirtualWidthGui, VirtualHeightGui), Color.Red * 0.3f);
      _spriteBatch.Draw(_renderTargetHud, new Rectangle(0, 0, VirtualWidthGui, VirtualHeightGui), Color.White);
      _spriteBatch.End();

      // Console.WriteLine(BoxingViewportAdapter.Viewport.Bounds);
      // GraphicsDevice.Viewport = viewport;

      if (ShouldDrawImGui)
      {
        var viewport = GraphicsDevice.Viewport;
        GraphicsDevice.Viewport = m_fullWindowViewport;
        _spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.LinearClamp);
        _spriteBatch.Draw(_renderTargetImgui, Vector2.Zero, Color.White);
        _spriteBatch.End();
        // GraphicsDevice.Viewport = BoxingViewportAdapter.Viewport;
        GraphicsDevice.Viewport = viewport;
      }

      DrawLoadingAssets();
      DrawFramerate(drawStart, updateMilliseconds);
    }

#if KNI_WEB
    public bool ShouldDrawImGui => false;
#else
    public bool ShouldDrawImGui => DrawImGuiEnabled && IsImGuiSPlatformSupported;
#endif
    public virtual bool DrawImGuiEnabled => true;
    public virtual bool ShouldDrawFramerateCounter => true;
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
#if !KNI_WEB
      if (!showLoadingScreen && AssetManager.IsLoadingContent())
      {

        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);

        var font = FontManager.GetDefaultFont(30);

        string s = AssetManager.GetTaskName();

        var text = "Loading Additional Assets...";
        if (!string.IsNullOrEmpty(s))
        {
          text += " " + s;
        }

        var failed = AssetManager.TaskFailed();
        if (!string.IsNullOrEmpty(failed))
        {
          text += " One task failed" + failed;
        }

        var text_size = font.MeasureString(text);
        var pos_x = 0;
        var pos_y = GraphicsDevice.Viewport.Height - text_size.Y;

        _spriteBatch.DrawString(font, text, new Vector2(pos_x, pos_y), Color.Yellow);


        _spriteBatch.End();

      }
#endif
    }

    private void DrawFramerate(long drawStart, double updateMilliseconds)
    {
#if !KNI_WEB
      if (!m_draw_framerate || _performanceView == 0 || !ShouldDrawFramerateCounter) return;
      long now = Stopwatch.GetTimestamp();
      var metrics = GraphicsDevice.Metrics; // Snapshot before the overlay adds its own draws.
      if (now >= _nextPerformanceText)
      {
        _nextPerformanceText = now + Stopwatch.Frequency / 4;
        var c = m_frameCounter;
        _performanceValues[0] = $"{c.AverageFramesPerSecond:0}";
        _performanceValues[1] = $"{c.MeanMilliseconds:0.00}";
        _performanceValues[2] = $"{c.P95Milliseconds:0.00} ms";
        _performanceValues[3] = $"{c.P99Milliseconds:0.00} ms";
        _performanceValues[4] = $"{c.MaxMilliseconds:0.00} ms";
        _performanceValues[5] = $"{c.OverBudgetFrames}";
        _performanceValues[6] = $"{updateMilliseconds:0.00} ms";
        _performanceValues[7] = $"{(now - drawStart) * 1000.0 / Stopwatch.Frequency:0.00} ms";
        _performanceValues[8] = $"{metrics.DrawCount}";
        _performanceValues[9] = $"{metrics.PrimitiveCount:N0}";
        _performanceValues[10] = $"{metrics.TextureCount}";
        _performanceValues[11] = $"{GC.GetTotalMemory(false) / (1024.0 * 1024):0.0} MB";
        _performanceValues[12] = _gcBaselineText();
        _performanceValues[13] = $"{c.SampleCount} / {c.HistorySeconds:0.0}s";
        _performanceStatus = $"VSync {(_graphics.SynchronizeWithVerticalRetrace ? "ON" : "OFF")}    Fixed step {(IsFixedTimeStep ? "ON" : "OFF")}";
      }
      var oldViewport = GraphicsDevice.Viewport;
      GraphicsDevice.Viewport = new Viewport(0, 0,
        GraphicsDevice.PresentationParameters.BackBufferWidth,
        GraphicsDevice.PresentationParameters.BackBufferHeight);
      try
      {
        var font = FontManager.GetDefaultFont(16);
        var headlineFont = FontManager.GetDefaultFont(28);
        string fpsValue = _performanceValues[0] ?? "--";
        string msValue = _performanceValues[1] ?? "--";
        float fpsWidth = font.MeasureString(fpsValue).X;
        float msWidth = font.MeasureString(msValue).X;
        float compactMsOffset = 12 + fpsWidth + 6 + font.MeasureString("FPS").X + 20;
        float width = _performanceView == 2 ? 600 : compactMsOffset + msWidth + 6 + font.MeasureString("ms").X + 12;
        int height = _performanceView == 2 ? 432 : 36;
        float scale = Math.Min(1f, Math.Min(GraphicsDevice.Viewport.Width / (width + 16),
          GraphicsDevice.Viewport.Height / (height + 16f)));
        float x = GraphicsDevice.Viewport.Width / scale - width - 8;
        const float y = 8;
        _spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointClamp,
          transformMatrix: Matrix.CreateScale(scale));
        var pixel = AssetManager.DefaultTexture;
        var labelColor = new Color(160, 179, 201);
        var valueColor = new Color(255, 221, 100);
        _spriteBatch.Draw(pixel, new Rectangle((int)x, (int)y, (int)width, height), new Color(12, 18, 28, 245));
        _spriteBatch.Draw(pixel, new Rectangle((int)x, (int)y, (int)width, 2), new Color(65, 205, 190));
        if (_performanceView == 1)
        {
          _spriteBatch.DrawString(font, fpsValue, new Vector2(x + 12, y + 9), valueColor);
          _spriteBatch.DrawString(font, "FPS", new Vector2(x + 12 + fpsWidth + 6, y + 9), labelColor);
          _spriteBatch.DrawString(font, msValue, new Vector2(x + compactMsOffset, y + 9), valueColor);
          _spriteBatch.DrawString(font, "ms", new Vector2(x + compactMsOffset + msWidth + 6, y + 9), labelColor);
        }
        if (_performanceView == 2)
        {
          _spriteBatch.DrawString(headlineFont, fpsValue, new Vector2(x + 18, y + 12), valueColor);
          _spriteBatch.DrawString(font, "FPS", new Vector2(x + 18, y + 48), labelColor);
          _spriteBatch.DrawString(headlineFont, msValue, new Vector2(x + 210, y + 12), valueColor);
          _spriteBatch.DrawString(font, "ms / frame", new Vector2(x + 210, y + 48), labelColor);
          for (int i = 0; i < PerformanceLabels.Length; ++i)
          {
            int row = i / 2, column = i % 2;
            float left = x + 18 + column * 290;
            float top = y + 90 + row * 29;
            _spriteBatch.DrawString(font, PerformanceLabels[i], new Vector2(left, top), labelColor);
            string value = _performanceValues[i + 2] ?? "--";
            float valueRight = left + 270;
            _spriteBatch.DrawString(font, value, new Vector2(valueRight - font.MeasureString(value).X, top), valueColor);
          }
          _spriteBatch.Draw(pixel, new Rectangle((int)x + 18, (int)y + 78, (int)width - 36, 1), new Color(45, 60, 78));
          _spriteBatch.DrawString(font, _performanceStatus, new Vector2(x + 18, y + 274), labelColor);
          int gx = (int)x + 18, gy = (int)y + 306, gw = (int)width - 36;
          const int gh = 80;
          float ceiling = Math.Max(33.34f, m_frameCounter.MaxMilliseconds * 1.1f);
          _spriteBatch.Draw(pixel, new Rectangle(gx, gy, gw, gh), new Color(5, 9, 16));
          // Compress older samples into pixel columns, preserving spikes using the maximum.
          int count = m_frameCounter.SampleCount;
          for (int column = 0; column < gw && count > 0; ++column)
          {
            int first = column * count / gw;
            int last = Math.Min(count, Math.Max(first + 1, (column + 1) * count / gw));
            float ms = 0;
            for (int i = first; i < last; ++i) ms = Math.Max(ms, m_frameCounter.GetFrameMilliseconds(i));
            int bar = Math.Clamp((int)(ms / ceiling * gh), 1, gh);
            var color = ms > 33.34f ? new Color(245, 100, 110) : ms > 16.67f
              ? new Color(245, 190, 85) : new Color(65, 205, 190);
            _spriteBatch.Draw(pixel, new Rectangle(gx + column, gy + gh - bar, 1, bar), color);
          }
          for (int guide = 0; guide < 2; ++guide)
          {
            float budget = guide == 0 ? 1000f / 120 : 1000f / 60;
            int lineY = gy + gh - (int)(budget / ceiling * gh);
            _spriteBatch.Draw(pixel, new Rectangle(gx, lineY, gw, 1), new Color(170, 185, 205, 150));
          }
          _spriteBatch.DrawString(font, $"Frame time / {ceiling:0.0} ms scale   lines: 120 / 60 FPS",
            new Vector2(gx, gy + gh + 4), new Color(150, 175, 195));
        }
        _spriteBatch.End();
      }
      finally { GraphicsDevice.Viewport = oldViewport; }
#endif
    }

    private string _gcBaselineText() =>
      $"{GC.CollectionCount(0) - _gcBaseline[0]} / {GC.CollectionCount(1) - _gcBaseline[1]} / {GC.CollectionCount(2) - _gcBaseline[2]}";
  }
}
