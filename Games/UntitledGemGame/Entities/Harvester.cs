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
using JapeFramework;
using MonoGame.Extended.ECS;
using JapeFramework.DataStructures;

namespace UntitledGemGame.Entities
{
  public class Harvester : ICollisionActorJ
  {
    public string Name { get; set; }

    // Keep track of what this specific harvester has claimed
    public int _currentTargetBucket = -1;

    public Vector2? TargetScreenPosition { get; set; } = null;

    // Random-gem targets need an identity as well as a position so they can be
    // invalidated if another collector takes the gem while we are travelling.
    public int TargetGemGridIndex = -1;
    public int TargetGemEntityId = -1;

    public bool ReturningToHomebase => CarryingGemCount >= BaseStats.GetHarvesterCapacity(this);

    public float TimeAlive = 0;


    public Bag<int> ClaimedGems = new Bag<int>(50);


    public bool PositionMoved = false;

    public bool MarkedForDestroy = false;

    public int Id { get; set; }
    public Entity Entity;
    public BoundingCircle2D BoundingCircle => m_boundingCircle;
    private BoundingCircle2D m_boundingCircle;

    private const float BaseMaxFuel = 2500.0f; //5000.0f

    private float m_radius;

    public Sprite m_sprite;
    public AnimatedSprite m_engineSprite;
    public float Fuel = BaseMaxFuel;

    public bool ReachedHome = false;
    // public bool IsHomeBase = false;

    // public bool IsDrone = false;
    // public bool ForceInstantCollection = false;

    public bool ForceInstantCollection => Type == HarvesterType.Drone || Type == HarvesterType.HomeBase;

    public float MovedDistance = 0;

    public HarvesterStrategy CollectionStrategy = HarvesterStrategy.RandomScreenPosition;

    public uint CarryingGemCount = 0;
    public uint CarryingGemBaseValue = 0;


    public void SetCollisionPosition(Vector2 position, float radius = -1)
    {
      m_boundingCircle.Center = position;

      if (radius >= 0)
        m_boundingCircle.Radius = radius;
    }

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

      if (Type == HarvesterType.Drone && UpgradeManager.Instance.UGA.DroneRecharge)
      {
        TimeAlive -= 0.02f;
        if (TimeAlive < 0)
          TimeAlive = 0;
      }

      CarryingGemBaseValue += gem.BaseValue;
      ++CarryingGemCount;
    }

    public double refuelProgressPercent = 0.0;

    public enum HarvesterState
    {
      None,
      Collecting,
      OutOfFuel,
      RequestingFuel,
      Refueling,
    }

    public enum HarvesterType
    {
      None,
      HomeBase,
      Drone,
      Harvester,
      AdvancedHarvester,
      ExpertHarvester,
      // MasterHarvester,
      UltimateHarvester,
    }

    float burstTimer;
    public HarvesterState CurrentState = HarvesterState.Collecting;
    public HarvesterType Type = HarvesterType.None;

    public void Refuel()
    {
      CurrentState = HarvesterState.Refueling;

      refuelProgressPercent = 0;
      burstTimer = 0.0f;

      m_engineSprite.Color = new Color(Color.White * 0.0f, 1.0f);

      // if (m_refuelButton != null)
      // {
      //
      //   RenderGuiSystem.Instance.hudItems.Remove(m_refuelButton.Visual);
      //   m_refuelButton.RemoveFromRoot();
      //   m_refuelButton = null;
      // }
    }

    private Transform2 m_transform;

