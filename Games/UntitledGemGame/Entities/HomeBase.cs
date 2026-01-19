using MonoGame.Extended;
using System;
using System.Collections.Generic;
using System.Linq;
using MonoGame.Extended.Collisions;
using UntitledGemGame.Screens;
using MonoGame.Extended.ECS;
using JapeFramework.Helpers;
using Microsoft.Xna.Framework;
using Gum.Forms.Controls;
using MonoGameGum;
using Gum.Forms.DefaultVisuals;
using MonoGameGum.GueDeriving;
using AsyncContent;
using Microsoft.Xna.Framework.Graphics;
using RenderingLibrary.Graphics;
using MonoGameGum.ExtensionMethods;
using UntitledGemGame.Systems;

namespace UntitledGemGame.Entities
{
  public abstract class IHomeBaseAbility
  {
    public int CooldownTime = 5000;
    public int MaxCooldownTime = 5000;
    public int DurationTime = 0;
    public virtual int DurationTimeMax => 1000;

    public virtual int Level => 0;

    public bool IsActive => DurationTime > 0;
    public abstract void Activate();
    public abstract void Deactivate();

    public virtual void Update(GameTime gameTime)
    {
    }

    public virtual string IconPath => "Textures/GUI/icon.jpg";
  }

  public class EmptyAbility : IHomeBaseAbility
  {
    public override string IconPath => "Textures/GUI/icon.png";

    public EmptyAbility()
    {
      CooldownTime = 0;
      MaxCooldownTime = 0;
      DurationTime = 0;
    }

    public override void Activate()
    {
    }

    public override void Deactivate()
    {
    }
  }

  public class SpeedboostAbility : IHomeBaseAbility
  {
    public override string IconPath => "Textures/scifi_icons/icon_accuracy/18_accuracy.png";
    public override int Level => UpgradeManager.UG.Speedboost;

    public override void Activate()
    {
      HomeBase.BonusMoveSpeed = 2.0f;
    }

    public override void Deactivate()
    {
      HomeBase.BonusMoveSpeed = 1.0f;
    }
  }

  public class MagnetAbility : IHomeBaseAbility
  {
    public override string IconPath => "Textures/scifi_icons/icon_power/11_power.png";
    public override int Level => UpgradeManager.UG.HomebaseMagnetizer;

    public override void Activate()
    {
      HomeBase.BonusMagnetPower = Level switch
      {
        1 => 5.0f,
        2 => 35.0f,
        3 => 100.0f,
        _ => 0.0f,
      };
    }

    public override void Deactivate()
    {
      HomeBase.BonusMagnetPower = 0.0f;
    }
  }

  public class ChainLightningAbility : IHomeBaseAbility
  {
    public override string IconPath => "Textures/scifi_icons/icon_power/12_power.png";
    public override int Level => UpgradeManager.UG.ChainMagnetizer;

    public override int DurationTimeMax => 150;

    List<int> gems = new List<int>();
    public static Dictionary<int, LineShape> TargetLines = new();

    public override void Update(GameTime gameTime)
    {
      if (gems == null)
        return;

      foreach (var id in gems.ToArray())
      {
        var gem = HarvesterCollectionSystem.Instance.GetEntityP(id);
        if (gem != null)
        {
          var gemPos = gem?.Get<Transform2>()?.Position;
          var gemComp = gem?.Get<Gem>();

          if (gemPos == null || gemComp == null)
          {
            continue;
          }

          if (gemComp.PickedUp)
          {
            gems.Remove(id);
            continue;
          }

          if (Vector2.Distance(gemPos.Value, UntitledGemGameGameScreen.HomeBasePos) < 10.0f)
          {
            gems.Remove(id);
            TargetLines.Remove(id);
          }
          else
          {
            var dir = UntitledGemGameGameScreen.HomeBasePos - gemPos.Value;
            dir.Normalize();
            var distance = Vector2.Distance(gemPos.Value, UntitledGemGameGameScreen.HomeBasePos);
            gem.Get<Transform2>().Position += dir * 6.0f * (float)gameTime.GetElapsedSeconds() * distance;
            if (TargetLines.TryGetValue(id, out var line))
            {
              line.Start = gemPos.Value;
              line.End = UntitledGemGameGameScreen.HomeBasePos;
            }
          }
        }
      }
    }

