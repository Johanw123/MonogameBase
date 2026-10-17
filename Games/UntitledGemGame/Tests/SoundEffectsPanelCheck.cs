using System.Collections;
using System.Reflection;
using JapeFramework.ImGUI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ImGuiNET;
using UntitledGemGame;

internal sealed class SoundEffectsPanelCheck : Game
{
  private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
  private readonly string output;
  private readonly Type labType = typeof(GameMain).Assembly.GetType("UntitledGemGame.DebugSoundEffects")!;
  private object lab;
  private ImGuiRenderer renderer;
  private RenderTarget2D target;
  private int frames;

  public SoundEffectsPanelCheck(string output)
  {
    this.output = output;
    _ = new GraphicsDeviceManager(this) { PreferredBackBufferWidth = 1280, PreferredBackBufferHeight = 800 };
    IsMouseVisible = true;
    IsFixedTimeStep = false;
  }

  protected override void LoadContent()
  {
    renderer = new ImGuiRenderer(this);
    ImGui.GetIO().IniSavingRate = 0;
    renderer.RebuildFontAtlas();
    lab = Activator.CreateInstance(labType, Private, null, [Window], null)!;
    labType.GetField("Open", Private)!.SetValue(lab, true);
    target = new RenderTarget2D(GraphicsDevice, 1280, 800);
  }

  protected override void Draw(GameTime gameTime)
  {
    GraphicsDevice.SetRenderTarget(target);
    GraphicsDevice.Clear(new Color(20, 25, 32));
    renderer.BeginLayout(gameTime);
    ImGui.SetNextWindowPos(new System.Numerics.Vector2(30, 30), ImGuiCond.Always);
    labType.GetMethod("Draw", Private)!.Invoke(lab, null);
    renderer.EndLayout();
    GraphicsDevice.SetRenderTarget(null);
    ++frames;
    var targets = (IList)labType.GetField("targets", Private)!.GetValue(lab)!;
    if (frames == 3)
    {
      if (targets.Count != 0) throw new Exception("Sound sections must start collapsed");
      using (var stream = File.Create(output)) target.SaveAsPng(stream, target.Width, target.Height);
      labType.GetField("search", Private)!.SetValue(lab, "MenuClick");
    }
    if (frames == 6)
    {
      if (targets.Count != 1 || ((SoundEvent Event, System.Numerics.Vector2 Min, System.Numerics.Vector2 Max))targets[0]!
        is not { Event: SoundEvent.MenuClick })
        throw new Exception("Filtering must reveal a matching event inside a collapsed section");
      labType.GetField("search", Private)!.SetValue(lab, "");
    }
    if (frames == 9)
    {
      if (targets.Count != 0) throw new Exception("Clearing the filter must restore collapsed sections");
      ((HashSet<string>)labType.GetField("expandedGroups", Private)!.GetValue(lab)!).Add("Menu & UI");
    }
    if (frames < 12) return;
    if (targets.Count == 0) throw new Exception("Sound panel rendered no visible drop targets");
    var first = ((SoundEvent Event, System.Numerics.Vector2 Min, System.Numerics.Vector2 Max))targets[0]!;
    Mouse.SetPosition((int)(first.Min.X + first.Max.X) / 2, (int)(first.Min.Y + first.Max.Y) / 2);
    // Exercise OS-drop routing using the same event arguments delivered by GameWindow.FileDrop.
    labType.GetMethod("FileDrop", Private)!.Invoke(lab, [Window, new FileDropEventArgs(["/tmp/nonexistent-drop-check.wav"])]);
    var slots = (IDictionary)labType.GetField("slots", Private)!.GetValue(lab)!;
    object slot = slots[first.Event]!;
    if ((string)slot.GetType().GetField("Input")!.GetValue(slot)! != "/tmp/nonexistent-drop-check.wav")
      throw new Exception("File drop did not route to the visible event slot");
    ((Task)slot.GetType().GetField("Loading")!.GetValue(slot)!).ContinueWith(_ => { }).Wait();
    labType.GetMethod("Update", Private)!.Invoke(lab, null);
    string expandedOutput = Path.ChangeExtension(output, "expanded.png");
    using (var stream = File.Create(expandedOutput)) target.SaveAsPng(stream, target.Width, target.Height);
    Console.WriteLine("Sound panel sections, filtering, collapse restoration and drop routing passed: " + output + ", " + expandedOutput);
    Exit();
  }

  protected override void UnloadContent()
  {
    (lab as IDisposable)?.Dispose();
    target?.Dispose();
  }
}