    public void Update(GameTime gameTime, Vector2 mouseWorldPos, bool isMouseClicked)
    {
      if (CurrentState == HarvesterState.Refueling)
      {
        const float BaseRefuelSpeed = 50.0f;
        if (burstTimer < 0.20f)
        {
          // Increase the timer rapidly to fade the flash out over ~0.15 seconds
          burstTimer += (float)gameTime.GetElapsedSeconds() * 1.0f;

          // Apply the timer directly to the color's alpha channel
          if (burstTimer < 0.20f)
          {
            m_sprite.Color = new Color(Color.White, burstTimer);
          }
          else
          {
            // Once done, snap perfectly back to Hover state
            // m_sprite.Color = new Color(Color.White, 0.3f);

            // m_sprite.Color = new Color(Color.White, 1.0f);
          }
        }
        else if (refuelProgressPercent < 100)
        {
          // refuelProgressPercent += gameTime.GetElapsedSeconds() * UpgradeManager.Instance.UG.HarvesterRefuelSpeed;
          // m_sprite.Alpha = (float)refuelProgressPercent;

          // refuelProgressPercent += gameTime.GetElapsedSeconds() * UpgradeManager.Instance.UG.HarvesterRefuelSpeed;
          //
          // // Map 0-100 to a float between 0.60f and 0.99f
          // float normalizedPercent = (float)refuelProgressPercent / 100f;
          // m_sprite.Alpha = 0.60f + (normalizedPercent * 0.39f);

          // refuelProgressPercent += gameTime.GetElapsedSeconds() * UpgradeManager.Instance.UG.HarvesterRefuelSpeed;
          //
          refuelProgressPercent += gameTime.GetElapsedSeconds() * BaseRefuelSpeed * BaseStats.GetHarvesterRefuelSpeedMultiplier(this);

          // Map 0-100 to a float between 0.60f and 0.99f
          float normalizedPercent = (float)refuelProgressPercent / 100f;
          float stateAlpha = 0.60f + (normalizedPercent * 0.39f);

          m_sprite.Color = new Color(Color.White * stateAlpha, stateAlpha);
          m_engineSprite.Color = new Color(Color.White * stateAlpha, 1.0f);


          // m_sprite.Color = new Color(Color.White * stateAlpha, 0.65f);
        }
        // If we are currently bursting (burst timer hasn't reached the 0.20f Hover threshold)

        if (refuelProgressPercent >= 100)
        {
          // SetFuelMax();
          //
          // CurrentState = HarvesterState.Collecting;
          // refuelProgressPercent = 0;
          // m_sprite.Alpha = 1.0f;
          SetFuelMax();

          CurrentState = HarvesterState.Collecting;
          refuelProgressPercent = 0;
          m_engineSprite.Color = Color.White;
          // 1.0f is Normal state
          m_sprite.Color = Color.White;
        }
      }
      else if (CurrentState == HarvesterState.RequestingFuel)
      {
        float width = TextureCache.HarvesterShip.Value.Width;
        float height = TextureCache.HarvesterShip.Value.Height;

        m_transform ??= Entity.Get<Transform2>();

        bool isMouseOver = mouseWorldPos.X >= m_transform.Position.X - width / 2 &&
                           mouseWorldPos.X <= m_transform.Position.X + width / 2 &&
                           mouseWorldPos.Y >= m_transform.Position.Y - height / 2 &&
                           mouseWorldPos.Y <= m_transform.Position.Y + height / 2;

        // if (isMouseOver)
        // {
        //   m_sprite.Alpha = 0.3f;
        // }
        // else
        //   m_sprite.Alpha = 0.5f;
        if (isMouseOver)
        {
          // 0.3f is the Hover zone
          m_sprite.Color = new Color(Color.White, 0.3f);
        }
        else
        {
          // 0.5f is the Pulsating zone
          m_sprite.Color = new Color(Color.White * 0.5f, 0.5f);
        }

        if (isMouseOver && isMouseClicked)
        {
          Refuel();
        }
        // var vec = m_camera.WorldToScreen(new Vector2(harvester.BoundingCircle.Center.X, harvester.BoundingCircle.Center.Y));
        // harvester.ReuqestRefuel(new Vector2(vec.X, vec.Y));
      }
    }


