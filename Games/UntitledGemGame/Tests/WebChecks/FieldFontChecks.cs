using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

static class FieldFontChecks
{
  public static void Run(string assemblies, string content)
  {
    assemblies = Path.GetFullPath(assemblies);
    content = Path.GetFullPath(content);
    AssemblyLoadContext.Default.Resolving += (_, name) =>
    {
      string path = Path.Combine(assemblies, name.Name + ".dll");
      return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
    };
    var framework = Assembly.LoadFrom(Path.Combine(assemblies, "JapeFramework.dll"));
    FontAttributeChecks.Run(framework);
    var type = framework.GetType("AsyncContent.AssetsLoader", throwOnError: true)!;
    // Field fonts parse staged JSON/PNG bytes without needing a graphics device.
    var loader = Activator.CreateInstance(type, new object[] { null!, null! })!;
    string previous = Environment.CurrentDirectory;
    string scratch = Path.Combine(Path.GetTempPath(), "btb-field-fonts-" + Guid.NewGuid());
    string staged = Path.Combine(scratch, "Content/Fonts/GeneratedFonts");
    Directory.CreateDirectory(staged);
    try
    {
      foreach (string path in Directory.GetFiles(Path.Combine(content, "Fonts/GeneratedFonts")))
        File.Copy(path, Path.Combine(staged, Path.GetFileName(path)));
      Environment.CurrentDirectory = scratch;
      CheckTitleContainer(assemblies);
      foreach (string path in new[] { "Fonts/Roboto-Regular.ttf", "Content/Fonts/Roboto-Regular.ttf", "Fonts\\Roboto-Regular.ttf" })
      {
        object font = type.GetMethod("LoadFieldFont")!.Invoke(loader, new object[] { path, false })!;
        var measure = font.GetType().GetMethod("MeasureString", new[] { typeof(string), typeof(bool) })!.Invoke(font, new object[] { "Upgrades", false })!;
        float width = (float)measure.GetType().GetField("X")!.GetValue(measure)!;
        if (!(width > 0)) throw new Exception("HUD text measured zero width: " + path);
        if (path == "Fonts/Roboto-Regular.ttf") CheckTooltipMeasurement(framework, font);
      }
      Console.WriteLine("Browser field font checks passed: actual loader, staged font files, normalized paths and HUD text measurement.");
    }
    finally
    {
      Environment.CurrentDirectory = previous;
      Directory.Delete(scratch, recursive: true);
    }
  }
  // Use the real tooltip adapter and field-font metrics, with GPU allocation
  // bypassed because measuring text requires no texture or graphics context.
  private static void CheckTooltipMeasurement(Assembly framework, object font)
  {
    const BindingFlags instance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    var rendererType = framework.GetType("BracketHouse.FontExtension.TextRenderer", true)!;
    object renderer = RuntimeHelpers.GetUninitializedObject(rendererType);
    rendererType.GetField("<Font>k__BackingField", instance)!.SetValue(renderer, font);
    var sprites = rendererType.GetField("Sprites", instance)!;
    sprites.SetValue(renderer, Activator.CreateInstance(sprites.FieldType));
    var manager = framework.GetType("FontManager", true)!;
    var renderers = (IDictionary)manager.GetField("fieldFontrenderers", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
    renderers["Roboto_Regular_ttf"] = renderer;
    try
    {
      var tooltipType = framework.GetType("FontStashSharpText", true)!;
      object parent = Activator.CreateInstance(tooltipType)!;
      tooltipType.GetProperty("Width")!.SetValue(parent, 260f);
      object tooltip = Activator.CreateInstance(tooltipType)!;
      tooltipType.GetProperty("Parent")!.SetValue(tooltip, parent);
      tooltipType.GetField("FontSize")!.SetValue(tooltip, 28f);
      tooltipType.GetField("Text")!.SetValue(tooltip, "Collect gems faster with upgraded harvester ships and a higher gem spawn rate.");
      object Size() => tooltipType.GetMethod("Measure2")!.Invoke(tooltip, null)!;
      float Height(object size) => (float)size.GetType().GetField("Y")!.GetValue(size)!;
      float single = Height(Size());
      tooltipType.GetField("WrapText")!.SetValue(tooltip, true);
      float wrapped = Height(Size());
      if (!(single > 0 && wrapped > single))
        throw new Exception($"Tooltip wrapping failed: single={single}, wrapped={wrapped}");
    }
    finally { renderers.Remove("Roboto_Regular_ttf"); }
  }

  private static void CheckTitleContainer(string assemblies)
  {
    var game = Assembly.LoadFrom(Path.Combine(assemblies, "UntitledGemGame.web.dll"));
    var factoryType = game.GetType("UntitledGemGame.Web.WebTitleContainerFactory", true)!;
    object factory = Activator.CreateInstance(factoryType, nonPublic: true)!;
    factoryType.BaseType!.GetMethod("RegisterTitleContainerFactory")!.Invoke(null, new[] { factory });
    var content = Assembly.Load("Xna.Framework.Content");
    var title = content.GetType("Microsoft.Xna.Framework.TitleContainer", true)!;
    var open = title.GetMethod("OpenStream")!;
    Directory.CreateDirectory("Content/Textures");
    byte[] bytes = { 88, 78, 66, 0, 255 };
    File.WriteAllBytes("Content/Textures/Test asset.xnb", bytes);
    using (var stream = (Stream)open.Invoke(null, new object[] { "Content/Textures/Test asset.xnb" })!)
    using (var copy = new MemoryStream())
    {
      stream.CopyTo(copy);
      if (!copy.ToArray().SequenceEqual(bytes)) throw new Exception("TitleContainer changed compiled asset bytes");
    }
    try
    {
      open.Invoke(null, new object[] { "Content/does-not-exist.xnb" });
      throw new Exception("Missing assets should fail locally without a synchronous HTTP request");
    }
    catch (TargetInvocationException error) when (error.InnerException is FileNotFoundException) { }
    Console.WriteLine("Browser TitleContainer check passed: real KNI entry point reads staged binary content and fails locally for missing assets.");
  }

}
