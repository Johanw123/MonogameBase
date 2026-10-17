using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using AsyncContent;
using JapeFramework;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.Input;
using Serilog;
using UntitledGemGame.Entities;
using UntitledGemGame.Screens;

namespace UntitledGemGame;

public enum FeedbackMode
{
  None,
  MainMenuSurvey,
  InGameQuick
}

public sealed class PlaytestFeedbackPayload
{
  public string Sheet { get; set; } = "MainMenuFeedback";
  public string TimestampUtc { get; set; } = "";
  public string Source { get; set; } = "MainMenu";

  // 1-5 survey ratings (0 when skipped or submitted via In-Game Quick Feedback)
  public int OverallFun { get; set; }
  public int PacingProgression { get; set; }
  public int VisualsUiClarity { get; set; }
  public int PerformanceControls { get; set; }

  // Free-text comment or quick feedback note
  public string Message { get; set; } = "";

  // Build & system info
  public string GameVersion { get; set; } = Demo.Version;
  public string VersionLabel { get; set; } = "";
  public bool IsDemo { get; set; } = Demo.IsDemo;
  public bool IsDev { get; set; } = Demo.IsDev;
  public string Platform { get; set; } = "";
  public string Resolution { get; set; } = "";
  public string Language { get; set; } = "en";
  public double CurrentFps { get; set; }

  // Progression stats
  public double SessionPlayTimeSeconds { get; set; }
  public ulong CoreExtractions { get; set; }
  public ulong PrestigePointsEarned { get; set; }
  public ulong PendingPrestigePoints { get; set; }
  public int CoreFractures { get; set; }
  public int CoreDrillTunnels { get; set; }
  public string RedGems { get; set; } = "0";
  public string RedGemsEarnedThisRun { get; set; } = "0";
  public string BlueGems { get; set; } = "0";
  public string PurpleGems { get; set; } = "0";
  public ulong CoreShards { get; set; }

  // Loadout & run snapshot
  public int UpgradesPurchased { get; set; }
  public int AbilitiesPurchased { get; set; }
  public int TalentsPurchased { get; set; }
  public string EquippedAbilities { get; set; } = "";
  public double PlanetDamagePerMinute { get; set; }
  public double PlanetDamageThisRun { get; set; }
}

[JsonSourceGenerationOptions(
  PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
  WriteIndented = false)]
[JsonSerializable(typeof(PlaytestFeedbackPayload))]
internal partial class PlaytestFeedbackJsonContext : JsonSerializerContext { }

public static class PlaytestFeedback
{
  // Paste your deployed Google Apps Script Web App URL here (or set FeedbackWebhookUrl in Settings.json
  // or BTB_FEEDBACK_WEBHOOK_URL in the environment). See FEEDBACK_SETUP.md for the Apps Script.
  public const string DefaultWebhookUrl = "https://script.google.com/macros/s/AKfycbzjFOF049vBsq5PG4ZxBpprN6PkvDXrrYToN0K_R_cVvsV0oibkMl1DpAbntUo9okcsQA/exec";

  public const int MaxCommentLength = 1000;
  public const int QuestionCount = 4;

  public static readonly string[] SurveyQuestions =
  [
    Loc.N("Overall Fun & Enjoyment"),
    Loc.N("Pacing & Progression"),
    Loc.N("Visuals & UI Clarity"),
    Loc.N("Performance & Controls")
  ];

  private static readonly HttpClient SharedHttpClient = new()
  {
    Timeout = TimeSpan.FromSeconds(12)
  };

  private static GameWindow boundWindow;
  private static double sessionSeconds;
  private static readonly int[] ratings = new int[QuestionCount];
  private static readonly StringBuilder commentBuffer = new();
  private static bool textFocused = true;
  private static float caretBlinkTimer;
  private static bool isSubmitting;
  private static string statusMessage = "";
  private static bool statusIsSuccess;
  private static bool statusIsError;
  private static float autoCloseTimer = -1f;

  private static long lastTextInputBackspaceTick = -1000;
  private static long lastTextInputEnterTick = -1000;
  private static float backspaceHoldTimer;

  public static string WebhookUrlOverride { get; set; }
  public static FeedbackMode Mode { get; private set; } = FeedbackMode.None;
  public static bool IsOpen => Mode != FeedbackMode.None;
  public static bool IsMainMenuFormOpen => Mode == FeedbackMode.MainMenuSurvey;
  public static bool IsInGameQuickOpen => Mode == FeedbackMode.InGameQuick;
  public static bool IsTextInputActive => IsOpen && textFocused && !isSubmitting && autoCloseTimer < 0f;