    // public void SetFuelMax()
    // {
    //   Fuel = UpgradeManager.Instance.UG.HarvesterMaxFuel * RandomHelper.Float(0.8f, 1.2f);
    // }

    public void SetFuelMax()
    {
      // ug.HarvesterMaxFuelMultiplier starts at 1.0f (100%)
      float maxCapacity = BaseMaxFuel * BaseStats.GetHarvesterMaxFuelMultiplier(this);
      Fuel = maxCapacity * RandomHelper.Float(0.8f, 1.2f);
    }

    public void IncreaseFuelPartial()
    {
      float currentMaxFuel = BaseMaxFuel * BaseStats.GetHarvesterMaxFuelMultiplier(this);
      Fuel += currentMaxFuel * RandomHelper.Float(0.1f, 0.2f);
      Fuel = MathF.Min(Fuel, currentMaxFuel);
    }

    private void SetRequestRefuelButtonPosition()
    {
      // var position = new Vector2(Bounds.BoundingRectangle.Left, Bounds.BoundingRectangle.Top);
      // Viewport viewport = UntitledGemGame.GameMain.BoxingViewportAdapter.Viewport;
      // var vec = Vector2.Transform(position + new Vector2(viewport.X, viewport.Y), UntitledGemGameGameScreen.m_camera.GetViewMatrix());

      // var box = UntitledGemGame.GameMain.BoxingViewportAdapter;
      // var vec = UntitledGemGameGameScreen.m_camera.WorldToScreen(
      //     new Vector2(Bounds.BoundingRectangle.Right, Bounds.BoundingRectangle.Top));


      float posX = BoundingCircle.Center.X;
      float posY = BoundingCircle.Center.Y;

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

      // m_refuelButton.X = x;
      // m_refuelButton.Y = y;
    }

    public void ReuqestRefuel(Vector2 buttonPosition)
    {
      if (!UpgradeManager.Instance.UGM.AutoRefuel)
      {
        AudioManager.Instance.PlaySound(AudioManager.Instance.BlipSoundEffect);
      }

      // m_sprite.Alpha = 0.5f;
      m_sprite.Color = new Color(Color.White, 0.5f);

      var w = 100;
      var h = 10;

      //buttonPosition is in screenspace
      //Convert to canvas space

      var screenX = BaseGame.BoxingViewportAdapterGui.Viewport.X;
      var screenY = BaseGame.BoxingViewportAdapterGui.Viewport.Y;
      var screenWidth = BaseGame.BoxingViewportAdapterGui.ViewportWidth;
      var screenheight = BaseGame.BoxingViewportAdapterGui.ViewportHeight;

      var canvasWidth = Gum.GumService.Default.Root.Width; //3840
      var canvasHeight = Gum.GumService.Default.Root.Height; //2160
                                                             //


      var canvasX = (buttonPosition.X - screenX) / screenWidth * canvasWidth;
      var canvasY = (buttonPosition.Y - screenY) / screenheight * canvasHeight;


      // var rect = new RectangleF(canvasX, canvasY, w, h);
      // bool foundIntersect;
      // do
      // {
      //   foundIntersect = false;
      //   foreach (var c in Gum.GumService.Default.Root.Children.ToArray())
      //   {
      //     var childRect = new RectangleF(c.GetAbsoluteX(), c.GetAbsoluteY(), c.Width, c.Height);
      //
      //     if (rect.Intersects(childRect))
      //     {
      //       canvasY += 10;
      //
      //       rect = new RectangleF(canvasX, canvasY, w, h);
      //       foundIntersect = true;
      //       break;
      //     }
      //   }
      // } while (foundIntersect);

      // m_refuelButton = new Button
      // {
      //   Text = "Refuel",
      //   X = canvasX - (w / 2.0f),
      //   Y = canvasY - 90,
      //   Width = w,
      //   Height = h,
      // };
      //
      // var buttonVisual = m_refuelButton.Visual;
      // var background = buttonVisual.Children.First() as NineSliceRuntime;
      //
      // background.BorderScale = 1.0f;
      // background.Color = new Color(255, 255, 255, 255);
      // background.Texture = TextureCache.RefuelButtonBackground;
      // background.TextureAddress = TextureAddress.EntireTexture;
      //
      // foreach (var a in buttonVisual.Categories)
      // {
      //   foreach (var b in a.Value.States)
      //   {
      //     switch (b.Name)
      //     {
      //       case "Focused":
      //         b.Apply = () =>
      //         {
      //           background.Color = new Color(255, 255, 255, 255);
      //         };
      //         break;
      //       case "Highlighted":
      //         b.Apply = () =>
      //         {
      //           background.Color = new Color(255, 255, 255, 255);
      //           background.Texture = TextureCache.RefuelButtonBackgroundHighlight;
      //         };
      //         break;
      //
      //       case "HighlightedFocused":
      //         b.Apply = () =>
      //         {
      //           background.Color = new Color(255, 255, 255, 255);
      //           background.Texture = TextureCache.RefuelButtonBackgroundHighlight;
      //         };
      //         break;
      //       case "Pushed":
      //         b.Apply = () =>
      //         {
      //           background.Color = new Color(255, 255, 255, 255);
      //         };
      //         break;
      //       case "Enabled":
      //         b.Apply = () =>
      //         {
      //           background.Color = new Color(255, 255, 255, 255);
      //           background.Texture = TextureCache.RefuelButtonBackground;
      //         };
      //         break;
      //     }
      //   }
      // }
      //
      // m_refuelButton.Visual.AddToManagers(Gum.GumService.Default.SystemManagers, Gum.GumService.Default.Renderer.MainLayer);
      // RenderGuiSystem.Instance.hudItems.Add(m_refuelButton.Visual);
      //
      // m_refuelButton.Click += (_, _) =>
      // {
      //   //m_refuelButton.RemoveFromRoot();
      //   Refuel();
      // };

      CurrentState = HarvesterState.RequestingFuel;
    }

