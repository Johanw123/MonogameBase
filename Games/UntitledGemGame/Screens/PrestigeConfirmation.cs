using System;
using Gum.Forms.Controls;
using Gum.GueDeriving;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using MonoGameGum;
using RenderingLibrary.Graphics;

namespace UntitledGemGame.Screens;

public partial class UntitledGemGameGameScreen
{
  private GraphicalUiElement prestigeDialog;
  private ContainerRuntime prestigeCancelButton;
  private ContainerRuntime prestigeConfirmButton;
  public bool IsPrestigeConfirmationOpen => prestigeDialog != null;

  public void ShowPrestigeConfirmation(Action confirmPrestige)
  {
    if (IsPrestigeConfirmationOpen || m_prestiging || m_postPrestige)
      return;

    var overlay = new ContainerRuntime
    {
      WidthUnits = Gum.DataTypes.DimensionUnitType.Absolute,
      HeightUnits = Gum.DataTypes.DimensionUnitType.Absolute,
      Width = GumService.Default.CanvasWidth,
      Height = GumService.Default.CanvasHeight
    };
    prestigeDialog = overlay;
    overlay.Children.Add(new RectangleRuntime
    {
      WidthUnits = Gum.DataTypes.DimensionUnitType.RelativeToParent,
      HeightUnits = Gum.DataTypes.DimensionUnitType.RelativeToParent,
      Width = 0, Height = 0, IsFilled = true,
      FillColor = new Color(0, 0, 0, 180), StrokeWidth = 0
    });

    var panel = new ContainerRuntime { Width = 1600, Height = 760 };
    overlay.Children.Add(panel);
    panel.Anchor(Anchor.Center);
    panel.Children.Add(new RectangleRuntime
    {
      Width = 1600, Height = 760, IsFilled = true,
      FillColor = HudLayout.PanelColor,
      StrokeColor = HudLayout.ButtonBorderColor, StrokeWidth = 4, CornerRadius = 0
    });

    var cancel = prestigeCancelButton = CreatePrestigeDialogButton(100);
    var confirm = prestigeConfirmButton = CreatePrestigeDialogButton(840);
    var fontTemplate = GameMain.GumProject.GetComponentSave("Controls/ButtonMainMenu").ToGraphicalUiElement();
    var buttonText = (Text)fontTemplate.GetChildByNameRecursively("TextInstance").RenderableComponent;
    ulong reward = PrestigeProgression.GetReward(GetPrestigeEarnings());
    panel.Children.Add(new TextRuntime
    {
      Text = "Ready to prestige?\n\n"
        + "Your upgrade tree and red gems will reset.\n"
        + $"You will earn {reward:N0} prestige points to spend on\n"
        + "powerful permanent upgrades in the Prestige Upgrades tree.\n"
        + "These upgrades stay with you through future prestiges.",
      WidthUnits = Gum.DataTypes.DimensionUnitType.Absolute,
      HeightUnits = Gum.DataTypes.DimensionUnitType.Absolute,
      X = 70, Y = 40, Width = 1460, Height = 510,
      BitmapFont = buttonText.BitmapFont, FontScale = 0.6f,
      HorizontalAlignment = HorizontalAlignment.Center,
      VerticalAlignment = VerticalAlignment.Center
    });
    panel.Children.Add(cancel);
    panel.Children.Add(confirm);
    cancel.Click += (_, _) => ClosePrestigeConfirmation();
    confirm.Click += (_, _) =>
    {
      if (!IsPrestigeConfirmationOpen) return;
      ClosePrestigeConfirmation();
      confirmPrestige();
    };
    GumService.Default.ModalRoot.Children.Add(overlay);
    // ModalRoot is centered on the canvas. Cancel its offset so this full-screen
    // overlay starts at the screen origin and its centered panel stays on-screen.
    overlay.Anchor(Anchor.TopLeft);
    overlay.X = -GumService.Default.ModalRoot.AbsoluteLeft;
    overlay.Y = -GumService.Default.ModalRoot.AbsoluteTop;
    overlay.RemoveFromManagers();
    overlay.AddToManagers(GumService.Default.SystemManagers,
      _renderGuiSystem.PrestigeDialogLayer);
    overlay.UpdateLayout();
  }

  private static ContainerRuntime CreatePrestigeDialogButton(float x)
    => new ContainerRuntime
    {
      WidthUnits = Gum.DataTypes.DimensionUnitType.Absolute,
      HeightUnits = Gum.DataTypes.DimensionUnitType.Absolute,
      Width = 660, Height = 100, X = x, Y = 600,
      HasEvents = true
    };

  public void DrawPrestigeDialogButtons(Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch)
  {
    if (!IsPrestigeConfirmationOpen) return;
    DrawButton(prestigeCancelButton, "Cancel", HudLayout.UpgradeAccent);
    DrawButton(prestigeConfirmButton, "Prestige", HudLayout.AbilityAccent);

    void DrawButton(ContainerRuntime button, string label, Color accent)
    {
      var bounds = new Rectangle((int)button.AbsoluteLeft, (int)button.AbsoluteTop,
        (int)button.Width, (int)button.Height);
      bool hovered = bounds.Contains((int)GumService.Default.Cursor.X, (int)GumService.Default.Cursor.Y);
      _renderGuiSystem.DrawHudButton(spriteBatch, bounds, label, accent, false, hovered, 0);
    }
  }

  private void ClosePrestigeConfirmation()
  {
    if (prestigeDialog == null) return;
    GumService.Default.ModalRoot.Children.Remove(prestigeDialog);
    prestigeDialog = null;
  }
}
