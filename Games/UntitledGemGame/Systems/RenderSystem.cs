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
using System.Linq;
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
    private readonly SdfLineRenderer _entanglementLineRenderer;
    private OrthographicCamera m_camera;

    private const int MaxEntanglementPulsesPerFrame = 8;

    private ComponentMapper<AnimatedSprite> _animatedSpriteMapper;
    private ComponentMapper<Sprite> _spriteMapper;
    private ComponentMapper<Transform2> _transforMapper;
    private ComponentMapper<Harvester> _harvesterMapper;

    private EffectParameter m_viewProjectionParameter;
    private EffectParameter m_texelSizeParameter;
    private EffectParameter m_outlineColorParameter;
    // private EffectParameter m_deltaTimeParameter;
    private EffectParameter m_totalTimeParameter;

    public RenderSystem(SpriteBatch spriteBatch, ShapeBatch shapeBatch, GraphicsDevice graphicsDevice, OrthographicCamera camera)
: base(Aspect.All(typeof(Transform2)).One(typeof(AnimatedSprite), typeof(Sprite)).Exclude(typeof(Gem)))
    {
      _spriteBatch = spriteBatch;
      _shapeBatch = shapeBatch;
      _graphicsDevice = graphicsDevice;
      _entanglementLineRenderer = new SdfLineRenderer(graphicsDevice, EffectCache.LineSdfFx)
      {
        WobbleAmount = 0.35f,
        ThicknessPulseAmount = 0.2f,
        PulseLengthScale = 90f,
        PulseWidthScale = 10f,
        PulseThicknessBoost = 1.6f,
        BaseGlowSpread = 2.5f,
        PulseGlowSpread = 5f,
        BaseGlowPadding = 10f,
        PulseExtraPadding = 8f
      };
      m_camera = camera;
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
      _transforMapper = mapperService.GetMapper<Transform2>();
      _animatedSpriteMapper = mapperService.GetMapper<AnimatedSprite>();
      _spriteMapper = mapperService.GetMapper<Sprite>();
      _harvesterMapper = mapperService.GetMapper<Harvester>();

      InitEffectParameters();
    }

    private void InitEffectParameters()
    {
      m_viewProjectionParameter = EffectCache.HarvesterEffect.Value.Parameters["view_projection"];

      m_texelSizeParameter = EffectCache.HarvesterEffect.Value.Parameters["TexelSize"];
      m_outlineColorParameter = EffectCache.HarvesterEffect.Value.Parameters["_OutlineColor"];
      // m_deltaTimeParameter = EffectCache.HarvesterEffect.Value.Parameters["_DeltaTime"];
      m_totalTimeParameter = EffectCache.HarvesterEffect.Value.Parameters["_TotalTime"];
    }

    public override void Draw(GameTime gameTime)
    {
      if (EffectCache.HarvesterEffect == null || !EffectCache.HarvesterEffect.IsLoaded)
        return;

      m_viewProjectionParameter?.SetValue(m_camera.GetBoundingFrustum().Matrix);

      float texelWidth = 1f / TextureCache.HarvesterShip.Value.Width;
      float texelHeight = 1f / TextureCache.HarvesterShip.Value.Height;
      m_texelSizeParameter?.SetValue(new Vector2(texelWidth, texelHeight));

      float resonancePulse = 0.5f + 0.5f * MathF.Sin((float)gameTime.TotalGameTime.TotalSeconds * 9.0f);
      Color outlineColor = HarvesterCollectionSystem.ResonanceCascadeActive
        ? Color.Lerp(new Color(60, 255, 220), Color.Gold, resonancePulse)
        : new Color(0.1f, 0.85f, 0.84f, 1.0f);
      m_outlineColorParameter?.SetValue(outlineColor.ToVector4());
      // m_deltaTimeParameter.SetValue((float)gameTime.TotalGameTime.TotalSeconds);
      m_totalTimeParameter?.SetValue((float)gameTime.TotalGameTime.TotalSeconds);

      DrawEntanglementPulses(gameTime);

      _shapeBatch.Begin(m_camera.GetViewMatrix());
      _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
        DepthStencilState.Default, RasterizerState.CullNone, effect: EffectCache.HarvesterEffect, transformMatrix: m_camera.GetViewMatrix());

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
          if(harvester.refuelProgressPercent > 0)
          {
            // animatedSprite.Color *= (float)(harvester.refuelProgressPercent / 100.0f);
            // animatedSprite.Color = new Color(animatedSprite.Color, (float)harvester.refuelProgressPercent / 100.0f);
            animatedSprite.Color = new Color(animatedSprite.Color * ((float)harvester.refuelProgressPercent / 100.0f), 1.0f);
          }
        }

        if (harvester != null && harvester.ReturningToHomebase && UntitledGemGameGameScreen.HomeBasePos != Vector2.Zero)
        {
          // _shapeBatch.DrawLine(harvester.Bounds.Position, harvester.TargetScreenPosition.Value, 0.1f, Color.AliceBlue, Color.White, 1, 1.5f);
          _shapeBatch.FillLine(harvester.BoundingCircle.Center, UntitledGemGameGameScreen.HomeBasePos, 0.1f, new Color(0.2f, 0.1f, 0.9f, 0.4f), 3.0f);
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
          _spriteBatch.Draw(sprite, transform);
          // var rect = new RectangleF(
          //   transform.Position.X,
          //   transform.Position.Y,
          //   sprite.TextureRegion.Width * transform.Scale.X,
          //   sprite.TextureRegion.Height * transform.Scale.Y
          //   );
          // //TODO: outline stops working using this.
          // _shapeBatch.Draw(sprite.TextureRegion.Texture, rect, sprite.Color, transform.Rotation, sprite.Origin);
        }
      }

      foreach (var line in ChainLightningAbility.TargetLines.Values.ToArray())
      {
        //FIXME: Exception once with modified collection
        //Added .ToArray() for fix but its a copy
        if (line != null)
        {
          _shapeBatch.FillLine(line.Start, line.End, line.Thickness, line.ColorStart, 0.6f);
        }
      }

      _spriteBatch.End();
      _shapeBatch.End();
    }

    private void DrawEntanglementPulses(GameTime gameTime)
    {
      if (!UpgradeManager.Instance.UGM.QuantumEntanglement)
        return;

      _entanglementLineRenderer.Begin(
        // SdfLineRenderer draws directly through the graphics device, so it
        // needs the complete world-to-clip matrix rather than the view-only
        // transform normally passed to SpriteBatch.
        m_camera.GetBoundingFrustum().Matrix,
        (float)gameTime.TotalGameTime.TotalSeconds);

      int pulseCount = 0;
      foreach (var entity in ActiveEntities)
      {
        if (pulseCount >= MaxEntanglementPulsesPerFrame)
          break;

        if (!_harvesterMapper.Has(entity))
          continue;

        var harvester = _harvesterMapper.Get(entity);
        if (harvester.EntanglementPulseTimeRemaining <= 0f
          || harvester.EntangledPartnerEntityId < 0)
        {
          continue;
        }

        var partnerEntity = GetEntity(harvester.EntangledPartnerEntityId);
        var partnerHarvester = partnerEntity?.Get<Harvester>();
        var partnerTransform = partnerEntity?.Get<Transform2>();
        if (partnerHarvester == null
          || partnerTransform == null
          || partnerHarvester.EntangledPartnerEntityId != harvester.Id)
        {
          continue;
        }

        float progress = 1f - Math.Clamp(
          harvester.EntanglementPulseTimeRemaining / BaseStats.QuantumEntanglementPulseSeconds,
          0f,
          1f);
        Vector2 sourcePosition = _transforMapper.Get(entity).Position;

        _entanglementLineRenderer.DrawLine(
          sourcePosition,
          partnerTransform.Position,
          0.65f,
          new Color(80, 60, 160, 75),
          new Color(55, 25, 135, 30),
          progress,
          new Color(130, 255, 255));
        ++pulseCount;
      }

      _entanglementLineRenderer.End();
    }
  }

  public class RenderGemSystem : EntityDrawSystem
  {
    private readonly SpriteBatch _spriteBatch;
    private readonly ShapeBatch _shapeBatch;
    private readonly GraphicsDevice _graphicsDevice;
    private OrthographicCamera m_camera;

    private ComponentMapper<Sprite> _spriteMapper;
    private ComponentMapper<Gem> _gemMapper;
    private ComponentMapper<Transform2> _transforMapper;

    private EffectParameter m_viewProjectionParameter;

    private EffectParameter m_texelSizeParameter;
    private EffectParameter m_outlineColorParameter;
    private EffectParameter m_timeParameter;

    public RenderGemSystem(SpriteBatch spriteBatch, ShapeBatch shapeBatch, GraphicsDevice graphicsDevice, OrthographicCamera camera)
      : base(Aspect.All(typeof(Transform2), typeof(Sprite), typeof(Gem)))
    {
      _spriteBatch = spriteBatch;
      _shapeBatch = shapeBatch;
      _graphicsDevice = graphicsDevice;
      m_camera = camera;
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
      _transforMapper = mapperService.GetMapper<Transform2>();
      _spriteMapper = mapperService.GetMapper<Sprite>();
      _gemMapper = mapperService.GetMapper<Gem>();

      InitEffectParameters();
    }

    private void InitEffectParameters()
    {
      m_viewProjectionParameter = EffectCache.GemEffect.Value.Parameters["view_projection"];

      m_texelSizeParameter = EffectCache.GemEffect.Value.Parameters["TexelSize"];
      m_outlineColorParameter = EffectCache.GemEffect.Value.Parameters["_OutlineColor"];
      m_timeParameter = EffectCache.GemEffect.Value.Parameters["_Time"];
    }

    public override void Draw(GameTime gameTime)
    {
      if (!EffectCache.GemEffect.IsLoaded)
        return;

      if (EffectCache.GemEffect.Value == null)
        return;

      m_viewProjectionParameter?.SetValue(m_camera.GetBoundingFrustum().Matrix);

      var texelWidth = 1f / TextureCache.HudRedGem.Value.Width;
      var texelHeight = 1f / TextureCache.HudRedGem.Value.Height;
      m_texelSizeParameter?.SetValue(new Vector2(texelWidth, texelHeight));
      m_outlineColorParameter?.SetValue(new Vector4(1.0f, 1.0f, 1.0f, 1.0f));
      // m_timeParameter.SetValue((float)gameTime.TotalGameTime.TotalSeconds);

      // gemEffect.Value.Parameters["mvp"]?.SetValue(Matrix.Identity * m_camera.GetViewMatrix() * m_camera.GetBoundingFrustum().Matrix);

      //_simpleEffect.EmissiveColor = new Vector3(1.0f, 0.0f, 0.0f);

      var m = m_camera.GetViewMatrix();
      var m2 = m_camera.GetBoundingFrustum().Matrix;
      //, transformMatrix: m_camera.GetViewMatrix(),

      // _shapeBatch.Begin();
      _spriteBatch.Begin(transformMatrix: m, effect: EffectCache.GemEffect, samplerState: SamplerState.LinearClamp);

      var dt = (float)gameTime.GetElapsedSeconds();

      foreach (var entity in ActiveEntities)
      {
        var sprite = _spriteMapper.Get(entity);
        var gem = _gemMapper.Get(entity);
        var transform = _transforMapper.Get(entity);

        _spriteBatch.Draw(sprite, transform);
          // var rect = new RectangleF(
          //   transform.Position.X,
          //   transform.Position.Y,
          //   sprite.TextureRegion.Width * transform.Scale.X,
          //   sprite.TextureRegion.Height * transform.Scale.Y
          //   );
          // //TODO: outline stops working using this.
          // _shapeBatch.Draw(sprite.TextureRegion.Texture, rect, sprite.Color, transform.Rotation, sprite.Origin);
      }

      _spriteBatch.End();
      // _shapeBatch.End();
    }
  }

}
