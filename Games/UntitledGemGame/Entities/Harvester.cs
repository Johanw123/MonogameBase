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

namespace UntitledGemGame.Entities
{
  public class Harvester : ICollisionActor
  {
    public string Name { get; set; }
    public int ID { get; set; }

    public Vector2? TargetScreenPosition { get; set; } = null;

    public bool ReturningToHomebase => CarryingGemCount >= UpgradeManager.UG.HarvesterCapacity;

    public void OnCollision(CollisionEventArgs collisionInfo)
    {

    }

    public Sprite m_sprite;
    public float Fuel = UpgradeManager.UG.HarvesterMaxFuel;

    public bool ReachedHome = false;

    public IShapeF Bounds { get; set; }

    public int CarryingGemCount = 0;

    public void PickedUpGem(Gem gem)
    {
      //Check gem type etc and save for when delivering
      ++CarryingGemCount;
    }

    private double refuelProgressPercent = 0.0;

    public enum HarvesterState
    {
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

        RenderGuiSystem.itemsToUpdate.Remove(m_refuelButton.Visual);
        m_refuelButton.RemoveFromRoot();
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
          Fuel = UpgradeManager.UG.HarvesterMaxFuel;

          CurrentState = HarvesterState.Collecting;
          refuelProgressPercent = 0;
          m_sprite.Alpha = 1.0f;
        }
      }
    }

    private Button m_refuelButton;

    public void ReuqestRefuel(Vector2 buttonPosition)
    {
      CurrentState = HarvesterState.RequestingFuel;
      m_sprite.Alpha = 0.0f;

      var w = 100;
      var h = 10;
      var x = buttonPosition.X - (w / 2.0f);
      var y = buttonPosition.Y - (h / 2.0f) - 90;

      var rect = new RectangleF(x, y, w, h);

      bool foundIntersect;
      do
      {
        foundIntersect = false;
        foreach (var c in GumService.Default.Root.Children.ToArray())
        {
          var childRect = new RectangleF(c.GetAbsoluteX(), c.GetAbsoluteY(), c.Width, c.Height);

          if (rect.Intersects(childRect))
          {
            y += 10;

            rect = new RectangleF(x, y, w, h);
            foundIntersect = true;
            break;
          }
        }
      } while (foundIntersect);

      m_refuelButton = new Button
      {
        Text = "Refuel",
        X = x,
        Y = y,
        Width = w,
        Height = h,
      };

      var buttonVisual = m_refuelButton.Visual as ButtonVisual;
      buttonVisual.Background.Color = new Color(255, 255, 255, 255);
      buttonVisual.Background.BorderScale = 1.0f;

      buttonVisual.Background.Texture = TextureCache.RefuelButtonBackground;
      buttonVisual.Background.TextureAddress = TextureAddress.EntireTexture;

      buttonVisual.States.Focused.Apply = () =>
      {
        buttonVisual.Background.Color = new Color(255, 255, 255, 255);
      };

      buttonVisual.States.Highlighted.Apply = () =>
      {
        buttonVisual.Background.Color = new Color(255, 255, 255, 255);
        buttonVisual.Background.Texture = TextureCache.RefuelButtonBackgroundHighlight;
      };

      buttonVisual.States.HighlightedFocused.Apply = () =>
      {
        buttonVisual.Background.Color = new Color(255, 255, 255, 255);
        buttonVisual.Background.Texture = TextureCache.RefuelButtonBackgroundHighlight;
      };

      buttonVisual.States.Pushed.Apply = () =>
      {
        buttonVisual.Background.Color = new Color(255, 255, 255, 255);
      };

      buttonVisual.States.Enabled.Apply = () =>
      {
        buttonVisual.Background.Color = new Color(255, 255, 255, 255);
        buttonVisual.Background.Texture = TextureCache.RefuelButtonBackground;
      };

      m_refuelButton.Visual.AddToManagers(GumService.Default.SystemManagers, GumService.Default.Renderer.MainLayer);
      RenderGuiSystem.itemsToUpdate.Add(m_refuelButton.Visual);

      m_refuelButton.Click += (_, _) =>
      {
        //m_refuelButton.RemoveFromRoot();
        Refuel();
      };

    }
  }
}
