# Playtest Feedback — Google Sheets Setup

The playtest feedback system sends JSON payloads to a **Google Apps Script Web App** attached to a Google Sheet, routing submissions into two separate tabs:

1. **`MainMenuFeedback`** — Receives the Main Menu survey responses (4 rating questions 1–5 + free-text comment + build & progression stats).
2. **`QuickFeedback`** — Receives the In-Game HUD quick feedback notes (free-text message + build & current run stats).

Every submission is also saved locally to `feedback_log.jsonl` in the save folder (`%LOCALAPPDATA%/UntitledGemGame/feedback_log.jsonl` on Windows, `~/.local/share/UntitledGemGame/feedback_log.jsonl` on Linux) so feedback is never lost if a playtester is offline.

---

## 1. Create the Google Sheet & Apps Script

1. Open [Google Sheets](https://sheets.google.com) and create a new spreadsheet (e.g., **Beyond the Belt — Playtest Feedback**).
2. Click **Extensions → Apps Script**.
3. Replace the contents of `Code.gs` with the following script:

```javascript
const MAIN_MENU_COLUMNS = [
  ["timestampUtc", "Timestamp (UTC)"],
  ["gameVersion", "Game Version"],
  ["versionLabel", "Version Label"],
  ["overallFun", "Overall Fun (1-5)"],
  ["pacingProgression", "Pacing & Progression (1-5)"],
  ["visualsUiClarity", "Visuals & UI Clarity (1-5)"],
  ["performanceControls", "Performance & Controls (1-5)"],
  ["message", "Comments"],
  ["sessionPlayTimeSeconds", "Session Time (s)"],
  ["coreExtractions", "Core Extractions"],
  ["prestigePointsEarned", "Prestige Points Earned"],
  ["pendingPrestigePoints", "Pending Prestige Points"],
  ["coreFractures", "Core Fractures"],
  ["coreDrillTunnels", "Core Drill Tunnels"],
  ["redGems", "Red Gems"],
  ["redGemsEarnedThisRun", "Red Gems (This Run)"],
  ["blueGems", "Power Cells"],
  ["purpleGems", "Prestige Wallet"],
  ["coreShards", "Core Shards"],
  ["upgradesPurchased", "Upgrades Bought"],
  ["abilitiesPurchased", "Systems Bought"],
  ["talentsPurchased", "Talents Bought"],
  ["equippedAbilities", "Equipped Abilities"],
  ["planetDamagePerMinute", "Planet Damage / Min"],
  ["planetDamageThisRun", "Planet Damage (Run)"],
  ["platform", "Platform"],
  ["resolution", "Resolution"],
  ["language", "Language"],
  ["currentFps", "FPS"],
  ["isDemo", "Is Demo"],
  ["isDev", "Is Dev"]
];

const QUICK_FEEDBACK_COLUMNS = [
  ["timestampUtc", "Timestamp (UTC)"],
  ["gameVersion", "Game Version"],
  ["versionLabel", "Version Label"],
  ["message", "Quick Feedback"],
  ["sessionPlayTimeSeconds", "Session Time (s)"],
  ["coreExtractions", "Core Extractions"],
  ["prestigePointsEarned", "Prestige Points Earned"],
  ["pendingPrestigePoints", "Pending Prestige Points"],
  ["coreFractures", "Core Fractures"],
  ["coreDrillTunnels", "Core Drill Tunnels"],
  ["redGems", "Red Gems"],
  ["redGemsEarnedThisRun", "Red Gems (This Run)"],
  ["blueGems", "Power Cells"],
  ["purpleGems", "Prestige Wallet"],
  ["coreShards", "Core Shards"],
  ["upgradesPurchased", "Upgrades Bought"],
  ["abilitiesPurchased", "Systems Bought"],
  ["talentsPurchased", "Talents Bought"],
  ["equippedAbilities", "Equipped Abilities"],
  ["planetDamagePerMinute", "Planet Damage / Min"],
  ["planetDamageThisRun", "Planet Damage (Run)"],
  ["platform", "Platform"],
  ["resolution", "Resolution"],
  ["language", "Language"],
  ["currentFps", "FPS"],
  ["isDemo", "Is Demo"],
  ["isDev", "Is Dev"]
];

function doPost(e) {
  const lock = LockService.getScriptLock();
  lock.waitLock(10000);
  try {
    const raw = e && e.postData && e.postData.contents ? e.postData.contents : "{}";
    const data = JSON.parse(raw);
    const isQuick = data.sheet === "QuickFeedback" || data.source === "InGame";
    const sheetName = isQuick ? "QuickFeedback" : "MainMenuFeedback";
    const columns = isQuick ? QUICK_FEEDBACK_COLUMNS : MAIN_MENU_COLUMNS;

    const ss = SpreadsheetApp.getActiveSpreadsheet();
    let sheet = ss.getSheetByName(sheetName);
    if (!sheet) {
      sheet = ss.insertSheet(sheetName);
    }

    if (sheet.getLastRow() === 0) {
      const headers = columns.map(col => col[1]);
      sheet.appendRow(headers);
      sheet.getRange(1, 1, 1, headers.length).setFontWeight("bold");
      sheet.setFrozenRows(1);
    }

    const row = columns.map(col => {
      const key = col[0];
      const val = data[key];
      return val === undefined || val === null ? "" : val;
    });

    sheet.appendRow(row);

    return ContentService
      .createTextOutput(JSON.stringify({ status: "ok", sheet: sheetName }))
      .setMimeType(ContentService.MimeType.JSON);
  } catch (err) {
    return ContentService
      .createTextOutput(JSON.stringify({ status: "error", message: String(err) }))
      .setMimeType(ContentService.MimeType.JSON);
  } finally {
    lock.releaseLock();
  }
}
```

---

## 2. Deploy as a Web App

1. In the Apps Script editor, click **Deploy → New deployment**.
2. Click the gear icon next to **Select type** and choose **Web app**.
3. Set:
   - **Execute as**: `Me`
   - **Who has access**: `Anyone`
4. Click **Deploy** (authorize the script when prompted) and copy the **Web app URL** (`https://script.google.com/macros/s/.../exec`).

---

## 3. Configure the URL in the Game

Set the copied Web App URL in any of the following places:

- **In code (for playtest builds)**: Set `PlaytestFeedback.DefaultWebhookUrl` in [`PlaytestFeedback.cs`](PlaytestFeedback.cs).
- **In `Settings.json`**: Set `"feedbackWebhookUrl": "https://script.google.com/macros/s/.../exec"`.
- **Environment variable**: Set `BTB_FEEDBACK_WEBHOOK_URL=https://script.google.com/macros/s/.../exec`.
