using GUI.Shared.Helpers;
using Gum.Forms.Controls;
using JapeFramework;
using JapeFramework.DataStructures;
using JapeFramework.Helpers;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MonoGame.Extended.Collisions;
using MonoGame.Extended.ECS;
using MonoGame.Extended.Graphics;
using MonoGame.Extended.Input;
using MonoGame.Extended.Tweening;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using UntitledGemGame.Screens;
using UntitledGemGame.Systems;

namespace UntitledGemGame.Entities
{
  public enum GemTypes
  {
    Blue,
    DarkBlue,
    Gold,
    LightGreen,
    Lilac,
    Purple,
    Red,
    Teal
  }

  //TODO: Optimization: Should this be made into a system class instead with just arrays of structs? 

  public class Gem : ICollisionActorJ
  {
    //public string Name { get; set; }
    // public int ID { get; set; }
    // public IShapeF Bounds => BoundsCircle;

    public int Id { get; set; }
    public int GridIndex { get; set; }
    // public CollisionShape2D Shape { get; set; }
    public BoundingCircle2D BoundingCircle => m_boundingCircle;
    private BoundingCircle2D m_boundingCircle;
    private float m_radius;
    // public BoundingCircle2D BoundsCircle = new BoundingCircle2D();
    // public CircleF BoundsCircle = new CircleF();

    public bool PickedUp { get; set; }

    public bool ShouldDestroy { get; set; }
    public bool PositionMoved = true;

    private Vector2 m_targetScale;
    private Vector2 m_targetPosition;
    private float m_animationSpeedScale = 5.0f;
    private float m_animationSpeedPosition = 5.0f;

    private bool m_animating = false;
    private bool m_destroyAfterAnimation = false;

    // private readonly Tweener _tweener = new();
    //
    // private Tween m_tween;
    // private Tween m_tween2;

    private Transform2 m_targetHarvester;

    private Entity m_entity;
    private Transform2 m_transform;
    private Sprite m_sprite;

    public GemTypes GemType { get; set; }

    public string LayerName => "Gem";



    // private bool isTweeningStart = false;
    // private bool isTweeningHarvester = false;
    // private bool isTweeningClicked = false;

    public uint BaseValue = 1;

    // public void OnCollision(CollisionEventArgs collisionInfo)
    // {
    //   //Console.WriteLine("Gem Collision");
    // }

    public Gem()
    {
      m_boundingCircle = new BoundingCircle2D(Vector2.Zero, 0);
    }

    //public Gem(Entity gemEntity, IShapeF bounds)
    //{
    //  //m_entity = gemEntity;
    //  //m_transform = m_entity.Get<Transform2>();

    //  Initialize(gemEntity, bounds);
    //}


    public void SetCollisionPosition(Vector2 position, float radius = -1)
    {
      m_boundingCircle.Center = position;

      if (radius >= 0)
        m_boundingCircle.Radius = radius;
    }
    private Vector2 OrigScale = Vector2.One;

    public void Initialize(Entity gemEntity, float radius, uint baseValue)
    {
      m_targetHarvester = null;
      m_entity = gemEntity;
      m_transform = m_entity.Get<Transform2>();

      OrigScale = m_transform.Scale;
      m_transform.Scale = new Vector2(0.1f, 0.1f);

      SetAnimation(OrigScale, m_transform.Position, false);

      Id = gemEntity.Id;

      // m_tween = _tweener.TweenTo(gemEntity.Get<Transform2>(), transform => transform.Scale, OrigScale, 0.2f)
      //   .Easing(EasingFunctions.Linear).OnEnd((tween) =>
      //   {
      //     isTweeningStart = false;
      //   });


      SetCollisionPosition(m_transform.Position, radius);

      // TweenHandler.Instance.AddTweenScale(gemEntity.Get<Transform2>(), OrigScale, 0.2f, EasingFunctions.Linear);

      m_radius = radius;
      // BoundsCircle.Center = m_transform.Position;
      // BoundsCircle.Radius = radius;


      m_sprite = gemEntity.Get<Sprite>();
      // isTweeningStart = true;

      BaseValue = baseValue;
      //Bounds = bounds;
    }

