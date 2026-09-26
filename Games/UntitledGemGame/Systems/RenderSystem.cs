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
    private readonly Transform2 _collectorDrawTransform = new Transform2();
    private readonly ShapeBatch _shapeBatch;
    private readonly GraphicsDevice _graphicsDevice;
    private readonly SdfLineRenderer _entanglementLineRenderer;
    private OrthographicCamera m_camera;

    private const int MaxEntanglementPulsesPerFrame = 8;
    private static readonly Vector2[] FinalSweepRingPoints = CreateFinalSweepRingPoints();

    private static Vector2[] CreateFinalSweepRingPoints()
    {
      // Keep the same 32 segments, but calculate their angles only once.
      var points = new Vector2[33];
      points[0] = Vector2.UnitX;
      for (int segment = 1; segment < points.Length; segment++)
      {
        float angle = segment * MathHelper.TwoPi / 32f;
        points[segment] = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
      }
      return points;
    }

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

      // Exponential lerp keeps resizing consistent across frame rates (~95% in 0.3 seconds).
      float sizeBlend = 1f - MathF.Exp(-10f * Math.Max(0f, (float)gameTime.ElapsedGameTime.TotalSeconds));
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

        if (harvester != null && harvester.Type != Harvester.HarvesterType.Drone
          && harvester.ReturningToHomebase && UntitledGemGameGameScreen.HomeBasePos != Vector2.Zero)
        {
          // _shapeBatch.DrawLine(harvester.Bounds.Position, harvester.TargetScreenPosition.Value, 0.1f, Color.AliceBlue, Color.White, 1, 1.5f);
          _shapeBatch.FillLine(harvester.BoundingCircle.Center, UntitledGemGameGameScreen.HomeBasePos, 0.1f, new Color(0.2f, 0.1f, 0.9f, 0.4f), 3.0f);
        }

        if (harvester != null && harvester.TractorFlashRemaining > 0f)
          _shapeBatch.FillLine(harvester.TractorOrigin, harvester.TractorTarget, 0.1f,
            Color.Cyan * (harvester.TractorFlashRemaining / 0.25f), 2f);
        if (harvester != null && harvester.WakeFlashRemaining > 0f)
          _shapeBatch.FillLine(harvester.WakeStart, harvester.WakeEnd, 0.1f,
            Color.LightCyan * 0.25f, ModuleCatalog.WakeRadius * 2f);

        if (harvester != null && harvester.OverdriveTimeRemaining > 0f)
          _shapeBatch.FillLine(transform.Position, transform.Position - new Vector2(
            MathF.Cos(transform.Rotation - MathHelper.PiOver2), MathF.Sin(transform.Rotation - MathHelper.PiOver2)) * 32f, 0.1f,
            Color.Orange * (0.6f * harvester.OverdriveTimeRemaining / ModuleCatalog.OverdriveDuration), 5f);

        if (harvester != null && harvester.StormArcRemaining > 0f)
          for (int arc = 1; arc < harvester.StormArcCount; arc++)
            _shapeBatch.FillLine(harvester.StormArcPoints[arc - 1], harvester.StormArcPoints[arc], 0.1f,
              Color.LightSkyBlue * (harvester.StormArcRemaining / ModuleCatalog.PulseDuration), 3f);

        if (harvester != null && harvester.RelayFlashRemaining > 0f)
          _shapeBatch.FillLine(harvester.RelayOrigin, UntitledGemGameGameScreen.HomeBasePos, 0.1f,
            Color.Gold * (harvester.RelayFlashRemaining / ModuleCatalog.PulseDuration), 3f);

        if (harvester != null && harvester.ModulePulseRemaining > 0f)
        {
          float progress = 1f - harvester.ModulePulseRemaining / ModuleCatalog.PulseDuration;
          bool collapsing = harvester.ModulePulseColor == Color.MediumPurple;
          float radius = harvester.ModulePulseRadius * (collapsing ? 1f - progress : MathHelper.Lerp(0.1f, 1f, progress));
          var color = harvester.ModulePulseColor * (0.8f * (1f - progress));
          var previous = harvester.ModulePulsePosition + FinalSweepRingPoints[0] * radius;
          for (int segment = 1; segment < FinalSweepRingPoints.Length; segment++)
          {
            var next = harvester.ModulePulsePosition + FinalSweepRingPoints[segment] * radius;
            _shapeBatch.FillLine(previous, next, 0.1f, color, 3f);
            previous = next;
          }
        }

        if (harvester != null && harvester.FinalSweepTimeRemaining > 0f)
        {
          float progress = 1f - harvester.FinalSweepTimeRemaining / BaseStats.DroneFinalSweepDurationSeconds;
          float radius = harvester.FinalSweepRadius * MathHelper.Lerp(0.25f, 1f, progress);
          var color = Color.LightCyan * (0.5f * (1f - progress));
          var previous = harvester.FinalSweepPosition + FinalSweepRingPoints[0] * radius;
          for (int segment = 1; segment < FinalSweepRingPoints.Length; segment++)
          {
            var next = harvester.FinalSweepPosition + FinalSweepRingPoints[segment] * radius;
            _shapeBatch.FillLine(previous, next, 0.1f, color, 1.5f);
            previous = next;
          }
        }

        if (harvester != null && harvester.WarpDriveFlashTimeRemaining > 0f)
        {
          float progress = 1f - harvester.WarpDriveFlashTimeRemaining / BaseStats.WarpDriveFlashDurationSeconds;
          float radius = Math.Max(20f, BaseStats.GetHarvesterBaseCollectionRange(harvester) * 2f);
          DrawWarpFlash(harvester.WarpDriveDeparturePosition, radius, progress, arriving: false);
          DrawWarpFlash(harvester.WarpDriveArrivalPosition, radius, progress, arriving: true);
        }

        // Share the pickup multiplier with both hull and engines, without changing
        // the entity scale used to calculate the base collection radius.
        var drawTransform = transform;
        if (harvester != null)
        {
          float targetSize = BaseStats.GetHarvesterCollectionRangeMultiplier(harvester);
          float visualSize = harvester.VisualCollectionRangeMultiplier.HasValue
            ? MathHelper.Lerp(harvester.VisualCollectionRangeMultiplier.Value, targetSize, sizeBlend)
            : targetSize;
          harvester.VisualCollectionRangeMultiplier = visualSize;
          _collectorDrawTransform.Position = transform.Position;
          _collectorDrawTransform.Rotation = transform.Rotation;
          _collectorDrawTransform.Scale = transform.Scale * visualSize;
          drawTransform = _collectorDrawTransform;
        }

        if (animatedSprite != null && drawAnimated)
        {
          _spriteBatch.Draw(animatedSprite, drawTransform);
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
          _spriteBatch.Draw(sprite, drawTransform);
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

      // Convert display pixels to world units, including camera zoom and the
      // final downscale from the virtual render target to the window.
      // var worldToScreen = m_camera.GetViewMatrix()
      //   * JapeFramework.BaseGame.BoxingViewportAdapter.GetScaleMatrix();
      // float pixelsPerWorldUnit = Math.Max(0.0001f,
      //   Math.Min(new Vector2(worldToScreen.M11, worldToScreen.M12).Length(),
      //     new Vector2(worldToScreen.M21, worldToScreen.M22).Length()));
      // float chainThickness = 0.5f / pixelsPerWorldUnit;
      // float chainFeather = 0.5f / pixelsPerWorldUnit;

      int renderedChainLines = 0;
      foreach (var entry in ChainLightningAbility.TargetLines)
      {
        if (renderedChainLines >= BaseStats.MaxRenderedChainMagnetizerLines)
          break;

        // ConcurrentDictionary enumeration is safe without copying its values.
        var line = entry.Value;
        if (line != null)
        {
          // _shapeBatch.FillLine(line.Start, line.End,
          //   Math.Max(line.Thickness, 0.5f), line.ColorStart, 0.5f);
          _shapeBatch.FillLine(line.Start, line.End, line.Thickness, line.ColorStart, 1.5f);
          renderedChainLines++;
          // _shapeBatch.FillLine(harvester.BoundingCircle.Center, UntitledGemGameGameScreen.HomeBasePos, 0.1f, new Color(0.2f, 0.1f, 0.9f, 0.4f), 3.0f);
        }
      }

      DrawSpawnerEffects((float)gameTime.TotalGameTime.TotalSeconds);
      // Bound drawing only: every multicast net still captures and resolves.
      var nets = ChainLightningAbility.Constellations;
      for (int i = Math.Max(0, nets.Count - 8); i < nets.Count; ++i)
        DrawConstellation(nets[i]);

      _spriteBatch.End();
      _shapeBatch.End();
    }

    private void DrawSpawnerEffects(float time)
    {
      foreach (var pulse in SpawnerEffects.Pulses)
      {
        float progress = pulse.Progress;
        float radius = MathHelper.Lerp(pulse.StartRadius, pulse.EndRadius, progress);
        var glow = pulse.Color * (0.6f * (1f - progress));
        var core = Color.Lerp(pulse.Color, Color.White, 0.5f) * (1f - progress * 0.8f);
        var previous = pulse.Position + FinalSweepRingPoints[0] * radius;
        for (int i = 1; i < FinalSweepRingPoints.Length; ++i)
        {
          var next = pulse.Position + FinalSweepRingPoints[i] * radius;
          _shapeBatch.FillLine(previous, next, 2.5f, glow, 5f);
          _shapeBatch.FillLine(previous, next, 0.7f, core, 1.5f);
          previous = next;
        }
      }
      int drawn = 0;
      foreach (var seed in SpawnerEffects.Seeds)
      {
        if (!seed.IsLive || seed.PickedUp || seed.WasClicked) continue;
        if (++drawn > 128) break;
        var center = seed.BoundingCircle.Center;
        float size = MathF.Max(2f, seed.CollectionRadius * 0.9f);
        float pulse = 0.3f + 0.08f * MathF.Sin(time * 2f + seed.Id);
        var color = (seed.IsGilded || seed.IsLucky ? Color.Gold : Color.Aquamarine) * pulse;
        // A restrained diamond and faint fissure identify seeds without a bright halo.
        var top = center - Vector2.UnitY * size * 1.15f;
        var right = center + Vector2.UnitX * size;
        var bottom = center + Vector2.UnitY * size * 1.15f;
        var left = center - Vector2.UnitX * size;
        _shapeBatch.FillLine(top, right, 0.5f, color, 0.8f);
        _shapeBatch.FillLine(right, bottom, 0.5f, color, 0.8f);
        _shapeBatch.FillLine(bottom, left, 0.5f, color, 0.8f);
        _shapeBatch.FillLine(left, top, 0.5f, color, 0.8f);
        var bend = center + new Vector2(size * 0.3f, -size * 0.2f);
        _shapeBatch.FillLine(top, bend, 0.35f, color * 0.7f, 0.5f);
        _shapeBatch.FillLine(bend, center - Vector2.UnitX * size * 0.2f, 0.35f, color * 0.7f, 0.5f);
        _shapeBatch.FillLine(center - Vector2.UnitX * size * 0.2f, bottom, 0.35f, color * 0.7f, 0.5f);
      }
    }

    private void DrawConstellation(ConstellationNet net)
    {
      float flash = net.Age - ConstellationNet.Windup - ConstellationNet.CollapseDuration;
      if (flash >= 0f)
      {
        DrawWarpFlash(net.Destination, 70f, flash / ConstellationNet.FlashDuration, true);
        return;
      }
      float charge = Math.Clamp(net.Age / ConstellationNet.Windup, 0f, 1f);
      float pull = net.PullProgress;
      float shimmer = 0.8f + 0.2f * MathF.Sin(net.Age * 35f);
      var glow = new Color(90, 110, 255) * (0.65f * shimmer);
      var core = Color.Lerp(Color.Cyan, Color.White, charge) * 0.95f;
      int count = net.Hull.Length;
      for (int i = 0; i < count; ++i)
      {
        var start = Vector2.Lerp(net.Hull[i], net.Destination, pull);
        var end = Vector2.Lerp(net.Hull[(i + 1) % count], net.Destination, pull);
        float stitch = Math.Clamp(charge * count - i, 0f, 1f);
        if (stitch <= 0f) continue;
        var stitchedEnd = Vector2.Lerp(start, end, stitch);
        _shapeBatch.FillLine(start, stitchedEnd, 3f, glow, 5f);
        _shapeBatch.FillLine(start, stitchedEnd, 0.9f, core, 1.5f);
        float starSize = 3f + 3f * charge;
        _shapeBatch.FillLine(start - Vector2.UnitX * starSize, start + Vector2.UnitX * starSize, 1f, core, 2f);
        _shapeBatch.FillLine(start - Vector2.UnitY * starSize, start + Vector2.UnitY * starSize, 1f, core, 2f);
      }
      // Three bright pulses race around the completed outline. Captures use a
      // bounded set of flickering sparks, not hundreds of extra chain lines.
      if (charge >= 1f)
      {
        for (int pulse = 0; pulse < 3; ++pulse)
        {
          float along = (net.Age * 2f + pulse / 3f) % 1f * count;
          int edge = (int)along;
          var point = Vector2.Lerp(net.Hull[edge], net.Hull[(edge + 1) % count], along - edge);
          point = Vector2.Lerp(point, net.Destination, pull);
          _shapeBatch.FillLine(point - Vector2.One * 3f, point + Vector2.One * 3f, 2f, Color.White, 3f);
        }
      }
      for (int i = 0; i < net.Sparks.Length; ++i)
      {
        var point = Vector2.Lerp(net.Sparks[i], net.Destination, pull);
        float twinkle = 0.5f + 0.5f * MathF.Sin(net.Age * 24f + i * 2.4f);
        var color = Color.Cyan * (twinkle * charge);
        _shapeBatch.FillLine(point - Vector2.UnitX * 2f, point + Vector2.UnitX * 2f, 0.8f, color, 2f);
        _shapeBatch.FillLine(point - Vector2.UnitY * 2f, point + Vector2.UnitY * 2f, 0.8f, color, 2f);
      }
    }

    private void DrawWarpFlash(Vector2 position, float size, float progress, bool arriving)
    {
      // A collapsing departure portal and expanding arrival flash share the
      // cached ring geometry. Both stay anchored to the actual teleport sites.
      float fade = (1f - progress) * (1f - progress);
      float radius = size * (arriving
        ? MathHelper.Lerp(0.2f, 1.4f, progress)
        : MathHelper.Lerp(1f, 0.15f, progress));
      var glow = (arriving ? Color.Cyan : new Color(155, 100, 255)) * (0.45f * fade);
      var core = new Color(220, 245, 255) * (0.85f * fade);
      var previous = position + FinalSweepRingPoints[0] * radius;
      for (int i = 1; i < FinalSweepRingPoints.Length; i++)
      {
        var next = position + FinalSweepRingPoints[i] * radius;
        _shapeBatch.FillLine(previous, next, 2f, glow, 4f);
        _shapeBatch.FillLine(previous, next, 0.7f, core, 1.5f);
        previous = next;
      }

      // Short radial streaks give the blink a bright, magical burst without
      // drawing a distracting line across the entire teleport distance.
      for (int i = 0; i < 32; i += 4)
      {
        var direction = FinalSweepRingPoints[i];
        float reach = size * (i % 8 == 0 ? 1.1f : 0.7f) * (1f - progress * 0.5f);
        var end = position + direction * reach;
        _shapeBatch.FillLine(position, end, 2f, glow, 4f);
        _shapeBatch.FillLine(position, end, 0.7f, core, 1.5f);
      }
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
    public static RenderGemSystem Instance { get; private set; }
    private readonly GemRenderBatch _batch;
    private readonly OrthographicCamera _camera;
    private ComponentMapper<Sprite> _sprites;
    private ComponentMapper<Transform2> _transforms;
    private ComponentMapper<Gem> _gems;
    private EffectParameter _viewProjection, _outlineColor;
    private Texture2D _surfaceSource, _surfaceTexture;
    public int UploadedPagesLastFrame => _batch.UploadedPagesLastFrame;
    public int RebuiltQuadsLastFrame => _batch.RebuiltQuadsLastFrame;

    public RenderGemSystem(SpriteBatch spriteBatch, ShapeBatch shapeBatch, GraphicsDevice graphicsDevice, OrthographicCamera camera)
      : base(Aspect.All(typeof(Transform2), typeof(Sprite), typeof(Gem)))
    {
      _camera = camera;
      _batch = new GemRenderBatch(graphicsDevice);
      Instance = this;
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
      _sprites = mapperService.GetMapper<Sprite>();
      _transforms = mapperService.GetMapper<Transform2>();
      _gems = mapperService.GetMapper<Gem>();
      _viewProjection = EffectCache.GemEffect.Value.Parameters["view_projection"];
      _outlineColor = EffectCache.GemEffect.Value.Parameters["_OutlineColor"];
    }

    protected override void OnEntityAdded(int entityId)
    {
      // Creation callbacks also include ships, the base, and incomplete entities.
      if (!_gems.Has(entityId) || !_sprites.Has(entityId) || !_transforms.Has(entityId)) return;
      _batch.Add(entityId, _sprites.Get(entityId), _transforms.Get(entityId));
    }
    protected override void OnEntityRemoved(int entityId) => _batch.Remove(entityId);
    public void UpdateGem(int entityId) => _batch.Update(entityId);
    public void RemoveGem(int entityId) => _batch.Remove(entityId);

    public void DisposeBuffers()
    {
      _batch.Dispose();
      _surfaceTexture?.Dispose();
      _surfaceTexture = _surfaceSource = null;
      if (Instance == this) Instance = null;
    }

    public override void Draw(GameTime gameTime)
    {
      if (!EffectCache.GemEffect.IsLoaded || EffectCache.GemEffect.Value == null) return;
      _viewProjection?.SetValue(_camera.GetBoundingFrustum().Matrix);
      var texture = TextureCache.HudRedGem.Value;
      if (_surfaceSource != texture)
      {
        var surface = GemSurfaceTexture.Create(texture);
        _surfaceTexture?.Dispose();
        _surfaceTexture = surface;
        _surfaceSource = texture;
      }
      _outlineColor?.SetValue(Vector4.One);
      _batch.Draw(EffectCache.GemEffect.Value, _surfaceTexture);
    }
  }
}
