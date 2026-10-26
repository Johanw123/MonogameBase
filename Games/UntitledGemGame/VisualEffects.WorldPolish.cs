using ImGuiNET;

namespace UntitledGemGame;

public sealed partial class VisualEffectsSettings
{
  public bool Atmosphere = true;
  public float AtmosphereStrength = 0.32f;
  public float AtmosphereWidth = 0.16f;
  public float AuroraStrength = 0.22f;
  public float AuroraSpeed = 0.25f;
  public float AtmosphereRed = 0.25f;
  public float AtmosphereGreen = 0.38f;
  public float AtmosphereBlue = 0.95f;
  public bool ImpactLighting = true;
  public float ImpactLightStrength = 0.65f;
  public float ImpactLightRadius = 0.3f;
  public float ImpactRippleStrength = 0.32f;
  public float ImpactDuration = 0.65f;
  public bool RockDebris = true;
  public float DebrisAmount = 1.0f;
  public float DebrisSize = 1.0f;
  public float DebrisSpeed = 1.0f;
  public float DebrisLifetime = 1.0f;
  public bool SpaceDust = true;
  public float DustAmount = 0.45f;
  public float DustBrightness = 0.35f;
  public float DustSize = 1.0f;
  public float DustSpeed = 1.0f;

  private void ValidateWorldPolish()
  {
    Check(AtmosphereStrength, 0.0f, 1.5f, nameof(AtmosphereStrength));
    Check(AtmosphereWidth, 0.03f, 0.45f, nameof(AtmosphereWidth));
    Check(AuroraStrength, 0.0f, 1.0f, nameof(AuroraStrength));
    Check(AuroraSpeed, 0.0f, 2.0f, nameof(AuroraSpeed));
    Check(AtmosphereRed, 0.0f, 1.0f, nameof(AtmosphereRed));
    Check(AtmosphereGreen, 0.0f, 1.0f, nameof(AtmosphereGreen));
    Check(AtmosphereBlue, 0.0f, 1.0f, nameof(AtmosphereBlue));
    Check(ImpactLightStrength, 0.0f, 2.0f, nameof(ImpactLightStrength));
    Check(ImpactLightRadius, 0.08f, 0.7f, nameof(ImpactLightRadius));
    Check(ImpactRippleStrength, 0.0f, 1.5f, nameof(ImpactRippleStrength));
    Check(ImpactDuration, 0.15f, 1.5f, nameof(ImpactDuration));
    Check(DebrisAmount, 0.0f, 3.0f, nameof(DebrisAmount));
    Check(DebrisSize, 0.3f, 3.0f, nameof(DebrisSize));
    Check(DebrisSpeed, 0.2f, 3.0f, nameof(DebrisSpeed));
    Check(DebrisLifetime, 0.25f, 2.0f, nameof(DebrisLifetime));
    Check(DustAmount, 0.0f, 1.0f, nameof(DustAmount));
    Check(DustBrightness, 0.0f, 1.0f, nameof(DustBrightness));
    Check(DustSize, 0.3f, 3.0f, nameof(DustSize));
    Check(DustSpeed, 0.0f, 3.0f, nameof(DustSpeed));
  }
}