    public void Reset(/*Entity gemEntity*/)
    {
      ShouldDestroy = false;
      PickedUp = false;
      WasClicked = false;
      Id = -1;
      GridIndex = -1;
      m_entity = null;
      m_transform = null;
      m_targetHarvester = null;
      // _tweener.CancelAndCompleteAll();
      PositionMoved = false;

      m_animating = false;
      m_destroyAfterAnimation = false;
      m_targetPosition = Vector2.Zero;
      m_targetScale = Vector2.Zero;

      //m_entity = gemEntity;
      //m_transform = m_entity.Get<Transform2>();

      // m_tween = _tweener.TweenTo(gemEntity.Get<Transform2>(), transform => transform.Scale, new Vector2(1.0f, 1.0f), 2)
      //  .Easing(EasingFunctions.Linear);
    }

    public bool WasClicked = false;

    // private void GravitateGem(float dt, Vector2 pos, float magnitude)
    // {
    //   var dir = pos - m_transform.Position;
    //   var dist = Vector2.Distance(pos, m_transform.Position);
    //   dir = Vector2.Normalize(dir);
    //   m_transform.Position += dir * magnitude * dt * (1 / dist) * 100.0f;
    // }
    public int TargetMagnetIndex = -1;
    public Vector2 TargetMagnetPos = Vector2.Zero;
    public float MinMagnetDistSqr = float.MaxValue;

    public void GravitateGem(float dt, Vector2 targetPos, float magnitude, float falloffPower, float maxSpeed)
    {
      var dir = targetPos - m_transform.Position;
      // var dist = dir.Length(); // More efficient than doing Distance() + Normalize() separately

      var dist = Vector2.Distance(targetPos, m_transform.Position);

      if (dist < 0.01f) dist = 0.01f;
      // if (dist > UpgradeManager.UG.HomebaseMagnetizerMaxDistance) return;

      dir = Vector2.Normalize(dir);

      // 3. Calculate gravity force using the falloff power exponent
      float gravityEffect = 1.0f / MathF.Pow(dist, falloffPower);
      Vector2 velocity = dir * magnitude * gravityEffect * 100.0f;

      // 4. Clamp to Max Speed so gems don't teleport or jitter when ultra-close
      if (velocity.Length() > maxSpeed)
      {
        velocity = Vector2.Normalize(velocity) * maxSpeed;
      }

      var movement = velocity * dt;
      m_transform.Position += movement;

      if (movement.Length() > 0.05f)
        PositionMoved = true;
    }

    private Vector2 randVecPos = Vector2.Zero;

