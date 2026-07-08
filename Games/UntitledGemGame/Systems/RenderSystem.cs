using Apos.Shapes;
using AsyncContent;
using JapeFramework.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using MonoGame.Extended.ECS;
using MonoGame.Extended.ECS.Systems;
using MonoGame.Extended.Graphics;
using MonoGame.Extended.Input;
using System;
using System.Collections.Generic;
using UntitledGemGame.Entities;
using UntitledGemGame.Screens;

namespace UntitledGemGame.Systems
{
  public class LineShape
  {
    public Vector2 Start;
    public Vector2 End;
    public float Thickness;
    public Color ColorStart;
    public Color ColorEnd;

    public LineShape(Vector2 start, Vector2 end, float thickness, Color colorStart, Color colorEnd)
    {
      Start = start;
      End = end;
      Thickness = thickness;
      ColorStart = colorStart;
      ColorEnd = colorEnd;
    }
  }
  public class RenderSystem : EntityDrawSystem
  {
    private readonly SpriteBatch _spriteBatch;
    private readonly ShapeBatch _shapeBatch;
    private readonly GraphicsDevice _graphicsDevice;
    private OrthographicCamera m_camera;

    private ComponentMapper<AnimatedSprite> _animatedSpriteMapper;
    private ComponentMapper<Sprite> _spriteMapper;
    private ComponentMapper<Transform2> _transforMapper;
    private ComponentMapper<Harvester> _harvesterMapper;

    private BasicEffect _simpleEffect;
    // private Effect harvesterEffect;
    // private Effect backgroundEffect;
    // private Texture2D spaceBackground;
    // private Texture2D spaceBackgroundDepth;

    // public static List<LineShape> DebugLines = new List<LineShape>();

    public RenderSystem(SpriteBatch spriteBatch, ShapeBatch shapeBatch, GraphicsDevice graphicsDevice, OrthographicCamera camera)
  : base(Aspect.All(typeof(Transform2)).One(typeof(AnimatedSprite), typeof(Sprite)).Exclude(typeof(Gem)))
    {
      _spriteBatch = spriteBatch;
      _shapeBatch = shapeBatch;
      _graphicsDevice = graphicsDevice;
      m_camera = camera;
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
      _transforMapper = mapperService.GetMapper<Transform2>();
      _animatedSpriteMapper = mapperService.GetMapper<AnimatedSprite>();
      _spriteMapper = mapperService.GetMapper<Sprite>();
      _harvesterMapper = mapperService.GetMapper<Harvester>();

      _simpleEffect = new BasicEffect(_graphicsDevice);
      _simpleEffect.TextureEnabled = true;

      // spaceBackground = TextureCache.SpaceBackground;
      // spaceBackgroundDepth = TextureCache.SpaceBackgroundDepth;

      // harvesterEffect = EffectCache.HarvesterEffect;
      // backgroundEffect = EffectCache.BackgroundEffect;



    }

