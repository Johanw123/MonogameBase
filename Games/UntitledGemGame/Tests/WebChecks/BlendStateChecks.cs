using System.Reflection;
using System.Runtime.Loader;

static class BlendStateChecks
{
  public static void Run(string assemblies)
  {
    assemblies = Path.GetFullPath(assemblies);
    AssemblyLoadContext.Default.Resolving += (_, name) =>
    {
      string path = Path.Combine(assemblies, name.Name + ".dll");
      return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
    };
    var platform = Assembly.LoadFrom(Path.Combine(assemblies, "Kni.Platform.dll"));
    var blendState = platform.GetType("Microsoft.Xna.Platform.Graphics.ConcreteBlendState", throwOnError: true)!;
    var mapper = blendState.GetMethod("ToGLBlendEquationMode", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
    var blendFunction = mapper.GetParameters()[0].ParameterType;
    try
    {
      mapper.Invoke(null, new[] { Enum.Parse(blendFunction, "Max") });
      throw new Exception("Expected the installed browser backend to reject Max blending");
    }
    catch (TargetInvocationException exception) when (exception.InnerException is ArgumentException) { }
    var game = Assembly.LoadFrom(Path.Combine(assemblies, "UntitledGemGame.web.dll"));
    var functions = game.GetType("RenderBlendFunctions", throwOnError: true)!;
    var choice = functions.GetField("MaxGlow", BindingFlags.Static | BindingFlags.Public)!.GetRawConstantValue()!;
    object equation = mapper.Invoke(null, new[] { Enum.ToObject(blendFunction, choice) })!;
    if (Convert.ToInt32(equation) != 32774) throw new Exception("Expected WebGL FUNC_ADD for the browser glow fallback");
    Console.WriteLine("Browser blend check passed: reproduces the Max-blend exception; compiled glow fallback maps successfully to WebGL FUNC_ADD.");
  }
}
