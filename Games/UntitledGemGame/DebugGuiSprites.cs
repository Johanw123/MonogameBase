using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Vector2 = System.Numerics.Vector2;
using Vector4 = System.Numerics.Vector4;

namespace UntitledGemGame;

public static class DebugGuiSprites
{
  private static readonly string[] ColorModeLabels =
  [
    "Auto (smart mask / palette)",
    "Palette Surface (recolor blue)",
    "Fill Tint (panel / button fill)",
    "Accent Tint (border / accent)",
    "Confirm Surface (recolor green)",
    "Untinted (raw texture colors)",
  ];

  private static readonly Dictionary<GuiSpriteSlot, string> paths = new();
  private static readonly Dictionary<GuiSpriteSlot, Rectangle> dropBoxes = new();
  private static readonly Dictionary<GuiSpriteSlot, Task<byte[]>> imports = new();
  private static GuiSpriteSlot? pendingDropSlot;
  private static GameWindow registeredWindow;
  private static string status;
  private static bool statusError;
  private static string filter = "";
  private static int lastDropBoxFrame = -1;
  private static int frameCounter;
  private static bool panelVisible;
  private static bool editingText;
  private static bool capturingMouse;
  private static Vector2 panelMin;
  private static Vector2 panelMax;

  public static bool StandaloneOpen;

  internal static bool CapturesInput
  {
    get
    {
      if (!StandaloneOpen || !panelVisible) return false;
      var mouse = Mouse.GetState();
      return editingText || capturingMouse
        || (mouse.X >= panelMin.X && mouse.Y >= panelMin.Y && mouse.X < panelMax.X && mouse.Y < panelMax.Y);
    }
  }

  public static int DropTargetCount => dropBoxes.Count;

  public static Rectangle GetDropTargetBounds(GuiSpriteSlot slot) =>
    dropBoxes.TryGetValue(slot, out var rect) ? rect : Rectangle.Empty;

  public static void RegisterFileDrop(GameWindow window)
  {
    if (window == null || ReferenceEquals(registeredWindow, window)) return;
    registeredWindow = window;
    window.FileDrop += OnFileDrop;
  }

  public static void DrawStandalone()
  {
    frameCounter++;
    PollImports();
    panelVisible = false;
    editingText = false;
    capturingMouse = false;
    if (ImGui.IsKeyPressed(ImGuiKey.F9, false)) StandaloneOpen = !StandaloneOpen;
    if (!StandaloneOpen) return;

    ImGui.SetNextWindowSize(new Vector2(920, 760), ImGuiCond.FirstUseEver);
    ImGui.SetNextWindowSizeConstraints(new Vector2(720, 380), new Vector2(float.MaxValue, float.MaxValue));
    ImGui.SetNextWindowBgAlpha(0.96f);
    bool expanded = ImGui.Begin("GUI sprites lab (F9)", ref StandaloneOpen);
    panelVisible = true;
    panelMin = ImGui.GetWindowPos();
    panelMax = panelMin + ImGui.GetWindowSize();
    capturingMouse = ImGui.GetIO().WantCaptureMouse;
    if (expanded)
      DrawLabContents("standalone", clearDropBoxes: true);
    ImGui.End();
  }

  public static void DrawInline()
  {
    frameCounter++;
    PollImports();
    DrawLabContents("inline", clearDropBoxes: !StandaloneOpen);
  }

