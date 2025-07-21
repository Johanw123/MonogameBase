using System;
using System.IO;
using FontExtension;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using MonoGame.Extended.ViewportAdapters;
using MonoMSDF.Text;

namespace UntitledMonoGameMetroidvania
{
  public class Game1 : Game
  {
    private GraphicsDeviceManager graphics;
    public static TextRenderer TextRenderer;
    private FontSystem _fontSystem;
    private SpriteBatch _spriteBatch;
    //private KeyboardListener _keyboardListener;
    //private MouseListener _mouseListener;

    public Game1()
    {
      this.graphics = new GraphicsDeviceManager(this)
      {
        PreferredBackBufferWidth = 2560,
        PreferredBackBufferHeight = 1440,
        IsFullScreen = false,
        SynchronizeWithVerticalRetrace = false,
        GraphicsProfile = GraphicsProfile.HiDef,
        
      };

      Window.AllowUserResizing = false;

      Window.ClientSizeChanged += OnResize;

      this.Content.RootDirectory = "Content";
    }

    protected override void Initialize()
    {
      base.Initialize();

      //_keyboardListener = new KeyboardListener();
      //_mouseListener = new MouseListener();

      //Components.Add(new InputListenerComponent(this, _keyboardListener, _mouseListener));

      //_keyboardListener.KeyPressed += (sender, args) =>
      //{
      //  Window.Title = $"Key {args.Key} Pressed";

      //  if (args.Key == Keys.A)
      //  {
      //    graphics.PreferredBackBufferWidth = 2560;
      //    graphics.PreferredBackBufferHeight = 1440;
      //    graphics.ApplyChanges();
      //  }
      //  if (args.Key == Keys.B)
      //  {
      //    graphics.PreferredBackBufferWidth = 1920;
      //    graphics.PreferredBackBufferHeight = 1080;
      //    graphics.ApplyChanges();
      //  }
      //  if (args.Key == Keys.C)
      //  {
      //    graphics.PreferredBackBufferWidth = 1280;
      //    graphics.PreferredBackBufferHeight = 720;
      //    graphics.ApplyChanges();
      //  }



      //  if (args.Key == Keys.A)
      //  {
      //    _camera.Move(new Vector2(-1, 0));
      //  }
      //  if (args.Key == Keys.D)
      //  {
      //    _camera.Move(new Vector2(1, 0));
      //  }
      //  if (args.Key == Keys.W)
      //  {
      //    _camera.Move(new Vector2(0, -1));
      //  }
      //  if (args.Key == Keys.S)
      //  {
      //    _camera.Move(new Vector2(0, 1));
      //  }
      //};

      //_mouseListener.MouseWheelMoved += (sender, args) =>
      //{
      //  //textScale += args.ScrollWheelDelta * 0.1f;
      //  if(args.ScrollWheelDelta > 0)
      //    _camera.ZoomIn(args.ScrollWheelDelta * 0.01f);
      //  else if(args.ScrollWheelDelta < 0)
      //    _camera.ZoomOut(args.ScrollWheelDelta * -1 * 0.01f);
      //};

      graphics.PreferredBackBufferWidth = 2560;
      graphics.PreferredBackBufferHeight = 1440;
      graphics.ApplyChanges();


      var viewportAdapter = new BoxingViewportAdapter(Window, GraphicsDevice, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
      _camera = new OrthographicCamera(viewportAdapter);

      _camera.Zoom = 50.0f;
    }

    protected override void LoadContent()
    {
      var effect = this.Content.Load<Effect>("FieldFontEffect");
      var font = this.Content.Load<FieldFont>("arial");

      TextRenderer = new TextRenderer(effect, font, this.GraphicsDevice);

      _spriteBatch = new SpriteBatch(GraphicsDevice);

      _fontSystem = new FontSystem();
      _fontSystem.AddFont(File.ReadAllBytes(@"Content/fonts/verdana.ttf"));
      //_fontSystem.AddFont(File.ReadAllBytes(@"Fonts/DroidSansJapanese.ttf"));
      //_fontSystem.AddFont(File.ReadAllBytes(@"Fonts/Symbola-Emoji.ttf"));
    }

    protected override void Update(GameTime gameTime)
    {
      if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed
          || Keyboard.GetState().IsKeyDown(Keys.Escape))
        Exit();

      base.Update(gameTime);
    }

    public Vector2 ScreenToWorldSpace(Matrix transformMatrix, in Vector2 point)
    {
      Matrix invertedMatrix = Matrix.Invert(transformMatrix);
      return Vector2.Transform(point, invertedMatrix);
    }

    static Point Transform(Point point, Matrix matrix)
    {
      var vector = point.ToVector2();
      var transformedVector = Vector2.Transform(vector, Matrix.Invert(matrix));
      return transformedVector.ToPoint();
    }