  public static string CurrentText => commentBuffer.ToString();
  public static int GetRating(int index) => index is >= 0 and < QuestionCount ? ratings[index] : 0;

  public static void SetRating(int index, int rating)
  {
    if (index is < 0 or >= QuestionCount) return;
    ratings[index] = Math.Clamp(rating, 0, 5);
  }

  public static void SetText(string text)
  {
    commentBuffer.Clear();
    if (string.IsNullOrEmpty(text)) return;
    foreach (char c in text)
    {
      if (commentBuffer.Length >= MaxCommentLength) break;
      if (c is '\r') continue;
      if (c == '\n' || !char.IsControl(c))
        commentBuffer.Append(c);
    }
  }

  public static void BindWindow(GameWindow window)
  {
    if (window == null || ReferenceEquals(boundWindow, window)) return;
    if (boundWindow != null)
      boundWindow.TextInput -= OnWindowTextInput;
    boundWindow = window;
    boundWindow.TextInput += OnWindowTextInput;
  }

  public static void RecordSessionTime(GameTime gameTime)
  {
    if (gameTime != null)
      sessionSeconds += Math.Max(0, gameTime.ElapsedGameTime.TotalSeconds);
  }

  public static void OpenMainMenuSurvey()
  {
    ResetFormState();
    Mode = FeedbackMode.MainMenuSurvey;
    textFocused = true;
  }

  public static void OpenInGameQuick()
  {
    ResetFormState();
    Mode = FeedbackMode.InGameQuick;
    textFocused = true;
  }

  public static void Close()
  {
    Mode = FeedbackMode.None;
    isSubmitting = false;
    autoCloseTimer = -1f;
    statusMessage = "";
  }

  private static void ResetFormState()
  {
    Array.Clear(ratings);
    commentBuffer.Clear();
    textFocused = true;
    caretBlinkTimer = 0f;
    isSubmitting = false;
    statusMessage = "";
    statusIsSuccess = false;
    statusIsError = false;
    autoCloseTimer = -1f;
    backspaceHoldTimer = 0f;
  }

  public static bool CanSubmit
  {
    get
    {
      if (isSubmitting || autoCloseTimer >= 0f) return false;
      bool hasText = !string.IsNullOrWhiteSpace(commentBuffer.ToString());
      if (Mode == FeedbackMode.InGameQuick)
        return hasText;
      bool hasRating = false;
      for (int i = 0; i < QuestionCount; i++)
        if (ratings[i] > 0) hasRating = true;
      return hasRating || hasText;
    }
  }

  private static void OnWindowTextInput(object sender, TextInputEventArgs args)
  {
    if (!IsTextInputActive) return;
    char c = args.Character;
    if (c == '\b')
    {
      lastTextInputBackspaceTick = Environment.TickCount64;
      DeleteLastCharacter();
    }
    else if (c is '\r' or '\n')
    {
      lastTextInputEnterTick = Environment.TickCount64;
      AppendCharacter('\n');
    }
    else if (!char.IsControl(c))
    {
      AppendCharacter(c);
    }
  }

  public static void AppendCharacter(char c)
  {
    if (commentBuffer.Length >= MaxCommentLength) return;
    if (c == '\n' || !char.IsControl(c))
    {
      commentBuffer.Append(c);
      caretBlinkTimer = 0f;
    }
  }

  public static void DeleteLastCharacter()
  {
    if (commentBuffer.Length == 0) return;
    int removeCount = 1;
    if (commentBuffer.Length >= 2 && char.IsLowSurrogate(commentBuffer[^1]) && char.IsHighSurrogate(commentBuffer[^2]))
      removeCount = 2;
    commentBuffer.Remove(commentBuffer.Length - removeCount, removeCount);
    caretBlinkTimer = 0f;
  }

  // Layout geometry in virtual HUD coordinates (3840 x 2160)
  public static Rectangle ModalBounds => Mode == FeedbackMode.InGameQuick
    ? new Rectangle((HudLayout.Width - 1440) / 2, (HudLayout.Bottom - 860) / 2, 1440, 860)
    : new Rectangle((HudLayout.Width - 1760) / 2, (HudLayout.Bottom - 1480) / 2, 1760, 1480);

  public static Rectangle QuestionRowBounds(int index)
  {
    var panel = ModalBounds;
    return new Rectangle(panel.X + 64, panel.Y + 178 + index * 122, panel.Width - 128, 108);
  }

