using AsyncContent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using MonoGame.Extended.ECS;
using MonoGame.Extended.ECS.Systems;
using MonoGame.Extended.Graphics;
using MonoGame.Extended.Input;
using System;
using UntitledGemGame.Entities;


namespace UntitledGemGame.Systems
{
  public class RenderSystem : EntityDrawSystem
  {
    private readonly SpriteBatch _spriteBatch;
    private readonly GraphicsDevice _graphicsDevice;
    private OrthographicCamera m_camera;

    private ComponentMapper<AnimatedSprite> _animatedSpriteMapper;
    private ComponentMapper<Sprite> _spriteMapper;
    private ComponentMapper<Transform2> _transforMapper;

    private BasicEffect _simpleEffect;
    private AsyncAsset<Effect> harvesterEffect;
    private AsyncAsset<Effect> backgroundEffect;
    private Texture2D spaceBackground;
    private Texture2D spaceBackgroundDepth;

    public RenderSystem(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, OrthographicCamera camera)
  : base(Aspect.All(typeof(Transform2)).One(typeof(AnimatedSprite), typeof(Sprite)).Exclude(typeof(Gem)))
    {
      _spriteBatch = spriteBatch;
      _graphicsDevice = graphicsDevice;
      m_camera = camera;
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
      _transforMapper = mapperService.GetMapper<Transform2>();
      _animatedSpriteMapper = mapperService.GetMapper<AnimatedSprite>();
      _spriteMapper = mapperService.GetMapper<Sprite>();

      _simpleEffect = new BasicEffect(_graphicsDevice);
      _simpleEffect.TextureEnabled = true;

      harvesterEffect = AssetManager.LoadAsync<Effect>(ContentDirectory.Shaders.HarvesterShader_fx);
      backgroundEffect = AssetManager.LoadAsync<Effect>(ContentDirectory.Shaders.BackgroundShader_fx);
      spaceBackground = AssetManager.Load<Texture2D>(ContentDirectory.Textures.SpaceBackground2_png);
      spaceBackgroundDepth = AssetManager.Load<Texture2D>(ContentDirectory.Textures.result_upscaled_png);


    }

    public override void Draw(GameTime gameTime)
    {
      if (!harvesterEffect.IsLoaded)
        return;

      harvesterEffect.Value.Parameters["view_projection"]?.SetValue(m_camera.GetBoundingFrustum().Matrix);
      harvesterEffect.Value.Parameters["view_matrix"]?.SetValue(m_camera.GetViewMatrix());
      harvesterEffect.Value.Parameters["inv_view_matrix"]?.SetValue(m_camera.GetInverseViewMatrix());

      _simpleEffect.Projection = m_camera.GetBoundingFrustum().Matrix;
      _simpleEffect.View = m_camera.GetViewMatrix();
      _simpleEffect.World = Matrix.Identity;


      backgroundEffect.Value.Parameters["view_projection"]?.SetValue(m_camera.GetBoundingFrustum().Matrix);
      backgroundEffect.Value.Parameters["DepthTexture"].SetValue(spaceBackgroundDepth);

      var p = MouseExtended.GetState().Position.ToVector2();
      p.X = (p.X / (float)_graphicsDevice.Viewport.Width * 2.0f - 1.0f) * -0.02f;
      p.Y = (p.Y / (float)_graphicsDevice.Viewport.Height * 2.0f - 1.0f) * -0.02f;
      backgroundEffect.Value.Parameters["u_mouse"].SetValue(p);

      //_simpleEffect.EmissiveColor = new Vector3(1.0f, 0.0f, 0.0f);
      //_simpleEffect.EnableDefaultLighting();

      // _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.Default, RasterizerState.CullNone, transformMatrix: m_camera.GetViewMatrix());
      //
      //

      _spriteBatch.Begin(effect: backgroundEffect, transformMatrix: m_camera.GetViewMatrix());
      _spriteBatch.Draw(spaceBackground, Vector2.Zero, Color.White);
      _spriteBatch.End();

      _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
        DepthStencilState.Default, RasterizerState.CullNone, effect: harvesterEffect, transformMatrix: m_camera.GetViewMatrix());



      //harvesterEffect.Value.Parameters["grayFactor"]?.SetValue(harve);

      foreach (var entity in ActiveEntities)
      {
        var sprite = _animatedSpriteMapper.Has(entity)
          ? _animatedSpriteMapper.Get(entity)
          : _spriteMapper.Get(entity);
        var transform = _transforMapper.Get(entity);

        if (sprite is AnimatedSprite animatedSprite)
          animatedSprite.Update(gameTime);

        //sprite.Color = Color.White;

        //sprite.Effect |= SpriteEffects.FlipVertically;

        _spriteBatch.Draw(sprite, transform);
      }

