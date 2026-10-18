#if !KNI_WEB
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Threading.Tasks;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Vector2 = System.Numerics.Vector2;
using Vector4 = System.Numerics.Vector4;

namespace UntitledGemGame;

internal sealed class DebugSoundEffects : IDisposable
{
  private sealed class Slot
  {
    public string Input = "";
    public string Source;
    public byte[] Wave;
    public Task<byte[]> Loading;
    public string Error;
    public string Status;
  }

  private readonly Dictionary<SoundEvent, Slot> slots = Enum.GetValues<SoundEvent>().ToDictionary(e => e, _ => new Slot());
  private static readonly AudioManager.SoundType[] SoundTypes = Enum.GetValues<AudioManager.SoundType>();
  private static readonly string[] SoundTypeNames = SoundTypes.Select(t => t.ToString()).ToArray();
  private static readonly (string Name, SoundEvent[] Events)[] Groups = new[]
  {
    "Menu & UI", "Gems & Cursor", "Harvesters & Refueling", "Shipyard & Modules", "Signals",
    "Ship & Intro", "Abilities & Ship Systems", "Weapons", "Planet & Core", "Prestige & Time Loop", "Other"
  }.Select(name => (Name: name, Events: Enum.GetValues<SoundEvent>().Where(e => GroupName(e) == name).ToArray()))
    .Where(group => group.Events.Length > 0).ToArray();
  private readonly HashSet<string> expandedGroups = new(StringComparer.Ordinal);
  private readonly HashSet<string> searchCollapsedGroups = new(StringComparer.Ordinal);
  private string previousFilter = "";
  private readonly GameWindow window;
  private readonly List<(SoundEvent Event, Vector2 Min, Vector2 Max)> targets = new();
  private string search = "";
  private string dropError;
  private Vector2 panelMin, panelMax;
  private bool panelVisible, editingText, capturingMouse;
  internal bool Open;