  public static Rectangle RatingButtonBounds(int questionIndex, int ratingValue)
  {
    var row = QuestionRowBounds(questionIndex);
    const int btnWidth = 112;
    const int btnHeight = 72;
    const int gap = 16;
    int totalRightWidth = 5 * btnWidth + 4 * gap;
    int startX = row.Right - 28 - totalRightWidth;
    int index = ratingValue - 1;
    return new Rectangle(startX + index * (btnWidth + gap), row.Y + (row.Height - btnHeight) / 2, btnWidth, btnHeight);
  }

  public static Rectangle TextBoxBounds
  {
    get
    {
      var panel = ModalBounds;
      if (Mode == FeedbackMode.InGameQuick)
        return new Rectangle(panel.X + 64, panel.Y + 188, panel.Width - 128, 410);
      return new Rectangle(panel.X + 64, panel.Y + 726, panel.Width - 128, 460);
    }
  }

  public static Rectangle CancelButtonBounds
  {
    get
    {
      var panel = ModalBounds;
      int h = Mode == FeedbackMode.InGameQuick ? 92 : 100;
      int y = panel.Bottom - h - 48;
      return new Rectangle(panel.X + 64, y, 440, h);
    }
  }

  public static Rectangle SendButtonBounds
  {
    get
    {
      var panel = ModalBounds;
      int h = Mode == FeedbackMode.InGameQuick ? 92 : 100;
      int y = panel.Bottom - h - 48;
      return new Rectangle(panel.Right - 64 - 520, y, 520, h);
    }
  }

  public static Vector2 VirtualCursor()
  {
#if !KNI_WEB
    if (Capture.CaptureSession.Active) return GameInput.UiCursor;
#endif
    if (BaseGame.BoxingViewportAdapterGui == null)
      return GameInput.UiCursor;
    var vp = BaseGame.BoxingViewportAdapterGui.Viewport;
    var scale = BaseGame.BoxingViewportAdapterGui.GetScaleMatrix();
    Matrix.Invert(ref scale, out scale);
    var mouse = Mouse.GetState();
    return Vector2.Transform(new Vector2(mouse.X - vp.X, mouse.Y - vp.Y), scale);
  }

  public static void Update(GameTime gameTime)
  {
    if (!IsOpen) return;
    float dt = (float)(gameTime?.ElapsedGameTime.TotalSeconds ?? (1.0 / 60.0));
    caretBlinkTimer = (caretBlinkTimer + dt) % 1.06f;

    if (autoCloseTimer >= 0f)
    {
      autoCloseTimer -= dt;
      if (autoCloseTimer <= 0f)
        Close();
      return;
    }

    if (!GameInput.WindowActive) return;

    var keyboard = KeyboardExtended.GetState();
    if (keyboard.WasKeyPressed(Keys.Escape))
    {
      AudioManager.Instance?.PlaySound(SoundEvent.MenuClick);
      Close();
      return;
    }

    if (IsTextInputActive)
    {
      long nowTick = Environment.TickCount64;
      if (keyboard.WasKeyPressed(Keys.Back) && nowTick - lastTextInputBackspaceTick > 35)
      {
        DeleteLastCharacter();
        backspaceHoldTimer = 0.42f;
      }
      else if (keyboard.IsKeyDown(Keys.Back))
      {
        backspaceHoldTimer -= dt;
        if (backspaceHoldTimer <= 0f && nowTick - lastTextInputBackspaceTick > 35)
        {
          DeleteLastCharacter();
          backspaceHoldTimer = 0.045f;
        }
      }
      else
      {
        backspaceHoldTimer = 0f;
      }

      if (keyboard.WasKeyPressed(Keys.Enter) && nowTick - lastTextInputEnterTick > 35)
      {
        AppendCharacter('\n');
      }
    }

    var mouse = GameInput.Mouse;
    if (!mouse.WasButtonPressed(MouseButton.Left) || isSubmitting) return;

    var cursor = VirtualCursor();
    var point = new Point((int)cursor.X, (int)cursor.Y);

    if (CancelButtonBounds.Contains(point))
    {
      AudioManager.Instance?.PlaySound(SoundEvent.MenuClick);
      Close();
      return;
    }

    if (SendButtonBounds.Contains(point) && CanSubmit)
    {
      AudioManager.Instance?.PlaySound(SoundEvent.MenuClick);
      _ = SubmitCurrentAsync();
      return;
    }

    if (TextBoxBounds.Contains(point))
    {
      textFocused = true;
      caretBlinkTimer = 0f;
      return;
    }

    if (Mode == FeedbackMode.MainMenuSurvey)
    {
      for (int q = 0; q < QuestionCount; q++)
      {
        for (int r = 1; r <= 5; r++)
        {
          if (RatingButtonBounds(q, r).Contains(point))
          {
            ratings[q] = ratings[q] == r ? 0 : r;
            AudioManager.Instance?.PlaySound(SoundEvent.MenuClick);
            return;
          }
        }
      }
    }

    if (!ModalBounds.Contains(point) && commentBuffer.Length == 0)
    {
      AudioManager.Instance?.PlaySound(SoundEvent.MenuClick);
      Close();
    }
  }

