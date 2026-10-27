#if !KNI_WEB
using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MonoGame.Extended.Tweening;

namespace UntitledGemGame;

internal static class TweenRuntime
{
  // TweenTo uses Activator.CreateInstance. Explicitly preserve the constructors
  // of the closed generic types used by the game, in addition to the existing
  // namespace descriptor, so Native AOT can construct them through reflection.
  [DynamicDependency(DynamicallyAccessedMemberTypes.NonPublicConstructors, typeof(LinearTween<float>))]
  [DynamicDependency(DynamicallyAccessedMemberTypes.NonPublicConstructors, typeof(LinearTween<Vector2>))]
  internal static void Initialize() { }

  // Test the published executable before graphics, Steam or save loading.
  internal static void Check()
  {
    using var tweener = new Tweener();
    var target = new CheckTarget();
    var transform = new Transform2();
    int completed = 0;
    tweener.TweenTo(target, t => t.Progress, 1f, 1f).OnEnd(_ => completed++);
    tweener.TweenTo(target, t => t.Alpha, 0f, 1f).OnEnd(_ => completed++);
    tweener.TweenTo(transform, t => t.Position, new Vector2(10f, 20f), 1f).OnEnd(_ => completed++);
    tweener.TweenTo(transform, t => t.Scale, new Vector2(3f, 5f), 1f).OnEnd(_ => completed++);
    tweener.Update(0.5f);
    if (target.Progress != 0.5f || target.Alpha != 0.5f
      || transform.Position != new Vector2(5f, 10f) || transform.Scale != new Vector2(2f, 3f)
      || completed != 0)
      throw new InvalidOperationException("Published tween interpolation failed.");
    tweener.Update(0.5f);
    if (target.Progress != 1f || target.Alpha != 0f
      || transform.Position != new Vector2(10f, 20f) || transform.Scale != new Vector2(3f, 5f)
      || completed != 4 || tweener.ActiveTweens.Length != 0)
      throw new InvalidOperationException("Published tween completion failed.");
    Console.WriteLine("Published tween checks passed: float properties, Vector2 position/scale and completion callbacks.");
  }

  private sealed class CheckTarget
  {
    public float Progress { get; set; }
    public float Alpha { get; set; } = 1f;
  }
}
#endif
