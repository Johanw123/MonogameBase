using AsyncContent;
using BracketHouse.FontExtension;
using FontStashSharp;
using ImGuiNET;
using JapeFramework;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.Input;
using MonoGame.Extended.Screens;
using MonoGame.Extended.Screens.Transitions;
using MonoGame.ImGuiNet;
using Serilog;
using Serilog.Sinks.Console.LogThemes;
using Serilog.Sinks.Console.LogThemes.Demo;
using Serilog.Sinks.SystemConsole.Themes;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using static System.Net.Mime.MediaTypeNames;
using Color = Microsoft.Xna.Framework.Color;

// https://badecho.com/index.php/2023/09/29/msdf-fonts-2/
//https://github.com/craftworkgames/MonoGame.Squid
//https://github.com/rive-app/rive-sharp
//https://docs.flatredball.com/gum/code/monogame
//Monogame extended uses GUM gui

namespace Base
{
  public class BaseGame : Game
  {
    protected GraphicsDeviceManager _graphics;
    protected SpriteBatch _spriteBatch;
    protected readonly ScreenManager _screenManager;
    protected bool showLoadingScreen = false;
    private ImGuiRenderer _imGuiRenderer;

    //public static GraphicsDevice Graphics;

    protected bool UseLoadingscreen = true;

    public BaseGame(string gameName, int bufferWidht = 1920, int bufferHeight = 1080, float targetFps = 60.0f, bool fixedTimeStep = true)
    {
      SetupLogger(gameName);

      _graphics = new GraphicsDeviceManager(this)
      {
        PreferredBackBufferWidth = bufferWidht,
        PreferredBackBufferHeight = bufferHeight,
        SynchronizeWithVerticalRetrace = false
      };

      Content.RootDirectory = "Content";

      IsFixedTimeStep = fixedTimeStep;
      TargetElapsedTime = TimeSpan.FromSeconds(1f / targetFps);

      _screenManager = new ScreenManager();
      Components.Add(_screenManager);
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

    protected override void Initialize()
    {
      AssetManager.Initialize(Content, GraphicsDevice);
      TextRenderer.Initialize(_graphics, Window, Content);

      _imGuiRenderer = new ImGuiRenderer(this);
      _imGuiRenderer.RebuildFontAtlas();

      _renderTarget = new RenderTarget2D(GraphicsDevice, _graphics.PreferredBackBufferWidth, _graphics.PreferredBackBufferHeight);
      _renderTargetImgui = new RenderTarget2D(GraphicsDevice, _graphics.PreferredBackBufferWidth, _graphics.PreferredBackBufferHeight);

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

    private RenderTarget2D _renderTarget;
    private RenderTarget2D _renderTargetImgui;

    protected override void Draw(GameTime gameTime)
    {
      GraphicsDevice.Clear(Color.CornflowerBlue);

      if (UseLoadingscreen && showLoadingScreen && AssetManager.IsLoadingContent())
      {
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        DrawText(_spriteBatch, "Loading...");
        _spriteBatch.End();
        return;
      }

      _graphics.GraphicsDevice.SetRenderTarget(_renderTarget);

      GraphicsDevice.Clear(Color.CornflowerBlue);
      //Game itself is drawn here
      base.Draw(gameTime);

      DrawImGui(gameTime);

      GraphicsDevice.SetRenderTarget(null);

      _spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointClamp);
      _spriteBatch.Draw(_renderTarget, new Microsoft.Xna.Framework.Rectangle(0, 0, _graphics.PreferredBackBufferWidth, _graphics.PreferredBackBufferHeight), Color.White);
      if (DrawImGuiEnabled)
      {
        _spriteBatch.Draw(_renderTargetImgui, new Microsoft.Xna.Framework.Rectangle(0, 0,
              _graphics.PreferredBackBufferWidth, _graphics.PreferredBackBufferHeight), Color.White);
      }
      _spriteBatch.End();

      DrawLoadingAssets();
    }

    public virtual bool DrawImGuiEnabled => true;

    public void DrawImGui(GameTime gameTime)
    {
      if (DrawImGuiEnabled)
      {
        _graphics.GraphicsDevice.SetRenderTarget(_renderTargetImgui);
        GraphicsDevice.Clear(Color.Transparent);

        DrawCustomImGuiContent(_imGuiRenderer, gameTime);
      }
    }

    public virtual void DrawCustomImGuiContent(ImGuiRenderer _imGuiRenderer, GameTime gameTime)
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
