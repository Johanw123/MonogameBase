using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using AsyncContent;
using Gum.Forms.Controls;
using Gum.Forms.DefaultVisuals;
using Gum.Wireframe;
using JapeFramework;
using JapeFramework.Aseprite;
using JapeFramework.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;
using MonoGame.Extended;
using MonoGame.Extended.Graphics;
using MonoGame.Extended.Input;
using MonoGame.Extended.Screens;
using MonoGame.Extended.Screens.Transitions;
using MonoGameGum;
using UntitledGemGame.Entities;

namespace UntitledGemGame.Screens
{
  public class HarvesterStruct
  {
    public Sprite Sprite;
    public AnimatedSprite AnimatedSprite;
    public Transform2 Transform;
    public Vector2 TargetPosition = Vector2.Zero;
  }
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

      // m_camera = JapeFramework.BaseGame.Camera;
      m_camera = new OrthographicCamera(GameMain.BoxingViewportAdapter);
      m_camera.Zoom = 1.0f;

      var play = m_menuScreen.GetChildByNameRecursively("ButtonPlay") as Gum.Forms.DefaultFromFileVisuals.DefaultFromFileButtonRuntime;
      var exit = m_menuScreen.GetChildByNameRecursively("ButtonExit") as Gum.Forms.DefaultFromFileVisuals.DefaultFromFileButtonRuntime;

      play.Click += (s, e) =>
      {
        AudioManager.Instance.MenuClickButtonSoundEffect.Play();
        StartGame();
      };

      exit.Click += (s, e) =>
      {
        AudioManager.Instance.MenuClickButtonSoundEffect.Play();
        Game.Exit();
      };

      GumService.Default.Root.Children.Add(m_menuScreen);

