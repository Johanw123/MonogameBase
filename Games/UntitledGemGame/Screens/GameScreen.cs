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
using UntitledGemGame.Systems;

namespace UntitledGemGame.Screens
{
  public class UntitledGemGameGameScreen : GameScreen
  {
    private SpriteBatch m_spriteBatch;

    private World m_escWorld;
    private EntityFactory m_entityFactory;
    private OrthographicCamera m_camera;

    public UntitledGemGameGameScreen(Game game) : base(game)
    {
      game.IsMouseVisible = true;
    }

    public override void LoadContent()
    {
      base.LoadContent();
      m_spriteBatch = new SpriteBatch(GraphicsDevice);

      m_camera = new OrthographicCamera(GraphicsDevice);

      m_escWorld = new WorldBuilder()
        .AddSystem(new RenderSystem(m_spriteBatch, GraphicsDevice, m_camera))
        .AddSystem(new HarvesterSystem())
        .Build();

      m_entityFactory = new EntityFactory(m_escWorld, GraphicsDevice);

      m_camera.Zoom = 2.5f;
    }

    public override void Update(GameTime gameTime)
    {
      var keyboardState = KeyboardExtended.GetState();
      //if (keyboardState.WasKeyPressed(Keys.A))
      {
        var a = m_camera.ScreenToWorld(RandomHelper.Vector2(Vector2.Zero, new Vector2(1920, 900)));
        //var a = m_camera.ScreenToWorld(0, 0);
        //var b = m_camera.ScreenToWorld(GraphicsDevice.Viewport.Width - (18 * m_camera.Zoom), GraphicsDevice.Viewport.Height - (30 * m_camera.Zoom));
        //m_entityFactory.CreateGem(RandomHelper.Vector2(Vector2.Zero, new Vector2(1920, 900)), (GemTypes)RandomHelper.Int(0, 7));
        m_entityFactory.CreateGem(a, GemTypes.Red);
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

    public override void Draw(GameTime gameTime)
    {
      m_spriteBatch.Begin();

      //FontManager.RenderFieldFont(() => ContentDirectory.Fonts.Roboto_Regular_ttf, "Game", new Vector2(10, 10), Color.Gold, Color.Black, 128);

      m_spriteBatch.End();

      m_escWorld.Draw(gameTime);
    }
  }
}
