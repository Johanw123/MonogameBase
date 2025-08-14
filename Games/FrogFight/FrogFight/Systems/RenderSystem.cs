using Base;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using MonoGame.Extended.ECS;
using MonoGame.Extended.ECS.Systems;
using MonoGame.Extended.Graphics;
using OrthographicCamera = FrogFight.Graphics.OrthographicCamera;

namespace FrogFight.Systems
{
  public class RenderSystem : EntityDrawSystem
  {
    private readonly SpriteBatch _spriteBatch;
    private readonly OrthographicCamera _camera;
    private readonly GraphicsDevice _graphicsDevice;

    private ComponentMapper<AnimatedSprite> _animatedSpriteMapper;
    private ComponentMapper<Sprite> _spriteMapper;
    private ComponentMapper<Transform2> _transforMapper;

    private BasicEffect _simpleEffect;

    public RenderSystem(SpriteBatch spriteBatch, OrthographicCamera camera, GraphicsDevice graphicsDevice)
      : base(Aspect.All(typeof(Transform2)).One(typeof(AnimatedSprite), typeof(Sprite)))
    {
      _spriteBatch = spriteBatch;
      _camera = camera;
      _graphicsDevice = graphicsDevice;
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
      _simpleEffect.View = _camera.GetViewTransform();
      _simpleEffect.Projection = _camera.GetProjectionTransform(new Graphics.Viewport(_graphicsDevice.Viewport.Width, _graphicsDevice.Viewport.Height));

      //_spriteBatch.Begin(samplerState: SamplerState.PointClamp/*, transformMatrix: _camera.GetViewTransform()*/ ,effect: _simpleEffect);

      _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.Default, RasterizerState.CullClockwise, _simpleEffect);

      foreach (var entity in ActiveEntities)
      {
        var sprite = _animatedSpriteMapper.Has(entity)
          ? _animatedSpriteMapper.Get(entity)
          : _spriteMapper.Get(entity);
        var transform = _transforMapper.Get(entity);

        if (sprite is AnimatedSprite animatedSprite)
          animatedSprite.Update(gameTime);

        //transform.Scale = Vector2.One;

        Texture2D texture = sprite.TextureRegion.Texture;
        var texSize = new Vector2(texture.Width, texture.Height);
        var texSizeOrigin = texSize / 2f;

        sprite.Effect = SpriteEffects.FlipVertically;

        //var apa = new Vector2(aabb.Width, aabb.Height) / new Vector2(texture.Width, texture.Height);
        //_spriteBatch.Draw(texture, transform.Position, null, Microsoft.Xna.Framework.Color.White, 0.0f, texSizeOrigin, 0.1f, SpriteEffects.FlipVertically, 0f);
        _spriteBatch.Draw(sprite, transform);
      }

      _spriteBatch.End();
    }
  }
}