  private static string GroupName(SoundEvent soundEvent) => soundEvent switch
  {
    SoundEvent.MenuHover or SoundEvent.MenuClick or SoundEvent.TooltipShow
      or SoundEvent.WindowOpen or SoundEvent.WindowClose or SoundEvent.UpgradeStart
      or SoundEvent.UpgradeComplete or SoundEvent.UpgradeError or SoundEvent.UpgradeRefund => "Menu & UI",
    SoundEvent.GemPickup or SoundEvent.GemClick or SoundEvent.GemMerge
      or SoundEvent.CursorGravityCollapse or SoundEvent.CursorGravityReady => "Gems & Cursor",
    SoundEvent.RefuelRequest or SoundEvent.RefuelStart or SoundEvent.RefuelLoop or SoundEvent.RefuelComplete
      or SoundEvent.HarvesterDeliverCargo or SoundEvent.HarvesterWarpDrive or SoundEvent.JackpotHaul => "Harvesters & Refueling",
    SoundEvent.ModuleSalvageReady or SoundEvent.ModuleRevealStart or SoundEvent.ModuleRevealCommon
      or SoundEvent.ModuleRevealEpic or SoundEvent.ModuleRevealLegendary
      or SoundEvent.ModuleEquip or SoundEvent.ModuleUnequip => "Shipyard & Modules",
    SoundEvent.SignalScan or SoundEvent.SignalChoose => "Signals",
    SoundEvent.ShipEngineDying or SoundEvent.ShipCrashLand => "Ship & Intro",
    SoundEvent.ManualAbilityActivate or SoundEvent.AbilitySpeedboostActivate or SoundEvent.AbilityMagnetActivate
      or SoundEvent.AbilityChainLightningActivate or SoundEvent.AbilityDroneSwarmActivate
      or SoundEvent.AbilityGemSpawnerActivate or SoundEvent.CoreDrillLaunch or SoundEvent.CoreDrillImpact
      or SoundEvent.CoreDrillRupture or SoundEvent.CoreDrillCoreTap => "Abilities & Ship Systems",
    SoundEvent.CannonFireManual or SoundEvent.CannonFireAuto or SoundEvent.RocketLaunch
      or SoundEvent.RocketClusterSplit or SoundEvent.LaserOverheatSurge or SoundEvent.RailgunWindUp
      or SoundEvent.RailgunFire or SoundEvent.ArcHarpoonLaunch or SoundEvent.ArcHarpoonPulse
      or SoundEvent.ArcHarpoonRelease or SoundEvent.CannonImpact or SoundEvent.CannonCriticalImpact
      or SoundEvent.CannonRicochet or SoundEvent.RocketImpact or SoundEvent.RocketMiniImpact
      or SoundEvent.ArcHarpoonImpact or SoundEvent.RailgunImpact or SoundEvent.PlanetCrackerStart
      or SoundEvent.KamikazeWingLaunch or SoundEvent.KamikazeImpact or SoundEvent.KamikazeBombletImpact
      or SoundEvent.KamikazeDoomsdayImpact or SoundEvent.MagmaDetonation or SoundEvent.PlanetaryOverloadErupt
      or SoundEvent.WeakPointBurst or SoundEvent.WeaponJackpot => "Weapons",
    SoundEvent.PlanetShellShatterStart or SoundEvent.PlanetShellBurst or SoundEvent.CoreFractureStart
      or SoundEvent.CoreFractureHeartbeat or SoundEvent.CoreFractureSwallow or SoundEvent.CoreFractureEruption
      or SoundEvent.CoreShardRelease or SoundEvent.CoreShardClick or SoundEvent.CoreShardCollect => "Planet & Core",
    SoundEvent.PrestigePointEarned or SoundEvent.PrestigePointLanded or SoundEvent.CoreExtractionStart
      or SoundEvent.TimeLoopCollapseBuildup or SoundEvent.TimeLoopCollapseImplode
      or SoundEvent.TimeLoopPlanetSwallowed or SoundEvent.TimeLoopRestart => "Prestige & Time Loop",
    _ => "Other"
  };

  internal bool CapturesInput
  {
    get
    {
      if (!Open || !panelVisible) return false;
      var mouse = Mouse.GetState();
      return editingText || capturingMouse
        || (mouse.X >= panelMin.X && mouse.Y >= panelMin.Y && mouse.X < panelMax.X && mouse.Y < panelMax.Y);
    }
  }

  internal DebugSoundEffects(GameWindow window)
  {
    this.window = window;
    window.FileDrop += FileDrop;
  }

  internal static string FindSourceContentDirectory()
  {
    foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
      for (var directory = new DirectoryInfo(start); directory != null; directory = directory.Parent)
        if (File.Exists(Path.Combine(directory.FullName, "UntitledGemGame.csproj"))
          && Directory.Exists(Path.Combine(directory.FullName, "Content")))
          return Path.Combine(directory.FullName, "Content");
    return null;
  }

  private void FileDrop(object sender, FileDropEventArgs args)
  {
    if (!Demo.IsDev || !Open) return;
    var mouse = Mouse.GetState();
    var point = new Vector2(mouse.X, mouse.Y);
    foreach (var target in targets)
      if (point.X >= target.Min.X && point.Y >= target.Min.Y && point.X < target.Max.X && point.Y < target.Max.Y)
      {
        if (args.Files.Length != 1) { dropError = "Drop one sound file at a time."; return; }
        BeginImport(target.Event, args.Files[0]);
        dropError = null;
        return;
      }
    dropError = "Drop the file on an event's sound slot.";
  }

  private void BeginImport(SoundEvent soundEvent, string path)
  {
    var slot = slots[soundEvent];
    if (slot.Loading != null) return;
    slot.Input = path.Trim().Trim('"');
    slot.Error = null;
    slot.Status = "Loading...";
    string source = slot.Input;
    slot.Loading = Task.Run(() => SoundEffectImport.ReadWaveAsync(source));
  }

