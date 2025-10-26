using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Linq;

[Generator]
public class UpgradesGenerator : IIncrementalGenerator
{
  public void Initialize(IncrementalGeneratorInitializationContext context)
  {
    var files = context.AdditionalTextsProvider.Where(file => file.Path.EndsWith("upgrades_buttons.json"));
    var namesAndContents = files.Select((file, cancellationToken) => (Name: Path.GetFileNameWithoutExtension(file.Path), Content: file.GetText(cancellationToken).ToString(), Path: file.Path));

    context.RegisterSourceOutput(namesAndContents, AddSource);
  }

  private void AddIncrementMethod(ref string sourceCode, Root root)
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

  private void AddSetValueMethod(ref string sourceCode, Root root)
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


  private void AddResetValueMethod(ref string sourceCode, Root root)
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
  private void AddSource(SourceProductionContext context, (string Name, string Content, string Path) file)
  {
    string fileName = $"UpgradesGenerator.g.cs";

    try
    {
      if (file.Content == null)
        throw new Exception("Failed to read file \"" + file.Path + "\"");

      string sourceCode = @"public class UpgradesGenerator" + "\n" + "{" + "\n";

      Root root = JsonSerializer.Deserialize(file.Content, SerializerContext.Default.Root);
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
      AddResetValueMethod(ref sourceCode, root);

      sourceCode += @"}";
      context.AddSource(fileName, sourceCode);
    }
    catch (Exception e)
    {
      string errorMessage = $"Error: {e.Message}\n\nStrack trace: {e.StackTrace}";

      context.AddSource(fileName, errorMessage);
    }
  }
}
