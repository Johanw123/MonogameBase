using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using UntitledGemGame;
using UntitledGemGame.Screens;
using UntitledGemGame.Systems;

internal static class WeaponLoadChecks
{
  private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

  public static void Run()
  {
    var manager = new UpgradeManager();
    var screen = (UntitledGemGameGameScreen)RuntimeHelpers.GetUninitializedObject(typeof(UntitledGemGameGameScreen));
    void Set(string name, object value) => typeof(UntitledGemGameGameScreen).GetField(name, Private)!.SetValue(screen, value);
    object Get(string name) => typeof(UntitledGemGameGameScreen).GetField(name, Private)!.GetValue(screen)!;
    var yield = typeof(UntitledGemGameGameScreen).GetMethod("WeaponHitYield", Private)!;
    int Hit(int damage) => (int)yield.Invoke(screen, [damage])!;

    Set("weaponYieldBonus", 2.25f);
    if (Hit(1_500_000_000) != int.MaxValue)
      throw new Exception("Large weapon hits must saturate at the damage limit");
    if (Convert.ToDouble(Get("weaponYieldCarry")) >= 1 || Hit(1) != 3)
      throw new Exception("Saturated hits must not leave whole bonus damage for later laser hits");
    if (Hit(1) != 3 || Hit(1) != 3 || Hit(1) != 4)
      throw new Exception("Ordinary hits must still carry fractional bonus damage");

    var previousFleet = HarvesterCollectionSystem.Instance;
    try
    {
      // A full field must discard unavailable debris without iterating billions of damage points.
      var fleet = (HarvesterCollectionSystem)RuntimeHelpers.GetUninitializedObject(typeof(HarvesterCollectionSystem));
      fleet.flatSpatialHash = new GemSpatialIndex(1, 30);
      fleet.flatSpatialHash.AddGem(1, 0, 0, 1);
      HarvesterCollectionSystem.Instance = fleet;
      manager.UG.MaxGemCount = 1;
      var factory = (EntityFactory)RuntimeHelpers.GetUninitializedObject(typeof(EntityFactory));
      typeof(EntityFactory).GetField("_gemSpawnQueue", Private)!.SetValue(factory, new Queue<GemSpawnData>());
      var state = new GameState { ShellDamage = PlanetShell.Health };
      Set("m_gameState", state);
      Set("planetDamage", new RollingMinute());
      Set("m_entityFactory", factory);
      Set("deferredDebris", new Queue<GemSpawnData>());
      var field = typeof(UntitledGemGameGameScreen).GetField("shockwaves", Private)!;
      var waves = (IList)Activator.CreateInstance(field.FieldType)!;
      field.SetValue(screen, waves);
      var waveType = field.FieldType.GetGenericArguments()[0];
      object AddWave(int damage)
      {
        var wave = Activator.CreateInstance(waveType, nonPublic: true)!;
        waveType.GetField("Source")!.SetValue(wave, PlanetDamageSource.TectonicShockwave);
        waveType.GetField("Gems")!.SetValue(wave, damage);
        waves.Add(wave);
        return wave;
      }
      var wave = AddWave(int.MaxValue);
      var update = typeof(UntitledGemGameGameScreen).GetMethod("UpdateWeaponSpecials", Private)!;
      var timer = Stopwatch.StartNew();
      update.Invoke(screen, [0.1f, default(PlayAreaBounds)]);
      int emitted = (int)waveType.GetField("Emitted")!.GetValue(wave)!;
      if (emitted <= 0 || state.Damage.TotalThisRun != emitted || factory.PendingGemSpawnCount != 0)
        throw new Exception("A full field must record shockwave damage without spawning debris");
      update.Invoke(screen, [2f, default(PlayAreaBounds)]);
      if (state.Damage.TotalThisRun != int.MaxValue || waves.Count != 0)
        throw new Exception("Shockwaves must record their exact full payload and retire, including int.MaxValue");
      if (timer.Elapsed > TimeSpan.FromSeconds(1))
        throw new Exception("Full-field shockwave work must be independent of damage magnitude");
    }
    finally { HarvesterCollectionSystem.Instance = previousFleet; }
    Console.WriteLine("Weapon load checks passed: saturation, fractional bonuses and full-field shockwaves.");
  }
}