  internal void Update()
  {
    foreach (var (soundEvent, slot) in slots)
    {
      if (slot.Loading == null || !slot.Loading.IsCompleted) continue;
      try
      {
        byte[] wave = slot.Loading.GetAwaiter().GetResult();
        AudioManager.Instance.DebugReplace(soundEvent, wave, slot.Input);
        slot.Wave = wave;
        slot.Source = slot.Input;
        slot.Status = "Live override (unsaved)";
      }
      catch (Exception error) { slot.Error = error.Message; slot.Status = null; }
      finally { slot.Loading = null; }
    }
  }

  private void Save(SoundEvent soundEvent)
  {
    var slot = slots[soundEvent];
    string content = FindSourceContentDirectory() ?? throw new IOException("Source Content folder not found. Run from the game project to save defaults.");
    string manifest = Path.Combine(content, SoundEffectDefaults.ManifestPath);
    Dictionary<string, SoundCue[]> defaults;
    using (var stream = File.OpenRead(manifest)) defaults = SoundEffectDefaults.Read(stream);
    var cues = AudioManager.Instance.DebugCues(soundEvent).ToArray();
    if (slot.Wave != null)
    {
      // Never overwrite a startup asset: Revert must keep that exact sound for this session.
      string hash = Convert.ToHexString(SHA256.HashData(slot.Wave)).ToLowerInvariant();
      string asset = $"{SoundEffectDefaults.ImportedDirectory}/{soundEvent}-{hash[..16]}.wav";
      string destination = Path.Combine(content, asset);
      Directory.CreateDirectory(Path.GetDirectoryName(destination));
      if (!File.Exists(destination)) File.WriteAllBytes(destination, slot.Wave);
      cues[0] = cues[0] with { Path = asset };
    }
    if (AudioManager.Instance.DebugIsBuiltIn(soundEvent, cues)) defaults.Remove(soundEvent.ToString());
    else defaults[soundEvent.ToString()] = cues;
    SoundEffectDefaults.Write(content, defaults);
    slot.Status = "Saved as default";
  }

  private void Revert(SoundEvent soundEvent)
  {
    AudioManager.Instance.DebugRevert(soundEvent);
    var slot = slots[soundEvent];
    slot.Wave = null; slot.Source = null; slot.Input = ""; slot.Error = null; slot.Status = "Startup assignment restored";
  }

  private void SetSilent(SoundEvent soundEvent)
  {
    AudioManager.Instance.DebugSetSilent(soundEvent);
    var slot = slots[soundEvent];
    slot.Wave = null; slot.Source = null; slot.Input = ""; slot.Error = null; slot.Status = "Silent (unsaved)";
  }