  public static async Task<bool> SubmitCurrentAsync(HttpMessageHandler customHandler = null)
  {
    if (!CanSubmit) return false;
    isSubmitting = true;
    statusIsError = false;
    statusIsSuccess = false;
    statusMessage = Loc.T("Sending feedback...");

    var payload = BuildCurrentPayload();
    try
    {
      bool uploaded = await SendPayloadAsync(payload, customHandler);
      isSubmitting = false;
      statusIsSuccess = true;
      statusMessage = uploaded
        ? Loc.T("Thank you! Your feedback has been sent.")
        : Loc.T("Thank you! Feedback saved.");
      autoCloseTimer = 0.85f;
      return true;
    }
    catch (Exception e)
    {
      Log.Warning(e, "Could not upload playtest feedback");
      isSubmitting = false;
      statusIsError = true;
      statusMessage = Loc.T("Saved locally (could not reach feedback server).");
      autoCloseTimer = 1.25f;
      return false;
    }
  }

  public static PlaytestFeedbackPayload BuildCurrentPayload()
    => BuildPayload(
      mode: Mode == FeedbackMode.InGameQuick ? FeedbackMode.InGameQuick : FeedbackMode.MainMenuSurvey,
      message: commentBuffer.ToString().Trim(),
      ratingsSnapshot: ratings);