    public override void Draw(GameTime gameTime)
    {
      // if (!harvesterEffect.IsLoaded)
      //   return;
      //
      // if (!backgroundEffect.IsLoaded)
      //   return;

      if (!EffectCache.HarvesterEffect.IsLoaded)
        return;

      var deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

      EffectCache.HarvesterEffect.Value.Parameters["view_projection"]?.SetValue(m_camera.GetBoundingFrustum().Matrix);
      EffectCache.HarvesterEffect.Value.Parameters["view_matrix"]?.SetValue(m_camera.GetViewMatrix());
      EffectCache.HarvesterEffect.Value.Parameters["inv_view_matrix"]?.SetValue(m_camera.GetInverseViewMatrix());

      float texelWidth = 1f / TextureCache.HarvesterShip.Value.Width;
      float texelHeight = 1f / TextureCache.HarvesterShip.Value.Height;
      EffectCache.HarvesterEffect.Value.Parameters["TexelSize"]?.SetValue(new Vector2(texelWidth, texelHeight));

      EffectCache.HarvesterEffect.Value.Parameters["_OutlineColor"]?.SetValue(new Vector4(0.1f, 0.85f, 0.84f, 1.0f));
      EffectCache.HarvesterEffect.Value.Parameters["_DeltaTime"]?.SetValue((float)gameTime.TotalGameTime.TotalSeconds);


      // EffectCache.HarvesterEffect.Value.Parameters["_OutlineSize"]?.SetValue(1.0f);
      // harvesterEffect.Parameters["_Outline"].SetValue(1.0f);

      var zoom = 2.0f + (m_camera.Zoom * m_camera.Zoom * 0.2f);
      // zoom = 0.3f;
      // Matrix layerMat = Matrix.Invert(Matrix.CreateTranslation(100, 100, 0) * m_camera.GetViewMatrix());
      // Matrix layerMat = Matrix.Invert(Matrix.Invert(Matrix.CreateScale(1, 1, 1)) * m_camera.GetInverseViewMatrix());
      Matrix layerMat = m_camera.GetInverseViewMatrix();
      _simpleEffect.Projection = m_camera.GetBoundingFrustum().Matrix;
      // _simpleEffect.View = m_camera.GetViewMatrix(new Vector2(50, 59));
      _simpleEffect.View = layerMat * Matrix.CreateScale(zoom, zoom, 1.0f);
      _simpleEffect.World = Matrix.Identity;

      // var view_proj = m_camera.GetBoundingFrustum().Matrix * Matrix.CreateScale(zoom, zoom, 1.0f);
      // backgroundEffect.Parameters["view_projection"]?.SetValue(view_proj);
      // backgroundEffect.Parameters["DepthTexture"]?.SetValue(spaceBackgroundDepth);

      // var p = MouseExtended.GetState().Position.ToVector2();
      // p.X = (p.X / (float)_graphicsDevice.Viewport.Width * 2.0f - 1.0f) * -0.02f;
      // p.Y = (p.Y / (float)_graphicsDevice.Viewport.Height * 2.0f - 1.0f) * -0.02f;
      // backgroundEffect.Parameters["u_mouse"].SetValue(p);

      // _shapeBatch.Begin();
      // _shapeBatch.End();
      _shapeBatch.Begin(m_camera.GetViewMatrix());
      _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
        DepthStencilState.Default, RasterizerState.CullNone, effect: EffectCache.HarvesterEffect, transformMatrix: m_camera.GetViewMatrix());
      //harvesterEffect.Value.Parameters["grayFactor"]?.SetValue(harve);



      foreach (var entity in ActiveEntities)
      {
        var animatedSprite = _animatedSpriteMapper.Has(entity)
          ? _animatedSpriteMapper.Get(entity) : null;

        var sprite = _spriteMapper.Has(entity) ? _spriteMapper.Get(entity) : null;

        var transform = _transforMapper.Get(entity);

        if (animatedSprite != null)
          animatedSprite.Update(gameTime);

        bool drawAnimated = true;

        var harvester = _harvesterMapper.Has(entity) ? _harvesterMapper.Get(entity) : null;

        if (harvester != null &&
             harvester.CurrentState != Harvester.HarvesterState.Collecting &&
             harvester.CurrentState != Harvester.HarvesterState.Refueling)
          drawAnimated = false;

        if (harvester != null)
        {
          // EffectCache.HarvesterEffect.Value.Parameters["_OutlineSize"]?.SetValue(
          //     harvester.CurrentState == Harvester.HarvesterState.RequestingFuel ? 1.0f : 0.0f);

        }

        if (harvester != null && harvester.ReturningToHomebase && UntitledGemGameGameScreen.HomeBasePos != Vector2.Zero)
        {
          // _shapeBatch.DrawLine(harvester.Bounds.Position, harvester.TargetScreenPosition.Value, 0.1f, Color.AliceBlue, Color.White, 1, 1.5f);
          _shapeBatch.FillLine(harvester.Shape.BoundingBox.Center, UntitledGemGameGameScreen.HomeBasePos, 0.1f, new Color(0.2f, 0.1f, 0.9f, 0.4f), 3.0f);
        }

        if (animatedSprite != null && drawAnimated)
        {
          _spriteBatch.Draw(animatedSprite, transform);
          // var rect = new RectangleF(
          //   transform.Position.X,
          //   transform.Position.Y,
          //   animatedSprite.TextureRegion.Width * transform.Scale.X,
          //   animatedSprite.TextureRegion.Height * transform.Scale.Y
          //   );
          //
          // _shapeBatch.Draw(animatedSprite.TextureRegion.Texture, rect, animatedSprite.TextureRegion.Bounds, Color.White, transform.Rotation, new Vector2(0.5f,0.5f));
        }
        if (sprite != null)
        {
          // _spriteBatch.Draw(sprite, transform);
          var rect = new RectangleF(
            transform.Position.X,
            transform.Position.Y,
            sprite.TextureRegion.Width * transform.Scale.X,
            sprite.TextureRegion.Height * transform.Scale.Y
            );
          _shapeBatch.Draw(sprite.TextureRegion.Texture, rect, Color.White, transform.Rotation, sprite.Origin);
        }
      }

