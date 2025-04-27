using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace ContentSourceGenerator
{
  public static class Content {
    public static class Textures
    {
      public const string Test = "test";
    }
  }

  public static class Program
  {
    public class Node
    {
      public string Name;
      public List<Node> Children;
    }

    public static void Main(string[] args)
    {
      try
      {
        string s = "apa";

        var fullPath = Path.GetFullPath("../../../../HelloMonoGame/Content");
        var contentFiles = Directory.EnumerateFiles(fullPath, "*.*", SearchOption.AllDirectories);

        var root = new Node { Name = "Content", Children = [] };

        foreach (var file in contentFiles)
        {
          var path = file.Replace(fullPath, "").Trim('\\');


          var split = path.Split('\\');


          var contentName = split.LastOrDefault();

          foreach (var s1 in split.SkipLast(1))
          {
            var name = s1;

            //foreach (var child in root.Children)
            //{
            //  if (name == child.Name)
            //  {

            //  }
            //}
          }




          path = Path.GetFileNameWithoutExtension(path);





          //"font"
          //Textures\\MainMenu\\background_mainmenu
        }

        Console.WriteLine(s);
      }
      catch (Exception e)
      {
        Console.WriteLine(e);
      }

    }
  }
}