  public static PlaytestFeedbackPayload BuildPayload(
    FeedbackMode mode,
    string message,
    IReadOnlyList<int> ratingsSnapshot = null,
    GameState liveState = null,
    Upgrades liveUpgrades = null,
    IReadOnlyList<string> equippedOverride = null,
    GameSave fallbackSave = null)
  {
    bool isQuick = mode == FeedbackMode.InGameQuick;
    var screen = UntitledGemGameGameScreen.Instance;
    liveState ??= screen?.State;
    liveUpgrades ??= UpgradeManager.CurrentUpgrades;

    if (liveState == null && fallbackSave == null)
    {
      try
      {
        fallbackSave = new GameSaveStore(GameSaveStore.DefaultPath).Load();
      }
      catch
      {
        fallbackSave = null;
      }
    }

    var payload = new PlaytestFeedbackPayload
    {
      Sheet = isQuick ? "QuickFeedback" : "MainMenuFeedback",
      TimestampUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
      Source = isQuick ? "InGame" : "MainMenu",
      OverallFun = !isQuick && ratingsSnapshot is { Count: > 0 } ? Math.Clamp(ratingsSnapshot[0], 0, 5) : 0,
      PacingProgression = !isQuick && ratingsSnapshot is { Count: > 1 } ? Math.Clamp(ratingsSnapshot[1], 0, 5) : 0,
      VisualsUiClarity = !isQuick && ratingsSnapshot is { Count: > 2 } ? Math.Clamp(ratingsSnapshot[2], 0, 5) : 0,
      PerformanceControls = !isQuick && ratingsSnapshot is { Count: > 3 } ? Math.Clamp(ratingsSnapshot[3], 0, 5) : 0,
      Message = message ?? "",
      GameVersion = Demo.Version,
      VersionLabel = Demo.VersionLabel,
      IsDemo = Demo.IsDemo,
      IsDev = Demo.IsDev,
      Platform = DescribePlatform(),
      Resolution = DescribeResolution(),
      Language = Loc.Current?.Code ?? "en",
      CurrentFps = Math.Round(BaseGame.m_frameCounter?.AverageFramesPerSecond ?? 0f, 1),
      SessionPlayTimeSeconds = Math.Round(sessionSeconds, 1)
    };

    if (liveState != null)
    {
      ulong redGems = PrestigeProgression.AddSaturating(liveState.CurrentRedGemCount, UntitledGemGameGameScreen.DeliveredUncounted);
      ulong redEarned = PrestigeProgression.AddSaturating(liveState.RedGemsEarnedThisRun, UntitledGemGameGameScreen.DeliveredUncounted);
      payload.CoreExtractions = liveState.CoreExtractions;
      payload.PrestigePointsEarned = liveState.PrestigePointsEarned;
      payload.PendingPrestigePoints = liveState.PendingPrestigePoints;
      payload.CoreFractures = liveState.CoreFractures;
      payload.CoreDrillTunnels = liveState.CoreDrillTunnels;
      payload.RedGems = redGems.ToString(CultureInfo.InvariantCulture);
      payload.RedGemsEarnedThisRun = redEarned.ToString(CultureInfo.InvariantCulture);
      payload.BlueGems = liveState.CurrentBlueGemCount.ToString(CultureInfo.InvariantCulture);
      payload.PurpleGems = liveState.CurrentPurpleGemCount.ToString(CultureInfo.InvariantCulture);
      payload.CoreShards = liveState.CurrentCoreShardCount;
      payload.PlanetDamagePerMinute = Math.Round(liveState.Damage.TotalPerMinute, 1);
      payload.PlanetDamageThisRun = Math.Round(liveState.Damage.TotalThisRun, 1);
    }
    else if (fallbackSave != null)
    {
      payload.CoreExtractions = fallbackSave.CoreExtractions;
      payload.PrestigePointsEarned = fallbackSave.PrestigePointsEarned;
      payload.PendingPrestigePoints = fallbackSave.PendingPrestigePoints;
      payload.CoreFractures = fallbackSave.CoreFractures;
      payload.CoreDrillTunnels = fallbackSave.CoreDrillTunnels;
      payload.RedGems = fallbackSave.RedGems.ToString(CultureInfo.InvariantCulture);
      payload.RedGemsEarnedThisRun = fallbackSave.RedGemsEarnedThisRun.ToString(CultureInfo.InvariantCulture);
      payload.BlueGems = fallbackSave.BlueGems.ToString(CultureInfo.InvariantCulture);
      payload.PurpleGems = fallbackSave.PurpleGems.ToString(CultureInfo.InvariantCulture);
      payload.CoreShards = fallbackSave.CoreShards;
      payload.PlanetDamagePerMinute = 0;
      payload.PlanetDamageThisRun = fallbackSave.DamageThisRun != null
        ? Math.Round(fallbackSave.DamageThisRun.Values.Sum(), 1) : 0;
    }

    if (liveUpgrades != null && (liveUpgrades.UpgradeButtons.Count > 0
      || liveUpgrades.UpgradeButtonsAbilities.Count > 0
      || liveUpgrades.UpgradeButtonsMeta.Count > 0))
    {
      payload.UpgradesPurchased = liveUpgrades.UpgradeButtons.Values.Sum(b => Math.Max(0, b.CurrentLevel));
      payload.AbilitiesPurchased = liveUpgrades.UpgradeButtonsAbilities.Values.Sum(b => Math.Max(0, b.CurrentLevel));
      payload.TalentsPurchased = liveUpgrades.UpgradeButtonsMeta.Values.Sum(b => Math.Max(0, b.CurrentLevel));
    }
    else if (fallbackSave != null)
    {
      payload.UpgradesPurchased = fallbackSave.Upgrades?.Values.Sum(v => Math.Max(0, v)) ?? 0;
      payload.AbilitiesPurchased = fallbackSave.Abilities?.Values.Sum(v => Math.Max(0, v)) ?? 0;
      payload.TalentsPurchased = fallbackSave.Meta?.Values.Sum(v => Math.Max(0, v)) ?? 0;
    }

    var equipped = equippedOverride
      ?? HomeBase.Instance?.GetEquippedAbilities()
      ?? (IReadOnlyList<string>)fallbackSave?.EquippedAbilities;
    if (equipped != null)
      payload.EquippedAbilities = string.Join(", ", equipped.Where(a => !string.IsNullOrWhiteSpace(a)));

    return payload;
  }

  public static string SerializePayload(PlaytestFeedbackPayload payload)
    => JsonSerializer.Serialize(payload, PlaytestFeedbackJsonContext.Default.PlaytestFeedbackPayload);

  public static PlaytestFeedbackPayload DeserializePayload(string json)
    => JsonSerializer.Deserialize(json, PlaytestFeedbackJsonContext.Default.PlaytestFeedbackPayload);

  public static string ResolveWebhookUrl()
  {
    if (!string.IsNullOrWhiteSpace(WebhookUrlOverride))
      return WebhookUrlOverride.Trim();
    if (GameMain.Instance is GameMain gm && !string.IsNullOrWhiteSpace(gm.PlayerSettings?.FeedbackWebhookUrl))
      return gm.PlayerSettings.FeedbackWebhookUrl.Trim();
    string env = Environment.GetEnvironmentVariable("BTB_FEEDBACK_WEBHOOK_URL");
    if (!string.IsNullOrWhiteSpace(env))
      return env.Trim();
    return DefaultWebhookUrl.Trim();
  }

