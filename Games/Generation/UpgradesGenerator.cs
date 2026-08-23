using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Linq;

public static class StringExtensions
{
  public static string FirstCharToUpper(this string input) =>
      input switch
      {
        null => throw new ArgumentNullException(nameof(input)),
        "" => throw new ArgumentException($"{nameof(input)} cannot be empty", nameof(input)),
        _ => input[0].ToString().ToUpper() + input.Substring(1)
      };
}

[Generator]
public class UpgradesGenerator : IIncrementalGenerator
{
  public void Initialize(IncrementalGeneratorInitializationContext context)
  {
    // File.WriteAllText("error.txt", "");
    // File.WriteAllText("testoutput.txt", "");

    // File.Delete("error.txt");
    // File.Delete("testoutput.txt");

    // var files = context.AdditionalTextsProvider.Where(file => file.Path.EndsWith(".json"));
    var files = context.AdditionalTextsProvider;
    var namesAndContents = files.Select((file, cancellationToken) => (Name: Path.GetFileNameWithoutExtension(file.Path), Content: file.GetText(cancellationToken).ToString(), Path: file.Path));

    // foreach(var a in files.Select(s => s.Path))
    // {
    //   File.WriteAllText("/home/johan/Dev/out.txt", a);
    // }

    context.RegisterSourceOutput(namesAndContents, AddSource);
  }

  private void AddIncrementMethod(ref string sourceCode, RootUpgrades root)
  {
    sourceCode += $@"  public void Increment(string shortName, float value)" + "\n";
    sourceCode += @"  {" + "\n";
    foreach (var u in root.Upgrades)
    {
      var name = u.Name;
      var shortname = u.ShortName;
      var propName = u.PropertyName;
      var type = u.Type;
      var baseValue = u.BaseValue;
      if (type == "float")
      {
        sourceCode += $@"    if (shortName == ""{shortname}"")" + "\n";
        sourceCode += $@"      {propName} += value;" + "\n";
      }
    }

    sourceCode += @"  }" + "\n";

    sourceCode += $@"  public void Increment(string shortName, int value)" + "\n";
    sourceCode += @"  {" + "\n";
    foreach (var u in root.Upgrades)
    {
      var name = u.Name;
      var shortname = u.ShortName;
      var propName = u.PropertyName;
      var type = u.Type;
      var baseValue = u.BaseValue;
      if (type == "int")
      {
        sourceCode += $@"    if (shortName == ""{shortname}"")" + "\n";
        sourceCode += $@"      {propName} += value;" + "\n";
      }
    }

    sourceCode += @"  }" + "\n";
  }

  private void AddSetValueMethod(ref string sourceCode, RootUpgrades root)
  {
    sourceCode += $@"  public void Set(string shortName, float value)" + "\n";
    sourceCode += @"  {" + "\n";
    foreach (var u in root.Upgrades)
    {
      var name = u.Name;
      var shortname = u.ShortName;
      var propName = u.PropertyName;
      var type = u.Type;
      var baseValue = u.BaseValue;
      if (type == "float")
      {
        sourceCode += $@"    if (shortName == ""{shortname}"")" + "\n";
        sourceCode += $@"      {propName} = value;" + "\n";
      }
    }

    sourceCode += @"  }" + "\n";

    sourceCode += $@"  public void Set(string shortName, int value)" + "\n";
    sourceCode += @"  {" + "\n";
    foreach (var u in root.Upgrades)
    {
      var name = u.Name;
      var shortname = u.ShortName;
      var propName = u.PropertyName;
      var type = u.Type;
      var baseValue = u.BaseValue;
      if (type == "int")
      {
        sourceCode += $@"    if (shortName == ""{shortname}"")" + "\n";
        sourceCode += $@"      {propName} = value;" + "\n";
      }
    }

    sourceCode += @"  }" + "\n";


    sourceCode += $@"  public void Set(string shortName, bool value)" + "\n";
    sourceCode += @"  {" + "\n";
    foreach (var u in root.Upgrades)
    {
      var name = u.Name;
      var shortname = u.ShortName;
      var propName = u.PropertyName;
      var type = u.Type;
      var baseValue = u.BaseValue;
      if (type == "bool")
      {
        sourceCode += $@"    if (shortName == ""{shortname}"")" + "\n";
        sourceCode += $@"      {propName} = value;" + "\n";
      }
    }

    sourceCode += @"  }" + "\n";
  }