  private static void DrawLabContents(string scopeId, bool clearDropBoxes)
  {
    ImGui.PushID(scopeId);
    if (clearDropBoxes && lastDropBoxFrame != frameCounter)
    {
      dropBoxes.Clear();
      lastDropBoxFrame = frameCounter;
    }

    ImGui.TextWrapped("Drag & drop .png (or .jpg/.webp via ffmpeg) onto any slot's blue drop target, pick any built-in HUD/menu sprite, or paste a file path. Changes apply live to both HUD panels and Gum menus.");

    ImGui.Spacing();
    ImGui.SetNextItemWidth(340);
    ImGui.InputTextWithHint("##filter", "Filter slots (e.g. border, button, panel, tree)...", ref filter, 128);
    ImGui.SameLine();
    if (!string.IsNullOrEmpty(filter) && ImGui.Button("Clear filter"))
      filter = "";
    ImGui.SameLine();
    bool anyOverride = GuiSkin.HasAnyOverride;
    if (!anyOverride) ImGui.BeginDisabled();
    if (ImGui.Button("Reset all to built-in"))
    {
      GuiSkin.DebugResetAllOriginals();
      SetStatus("All GUI sprite slots reset to built-in defaults.", error: false);
    }
    if (!anyOverride) ImGui.EndDisabled();

    if (!string.IsNullOrEmpty(status))
    {
      var color = statusError ? new Vector4(1f, 0.45f, 0.35f, 1f) : new Vector4(0.55f, 0.9f, 0.65f, 1f);
      ImGui.TextColored(color, status);
    }

    var builtInList = GuiSkin.GetBuiltInAssetNames();
    string currentCategory = null;
    bool categoryOpen = true;

    foreach (var info in GuiSkin.AllSlotInfos)
    {
      if (!MatchesFilter(info)) continue;

      if (!string.Equals(currentCategory, info.Category, StringComparison.Ordinal))
      {
        currentCategory = info.Category;
        categoryOpen = ImGui.CollapsingHeader(currentCategory, ImGuiTreeNodeFlags.DefaultOpen);
      }
      if (!categoryOpen) continue;

      DrawSlotRow(info, builtInList);
    }

    ImGui.PopID();
  }

  private static bool MatchesFilter(GuiSpriteSlotInfo info)
  {
    if (string.IsNullOrWhiteSpace(filter)) return true;
    string f = filter.Trim();
    return info.Label.Contains(f, StringComparison.OrdinalIgnoreCase)
      || info.Slot.ToString().Contains(f, StringComparison.OrdinalIgnoreCase)
      || info.Category.Contains(f, StringComparison.OrdinalIgnoreCase)
      || GuiSkin.GetSlotSourceName(info.Slot).Contains(f, StringComparison.OrdinalIgnoreCase);
  }