    public void Update(GameTime gameTime, Vector2 mouseWorldPos, bool isMouseClicked, float dt)
    {
      PositionMoved = false;
      // WasClicked = false;
      //if (PickedUp)
      //{
      //  //TODO: only for instant collection upgrade?
      //  if (m_tween is { IsComplete: true })
      //  {
      //    //ShouldDestroy = true;
      //  }
      //}
      //
      // if (isTweeningStart || isTweeningHarvester || isTweeningClicked)
      // {
      //   timeSinceUpdateTweener += dt;
      //   var fps = BaseGame.m_frameCounter.AverageFramesPerSecond;
      //   if(fps < 20)
      //   {
      //     _tweener.Update(10000000);
      //   }
      //   else if (fps < 60 && timeSinceUpdateTweener > 0.1f)
      //   {
      //     _tweener.Update(timeSinceUpdateTweener);
      //     timeSinceUpdateTweener = 0;
      //   }
      //   else if (fps >= 60 && timeSinceUpdateTweener > 0.005f)
      //   {
      //     _tweener.Update(timeSinceUpdateTweener);
      //     timeSinceUpdateTweener = 0;
      //   }
      //   else if (fps >= 230)
      //   {
      //     _tweener.Update(timeSinceUpdateTweener);
      //     timeSinceUpdateTweener = 0;
      //   }
      // }

      if (m_animating)
      {
        bool animationDone = false;

        if (m_transform.Scale != m_targetScale)
        {
          // Use Lerp to move towards the target
          var x = MathHelper.Lerp(m_transform.Scale.X, m_targetScale.X, 10.0f * dt);
          var y = MathHelper.Lerp(m_transform.Scale.Y, m_targetScale.Y, 10.0f * dt);

          x = MathHelper.Lerp(m_transform.Scale.X, m_targetScale.X, gameTime.GetElapsedSeconds() * m_animationSpeedScale);
          y = MathHelper.Lerp(m_transform.Scale.Y, m_targetScale.Y, gameTime.GetElapsedSeconds() * m_animationSpeedScale);

          m_transform.Scale = new Vector2(x, y);

          // Optimization: Snap to target if very close to avoid infinite microscopic movement
          if (Vector2.DistanceSquared(m_transform.Scale, m_targetScale) < 0.0001f)
          {
            m_transform.Scale = m_targetScale;
            animationDone = true;
          }
        }
        else
        {
          animationDone = true;
        }

        if (m_transform.Position != m_targetPosition && m_targetHarvester == null)
        {
          // Use Lerp to move towards the target
          var x = MathHelper.Lerp(m_transform.Position.X, m_targetPosition.X, 10.0f * dt);
          var y = MathHelper.Lerp(m_transform.Position.Y, m_targetPosition.Y, 10.0f * dt);

          x = MathHelper.Lerp(m_transform.Position.X, m_targetPosition.X, gameTime.GetElapsedSeconds() * m_animationSpeedPosition);
          y = MathHelper.Lerp(m_transform.Position.Y, m_targetPosition.Y, gameTime.GetElapsedSeconds() * m_animationSpeedPosition);


          m_transform.Position = new Vector2(x, y);

          // Optimization: Snap to target if very close to avoid infinite microscopic movement
          if (Vector2.DistanceSquared(m_transform.Position, m_targetPosition) < 0.0001f)
          {
            m_transform.Position = m_targetPosition;
          }

          PositionMoved = true;
        }

        if (animationDone)
        {
          m_animating = false;
          if (m_destroyAfterAnimation)
          {
            ShouldDestroy = true;
          }
        }
      }


      if (m_targetHarvester != null)
      {
        // var distance = Vector2.Distance(m_targetHarvester.Position, m_transform.Position);
        //
        // Vector2 dir = m_targetHarvester.Position - m_transform.Position;
        // dir.Normalize();
        // var movement = dir * (float)gameTime.ElapsedGameTime.TotalSeconds * 8.0f * /*(1.0f / distance)*/distance;
        // m_transform.Position += movement;

        var x = MathHelper.Lerp(m_transform.Position.X, m_targetHarvester.Position.X, gameTime.GetElapsedSeconds() * m_animationSpeedPosition);
        var y = MathHelper.Lerp(m_transform.Position.Y, m_targetHarvester.Position.Y, gameTime.GetElapsedSeconds() * m_animationSpeedPosition);

        m_transform.Position = new Vector2(x, y);
      }
      // else if (m_wasPickedUp)
      // {
      //   var hbPos = UntitledGemGameGameScreen.HomeBasePos;
      //
      //   var dir = hbPos - m_transform.Position;
      //   var dist = Vector2.Distance(hbPos, m_transform.Position);
      //   dir = Vector2.Normalize(dir);
      //   m_transform.Position += dir * 60.0f * dt * (1 / dist) * 10.0f;
      //
      //   BoundsCircle.Center = m_transform.Position;
      // }
      else
      {
        //FIXME, Should this logic just move to the harvester code instead? i think yes, every gem doesnt need to be checked yah?
        const float maxRadius = 200.0f;
        const float maxRadiusSqr = maxRadius * maxRadius;

        var magnets = MagnetizerCache.ActiveMagnets;
        int count = magnets.Count;

        if (count > 0)
        {
          Vector2 gemPos = m_transform.Position;
          float minSqrDist = maxRadiusSqr; // Acts as range filter
          int winningMagnetIndex = -1;

          // Single fast distance check across ALL magnet sources
          for (int i = 0; i < count; i++)
          {
            Vector2 magPos = magnets[i].Position;
            float dx = magPos.X - gemPos.X;
            float dy = magPos.Y - gemPos.Y;
            float sqrDist = dx * dx + dy * dy;

            if (sqrDist < minSqrDist)
            {
              minSqrDist = sqrDist;
              winningMagnetIndex = i;
            }
          }

          // Apply movement ONLY to the single winning magnet
          if (winningMagnetIndex != -1)
          {
            var winningMagnet = magnets[winningMagnetIndex];

            // Add random offset for beacons if desired
            Vector2 targetPos = winningMagnet.Position;
            if (randVecPos != Vector2.Zero)
              targetPos += randVecPos;

            GravitateGem(dt, targetPos, winningMagnet.Power, UpgradeManager.UG.HomebaseMagnetizerFalloff, maxRadius);
          }
        }



        // if (TargetMagnetIndex != -1)
        // {
        //   GravitateGem(dt, TargetMagnetPos, 200.0f, UpgradeManager.UG.HomebaseMagnetizerFalloff, 5000);
        // }
        // if (HomeBase.BonusHarvesterMagnetPower > 0)
        // {
        //   var harvesters = EntityFactory.Instance.Harvesters;
        //   var closesHarvester = harvesters
        //     .OrderBy(h => Vector2.Distance(h.Value.Get<Transform2>().Position, m_transform.Position))
        //     .FirstOrDefault();
        //
        //   if (harvesters.Count != 0 && closesHarvester.Value != null)
        //   {
        //     var pos = closesHarvester.Value.Get<Transform2>().Position;
        //     GravitateGem(dt, pos, HomeBase.BonusHarvesterMagnetPower, UpgradeManager.UG.HomebaseMagnetizerFalloff, 200.0f);
        //   }
        //
        //   if (UpgradeManager.UG.MagnetizerDrones)
        //   {
        //     var drones = EntityFactory.Instance.Drones;
        //     var closesDrone = drones
        //       .OrderBy(h => Vector2.Distance(h.Value.Get<Transform2>().Position, m_transform.Position))
        //       .FirstOrDefault();
        //
        //     if (drones.Count != 0 && closesDrone.Value != null)
        //     {
        //       var pos = closesDrone.Value.Get<Transform2>().Position;
        //       GravitateGem(dt, pos, HomeBase.BonusHarvesterMagnetPower, UpgradeManager.UG.HomebaseMagnetizerFalloff, 200.0f);
        //     }
        //   }
        // }
        //
        // if (HomeBase.BonusMagnetPower > 0)
        // {
        //   GravitateGem(dt, UntitledGemGameGameScreen.HomeBasePos, HomeBase.BonusMagnetPower, UpgradeManager.UG.HomebaseMagnetizerFalloff, 200.0f);
        // }
        //
        // if (UpgradeManager.UG.MagnetizerBeacons)
        // {
        //   if (randVecPos == Vector2.Zero)
        //     randVecPos = RandomHelper.Vector2(-5, 5);
        //   foreach (var beacon in EntityFactory.Instance.Beacons)
        //   {
        //     var pos = beacon.Value.Get<Transform2>().Position;
        //     GravitateGem(dt, pos + randVecPos, HomeBase.BonusMagnetPower, UpgradeManager.UG.HomebaseMagnetizerFalloff, 200.0f);
        //   }
        // }
      }

      TargetMagnetIndex = -1;
      // bool isMouseOver = BoundsCircle.Contains(mouseWorldPos);


      float clickRangeMultiplier = UpgradeManager.UG.ClickRadius;
      float gemWidth = TextureCache.HudRedGem.Value.Width * clickRangeMultiplier;
      float gemHeight = TextureCache.HudRedGem.Value.Height * clickRangeMultiplier;
      bool isMouseOver = mouseWorldPos.X >= m_transform.Position.X - gemWidth / 2 &&
                         mouseWorldPos.X <= m_transform.Position.X + gemWidth / 2 &&
                         mouseWorldPos.Y >= m_transform.Position.Y - gemHeight / 2 &&
                         mouseWorldPos.Y <= m_transform.Position.Y + gemHeight / 2;
      // bool isMouseOver = mouseWorldPos 


      if (isMouseOver)
      {
        m_sprite.Color = new Color(m_sprite.Color.R, m_sprite.Color.G, m_sprite.Color.B, (byte)255);
      }
      else
      {
        m_sprite.Color = new Color(m_sprite.Color.R, m_sprite.Color.G, m_sprite.Color.B, (byte)0);
      }

      if (isMouseClicked && isMouseOver && !PickedUp && !RenderGuiSystem.Instance.drawUpgradesGui)
      {
        // var dir = UntitledGemGameGameScreen.HomeBasePos - gemPos.Value;
        // dir.Normalize();
        // var distance = Vector2.Distance(gemPos.Value, UntitledGemGameGameScreen.HomeBasePos);
        // gem.Get<Transform2>().Position += dir * 6.0f * (float)gameTime.GetElapsedSeconds() * distance;

        // m_transform.Position = UntitledGemGameGameScreen.HomeBasePos;
        // SetPickedUp(m_entity, EntityFactory.Instance.HomeBaseEntity, null);

        OnClicked(true);
      }

      // BoundsCircle.Center = m_transform.Position;
      // Shape.BoundingBox.Center = m_transform.Position;
      // Shape = new CollisionShape2D(new BoundingCircle2D(m_transform.Position, m_radius));
      m_boundingCircle.Center = m_transform.Position;
      m_boundingCircle.Radius = m_radius;
    }

