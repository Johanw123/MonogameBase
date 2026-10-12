using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;

static class FontAttributeChecks
{
  public static void Run(Assembly framework)
  {
    const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    var platform = Assembly.Load("Kni.Platform");
    var shaderType = platform.GetType("Microsoft.Xna.Platform.Graphics.ConcreteVertexShader", true)!;
    object shader = RuntimeHelpers.GetUninitializedObject(shaderType);
    GC.SuppressFinalize(shader); // No graphics context exists in this CPU-only check.
    FieldInfo Field(Type type, string name)
    {
      for (Type current = type; current != null; current = current.BaseType)
        if (current.GetField(name, flags) is { } field) return field;
      throw new Exception("Missing backend field: " + name);
    }
    var attributeField = Field(shaderType, "_attributes");
    var attributeType = attributeField.FieldType.GetElementType()!;
    Array Attributes(int fill, int stroke)
    {
      var values = Array.CreateInstance(attributeType, 2);
      for (int i = 0; i < 2; i++)
      {
        object attribute = Activator.CreateInstance(attributeType)!;
        var usage = attributeType.GetField("usage")!;
        usage.SetValue(attribute, Enum.Parse(usage.FieldType, "Color"));
        attributeType.GetField("index")!.SetValue(attribute, i);
        attributeType.GetField("location")!.SetValue(attribute, i == 0 ? fill : stroke);
        values.SetValue(attribute, i);
      }
      return values;
    }
    var vertex = framework.GetType("BracketHouse.FontExtension.FontVertex", true)!;
    object declaration = vertex.GetField("VertexDeclaration", flags)!.GetValue(null)!;
    var getInfo = shaderType.GetMethod("GetVertexAttribInfo", flags)!;
    var declarationInterface = getInfo.GetParameters()[1].ParameterType;
    var getter = declarationInterface.GetInterfaces().Append(declarationInterface)
      .SelectMany(t => t.GetMethods(flags)).Single(m => m.Name == "get_InternalVertexElements");
    object elements = getter.Invoke(declaration, null)!;
    var create = shaderType.GetMethod("CreateVertexAttribInfo", flags)!;
    object Mapping() => create.Invoke(null, new[] { shader, elements, 16 })!;
    int Offset(object info, int location)
    {
      var entries = (IEnumerable)info.GetType().GetField("Elements", flags)!.GetValue(info)!;
      foreach (object entry in entries)
        if ((int)entry.GetType().GetField("AttributeLocation", flags)!.GetValue(entry)! == location)
          return (int)entry.GetType().GetField("Offset", flags)!.GetValue(entry)!;
      return -1;
    }
    attributeField.SetValue(shader, Attributes(-1, 1)); // Stroke program: fill optimized out.
    object strokeInfo = Mapping();
    var cacheField = Field(shaderType, "_vertexAttribInfoCache");
    var cache = (IDictionary)Activator.CreateInstance(cacheField.FieldType)!;
    cache[elements] = strokeInfo;
    cacheField.SetValue(shader, cache);
    attributeField.SetValue(shader, Attributes(1, -1)); // Fill program: stroke optimized out.
    object cached = getInfo.Invoke(shader, new object[] { null, declaration })!;
    if (Offset(cached, 1) != 16 || Offset(Mapping(), 1) != 12)
      throw new Exception("Could not reproduce the installed backend's stale stroke attribute mapping");
    // Both color inputs remain live in the combined outline/fill program.
    attributeField.SetValue(shader, Attributes(1, 2));
    object combined = Mapping();
    if (Offset(combined, 1) != 12 || Offset(combined, 2) != 16)
      throw new Exception("Combined font program has incorrect fill/stroke offsets");
    Console.WriteLine("Browser font attribute check passed: reproduces stale black stroke binding; combined program preserves both colors.");
  }
}
