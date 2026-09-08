using Microsoft.Xna.Framework;
using MonoGame.Extended.ECS;
using UntitledGemGame.Entities;
using UntitledGemGame.Systems;

internal static class SleepingGemChecks
{
  private sealed class Probe : UpdateSystem2
  {
    public Probe() : base(null) { }
    // Exercise ECS lifecycle and wakeups without camera/input/content dependencies.
    public override void Update(GameTime gameTime) { }
  }

  private sealed class RenderProbe : RenderGemSystem
  {
    public RenderProbe() : base(null, null, null, null) { }
    public override void Initialize(IComponentMapperService mapperService)
    {
      // Supply real ECS mappers without loading the renderer's shader/assets.
      const System.Reflection.BindingFlags fields = System.Reflection.BindingFlags.Instance
        | System.Reflection.BindingFlags.NonPublic;
      typeof(RenderGemSystem).GetField("_sprites", fields)!.SetValue(this,
        mapperService.GetMapper<MonoGame.Extended.Graphics.Sprite>());
      typeof(RenderGemSystem).GetField("_transforms", fields)!.SetValue(this,
        mapperService.GetMapper<MonoGame.Extended.Transform2>());
      typeof(RenderGemSystem).GetField("_gems", fields)!.SetValue(this, mapperService.GetMapper<Gem>());
    }
  }

  public static void Run()
  {
    var system = new Probe();
    var renderer = new RenderProbe();
    using var world = new WorldBuilder().AddSystem(system).AddSystem(renderer).Build();
    // The actual world contains non-gem entities too (ships, the base and effects).
    var ship = world.CreateEntity();
    ship.Attach(new MonoGame.Extended.Transform2());
    ship.Attach(new Harvester());
    // No texture is needed: a non-gem sprite must never enter the gem render batch.
    ship.Attach((MonoGame.Extended.Graphics.Sprite)System.Runtime.CompilerServices.RuntimeHelpers
      .GetUninitializedObject(typeof(MonoGame.Extended.Graphics.Sprite)));
    world.CreateEntity();
    var entity = world.CreateEntity();
    var gem = new Gem { Id = entity.Id };
    entity.Attach(gem);
    var frame = new GameTime();
    world.Update(frame);
    if (system.UpdatingGemCount != 1) throw new Exception("New gems must enter the animation update list");
    ship.Destroy();
    world.Update(frame);
    if (system.UpdatingGemCount != 1) throw new Exception("Removing a non-gem must not retire a gem update entry");
    var sleep = typeof(UpdateSystem2).GetMethod("Sleep",
      System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
    sleep.Invoke(system, new object[] { gem });
    if (system.UpdatingGemCount != 0) throw new Exception("Settled gems must leave the update list");
    gem.SetAnimation(Vector2.Zero, Vector2.Zero, true);
    gem.SetAnimation(Vector2.One, Vector2.Zero, false);
    if (system.UpdatingGemCount != 1) throw new Exception("Animation wakeups must be unique");
    sleep.Invoke(system, new object[] { gem });
    gem.ShouldDestroy = true;
    if (system.UpdatingGemCount != 1) throw new Exception("Prestige must wake sleeping gems for destruction");
    entity.Destroy();
    world.Update(frame);
    if (system.UpdatingGemCount != 0) throw new Exception("ECS removal must retire gem update entries");
    gem.Reset();
    gem.SetAnimation(Vector2.One, Vector2.Zero, false);
    if (system.UpdatingGemCount != 0) throw new Exception("A pooled gem must not wake until registered again");
    var reusedEntity = world.CreateEntity();
    gem.Id = reusedEntity.Id;
    reusedEntity.Attach(gem);
    world.Update(frame);
    if (system.UpdatingGemCount != 1) throw new Exception("Reused gems must resume spawn animations");
    reusedEntity.Destroy();
    world.Update(frame);
    renderer.DisposeBuffers();
    Console.WriteLine("Sleeping gem checks passed: mixed-world registration/removal, animation/destruction wakeups and pooled reuse.");
  }
}