    public void UpdateRefuelButtonPosition(Vector2 buttonPosition)
    {
      var w = 100;
      var h = 10;

      //buttonPosition is in screenspace
      //Convert to canvas space

      var screenX = BaseGame.BoxingViewportAdapterGui.Viewport.X;
      var screenY = BaseGame.BoxingViewportAdapterGui.Viewport.Y;
      var screenWidth = BaseGame.BoxingViewportAdapterGui.ViewportWidth;
      var screenheight = BaseGame.BoxingViewportAdapterGui.ViewportHeight;

      var canvasWidth = Gum.GumService.Default.Root.Width; //3840
      var canvasHeight = Gum.GumService.Default.Root.Height; //2160
                                                             //

      var canvasX = (buttonPosition.X - screenX) / screenWidth * canvasWidth;
      var canvasY = (buttonPosition.Y - screenY) / screenheight * canvasHeight;


      // var rect = new RectangleF(canvasX, canvasY, w, h);
      // bool foundIntersect;
      // do
      // {
      //   foundIntersect = false;
      //   foreach (var c in Gum.GumService.Default.Root.Children.ToArray())
      //   {
      //     var childRect = new RectangleF(c.GetAbsoluteX(), c.GetAbsoluteY(), c.Width, c.Height);
      //
      //     if (rect.Intersects(childRect))
      //     {
      //       canvasY += 10;
      //
      //       rect = new RectangleF(canvasX, canvasY, w, h);
      //       foundIntersect = true;
      //       break;
      //     }
      //   }
      // } while (foundIntersect);

      // m_refuelButton = new Button
      // {
      //   Text = "Refuel",
      //   X = canvasX - (w / 2.0f),
      //   Y = canvasY - 90,
      //   Width = w,
      //   Height = h,
      // };

      // m_refuelButton.X = canvasX - (w / 2.0f);
      // m_refuelButton.Y = canvasY - 90;
    }
  }
}