    // public void FindOtherGems()
    // {
    //   bool procc = RandomHelper.Int(0, 2) == 0;
    //   if (!procc) return;
    //
    //   for (int i = 0; i < Math.Min(3, HarvesterCollectionSystem.Instance.m_gems2.Count); i++)
    //   {
    //     for (int attempt = 0; attempt < 100; attempt++)
    //     {
    //       var id = HarvesterCollectionSystem.Instance.m_gems2.GetRandom();
    //       var gemEntity = HarvesterCollectionSystem.Instance.GetEntityP(id);
    //       var gemPos = gemEntity?.Get<Transform2>()?.Position;
    //
    //       if (gemPos == null)
    //         break;
    //
    //       if (ChainLightningAbility.TargetLines.ContainsKey(id))
    //         continue;
    //
    //       var gem = gemEntity.Get<Gem>();
    //
    //       if (gem == null || gem.WasClicked)
    //         return;
    //
    //       bool success = ChainLightningAbility.TargetLines2.TryAdd(id, new LineShape(gemPos.Value, m_transform.Position, 0.05f, Color.Yellow, Color.Yellow));
    //
    //       if (success)
    //       {
    //         // TimerHelper.DoAfter(() =>
    //         // {
    //         //   ChainLightningAbility.TargetLines2.TryRemove(id, out var _);
    //         //
    //         //   var gem = gemEntity.Get<Gem>();
    //         //
    //         //   if (gem == null || gem.WasClicked)
    //         //     return;
    //         //
    //         //   gem.OnClicked(false);
    //         // }, 100, true);
    //         // gems.Add(id);
    //
    //         gem.OnClicked(false);
    //         break;
    //       }
    //     }
    //   }
    // }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryGetClosestPosition(Vector2 gemPos, List<Vector2> targets, float maxRadiusSqr, out Vector2 closestPos)
    {
      closestPos = Vector2.Zero;
      float minSqrDist = maxRadiusSqr; // Automatically culls targets outside the max range!
      bool found = false;

      int count = targets.Count;
      for (int i = 0; i < count; i++)
      {
        Vector2 target = targets[i];
        float dx = target.X - gemPos.X;
        float dy = target.Y - gemPos.Y;
        float sqrDist = dx * dx + dy * dy;

        if (sqrDist < minSqrDist)
        {
          minSqrDist = sqrDist;
          closestPos = target;
          found = true;
        }
      }

      return found;
    }