  private static void DrawSlotRow(GuiSpriteSlotInfo info, IReadOnlyList<string> builtInList)
  {
    var slot = info.Slot;
    ImGui.PushID((int)slot);
    ImGui.Separator();

    bool dirty = GuiSkin.IsSlotDirty(slot);
    bool replaced = GuiSkin.IsSlotReplaced(slot);
    bool builtIn = GuiSkin.IsBuiltIn(slot);
    bool hidden = GuiSkin.GetSlotHidden(slot);
    bool importing = imports.ContainsKey(slot);
    var (texW, texH, isMask) = GuiSkin.GetSlotDimensions(slot);
    var effMode = GuiSkin.ResolveEffectiveColorMode(slot);

    var headerColor = dirty
      ? new Vector4(1f, 0.84f, 0.35f, 1f)
      : replaced || !builtIn
        ? new Vector4(0.45f, 0.92f, 0.68f, 1f)
        : new Vector4(0.90f, 0.94f, 0.98f, 1f);
    ImGui.TextColored(headerColor, $"{info.Label} ({slot})");
    ImGui.SameLine();
    string maskTag = isMask ? "White/Tint Mask" : "Painted Sprite";
    string stateTag = hidden ? " [HIDDEN]" : dirty ? " [unsaved]" : !builtIn ? " [customized]" : "";
    ImGui.TextDisabled($"{GuiSkin.GetSlotSourceName(slot)} ({texW}x{texH}px, {maskTag}, {effMode}){stateTag}");

    DrawPreviewThumbnail(slot, info);
    ImGui.SameLine();

    ImGui.BeginGroup();
    bool waitingForDrop = pendingDropSlot == slot;
    string dropLabel = importing
      ? "Importing image..."
      : waitingForDrop
        ? "Drop image file on game window now (click to cancel)"
        : "Drop image file here (.png / .jpg / .webp)";
    ImGui.PushStyleColor(ImGuiCol.Button, waitingForDrop
      ? new Vector4(0.22f, 0.44f, 0.62f, 1f)
      : new Vector4(0.14f, 0.24f, 0.34f, 1f));
    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.24f, 0.42f, 0.58f, 1f));
    if (ImGui.Button(dropLabel, new Vector2(360, 28)))
    {
      pendingDropSlot = waitingForDrop ? null : slot;
      SetStatus(pendingDropSlot == slot
        ? $"{slot}: drop an image file onto the game window"
        : null, error: false);
    }
    ImGui.PopStyleColor(2);
    RecordDropBox(slot);

    ImGui.SameLine();
    DrawBuiltInPicker(slot, info, builtInList);

    string path = paths.GetValueOrDefault(slot, "");
    ImGui.SetNextItemWidth(360);
    if (ImGui.InputTextWithHint("##path", "Paste image file path (.png, .jpg, .webp)...", ref path, 512))
      paths[slot] = path;
    ImGui.SameLine();
    if (importing) ImGui.BeginDisabled();
    if (ImGui.Button("Load"))
      StartImport(slot, path);
    if (importing) ImGui.EndDisabled();

    // Parameters rows
    if (info.IsNineSlice)
    {
      int maxFrame = Math.Max(16, Math.Min(texW, texH) / 2);
      int frameWidth = GuiSkin.GetSlotFrameWidth(slot);
      ImGui.SetNextItemWidth(140);
      if (ImGui.SliderInt("9-slice px", ref frameWidth, 1, maxFrame))
        GuiSkin.DebugSetParameters(slot, frameWidth: frameWidth);

      ImGui.SameLine();
      float cornerScale = GuiSkin.GetSlotCornerScale(slot);
      ImGui.SetNextItemWidth(140);
      if (ImGui.SliderFloat("Corner scale", ref cornerScale, 0.25f, 3.0f, "%.2fx"))
        GuiSkin.DebugSetParameters(slot, cornerScale: cornerScale);
    }
    else if (slot is GuiSpriteSlot.ScanlinesOverlay or GuiSpriteSlot.HexagonsOverlay or GuiSpriteSlot.SliderKnob)
    {
      float scale = GuiSkin.GetSlotCornerScale(slot);
      ImGui.SetNextItemWidth(140);
      if (ImGui.SliderFloat("Scale", ref scale, 0.25f, 3.0f, "%.2fx"))
        GuiSkin.DebugSetParameters(slot, cornerScale: scale);
    }

    float opacity = GuiSkin.GetSlotOpacity(slot);
    ImGui.SetNextItemWidth(140);
    if (ImGui.SliderFloat("Opacity", ref opacity, 0f, 1f, "%.2f"))
      GuiSkin.DebugSetParameters(slot, opacity: opacity);

    ImGui.SameLine();
    int colorModeIdx = (int)GuiSkin.GetSlotColorMode(slot);
    ImGui.SetNextItemWidth(220);
    if (ImGui.Combo("Color mode", ref colorModeIdx, ColorModeLabels, ColorModeLabels.Length))
      GuiSkin.DebugSetParameters(slot, colorMode: (GuiSpriteColorMode)colorModeIdx);

    // Actions row
    if (!dirty) ImGui.BeginDisabled();
    if (ImGui.Button("Revert"))
    {
      GuiSkin.DebugRevert(slot);
      SetStatus($"{slot}: reverted to saved default", error: false);
    }
    if (!dirty) ImGui.EndDisabled();

    ImGui.SameLine();
    if (builtIn) ImGui.BeginDisabled();
    if (ImGui.Button("Original"))
    {
      GuiSkin.DebugResetOriginal(slot);
      SetStatus($"{slot}: restored built-in sprite ({info.DefaultAsset})", error: false);
    }
    if (builtIn) ImGui.EndDisabled();

    ImGui.SameLine();
    if (!dirty) ImGui.BeginDisabled();
    if (ImGui.Button("Save"))
    {
      try
      {
        SetStatus(GuiSpriteDefaults.Save(slot), error: false);
      }
      catch (Exception e)
      {
        SetStatus($"{slot}: save failed ({e.Message})", error: true);
      }
    }
    if (!dirty) ImGui.EndDisabled();

    ImGui.SameLine();
    if (ImGui.Button(hidden ? "Unhide layer" : "Set hidden"))
    {
      GuiSkin.DebugSetParameters(slot, hidden: !hidden);
      SetStatus($"{slot}: {(!hidden ? "layer hidden" : "layer visible")}", error: false);
    }

    ImGui.EndGroup();
    ImGui.PopID();
  }

  private static void DrawBuiltInPicker(GuiSpriteSlot slot, GuiSpriteSlotInfo info, IReadOnlyList<string> builtInList)
  {
    string currentAsset = GuiSkin.GetSlotAssetName(slot) ?? "(custom file)";
    ImGui.SetNextItemWidth(250);
    if (ImGui.BeginCombo("Built-in sprite", currentAsset))
    {
      if (info.DefaultAsset == "$vector_rect")
      {
        bool selected = currentAsset == "$vector_rect";
        if (ImGui.Selectable("$vector_rect (procedural outline)", selected))
        {
          GuiSkin.DebugSetBuiltInAsset(slot, "$vector_rect");
          SetStatus($"{slot}: set to procedural vector outline", error: false);
        }
        if (selected) ImGui.SetItemDefaultFocus();
      }
      foreach (var asset in builtInList)
      {
        bool selected = string.Equals(currentAsset, asset, StringComparison.Ordinal);
        string label = string.Equals(asset, info.DefaultAsset, StringComparison.Ordinal)
          ? $"{asset} (default)"
          : asset;
        if (ImGui.Selectable(label, selected))
        {
          if (GuiSkin.DebugSetBuiltInAsset(slot, asset))
            SetStatus($"{slot}: switched to built-in '{asset}'", error: false);
        }
        if (selected) ImGui.SetItemDefaultFocus();
      }
      ImGui.EndCombo();
    }
  }

  private static void DrawPreviewThumbnail(GuiSpriteSlot slot, GuiSpriteSlotInfo info)
  {
    const float boxSize = 56f;
    var pos = ImGui.GetCursorScreenPos();
    ImGui.InvisibleButton("##thumb", new Vector2(boxSize, boxSize));
    var drawList = ImGui.GetWindowDrawList();
    var max = new Vector2(pos.X + boxSize, pos.Y + boxSize);

    // Checkerboard background for alpha visibility
    const int grid = 14;
    float cell = boxSize / grid;
    uint cDark = ImGui.ColorConvertFloat4ToU32(new Vector4(0.10f, 0.12f, 0.15f, 1f));
    uint cLight = ImGui.ColorConvertFloat4ToU32(new Vector4(0.18f, 0.21f, 0.25f, 1f));
    var raw = GuiSkin.GetRawSprite(slot);
    bool hidden = GuiSkin.GetSlotHidden(slot);
    float opacity = GuiSkin.GetSlotOpacity(slot);

    for (int gy = 0; gy < grid; gy++)
    for (int gx = 0; gx < grid; gx++)
    {
      var p0 = new Vector2(pos.X + gx * cell, pos.Y + gy * cell);
      var p1 = new Vector2(p0.X + cell, p0.Y + cell);
      drawList.AddRectFilled(p0, p1, ((gx + gy) & 1) == 0 ? cDark : cLight);

      if (!hidden && raw != null && raw.Width > 0 && raw.Height > 0)
      {
        int sx = Math.Clamp((int)((gx + 0.5f) / grid * raw.Width), 0, raw.Width - 1);
        int sy = Math.Clamp((int)((gy + 0.5f) / grid * raw.Height), 0, raw.Height - 1);
        var px = raw.Pixels[sy * raw.Width + sx];
        if (px.A > 4)
        {
          var v = new Vector4(px.R / 255f, px.G / 255f, px.B / 255f, (px.A / 255f) * opacity);
          drawList.AddRectFilled(p0, p1, ImGui.ColorConvertFloat4ToU32(v));
        }
      }
    }

    // 9-slice guide lines overlay
    if (info.IsNineSlice && raw != null && raw.Width > 0 && raw.Height > 0)
    {
      int fw = GuiSkin.GetSlotFrameWidth(slot);
      float rx = Math.Clamp(fw / (float)raw.Width, 0.05f, 0.49f) * boxSize;
      float ry = Math.Clamp(fw / (float)raw.Height, 0.05f, 0.49f) * boxSize;
      uint guideCol = ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 0.78f, 0.22f, 0.75f));
      drawList.AddLine(new Vector2(pos.X + rx, pos.Y), new Vector2(pos.X + rx, max.Y), guideCol, 1f);
      drawList.AddLine(new Vector2(max.X - rx, pos.Y), new Vector2(max.X - rx, max.Y), guideCol, 1f);
      drawList.AddLine(new Vector2(pos.X, pos.Y + ry), new Vector2(max.X, pos.Y + ry), guideCol, 1f);
      drawList.AddLine(new Vector2(pos.X, max.Y - ry), new Vector2(max.X, max.Y - ry), guideCol, 1f);
    }

    uint borderCol = ImGui.ColorConvertFloat4ToU32(new Vector4(0.32f, 0.62f, 0.76f, 0.9f));
    drawList.AddRect(pos, max, borderCol);
  }

  private static void RecordDropBox(GuiSpriteSlot slot)
  {
    var min = ImGui.GetItemRectMin();
    var max = ImGui.GetItemRectMax();
    dropBoxes[slot] = new Rectangle((int)min.X, (int)min.Y,
      Math.Max(1, (int)(max.X - min.X)), Math.Max(1, (int)(max.Y - min.Y)));
  }

  private static void OnFileDrop(object sender, FileDropEventArgs e)
  {
    if (e.Files == null || e.Files.Length == 0) return;
    var mouse = Mouse.GetState().Position;
    HandleFileDropAt(mouse, e.Files[0]);
  }

  public static bool HandleFileDropAt(Point mousePosition, string filePath)
  {
    GuiSpriteSlot? target = null;
    foreach (var (slot, box) in dropBoxes)
    {
      if (box.Contains(mousePosition))
      {
        target = slot;
        break;
      }
    }
    target ??= pendingDropSlot;
    if (target == null)
    {
      if (StandaloneOpen)
        SetStatus("Drop the image file onto a slot's blue drop box, or click a drop box first.", error: true);
      return false;
    }

    pendingDropSlot = null;
    paths[target.Value] = filePath;
    StartImport(target.Value, filePath);
    return true;
  }

  private static void StartImport(GuiSpriteSlot slot, string rawPath)
  {
    string path = GuiSpriteImport.NormalizePath(rawPath);
    paths[slot] = path;
    if (string.IsNullOrWhiteSpace(path))
    {
      SetStatus($"{slot}: enter an image file path first", error: true);
      return;
    }
    imports[slot] = GuiSpriteImport.ReadPngAsync(path);
    SetStatus($"{slot}: importing {Path.GetFileName(path)}...", error: false);
  }

  public static void PollImports()
  {
    if (imports.Count == 0) return;
    foreach (var (slot, task) in imports.ToArray())
    {
      if (!task.IsCompleted) continue;
      imports.Remove(slot);
      if (task.IsFaulted)
      {
        string message = task.Exception?.GetBaseException().Message ?? "import failed";
        SetStatus($"{slot}: {message}", error: true);
      }
      else if (task.IsCompletedSuccessfully)
      {
        string srcName = Path.GetFileName(paths.GetValueOrDefault(slot, "custom.png"));
        if (GuiSkin.DebugReplace(slot, task.Result, srcName, out string err))
          SetStatus($"{slot}: loaded {srcName}", error: false);
        else
          SetStatus($"{slot}: {err}", error: true);
      }
    }
  }

  private static void SetStatus(string message, bool error)
  {
    status = message;
    statusError = error;
  }
}
