using Base;
using FrogFight.Physics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using MonoGame.Extended.ECS;
using MonoGame.Extended.ECS.Systems;
using MonoGame.Extended.Graphics;
using OrthographicCamera = FrogFight.Graphics.OrthographicCamera;

namespace FrogFight.Systems
{
  public class DebugPhysicsRenderSystem(
    SpriteBatch spriteBatch,
    OrthographicCamera camera,
    nkast.Aether.Physics2D.Dynamics.World world,
    GraphicsDevice graphicsDevice)
    : EntityDrawSystem(Aspect.All(typeof(Transform2)).One(typeof(AnimatedSprite), typeof(Sprite), typeof(PhysicsBody)))
  {
    private ComponentMapper<AnimatedSprite> _animatedSpriteMapper;
    private ComponentMapper<Sprite> _spriteMapper;
    private ComponentMapper<Transform2> _transforMapper;
    private ComponentMapper<PhysicsBody> _bodyMapper;

    private BasicEffect _simpleEffect;

    public override void Initialize(IComponentMapperService mapperService)
    {
      _transforMapper = mapperService.GetMapper<Transform2>();
      _animatedSpriteMapper = mapperService.GetMapper<AnimatedSprite>();
      _spriteMapper = mapperService.GetMapper<Sprite>();
      _bodyMapper = mapperService.GetMapper<PhysicsBody>();

      _simpleEffect = new BasicEffect(graphicsDevice);
      _simpleEffect.TextureEnabled = true;
      _simpleEffect.Alpha = 0.3f;
      _simpleEffect.DiffuseColor = new Vector3(1, 0, 0);
    }

    public override void Draw(GameTime gameTime)
    {
      var old = camera.ViewHeight;

      camera.ViewHeight /= 24f;

      _simpleEffect.View = camera.GetViewTransform();
      _simpleEffect.Projection = camera.GetProjectionTransform(new Graphics.Viewport(graphicsDevice.Viewport.Width, graphicsDevice.Viewport.Height));

      //_simpleEffect.View = Matrix.CreateLookAt(_cameraPosition, _cameraPosition + Microsoft.Xna.Framework.Vector3.Forward, Microsoft.Xna.Framework.Vector3.Up);
      //_simpleEffect.Projection = Matrix.CreateOrthographic(cameraViewWidth, cameraViewWidth / vp.AspectRatio, 0f, -1f);

      //m_spriteBatch.Begin(blendState:BlendState.Additive, effect: _simpleEffect);

      spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.Default, RasterizerState.CullClockwise, _simpleEffect);

      //m_spriteBatch.Begin();

      //Render physics boxes, TODO: Move to an ECS system
      foreach (var body in world.BodyList)
      {
        foreach (var fixture in body.FixtureList)
        {
          fixture.GetAABB(out var aabb, 0);

          if (body.Tag is not Texture2D tex)
            continue;

          var texSize = new Vector2(tex.Width, tex.Height);
          var texSizeOrigin = texSize / 2f;

          var apa = new Vector2(aabb.Width, aabb.Height) / new Vector2(tex.Width, tex.Height);
          spriteBatch.Draw(tex, body.Position, null, Microsoft.Xna.Framework.Color.White, body.Rotation, texSizeOrigin, apa, SpriteEffects.FlipVertically, 0f);
        }
      }
      spriteBatch.End();
      camera.ViewHeight = old;
    }
  }
}
