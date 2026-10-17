using System;
using Microsoft.Xna.Framework;
using UntitledGemGame;

public partial class RenderGuiSystem
{
  // Local coordinates let a reveal follow its card when docking or resizing.
  // Each 32x32 pool holds at most three burst descriptors, isolated from combat.
  private readonly GpuParticleSystem[] signalParticlePools = new GpuParticleSystem[3];
  private int signalParticleRevealedMask;

  private void ClearSignalParticles()
  {
    signalParticleRevealedMask = 0;
    foreach (var pool in signalParticlePools) pool?.Clear();
  }

  internal void UpdateSignalParticles(float dt)
  {
    if (!signalChoicesVisible || !drawUpgradesGui || m_upgradeWindowType != UpgradeTypes.Signals)
    {
      foreach (var pool in signalParticlePools) pool?.Clear();
      return;
    }
    for (int i = 0; i < 3; i++)
    {
      float elapsed = SignalRevealAge(i) - 0.3f;
      if (elapsed >= 0 && (signalParticleRevealedMask & (1 << i)) == 0)
      {
        signalParticleRevealedMask |= 1 << i;
        int rarity = Signals.PendingChoices[i].Rarity;
        // Opening an already revealed choice never replays the discharge.
        if (rarity > 0 && elapsed < 0.2f) EmitSignalParticles(i, rarity);
      }
      signalParticlePools[i]?.Update(dt);
    }
  }

  private void EmitSignalParticles(int index, int rarity)
  {
#if !KNI_WEB
    if (EffectCache.GpuParticlesFx?.IsLoaded != true || EffectCache.GpuParticlesFx.IsFailed) return;
    var pool = signalParticlePools[index] ??= new GpuParticleSystem(GameMain.Instance.GraphicsDevice,
      EffectCache.GpuParticlesFx.Value, 32) { Drag = 2.4f };
    var card = SignalCard(index);
    float radius = Math.Min(card.Width * 0.32f, card.Height * 0.22f);
    var color = SignalRarity(rarity).Color;
    float duration = 0.7f + rarity * 0.3f;
    pool.Emit(new GpuParticleBurst(Vector2.Zero, 40 + rarity * 48, Color.Lerp(color, Color.White, 0.45f))
    {
      Motion = GpuParticleMotion.Outward, Radius = radius * 0.4f, InnerRadius = radius * 0.32f,
      Speed = new Vector2(0.65f, 1.65f) * radius, Spread = 0.18f,
      Lifetime = new(duration * 0.45f, duration), Size = new(3, 6 + rarity), Streak = true
    });
    pool.Emit(new GpuParticleBurst(Vector2.Zero, 20 + rarity * 20, color)
    {
      Motion = GpuParticleMotion.Outward, Radius = radius * 0.45f, InnerRadius = radius * 0.3f,
      Speed = new Vector2(0.25f, 0.65f) * radius, Spread = 0.5f,
      Lifetime = new(duration * 0.5f, duration), Size = new(8, 15 + rarity * 2)
    });
    if (rarity >= 3)
      pool.Emit(new GpuParticleBurst(Vector2.Zero, rarity * 24, Color.Lerp(color, Color.White, 0.25f))
      {
        Motion = GpuParticleMotion.Tangential, Radius = radius * 0.5f, InnerRadius = radius * 0.48f,
        Direction = 0, Spread = 0.15f, Speed = new Vector2(0.3f, 0.6f) * radius,
        Lifetime = new(0.4f, duration * 0.8f), Size = new(2, 4), Streak = true
      });
#endif
  }

  internal void SimulateSignalParticles()
  {
    foreach (var pool in signalParticlePools) pool?.Simulate();
  }

  private void DisposeSignalParticles()
  {
    foreach (var pool in signalParticlePools) pool?.Dispose();
    Array.Clear(signalParticlePools);
  }
}
