
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MonoGame.Extended.Tweening;

public sealed class TweenHandler
{
  private static readonly TweenHandler instance = new TweenHandler();

    // Explicit static constructor to tell C# compiler
    // not to mark type as beforefieldinit
    static TweenHandler()
    {
    }

    private TweenHandler()
    {
    }

    public static TweenHandler Instance
    {
        get
        {
            return instance;
        }
    }

    private readonly Tweener m_tweener = new();
    private List<Tween> m_tweens = new();

    private List<PendingTween> m_pendingTweens = new ();
    
    private class PendingTween
    {
      public enum TweenType
      {
        Scale,
        Position
      }
      public Transform2 Transform;
      public Vector2 TargetScale;
      public Vector2 TargetPosition;
      public float Duration;
      public Func<float, float> Easing;
      public Action OnEnd;

      public bool Added;
      public TweenType Type; 
    }

    public void AddTweenScale(Transform2 transform, Vector2 targetScale, float duration, Func<float, float> easing, Action onEnd = null)
    {
      // var tween = m_tweener.TweenTo(transform, t => t.Scale, targetScale, duration)
      //   .Easing(easing).OnEnd((tween) =>
      //   {
      //     m_tweens.Remove(tween);
      //     onEnd?.Invoke();
      //   });
      // m_tweens.Add(tween);

      m_pendingTweens.Add(new PendingTween {
            Transform = transform,
            TargetScale = targetScale,
            Duration = duration,
            Easing = easing,
            OnEnd = onEnd,
            Type = PendingTween.TweenType.Scale
          });
    }

    public void AddTweenPosition(Transform2 transform, Vector2 targetPosition, float duration, Func<float, float> easing, Action onEnd = null)
    {
      // var tween = m_tweener.TweenTo(transform, t => t.Position, targetPosition, duration)
      //   .Easing(easing).OnEnd((tween) =>
      //   {
      //     m_tweens.Remove(tween);
      //     onEnd?.Invoke();
      //   });
      // m_tweens.Add(tween);

      m_pendingTweens.Add(new PendingTween {
            Transform = transform,
            TargetPosition = targetPosition,
            Duration = duration,
            Easing = easing,
            OnEnd = onEnd,
            Type = PendingTween.TweenType.Position
          });
    }

    public void Update(float dt)
    {
        m_tweener.Update(dt);

        foreach(var pendingTween in m_pendingTweens)
        {
          if(pendingTween.Added) continue;
          pendingTween.Added = true;

          if(pendingTween.Type == PendingTween.TweenType.Position)
          {
            var tween = m_tweener.TweenTo(pendingTween.Transform, t => t.Position, pendingTween.TargetPosition, pendingTween.Duration)
              .Easing(pendingTween.Easing).OnEnd((tween) =>
              {
                m_tweens.Remove(tween);
                pendingTween.OnEnd?.Invoke();
                m_pendingTweens.Remove(pendingTween);
              });
            m_tweens.Add(tween);
          }
          else if (pendingTween.Type == PendingTween.TweenType.Scale)
          {
            var tween = m_tweener.TweenTo(pendingTween.Transform, t => t.Scale, pendingTween.TargetScale, pendingTween.Duration)
              .Easing(pendingTween.Easing).OnEnd((tween) =>
              {
                m_tweens.Remove(tween);
                pendingTween.OnEnd?.Invoke();
                m_pendingTweens.Remove(pendingTween);
              });
            m_tweens.Add(tween);
          }
        }
    }

}
