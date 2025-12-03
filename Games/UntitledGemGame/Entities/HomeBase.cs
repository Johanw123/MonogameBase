using MonoGame.Extended;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MonoGame.Extended.Collisions;
using UntitledGemGame.Screens;
using MonoGame.Extended.ECS;
using JapeFramework.Helpers;
using Microsoft.Xna.Framework;
using Gum.Forms.Controls;
using RenderingLibrary;
using MonoGameGum;
using Gum.Forms.DefaultVisuals;
using MonoGameGum.GueDeriving;
using AsyncContent;
using Microsoft.Xna.Framework.Graphics;
using RenderingLibrary.Graphics;
using Gum.Converters;
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

    public int Level = 0;

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

    public override void Activate()
    {
      HomeBase.BonusMoveSpeed = 2.0f;
      Console.WriteLine("Speedboost activated!");
    }

    public override void Deactivate()
    {
      HomeBase.BonusMoveSpeed = 1.0f;
      Console.WriteLine("Speedboost deactivated!");
    }
  }

  public class MagnetAbility : IHomeBaseAbility
  {
    public override string IconPath => "Textures/scifi_icons/icon_power/11_power.png";

    public override void Activate()
    {
      HomeBase.BonusMagnetPower = 5.0f;
      Console.WriteLine("Magnet activated!");
    }

    public override void Deactivate()
    {
      HomeBase.BonusMagnetPower = 0.0f;
      Console.WriteLine("Magnet deactivated!");
    }
  }

  public class ChainLightningAbility : IHomeBaseAbility
  {
    public override string IconPath => "Textures/scifi_icons/icon_power/12_power.png";

    List<int> gems = new List<int>();

    public override void Update(GameTime gameTime)
    {
      if (gems == null)
        return;

      foreach (var id in gems.ToArray())
      {
        var gem = HarvesterCollectionSystem.Instance.GetEntityP(id);
        if (gem != null)
        {
          var dir = UntitledGemGameGameScreen.HomeBasePos - gem.Get<Transform2>().Position;
          dir.Normalize();
          gem.Get<Transform2>().Position += dir * 200.0f * (float)gameTime.GetElapsedSeconds();

          if (Vector2.Distance(gem.Get<Transform2>().Position, UntitledGemGameGameScreen.HomeBasePos) < 10.0f)
          {
            gems.Remove(id);
          }
        }
      }
    }

    public override void Activate()
    {
      Console.WriteLine("Chain Lightning activated!");

      gems.Clear();
      for (int i = 0; i < Math.Min(5, HarvesterCollectionSystem.m_gems2.Count); i++)
      {
        //TODO: fix so same gem cant be randomed twice
        var gem = HarvesterCollectionSystem.m_gems2.GetRandom();
        gems.Add(gem);
      }
    }

    public override void Deactivate()
    {
      Console.WriteLine("Chain Lightning deactivated!");
    }
  }

  public class HarvesterMagnetAbility : IHomeBaseAbility
  {
    public override string IconPath => "Textures/scifi_icons/icon_power/10_power.png";

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

    private List<Entity> drones = new List<Entity>();

    public override int DurationTimeMax => 5000;

    public override void Activate()
    {
      Console.WriteLine("Drone activated!");
      var random = new Random();
      for (int i = 0; i < 10; i++)
      {
        var drone = EntityFactory.Instance.CreateDrone(UntitledGemGameGameScreen.HomeBasePos + new Vector2(random.NextSingle(-50, 50), random.NextSingle(-50, 50)));
        drones.Add(drone);
      }
    }

    public override void Deactivate()
    {
      foreach (var drone in drones)
      {
        // EntityFactory.Instance.RemoveHarvester(drone.Id);
        drone.Destroy();
      }
      drones.Clear();
      Console.WriteLine("Drone deactivated!");
    }
  }

  // public class AbilitySlot
  // {
  //   public IHomeBaseAbility ActiveAbility;
  //
  //   public List<Button> AbilityButtons = new List<Button>();
  //   public List<Button> AvailableAbilityButtons = new List<Button>();
  // }

  public class HomeBase : ICollisionActor
  {
    public static float BonusMoveSpeed = 1.0f;
    public static float BonusMagnetPower = 0.0f;
    public static float BonusHarvesterMagnetPower = 0.0f;

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


      ActivateAbilities();
    }

    public void ActivateAbilities()
    {
      Abilities.Add(new SpeedboostAbility());
      Abilities.Add(new MagnetAbility());
      Abilities.Add(new DroneAbility());
      Abilities.Add(new HarvesterMagnetAbility());
      Abilities.Add(new ChainLightningAbility());

      CreateButtonPanel();
      CreateAvailableButtonPanel();

      foreach (var ability in Abilities)
      {
        CreateButtonAvailable(ability);
        CreateButton(ability);
      }


      var empty = new EmptyAbility();
      Abilities.Add(empty);
      CreateButton(empty, true);
      CreateButtonAvailable(empty, true);

      UpgradeManager.UG.AbilitySlot = 2;

      // var slot = new AbilitySlot();


      ActiveAbilities.Add(empty);

      var b = EmptyButtons.FirstOrDefault();
      if (b != null)
      {
        var bVis = b.Visual as ButtonVisual;
        bVis.Visible = true;
        stackPanel.AddChild(b);
      }





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
      RenderGuiSystem.hudItems.Add(stackPanelAvailable.Visual);
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

      RenderGuiSystem.hudItems.Add(stackPanel.Visual);
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

    public void Update(GameTime gameTime)
    {
      int slots = UpgradeManager.UG.AbilitySlot;

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