  private void AddResetValueMethod(ref string sourceCode, RootUpgrades root)
  {

    sourceCode += $@"  public void Reset(string shortName)" + "\n";
    sourceCode += @"  {" + "\n";
    foreach (var u in root.Upgrades)
    {
      var name = u.Name;
      var shortname = u.ShortName;
      var propName = u.PropertyName;
      var type = u.Type;
      var baseValue = u.BaseValue;
      string f = u.Type == "float" ? "f" : "";

      sourceCode += $@"    if (shortName == ""{shortname}"")" + "\n";
      sourceCode += $@"      {propName} = {baseValue}{f};" + "\n";
    }

    sourceCode += @"  }" + "\n";
  }

  private void AddGetMethods(ref string sourceCode, RootUpgrades root)
  {
    sourceCode += $@"  public float GetFloat(string shortName)" + "\n";
    sourceCode += @"  {" + "\n";
    foreach (var u in root.Upgrades)
    {
      var name = u.Name;
      var shortname = u.ShortName;
      var propName = u.PropertyName;
      var type = u.Type;
      var baseValue = u.BaseValue;
      if (type == "float")
      {
        sourceCode += $@"    if (shortName == ""{shortname}"")" + "\n";
        // sourceCode += $@"      {propName} = value;" + "\n";
        sourceCode += $@"      return {propName};" + "\n";
      }
    }

    sourceCode += $@"      return 0;" + "\n";
    sourceCode += @"  }" + "\n";


    sourceCode += $@"  public int GetInt(string shortName)" + "\n";
    sourceCode += @"  {" + "\n";
    foreach (var u in root.Upgrades)
    {
      var name = u.Name;
      var shortname = u.ShortName;
      var propName = u.PropertyName;
      var type = u.Type;
      var baseValue = u.BaseValue;
      if (type == "int")
      {
        sourceCode += $@"    if (shortName == ""{shortname}"")" + "\n";
        // sourceCode += $@"      {propName} = value;" + "\n";
        sourceCode += $@"      return {propName};" + "\n";
      }
    }

    sourceCode += $@"      return 0;" + "\n";
    sourceCode += @"  }" + "\n";


    sourceCode += $@"  public bool GetBool(string shortName)" + "\n";
    sourceCode += @"  {" + "\n";
    foreach (var u in root.Upgrades)
    {
      var name = u.Name;
      var shortname = u.ShortName;
      var propName = u.PropertyName;
      var type = u.Type;
      var baseValue = u.BaseValue;
      if (type == "bool")
      {
        sourceCode += $@"    if (shortName == ""{shortname}"")" + "\n";
        // sourceCode += $@"      {propName} = value;" + "\n";
        sourceCode += $@"      return {propName};" + "\n";
      }
    }


    sourceCode += $@"      return false;" + "\n";
    sourceCode += @"  }" + "\n";
  }

  private void AddSource(SourceProductionContext context, (string Name, string Content, string Path) file)
  {
    // string fileName = $"UpgradesGenerator.g.cs";
    string fileName = $"UpgradesGenerator" + file.Name.FirstCharToUpper() + ".g.cs";

    // if(!file.Path.EndsWith(".json")) return;

    try
    {
      File.WriteAllText("/home/johan/Dev/out_" + file.Name.FirstCharToUpper() + ".txt", file.Content);

      if (file.Content == null)
        throw new Exception("Failed to read file \"" + file.Path + "\"");

      string sourceCode = @"public class UpgradesGenerator" + file.Name.FirstCharToUpper() + "\n" + "{" + "\n";

      var root = JsonSerializer.Deserialize(file.Content, SerializerContext.Default.RootUpgrades);
      foreach (var u in root.Upgrades)
      {
        var name = u.Name;
        var shortname = u.ShortName;
        var propName = u.PropertyName;
        var type = u.Type;
        var baseValue = u.BaseValue;
        string f = u.Type == "float" ? "f" : "";
        sourceCode += $@"  public {type} {propName} = {baseValue}{f};" + "\n";
      }

      AddIncrementMethod(ref sourceCode, root);
      AddSetValueMethod(ref sourceCode, root);
      AddGetMethods(ref sourceCode, root);
      AddResetValueMethod(ref sourceCode, root);

      sourceCode += @"}";
      context.AddSource(fileName, sourceCode);
      // File.WriteAllText("testoutput.txt", sourceCode);
    }
    catch (Exception e)
    {
      string errorMessage = $"Error: {e.Message}\n\nStrack trace: {e.StackTrace}";

      context.AddSource(fileName, errorMessage);
      Console.WriteLine(errorMessage);

      // File.WriteAllText("error.txt", errorMessage);
    }
  }
}