    public override void Activate()
    {
      var maxGemCount = Level switch
      {
        1 => 5,
        2 => 7,
        3 => 10,
        _ => 0,
      };

      gems.Clear();
      for (int i = 0; i < Math.Min(maxGemCount, HarvesterCollectionSystem.Instance.m_gems2.Count); i++)
      {
        for (int attempt = 0; attempt < 100; attempt++)
        {
          var id = HarvesterCollectionSystem.Instance.m_gems2.GetRandom();
          var gem = HarvesterCollectionSystem.Instance.GetEntityP(id);
          var gemPos = gem?.Get<Transform2>()?.Position;

          if (gemPos == null)
            break;

          if (TargetLines.ContainsKey(id))
            continue;

          TargetLines.Add(id, new LineShape(gemPos.Value, UntitledGemGameGameScreen.HomeBasePos, 0.05f, Color.Yellow, Color.Yellow));
          gems.Add(id);
          break;
        }
      }
    }

    public override void Deactivate()
    {
      TargetLines.Clear();
    }
  }

  public class HarvesterMagnetAbility : IHomeBaseAbility
  {
    public override string IconPath => "Textures/scifi_icons/icon_power/10_power.png";
    public override int Level => UpgradeManager.UG.HarvesterMagnetizer;

    public override void Activate()
    {
      HomeBase.BonusHarvesterMagnetPower = 5.0f;
    }

    public override void Deactivate()
    {
      HomeBase.BonusHarvesterMagnetPower = 0.0f;
    }
  }

  public class DroneAbility : IHomeBaseAbility
  {
    public override string IconPath => "Textures/scifi_icons/icon_snipe/20_snipe.png";
    public override int Level => UpgradeManager.UG.Drones;

    private List<Entity> drones = new List<Entity>();

    public override int DurationTimeMax => 5000;

    public override void Activate()
    {
      var numDrones = Level switch
      {
        1 => 3,
        2 => 5,
        3 => 7,
        _ => 0,
      };

      var random = new Random();
      for (int i = 0; i < numDrones; i++)
      {
        var drone = EntityFactory.Instance.CreateDrone(UntitledGemGameGameScreen.HomeBasePos + new Vector2(random.NextSingle(-50, 50), random.NextSingle(-50, 50)));
        drones.Add(drone);
      }
    }

    public override void Deactivate()
    {
      foreach (var drone in drones)
      {
        drone.Destroy();
      }
      drones.Clear();
    }
  }

  public class HomeBase : ICollisionActor
  {
    public static float BonusMoveSpeed = 1.0f;
    public static float BonusMagnetPower = 0.0f;
    public static float BonusHarvesterMagnetPower = 0.0f;

    public Entity Entity { get; set; }

    public List<IHomeBaseAbility> Abilities = new List<IHomeBaseAbility>();
    public List<IHomeBaseAbility> ActiveAbilities = new List<IHomeBaseAbility>();

    // public List<AbilitySlot> AbilitySlots = new List<AbilitySlot>();

    public Dictionary<IHomeBaseAbility, Button> AbilityButtons = new Dictionary<IHomeBaseAbility, Button>();
    public Dictionary<IHomeBaseAbility, Button> AvailableAbilityButtons = new Dictionary<IHomeBaseAbility, Button>();

    public List<Button> EmptyButtons = new List<Button>();

    public HomeBase()
    {
      // foreach (var ability in Abilities.Take(2))
      // {
      //   ActiveAbilities.Add(ability);
      //
      //   AbilityButtons.TryGetValue(ability, out var button);
      //   if (button != null)
      //   {
      //     var buttonVis = button.Visual as ButtonVisual;
      //     buttonVis.Visible = true;
      //   }
      //
      //   stackPanel.AddChild(button);
      // }


      UpgradeManager.UG.AbilitySlot = 0;
      ActivateAbilities();
    }

    public void ActivateAbility(string upgradeName)
    {
      Console.WriteLine(
        $"Activating ability from upgrade: {upgradeName}"
          );

      IHomeBaseAbility ability = upgradeName switch
      {
        "Speed1" => new SpeedboostAbility(),
        "HBM1" => new MagnetAbility(),
        "Drones1" => new DroneAbility(),
        "HM1" => new HarvesterMagnetAbility(),
        "CM1" => new ChainLightningAbility(),
        _ => null,
      };

      if (ability == null)
      {
        return;
      }

      ActivateAbility(ability);
    }

    public void ActivateAbility(IHomeBaseAbility ability)
    {
      Abilities.Add(ability);

      CreateButtonAvailable(ability);
      CreateButton(ability);

      Console.WriteLine(
        $"Activated ability: {ability.GetType().Name}"
          );
    }

