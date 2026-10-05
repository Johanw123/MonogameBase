using AsyncContent;
using JapeFramework;
using JapeFramework.Aseprite;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.Graphics;
using UntitledGemGame;
using UntitledGemGame.Screens;

public partial class RenderGuiSystem
{
  private static readonly Rectangle CoreFracturePanel = new(40, 172, 700, 290);
  private (Texture2D Texture, Texture2DRegion Region)? coreShardIcon;

  // The shard balance beside the tree that spends them. When the next fracture comes
  // stays a surprise, so no damage figures.
  private void DrawCoreFracturePanel(SpriteBatch batch)
  {
    var screen = UntitledGemGameGameScreen.Instance;
    if (screen == null) return;
    var state = screen.State;
    var panel = CoreFracturePanel;
    int left = panel.X + 28, right = panel.Right - 28;

    coreShardIcon ??= AsepriteHelper.LoadTextureFromAnimationFrame(CoreShards.IconPath, 0, CoreShards.IconFrames);
    var icon = coreShardIcon.Value;
    batch.Begin();
    OrbitSkin.Panel(batch, panel);
    batch.Draw(AssetManager.DefaultTexture,
      new Rectangle(panel.X, panel.Y, HudLayout.ButtonBorderThickness, panel.Height), CoreShards.Color * 0.8f);
    batch.Draw(icon.Texture, new Rectangle(left, panel.Y + 66, icon.Region.Width * 2, icon.Region.Height * 2),
      icon.Region.Bounds, Color.White);
    batch.End();

    ulong shards = state.CurrentCoreShardCount;
    DrawPanelText("CORE FRACTURES", left, panel.Y + 20, 26f, OrbitSkin.MutedTextColor);
    DrawPanelTextRight($"{state.CoreFractures} this run", right, panel.Y + 20, 26f, OrbitSkin.MutedTextColor);
    DrawPanelText($"{shards} {(shards == 1 ? "Core Shard" : CoreShards.Name)}", left + 60, panel.Y + 70, 44f,
      CoreShards.Color);
    DrawPanelText("Spend on gold-ringed upgrades. Lost on prestige.", left, panel.Y + 136, 22f,
      OrbitSkin.MutedTextColor);

    DrawPanelText("Hit the planet hard enough and its core fractures,", left, panel.Y + 196, 22f,
      OrbitSkin.MutedTextColor);
    DrawPanelText("releasing a Core Shard. Each one takes harder hits.", left, panel.Y + 226, 22f,
      OrbitSkin.MutedTextColor);
  }

  private static void DrawPanelText(string text, float x, float y, float size, Color color)
    => FontManager.RenderFieldFont(() => ContentDirectory.Fonts.Roboto_Regular_ttf,
      text, new Vector2(x, y), color, Color.Black, size);

  private void DrawPanelTextRight(string text, float right, float y, float size, Color color)
    => DrawPanelText(text, right - Measure2(text, Vector2.Zero, size).X, y, size, color);
}
