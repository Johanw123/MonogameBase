using Box2dNet.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;

namespace FrogFight.Physics
{
  internal class PhysicsBody
  {
    public b2BodyId bodyId;
    public b2BodyDef bodyDef;
    public b2ShapeDef shapeDef;
    public b2ShapeId shapeId;

    public Vector2 Position => B2Api.b2Body_GetPosition(bodyId);

    public Vector2 Size;

    public PhysicsBody(Vector2 position, b2WorldId worldId, Vector2 size)
    {
      bodyDef = B2Api.b2DefaultBodyDef();
      bodyDef.type = b2BodyType.b2_dynamicBody;
      bodyDef.position = new(position.X, position.Y);
      bodyDef.enableSleep = false;

      Size = size;

      bodyDef.motionLocks.angularZ = true;

      bodyId = B2Api.b2CreateBody(worldId, bodyDef);
      b2Polygon box = B2Api.b2MakeBox(size.X * 0.5f, size.Y * 0.5f);
      shapeDef = B2Api.b2DefaultShapeDef();
      shapeDef.material.friction = 0.0f;

      shapeId = B2Api.b2CreatePolygonShape(bodyId, shapeDef, box);
    }

    public void SetSize(Vector2 size)
    {
      b2Polygon box = B2Api.b2MakeBox(size.X * 0.5f, size.Y * 0.5f);
      B2Api.b2Shape_SetPolygon(shapeId, box);
    }

    public void Jump()
    {
      B2Api.b2Body_SetLinearVelocity(bodyId, new System.Numerics.Vector2(0, -10));
    }
  }
}
