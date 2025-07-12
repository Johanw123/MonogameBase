using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using static ContentSourceGenerator.Program;

namespace ContentSourceGenerator
{
  public static class Program
  {
    public class Node
    {
      public string FullPath;
      public string Name;
      public List<Node> Children = [];
    }

    private static Node root = new Node { Name = "ContentDirectory" };

    public static string RemoveInvalidChars(string filename)
    {
      return string.Concat(filename.Split(Path.GetInvalidFileNameChars())).Replace(" ", "").Replace("-", "_");
    }

    private static void CopyFilesRecursively(string sourcePath, string targetPath)
    {
      foreach (string dirPath in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
      {
        Directory.CreateDirectory(dirPath.Replace(sourcePath, targetPath));
      }

      foreach (string newPath in Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories))
      {
        File.Copy(newPath, newPath.Replace(sourcePath, targetPath), true);
      }
    }

    private static void CopyContent(string path)
    {
      string workingDirectory = Environment.CurrentDirectory;

      string targetPath =
        Path.Combine(
          Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
            .Replace("ContentSourceGenerator", "HelloMonoGame"), "Content");

      CopyFilesRecursively(path, targetPath);

      Console.WriteLine(path);
      Console.WriteLine(workingDirectory);
      Console.WriteLine(targetPath);
    }

    private static string GenerateContent(string contentPath, string addToBase = "")
    {
      var fullPath = Path.GetFullPath(contentPath);
      var contentFiles = Directory.EnumerateFiles(fullPath, "*.*", SearchOption.AllDirectories);

      foreach (var file in contentFiles)
      {
        var path = file.Replace(fullPath, "").Replace('\\', '/').Trim('/');

        if (path.Contains("DS_Store"))
          continue;

        var split = path.Split('/');

        Node curNode = root;

        foreach (var s1 in split)
        {
          var name = s1;

          if (name is "bin" or "obj")
            break;

          if (string.IsNullOrWhiteSpace(name))
            break;

          bool found = false;
          foreach (var child in curNode.Children)
          {
            if (name == child.Name)
            {
              found = true;
              curNode = child;
              break;
            }
          }

          if (!found && !string.IsNullOrWhiteSpace(name))
          {
            var newNode = new Node { Name = name, FullPath = addToBase + path };
            curNode.Children.Add(newNode);
            curNode = newNode;
          }
        }
      }

      string code = "";
      int depth = 0;
      GenerateFromNode(root, ref code, ref depth);

      return code;
    }

    public static DirectoryInfo TryGetSolutionDirectoryInfo(string currentPath = null)
    {
      var directory = new DirectoryInfo(
        currentPath ?? Directory.GetCurrentDirectory());
      while (directory != null && directory.GetFiles("*.sln").Length == 0)
      {
        directory = directory.Parent;
      }

      return directory;
    }

    private static DirectoryInfo RootDir = TryGetSolutionDirectoryInfo(Directory.GetCurrentDirectory());

    public static void Main(string[] args)
    {
      try
      {
        // string contentPath = Path.Combine(RootDir.ToString(), "HelloMonoGame/Content/bin/DesktopGL/Content");
        string contentPath = Path.Combine(RootDir.ToString(), "HelloMonoGame/Content/");
        string contentCopyPath = Path.Combine(RootDir.ToString(), "HelloMonoGame/ContentCopy");
        string outputPath = Path.Combine(RootDir.ToString(), "ContentSourceGenerator/ContentDirectory.cs");

        Console.WriteLine("test");

        GenerateContent(contentPath);

        CopyContent(contentCopyPath);
        var code = GenerateContent(contentCopyPath, "Content/");

        File.WriteAllText(outputPath, code);
      }
      catch (Exception e)
      {
        Console.WriteLine(e);
      }

    }

    public static void GenerateFromNode(Node node, ref string code, ref int depth)
    {
      if (string.IsNullOrWhiteSpace(node.Name))
        return;

      code += new string(' ', depth) + $"public static class {node.Name} {Environment.NewLine}{new string(' ', depth)}{{" + Environment.NewLine;

      foreach (var nodeChild in node.Children.Where(c => !c.Children.Any()))
      {
        var name = RemoveInvalidChars(Path.GetFileNameWithoutExtension(nodeChild.Name));
        if (string.IsNullOrWhiteSpace(name))
          continue;
        code += new string(' ', depth + 2) +
        $"public static string {name} => \"{nodeChild.FullPath.Replace("\\", "/").Replace(".xnb", "")}\";" +
        Environment.NewLine;
      }

      int origDepth = depth;
      depth += 1;

      foreach (var nodeChild in node.Children.Where(c => c.Children.Count != 0))
      {
        GenerateFromNode(nodeChild, ref code, ref depth);
      }

      code += new string(' ', origDepth) + "}" + Environment.NewLine;
    }
  }
}