internal static class DebugWorldPolish
{
  public static void Draw()
  {
    var s = VisualEffects.Settings;
    if (ImGui.TreeNode("Planet atmosphere"))
    {
      ImGui.Checkbox("Enabled##Atmosphere", ref s.Atmosphere);
      ImGui.BeginDisabled(!s.Atmosphere);
      ImGui.SliderFloat("Corona brightness", ref s.AtmosphereStrength, 0.0f, 1.5f, "%.3f", ImGuiSliderFlags.AlwaysClamp);
      ImGui.SliderFloat("Corona reach", ref s.AtmosphereWidth, 0.03f, 0.45f, "%.3f", ImGuiSliderFlags.AlwaysClamp);
      ImGui.SliderFloat("Aurora wisps", ref s.AuroraStrength, 0.0f, 1.0f, "%.3f", ImGuiSliderFlags.AlwaysClamp);
      ImGui.SliderFloat("Aurora speed", ref s.AuroraSpeed, 0.0f, 2.0f, "%.3f", ImGuiSliderFlags.AlwaysClamp);
      ImGui.SliderFloat("Atmosphere red", ref s.AtmosphereRed, 0.0f, 1.0f, "%.3f", ImGuiSliderFlags.AlwaysClamp);
      ImGui.SliderFloat("Atmosphere green", ref s.AtmosphereGreen, 0.0f, 1.0f, "%.3f", ImGuiSliderFlags.AlwaysClamp);
      ImGui.SliderFloat("Atmosphere blue", ref s.AtmosphereBlue, 0.0f, 1.0f, "%.3f", ImGuiSliderFlags.AlwaysClamp);
      ImGui.EndDisabled();
      ImGui.TreePop();
    }
    if (ImGui.TreeNode("Impact light and surface ripples"))
    {
      ImGui.Checkbox("Enabled##ImpactLighting", ref s.ImpactLighting);
      ImGui.BeginDisabled(!s.ImpactLighting);
      ImGui.SliderFloat("Local hit light", ref s.ImpactLightStrength, 0.0f, 2.0f, "%.3f", ImGuiSliderFlags.AlwaysClamp);
      ImGui.SliderFloat("Light spread", ref s.ImpactLightRadius, 0.08f, 0.7f, "%.3f", ImGuiSliderFlags.AlwaysClamp);
      ImGui.SliderFloat("Surface ripple brightness", ref s.ImpactRippleStrength, 0.0f, 1.5f, "%.3f", ImGuiSliderFlags.AlwaysClamp);
      ImGui.SliderFloat("Impact duration (seconds)", ref s.ImpactDuration, 0.15f, 1.5f, "%.3f", ImGuiSliderFlags.AlwaysClamp);
      ImGui.EndDisabled();
      ImGui.TreePop();
    }
    if (ImGui.TreeNode("Ejected rock fragments"))
    {
      ImGui.Checkbox("Enabled##RockDebris", ref s.RockDebris);
      ImGui.BeginDisabled(!s.RockDebris);
      ImGui.SliderFloat("Fragment amount", ref s.DebrisAmount, 0.0f, 3.0f, "%.3f", ImGuiSliderFlags.AlwaysClamp);
      ImGui.SliderFloat("Fragment size", ref s.DebrisSize, 0.3f, 3.0f, "%.3f", ImGuiSliderFlags.AlwaysClamp);
      ImGui.SliderFloat("Ejection speed", ref s.DebrisSpeed, 0.2f, 3.0f, "%.3f", ImGuiSliderFlags.AlwaysClamp);
      ImGui.SliderFloat("Fragment lifetime", ref s.DebrisLifetime, 0.25f, 2.0f, "%.3f", ImGuiSliderFlags.AlwaysClamp);
      ImGui.EndDisabled();
      ImGui.TreePop();
    }
    if (ImGui.TreeNode("Drifting space dust"))
    {
      ImGui.Checkbox("Enabled##SpaceDust", ref s.SpaceDust);
      ImGui.BeginDisabled(!s.SpaceDust);
      ImGui.SliderFloat("Dust density", ref s.DustAmount, 0.0f, 1.0f, "%.3f", ImGuiSliderFlags.AlwaysClamp);
      ImGui.SliderFloat("Dust brightness", ref s.DustBrightness, 0.0f, 1.0f, "%.3f", ImGuiSliderFlags.AlwaysClamp);
      ImGui.SliderFloat("Dust size", ref s.DustSize, 0.3f, 3.0f, "%.3f", ImGuiSliderFlags.AlwaysClamp);
      ImGui.SliderFloat("Dust drift", ref s.DustSpeed, 0.0f, 3.0f, "%.3f", ImGuiSliderFlags.AlwaysClamp);
      ImGui.EndDisabled();
      ImGui.TreePop();
    }
  }
}
