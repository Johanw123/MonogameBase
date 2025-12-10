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

    private BloomFilter _bloomFilter;
    private Bloom bloom;

    private int VirtualWidth = 1280;
    private int VirtualHeight = 720;
    private Matrix _scaleMatrix; // Scaling matrix for the SpriteBatch

    protected bool UseLoadingscreen = true;

    public BaseGame(string gameName, int bufferWidht = 1920, int bufferHeight = 1080, float targetFps = 60.0f, bool fixedTimeStep = true, bool fullscreen = false)
    {
      SetupLogger(gameName);

      VirtualWidth = bufferWidht;
      VirtualHeight = bufferHeight;

      // int newWidth = Window.ClientBounds.Width;
      // int newHeight = Window.ClientBounds.Height;

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
      };

      // Window.AllowUserResizing = true;

      Content.RootDirectory = "Content";

      IsFixedTimeStep = fixedTimeStep;
      TargetElapsedTime = TimeSpan.FromSeconds(1f / targetFps);

      _screenManager = new ScreenManager();
      Components.Add(_screenManager);
    }

    private void CalculateScaleMatrix(int actualWidth, int actualHeight)
    {
      // Calculate the scale factors needed to stretch the virtual resolution
      float scaleX = (float)actualWidth / VirtualWidth;
      float scaleY = (float)actualHeight / VirtualHeight;

      // If you want a non-uniform stretch (to fill without borders), use individual scale factors.
      // If you only want to scale up uniformly to fit without stretching, you'd use:
      // float scale = Math.Min(scaleX, scaleY);
      // _scaleMatrix = Matrix.CreateScale(scale, scale, 1.0f);

      // To stretch and fill the whole screen (this will prevent black bars)
      _scaleMatrix = Matrix.CreateScale(scaleX, scaleY, 1.0f);
    }

    private void GoToFullscreen()
    {
      // 1. Get the current display's resolution
      DisplayMode displayMode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;

      // 2. Set the preferred back buffer size to the display's resolution
      _graphics.PreferredBackBufferWidth = displayMode.Width;
      _graphics.PreferredBackBufferHeight = displayMode.Height;

      // 3. Enable fullscreen
      _graphics.IsFullScreen = true;

      // 4. Apply the changes
      _graphics.ApplyChanges();

      // 5. Calculate the new scaling matrix to stretch the virtual resolution to the screen
      CalculateScaleMatrix(displayMode.Width, displayMode.Height);
    }

    private bool _isResizing = false;

    private void Window_ClientSizeChanged(object sender, EventArgs e)
    {
      if (_isResizing)
        return; // Already handling a resize, skip to prevent infinite loop

      _isResizing = true;

      int newWidth = Window.ClientBounds.Width;
      int newHeight = Window.ClientBounds.Height;

      _graphics.PreferredBackBufferWidth = newWidth;
      _graphics.PreferredBackBufferHeight = newHeight;
      _graphics.ApplyChanges();

      _isResizing = false;

      // Update game view/UI here...
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
      // Window.ClientSizeChanged += Window_ClientSizeChanged;
      _graphics.PreferredBackBufferFormat = SurfaceFormat;
      _graphics.GraphicsProfile = GraphicsProfile.HiDef;

      AssetManager.Initialize(Content, GraphicsDevice);
      TextRenderer.Initialize(_graphics, Window, Content);

      bool isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
      // bool isMac = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
      // bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
      bool isArm = RuntimeInformation.OSArchitecture == Architecture.Arm64;

      bool supportImGui = !(isLinux && isArm);

      // if (supportImGui)
      {
        _imGuiRenderer = new ImGuiRenderer(this);
        _imGuiRenderer.RebuildFontAtlas();
      }

      // _renderTarget = new RenderTarget2D(GraphicsDevice, _graphics.PreferredBackBufferWidth, _graphics.PreferredBackBufferHeight, true, SurfaceFormat, DepthFormat);
      _renderTargetImgui = new RenderTarget2D(GraphicsDevice, _graphics.PreferredBackBufferWidth, _graphics.PreferredBackBufferHeight, true, SurfaceFormat, DepthFormat);
      _renderTargetHud = new RenderTarget2D(GraphicsDevice, _graphics.PreferredBackBufferWidth, _graphics.PreferredBackBufferHeight, true, SurfaceFormat, DepthFormat);

      renderTarget1 = new RenderTarget2D(GraphicsDevice, _graphics.PreferredBackBufferWidth, _graphics.PreferredBackBufferHeight, true, SurfaceFormat, DepthFormat);
      renderTarget2 = new RenderTarget2D(GraphicsDevice, _graphics.PreferredBackBufferWidth, _graphics.PreferredBackBufferHeight, true, SurfaceFormat, DepthFormat);

      //https://www.alienscribbleinteractive.com/Tutorials/bloom_tutorial.html
      _bloomFilter = new BloomFilter();
      _bloomFilter.Load(GraphicsDevice, Content, _graphics.PreferredBackBufferWidth, _graphics.PreferredBackBufferHeight, SurfaceFormat);
      _bloomFilter.BloomPreset = BloomFilter.BloomPresets.Focussed;
      _bloomFilter.BloomUseLuminance = true;
      _bloomFilter.BloomStreakLength = 3;
      _bloomFilter.BloomThreshold = 0.6f;

      // GoToFullscreen();

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
      //_screenManager.LoadScreen(new MainMenu(this), new FadeTransition(GraphicsDevice, Color.Black, 1.5f));


    }

    protected virtual void LoadInitialScreen(ScreenManager screenManager)
    {

    }

    public static GameTime Time;
    protected override void Update(GameTime gameTime)
    {
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
      GraphicsDevice.Clear(Color.CornflowerBlue);

      if (_isResizing)
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

      //_graphics.GraphicsDevice.SetRenderTarget(_renderTarget);

      //GraphicsDevice.Clear(Color.CornflowerBlue);
      ////Game itself is drawn here
      //base.Draw(gameTime);

      //Texture2D bloom = _bloomFilter.Draw(_renderTarget, _graphics.PreferredBackBufferWidth, _graphics.PreferredBackBufferHeight);

      //DrawImGui(gameTime);

      //GraphicsDevice.SetRenderTarget(null);

      //_spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive);
      //_spriteBatch.Draw(_renderTarget, new Microsoft.Xna.Framework.Rectangle(0, 0, _graphics.PreferredBackBufferWidth, _graphics.PreferredBackBufferHeight), Color.White);
      //_spriteBatch.Draw(bloom, new Microsoft.Xna.Framework.Rectangle(0, 0, _graphics.PreferredBackBufferWidth, _graphics.PreferredBackBufferHeight), Color.White);
      //_spriteBatch.End();

      //_spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointClamp);
      //if (DrawImGuiEnabled)
      //{
      //  _spriteBatch.Draw(_renderTargetImgui, new Microsoft.Xna.Framework.Rectangle(0, 0,
      //    _graphics.PreferredBackBufferWidth, _graphics.PreferredBackBufferHeight), Color.White);
      //}
      //_spriteBatch.End();

      //Render to HUD and ImGui to their own render targets
      DrawHud(gameTime);
      DrawImGui(gameTime);

      GraphicsDevice.SetRenderTarget(renderTarget1);
      GraphicsDevice.Clear(Color.Transparent);

      base.Draw(gameTime);

      bloom.Draw(renderTarget1, renderTarget2);
      bloom.Settings = BloomSettings.PresetSettings[0];

      GraphicsDevice.SetRenderTarget(null);

      var windowWidth = Window.ClientBounds.Width;
      var windowHeight = Window.ClientBounds.Height;

      var graphicsWidth = _graphics.PreferredBackBufferWidth;
      var graphicsHeight = _graphics.PreferredBackBufferHeight;

      _spriteBatch.Begin(0, BlendState.AlphaBlend);
      // _spriteBatch.Draw(renderTarget2, new Rectangle(0, 0, graphicsWidth, graphicsHeight), Color.White); // draw all glowing components            
      _spriteBatch.Draw(renderTarget2, new Vector2(0, 0), Color.White); // draw all glowing components            
      // _spriteBatch.Draw(renderTarget2, new Rectangle(0, 0, graphicsWidth, graphicsHeight), new Rectangle(0, 0, windowWidth, windowHeight), Color.White); // draw all glowing components            
      _spriteBatch.End();

      _spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointClamp);

      if (ShouldDrawImGui)
      {
        _spriteBatch.Draw(_renderTargetImgui, new Rectangle(0, 0, graphicsWidth, graphicsHeight), Color.White);
      }
      _spriteBatch.Draw(_renderTargetHud, new Rectangle(0, 0, graphicsWidth, graphicsHeight), Color.White);

      _spriteBatch.End();

      DrawLoadingAssets();
    }

    public bool ShouldDrawImGui => DrawImGuiEnabled && IsImGuiSPlatformSupported;
    public virtual bool DrawImGuiEnabled => true;
    public virtual bool IsImGuiSPlatformSupported => true;
    // public virtual bool IsImGuiSPlatformSupported => !(RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && RuntimeInformation.OSArchitecture == Architecture.Arm64);

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