      // GumService.Default.CanvasWidth = 1920 * 2;
      // GumService.Default.CanvasHeight = 1080 * 2;
      // GumService.Default.Root.UpdateLayout();
    }

    public override void LoadContent()
    {
      base.LoadContent();

      m_spriteBatch = new SpriteBatch(GraphicsDevice);

      TextureCache.PreloadTextures();
      EffectCache.PreloadEffects();

      AudioManager.Instance.LoadContent();

      FontManager.InitFieldFont(() => ContentDirectory.Fonts.Roboto_Regular_ttf);

      GameMain.AddCustomHudContent(DrawMenu);


      var p0 = m_camera.ScreenToWorld(BaseGame.ViewportMin);
      var p1 = m_camera.ScreenToWorld(BaseGame.ViewportMax);

      AssetManager.BatchLoaded += () =>
      {
        AudioManager.Instance.PlaySong("Greys");
        MediaPlayer.IsRepeating = true;

        for (int i = 0; i < 100; ++i)
        {
          var position = RandomHelper.Vector2(p0, p1);
          var p = m_camera.ScreenToWorld(position);
          CreateHarvester(p);
        }
      };
    }

    private List<HarvesterStruct> m_harvesters = new List<HarvesterStruct>();

    public void CreateHarvester(Vector2 position)
    {
      var animatedSprite = AsepriteHelper.LoadAnimation(
        "Textures/Foozle_2DS0013_Void_EnemyFleet_2/Nairan/Engine Effects/PNGs/Nairan - Scout - Engine.png",
        true,
        8,
        150);

      var sprite = new Sprite(TextureCache.HarvesterShip);
      sprite.Origin = new Vector2(sprite.TextureRegion.Width / 2.0f, sprite.TextureRegion.Height / 2.0f);

      m_harvesters.Add(new HarvesterStruct { Sprite = sprite, AnimatedSprite = animatedSprite, Transform = new Transform2(position) });
    }

    private void DrawMenu()
    {
      GumService.Default.Draw();
    }

    private float LerpAngle(float currentAngle, float targetAngle, float amount)
    {
      float difference = targetAngle - currentAngle;

      // Wrap the difference to ensure it is between -PI and PI
      while (difference < -MathHelper.Pi) difference += MathHelper.TwoPi;
      while (difference > MathHelper.Pi) difference -= MathHelper.TwoPi;

      // Apply the interpolated difference to the current angle
      return currentAngle + difference * amount;
    }

    private string previousButtonName = "null";
    public override void Update(GameTime gameTime)
    {
      var vp = BaseGame.BoxingViewportAdapter.Viewport;
      var scale = BaseGame.BoxingViewportAdapter.GetScaleMatrix();
      Matrix.Invert(ref scale, out scale);
      GumService.Default.Cursor.TransformMatrix = Matrix.CreateTranslation(-vp.X, -vp.Y, 0) * scale;

      GumService.Default.Update(gameTime);

      var curOverButtonName = GumService.Default.Cursor.WindowOver?.Name ?? "null";

      if (curOverButtonName != previousButtonName && curOverButtonName.Contains("Button"))
      {
        if (curOverButtonName != "null")
        {
          Console.WriteLine("Hovering over button: " + curOverButtonName);
          // AudioManager.Instance.PlaySound("MenuHover");
          AudioManager.Instance.MenuHoverButtonSoundEffect.Play();
        }
      }

      previousButtonName = curOverButtonName;

      var p0 = m_camera.ScreenToWorld(new Vector2(vp.X, vp.Y));
      var p1 = m_camera.ScreenToWorld(new Vector2(vp.X + vp.Width, vp.Y + vp.Height));

      foreach (var harvester in m_harvesters)
      {
        Vector2 spriteSize = new Vector2(harvester.Sprite.TextureRegion.Width, harvester.Sprite.TextureRegion.Height);
        Vector2 halfSpriteSize = spriteSize / 2.0f;

        if (harvester.TargetPosition == Vector2.Zero || Vector2.Distance(harvester.Transform.Position, harvester.TargetPosition) < 1.0f)
        {
          var position = RandomHelper.Vector2(p0 + halfSpriteSize, p1 - halfSpriteSize);
          harvester.TargetPosition = position;
        }
        else
        {
          var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

          var dir = harvester.TargetPosition - harvester.Transform.Position;
          dir.Normalize();
          var movement = dir * dt * UpgradeManager.UG.HarvesterSpeed * HomeBase.BonusMoveSpeed;

          float radians = (float)Math.Atan2(dir.Y, dir.X);
          harvester.Transform.Rotation = LerpAngle(harvester.Transform.Rotation, radians + (float)Math.PI / 2, dt * 20.0f);
          harvester.Transform.Position += movement;
        }

        harvester.AnimatedSprite.Update(gameTime);

        // harvester.Transform.Position = p1 - halfSpriteSize;
      }
    }

    private void StartGame()
    {
      MediaPlayer.IsRepeating = false;
      GumService.Default.Root.Children.Clear();
      ScreenManager.LoadScreen(new UntitledGemGameGameScreen(Game), new TestTransition(GraphicsDevice, Color.Black, m_camera, m_harvesters, 1.5f));
      GameMain.RemoveCustomHudContent(DrawMenu);
    }

    public Vector2 Measure2(string Text, Vector2 position, float FontSize)
    {
      var r = FontManager.GetTextRenderer("Roboto_Regular_ttf");
      r.PositiveYIsDown = true;
      r.ResetLayout();

      var fontSize = FontSize;
      var measure = r.MeasureText(Text, position, 0, 0, fontSize, Color.Transparent, Color.Transparent, r.EnableKerning, r.PositiveYIsDown, r.PositionByBaseline, 0, new Vector2(0, 0), true, -1);
      return measure;
    }
    // private void DrawText(SpriteBatch spriteBatch, string text)
    // {
    //   var font = FontManager.GetDefaultFont(150);
    //   var text_size = font.MeasureString(text);
    //   var pos_x = GraphicsDevice.Viewport.Width / 2.0f - text_size.X / 2.0f;
    //   var pos_y = GraphicsDevice.Viewport.Height / 2.0f - text_size.Y / 2.0f;
    //   spriteBatch.DrawString(font, text, new Vector2(pos_x, pos_y), Color.Yellow);
    // }
    //
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

      foreach (var harvester in m_harvesters)
      {
        m_spriteBatch.Begin(transformMatrix: m_camera.GetViewMatrix());
        m_spriteBatch.Draw(harvester.AnimatedSprite, harvester.Transform);
        m_spriteBatch.Draw(harvester.Sprite, harvester.Transform);
        m_spriteBatch.End();
      }

      var width = GameMain.Instance.GraphicsDevice.Viewport.Width;
      var height = GameMain.Instance.GraphicsDevice.Viewport.Height;

      // var windowWidth = GameMain.Instance.Window.ClientBounds.Width;
      // var windowHeight = GameMain.Instance.Window.ClientBounds.Height;

      // var width2 = GameMain.Instance.GraphicsDevice.PresentationParameters.BackBufferWidth;
      // var height2 = GameMain.Instance.GraphicsDevice.PresentationParameters.BackBufferHeight;

      // Console.WriteLine("w1: " + width + " - " + height);
      // Console.WriteLine("w2: " + windowWidth + " - " + windowHeight);
      // Console.WriteLine("w3: " + width2 + " - " + height2);

      string title = "Beyond the Belt";
      float scale = 128;
      var textSize = Measure2(title, Vector2.Zero, scale);

      var textRenderer = FontManager.GetTextRenderer(() => ContentDirectory.Fonts.Roboto_Regular_ttf);
      textRenderer.SetOrtographicProjection(width, height);
      textRenderer.UseScreenSpace = false;

      // textRenderer.ResetLayout();
      // textRenderer.SimpleLayoutText(title, position, color, strokeColor, scale, -1, wrap, wrapAt);
      // textRenderer.RenderStroke();
      // textRenderer.RenderText();


      m_spriteBatch.Begin();
      FontManager.RenderFieldFont(() => ContentDirectory.Fonts.Roboto_Regular_ttf, title, new Vector2(width / 2.0f - textSize.X / 2.0f, 25), Color.Gold, Color.Black, scale);
      m_spriteBatch.End();
    }
  }
}