      foreach (var line in ChainLightningAbility.TargetLines.Values)
      {
        if (line != null)
        {
          _shapeBatch.FillLine(line.Start, line.End, line.Thickness, line.ColorStart, 0.6f);
          // _shapeBatch.Draw(TextureCache.SpaceBackground, new RectangleF(),)
        }
      }

      _spriteBatch.End();
      _shapeBatch.End();




    }
  }

  public class RenderGemSystem : EntityDrawSystem
  {
    private readonly SpriteBatch _spriteBatch;
    private readonly GraphicsDevice _graphicsDevice;
    private OrthographicCamera m_camera;

    private ComponentMapper<Sprite> _spriteMapper;
    private ComponentMapper<Gem> _gemMapper;
    private ComponentMapper<Transform2> _transforMapper;

    private BasicEffect _simpleEffect;

    public RenderGemSystem(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, OrthographicCamera camera)
      : base(Aspect.All(typeof(Transform2), typeof(Sprite), typeof(Gem)))
    {
      _spriteBatch = spriteBatch;
      _graphicsDevice = graphicsDevice;
      m_camera = camera;

      _simpleEffect = new BasicEffect(_graphicsDevice);
      _simpleEffect.TextureEnabled = true;
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
      _transforMapper = mapperService.GetMapper<Transform2>();
      _spriteMapper = mapperService.GetMapper<Sprite>();
      _gemMapper = mapperService.GetMapper<Gem>();
    }

    public override void Draw(GameTime gameTime)
    {
      if (!EffectCache.GemEffect.IsLoaded)
        return;

      if (EffectCache.GemEffect.Value == null)
        return;

      EffectCache.GemEffect.Value.Parameters["view_projection"]?.SetValue(m_camera.GetBoundingFrustum().Matrix);
      EffectCache.GemEffect.Value.Parameters["view_matrix"]?.SetValue(m_camera.GetViewMatrix());
      EffectCache.GemEffect.Value.Parameters["inv_view_matrix"]?.SetValue(m_camera.GetInverseViewMatrix());


      var texelWidth = 1f / TextureCache.HudRedGem.Value.Width;
      var texelHeight = 1f / TextureCache.HudRedGem.Value.Height;
      EffectCache.GemEffect.Value.Parameters["TexelSize"]?.SetValue(new Vector2(texelWidth, texelHeight));
      EffectCache.GemEffect.Value.Parameters["_OutlineColor"]?.SetValue(new Vector4(1.0f, 1.0f, 1.0f, 1.0f));
      EffectCache.GemEffect.Value.Parameters["_Time"]?.SetValue((float)gameTime.TotalGameTime.TotalSeconds);

      // gemEffect.Value.Parameters["mvp"]?.SetValue(Matrix.Identity * m_camera.GetViewMatrix() * m_camera.GetBoundingFrustum().Matrix);

      _simpleEffect.Projection = m_camera.GetBoundingFrustum().Matrix;
      _simpleEffect.View = m_camera.GetViewMatrix();
      _simpleEffect.World = Matrix.Identity;


      // Console.WriteLine(m_camera.Zoom);

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

      _spriteBatch.Begin(transformMatrix: m, effect: EffectCache.GemEffect, samplerState: SamplerState.LinearClamp);

      var dt = (float)gameTime.GetElapsedSeconds();

      foreach (var entity in ActiveEntities)
      {
        var sprite = _spriteMapper.Get(entity);
        var gem = _gemMapper.Get(entity);
        var transform = _transforMapper.Get(entity);

        // var hbPos = UntitledGemGameGameScreen.HomeBasePos;
        // var mag = UpgradeManager.UG.HomebaseMagnetizer;
        //
        // if (mag > 0)
        // {
        //   var dir = hbPos - transform.Position;
        //   dir = Vector2.Normalize(dir);
        //   transform.Position += dir * mag * dt * 100.0f;
        //   gem.BoundsCircle.Center = transform.Position;
        // }

        _spriteBatch.Draw(sprite, transform);
        // _spriteBatch.Draw(sprite, transform.Position, transform.Rotation, transform.Scale);
        // _spriteBatch.Draw(sprite.TextureRegion, transform.Position, sprite.Color * sprite.Alpha,
        //     transform.Rotation, sprite.Origin, transform.Scale, sprite.Effect, sprite.Depth);

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
