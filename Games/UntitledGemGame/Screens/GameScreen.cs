using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.ECS;
using MonoGame.Extended.Input;
using MonoGame.Extended.Screens;
using System;
using System.IO;
using System.Threading;
using JapeFramework.Helpers;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using UntitledGemGame.Entities;
using UntitledGemGame.Systems;

namespace UntitledGemGame.Screens
{
  public class UntitledGemGameGameScreen : GameScreen
  {
    private SpriteBatch m_spriteBatch;

    private World m_escWorld;
    private EntityFactory m_entityFactory;
    private OrthographicCamera m_camera;

    private FrameCounter _frameCounter = new FrameCounter();

    public static int Collected;
    public static int Delivered;


    public UntitledGemGameGameScreen(Game game) : base(game)
    {
      game.IsMouseVisible = true;
    }

    public static Vector2 HomeBasePos;

    public override void LoadContent()
    {
      base.LoadContent();
      m_spriteBatch = new SpriteBatch(GraphicsDevice);

      m_camera = new OrthographicCamera(GraphicsDevice);

      m_escWorld = new WorldBuilder()
        .AddSystem(new HarvesterCollectionSystem(m_camera))
        .AddSystem(new UpdateSystem2())
        .AddSystem(new RenderSystem(m_spriteBatch, GraphicsDevice, m_camera))
        .Build();

      m_entityFactory = new EntityFactory(m_escWorld, GraphicsDevice);

      m_camera.Zoom = Upgrades.CameraZoomScale;

      HomeBasePos = m_camera.ScreenToWorld(new Vector2(GraphicsDevice.Viewport.Width / 2.0f, GraphicsDevice.Viewport.Height / 2.0f));
      m_entityFactory.CreateHomeBase(HomeBasePos);

      ImGuiContent();
    }

    public override void Update(GameTime gameTime)
    {
      var keyboardState = KeyboardExtended.GetState();
      //if(keyboardState.IsKeyDown(Keys.A))
      //if (keyboardState.WasKeyPressed(Keys.A))
      if (HarvesterCollectionSystem.m_gems2.Count < Upgrades.MaxGemCount)
      {
        //var a = m_camera.ScreenToWorld(0, 0);
        //var b = m_camera.ScreenToWorld(GraphicsDevice.Viewport.Width - (18 * m_camera.Zoom), GraphicsDevice.Viewport.Height - (30 * m_camera.Zoom));
        //m_entityFactory.CreateGem(RandomHelper.Vector2(Vector2.Zero, new Vector2(1920, 900)), (GemTypes)RandomHelper.Int(0, 7));
        for (int i = 0; i < Upgrades.GemSpawnRate; i++)
        {
          var a = m_camera.ScreenToWorld(RandomHelper.Vector2(new Vector2(50, 50), new Vector2(1900, 800)));
          m_entityFactory.CreateGem(a, GemTypes.Red);
        }

        //m_entityFactory.CreateGem(b, GemTypes.Gold);
      }

      if (keyboardState.WasKeyPressed(Keys.B))
      {
        var a = m_camera.ScreenToWorld(RandomHelper.Vector2(Vector2.Zero, new Vector2(1920, 900)));
        m_entityFactory.CreateHarvester(a);
      }

      if (keyboardState.IsKeyDown(Keys.I))
      {
        m_camera.ZoomIn(0.01f);
      }

      if (keyboardState.IsKeyDown(Keys.O))
      {
        m_camera.ZoomOut(0.01f);
      }

      m_escWorld.Update(gameTime);
    }

    private void ImGuiContent()
    {
      GameMain.AddCustomImGuiContent(() =>
          {
            // var deltaTime = (float)GameMain.GameInstance.TargetElapsedTime.TotalSeconds;
            // _frameCounter.Update(deltaTime);
            var fps = string.Format("FPS: {0}", _frameCounter.AverageFramesPerSecond);
            ImGuiNET.ImGui.Text(fps);
            ImGuiNET.ImGui.Text($"Entities: {m_escWorld.EntityCount}");
            ImGuiNET.ImGui.Text($"Picked Up: {Collected}");
            ImGuiNET.ImGui.Text($"Delivered: {Delivered}");
          });
    }

    public override void Draw(GameTime gameTime)
    {
      m_escWorld.Draw(gameTime);

      var deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
      _frameCounter.Update(deltaTime);
      // var fps = string.Format("FPS: {0}", _frameCounter.AverageFramesPerSecond);

      // m_spriteBatch.Begin(SpriteSortMode.Immediate);
      // FontManager.RenderFieldFont(() => ContentDirectory.Fonts.Roboto_Regular_ttf, fps, new Vector2(10, 10), Color.Black, Color.White, 35);
      // FontManager.RenderFieldFont(() => ContentDirectory.Fonts.Roboto_Regular_ttf, $"Entities: {m_escWorld.EntityCount}", new Vector2(10, 40), Color.Black, Color.White, 35);
      // FontManager.RenderFieldFont(() => ContentDirectory.Fonts.Roboto_Regular_ttf, $"Picked Up: {Collected}", new Vector2(10, 70), Color.Black, Color.White, 35);
      // FontManager.RenderFieldFont(() => ContentDirectory.Fonts.Roboto_Regular_ttf, $"Delivered: {Delivered}", new Vector2(10, 100), Color.Black, Color.White, 35);
      // m_spriteBatch.End();
    }
  }
}