    public void OnClicked(bool fromClick)
    {
      if (WasClicked && !fromClick)
        return;

      if (PickedUp)
        return;
      if (ShouldDestroy)
        return;

      WasClicked = true;

      SetAnimation(Vector2.Zero, UntitledGemGameGameScreen.HomeBasePos, false);
      HarvesterCollectionSystem.Instance.flatSpatialHash.Gems[GridIndex].ClaimState = 2;

      // _tweener.CancelAndCompleteAll();

      // var gemTransform = m_entity.Get<Transform2>();
      // m_tween = _tweener.TweenTo(gemTransform, transform => transform.Position, UntitledGemGameGameScreen.HomeBasePos, 0.5f)
      //   .Easing(EasingFunctions.Linear).OnEnd((tween) =>
      //   {
      //     isTweeningClicked = false;
      //   });
      //
      // m_tween2 = _tweener.TweenTo(gemTransform, transform => transform.Scale, Vector2.Zero, 0.5f)
      //   .Easing(EasingFunctions.CubicIn);

      // TweenHandler.Instance.AddTweenPosition(gemTransform, UntitledGemGameGameScreen.HomeBasePos, 0.5f, EasingFunctions.Linear);
      // TweenHandler.Instance.AddTweenScale(gemTransform, Vector2.Zero, 0.5f, EasingFunctions.CubicIn);

      // isTweeningClicked = true;

      // FindOtherGems();
    }

