using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using MonoGame.Extended.ECS;
using MonoGame.Extended.ECS.Systems;
using MonoGame.Extended.Graphics;

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
      : base(Aspect.All(typeof(Transform2)).One(typeof(AnimatedSprite), typeof(Sprite)))
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
      _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.Default, RasterizerState.CullNone, transformMatrix: m_camera.GetViewMatrix());

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
}
