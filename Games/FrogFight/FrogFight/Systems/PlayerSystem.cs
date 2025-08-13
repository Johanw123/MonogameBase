using FlatRedBall.Glue.StateInterpolation;
using FrogFight.Collisions;
using FrogFight.Components;
using FrogFight.Scenes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using MonoGame.Extended.Animations;
using MonoGame.Extended.ECS;
using MonoGame.Extended.ECS.Systems;
using MonoGame.Extended.Graphics;
using MonoGame.Extended.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrogFight.Systems
{
  public class PlayerSystem : EntityProcessingSystem
  {
    private ComponentMapper<Player> _playerMapper;
    private ComponentMapper<AnimatedSprite> _spriteMapper;
    private ComponentMapper<Transform2> _transformMapper;
    private ComponentMapper<Body> _bodyMapper;

    public PlayerSystem()
        : base(Aspect.All(typeof(Body), typeof(Player), typeof(Transform2), typeof(AnimatedSprite)))
    {
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
      _playerMapper = mapperService.GetMapper<Player>();
      _spriteMapper = mapperService.GetMapper<AnimatedSprite>();
      _transformMapper = mapperService.GetMapper<Transform2>();
      _bodyMapper = mapperService.GetMapper<Body>();
    }

    public override void Process(GameTime gameTime, int entityId)
    {
      var player = _playerMapper.Get(entityId);
      var sprite = _spriteMapper.Get(entityId);
      var transform = _transformMapper.Get(entityId);
      var body = _bodyMapper.Get(entityId);
      //var keyboardState = KeyboardExtended.GetState();

      var inputs = TestScene.GlobalInputs;
      var chunks = inputs.Chunk(4).ToArray();

      var pInput = chunks[player.PlayerNumber - 1];

      if (pInput[0] != 0)
      {
        Console.WriteLine($"Player ({player.PlayerNumber}) is pressing a button!");

        if (sprite.CurrentAnimation != "hop")
          sprite.SetAnimation("hop").OnAnimationEvent += (s, e) =>
        {
          if (e == AnimationEventTrigger.AnimationCompleted)
          {
            //player.State = State.Idle;
            sprite.SetAnimation("idle");
          }
        };
      }
      else
      {
        //sprite.SetAnimation("idle");
        //player.State = State.Idle;
      }

      //body.Position = new Vector2(100, player.);

      //for (int i = 0; i < 2; i++)
      //{
      //  if (player.PlayerNumber == i)
      //  {
      //    var input = chunks[i];

      //    if (input[0] != 0)
      //    {
      //      //Console.WriteLine("Button Down: " + player.PlayerNumber);
      //     // sprite.SetAnimation("jump");
      //    }
      //    else
      //    {
      //      //sprite.SetAnimation("idle");
      //    }
      //  }
      //}

      // var players = FindComponentsOfType<IPlayer>().OrderBy(player => player.PlayerNumber).ToArray();
      // var chunks = inputs.Chunk(InputSize).ToArray();
      //
      // for (var i = 0; i < players.Length; i++)
      // {
      //   var player = players[i];
      //
      //   //TODO check disconnects
      //   player.Update(chunks[i]);
      // }



      //if (player.CanJump)
      //{
      //  if (keyboardState.WasKeyPressed(Keys.Up))
      //    body.Velocity.Y -= 550 + Math.Abs(body.Velocity.X) * 0.4f;

      //  if (keyboardState.WasKeyPressed(Keys.Z))
      //  {
      //    body.Velocity.Y -= 550 + Math.Abs(body.Velocity.X) * 0.4f;
      //    player.State = player.State == State.Idle ? State.Punching : State.Kicking;
      //  }
      //}

      //if (keyboardState.IsKeyDown(Keys.Right))
      //{
      //  body.Velocity.X += 150;
      //  player.Facing = Facing.Right;
      //}

      //if (keyboardState.IsKeyDown(Keys.Left))
      //{
      //  body.Velocity.X -= 150;
      //  player.Facing = Facing.Left;
      //}

      //if (!player.IsAttacking)
      //{
      //  if (body.Velocity.X > 0 || body.Velocity.X < 0)
      //    player.State = State.Walking;

      //  if (body.Velocity.Y < 0)
      //    player.State = State.Jumping;

      //  if (body.Velocity.Y > 0)
      //    player.State = State.Falling;

      //  if (body.Velocity.EqualsWithTolerence(Vector2.Zero, 5))
      //    player.State = State.Idle;
      //}

      //if (keyboardState.IsKeyDown(Keys.Down))
      //  player.State = State.Cool;

      //switch (player.State)
      //{
      //  case State.Jumping:
      //    if (sprite.CurrentAnimation != "jump")
      //      sprite.SetAnimation("jump");
      //    break;
      //  case State.Walking:
      //    if (sprite.CurrentAnimation != "walk")
      //      sprite.SetAnimation("walk");
      //    sprite.Effect = player.Facing == Facing.Right ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
      //    break;
      //  case State.Falling:
      //    if (sprite.CurrentAnimation != "fall")
      //      sprite.SetAnimation("fall");
      //    break;
      //  case State.Idle:
      //    if (sprite.CurrentAnimation != "idle")
      //      sprite.SetAnimation("idle");
      //    break;
      //  case State.Kicking:
      //    if (sprite.CurrentAnimation != "kick")
      //      sprite.SetAnimation("kick").OnAnimationEvent += (s, e) =>
      //      {
      //        if (e == AnimationEventTrigger.AnimationCompleted)
      //        {
      //          player.State = State.Idle;
      //        }
      //      };
      //    break;
      //  case State.Punching:
      //    if (sprite.CurrentAnimation != "punch")
      //      sprite.SetAnimation("punch").OnAnimationEvent += (s, e) =>
      //      {
      //        if (e == AnimationEventTrigger.AnimationCompleted)
      //        {
      //          player.State = State.Idle;
      //        }
      //      };
      //    break;
      //  case State.Cool:
      //    if (sprite.CurrentAnimation != "cool")
      //      sprite.SetAnimation("cool");
      //    break;
      //  default:
      //    throw new ArgumentOutOfRangeException();
      //}

      //body.Velocity.X *= 0.7f;
    }
  }
}
