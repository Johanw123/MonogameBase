using AsyncContent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using MonoGame.Extended.ECS;
using MonoGame.Extended.ECS.Systems;
using MonoGame.Extended.Graphics;
using System;
using UntitledGemGame.Entities;
using static Assimp.Metadata;

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
    }

    public override void Draw(GameTime gameTime)
    {
      // _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.Default, RasterizerState.CullNone, transformMatrix: m_camera.GetViewMatrix());
      _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
        DepthStencilState.Default, RasterizerState.CullNone, transformMatrix: m_camera.GetViewMatrix());

      foreach (var entity in ActiveEntities)
      {
        var sprite = _animatedSpriteMapper.Has(entity)
          ? _animatedSpriteMapper.Get(entity)
          : _spriteMapper.Get(entity);
        var transform = _transforMapper.Get(entity);

        if (sprite is AnimatedSprite animatedSprite)
          animatedSprite.Update(gameTime);

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

    public RenderGemSystem(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, OrthographicCamera camera)
      : base(Aspect.All(typeof(Transform2), typeof(Sprite), typeof(Gem)))
    {
      _spriteBatch = spriteBatch;
      _graphicsDevice = graphicsDevice;
      m_camera = camera;

       // < Effect > (ContentDirectory.Shaders.GemShader_fx);
       gemEffect = AssetManager.LoadAsync<Effect>(ContentDirectory.Shaders.GemShader_fx);
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

      if (!gemEffect.IsLoaded)
        return;

      gemEffect.Value.Parameters["view_projection"].SetValue(m_camera.GetBoundingFrustum().Matrix);

      _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
        DepthStencilState.Default, RasterizerState.CullNone, transformMatrix: m_camera.GetViewMatrix(),
        effect: gemEffect);

      foreach (var entity in ActiveEntities)
      {
        var sprite = _spriteMapper.Get(entity);
        var transform = _transforMapper.Get(entity);

        _spriteBatch.Draw(sprite, transform);

        _spriteBatch.Draw(sprite.TextureRegion.Texture, transform.Position, sprite.TextureRegion.Bounds,
          sprite.Color * sprite.Alpha, transform.Rotation,
          sprite.Origin, transform.Scale, sprite.Effect, sprite.Depth);
      }

      _spriteBatch.End();
    }
  }

}
