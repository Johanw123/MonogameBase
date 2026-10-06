#if !KNI_WEB
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.Input;
using UntitledGemGame.Entities;
using UntitledGemGame.Screens;
using UntitledGemGame.Systems;

namespace UntitledGemGame.Capture;

// The mouse as seen by gameplay during a capture. Positions are normalized frame
// coordinates; hidden parks the pointer off screen so no cursor ring is drawn.
public sealed class ScriptedPointer
{
  public Vector2 Position = new(0.5f, 0.5f);
  public bool Visible;
  public bool Left;
  public bool Right;
  private MouseState current;
  private MouseState previous;

  public MouseStateExtended State => new(current, previous);

  public void Commit()
  {
    previous = current;
    int x = Visible ? (int)MathF.Round(Position.X * CaptureSession.WindowWidth) : -100_000;
    int y = Visible ? (int)MathF.Round(Position.Y * CaptureSession.WindowHeight) : -100_000;
    current = new MouseState(x, y, 0, Left ? ButtonState.Pressed : ButtonState.Released, ButtonState.Released,
      Right ? ButtonState.Pressed : ButtonState.Released, ButtonState.Released, ButtonState.Released);
  }
}

internal static class Actions
{
  private static readonly Dictionary<string, string> AbilityNames = new(StringComparer.OrdinalIgnoreCase)
  {
    ["spawner"] = "GS1", ["genesis"] = "GS1", ["genesis pulse"] = "GS1",
    ["speed"] = "Speed1", ["ion"] = "Speed1", ["ion surge"] = "Speed1",
    ["magnet"] = "HBM1", ["tractor"] = "HBM1", ["tractor field"] = "HBM1",
    ["drones"] = "Drones1",
    ["chain"] = "CM1", ["graviton"] = "CM1", ["graviton cascade"] = "CM1",
    ["drill"] = "CoreDrill1", ["core drill"] = "CoreDrill1",
    ["kamikaze"] = "KW1", ["kamikaze wing"] = "KW1", ["bombers"] = "KW1"
  };

  public static readonly string[] AbilityIds = ["GS1", "Speed1", "HBM1", "Drones1", "CM1", "CoreDrill1", "KW1"];

  public static string AbilityId(string name) => AbilityNames.TryGetValue(name, out var id) ? id
    : AbilityIds.FirstOrDefault(i => i.Equals(name, StringComparison.OrdinalIgnoreCase))
      ?? throw new ArgumentException($"Unknown ability {name}; use {string.Join(", ", AbilityIds)} or {string.Join(", ", AbilityNames.Keys)}");

  public static int ManualSlot(string name)
  {
    if (int.TryParse(name, out int slot) && slot >= 0 && slot < ManualFleetAbilities.Definitions.Length) return slot;
    for (int i = 0; i < ManualFleetAbilities.Definitions.Length; i++)
      if (Slug(ManualFleetAbilities.Definitions[i].Name) == Slug(name)) return i;
    throw new ArgumentException($"Unknown manual ability {name}; use {string.Join(", ", ManualFleetAbilities.Definitions.Select(d => d.Name))}");
  }

