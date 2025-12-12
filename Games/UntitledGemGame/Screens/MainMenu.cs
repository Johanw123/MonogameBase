using System;
using System.IO;
using System.Linq;
using System.Threading;
using AsyncContent;
using BracketHouse.FontExtension;
using FontStashSharp;
using Gum.Forms.Controls;
using Gum.Forms.DefaultVisuals;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using MonoGame.Extended.Input;
using MonoGame.Extended.Screens;
using MonoGame.Extended.Screens.Transitions;
using MonoGameGum;

namespace UntitledGemGame.Screens
{
  public class MainMenu : GameScreen
  {
    private SpriteBatch m_spriteBatch;
    private GraphicalUiElement m_menuScreen;
    private OrthographicCamera m_camera;

    public MainMenu(Game game, GraphicalUiElement menuScreen)
    : base(game)
    {
      m_menuScreen = menuScreen;
      game.IsMouseVisible = true;

      m_camera = new OrthographicCamera(GraphicsDevice);
      m_camera.Zoom = 1.0f;

      var play = m_menuScreen.GetChildByNameRecursively("ButtonPlay") as Gum.Forms.DefaultFromFileVisuals.DefaultFromFileButtonRuntime;
      var exit = m_menuScreen.GetChildByNameRecursively("ButtonExit") as Gum.Forms.DefaultFromFileVisuals.DefaultFromFileButtonRuntime;

      play.Click += (s, e) =>
      {
        StartGame();
        // m_camera.Zoom = UpgradeManager.UG.CameraZoomScale;
      };

      exit.Click += (s, e) =>
      {
        Game.Exit();
      };

      GumService.Default.Root.Children.Add(m_menuScreen);
    }

    public override void LoadContent()
    {
      base.LoadContent();

      m_spriteBatch = new SpriteBatch(GraphicsDevice);

      TextureCache.PreloadTextures();
      EffectCache.PreloadEffects();

      FontManager.InitFieldFont(() => ContentDirectory.Fonts.Roboto_Regular_ttf);
    }

    public override void Update(GameTime gameTime)
    {
      var mouseState = MouseExtended.GetState();
      var keyboardState = KeyboardExtended.GetState();

      if (keyboardState.WasKeyReleased(Keys.Escape))
        Game.Exit();

      // if (mouseState.LeftButton == ButtonState.Pressed || keyboardState.WasAnyKeyJustDown())
      //   StartGame();

      GumService.Default.Update(gameTime);
    }

    private void StartGame()
    {
      GumService.Default.Root.Children.Clear();
      // ScreenManager.LoadScreen(new UntitledGemGameGameScreen(Game), new FadeTransition(GraphicsDevice, Color.Black, 0.5f));
      ScreenManager.LoadScreen(new UntitledGemGameGameScreen(Game), new TestTransition(GraphicsDevice, Color.Black, m_camera, 1.5f));
    }

    public override void Draw(GameTime gameTime)
    {
      var effect = EffectCache.BackgroundEffect.Value;

      var zoom = m_camera.Zoom;
      m_camera.Zoom = 0.5f * zoom;
      effect.Parameters["view_projection"]?.SetValue(m_camera.GetBoundingFrustum().Matrix);
      m_camera.Zoom = zoom;

      var bkg = TextureCache.SpaceBackground.Value;
      var bounds = new Rectangle(TextureCache.SpaceBackground.Value.Bounds.X, TextureCache.SpaceBackground.Value.Bounds.Y,
        TextureCache.SpaceBackground.Value.Bounds.Width * 5, TextureCache.SpaceBackground.Value.Bounds.Height * 5);

      Rectangle size = new Rectangle(-bkg.Width * 5, -bkg.Height * 5, bkg.Width * 10, bkg.Height * 10);

      m_spriteBatch.Begin(effect: effect, depthStencilState: DepthStencilState.Default, samplerState: SamplerState.AnisotropicWrap);
      m_spriteBatch.Draw(TextureCache.SpaceBackground, size, bounds,
          Color.White, 0, new Vector2(0, 0), SpriteEffects.None, 0);
      m_spriteBatch.Draw(TextureCache.SpaceBackground2, size, bounds,
          Color.White, 0, new Vector2(0, 0), SpriteEffects.None, 0);
      m_spriteBatch.Draw(TextureCache.SpaceBackground3, size, bounds,
          Color.White, 0, new Vector2(0, 0), SpriteEffects.None, 0);
      m_spriteBatch.End();

      GumService.Default.Draw();

      m_spriteBatch.Begin();
      FontManager.RenderFieldFont(() => ContentDirectory.Fonts.Roboto_Regular_ttf, "Beyond the Belt", new Vector2(10, 10), Color.Gold, Color.Black, 128);
      m_spriteBatch.End();
    }
  }
}
