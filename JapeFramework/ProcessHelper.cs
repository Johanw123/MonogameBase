using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Serilog;

namespace AsyncContent
{
  public static class ProcessHelper
  {
    public static void RunExe(string exePath, string arguments, string workDir = "")
    {
      if (string.IsNullOrWhiteSpace(workDir))
        workDir = Directory.GetCurrentDirectory();

      bool isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
      bool isMac = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
      bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
      bool isArm = RuntimeInformation.OSArchitecture == Architecture.Arm64;

      if (isArm && isLinux)
      {
        string winePath = "~/Downloads/wine-10.11-amd64/bin/wine"; //TODO: hard coded

        string script = "#/bin/bash" + Environment.NewLine + $"muvm -ti -- box64 {winePath} {exePath} {arguments}";
        string rnd = Path.GetTempFileName();
        File.WriteAllText(rnd, script);

        var proc = new Process();
        proc.StartInfo.FileName = "bash";
        proc.StartInfo.Arguments = rnd;
        proc.StartInfo.WorkingDirectory = workDir;

        proc.Start();
        proc.WaitForExit();

        if (File.Exists(rnd))
          File.Delete(rnd);
      }
      else if (isArm && isMac)
      {
        //WINEPREFIX=$HOME/.winemonogame wine mgfxc ./HarvesterShader.fx test.out /Profile:OpenGL
        Log.Debug("Running exe with wine: " + exePath);

   
        // string script = "#/bin/bash" + Environment.NewLine + "WINEPREFIX=$HOME/.winemonogame dotnet tool install --global dotnet-mgfxc --version 3.8.4";
        // script += Environment.NewLine + $"WINEPREFIX=$HOME/.winemonogame wine mgfxc {arguments} /Profile:OpenGL";

        // string rnd = Path.GetTempFileName();
        // File.WriteAllText(rnd, script);

        // var proc = new Process();
        // proc.StartInfo.FileName = "bash";
        // proc.StartInfo.Arguments = rnd;
        // proc.StartInfo.WorkingDirectory = workDir;
        // proc.StartInfo.UseShellExecute = false;
        //  //proc.StartInfo.RedirectStandardOutput = true;
        // proc.Start();
        // proc.WaitForExit();

        // if (File.Exists(rnd))
        //   File.Delete(rnd);
      }
      else if (isLinux || isMac)
      {
        Log.Debug("Running exe with wine: " + exePath);

        var proc = new Process();
        proc.StartInfo.FileName = "wine";
        proc.StartInfo.Arguments = exePath + " " + arguments;
        proc.StartInfo.WorkingDirectory = workDir;

        proc.Start();
        proc.WaitForExit();
      }
      else if (isWindows)
      {
        var proc = new Process();
        proc.StartInfo.FileName = exePath;
        proc.StartInfo.Arguments = arguments;
        proc.StartInfo.WorkingDirectory = workDir;

        proc.Start();
        proc.WaitForExit();
      }
    }

    public static void RunCommand(string command, string arguments, string workDir = "")
    {
      if (string.IsNullOrWhiteSpace(workDir))
        workDir = Directory.GetCurrentDirectory();

      var proc = new Process();

      proc.StartInfo.FileName = command;
      proc.StartInfo.Arguments = arguments;
      proc.StartInfo.WorkingDirectory = workDir;

      proc.Start();
      proc.WaitForExit();
    }
  }
}
