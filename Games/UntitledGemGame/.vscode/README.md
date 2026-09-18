# Debugging in VS Code

1. Open `Games/UntitledGemGame` as the VS Code folder (the folder containing `UntitledGemGame.csproj`). Keep the surrounding repository checkout: the game references sibling projects.
2. Install the .NET 10 SDK and the recommended **C#** extension (`ms-dotnettools.csharp`) if needed. VS Code lists it under Extensions → Recommended.
3. Set a breakpoint, for example on `game.Run()` in `Program.cs`.
4. Press **F5** to build the game and its content in Debug mode, then launch **Debug UntitledGemGame**. If prompted to choose a configuration, select that name.

Use **F9** to toggle breakpoints, **F10** to step over, **F11** to step into, and **Shift+F5** to stop. Game log output appears in the integrated Terminal; debugger exceptions and exit messages appear in the Debug Console. Build errors appear in the Problems panel. **Ctrl+Shift+B** runs the same Debug build without launching.

The desktop debugger explicitly ignores `Properties/launchSettings.json` and launches the desktop DLL directly. That file also provides a desktop-only default for tools that use launch profiles. If startup fails, check both the Terminal and Debug Console for the error and exit code.

Hot Reload is disabled in this workspace while investigating a native .NET 10 runtime crash during debugger startup on Linux. Breakpoints and stepping remain available; restart debugging to apply code edits.

The first build restores NuGet packages and local .NET tools, so it needs network access. Run locally in a graphical desktop session so MonoGame can open its game window.

Configuration reference: [VS Code C# debugger settings](https://code.visualstudio.com/docs/csharp/debugger-settings).