    private OrthographicCamera _camera;

    public void OnResize(object sender, EventArgs e)
    {

      if ((graphics.PreferredBackBufferWidth != graphics.GraphicsDevice.Viewport.Width) ||
          (graphics.PreferredBackBufferHeight != graphics.GraphicsDevice.Viewport.Height))
      {
        graphics.PreferredBackBufferWidth = graphics.GraphicsDevice.Viewport.Width;
        graphics.PreferredBackBufferHeight = graphics.GraphicsDevice.Viewport.Height;
        graphics.ApplyChanges();

        var viewportAdapter = new BoxingViewportAdapter(Window, GraphicsDevice, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
        _camera = new OrthographicCamera(viewportAdapter);
        //States[_currentState].Rearrange();
      }
    }

    private float textScale = 25;

    protected override void Draw(GameTime gameTime)
    {
      this.GraphicsDevice.Clear(Color.Black);

      this.GraphicsDevice.BlendState = BlendState.AlphaBlend;
      this.GraphicsDevice.DepthStencilState = DepthStencilState.None;
      this.GraphicsDevice.RasterizerState = RasterizerState.CullNone;

      //_camera.WorldToScreen(scaledScale, 0);

      float scale = _camera.Zoom;

      var tb = new TextBlock();
      tb.Text = "Hello World";
      tb.Scale = scale;
      tb.TextColor = Color.Gold;
      tb.HorizontalTextAlignment = HorizontalTextAlignment.Center;
      tb.Draw(graphics.GraphicsDevice);

      var tb2 = new TextBlock();
      tb2.Text = "Hello World Left";
      tb2.TextColor = Color.Bisque;
      tb2.Scale = scale;
      tb2.HorizontalAlignment = HorizontalAlignment.Left;
      tb2.Draw(graphics.GraphicsDevice);

      var tb3 = new TextBlock();
      tb3.Text = "Hello World Right";
      tb3.TextColor = Color.AliceBlue;
      tb3.Scale = scale;
      tb3.HorizontalAlignment = HorizontalAlignment.Right;
      tb3.Draw(graphics.GraphicsDevice);

      //_spriteBatch.Begin(transformMatrix: _camera.GetViewMatrix());

      //SpriteFontBase font18 = _fontSystem.GetFont(18);
      //_spriteBatch.DrawString(font18, "The quick いろは brown\nfox にほへ jumps over\nt🙌h📦e l👏a👏zy dog", new Vector2(500, 0), Color.White);

      //SpriteFontBase font30 = _fontSystem.GetFont(800);
      //_spriteBatch.DrawString(font30, "The quick いろは brown\nfox にほへ jumps over\nt🙌h📦e l👏a👏zy dog", new Vector2(0, 80), Color.Yellow);
      //      _spriteBatch.End();


      //textRenderer.GetTextHeight("")

      //var height = TextRenderer.GetTextHeight("aiwudhiawdhwiaud");

      //DrawText("aiwudhiawdhwiaud", textScale, new Vector2(0,
      //  0));


      //DrawText("aiwudhiawdhwiaud", textScale, new Vector2(-GraphicsDevice.Viewport.Width / 2.0f,
      //  GraphicsDevice.Viewport.Height / 2.0f - height * _camera.Zoom * 0.03f));

      //DrawTextScaleFromBottom("aiwudhiawdhwiaud", textScale, new Vector2(0,
      //  GraphicsDevice.Viewport.Height - height));
      //world = Matrix.CreateScale(0.01f) * Matrix.CreateRotationY((float)gameTime.TotalGameTime.TotalSeconds) * Matrix.CreateRotationZ(MathHelper.PiOver4);
      //view = Matrix.CreateLookAt(Vector3.Backward, Vector3.Forward, Vector3.Up);
      //projection = Matrix.CreatePerspectiveFieldOfView(
      //    MathHelper.PiOver2,
      //    viewport.Width / (float)viewport.Height,
      //    0.01f,
      //    1000.0f);

      //wvp = world * view * projection;

      //l = this.textRenderer.Render("          To Infinity And Beyond!", wvp);
    }

    public enum HorizontalAlignment
    {
      Left,
      Center,
      Stretch,
      Right
    }

    public enum VerticalAlignment
    {
      Top,
      Center,
      Stretch,
      Bottom
    }

    public enum HorizontalTextAlignment
    {
      Left,
      Right,
      Center
    }

    public enum VerticalTextAlignment
    {
      Top,
      Bottom,
      Center
    }


    public class TextBlock
    {
      private float m_scale = 1.0f;
      private float m_scaleFactor = 0.05f;

      public string Text { get; set; }

      public Color TextColor { get; set; } = Color.White;

      public float Scale
      {
        get => m_scale * m_scaleFactor;
        set => m_scale = value;
      }

      public HorizontalAlignment HorizontalAlignment { get; set; } = HorizontalAlignment.Center;
      public VerticalAlignment VerticalAlignment { get; set; } = VerticalAlignment.Center;

      public HorizontalTextAlignment HorizontalTextAlignment { get; set; } = HorizontalTextAlignment.Left;
      public VerticalTextAlignment VerticalTextAlignment { get; set; } = VerticalTextAlignment.Center;

      public void Draw(GraphicsDevice graphicsDevice)
      {
        var viewport = graphicsDevice.Viewport;

        //var height = textRenderer.GetTextHeight("aiwudhiawdhwiaud");

        var view = Matrix.CreateLookAt(Vector3.Backward, Vector3.Forward, Vector3.Up);

        Matrix projection = Matrix.CreateOrthographic(viewport.Width, viewport.Height, 0.01f, 1000.0f);

        var wvp = Matrix.Identity * view * projection;

        Vector2 offsetPosition = Vector2.Zero;

        var width = TextRenderer.GetTextWidth(Text, Scale);

        switch (HorizontalAlignment)
        {
          case HorizontalAlignment.Left:
            offsetPosition.X = -viewport.Width / 2.0f;
            break;
          case HorizontalAlignment.Center:
            break;
          case HorizontalAlignment.Stretch:
            break;
          case HorizontalAlignment.Right:
            offsetPosition.X = viewport.Width / 2.0f - (width);
            break;
          default:
            throw new ArgumentOutOfRangeException();
        }

        switch (HorizontalTextAlignment)
        {
          case HorizontalTextAlignment.Left:
            break;
          case HorizontalTextAlignment.Right:
            break;
          case HorizontalTextAlignment.Center:
            offsetPosition.X -= width / 2.0f;
            break;
          default:
            throw new ArgumentOutOfRangeException();
        }

        TextRenderer.OptimizeForTinyText = m_scale <= 15.0f;
        TextRenderer.Render(Text, wvp, Scale, offsetPosition, TextColor);
      }
    }







    //void DrawText(string text, float scale, Vector2 position)
    //{
    //  var viewport = GraphicsDevice.Viewport;

    //  var height = TextRenderer.GetTextHeight("aiwudhiawdhwiaud", scale);

    //  float scaledScale = _camera.Zoom * 0.3f;

    //  var world = /*Matrix.CreateScale(scaledScale) **/ Matrix.CreateTranslation((-viewport.Width / 2.0f + position.X),
    //    viewport.Height / 2.0f - ((height ) * scaledScale * 2.0f) /*- (26 * scale * 0.03f)*/ - position.Y, 0);

      

    //  var view = Matrix.CreateLookAt(Vector3.Backward, Vector3.Forward, Vector3.Up);

    //  Matrix projection = Matrix.CreateOrthographic(viewport.Width, viewport.Height, 0.01f, 1000.0f);


    //  //_camera.LookAt(Vector2.Zero);


    //  var v = _camera.WorldToScreen(scaledScale, 0);

    //  //world = Matrix.CreateTranslation((-viewport.Width / 2.0f) + position.X,
    //  //  viewport.Height / 2.0f - position.Y, 0);
    //  var wvp = Matrix.Identity * view * projection;

    //  TextRenderer.Render(text, wvp, _camera.Zoom * 0.03f, position);
    //}

    //void DrawTextScaleFromBottom(string text, float scale, Vector2 position)
    //{
    //  var viewport = GraphicsDevice.Viewport;

    //  var height = TextRenderer.GetTextHeight("aiwudhiawdhwiaud", scale);

    //  float scaledScale = _camera.Zoom * 0.3f;

    //  var world = Matrix.CreateScale(scaledScale) * Matrix.CreateTranslation((-viewport.Width / 2.0f + position.X),
    //    viewport.Height / 2.0f - (height / 2.0f) * scaledScale /*- (26 * scale * 0.03f)*/ - position.Y, 0);




    //  var view = Matrix.CreateLookAt(Vector3.Backward, Vector3.Forward, Vector3.Up);

    //  Matrix projection = Matrix.CreateOrthographic(viewport.Width, viewport.Height, 0.01f, 1000.0f);


    //  //_camera.LookAt(Vector2.Zero);


    //  var v = _camera.WorldToScreen(scaledScale, 0);

    //  world = Matrix.CreateTranslation((-viewport.Width / 2.0f) + position.X,
    //    viewport.Height / 2.0f - position.Y, 0);
    //  var wvp = world *  view  * projection;

    //  TextRenderer.Render(text, wvp, _camera.Zoom, position);
    //}
  }
}
