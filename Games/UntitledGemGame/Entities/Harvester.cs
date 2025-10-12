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
using MonoGame.Extended.Tweening;
using RenderingLibrary;

namespace UntitledGemGame.Entities
{
  public class Harvester : ICollisionActor
  {
    public string Name { get; set; }
    public int ID { get; set; }

    public Vector2? TargetScreenPosition { get; set; } = null;

    public void OnCollision(CollisionEventArgs collisionInfo)
    {

    }

    public float Fuel = Upgrades.HarvesterMaximumFuel;
    public bool RequestingRefuel { get; set; }

    public bool ReachedHome = false;

    public IShapeF Bounds { get; set; }

    private readonly Tweener _tweener = new();

    //public int CurrentCapacity = 2000;

    //public Bag<int> CarryingGems { get; } = new(5000);

    public int CarryingGemCount = 0;

    public void PickedUpGem(Gem gem)
    {
      //Check gem type etc and save for when delivering
      ++CarryingGemCount;
    }

    public void Refuel()
    {
      RequestingRefuel = false;

      if (m_refuelButton != null)
      {
        var prog = new Slider
        {
          X = m_refuelButton.X,
          Y = m_refuelButton.Y,
          Width = m_refuelButton.Width,
          Height = m_refuelButton.Height,
          Value = 0.0,
          Maximum = 100.0
        };

        m_tween = _tweener.TweenTo(prog, slider => slider.Value, 100.0, 0.5f)
          .Easing(EasingFunctions.ExponentialIn);

        m_tween.OnEnd((_) =>
        {
          prog.RemoveFromRoot();
          IsOutOfFuel = false;
          Fuel = Upgrades.HarvesterMaximumFuel;
        });

        m_refuelButton?.RemoveFromRoot();
        prog.AddToRoot();
      }
    }

    public void Update(GameTime gameTime)
    {
      if (m_tween is { IsComplete: false })
        //if (m_transform.Scale.X is > 0.0f and < 1.0f)
        _tweener.Update(gameTime.GetElapsedSeconds());
    }

    private static Texture2D m_texture = AssetManager.Load<Texture2D>(ContentDirectory.Textures.ButtonBackground_png);
    private static Texture2D m_texture2 = AssetManager.Load<Texture2D>(ContentDirectory.Textures.ButtonBackgroundHighlight_png);

    private Button m_refuelButton;

    public bool IsOutOfFuel;
    private Tween m_tween;

    public void ReuqestRefuel(Vector2 buttonPosition)
    {
      if (RequestingRefuel) return;
      if (!IsOutOfFuel) return;

      RequestingRefuel = true;
      
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
        
      var buttonVisual = (m_refuelButton.Visual as ButtonVisual);
      buttonVisual.Background.Color = new Color(255, 255, 255, 255);
      buttonVisual.Background.BorderScale = 1.0f;

      buttonVisual.Background.Texture = m_texture;
      buttonVisual.Background.TextureAddress = TextureAddress.EntireTexture;


      buttonVisual.States.Focused.Apply = () =>
      {
        buttonVisual.Background.Color = new Color(255, 255, 255, 255);
      };

      buttonVisual.States.Highlighted.Apply = () =>
      {
        buttonVisual.Background.Color = new Color(255, 255, 255, 255);
        buttonVisual.Background.Texture = m_texture2;
      };

      buttonVisual.States.HighlightedFocused.Apply = () =>
      {
        buttonVisual.Background.Color = new Color(255, 255, 255, 255);
        buttonVisual.Background.Texture = m_texture2;
      };

      buttonVisual.States.Pushed.Apply = () =>
      {
        buttonVisual.Background.Color = new Color(255, 255, 255, 255);
      };

      buttonVisual.States.Enabled.Apply = () =>
      {
        buttonVisual.Background.Color = new Color(255, 255, 255, 255);
        buttonVisual.Background.Texture = m_texture;
      };

      m_refuelButton.AddToRoot();

      m_refuelButton.Click += (_, _) =>
      {
        Refuel();
      };

    }
  }
}