    public void ActivateAbilities()
    {
      // Abilities.Add(new SpeedboostAbility());
      // Abilities.Add(new MagnetAbility());
      // Abilities.Add(new DroneAbility());
      // Abilities.Add(new HarvesterMagnetAbility());
      // Abilities.Add(new ChainLightningAbility());

      CreateButtonPanel();
      CreateAvailableButtonPanel();

      foreach (var ability in Abilities)
      {
        CreateButtonAvailable(ability);
        CreateButton(ability);
      }


      // var empty = new EmptyAbility();
      // Abilities.Add(empty);
      // CreateButton(empty, true);
      // CreateButtonAvailable(empty, true);
      //
      // ActiveAbilities.Add(empty);
      //
      // var b = EmptyButtons.FirstOrDefault();
      // if (b != null)
      // {
      //   var bVis = b.Visual as ButtonVisual;
      //   bVis.Visible = true;
      //   stackPanel.AddChild(b);
      // }
      //




      // AbilitySlots.Add(slot);

      // foreach (var ability in Abilities.Take(1))
      // {
      //   ActiveAbilities.Add(ability);
      //
      //   AbilityButtons.TryGetValue(ability, out var button);
      //   if (button != null)
      //   {
      //     var buttonVis = button.Visual as ButtonVisual;
      //     buttonVis.Visible = true;
      //   }
      //
      //   stackPanel.AddChild(button);
      // }
    }

    private StackPanel stackPanel;
    private StackPanel stackPanelAvailable;

    public void CreateAvailableButtonPanel()
    {

      // var w = GameMain.Instance.Window.ClientBounds.Width;
      // var h = GameMain.Instance.Window.ClientBounds.Height;

      var w = GameMain.Instance.GraphicsDevice.Viewport.Width;
      var h = GameMain.Instance.GraphicsDevice.Viewport.Height;

      stackPanelAvailable = new StackPanel();
      stackPanelAvailable.Orientation = Orientation.Vertical;
      stackPanelAvailable.X = w / 2;
      stackPanelAvailable.Y = h - 200;
      stackPanelAvailable.Spacing = 30;
      stackPanelAvailable.Visual.XOrigin = HorizontalAlignment.Center;
      stackPanelAvailable.Visual.YOrigin = VerticalAlignment.Bottom;

      stackPanelAvailable.IsVisible = false;

      stackPanelAvailable.Visual.AddToManagers(GumService.Default.SystemManagers, GumService.Default.Renderer.MainLayer);
      RenderGuiSystem.Instance.hudItems.Add(stackPanelAvailable.Visual);
    }

    public void CreateButtonPanel()
    {
      var w = GameMain.Instance.GraphicsDevice.Viewport.Width;
      var h = GameMain.Instance.GraphicsDevice.Viewport.Height;

      // var camera = SystemManagers.Default.Renderer.Camera;
      // camera.ScreenToWorld(0, 0, out var worldX, out var worldY);
      stackPanel = new StackPanel();
      stackPanel.Orientation = Orientation.Horizontal;
      stackPanel.X = w / 2;
      stackPanel.Y = h - 100;
      stackPanel.Spacing = 30;
      stackPanel.Visual.XOrigin = HorizontalAlignment.Center;

      stackPanel.Visual.AddToManagers(GumService.Default.SystemManagers, GumService.Default.Renderer.MainLayer);

      RenderGuiSystem.Instance.hudItems.Add(stackPanel.Visual);
    }

