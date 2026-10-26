using System;
using System.IO;
using ImGuiNET;

namespace UntitledGemGame;

internal static class DebugVisualEffects
{
  private static string error;
  public static void Draw()
  {
    if (!ImGui.CollapsingHeader("Visual effects / space atmosphere", ImGuiTreeNodeFlags.DefaultOpen)) return;
    ImGui.PushID("visual-effects");
    ImGui.TextWrapped("Live world effects. Recommended is a restrained space look. Ctrl-click sliders for numeric entry. Save as default keeps your look for future launches and builds.");
    if (ImGui.Button("Recommended")) { VisualEffects.Recommended(); VisualEffects.Bypass = false; }
    ImGui.SameLine();
    if (ImGui.Button("Deep space")) { VisualEffects.DeepSpace(); VisualEffects.Bypass = false; }
    ImGui.SameLine();
    if (ImGui.Button("Original look")) { VisualEffects.Original(); VisualEffects.Bypass = false; }
    ImGui.Checkbox("Compare original (keep my tweaks)", ref VisualEffects.Bypass);
#if !KNI_WEB
    string content = Demo.IsDev ? DebugSoundEffects.FindSourceContentDirectory() : null;
    ImGui.BeginDisabled(content == null);
    if (ImGui.Button("Save as default"))
    {
      try { VisualEffects.Save(Path.Combine(content, VisualEffects.ManifestPath)); error = null; }
      catch (Exception exception) { error = "Save failed: " + exception.Message; }
    }
    ImGui.EndDisabled();
    ImGui.SameLine();
#endif
    if (ImGui.Button("Revert to saved")) { VisualEffects.Revert(); VisualEffects.Bypass = false; }
    ImGui.SameLine();
    if (ImGui.Button("Copy settings JSON")) ImGui.SetClipboardText(VisualEffects.ToJson());
    if (VisualEffects.Status != null) ImGui.TextWrapped(VisualEffects.Status);
    if (error != null) ImGui.TextWrapped(error);
    var s = VisualEffects.Settings;
    ImGui.Checkbox("Enable visual effects", ref s.Enabled);
    ImGui.Checkbox("Animate background and grain", ref s.Animate);
    ImGui.BeginDisabled(!s.Enabled || VisualEffects.Bypass);
    DebugWorldPolish.Draw();
    if (ImGui.TreeNode("Background layers"))
    {
      ImGui.Checkbox("Enabled##Background", ref s.Background);
      ImGui.BeginDisabled(!s.Background);
      ImGui.SliderFloat("Background brightness", ref s.BackgroundBrightness, 0.0f, 1.5f, "%.3f", ImGuiSliderFlags.AlwaysClamp);
      ImGui.SliderFloat("Background saturation", ref s.BackgroundSaturation, 0.0f, 2.0f, "%.3f", ImGuiSliderFlags.AlwaysClamp);
      ImGui.SliderFloat("Cloud layer opacity", ref s.CloudOpacity, 0.0f, 1.0f, "%.3f", ImGuiSliderFlags.AlwaysClamp);
      ImGui.SliderFloat("Star layer opacity", ref s.StarOpacity, 0.0f, 1.0f, "%.3f", ImGuiSliderFlags.AlwaysClamp);
      ImGui.EndDisabled();
      ImGui.TreePop();
    }
    if (ImGui.TreeNode("Atmospheric fog"))
    {
      ImGui.Checkbox("Enabled##Fog", ref s.Fog);
      ImGui.BeginDisabled(!s.Fog);
      ImGui.SliderFloat("Fog density", ref s.FogDensity, 0.0f, 0.65f, "%.3f", ImGuiSliderFlags.AlwaysClamp);
      ImGui.SliderFloat("Fog scale", ref s.FogScale, 1.0f, 20.0f, "%.3f", ImGuiSliderFlags.AlwaysClamp);
      ImGui.SliderFloat("Fog flow speed", ref s.FogSpeed, 0.0f, 0.3f, "%.3f", ImGuiSliderFlags.AlwaysClamp);
      ImGui.SliderFloat("Fog coverage", ref s.FogCoverage, 0.0f, 1.0f, "%.3f", ImGuiSliderFlags.AlwaysClamp);
      ImGui.SliderFloat("Fog red", ref s.FogRed, 0.0f, 1.0f, "%.3f", ImGuiSliderFlags.AlwaysClamp);
      ImGui.SliderFloat("Fog green", ref s.FogGreen, 0.0f, 1.0f, "%.3f", ImGuiSliderFlags.AlwaysClamp);
      ImGui.SliderFloat("Fog blue", ref s.FogBlue, 0.0f, 1.0f, "%.3f", ImGuiSliderFlags.AlwaysClamp);
      ImGui.EndDisabled();
      ImGui.TreePop();
    }
    if (ImGui.TreeNode("Background motion"))
    {
      ImGui.Checkbox("Enabled##Motion", ref s.Motion);
      ImGui.BeginDisabled(!s.Motion);
      ImGui.SliderFloat("Drift (world units / second)", ref s.DriftSpeed, -30.0f, 30.0f, "%.3f", ImGuiSliderFlags.AlwaysClamp);
      ImGui.SliderFloat("Camera parallax", ref s.Parallax, 0.0f, 1.0f, "%.3f", ImGuiSliderFlags.AlwaysClamp);
      ImGui.SliderFloat("Cloud distortion", ref s.CloudWarp, 0.0f, 0.03f, "%.3f", ImGuiSliderFlags.AlwaysClamp);
      ImGui.SliderFloat("Star shimmer", ref s.Twinkle, 0.0f, 0.75f, "%.3f", ImGuiSliderFlags.AlwaysClamp);
      ImGui.SliderFloat("Shimmer speed", ref s.TwinkleSpeed, 0.0f, 3.0f, "%.3f", ImGuiSliderFlags.AlwaysClamp);
      ImGui.EndDisabled();
      ImGui.TreePop();
    }
    if (ImGui.TreeNode("Bloom"))
    {
      ImGui.Checkbox("Enabled##Bloom", ref s.Bloom);
      ImGui.BeginDisabled(!s.Bloom);
      ImGui.Checkbox("Soft threshold + wide halo", ref s.SoftBloom);
      ImGui.TextWrapped("Disable soft threshold for the original saturated extraction. Wide halo reuses the blurred bloom buffer; it is an inexpensive approximation, not HDR bloom.");
      ImGui.SliderFloat("Threshold", ref s.Threshold, 0.05f, 0.9f, "%.3f", ImGuiSliderFlags.AlwaysClamp);
      ImGui.SliderFloat("Intensity", ref s.Intensity, 0.0f, 3.0f, "%.3f", ImGuiSliderFlags.AlwaysClamp);
      ImGui.SliderFloat("Blur", ref s.Blur, 0.5f, 10.0f, "%.3f", ImGuiSliderFlags.AlwaysClamp);
      ImGui.SliderFloat("Bloom saturation", ref s.BloomSaturation, 0.0f, 2.5f, "%.3f", ImGuiSliderFlags.AlwaysClamp);
      ImGui.SliderFloat("Soft knee", ref s.SoftKnee, 0.0f, 1.0f, "%.3f", ImGuiSliderFlags.AlwaysClamp);
      ImGui.SliderFloat("Wide halo mix", ref s.Halo, 0.0f, 1.0f, "%.3f", ImGuiSliderFlags.AlwaysClamp);
      ImGui.SliderFloat("Halo radius (1080p pixels)", ref s.HaloRadius, 0.0f, 18.0f, "%.3f", ImGuiSliderFlags.AlwaysClamp);
      ImGui.SliderFloat("Halo horizontal stretch", ref s.Anamorphic, 1.0f, 4.0f, "%.3f", ImGuiSliderFlags.AlwaysClamp);
      ImGui.EndDisabled();
      ImGui.TreePop();
    }
    if (ImGui.TreeNode("Color grading"))
    {
      ImGui.Checkbox("Enabled##Grading", ref s.Grading);
      ImGui.BeginDisabled(!s.Grading);
      ImGui.SliderFloat("Exposure (stops)", ref s.Exposure, -2.0f, 2.0f, "%.3f", ImGuiSliderFlags.AlwaysClamp);
      ImGui.SliderFloat("Contrast", ref s.Contrast, 0.5f, 1.5f, "%.3f", ImGuiSliderFlags.AlwaysClamp);
      ImGui.SliderFloat("Saturation", ref s.Saturation, 0.0f, 2.0f, "%.3f", ImGuiSliderFlags.AlwaysClamp);
      ImGui.SliderFloat("Warm / cool", ref s.Temperature, -0.5f, 0.5f, "%.3f", ImGuiSliderFlags.AlwaysClamp);
      ImGui.SliderFloat("Magenta / green", ref s.Tint, -0.3f, 0.3f, "%.3f", ImGuiSliderFlags.AlwaysClamp);
      ImGui.SliderFloat("Shadow lift", ref s.ShadowLift, -0.06f, 0.1f, "%.3f", ImGuiSliderFlags.AlwaysClamp);
      ImGui.SliderFloat("Highlight compression", ref s.HighlightRolloff, 0.0f, 1.0f, "%.3f", ImGuiSliderFlags.AlwaysClamp);
      ImGui.EndDisabled();
      ImGui.TreePop();
    }
    if (ImGui.TreeNode("Vignette"))
    {
      ImGui.Checkbox("Enabled##Vignette", ref s.Vignette);
      ImGui.BeginDisabled(!s.Vignette);
      ImGui.SliderFloat("Edge darkening", ref s.VignetteStrength, 0.0f, 0.8f, "%.3f", ImGuiSliderFlags.AlwaysClamp);
      ImGui.SliderFloat("Clear center radius", ref s.VignetteRadius, 0.0f, 0.9f, "%.3f", ImGuiSliderFlags.AlwaysClamp);
      ImGui.SliderFloat("Edge softness", ref s.VignetteSoftness, 0.05f, 1.0f, "%.3f", ImGuiSliderFlags.AlwaysClamp);
      ImGui.EndDisabled();
      ImGui.TreePop();
    }
    if (ImGui.TreeNode("Chromatic aberration"))
    {
      ImGui.Checkbox("Enabled##Aberration", ref s.Aberration);
      ImGui.BeginDisabled(!s.Aberration);
      ImGui.SliderFloat("Color split (1080p pixels)", ref s.AberrationPixels, 0.0f, 4.0f, "%.3f", ImGuiSliderFlags.AlwaysClamp);
      ImGui.EndDisabled();
      ImGui.TreePop();
    }
    if (ImGui.TreeNode("Film grain"))
    {
      ImGui.Checkbox("Enabled##Grain", ref s.Grain);
      ImGui.BeginDisabled(!s.Grain);
      ImGui.SliderFloat("Grain strength", ref s.GrainAmount, 0.0f, 0.1f, "%.3f", ImGuiSliderFlags.AlwaysClamp);
      ImGui.EndDisabled();
      ImGui.TreePop();
    }
    if (ImGui.TreeNode("Sharpen"))
    {
      ImGui.Checkbox("Enabled##Sharpen", ref s.Sharpen);
      ImGui.BeginDisabled(!s.Sharpen);
      ImGui.SliderFloat("Sharpen strength", ref s.SharpenAmount, 0.0f, 1.0f, "%.3f", ImGuiSliderFlags.AlwaysClamp);
      ImGui.EndDisabled();
      ImGui.TreePop();
    }
    if (ImGui.TreeNode("Lens distortion"))
    {
      ImGui.Checkbox("Enabled##Lens", ref s.Lens);
      ImGui.BeginDisabled(!s.Lens);
      ImGui.SliderFloat("Barrel / pincushion", ref s.LensAmount, -0.15f, 0.15f, "%.3f", ImGuiSliderFlags.AlwaysClamp);
      ImGui.EndDisabled();
      ImGui.TreePop();
    }
    ImGui.EndDisabled();
    ImGui.PopID();
  }
}
