public static class Demo
{
  public const string Version = "0.1";
  public static bool IsDemo = true;
  public static bool IsDev = true;
  // Read at startup. Disable all Steam initialization and relaunch attempts.
  public static bool DisableSteam = true;
  public static string VersionLabel => $"v{Version}{(IsDemo ? " DEMO" : "")}";
}