    public void CreateButtonAvailable(IHomeBaseAbility ability, bool isEmptyButton = false)
    {
      var w = 100;
      var h = 100;

      var button = new Button()
      {
        Text = "",
        Name = ability.GetType().GetHashCode().ToString(),
        Width = w,
        Height = h,
      };

      var buttonVis = button.Visual as ButtonVisual;
      buttonVis.WidthUnits = Gum.DataTypes.DimensionUnitType.Absolute;
      buttonVis.HeightUnits = Gum.DataTypes.DimensionUnitType.Absolute;
      buttonVis.Width = w;
      buttonVis.Height = h;
      // buttonVis.XOrigin = HorizontalAlignment.Left;
      // buttonVis.YOrigin = VerticalAlignment.Top;

      buttonVis.Children.Clear();

      var background = AssetManager.Load<Texture2D>("Textures/GUI/icon_background.png");
      var icon = AssetManager.Load<Texture2D>(ability.IconPath);

      buttonVis.Children.Add(new ColoredRectangleRuntime()
      {
        Name = "BackgroundRect",
        Color = new Color(150, 150, 150, 255),
        Width = w,
        Height = h,
        HeightUnits = Gum.DataTypes.DimensionUnitType.Absolute,
        WidthUnits = Gum.DataTypes.DimensionUnitType.Absolute,
        // XOrigin = HorizontalAlignment.Center,
        // YOrigin = VerticalAlignment.Center,
      });

      buttonVis.Children.Add(new SpriteRuntime()
      {
        Name = "IconSprite",
        Texture = icon,
        Color = new Color(255, 255, 255, 255),
        Width = w,
        Height = h,
        TextureAddress = Gum.Managers.TextureAddress.EntireTexture,
        HeightUnits = Gum.DataTypes.DimensionUnitType.Absolute,
        WidthUnits = Gum.DataTypes.DimensionUnitType.Absolute,
        // XOrigin = HorizontalAlignment.Center,
        // YOrigin = VerticalAlignment.Center,
      });
      //
      buttonVis.States.Disabled.Apply = () => { };
      buttonVis.States.Focused.Apply = () => { };
      buttonVis.States.Highlighted.Apply = () => { };
      buttonVis.States.HighlightedFocused.Apply = () => { };
      buttonVis.States.Pushed.Apply = () => { };
      buttonVis.States.Enabled.Apply = () => { };
      buttonVis.States.DisabledFocused.Apply = () => { };

      // AbilityButtons.Add(ability, button);

      AvailableAbilityButtons.Add(ability, button);

      stackPanelAvailable.AddChild(button);

      button.Click += (s, e) =>
      {
        Console.WriteLine($"Clicked available ability button: {ability.GetType().Name}");

        if (ability is EmptyAbility && clickedAbility is EmptyAbility)
        {
          stackPanelAvailable.IsVisible = false;
          return;
        }

        {
          stackPanelAvailable.IsVisible = false;

          ActiveAbilities.Remove(clickedAbility);

          Console.WriteLine("Activated ability: " + ability.GetType().Name);
          // Console.WriteLine("index: " + index);
          Console.WriteLine("count: " + ActiveAbilities.Count);
          foreach (var ab in ActiveAbilities)
          {
            Console.WriteLine(" - " + ab.GetType().Name);
          }

          ActiveAbilities.Add(ability);

          AbilityButtons.TryGetValue(ability, out var aButton);
          if (aButton != null)
          {
            var buttonVis = aButton.Visual as ButtonVisual;
            buttonVis.Visible = true;

            ability.CooldownTime = ability.MaxCooldownTime;
          }
          else if (ability is EmptyAbility)
          {
            aButton = EmptyButtons.FirstOrDefault(b => !b.IsVisible);
            var buttonVis = aButton.Visual as ButtonVisual;
            buttonVis.Visible = true;
          }

          if (clickedButton != null)
          {
            int idx = stackPanel.Children.IndexOf(clickedButton);
            stackPanel.Visual.Children[idx] = aButton.Visual;

            Console.WriteLine("Replaced button at index: " + idx);

            var clickedButtonVis = clickedButton.Visual as ButtonVisual;
            clickedButtonVis.Visible = false;
            clickedAbility.Deactivate();
          }
        }
      };
    }

