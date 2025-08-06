using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AsyncContent
{
  public static class PathHelper
  {
    public static string FindSolutionFile(string fromPath = null)
    {
      if (string.IsNullOrWhiteSpace(fromPath))
        fromPath = Directory.GetCurrentDirectory();

      DirectoryInfo directory = new DirectoryInfo(fromPath);

      while (directory != null)
      {
        FileInfo[] solutionFiles = directory.GetFiles("*.sln");

        if (solutionFiles.Length > 0)
          return solutionFiles[0].FullName;

        directory = directory.Parent;
      }

      return null;
    }

    public static string FindSolutionDirectory(string fromPath = null)
    {
      return Path.GetDirectoryName(FindSolutionFile(fromPath));
    }

    public static string FindProjectDirectory()
    {
      DirectoryInfo directory = new DirectoryInfo(Directory.GetCurrentDirectory());
      FileInfo[] projFiles = directory.GetFiles("*.csproj");
      if (projFiles.Count() > 0)
        return Directory.GetCurrentDirectory();

      return Directory.GetParent(Directory.GetCurrentDirectory()).Parent.Parent.FullName;
    }
  }
}
