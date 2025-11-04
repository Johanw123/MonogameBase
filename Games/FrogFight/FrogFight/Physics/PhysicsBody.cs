using FrogFight.Graphics;
using Microsoft.Xna.Framework;
using nkast.Aether.Physics2D.Dynamics;
using nkast.Aether.Physics2D.Common;

namespace FrogFight.Physics
{
  internal class PhysicsBody
  {
    public Vector2 Size;
    public Body _playerBody;

    public PhysicsBody(Vector2 position, World world, Vector2 size)
    {
      float _playerBodyRadius = 1.5f / 2f; // player diameter is 1.5 meters

      _playerBody = world.CreateBody(position, 0, BodyType.Dynamic);
      Fixture pfixture = //_playerBody.CreateCircle(_playerBodyRadius, 1f);
        _playerBody.CreateRectangle(size.X, size.Y, 1.0f, Vector2.Zero);

      // var tex = TestScene.m_assetCreator.TextureFromShape(pfixture.Shape, MaterialType.Blank, Color.AliceBlue, 1f);
      // _playerBody.Tag = tex;

      _playerBody.BodyType = BodyType.Dynamic;
      //_playerBody.LinearDamping = 100f;

      _playerBody.FixedRotation = true;

      // Give it some bounce and friction
      pfixture.Restitution = 0.0f;
      pfixture.Friction = 0.1f;
    }

    public void SetSize(Vector2 size)
    {
      //b2Polygon box = B2Api.b2MakeBox(size.X * 0.5f, size.Y * 0.5f);
      //B2Api.b2Shape_SetPolygon(shapeId, box);
    }

    public void Jump()
    {
      _playerBody.ApplyLinearImpulse(new Vector2(0, 0.5f));
    }
  }
}