    public void MergeGem(Vector2 position)
    {
      // var gemTransform = m_entity.Get<Transform2>();

      PickedUp = true;
      SetAnimation(Vector2.Zero, position, true);

      // _tweener.CancelAndCompleteAll();

      // isTweeningHarvester = true;
      // m_tween = _tweener.TweenTo(gemTransform, transform => transform.Position, position, 0.5f)
      //   .Easing(EasingFunctions.Linear).OnEnd((tween) =>
      //   {
      //     isTweeningHarvester = false;
      //     ShouldDestroy = true;
      //   }); ;
      //
      // m_tween2 = _tweener.TweenTo(gemTransform, transform => transform.Scale, Vector2.Zero, 0.5f)
      //   .Easing(EasingFunctions.CubicIn);

      // TweenHandler.Instance.AddTweenPosition(gemTransform, position, 0.5f, EasingFunctions.Linear, () => { ShouldDestroy = true; });
      // TweenHandler.Instance.AddTweenScale(gemTransform, Vector2.Zero, 0.5f, EasingFunctions.CubicIn);
    }

    public void SetPickedUp(Entity gemEntity, Entity harvesterEntity, Action onDone)
    {
      if (PickedUp) return;
      if (ShouldDestroy) return;

      PickedUp = true;

      m_targetHarvester = harvesterEntity.Get<Transform2>();
      var gemTransform = gemEntity.Get<Transform2>();

      gemTransform.Scale = OrigScale;

      SetAnimation(Vector2.Zero, m_targetHarvester.Position, true, 5.0f, 10.0f);
    }

    private void SetAnimation(Vector2 targetScale, Vector2 targetPosition, bool destroyAfter, float speedScale = 5.0f, float speedPos = 5.0f)
    {
      m_animating = true;
      m_destroyAfterAnimation = destroyAfter;

      m_targetScale = targetScale;
      m_targetPosition = targetPosition;
      m_animationSpeedScale = speedScale;
      m_animationSpeedPosition = speedPos;
    }
  }
}
