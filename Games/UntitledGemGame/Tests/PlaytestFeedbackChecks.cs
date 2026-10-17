using System.Net;
using Microsoft.Xna.Framework;
using UntitledGemGame;
using UntitledGemGame.Screens;

internal static class PlaytestFeedbackChecks
{
  private sealed class RecordingHttpMessageHandler : HttpMessageHandler
  {
    public List<(Uri RequestUri, string Body)> Requests { get; } = new();
    public HttpStatusCode StatusToReturn { get; set; } = HttpStatusCode.OK;
    public string ResponseBody { get; set; } = "{\"ok\":true}";

    protected override async Task<HttpResponseMessage> SendAsync(
      HttpRequestMessage request,
      CancellationToken cancellationToken)
    {
      string body = request.Content != null
        ? await request.Content.ReadAsStringAsync(cancellationToken)
        : string.Empty;
      Requests.Add((request.RequestUri, body));
      return new HttpResponseMessage(StatusToReturn)
      {
        Content = new StringContent(ResponseBody)
      };
    }
  }

  public static void Run()
  {
    int checks = 0;
    void Check(bool condition, string message)
    {
      if (!condition)
        throw new Exception(message);
      checks++;
    }

    string tempDir = Path.Combine(Path.GetTempPath(), $"gem-feedback-checks-{Guid.NewGuid():N}");
    Directory.CreateDirectory(tempDir);
    string localLogFile = Path.Combine(tempDir, "feedback_log.jsonl");
    var handler = new RecordingHttpMessageHandler();

    try
    {
      PlaytestFeedback.LocalLogPathOverride = localLogFile;
      PlaytestFeedback.WebhookUrlOverride = "https://script.google.com/macros/s/test-deployment-id/exec";

      // 1. HUD button layout check
      Rectangle hudBounds = new(0, 0, HudLayout.Width, HudLayout.Bottom);
      Check(hudBounds.Contains(HudLayout.FeedbackButton),
        "HUD feedback button must lie inside the 3840x2160 virtual HUD canvas");
      Check(HudLayout.FeedbackButton.Y >= HudLayout.Top,
        "HUD feedback button must sit inside the bottom HUD bar out of the gem play area");
      Check(!HudLayout.FeedbackButton.Intersects(HudLayout.DamageButton),
        "HUD feedback button must not overlap the Damage button");

      // 2. Main Menu Survey flow
      PlaytestFeedback.Close();
      Check(!PlaytestFeedback.IsOpen && PlaytestFeedback.Mode == FeedbackMode.None,
        "Feedback modal starts closed");

      PlaytestFeedback.OpenMainMenuSurvey();
      Check(PlaytestFeedback.IsOpen
        && PlaytestFeedback.IsMainMenuFormOpen
        && PlaytestFeedback.Mode == FeedbackMode.MainMenuSurvey,
        "OpenMainMenuSurvey must open the survey modal");

      PlaytestFeedback.SetRating(0, 5);
      PlaytestFeedback.SetRating(1, 4);
      PlaytestFeedback.SetRating(2, 3);
      PlaytestFeedback.SetRating(3, 5);
      Check(PlaytestFeedback.GetRating(0) == 5
        && PlaytestFeedback.GetRating(1) == 4
        && PlaytestFeedback.GetRating(2) == 3
        && PlaytestFeedback.GetRating(3) == 5,
        "Survey ratings must store 1-5 scores for each question");

      foreach (char c in "Really fun loop!X")
        PlaytestFeedback.AppendCharacter(c);
      PlaytestFeedback.DeleteLastCharacter();
      PlaytestFeedback.AppendCharacter('\n');
      foreach (char c in "Love the lasers.")
        PlaytestFeedback.AppendCharacter(c);

      Check(PlaytestFeedback.CurrentText == "Really fun loop!\nLove the lasers.",
        $"Text input should support typing, backspace, and newline, got: {PlaytestFeedback.CurrentText}");

      var fakeSave = new GameSave
      {
        CoreExtractions = 3,
        PrestigePointsEarned = 9,
        PendingPrestigePoints = 2,
        CoreFractures = 4,
        CoreDrillTunnels = 2,
        RedGems = 420,
        RedGemsEarnedThisRun = 1200,
        BlueGems = 85,
        PurpleGems = 12,
        CoreShards = 7,
        Upgrades = new Dictionary<string, int> { ["DMG1"] = 3, ["SPD1"] = 2 },
        Abilities = new Dictionary<string, int> { ["SYS1"] = 1 },
        Meta = new Dictionary<string, int> { ["TR1"] = 2 },
        EquippedAbilities = new List<string> { "Overcharge", "LaserBeam" },
        DamageThisRun = new Dictionary<string, double> { ["Laser"] = 250.0 }
      };

      PlaytestFeedbackPayload surveyPayload = PlaytestFeedback.BuildPayload(
        mode: FeedbackMode.MainMenuSurvey,
        message: PlaytestFeedback.CurrentText,
        ratingsSnapshot: new[] { 5, 4, 3, 5 },
        fallbackSave: fakeSave);

      Check(surveyPayload.Sheet == "MainMenuFeedback",
        "Main menu survey must target the MainMenuFeedback sheet tab");
      Check(surveyPayload.Source == "MainMenu",
        "Main menu survey must record MainMenu as the source");
      Check(surveyPayload.OverallFun == 5
        && surveyPayload.PacingProgression == 4
        && surveyPayload.VisualsUiClarity == 3
        && surveyPayload.PerformanceControls == 5,
        "Survey payload must include all 4 question ratings");
      Check(surveyPayload.Message == "Really fun loop!\nLove the lasers.",
        "Survey payload must include the free-text comment");
      Check(!string.IsNullOrWhiteSpace(surveyPayload.GameVersion)
        && !string.IsNullOrWhiteSpace(surveyPayload.Platform)
        && !string.IsNullOrWhiteSpace(surveyPayload.Resolution)
        && !string.IsNullOrWhiteSpace(surveyPayload.Language),
        "Survey payload must attach game version, platform, resolution, and language");
      Check(surveyPayload.RedGems == "420"
        && surveyPayload.BlueGems == "85"
        && surveyPayload.PurpleGems == "12"
        && surveyPayload.CoreShards == 7,
        "Survey payload must attach currency counts");
      Check(surveyPayload.UpgradesPurchased == 5
        && surveyPayload.AbilitiesPurchased == 1
        && surveyPayload.TalentsPurchased == 2
        && surveyPayload.EquippedAbilities == "Overcharge, LaserBeam",
        "Survey payload must attach purchased upgrades/abilities/talents and equipped abilities");
      Check(Math.Abs(surveyPayload.PlanetDamageThisRun - 250.0) < 0.01,
        "Survey payload must attach planet run damage stats");

      bool surveySent = PlaytestFeedback.SubmitCurrentAsync(handler).GetAwaiter().GetResult();
      Check(surveySent && handler.Requests.Count == 1,
        "Submitting survey must POST to the configured webhook URL");
      var receivedSurvey = PlaytestFeedback.DeserializePayload(handler.Requests[0].Body)!;
      Check(receivedSurvey.Sheet == "MainMenuFeedback"
        && receivedSurvey.OverallFun == 5
        && receivedSurvey.PacingProgression == 4
        && receivedSurvey.VisualsUiClarity == 3
        && receivedSurvey.PerformanceControls == 5
        && receivedSurvey.Message == "Really fun loop!\nLove the lasers.",
        "Deserialized webhook JSON for survey must match the submitted payload");

      // 3. In-Game HUD Quick Feedback flow
      PlaytestFeedback.OpenInGameQuick();
      Check(PlaytestFeedback.IsOpen
        && PlaytestFeedback.IsInGameQuickOpen
        && PlaytestFeedback.Mode == FeedbackMode.InGameQuick,
        "OpenInGameQuick must open the compact in-game feedback modal");
      Check(string.IsNullOrEmpty(PlaytestFeedback.CurrentText),
        "Opening quick feedback must clear previous text input");
      Check(!PlaytestFeedback.CanSubmit,
        "In-game quick feedback cannot be submitted while empty");

      foreach (char c in "Quick note: blue gem visibility is great!")
        PlaytestFeedback.AppendCharacter(c);
      Check(PlaytestFeedback.CanSubmit,
        "In-game quick feedback can be submitted once text is entered");

      PlaytestFeedbackPayload quickPayload = PlaytestFeedback.BuildCurrentPayload();
      Check(quickPayload.Sheet == "QuickFeedback",
        "In-game quick feedback must target the QuickFeedback sheet tab");
      Check(quickPayload.Source == "InGame",
        "In-game quick feedback must record InGame as the source");
      Check(quickPayload.OverallFun == 0
        && quickPayload.PacingProgression == 0
        && quickPayload.VisualsUiClarity == 0
        && quickPayload.PerformanceControls == 0,
        "In-game quick feedback should have 0 for survey-only rating fields");
      Check(quickPayload.Message == "Quick note: blue gem visibility is great!",
        "Quick feedback payload must include the typed message");

      bool quickSent = PlaytestFeedback.SubmitCurrentAsync(handler).GetAwaiter().GetResult();
      Check(quickSent && handler.Requests.Count == 2,
        "Submitting quick feedback must POST a second request to the webhook");
      var receivedQuick = PlaytestFeedback.DeserializePayload(handler.Requests[1].Body)!;
      Check(receivedQuick.Sheet == "QuickFeedback"
        && receivedQuick.Source == "InGame"
        && receivedQuick.Message == "Quick note: blue gem visibility is great!",
        "Deserialized webhook JSON for quick feedback must target QuickFeedback with full game stats");

      Check(File.Exists(localLogFile) && File.ReadAllLines(localLogFile).Length == 2,
        "Both feedback submissions must also be appended to the local JSONL backup log");

      PlaytestFeedback.Close();
      Console.WriteLine($"Passed {checks} playtest feedback checks.");
    }
    finally
    {
      PlaytestFeedback.Close();
      PlaytestFeedback.LocalLogPathOverride = null;
      PlaytestFeedback.WebhookUrlOverride = null;
      if (Directory.Exists(tempDir))
        Directory.Delete(tempDir, recursive: true);
    }
  }
}