  internal void Draw()
  {
    targets.Clear();
    panelVisible = false;
    editingText = false;
    capturingMouse = false;
    if (ImGui.IsKeyPressed(ImGuiKey.F8, false)) Open = !Open;
    if (!Open) return;
    ImGui.SetNextWindowSize(new Vector2(1180, 740), ImGuiCond.FirstUseEver);
    ImGui.SetNextWindowSizeConstraints(new Vector2(900, 400), new Vector2(float.MaxValue, float.MaxValue));
    ImGui.SetNextWindowBgAlpha(1f);
    bool expanded = ImGui.Begin("Sound effects lab (F8)", ref Open);
    panelVisible = true;
    panelMin = ImGui.GetWindowPos();
    panelMax = panelMin + ImGui.GetWindowSize();
    capturingMouse = ImGui.GetIO().WantCaptureMouse;
    if (!expanded) { ImGui.End(); return; }
    ImGui.TextWrapped("Drop a file onto the named blue target to replace that event immediately. Adjust the controls to tune gameplay and Preview.");
    ImGui.TextWrapped("Save keeps the sound and parameters for future runs. Revert restores this launch's assignment. Set silent mutes the event; Save makes silence the default.");
    ImGui.PushTextWrapPos();
    ImGui.TextDisabled("Preview uses your SFX volume and includes pitch jitter. WAV loads directly; other formats need FFmpeg. A drop replaces all layers; existing layers can be tuned individually.");
    ImGui.PopTextWrapPos();
    ImGui.SetNextItemWidth(320);
    ImGui.InputTextWithHint("##filter", "Filter events...", ref search, 128);
    ImGui.SameLine();
    if (ImGui.Button("Expand all"))
    {
      search = "";
      foreach (var group in Groups) expandedGroups.Add(group.Name);
    }
    ImGui.SameLine();
    if (ImGui.Button("Collapse all")) { search = ""; expandedGroups.Clear(); }
    ImGui.SameLine();
    if (ImGui.Button("Stop preview")) AudioManager.Instance.DebugStopPreviews();
    if (dropError != null) ImGui.TextWrapped(dropError);
    ImGui.Separator();
    ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.07f, 0.09f, 0.13f, 1f));
    ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(0.29f, 0.36f, 0.46f, 1f));
    ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 6f);
    ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(12, 10));
    if (ImGui.BeginChild("event-list", new Vector2(0, 0)))
    {
      int index = 0;
      string filter = search.Trim();
      bool filtering = filter.Length > 0;
      if (filter != previousFilter) { searchCollapsedGroups.Clear(); previousFilter = filter; }
      bool anyMatches = false;
      foreach (var group in Groups)
      {
        var matches = group.Events.Where(e => !filtering || group.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
          || e.ToString().Contains(filter, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (matches.Length == 0) continue;
        anyMatches = true;
        // Search opens matches temporarily without changing the user's section choices.
        ImGui.SetNextItemOpen(filtering ? !searchCollapsedGroups.Contains(group.Name) : expandedGroups.Contains(group.Name), ImGuiCond.Always);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(10, 8));
        bool expandedGroup = ImGui.CollapsingHeader($"{group.Name} ({matches.Length})###sound-group-{group.Name}");
        ImGui.PopStyleVar();
        if (filtering)
        {
          if (expandedGroup) searchCollapsedGroups.Remove(group.Name);
          else searchCollapsedGroups.Add(group.Name);
        }
        else
        {
          if (expandedGroup) expandedGroups.Add(group.Name);
          else expandedGroups.Remove(group.Name);
        }
        if (expandedGroup)
        {
          ImGui.Indent(8);
          ImGui.Spacing();
          foreach (var soundEvent in matches)
          {
            DrawCard(soundEvent, slots[soundEvent], index++);
            ImGui.Dummy(new Vector2(0, 12));
          }
          ImGui.Unindent(8);
        }
        ImGui.Spacing();
      }
      if (!anyMatches) ImGui.TextDisabled("No sound events match this filter.");
    }
    ImGui.EndChild();
    ImGui.PopStyleVar(2);
    ImGui.PopStyleColor(2);
    editingText = ImGui.GetIO().WantTextInput;
    ImGui.End();
  }

  private void DrawCard(SoundEvent soundEvent, Slot slot, int index)
  {
    ImGui.PushID((int)soundEvent);
    ImGui.PushStyleColor(ImGuiCol.ChildBg, index % 2 == 0
      ? new Vector4(0.10f, 0.12f, 0.16f, 1f) : new Vector4(0.14f, 0.16f, 0.20f, 1f));
    bool visible = ImGui.BeginChild("card", new Vector2(0, 0),
      ImGuiChildFlags.Borders | ImGuiChildFlags.AutoResizeY | ImGuiChildFlags.AlwaysUseWindowPadding,
      ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
    if (visible)
    {
      ImGui.AlignTextToFramePadding();
      ImGui.TextColored(new Vector4(0.65f, 0.83f, 1f, 1f), soundEvent.ToString());
      float actionsWidth = ImGui.CalcTextSize("RevertPreviewSaveSet silent").X
        + ImGui.GetStyle().FramePadding.X * 8 + ImGui.GetStyle().ItemSpacing.X * 3;
      ImGui.SameLine(Math.Max(ImGui.GetCursorPosX(), ImGui.GetWindowWidth() - actionsWidth - 12));
      ImGui.BeginDisabled(slot.Loading != null);
      try
      {
        if (ImGui.Button("Revert")) Revert(soundEvent);
        ImGui.SameLine();
        if (ImGui.Button("Preview")) AudioManager.Instance.DebugPreview(soundEvent);
        ImGui.SameLine();
        if (ImGui.Button("Save")) { Save(soundEvent); slot.Error = null; }
        ImGui.SameLine();
        if (ImGui.Button("Set silent")) SetSilent(soundEvent);
      }
      catch (Exception error) { slot.Error = error.Message; }
      ImGui.EndDisabled();
      var mixRule = SoundMix.Rule(soundEvent);
      ImGui.TextDisabled($"Gameplay mix: {mixRule.Bus}, event spacing {mixRule.Cooldown:0.###}s. Preview bypasses mix limits.");
      ImGui.Separator();
      if (ImGui.BeginTable("content", 2, ImGuiTableFlags.SizingStretchProp))
      {
        ImGui.TableSetupColumn("Sound", ImGuiTableColumnFlags.WidthStretch, 0.45f);
        ImGui.TableSetupColumn("Parameters", ImGuiTableColumnFlags.WidthStretch, 0.55f);
        ImGui.TableNextColumn();
        DrawDropTarget(soundEvent, slot);
        ImGui.TableNextColumn();
        ImGui.BeginDisabled(slot.Loading != null);
        var cues = AudioManager.Instance.DebugCues(soundEvent);
        for (int layer = 0; layer < cues.Count; layer++)
        {
          ImGui.PushID(layer);
          if (cues.Count > 1) ImGui.TextUnformatted($"Layer {layer + 1}: {Path.GetFileName(cues[layer].Path)}");
          DrawParameters(soundEvent, layer, cues[layer], slot);
          ImGui.PopID();
        }
        ImGui.EndDisabled();
        ImGui.EndTable();
      }
      if (slot.Error != null) ImGui.TextWrapped(slot.Error);
      else if (slot.Status != null) ImGui.TextColored(new Vector4(0.85f, 0.77f, 0.46f, 1f), slot.Status);
    }
    ImGui.EndChild();
    ImGui.PopStyleColor();
    ImGui.PopID();
  }

  private void DrawDropTarget(SoundEvent soundEvent, Slot slot)
  {
    var cues = AudioManager.Instance.DebugCues(soundEvent);
    string file = slot.Source != null ? Path.GetFileName(slot.Source)
      : string.Join(" + ", cues.Select(c => c.Path == null ? "(silent)" : Path.GetFileName(c.Path)));
    ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.15f, 0.28f, 0.43f, 1f));
    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.24f, 0.46f, 0.65f, 1f));
    ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 4f);
    ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1f);
    ImGui.Button($"Drop sound for {soundEvent}\n{file}##drop", new Vector2(-1, 60));
    if (ImGui.IsItemHovered())
      ImGui.SetTooltip($"Assign a new sound to {soundEvent}\n" + (slot.Source ?? string.Join("\n", cues.Select(c => c.Path ?? "Silent placeholder"))));
    if (ImGui.IsItemVisible())
    {
      var min = Vector2.Max(ImGui.GetItemRectMin(), ImGui.GetWindowDrawList().GetClipRectMin());
      var max = Vector2.Min(ImGui.GetItemRectMax(), ImGui.GetWindowDrawList().GetClipRectMax());
      targets.Add((soundEvent, min, max));
      if (ImGui.IsItemHovered())
        ImGui.GetWindowDrawList().AddRect(min, max, ImGui.GetColorU32(new Vector4(0.5f, 0.83f, 1f, 1f)), 4f, ImDrawFlags.None, 2f);
    }
    ImGui.PopStyleVar(2);
    ImGui.PopStyleColor(2);
    ImGui.BeginDisabled(slot.Loading != null);
    ImGui.SetNextItemWidth(Math.Max(50, ImGui.GetContentRegionAvail().X - 55));
    bool enter = ImGui.InputTextWithHint("##path", "Paste full path...", ref slot.Input, 4096, ImGuiInputTextFlags.EnterReturnsTrue);
    ImGui.SameLine();
    if (ImGui.Button("Load") || enter) BeginImport(soundEvent, slot.Input);
    ImGui.EndDisabled();
  }

  private void DrawParameters(SoundEvent soundEvent, int layer, SoundCue cue, Slot slot)
  {
    if (!ImGui.BeginTable("parameters", 3, ImGuiTableFlags.SizingStretchSame)) return;
    float volume = cue.Volume, pitch = cue.Pitch, jitter = cue.PitchJitter, pan = cue.Pan, priority = cue.Priority;
    int type = Array.IndexOf(SoundTypes, cue.Type);
    ImGui.TableNextColumn();
    ImGui.TextUnformatted("Volume"); ImGui.SetNextItemWidth(-1);
    bool volumeChanged = ImGui.SliderFloat("##volume", ref volume, 0f, 2f, "%.2f", ImGuiSliderFlags.AlwaysClamp);
    bool changed = volumeChanged;
    ImGui.TableNextColumn();
    ImGui.TextUnformatted("Pitch"); ImGui.SetNextItemWidth(-1);
    changed |= ImGui.SliderFloat("##pitch", ref pitch, -1f, 1f, "%.2f", ImGuiSliderFlags.AlwaysClamp);
    ImGui.TableNextColumn();
    ImGui.TextUnformatted("Pitch jitter (+/-)"); ImGui.SetNextItemWidth(-1);
    changed |= ImGui.SliderFloat("##jitter", ref jitter, 0f, 1f, "%.2f", ImGuiSliderFlags.AlwaysClamp);
    ImGui.TableNextColumn();
    ImGui.TextUnformatted("Pan (L / R)"); ImGui.SetNextItemWidth(-1);
    changed |= ImGui.SliderFloat("##pan", ref pan, -1f, 1f, "%.2f", ImGuiSliderFlags.AlwaysClamp);
    ImGui.BeginDisabled(soundEvent == SoundEvent.RefuelLoop);
    ImGui.TableNextColumn();
    ImGui.TextUnformatted("Priority"); ImGui.SetNextItemWidth(-1);
    changed |= ImGui.SliderFloat("##priority", ref priority, 0f, 4f, "%.2f", ImGuiSliderFlags.AlwaysClamp);
    if (ImGui.IsItemHovered()) ImGui.SetTooltip("Higher values play first in the queued sound mix when voices are limited.");
    ImGui.TableNextColumn();
    ImGui.TextUnformatted("Category"); ImGui.SetNextItemWidth(-1);
    changed |= ImGui.Combo("##category", ref type, SoundTypeNames, SoundTypeNames.Length);
    if (ImGui.IsItemHovered()) ImGui.SetTooltip("GemCollect adds pickup throttling and escalating pitch. Default and UI use the regular sound queue.");
    ImGui.EndDisabled();
    ImGui.EndTable();
    if (changed)
    {
      AudioManager.Instance.DebugSetCue(soundEvent, layer, cue with
      {
        Volume = volume, FallbackVolume = volumeChanged ? volume : cue.FallbackVolume, Pitch = pitch, PitchJitter = jitter,
        Pan = pan, Priority = priority, Type = SoundTypes[type]
      });
      slot.Status = "Live changes (unsaved)";
      slot.Error = null;
    }
  }

  public void Dispose()
  {
    window.FileDrop -= FileDrop;
    AudioManager.Instance.DebugDisposeSounds();
  }
}
#endif