  public static string LocalLogPathOverride { get; set; }

  public static string LocalFeedbackLogPath
  {
    get
    {
      if (!string.IsNullOrWhiteSpace(LocalLogPathOverride))
        return LocalLogPathOverride;
#if KNI_WEB
      return "/saves/feedback_log.jsonl";
#else
      string saveDir = Path.GetDirectoryName(Path.GetFullPath(GameSaveStore.DefaultPath))
        ?? AppDomain.CurrentDomain.BaseDirectory;
      return Path.Combine(saveDir, "feedback_log.jsonl");
#endif
    }
  }

  private static void AppendLocalLog(string json)
  {
    try
    {
#if !KNI_WEB
      if (Capture.CaptureSession.Active) return;
#endif
      string path = LocalFeedbackLogPath;
      Directory.CreateDirectory(Path.GetDirectoryName(path)!);
      File.AppendAllText(path, json + Environment.NewLine, Encoding.UTF8);
    }
    catch (Exception e)
    {
      Log.Warning(e, "Could not write local playtest feedback backup");
    }
  }

  public static async Task<bool> SendPayloadAsync(PlaytestFeedbackPayload payload, HttpMessageHandler customHandler = null)
  {
    string json = SerializePayload(payload);
    AppendLocalLog(json);

    string url = ResolveWebhookUrl();
    if (string.IsNullOrWhiteSpace(url))
      return false;

    using var ownedClient = customHandler != null ? new HttpClient(customHandler, disposeHandler: false) : null;
    var client = ownedClient ?? SharedHttpClient;

    // text/plain avoids CORS preflight in browser builds while Google Apps Script still reads e.postData.contents as JSON.
    using var content = new StringContent(json, Encoding.UTF8, "text/plain");
    using var response = await client.PostAsync(url, content);
    if (!response.IsSuccessStatusCode && (int)response.StatusCode is not (301 or 302 or 303 or 307 or 308))
      throw new HttpRequestException($"Feedback server returned HTTP {(int)response.StatusCode}");
    return true;
  }

  private static string DescribePlatform()
  {
    if (OperatingSystem.IsBrowser())
      return "Browser (Wasm)";
    return $"{RuntimeInformation.OSDescription.Trim()} ({RuntimeInformation.ProcessArchitecture})";
  }

  private static string DescribeResolution()
  {
    var gd = GameMain.Instance?.GraphicsDevice;
    if (gd == null) return "Unknown";
    int w = gd.PresentationParameters.BackBufferWidth;
    int h = gd.PresentationParameters.BackBufferHeight;
    if (GameMain.Instance is GameMain gm && gm.PlayerSettings is { } s)
    {
      string mode = s.IsFullscreen ? (s.IsBorderless ? "Borderless" : "Fullscreen") : "Windowed";
      return $"{w}x{h} ({mode})";
    }
    return $"{w}x{h}";
  }