      _spriteBatch.End();
    }
  }

  public class RenderGemSystem : EntityDrawSystem
  {
    private readonly SpriteBatch _spriteBatch;
    private readonly GraphicsDevice _graphicsDevice;
    private OrthographicCamera m_camera;

    private ComponentMapper<Sprite> _spriteMapper;
    private ComponentMapper<Transform2> _transforMapper;

    private AsyncAsset<Effect> gemEffect;

    private BasicEffect _simpleEffect;

    public RenderGemSystem(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, OrthographicCamera camera)
      : base(Aspect.All(typeof(Transform2), typeof(Sprite), typeof(Gem)))
    {
      _spriteBatch = spriteBatch;
      _graphicsDevice = graphicsDevice;
      m_camera = camera;

      gemEffect = AssetManager.LoadAsync<Effect>(ContentDirectory.Shaders.GemShader_fx);

      _simpleEffect = new BasicEffect(_graphicsDevice);
      _simpleEffect.TextureEnabled = true;
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
      _transforMapper = mapperService.GetMapper<Transform2>();
      _spriteMapper = mapperService.GetMapper<Sprite>();
    }

    public override void Draw(GameTime gameTime)
    {
      //gemEffect.Parameters["TestColor"].SetValue(new Vector4(1.0f, 0.25f, 0.25f, 1));

      //gemEffect.Parameters["xViewProjection"].SetValue(m_camera.GetViewMatrix());
      //gemEffect.Parameters["g_fTime"].SetValue((float)Math.Sin(gameTime.ElapsedGameTime.TotalSeconds) * 2.5f);
      //var bs = new BasicEffect(_graphicsDevice);
      //var se = new SpriteEffect(_graphicsDevice);

      if (!gemEffect.IsLoaded)
        return;

      gemEffect.Value.Parameters["view_projection"]?.SetValue(m_camera.GetBoundingFrustum().Matrix);
      gemEffect.Value.Parameters["view_matrix"]?.SetValue(m_camera.GetViewMatrix());
      gemEffect.Value.Parameters["inv_view_matrix"]?.SetValue(m_camera.GetInverseViewMatrix());

      //_simpleEffect.Projection = m_camera.GetBoundingFrustum().Matrix;
      //_simpleEffect.View = m_camera.GetViewMatrix();
      //_simpleEffect.World = Matrix.Identity;

      ////_simpleEffect.

      //_simpleEffect.EmissiveColor = new Vector3(1.0f, 0.0f, 0.0f);

      var m = m_camera.GetViewMatrix();
      var m2 = m_camera.GetBoundingFrustum().Matrix;
      //, transformMatrix: m_camera.GetViewMatrix(),


      //_spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.AnisotropicClamp,
      //  DepthStencilState.Default, RasterizerState.CullNone, transformMatrix:m);

      //foreach (var entity in ActiveEntities)
      //{
      //  var sprite = _spriteMapper.Get(entity);
      //  var transform = _transforMapper.Get(entity);

      //  int x = (int)transform.Position.X;
      //  int y = (int)transform.Position.Y;
      //  var w = (sprite.TextureRegion.Texture.Width + 3) * transform.Scale.X;
      //  var h = (sprite.TextureRegion.Texture.Height + 3) * transform.Scale.Y;


      //  //_spriteBatch.Draw(sprite.TextureRegion.Texture, new Rectangle(x, y, (int)w, (int)h), sprite.TextureRegion.Bounds,
      //  //  Color.Black, transform.Rotation,
      //  //  sprite.Origin, sprite.Effect, sprite.Depth);
      //  sprite.Color = Color.Black;
      //  _spriteBatch.Draw(sprite, new Transform2(transform.Position, transform.Rotation, transform.Scale * 1.1f));
      //}

      //_spriteBatch.End();


      //_spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
      //  DepthStencilState.Default, RasterizerState.CullNone/*, transformMatrix: m2*/,
      //  effect: gemEffect);

      _spriteBatch.Begin(blendState: BlendState.AlphaBlend,/*, transformMatrix: m*/ effect: gemEffect);

      foreach (var entity in ActiveEntities)
      {
        var sprite = _spriteMapper.Get(entity);
        var transform = _transforMapper.Get(entity);

        _spriteBatch.Draw(sprite, transform);

        //var view = m_camera.GetViewMatrix();
        //var model = Matrix.Identity;
        //var projection = m_camera.GetBoundingFrustum().Matrix;

        //Matrix.CreateTranslation(new Vector3(transform.Position.X, transform.Position.Y, 1.0f));
        //var mvp = model * view * projection;
        //gemEffect.Value.Parameters["mvp"]?.SetValue(mvp);
        //_spriteBatch.Draw(sprite.TextureRegion.Texture, transform.Position, sprite.TextureRegion.Bounds,
        //  sprite.Color * sprite.Alpha, transform.Rotation,
        //  sprite.Origin, transform.Scale, sprite.Effect, sprite.Depth);

        //_spriteBatch.Draw(sprite.TextureRegion.Texture, transform.Position, sprite.Color * sprite.Alpha);
      }

      _spriteBatch.End();
    }
  }

}
