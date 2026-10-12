using System.Reflection;
using System.Collections;
using System.Runtime.Loader;
using System.Text.RegularExpressions;

// Exercise the installed KNI preprocessor without requiring Windows shader
// compilation. This checks profile selection, not shader bytecode or rendering.
static class ShaderProfileChecks
{
  public static void Run(string tools, string shaders)
  {
    tools = Path.GetFullPath(tools);
    AssemblyLoadContext.Default.Resolving += (_, name) =>
    {
      string path = Path.Combine(tools, name.Name + ".dll");
      return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
    };
    var graphics = Assembly.LoadFrom(Path.Combine(tools, "Xna.Framework.Content.Pipeline.Graphics.dll"));
    var mojo = Assembly.LoadFrom(Path.Combine(tools, "Xna.Framework.Content.Pipeline.Graphics.MojoProcessor.dll"));
    var runtime = Assembly.LoadFrom(Path.Combine(tools, "Xna.Framework.Graphics.dll"));
    var runtimeProfile = runtime.GetType("Microsoft.Xna.Framework.Graphics.GraphicsProfile", throwOnError: true)!;
    var shaderStrategy = runtime.GetType("Microsoft.Xna.Platform.Graphics.ShaderStrategy", throwOnError: true)!;
    var limits = (IDictionary)shaderStrategy.GetField("MaxShaderVersions", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(null)!;
    string webMaximum = limits[Enum.Parse(runtimeProfile, "FL10_0")]!.ToString()!;
    string hiDefMaximum = limits[Enum.Parse(runtimeProfile, "HiDef")]!.ToString()!;
    if (webMaximum != "4.0" || hiDefMaximum != "3.0")
      throw new Exception($"Unexpected runtime limits: FL10_0={webMaximum}, HiDef={hiDefMaximum}");
    var compilerVersion = mojo.GetType("Microsoft.Xna.Framework.Content.Pipeline.EffectCompiler.ShaderVersion", throwOnError: true)!;
    var inputType = graphics.GetType("Microsoft.Xna.Framework.Content.Pipeline.Graphics.EffectContent", throwOnError: true)!;
    var preType = mojo.GetType("Microsoft.Xna.Framework.Content.Pipeline.EffectCompiler.Preprocessor", throwOnError: true)!;
    const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    int count = 0;
    foreach (string path in Directory.EnumerateFiles(shaders, "*.fx", SearchOption.AllDirectories))
    {
      foreach (bool web in new[] { true, false })
      {
        var input = Activator.CreateInstance(inputType)!;
        inputType.GetProperty("EffectCode")!.SetValue(input, File.ReadAllText(path));
        var pre = Activator.CreateInstance(preType, flags, null, new object[] { input, null!, Path.GetFullPath(path) }, null)!;
        string[] macros = web ? new[] { "__KNIFX__", "__GL__", "__GLES__", "__MOJOSHADER__" } : new[] { "OPENGL" };
        foreach (string macro in macros)
          preType.GetMethod("AddMacro", flags)!.Invoke(pre, new[] { macro, "1" });
        string output = (string)preType.GetMethod("Preprocess", flags)!.Invoke(pre, null)!;
        var profiles = Regex.Matches(output, @"compile\s+([vp]s_\w+)").Select(m => m.Groups[1].Value).ToArray();
        string expected = web ? "_4_0" : "_3_0";
        if (profiles.Length == 0 || profiles.Any(p => p != "vs" + expected && p != "ps" + expected))
          throw new Exception($"{path} ({(web ? "KNI WebGL" : "MonoGame OpenGL")}): unexpected profiles {string.Join(", ", profiles)}");
        if (web)
          foreach (string profile in profiles)
          {
            string method = profile.StartsWith("vs_") ? "ParseVertexShaderModel" : "ParsePixelShaderModel";
            object metadata = compilerVersion.GetMethod(method, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!.Invoke(null, new[] { profile })!;
            string metadataVersion = $"{compilerVersion.GetField("Major", flags)!.GetValue(metadata)}.{compilerVersion.GetField("Minor", flags)!.GetValue(metadata)}";
            if (metadataVersion != webMaximum)
              throw new Exception($"{path}: metadata {metadataVersion} does not match the FL10_0 runtime profile ({webMaximum})");
          }
      }
      count++;
    }
    if (count == 0) throw new Exception("No shader files found");
    Console.WriteLine($"Shader profile checks passed for {count} shaders: KNI WebGL FL10_0 runtime limits, metadata and MonoGame OpenGL.");
  }
}
