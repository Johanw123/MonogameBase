using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

static class MenuRenderingChecks
{
  public static void Run(string assemblies)
  {
    assemblies = Path.GetFullPath(assemblies);
    AssemblyLoadContext.Default.Resolving += (_, name) =>
    {
      var path = Path.Combine(assemblies, name.Name + ".dll");
      return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
    };
    var game = Assembly.LoadFrom(Path.Combine(assemblies, "UntitledGemGame.web.dll"));
    var xna = Assembly.LoadFrom(Path.Combine(assemblies, "Xna.Framework.dll"));
    var matrix = xna.GetType("Microsoft.Xna.Framework.Matrix", true)!;
    var vector = xna.GetType("Microsoft.Xna.Framework.Vector2", true)!;
    object V(float x, float y) => Activator.CreateInstance(vector, x, y)!;
    int count = 0;
    void Check(bool condition, string name) { if (!condition) throw new Exception(name); count++; }
    var projection = matrix.GetMethod("CreateOrthographicOffCenter", Enumerable.Repeat(typeof(float), 6).ToArray())!
      .Invoke(null, new object[] { 0f, 1920f, 1080f, 0f, 0f, -1f })!;
    var quality = game.GetType("RenderQuality", true)!;
    var physicalZoom = (float)quality.GetMethod("WorldZoom")!.Invoke(null, new object[] { 1.5f })!;
    Check(1920f / physicalZoom == 3840f / 1.5f, "world framing survives resolution reduction");
    var visibility = game.GetType("RenderVisibility", true)!.GetMethod("Intersects")!;
    bool Visible(float l, float t, float r, float b) => (bool)visibility.Invoke(null, new[] { (object)l, t, r, b, projection })!;
    Check(Visible(1, 1, 20, 20), "visible rectangle");
    Check(!Visible(-80, 1, -1, 20), "left clipped rectangle");
    Check(!Visible(1921, 1, 2000, 20), "right clipped rectangle");
    Check(!Visible(1, -30, 20, -1), "top clipped rectangle");
    Check(!Visible(1, 1081, 20, 1100), "bottom clipped rectangle");
    Check(Visible(-100, 100, 2000, 120), "crossing link stays visible with both endpoints offscreen");
    Check(Visible(-8, 1, 0, 20), "edge glow stays visible");

    const BindingFlags instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    var gui = game.GetType("RenderGuiSystem", true)!;
    var guiInstance = RuntimeHelpers.GetUninitializedObject(gui);
    var cache = gui.GetField("jointPaths", instance)!;
    cache.SetValue(guiInstance, Activator.CreateInstance(cache.FieldType));
    var jointType = game.GetTypes().Single(t => t.Name == "UpgradeJoint");
    var joint = RuntimeHelpers.GetUninitializedObject(jointType);
    var midway = jointType.GetField("MidwayPoints", instance)!;
    var points = (IList)Activator.CreateInstance(midway.FieldType)!;
    points.Add(V(10, 0));
    midway.SetValue(joint, points);
    var getPath = gui.GetMethod("GetJointPath", instance)!;
    object PathFor(float end) => getPath.Invoke(guiInstance, new[] { joint, V(0, 0), V(end, 0), (object)0f })!;
    float Distance(object path) => (float)path.GetType().GetField("TotalDistance")!.GetValue(path)!;
    var first = PathFor(20);
    Check(Distance(first) == 20, "initial cached path length");
    Check(ReferenceEquals(first, PathFor(20)), "unchanged path reuses cache");
    Check(ReferenceEquals(first, PathFor(30)) && Distance(first) == 30, "endpoint movement updates cached length");
    points[0] = V(10, 10);
    Check(Distance(PathFor(30)) > 30, "midpoint movement updates cached length");
    points.Add(V(20, 10));
    Check(!ReferenceEquals(first, PathFor(30)), "changed path point count rebuilds cache");

    var gum = Assembly.LoadFrom(Path.Combine(assemblies, "KniGum.dll"));
    var managers = gum.GetType("RenderingLibrary.SystemManagers", true)!;
    var defaults = managers.GetProperty("Default")!;
    var system = Activator.CreateInstance(managers)!;
    defaults.SetValue(null, system);
    var renderer = Activator.CreateInstance(gum.GetType("RenderingLibrary.Graphics.Renderer", true)!)!;
    managers.GetProperty("Renderer")!.SetValue(system, renderer);
    var common = Assembly.LoadFrom(System.IO.Path.Combine(assemblies, "GumCommon.dll"));
    var camera = Activator.CreateInstance(common.GetType("RenderingLibrary.Camera", true)!)!;
    renderer.GetType().GetField("mCamera", instance)!.SetValue(renderer, camera);
    camera.GetType().GetProperty("Zoom")!.SetValue(camera, 0.7f);
    var layers = (IList)renderer.GetType().GetProperty("LayersWritable", instance)!.GetValue(renderer)!;
    var layer = Activator.CreateInstance((gum.GetType("RenderingLibrary.Graphics.Layer") ?? common.GetType("RenderingLibrary.Graphics.Layer", true))!)!;
    var settings = Activator.CreateInstance((gum.GetType("RenderingLibrary.Graphics.LayerCameraSettings") ?? common.GetType("RenderingLibrary.Graphics.LayerCameraSettings", true))!)!;
    settings.GetType().GetProperty("Zoom")!.SetValue(settings, 1f);
    layer.GetType().GetProperty("LayerCameraSettings")!.SetValue(layer, settings);
    layers.Add(layer);
    // Scope's disposal reads virtual dimensions from the adapter. Install the real
    // adapter without invoking the window/GPU constructor.
    var framework = Assembly.LoadFrom(Path.Combine(assemblies, "JapeFramework.dll"));
    var adapterField = framework.GetType("JapeFramework.BaseGame", true)!.GetField("BoxingViewportAdapterGui")!;
    var adapter = RuntimeHelpers.GetUninitializedObject(adapterField.FieldType);
    for (var type = adapterField.FieldType; type != null; type = type.BaseType)
      foreach (var f in type.GetFields(instance | BindingFlags.DeclaredOnly))
      {
        if (f.Name.Contains("VirtualWidth")) f.SetValue(adapter, 3840);
        if (f.Name.Contains("VirtualHeight")) f.SetValue(adapter, 2160);
      }
    adapterField.SetValue(null, adapter);
    var scopeType = game.GetType("UntitledGemGame.Web.WebHudRenderScope", true)!;
    camera.GetType().GetProperty("ClientWidth")!.SetValue(camera, 3840);
    camera.GetType().GetProperty("ClientHeight")!.SetValue(camera, 2160);
    camera.GetType().GetField("Position")!.SetValue(camera, new System.Numerics.Vector2(120, -50));
    var center = camera.GetType().GetProperty("CameraCenterOnScreen")!;
    center.SetValue(camera, Enum.Parse(center.PropertyType, "Center"));
    System.Numerics.Vector2 Project()
      => System.Numerics.Vector2.Transform(new System.Numerics.Vector2(1500, 900),
        (System.Numerics.Matrix4x4)camera.GetType().GetMethod("GetTransformationMatrix", new[] { typeof(bool) })!
          .Invoke(camera, new object[] { true })!);
    var before = Project();
    var scope = (IDisposable)scopeType.GetMethod("Begin")!.Invoke(null, null)!;
    Check((float)camera.GetType().GetProperty("Zoom")!.GetValue(camera)! == 0.35f, "draw camera scales");
    Check((float)settings.GetType().GetProperty("Zoom")!.GetValue(settings)! == 0.5f, "screen layer scales");
    Check(System.Numerics.Vector2.Distance(Project(), before * 0.5f) <= 0.75f,
      "panned and centered tree draws at half scale within Gum pixel snapping");
    scope.Dispose();
    Check(System.Numerics.Vector2.Distance(Project(), before) < 0.001f, "tree input matrix restores");
    Check((float)camera.GetType().GetProperty("Zoom")!.GetValue(camera)! == 0.7f, "input camera zoom restores");
    Check((int)camera.GetType().GetProperty("ClientWidth")!.GetValue(camera)! == 3840, "logical input width restores");
    Check((float)settings.GetType().GetProperty("Zoom")!.GetValue(settings)! == 1f, "screen layer restores");
    Console.WriteLine($"Menu rendering checks passed: {count} culling, cache invalidation and web camera checks.");
  }
}
