using Assimp;
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

    private string m_animation;
    private bool m_licking;
    private bool m_jumping;

    private bool wasLeft;
    private bool wasRight;
    private bool wasJump;
    private bool wasLick;

    public override void Process(GameTime gameTime, int entityId)
    {
      var player = _playerMapper.Get(entityId);
      var sprite = _spriteMapper.Get(entityId);
      var transform = _transformMapper.Get(entityId);
      var body = _bodyMapper.Get(entityId);
      var keyboardState = KeyboardExtended.GetState();

      var gameState = TestScene.m_gameState;

      if (TestScene.gameStateLoaded)
      {
        TestScene.gameStateLoaded = false;
        Console.WriteLine("Up");
        body._playerBody.Position = gameState.PlayerEntities[player.PlayerNumber - 1].Position;
        body._playerBody.LinearVelocity = gameState.PlayerEntities[player.PlayerNumber - 1].Velocity;
      }
      //body._playerBody.Position = gameState.PlayerEntities[player.PlayerNumber - 1].Position;
      //body._playerBody.LinearVelocity = gameState.PlayerEntities[player.PlayerNumber - 1].Velocity;

      //gameState.PlayerEntities[0].

      bool wasGrounded = player.IsGrounded;
      player.IsGrounded = false;

      string curAnimation = m_animation;

      ContactEdge e = body._playerBody.ContactList;

      while (e != null)
      {
        var bt = e.Other.BodyType;

        if (bt == BodyType.Static && e.Other != body._playerBody)
        {
          player.IsGrounded = true;
          break;
        }

        e = e.Next;
      }

      //Console.WriteLine(m_grounded);

      var inputs = TestScene.GlobalInputs;
      var chunks = inputs.Chunk(4).ToArray();

      var pInput = chunks[player.PlayerNumber - 1];

      bool left = pInput[0] == 1;
      bool right = pInput[1] == 1;
      bool jump = pInput[2] == 1;
      bool lick = pInput[3] == 1;

      var jumpPressed = jump && !wasJump;
      var lickPressed = lick && !wasLick;

      if (TestScene.singlePlayerTest)
      {
        left = keyboardState.IsKeyDown(Keys.Left);
        right = keyboardState.IsKeyDown(Keys.Right);

        jumpPressed = keyboardState.WasKeyPressed(Keys.Up);
        lickPressed = keyboardState.WasKeyPressed(Keys.Space);
      }

      transform.Position = new Vector2(body._playerBody.Position.X, body._playerBody.Position.Y) * 24.0f; //TODO: 24 is PTM value

      if (left)
        body._playerBody.ApplyForce(new Vector2(-2, 0));

      if (right)
        body._playerBody.ApplyForce(new Vector2(2, 0));

      if (body._playerBody.LinearVelocity.X > 3)
        body._playerBody.LinearVelocity = new Vector2(3, body._playerBody.LinearVelocity.Y);
      if (body._playerBody.LinearVelocity.X < -3)
        body._playerBody.LinearVelocity = new Vector2(-3, body._playerBody.LinearVelocity.Y);



      //Left, Right, Jump, Lick

      if (player.PlayerNumber > 2)
        return;

      if (player.PlayerNumber < 1)
        return;


      if (body._playerBody.LinearVelocity.X < 0)
      {
        //player.Facing = Facing.Right;
        transform.Scale = new Vector2(-1, 1);
      }

      if (body._playerBody.LinearVelocity.X > 0)
      {
        //player.Facing = Facing.Left;
        transform.Scale = new Vector2(1, 1);
      }

      if (jumpPressed && player.IsGrounded)
      {
        Console.WriteLine($"Player ({player.PlayerNumber}) is pressing a button!");

        //curAnimation = ContentDirectory.Textures.Game.Frog.frog_animations_Tags.Hop;

        sprite.SetAnimation(ContentDirectory.Textures.Game.Frog.frog_animations_Tags.Hop);
        body.Jump();
        //m_jumping = true;
        player.IsGrounded = false;
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

      if (lickPressed && !m_licking)
      {
        m_licking = true;

        sprite.SetAnimation(ContentDirectory.Textures.Game.Frog.frog_animations_Tags.Lick);
        //a.OnAnimationEvent += (s, e) =>
        //{
        //  if (e == AnimationEventTrigger.AnimationCompleted)
        //  {
        //    m_licking = false;
        //  }
        //};

        TimerHelper.AbortActions("lick");
        TimerHelper.DoAfter(() =>
        {
          m_licking = false;
        }, 400, "lick",false);
      }

      if (!sprite.Controller.IsAnimating)
      {
        if (!player.IsGrounded)
        {
          //sprite.SetAnimation(ContentDirectory.Textures.Game.Frog.frog_animations_Tags.Fall);
          sprite.SetAnimation(ContentDirectory.Textures.Game.Frog.frog_animations_Tags.Idle);
        }
        else
        {
          sprite.SetAnimation(ContentDirectory.Textures.Game.Frog.frog_animations_Tags.Idle);
        }
      }

      wasLeft = left;
      wasRight = right;
      wasJump = jump;
      wasLick = lick;
    }
  }
}