    public void CreateButton(IHomeBaseAbility ability, bool isEmptyButton = false)
    {
      var w = 100;
      var h = 100;

      var button = new Button()
      {
        Text = "",
        Name = ability.GetType().ToString(),
        Width = w,
        Height = h,
      };

      var buttonVis = button.Visual as ButtonVisual;
      buttonVis.WidthUnits = Gum.DataTypes.DimensionUnitType.Absolute;
      buttonVis.HeightUnits = Gum.DataTypes.DimensionUnitType.Absolute;
      buttonVis.Width = w;
      buttonVis.Height = h;
      buttonVis.Visible = false;
      // buttonVis.XOrigin = HorizontalAlignment.Left;
      // buttonVis.YOrigin = VerticalAlignment.Top;

      buttonVis.Children.Clear();

      var background = AssetManager.Load<Texture2D>("Textures/GUI/icon_background.png");
      var icon = AssetManager.Load<Texture2D>(ability.IconPath);
      var overlay = AssetManager.Load<Texture2D>("Textures/GUI/icon_background.png");

      buttonVis.Children.Add(new ColoredRectangleRuntime()
      {
        Name = "BackgroundRect",
        Color = new Color(150, 150, 150, 255),
        Width = w,
        Height = h,
        HeightUnits = Gum.DataTypes.DimensionUnitType.Absolute,
        WidthUnits = Gum.DataTypes.DimensionUnitType.Absolute,
        // XOrigin = HorizontalAlignment.Center,
        // YOrigin = VerticalAlignment.Center,
      });

      buttonVis.Children.Add(new SpriteRuntime()
      {
        Name = "IconSprite",
        Texture = icon,
        Color = new Color(255, 255, 255, 255),
        Width = w,
        Height = h,
        TextureAddress = Gum.Managers.TextureAddress.EntireTexture,
        HeightUnits = Gum.DataTypes.DimensionUnitType.Absolute,
        WidthUnits = Gum.DataTypes.DimensionUnitType.Absolute,
        // XOrigin = HorizontalAlignment.Center,
        // YOrigin = VerticalAlignment.Center,
      });
      //
      buttonVis.Children.Add(new SpriteRuntime()
      {
        Name = "OverlaySprite",
        Texture = overlay,
        Color = new Color(255, 255, 255, 100),
        Width = 50,
        Height = h,
        TextureAddress = Gum.Managers.TextureAddress.EntireTexture,
        HeightUnits = Gum.DataTypes.DimensionUnitType.Absolute,
        WidthUnits = Gum.DataTypes.DimensionUnitType.PercentageOfParent,
        XOrigin = HorizontalAlignment.Right,
        // YOrigin = VerticalAlignment.Center,
        // XUnits = GeneralUnitType.PixelsFromSmall,
        X = w,
      });

      buttonVis.States.Disabled.Apply = () => { };
      buttonVis.States.Focused.Apply = () => { };
      buttonVis.States.Highlighted.Apply = () => { };
      buttonVis.States.HighlightedFocused.Apply = () => { };
      buttonVis.States.Pushed.Apply = () => { };
      buttonVis.States.Enabled.Apply = () => { };
      buttonVis.States.DisabledFocused.Apply = () => { };

      if (!isEmptyButton)
        AbilityButtons.Add(ability, button);
      else
        EmptyButtons.Add(button);

      // stackPanel.AddChild(button);

      button.Click += (s, e) =>
      {
        Console.WriteLine($"Clicked ability button: {ability.GetType().Name}");

        stackPanelAvailable.IsVisible = !stackPanelAvailable.IsVisible;

        var empty = Abilities.OfType<EmptyAbility>().FirstOrDefault();

        var availableAbilities = Abilities.Except(ActiveAbilities).ToList();

        if (availableAbilities.Contains(empty) == false)
        {
          availableAbilities.Add(empty);
        }

        if (availableAbilities.Count > 0)
        {
          clickedButton = button;
          clickedAbility = ability;

          foreach (var kvp in AvailableAbilityButtons)
          {
            if (availableAbilities.Contains(kvp.Key))
            {
              kvp.Value.IsVisible = true;
            }
            else
            {
              kvp.Value.IsVisible = false;
            }
          }
        }
      };
    }

    private Button clickedButton;
    private IHomeBaseAbility clickedAbility;

    public void OnCollision(CollisionEventArgs collisionInfo)
    {

    }

    public float ShakeMagnitude { get; private set; }
    public float ShakeDuration { get; private set; }
    // NEW: Store the original values
    private float initialShakeMagnitude;
    private float initialShakeDuration;

    private Random random = new Random();

    private Vector2 GetShakeOffset()
    {
      if (ShakeDuration <= 0 || initialShakeDuration <= 0)
      {
        return Vector2.Zero;
      }

      // 1. Calculate the fade ratio (where 1.0 is full shake, 0.0 is no shake)
      // As ShakeDuration decreases, this ratio approaches zero.
      float fadeRatio = ShakeDuration / initialShakeDuration;

      // 2. Calculate the current effective magnitude
      // This scales the initial shake magnitude by the fade ratio.
      float currentMagnitude = initialShakeMagnitude * fadeRatio;

      // You could also use MathHelper.Lerp:
      // float currentMagnitude = MathHelper.Lerp(0, initialShakeMagnitude, fadeRatio);

      // 3. Generate random offset based on the CURRENT (diminishing) magnitude
      float offsetX = (float)(random.NextDouble() * 2 - 1) * currentMagnitude;
      float offsetY = (float)(random.NextDouble() * 2 - 1) * currentMagnitude;

      return new Vector2(offsetX, offsetY);
    }

