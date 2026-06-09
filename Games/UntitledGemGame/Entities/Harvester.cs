using Gum.DataTypes.Variables;
using Gum.Forms.Controls;
using Gum.Forms.DefaultVisuals;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MonoGame.Extended.Collections;
using MonoGame.Extended.Collisions;
using MonoGameGum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AsyncContent;
using Gum.Managers;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.Graphics;
using MonoGame.Extended.Tweening;
using RenderingLibrary;
using JapeFramework.Helpers;
using MonoGame.Extended.Screens;
using UntitledGemGame.Screens;
using Gum.GueDeriving;

namespace UntitledGemGame.Entities
{
  public class Harvester : ICollisionActor
  {
    public string Name { get; set; }

    public Vector2? TargetScreenPosition { get; set; } = null;

    public bool ReturningToHomebase => CarryingGemCount >= UpgradeManager.UG.HarvesterCapacity;


    public int Id { get; set; }
    public CollisionShape2D Shape { get; set; }
    private float m_radius;

    public Sprite m_sprite;
    public float Fuel = UpgradeManager.UG.HarvesterMaxFuel;

    public bool ReachedHome = false;
    // public bool IsHomeBase = false;
    // public bool IsDrone = false;
    public bool ForceInstantCollection = false;


    public uint CarryingGemCount = 0;
    public uint CarryingGemBaseValue = 0;

    public void PickedUpGem(Gem gem)
    {
      // switch (gem.GemType)
      // {
      //   case GemTypes.Red:
      //     CarryingGemBaseValue += 1;
      //     break;
      //
      //   case GemTypes.LightGreen:
      //     CarryingGemBaseValue += 2;
      //     break;
      // }

      CarryingGemBaseValue += gem.BaseValue;
      ++CarryingGemCount;
    }

    private double refuelProgressPercent = 0.0;

    public enum HarvesterState
    {
      None,
      Collecting,
      OutOfFuel,
      RequestingFuel,
      Refueling,
    }

    public HarvesterState CurrentState = HarvesterState.Collecting;

    public void Refuel()
    {
      CurrentState = HarvesterState.Refueling;

      if (m_refuelButton != null)
      {
        refuelProgressPercent = 0;
        m_sprite.Alpha = 0.0f;

        RenderGuiSystem.Instance.hudItems.Remove(m_refuelButton.Visual);
        m_refuelButton.RemoveFromRoot();
        m_refuelButton = null;
      }
    }

    public void Update(GameTime gameTime)
    {
      if (CurrentState == HarvesterState.Refueling)
      {
        if (refuelProgressPercent < 100)
        {
          refuelProgressPercent += gameTime.GetElapsedSeconds() * UpgradeManager.UG.HarvesterRefuelSpeed;
          m_sprite.Alpha = (float)refuelProgressPercent / 100.0f;
        }

        if (refuelProgressPercent >= 100)
        {
          SetFuelMax();

          CurrentState = HarvesterState.Collecting;
          refuelProgressPercent = 0;
          m_sprite.Alpha = 1.0f;
        }
      }
      else if (CurrentState == HarvesterState.RequestingFuel)
      {

      }
    }

    public void SetFuelMax()
    {
      Fuel = UpgradeManager.UG.HarvesterMaxFuel * RandomHelper.Float(0.8f, 1.2f);
    }

    private Button m_refuelButton;

    private void SetRequestRefuelButtonPosition()
    {
      // var position = new Vector2(Bounds.BoundingRectangle.Left, Bounds.BoundingRectangle.Top);
      // Viewport viewport = UntitledGemGame.GameMain.BoxingViewportAdapter.Viewport;
      // var vec = Vector2.Transform(position + new Vector2(viewport.X, viewport.Y), UntitledGemGameGameScreen.m_camera.GetViewMatrix());

      // var box = UntitledGemGame.GameMain.BoxingViewportAdapter;
      // var vec = UntitledGemGameGameScreen.m_camera.WorldToScreen(
      //     new Vector2(Bounds.BoundingRectangle.Right, Bounds.BoundingRectangle.Top));


      float posX = Shape.BoundingBox.Center.X;
      float posY = Shape.BoundingBox.Center.Y;

      var camera = SystemManagers.Default.Renderer.Camera;
      // camera.ScreenToWorld((Bounds.BoundingRectangle.Right, Bounds.BoundingRectangle.Top, out float worldX, out float worldY);
      camera.WorldToScreen(posX, posY, out var x, out var y);

      var w = 100;
      var h = 10;
      // var x = vec.X - (w / 2.0f);
      // var y = vec.Y - (h / 2.0f) - 90;
      // var x = vec.X;
      // var y = vec.Y;

      var rect = new RectangleF(x, y, w, h);

      // bool foundIntersect;
      // do
      // {
      //   foundIntersect = false;
      //   foreach (var c in GumService.Default.Root.Children.ToArray())
      //   {
      //     var childRect = new RectangleF(c.GetAbsoluteX(), c.GetAbsoluteY(), c.Width, c.Height);
      //
      //     if (rect.Intersects(childRect))
      //     {
      //       y += 10;
      //
      //       rect = new RectangleF(x, y, w, h);
      //       foundIntersect = true;
      //       break;
      //     }
      //   }
      // } while (foundIntersect);

      // x = Math.Clamp(x, 0, GumService.Default.Root.Width - w);
      // y = Math.Clamp(y, 0, GumService.Default.Root.Height - h);

      m_refuelButton.X = x;
      m_refuelButton.Y = y;
    }