  internal static string Slug(string name) => new(name.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

  private static readonly string[] Kinds =
    ["pointer", "hide", "click", "hold", "gravity", "click_gems", "ability", "manual", "upgrade", "level",
     "stat", "zoom", "panel", "prestige", "fracture", "new_run", "marker", "gems", "event"];

  // Catch typos before a long boot.
  public static void Validate(SceneAction action)
  {
    if (!Kinds.Contains(action.Do)) throw new ArgumentException($"Unknown action '{action.Do}'; use {string.Join(", ", Kinds)}");
    if (action.Pos != null && action.Pos.Length != 2) throw new ArgumentException($"{action.Do}: pos must be [x, y]");
    if (action.Target is not (null or "gems" or "home" or "planet" or "shard"))
      throw new ArgumentException($"{action.Do}: target must be gems, home, planet or shard");
    if (action.Do is "ability") AbilityId(Require(action, action.Id, "id"));
    if (action.Do is "manual") ManualSlot(Require(action, action.Id, "id"));
    if (action.Do is "upgrade" or "level" or "stat" or "panel") Require(action, action.Id, "id");
    if (action.Do is "stat" or "zoom" && action.Value == null) throw new ArgumentException($"{action.Do} needs a value");
    if (action.Do is "hold" or "gravity" or "click_gems" or "zoom" && action.Dur <= 0) throw new ArgumentException($"{action.Do} needs dur");
    if (action.Do is "event" && action.Id is not ("cannon" or "rockets" or "railgun"))
      throw new ArgumentException("event: id must be cannon, rockets or railgun");
    if (action.Do is "gems")
    {
      if (action.Points is not { Count: > 0 } || action.Points.Any(p => p.Length != 2))
        throw new ArgumentException("gems needs points [[x, y], ...] (normalized)");
      if (!Enum.TryParse<GemTypes>(action.Gem, true, out _))
        throw new ArgumentException($"Unknown gem {action.Gem}; use {string.Join(", ", Enum.GetNames<GemTypes>())}");
      if (action.From is not ("" or "home" or "edges")) throw new ArgumentException("gems: from must be \"\", home or edges");
      if (action.Order is not ("left" or "random" or "center")) throw new ArgumentException("gems: order must be left, random or center");
    }
    if (action.Do is "panel" && !Enum.TryParse<RenderGuiSystem.UpgradeTypes>(action.Id, true, out _))
      throw new ArgumentException($"Unknown panel {action.Id}; use {string.Join(", ", Enum.GetNames<RenderGuiSystem.UpgradeTypes>())}");
  }

  private static string Require(SceneAction action, string value, string field)
    => value ?? throw new ArgumentException($"{action.Do} needs {field}");

  // Starts an action; continuous ones return a behaviour run every frame until it returns true.
  public static Func<double, bool> Start(SceneAction action)
  {
    var pointer = CaptureSession.Pointer;
    var screen = UntitledGemGameGameScreen.Instance;
    double start = action.At;
    switch (action.Do)
    {
      case "pointer":
      {
        var from = pointer.Position;
        var to = Aim(action, from);
        pointer.Visible = true;
        if (action.Dur <= 0) { pointer.Position = to; return null; }
        return t => Glide(from, to, (t - start) / action.Dur);
      }
      case "hide":
        pointer.Visible = false;
        return null;
      case "click":
      {
        pointer.Position = Aim(action, pointer.Position);
        pointer.Visible = true;
        pointer.Left = true;
        LogClick(pointer);
        int frames = 0;
        return _ => { if (++frames < 2) return false; pointer.Left = false; return true; };
      }
      case "hold":
      case "gravity":
      {
        pointer.Position = Aim(action, pointer.Position);
        pointer.Visible = true;
        bool left = action.Do == "hold";
        if (left) pointer.Left = true; else pointer.Right = true;
        int frames = 0;
        return t =>
        {
          // The gravity well casts on a left click while the right button is held.
          if (!left) pointer.Left = ++frames == 2;
          if (t < start + action.Dur) return false;
          if (left) pointer.Left = false; else pointer.Right = false;
          return true;
        };
      }
      case "click_gems":
        return ClickGems(action);
      case "ability":
      {
        string id = AbilityId(action.Id);
        var ability = HomeBase.Instance.ActiveAbilities.FirstOrDefault(a => HomeBase.GetAbilityUpgradeId(a) == id)
          ?? throw new InvalidOperationException($"Ability {id} is not equipped; add it to save.equip");
        if (!ability.IsActive) ability.CooldownTime = 0;
        return null;
      }
      case "manual":
      {
        int slot = ManualSlot(action.Id);
        if (!screen.CaptureActivateManual(slot))
          throw new InvalidOperationException($"{ManualFleetAbilities.Definitions[slot].Name} is locked or recharging (needs more earnings this run?)");
        return null;
      }
      case "upgrade":
        Purchase(action.Id);
        return null;
      case "level":
        Staging.SetLevel(action.Id, action.Value);
        return null;
      case "stat":
        Staging.ApplyStats(new Dictionary<string, JsonElement> { [action.Id] = action.Value.Value });
        return null;
      case "zoom":
      {
        var upgrades = UpgradeManager.Instance.UG;
        float from = upgrades.CameraZoomScale;
        // Relative, like the scene zoom: 0.8 pulls out to show 25% more space.
        float to = from * action.Value.Value.GetSingle();
        return t =>
        {
          float progress = (float)Math.Clamp((t - start) / action.Dur, 0, 1);
          upgrades.CameraZoomScale = MathHelper.Lerp(from, to, progress * progress * (3 - 2 * progress));
          screen.m_camera.Zoom = upgrades.CameraZoomScale;
          return progress >= 1;
        };
      }
      case "panel":
        var panel = Enum.Parse<RenderGuiSystem.UpgradeTypes>(action.Id, true);
        // "abilities" (Ship Systems) takes an optional tab index as its value.
        if (panel == RenderGuiSystem.UpgradeTypes.Abilities && action.Value is { ValueKind: System.Text.Json.JsonValueKind.Number } tab)
          RenderGuiSystem.Instance.OpenShipSystems(tab.GetInt32());
        else
          RenderGuiSystem.Instance.SetUpgradeType(panel);
        return null;
      case "fracture":
        // A core fracture now, as if the damage had reached the next threshold.
        screen.StartCoreFracture();
        return null;
      case "prestige":
        // As completing the HUD hold does: extract the core, collapse and reset the run's upgrades.
        screen.ExtractCore();
        return null;
      case "new_run":
        screen.CaptureStartNewRun();
        return null;
      case "gems":
        return PlaceGems(action);
      case "event":
        screen.CaptureFireWeapon(action.Id);
        return null;
      default:
        return null;
    }
  }

  // The real purchase path, including its cost, sounds and button animation.
  private static void Purchase(string id)
  {
    var upgrades = UpgradeManager.CurrentUpgrades;
    var button = new[] { upgrades.UpgradeButtons, upgrades.UpgradeButtonsAbilities, upgrades.UpgradeButtonsMeta }
      .Select(buttons => buttons.GetValueOrDefault(id)).FirstOrDefault(b => b != null)
      ?? throw new ArgumentException($"Unknown upgrade {id} (see --capture-list)");
    var state = UntitledGemGameGameScreen.Instance.State;
    var before = (state.CurrentRedGemCount, state.CurrentBlueGemCount, state.CurrentPurpleGemCount, state.CurrentCoreShardCount);
    UpgradeManager.Instance.Upgrade(button);
    if ((state.CurrentRedGemCount, state.CurrentBlueGemCount, state.CurrentPurpleGemCount, state.CurrentCoreShardCount) == before)
      throw new InvalidOperationException($"Upgrade {id} was not bought (locked, maxed or not enough currency)");
    Staging.ReapplyStats();
  }

  private static Vector2 Aim(SceneAction action, Vector2 fallback)
  {
    var screen = UntitledGemGameGameScreen.Instance;
    if (action.Ui != null)
    {
      var target = RenderGuiSystem.Instance.CaptureTarget(action.Ui)
        ?? throw new InvalidOperationException($"UI target {action.Ui} not found (panel closed or unknown); targets: "
          + string.Join(", ", RenderGuiSystem.CaptureTargetNames));
      return new Vector2(target.Center.X / (float)CaptureSession.WindowWidth, target.Center.Y / (float)CaptureSession.WindowHeight);
    }
    Vector2? world = action.Target switch
    {
      "home" => UntitledGemGameGameScreen.HomeBasePos,
      "planet" => UntitledGemGameGameScreen.PlanetPos,
      "shard" => screen.CaptureShardPosition,
      "gems" => HarvesterCollectionSystem.Instance.flatSpatialHash.TryGetWeightedClusterPosition(Random.Shared, out var cluster)
        ? cluster : null,
      _ => null
    };
    if (world is Vector2 w)
    {
      var window = screen.m_camera.WorldToScreen(w);
      return new Vector2(window.X / CaptureSession.WindowWidth, window.Y / CaptureSession.WindowHeight);
    }
    return action.Pos != null ? new Vector2((float)action.Pos[0], (float)action.Pos[1]) : fallback;
  }

  private static bool Glide(Vector2 from, Vector2 to, double progress)
  {
    float p = (float)Math.Clamp(progress, 0, 1);
    CaptureSession.Pointer.Position = Vector2.Lerp(from, to, p * p * (3 - 2 * p));
    return p >= 1;
  }

  // A player clicking through the field: aim at a real gem near the pointer (denser spots preferred, never the
  // spot just cleared), glide there at a hand-like speed (longer moves take longer), click on arrival and move
  // straight on. The pointer follows a drifting target and re-aims if a ship collects it first.
  private static Func<double, bool> ClickGems(SceneAction action)
  {
    var pointer = CaptureSession.Pointer;
    var screen = UntitledGemGameGameScreen.Instance;
    var grid = HarvesterCollectionSystem.Instance.flatSpatialHash;
    pointer.Visible = true;
    double interval = 1 / Math.Max(0.2, action.Rate);
    // Keep clear of the HUD strip when it is drawn.
    float maxY = CaptureSession.ReserveHudSpace && CaptureSession.Scene.Hud ? 0.84f : 0.96f;
    Vector2 from = pointer.Position, to = from;
    int target = -1;
    double moveStart = action.At, moveDuration = 0, nextMove = action.At;
    bool moving = false;

    Vector2 ToPointer(Vector2 world)
    {
      var window = screen.m_camera.WorldToScreen(world);
      return new Vector2(window.X / CaptureSession.WindowWidth, window.Y / CaptureSession.WindowHeight);
    }
    bool Clickable(Vector2 p) => p.X > 0.04f && p.X < 0.96f && p.Y > 0.05f && p.Y < maxY;

    int Pick(Vector2 at)
    {
      var world = screen.m_camera.ScreenToWorld(new Vector2(at.X * CaptureSession.WindowWidth, at.Y * CaptureSession.WindowHeight));
      var bounds = PlayAreaBounds.ForCamera(screen.m_camera);
      float span = (bounds.Maximum - bounds.Minimum).Length();
      float click = screen.GemClickRadius;
      foreach (float reach in new[] { span * 0.1f, span * 0.25f, span })
      {
        int best = -1, seen = 0;
        double bestScore = 0;
        foreach (int index in grid.Query(world.X, world.Y, reach, reach))
        {
          if (++seen > 400) break;
          var gem = new Vector2(grid.Gems[index].X, grid.Gems[index].Y);
          float distance = Vector2.Distance(gem, world);
          if (distance < click || !Clickable(ToPointer(gem))) continue;
          int neighbours = 0;
          foreach (int _ in grid.Query(gem.X, gem.Y, click, click))
            if (++neighbours >= 12) break;
          double score = Math.Pow(neighbours, 1.3) / (distance + span * 0.02) * (0.7 + 0.6 * Random.Shared.NextDouble());
          if (score > bestScore) { bestScore = score; best = index; }
        }
        if (best >= 0) return best;
      }
      return -1;
    }

    void Move(double t)
    {
      from = pointer.Position;
      target = Pick(from);
      if (target < 0) { nextMove = t + 0.2; return; }
      to = ToPointer(new Vector2(grid.Gems[target].X, grid.Gems[target].Y));
      float pixels = Vector2.Distance(from * new Vector2(CaptureSession.WindowWidth, CaptureSession.WindowHeight),
        to * new Vector2(CaptureSession.WindowWidth, CaptureSession.WindowHeight));
      float diagonal = new Vector2(CaptureSession.WindowWidth, CaptureSession.WindowHeight).Length();
      moveDuration = Math.Clamp(0.1 + 0.6 * pixels / diagonal, 0.12, 0.45) * (0.9 + 0.2 * Random.Shared.NextDouble());
      moveStart = t;
      moving = true;
    }

    return t =>
    {
      pointer.Left = false;
      if (t >= action.At + action.Dur) return true;
      if (!moving)
      {
        if (t >= nextMove) Move(t);
        return false;
      }
      ref var gem = ref grid.Gems[target];
      if (!gem.IsActive || gem.ClaimState != 0) { Move(t); return false; }
      to = ToPointer(new Vector2(gem.X, gem.Y));
      double progress = (t - moveStart) / moveDuration;
      Glide(from, to, progress);
      if (progress < 1) return false;
      pointer.Left = true;
      LogClick(pointer);
      moving = false;
      nextMove = t + Math.Max(0.03, interval - moveDuration) * (0.6 + 0.6 * Random.Shared.NextDouble());
      return false;
    };
  }

  // Real gems at exact frame positions (text, shapes). With "from", each gem is launched so the
  // game's own launch motion (velocity decaying at e^-8t) stops it on its point; spawns are spread over dur.
  private static Func<double, bool> PlaceGems(SceneAction action)
  {
    var screen = UntitledGemGameGameScreen.Instance;
    var type = Enum.Parse<GemTypes>(action.Gem, true);
    var targets = action.Points.Select(p => screen.m_camera.ScreenToWorld(
      new Vector2((float)p[0] * CaptureSession.WindowWidth, (float)p[1] * CaptureSession.WindowHeight))).ToList();
    var centre = targets.Aggregate(Vector2.Zero, (sum, p) => sum + p) / targets.Count;
    targets = action.Order switch
    {
      "random" => targets.OrderBy(_ => Random.Shared.Next()).ToList(),
      "center" => targets.OrderBy(p => Vector2.DistanceSquared(p, centre)).ToList(),
      _ => targets.OrderBy(p => p.X).ThenBy(p => p.Y).ToList()
    };
    var bounds = PlayAreaBounds.ForCamera(screen.m_camera);
    float homeRange = BaseStats.GetHarvesterCollectionRange(HomeBase.Instance.Entity.Get<Harvester>());
    Vector2 Origin(Vector2 target) => action.From switch
    {
      // Just outside the homebase's pickup range, or it collects them on the spot.
      "home" => UntitledGemGameGameScreen.HomeBasePos + Vector2.Normalize(target - UntitledGemGameGameScreen.HomeBasePos
        + new Vector2(0.01f, 0)) * homeRange * 1.15f,
      // Straight out from the frame centre to the nearest edge, so paths do not cross the text.
      "edges" => bounds.Clamp(target + Vector2.Normalize(target - UntitledGemGameGameScreen.HomeBasePos + new Vector2(0.01f, 0))
        * (bounds.Maximum - bounds.Minimum).Length()),
      _ => target
    };
    int spawned = 0;
    double start = action.At;
    return t =>
    {
      int due = action.Dur <= 0 ? targets.Count
        : Math.Min(targets.Count, (int)Math.Ceiling((t - start) / action.Dur * targets.Count));
      for (; spawned < due; spawned++)
      {
        var target = targets[spawned];
        var origin = Origin(target);
        EntityFactory.Instance.QueueGemSpawn(origin, type, 1, launchVelocity: (target - origin) * 8f);
      }
      return spawned >= targets.Count;
    };
  }

  private static void LogClick(ScriptedPointer pointer)
    => CaptureSession.Log("click", null, [Math.Round(pointer.Position.X, 4), Math.Round(pointer.Position.Y, 4)]);
}
#endif