    public void StartShake(float magnitude, float duration)
    {
      // Only start a new, stronger shake
      if (magnitude > ShakeMagnitude)
      {
        ShakeMagnitude = magnitude; // This holds the current *maximum* magnitude
        initialShakeMagnitude = magnitude; // This holds the *starting* magnitude

        ShakeDuration = duration;
        initialShakeDuration = duration;
      }
    }

    public void Update(GameTime gameTime)
    {
      int slots = UpgradeManager.UG.AbilitySlot;
      float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

      // if (AbilitySlots.Count < slots)
      {
        // var slot = new AbilitySlot();
        // AbilitySlots.Add(slot);

        // foreach (var ability in Abilities.Take(2))
        // {
        //   ActiveAbilities.Add(ability);
        //
        //   AbilityButtons.TryGetValue(ability, out var button);
        //   if (button != null)
        //   {
        //     var buttonVis = button.Visual as ButtonVisual;
        //     buttonVis.Visible = true;
        //   }
        //
        //   stackPanel.AddChild(button);
        // }
      }

      if (ShakeDuration > 0)
      {
        ShakeDuration -= deltaTime;
      }
      else
      {
        ShakeMagnitude = 0;
      }

      var transform = Entity.Get<Transform2>();
      var x = MathHelper.Lerp(transform.Scale.X, 1.0f, gameTime.GetElapsedSeconds() * 5.0f);
      var y = MathHelper.Lerp(transform.Scale.Y, 1.0f, gameTime.GetElapsedSeconds() * 5.0f);
      transform.Scale = new Vector2(x, y);
      transform.Position += GetShakeOffset();

      if (EmptyButtons.Count < slots)
      {
        var empty = new EmptyAbility();

        ActiveAbilities.Add(empty);
        // Abilities.Add(empty);
        CreateButton(empty, true);
        CreateButtonAvailable(empty, true);

        var b = EmptyButtons.LastOrDefault();
        if (b != null)
        {
          var bVis = b.Visual as ButtonVisual;
          bVis.Visible = true;
          stackPanel.AddChild(b);
        }
      }

      foreach (var ability in Abilities)
      {
        ability.Update(gameTime);
      }

      foreach (var ability in ActiveAbilities)
      {
        if (ability is EmptyAbility)
          continue;

        if (ability.IsActive)
        {
          ability.DurationTime -= gameTime.ElapsedGameTime.Milliseconds;

          var percent = 1.0f - (float)ability.DurationTime / ability.DurationTimeMax;
          AbilityButtons.TryGetValue(ability, out var button);
          if (button != null)
          {
            var buttonVis = button.Visual as ButtonVisual;

            if (buttonVis.Children.FirstOrDefault(x => x.Name == "OverlaySprite") is SpriteRuntime overlaySprite)
            {
              overlaySprite.Width = percent * 100.0f;
              ((ColoredRectangleRuntime)buttonVis.Children.FirstOrDefault(x => x.Name == "BackgroundRect")).Color = new Color((int)(200 * (1.0f - percent)), (int)(200 * (1.0f - percent)), (int)(250 * (1.0f - percent)), 255);
            }
          }

          if (ability.DurationTime <= 0)
          {
            ability.Deactivate();
            ability.CooldownTime = ability.MaxCooldownTime;
          }
        }
        else
        {
          ability.CooldownTime -= gameTime.ElapsedGameTime.Milliseconds;

          var percent = 1.0f - (float)ability.CooldownTime / ability.MaxCooldownTime;
          AbilityButtons.TryGetValue(ability, out var button);
          if (button != null)
          {
            var buttonVis = button.Visual as ButtonVisual;

            if (buttonVis.Children.FirstOrDefault(x => x.Name == "OverlaySprite") is SpriteRuntime overlaySprite)
            {
              overlaySprite.Width = (1.0f - percent) * 100.0f;
              ((ColoredRectangleRuntime)buttonVis.Children.FirstOrDefault(x => x.Name == "BackgroundRect")).Color = new Color((int)(150 * (percent)), (int)(150 * (percent)), (int)(250 * (percent)), 255);
            }
          }

          if (ability.CooldownTime <= 0)
          {
            ability.Activate();
            ability.DurationTime = ability.DurationTimeMax;
          }
        }
      }
    }

    public IShapeF Bounds { get; set; }
  }
}