  // Drawing
  public static void Draw(SpriteBatch batch)
  {
    if (!IsOpen || batch == null) return;

    var textRenderer = FontManager.GetTextRenderer(GameFonts.Text);
    if (textRenderer != null)
    {
      textRenderer.SetOrtographicProjection(HudLayout.Width, HudLayout.Bottom);
      textRenderer.UseScreenSpace = false;
    }

    var panel = ModalBounds;
    var cursor = VirtualCursor();
    var point = new Point((int)cursor.X, (int)cursor.Y);
    var textBox = TextBoxBounds;
    var cancelBtn = CancelButtonBounds;
    var sendBtn = SendButtonBounds;
    bool canSend = CanSubmit;

    batch.Begin(transformMatrix: HudLayout.RenderTransform);

    // Dimmed full-screen backdrop
    batch.Draw(AssetManager.DefaultTexture, new Rectangle(0, 0, HudLayout.Width, HudLayout.Bottom),
      new Color(2, 8, 18, 208));

    // Main OrbitSkin tactical modal panel with header
    OrbitSkin.Panel(batch, panel, header: true, OrbitSkin.Accent);

    if (Mode == FeedbackMode.MainMenuSurvey)
    {
      for (int q = 0; q < QuestionCount; q++)
      {
        var row = QuestionRowBounds(q);
        OrbitSkin.TechCard(batch, row, GuiPalette.Base(new Color(8, 22, 40, 232)),
          ratings[q] > 0 ? OrbitSkin.Accent : OrbitSkin.BorderColor * 0.75f,
          hovered: row.Contains(point), selected: ratings[q] > 0, cornerGraphic: false);

        for (int r = 1; r <= 5; r++)
        {
          var rBounds = RatingButtonBounds(q, r);
          bool selected = ratings[q] == r;
          bool hovered = !isSubmitting && rBounds.Contains(point);
          OrbitSkin.HudButton(batch, rBounds, OrbitSkin.Accent, selected, hovered, confirm: selected);
        }
      }
    }

    // Recessed text input bay
    OrbitSkin.BlueprintBay(batch, textBox, GuiPalette.Base(new Color(5, 16, 30, 244)),
      textFocused ? OrbitSkin.Accent : OrbitSkin.BorderColor * 0.7f);

    // Action buttons
    bool cancelHovered = !isSubmitting && cancelBtn.Contains(point);
    bool sendHovered = canSend && sendBtn.Contains(point);
    OrbitSkin.HudButton(batch, cancelBtn, OrbitSkin.Accent, false, cancelHovered);
    OrbitSkin.HudButton(batch, sendBtn, canSend ? OrbitSkin.ConfirmAccent : OrbitSkin.BorderColor * 0.4f,
      canSend, sendHovered, confirm: canSend);

    batch.End();

    // Header & titles
    string title = Mode == FeedbackMode.InGameQuick
      ? Loc.T("QUICK FEEDBACK")
      : Loc.T("PLAYTEST FEEDBACK");
    DrawCenteredText(title, panel.Center.X, panel.Y + OrbitSkin.PanelHeaderHeight / 2f, 42f, OrbitSkin.StatHeadingColor);

    if (Mode == FeedbackMode.MainMenuSurvey)
    {
      DrawFittedText(Loc.T("Rate your experience (1 = Low, 5 = High) and share any thoughts below:"),
        new Vector2(panel.X + 64, panel.Y + 118), panel.Width - 128, 28f, OrbitSkin.MutedTextColor);

      for (int q = 0; q < QuestionCount; q++)
      {
        var row = QuestionRowBounds(q);
        int buttonsLeft = RatingButtonBounds(q, 1).X;
        DrawFittedText(Loc.T(SurveyQuestions[q]),
          new Vector2(row.X + 32, row.Y + 34), buttonsLeft - row.X - 56, 32f, OrbitSkin.ButtonTextColor);

        for (int r = 1; r <= 5; r++)
        {
          var rBounds = RatingButtonBounds(q, r);
          bool selected = ratings[q] == r;
          DrawCenteredText(r.ToString(CultureInfo.InvariantCulture),
            rBounds.Center.X, rBounds.Center.Y, 32f,
            selected ? Color.White : OrbitSkin.ButtonTextColor);
        }
      }

      DrawFittedText(Loc.T("Additional Comments / Bug Reports / Suggestions"),
        new Vector2(textBox.X, textBox.Y - 46), textBox.Width - 220, 28f, OrbitSkin.StatHeadingColor);
    }
    else
    {
      DrawFittedText(Loc.T("Game paused while typing. Share a quick thought, bug, or suggestion:"),
        new Vector2(textBox.X, panel.Y + 124), textBox.Width - 220, 28f, OrbitSkin.MutedTextColor);
    }

    string counter = $"{commentBuffer.Length} / {MaxCommentLength}";
    float counterWidth = MeasureText(counter, 24f).X;
    DrawFittedText(counter, new Vector2(textBox.Right - counterWidth, textBox.Y - 42),
      counterWidth + 8, 24f, OrbitSkin.MutedTextColor);

    DrawTextBoxContents(textBox);

    // Status or telemetry info line
    float statusY = cancelBtn.Y - 54;
    if (!string.IsNullOrEmpty(statusMessage))
    {
      Color statusColor = statusIsError ? OrbitSkin.LockedTextColor
        : statusIsSuccess ? OrbitSkin.ConfirmAccent
        : OrbitSkin.Accent;
      DrawFittedText(statusMessage, new Vector2(panel.X + 64, statusY), panel.Width - 128, 28f, statusColor);
    }
    else
    {
      DrawFittedText(Loc.F("Automatically includes {0} build & progression stats.", Demo.VersionLabel),
        new Vector2(panel.X + 64, statusY), panel.Width - 128, 24f, OrbitSkin.MutedTextColor);
    }

    DrawCenteredText(Loc.T("Cancel"), cancelBtn.Center.X, cancelBtn.Center.Y, 34f,
      cancelHovered ? Color.White : OrbitSkin.ButtonTextColor);
    DrawCenteredText(isSubmitting ? Loc.T("Sending...") : Loc.T("Send Feedback"),
      sendBtn.Center.X, sendBtn.Center.Y, 34f,
      canSend ? Color.White : OrbitSkin.DisabledTextColor);
  }

