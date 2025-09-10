using FrogFight.Components;
using FrogFight.Physics;
using FrogFight.Scenes;
using GUI.Shared.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using MonoGame.Extended.Animations;
using MonoGame.Extended.ECS;
using MonoGame.Extended.ECS.Systems;
using MonoGame.Extended.Graphics;
using MonoGame.Extended.Input;
using nkast.Aether.Physics2D.Dynamics;
using nkast.Aether.Physics2D.Dynamics.Contacts;
using System;
using System.Linq;
using Convert = JapeFramework.Helpers.Convert;

namespace FrogFight.Systems
{
  public class PlayerSystem : EntityProcessingSystem
  {
    private ComponentMapper<Player> _playerMapper;
    private ComponentMapper<AnimatedSprite> _spriteMapper;
    private ComponentMapper<Transform2> _transformMapper;
    private ComponentMapper<PhysicsBody> _bodyMapper;

    public PlayerSystem()
        : base(Aspect.All(typeof(PhysicsBody), typeof(Player), typeof(Transform2), typeof(AnimatedSprite)))
    {
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
      _playerMapper = mapperService.GetMapper<Player>();
      _spriteMapper = mapperService.GetMapper<AnimatedSprite>();
      _transformMapper = mapperService.GetMapper<Transform2>();
      _bodyMapper = mapperService.GetMapper<PhysicsBody>();
    }


    private bool m_grounded;
    private string m_animation;
    private bool m_licking;
    private bool m_jumping;

    public override void Process(GameTime gameTime, int entityId)
    {
      var player = _playerMapper.Get(entityId);
      var sprite = _spriteMapper.Get(entityId);
      var transform = _transformMapper.Get(entityId);
      var body = _bodyMapper.Get(entityId);
      var keyboardState = KeyboardExtended.GetState();

      bool wasGrounded = m_grounded;
      m_grounded = false;

      string curAnimation = m_animation;

      ContactEdge e = body._playerBody.ContactList;

      while (e != null)
      {
        var bt = e.Other.BodyType;

        if (bt == BodyType.Static && e.Other != body._playerBody)
        {
          m_grounded = true;
          break;
        }

        e = e.Next;
      }

      //Console.WriteLine(m_grounded);

      transform.Position = new Vector2(body._playerBody.Position.X, body._playerBody.Position.Y) * 24.0f; //TODO: 24 is PTM value

      if (keyboardState.IsKeyDown(Keys.Left))
        body._playerBody.ApplyForce(new Vector2(-1, 0));

      if (keyboardState.IsKeyDown(Keys.Right))
        body._playerBody.ApplyForce(new Vector2(1, 0));

      if (body._playerBody.LinearVelocity.X > 2)
        body._playerBody.LinearVelocity = new Vector2(2, body._playerBody.LinearVelocity.Y);
      if (body._playerBody.LinearVelocity.X < -2)
        body._playerBody.LinearVelocity = new Vector2(-2, body._playerBody.LinearVelocity.Y);

      var inputs = TestScene.GlobalInputs;
      var chunks = inputs.Chunk(4).ToArray();

      if (player.PlayerNumber > 2)
        return;

      if (player.PlayerNumber < 1)
        return;


      if (body._playerBody.LinearVelocity.X < 0)
      {
        player.Facing = Facing.Right;
        sprite.Effect = SpriteEffects.FlipHorizontally;


        //sprite.Effect |= SpriteEffects.FlipVertically;
        transform.Rotation = Convert.DegreesToRadians(0);
      }

      if (body._playerBody.LinearVelocity.X > 0)
      {
        player.Facing = Facing.Left;
        sprite.Effect = SpriteEffects.None;

        sprite.Effect |= SpriteEffects.FlipVertically;
        transform.Rotation = Convert.DegreesToRadians(180);
      }

      var pInput = chunks[player.PlayerNumber - 1];

      if (pInput[0] != 0 || keyboardState.WasKeyPressed(Keys.Up) && m_grounded)
      {
        Console.WriteLine($"Player ({player.PlayerNumber}) is pressing a button!");

        //curAnimation = ContentDirectory.Textures.Game.Frog.frog_animations_Tags.Hop;

        sprite.SetAnimation(ContentDirectory.Textures.Game.Frog.frog_animations_Tags.Hop);
        body.Jump();
        //m_jumping = true;
        m_grounded = false;
        //if (m_animation != "hop")
        //{
        //  //sprite.SetAnimation("hop").OnAnimationEvent += (s, e) =>
        //  //{
        //  //  if (e == AnimationEventTrigger.AnimationCompleted)
        //  //  {
        //  //    //player.State = State.Idle;
        //  //    sprite.SetAnimation(ContentDirectory.Textures.Game.Frog.frog_animations_Tags.Idle);
        //  //  }
        //  //};


        //}

      }
      else
      {
        //sprite.SetAnimation("idle");
        //player.State = State.Idle;
      }

      if (keyboardState.WasKeyPressed(Keys.Space) && !m_licking)
      {
        m_licking = true;

        var a = sprite.SetAnimation(ContentDirectory.Textures.Game.Frog.frog_animations_Tags.Lick);
        a.OnAnimationEvent += (s, e) =>
        {
          if (e == AnimationEventTrigger.AnimationCompleted)
          {
            m_licking = false;
          }
        };

        TimerHelper.DoAfter(() =>
        {
          m_licking = false;
        }, 1000, false);
      }

      if (!sprite.Controller.IsAnimating)
      {
        if (!m_grounded)
        {
          //sprite.SetAnimation(ContentDirectory.Textures.Game.Frog.frog_animations_Tags.Fall);
          sprite.SetAnimation(ContentDirectory.Textures.Game.Frog.frog_animations_Tags.Idle);
        }
        else
        {
          sprite.SetAnimation(ContentDirectory.Textures.Game.Frog.frog_animations_Tags.Idle);
        }
      }

      //if (keyboardState.WasKeyPressed(Keys.Space) && !m_licking)
      //{
      //  m_licking = true;
      //  curAnimation = ContentDirectory.Textures.Game.Frog.frog_animations_Tags.Lick;
      //}

      //if (!m_licking && m_grounded && !m_jumping)
      //{
      //  curAnimation = ContentDirectory.Textures.Game.Frog.frog_animations_Tags.Idle;
      //}

      //if (m_animation != curAnimation)
      //{
      //  var a = sprite.SetAnimation(curAnimation);
        
      //  a.OnAnimationEvent += (s, e) =>
      //  {
      //    if (e == AnimationEventTrigger.AnimationCompleted)
      //    {
      //      if (curAnimation == ContentDirectory.Textures.Game.Frog.frog_animations_Tags.Lick)
      //      {
      //        m_licking = false;
      //      }
      //      else if (curAnimation == ContentDirectory.Textures.Game.Frog.frog_animations_Tags.Hop)
      //      {
      //        m_jumping = false;
      //      }
      //    }
      //  };

      //  m_animation = curAnimation;
      //}


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