    public void ReuqestRefuel(Vector2 buttonPosition)
    {
      if (!UpgradeManager.UG.AutoRefuel)
      {
        AudioManager.Instance.PlaySound(AudioManager.Instance.BlipSoundEffect);
      }

      m_sprite.Alpha = 0.0f;

      var w = 100;
      var h = 10;


      //buttonPosition is in screenspace
      //Convert to canvas space

      var screenX = GameMain.BoxingViewportAdapterGui.Viewport.X;
      var screenY = GameMain.BoxingViewportAdapterGui.Viewport.Y;
      var screenWidth = GameMain.BoxingViewportAdapterGui.ViewportWidth;
      var screenheight = GameMain.BoxingViewportAdapterGui.ViewportHeight;

      var canvasWidth = GumService.Default.Root.Width; //3840
      var canvasHeight = GumService.Default.Root.Height; //2160
                                                         //


      var canvasX = (buttonPosition.X - screenX) / screenWidth * canvasWidth;
      var canvasY = (buttonPosition.Y - screenY) / screenheight * canvasHeight;


      var rect = new RectangleF(canvasX, canvasY, w, h);


      bool foundIntersect;
      do
      {
        foundIntersect = false;
        foreach (var c in GumService.Default.Root.Children.ToArray())
        {
          var childRect = new RectangleF(c.GetAbsoluteX(), c.GetAbsoluteY(), c.Width, c.Height);

          if (rect.Intersects(childRect))
          {
            canvasY += 10;

            rect = new RectangleF(canvasX, canvasY, w, h);
            foundIntersect = true;
            break;
          }
        }
      } while (foundIntersect);

      m_refuelButton = new Button
      {
        Text = "Refuel",
        X = canvasX - (w / 2.0f),
        Y = canvasY - 90,
        Width = w,
        Height = h,
      };

      // SetRequestRefuelButtonPosition();

      // Console.WriteLine($"Refuel button at: {m_refuelButton.X}, {m_refuelButton.Y}");

      var buttonVisual = m_refuelButton.Visual;
      var background = buttonVisual.Children.First() as NineSliceRuntime;

      background.BorderScale = 1.0f;
      background.Color = new Color(255, 255, 255, 255);
      background.Texture = TextureCache.RefuelButtonBackground;
      background.TextureAddress = TextureAddress.EntireTexture;

      foreach (var a in buttonVisual.Categories)
      {
        foreach (var b in a.Value.States)
        {
          switch (b.Name)
          {
            case "Focused":
              b.Apply = () =>
              {
                background.Color = new Color(255, 255, 255, 255);
              };
              break;
            case "Highlighted":
              b.Apply = () =>
              {
                background.Color = new Color(255, 255, 255, 255);
                background.Texture = TextureCache.RefuelButtonBackgroundHighlight;
              };
              break;

            case "HighlightedFocused":
              b.Apply = () =>
              {
                background.Color = new Color(255, 255, 255, 255);
                background.Texture = TextureCache.RefuelButtonBackgroundHighlight;
              };
              break;
            case "Pushed":
              b.Apply = () =>
              {
                background.Color = new Color(255, 255, 255, 255);
              };
              break;
            case "Enabled":
              b.Apply = () =>
              {
                background.Color = new Color(255, 255, 255, 255);
                background.Texture = TextureCache.RefuelButtonBackground;
              };
              break;
          }
        }
      }


      // buttonVisual.Background.Color = new Color(255, 255, 255, 255);
      // buttonVisual.Background.BorderScale = 1.0f;
      //
      // buttonVisual.Background.Texture = TextureCache.RefuelButtonBackground;
      // buttonVisual.Background.TextureAddress = TextureAddress.EntireTexture;
      //
      // buttonVisual.States.Focused.Apply = () =>
      // {
      //   buttonVisual.Background.Color = new Color(255, 255, 255, 255);
      // };
      //
      // buttonVisual.States.Highlighted.Apply = () =>
      // {
      //   buttonVisual.Background.Color = new Color(255, 255, 255, 255);
      //   buttonVisual.Background.Texture = TextureCache.RefuelButtonBackgroundHighlight;
      // };
      //
      // buttonVisual.States.HighlightedFocused.Apply = () =>
      // {
      //   buttonVisual.Background.Color = new Color(255, 255, 255, 255);
      //   buttonVisual.Background.Texture = TextureCache.RefuelButtonBackgroundHighlight;
      // };
      //
      // buttonVisual.States.Pushed.Apply = () =>
      // {
      //   buttonVisual.Background.Color = new Color(255, 255, 255, 255);
      // };
      //
      // buttonVisual.States.Enabled.Apply = () =>
      // {
      //   buttonVisual.Background.Color = new Color(255, 255, 255, 255);
      //   buttonVisual.Background.Texture = TextureCache.RefuelButtonBackground;
      // };

      m_refuelButton.Visual.AddToManagers(GumService.Default.SystemManagers, GumService.Default.Renderer.MainLayer);
      RenderGuiSystem.Instance.hudItems.Add(m_refuelButton.Visual);

      m_refuelButton.Click += (_, _) =>
      {
        //m_refuelButton.RemoveFromRoot();
        Refuel();
      };

      CurrentState = HarvesterState.RequestingFuel;
    }
  }
}