  private static void DrawTextBoxContents(Rectangle textBox)
  {
    const float fontSize = 30f;
    const float lineHeight = 40f;
    const int padX = 28;
    const int padY = 24;
    float maxLineWidth = textBox.Width - padX * 2;
    int maxVisibleLines = Math.Max(1, (int)((textBox.Height - padY * 2) / lineHeight));

    string raw = commentBuffer.ToString();
    bool showCaret = IsTextInputActive && caretBlinkTimer < 0.53f;

    if (raw.Length == 0)
    {
      string placeholder = Mode == FeedbackMode.InGameQuick
        ? Loc.T("Type your feedback here... (Press Esc to cancel)")
        : Loc.T("Type anything about the game here...");
      DrawFittedText(placeholder, new Vector2(textBox.X + padX, textBox.Y + padY),
        maxLineWidth, fontSize, OrbitSkin.MutedTextColor * 0.65f);
      if (showCaret)
      {
        FontManager.RenderFieldFont(GameFonts.Text, "|",
          new Vector2(textBox.X + padX, textBox.Y + padY), OrbitSkin.Accent, Color.Black, fontSize);
      }
      return;
    }

    string display = showCaret ? raw + "|" : raw;
    var lines = WrapTextLines(display, maxLineWidth, fontSize);
    int startLine = Math.Max(0, lines.Count - maxVisibleLines);
    for (int i = startLine; i < lines.Count; i++)
    {
      int row = i - startLine;
      float y = textBox.Y + padY + row * lineHeight;
      FontManager.RenderFieldFont(GameFonts.Text, lines[i],
        new Vector2(textBox.X + padX, y), OrbitSkin.ButtonTextColor, Color.Black, fontSize);
    }
  }

  public static List<string> WrapTextLines(string text, float maxWidth, float fontSize)
  {
    var lines = new List<string>();
    if (string.IsNullOrEmpty(text))
    {
      lines.Add("");
      return lines;
    }

    var paragraphs = text.Split('\n');
    foreach (string paragraph in paragraphs)
    {
      if (paragraph.Length == 0)
      {
        lines.Add("");
        continue;
      }

      string current = "";
      foreach (var (piece, join) in Loc.WrapPieces(paragraph))
      {
        string candidate = current.Length == 0 ? piece : current + join + piece;
        if (current.Length > 0 && MeasureText(candidate, fontSize).X > maxWidth)
        {
          lines.Add(current);
          current = piece;
        }
        else
        {
          current = candidate;
        }

        // Break very long single tokens that exceed maxWidth on their own
        while (current.Length > 1 && MeasureText(current, fontSize).X > maxWidth)
        {
          int split = current.Length - 1;
          while (split > 1 && MeasureText(current[..split], fontSize).X > maxWidth)
            split--;
          lines.Add(current[..split]);
          current = current[split..];
        }
      }
      lines.Add(current);
    }
    return lines;
  }

  private static Vector2 MeasureText(string text, float fontSize)
  {
    if (string.IsNullOrEmpty(text)) return new Vector2(0, fontSize);
    var r = FontManager.GetTextRenderer(GameFonts.Text);
    if (r?.Font == null) return new Vector2(text.Length * fontSize * 0.52f, fontSize);
    r.PositiveYIsDown = true;
    return r.MeasureText(text, Vector2.Zero, 1, 1.171875f, fontSize,
      Color.Transparent, Color.Transparent, r.EnableKerning, r.PositiveYIsDown,
      r.PositionByBaseline, 0, Vector2.Zero, true, -1);
  }

  private static void DrawFittedText(string text, Vector2 position, float maxWidth, float fontSize, Color color)
  {
    if (string.IsNullOrEmpty(text)) return;
    var measure = MeasureText(text, fontSize);
    fontSize *= Math.Min(1f, maxWidth / Math.Max(1f, measure.X));
    FontManager.RenderFieldFont(GameFonts.Text, text, position, color, Color.Black, fontSize);
  }

  private static void DrawCenteredText(string text, float centerX, float centerY, float fontSize, Color color)
  {
    if (string.IsNullOrEmpty(text)) return;
    var measure = MeasureText(text, fontSize);
    var pos = new Vector2(centerX - measure.X * 0.5f, centerY - measure.Y * 0.5f);
    FontManager.RenderFieldFont(GameFonts.Text, text, pos, color, Color.Black, fontSize);
  }
}
