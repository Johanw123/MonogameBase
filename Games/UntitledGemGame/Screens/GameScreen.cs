using System;
using Apos.Shapes;
using AsyncContent;
using ImGuiNET;
using JapeFramework.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using MonoGame.Extended.ECS;
using MonoGame.Extended.Input;
using MonoGame.Extended.Screens;
using MonoGame.Extended.Tweening;
using MonoGameGum;
using Serilog;
using UntitledGemGame.Entities;
using UntitledGemGame.Systems;
using Vector4 = System.Numerics.Vector4;


//https://github.com/cpt-max/MonoGame-Shader-Samples?tab=readme-ov-file
//https://github.com/Amrik19/Monogame-Spritesheet-Instancing

namespace UntitledGemGame.Screens
{
  public class UntitledGemGameGameScreen : GameScreen
  {
    private SpriteBatch m_spriteBatch;
    private ShapeBatch m_shapeBatch;

    private World m_escWorld;
    private EntityFactory m_entityFactory;
    private OrthographicCamera m_camera;
    private OrthographicCamera m_gui_camera;

    private FrameCounter _frameCounter = new FrameCounter();

    public static int Collected;
    public static int Delivered;
    public static int DeliveredUncounted;

    private GameState m_gameState = new GameState();
    private UpgradeManager m_upgradeManager = new UpgradeManager();

    GumService Gum => GumService.Default;

    public UntitledGemGameGameScreen(Game game) : base(game)
    {
      game.IsMouseVisible = true;
    }

    public static Vector2 HomeBasePos = Vector2.Zero;
    private RenderGuiSystem _renderGuiSystem;
    private Entity m_homeBaseEntity;

    public override void LoadContent()
    {
      base.LoadContent();

      TextureCache.PreloadTextures();
      EffectCache.PreloadEffects();

      AssetManager.BatchLoaded += PostInit;
    }

    private void PostInit()
    {
      Log.Information("UntitledGemGameGameScreen PostInit");

      m_shapeBatch = new ShapeBatch(GraphicsDevice, Content, EffectCache.ShapeFx);
      _renderGuiSystem = new RenderGuiSystem(m_spriteBatch, m_shapeBatch, GraphicsDevice,
          m_gui_camera, GameMain.GumServiceUpgrades, EffectCache.BlurFx);

      m_escWorld = new WorldBuilder()
        .AddSystem(new HarvesterCollectionSystem(m_camera, m_shapeBatch))
        .AddSystem(new UpdateSystem2())
        .AddSystem(new RenderSystem(m_spriteBatch, m_shapeBatch, GraphicsDevice, m_camera))
        .AddSystem(new RenderGemSystem(m_spriteBatch, GraphicsDevice, m_camera))
        // .AddSystem(new RenderGuiSystem(m_spriteBatch, GraphicsDevice, m_gui_camera, GameMain.GumServiceUpgrades))
        .Build();

      m_entityFactory = new EntityFactory(m_escWorld, GraphicsDevice);

      InitImGuiContent();
      InitHudContent();

      m_camera.Zoom = UpgradeManager.UG.CameraZoomScale;
      m_upgradeManager.Init(m_gameState);

      m_upgradeManager.OnUpgradeRoot += () =>
      {
        HomeBasePos = m_camera.ScreenToWorld(new Vector2(GraphicsDevice.Viewport.Width / 2.0f, GraphicsDevice.Viewport.Height / 2.0f));
        m_homeBaseEntity = m_entityFactory.CreateHomeBase(HomeBasePos);
      };

      for (int i = 0; i < UpgradeManager.UG.StartingGemCount; i++)
      {
        var a = m_camera.ScreenToWorld(RandomHelper.Vector2(new Vector2(50, 50), new Vector2(GraphicsDevice.Viewport.Width - 100, GraphicsDevice.Viewport.Height - 100)));
        m_entityFactory.CreateGem(a, GemTypes.Red);
      }
    }

    public override void Initialize()
    {
      m_spriteBatch = new SpriteBatch(GraphicsDevice);
      m_camera = new OrthographicCamera(GraphicsDevice);
      m_gui_camera = new OrthographicCamera(GraphicsDevice);

      FontStashSharpText.m_camera = m_camera;

      base.Initialize();
    }

    private int time = UpgradeManager.UG.GemSpawnCooldown;

    public override void Update(GameTime gameTime)
    {
      if (m_escWorld == null)
        return;

      m_upgradeManager.Update(gameTime);
      m_homeBaseEntity?.Get<HomeBase>()?.Update(gameTime);
      var keyboardState = KeyboardExtended.GetState();

      time -= gameTime.ElapsedGameTime.Milliseconds;

      if (time < 0)
        time = 0;

      while (time <= 0 && HarvesterCollectionSystem.m_gems2.Count < UpgradeManager.UG.MaxGemCount)
      {
        for (int i = 0; i < UpgradeManager.UG.GemSpawnRate; i++)
        {
          var a = m_camera.ScreenToWorld(RandomHelper.Vector2(new Vector2(50, 50), new Vector2(GraphicsDevice.Viewport.Width - 100, GraphicsDevice.Viewport.Height - 100)));
          m_entityFactory.CreateGem(a, GemTypes.Red);

          if (HarvesterCollectionSystem.m_gems2.Count >= UpgradeManager.UG.MaxGemCount)
            break;
        }

        // var a2 = m_camera.ScreenToWorld(RandomHelper.Vector2(new Vector2(50, 50), new Vector2(GraphicsDevice.Viewport.Width - 100, GraphicsDevice.Viewport.Height - 100)));
        // m_entityFactory.CreateGem(a2, GemTypes.Blue);

        time += UpgradeManager.UG.GemSpawnCooldown;
      }

      if (keyboardState.WasKeyPressed(Keys.F2))
      {
        // drawUpgradesGui = true;
        UpgradeManager.UpgradeGuiEditMode = !UpgradeManager.UpgradeGuiEditMode;

      }

      if (keyboardState.WasKeyPressed(Keys.B))
      {
        //var a = m_camera.ScreenToWorld(RandomHelper.Vector2(Vector2.Zero, new Vector2(1920, 900)));
        //m_entityFactory.CreateHarvester(a);

        // ++Upgrades.HarvesterCount;
        // Upgrades.HarvesterCount.Increment();
      }


      if (keyboardState.WasKeyPressed(Keys.F9))
      {
        UpgradeManager.CurrentUpgrades.SaveToJson();
      }

      if (keyboardState.WasKeyPressed(Keys.F5))
      {
        m_upgradeManager.RefreshButtons();
      }

      if (keyboardState.IsKeyDown(Keys.I))
      {
        //m_camera.ZoomIn(0.01f);

        UpgradeManager.UG.CameraZoomScale += 0.01f;
      }

      if (keyboardState.IsKeyDown(Keys.O))
      {
        //m_camera.ZoomOut(0.01f);

        UpgradeManager.UG.CameraZoomScale -= 0.01f;
      }

      //if (keyboardState.IsKeyDown(Keys.R))
      //{
      //}

      if (DeliveredUncounted > 0)
      {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        int toDeliver = Math.Clamp((int)(DeliveredUncounted * dt), 1, DeliveredUncounted);

        if (DeliveredUncounted > 100)
        // if (toDeliver > 10)
        {
          if (_gemCountTween == null)
          {

            _gemCountTween = _tweener.TweenTo(target: this, expression: t => t.gemCountFontSize, toValue: 65f, duration: 0.15f)
                             .Easing(EasingFunctions.BounceInOut).AutoReverse();
          }
          else
          {
            if (_gemCountTween.IsComplete)
            {
              _gemCountTween.CancelAndComplete();
              gemCountFontSize = 55f;
              _gemCountTween = _tweener.TweenTo(target: this, expression: t => t.gemCountFontSize, toValue: 65f, duration: 0.15f)
                               .Easing(EasingFunctions.BounceInOut).AutoReverse();
            }
          }

          toDeliver = (int)(DeliveredUncounted * 0.6f);
        }

        Delivered += toDeliver;
        DeliveredUncounted -= toDeliver;

        m_gameState.CurrentRedGemCount += toDeliver;

        Console.WriteLine($"ToDeliver: {toDeliver}");

        m_upgradeManager.UpdateTooltipContent();
      }

      // m_camera.Zoom = UpgradeManager.UG.CameraZoomScale;
      // m_camera.Zoom = MathHelper.Lerp(m_camera.Zoom, UpgradeManager.UG.CameraZoomScale, (float)gameTime.ElapsedGameTime.TotalSeconds);
      //TODO: find better lerp or an easing function
      m_camera.Zoom = MathHelper.Lerp(m_camera.Zoom, UpgradeManager.UG.CameraZoomScale, (float)gameTime.ElapsedGameTime.TotalSeconds);

      m_escWorld.Update(gameTime);

      var curHarvesters = m_entityFactory.Harvesters.Count;
      if (curHarvesters < UpgradeManager.UG.HarvesterCount)
      {
        var a = m_camera.ScreenToWorld(RandomHelper.Vector2(Vector2.Zero, new Vector2(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height)));
        m_entityFactory.CreateHarvester(a);
      }
      else if (curHarvesters > UpgradeManager.UG.HarvesterCount)
      {
        m_entityFactory.RemoveRandomHarvester();
      }

      if (!UpgradeManager.UpdatingButtons)
        _renderGuiSystem?.Update(gameTime);

      _tweener?.Update((float)gameTime.ElapsedGameTime.TotalSeconds);

      // Gum.Update(gameTime);
    }

    private bool showDebugGUI = false;

    public float gemCountFontSize { get; set; } = 55f;
    private readonly Tweener _tweener = new();
    private Tween? _gemCountTween;

    private void InitHudContent()
    {
      GameMain.AddCustomHudContent(() =>
      {
        // FontManager.RenderFieldFont(() => ContentDirectory.Fonts.Roboto_Regular_ttf, $"Picked Up: {Collected}", new Vector2(10, 70), Color.Yellow, Color.Black, 35);
        // FontManager.RenderFieldFont(() => ContentDirectory.Fonts.Roboto_Regular_ttf, $"zoom: {m_camera.Zoom}", new Vector2(10, 100), Color.Yellow, Color.Black, 35);
        // Gum.Draw();

        if (!UpgradeManager.UpdatingButtons)
          _renderGuiSystem?.Draw();


        var red = TextureCache.HudRedGem.Value;
        var blue = TextureCache.HudBlueGem.Value;

        m_spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
        m_spriteBatch.Draw(red, new Rectangle(10, 33, red.Bounds.Width, red.Bounds.Height), Color.White);
        m_spriteBatch.Draw(blue, new Rectangle(10, 110, blue.Bounds.Width, blue.Bounds.Height), Color.White);
        m_spriteBatch.End();

        var gemCount = m_gameState.CurrentRedGemCount;
        FontManager.RenderFieldFont(() => ContentDirectory.Fonts.Roboto_Regular_ttf, $"{gemCount}", new Vector2(50, 20), Color.Yellow, Color.Black, gemCountFontSize);


        FontManager.RenderFieldFont(() => ContentDirectory.Fonts.Roboto_Regular_ttf, $"{m_gameState.CurrentBlueGemCount}", new Vector2(50, 90), Color.Yellow, Color.Black, 55f);



        //FIXE: debug rendering
        // var camera = RenderingLibrary.SystemManagers.Default.Renderer.Camera;
        // m_shapeBatch.Begin();
        // foreach (var item in UpgradeManager.m_tooltipValueElements)
        // {
        //   Console.WriteLine(item.Width);
        //   camera.WorldToScreen(item.AbsoluteX, item.AbsoluteY, out float screenX, out float screenY);
        //   m_shapeBatch.BorderRectangle(new Vector2(screenX, screenY), new Vector2(item.Width, item.Height) * camera.Zoom, Color.AliceBlue);
        // }
        // m_shapeBatch.End();
      });
    }


    private void InitImGuiContent()
    {
      GameMain.AddCustomImGuiContent(() =>
      {
        if (KeyboardExtended.GetState().WasKeyPressed(Keys.Tab))
        {
          showDebugGUI = !showDebugGUI;
        }

        if (showDebugGUI && !UpgradeManager.UpgradeGuiEditMode)
        {
          ImGui.SetNextWindowBgAlpha(1.0f);
          // var deltaTime = (float)GameMain.GameInstance.TargetElapsedTime.TotalSeconds;
          // _frameCounter.Update(deltaTime);
          var fps = string.Format("FPS: {0}", _frameCounter.AverageFramesPerSecond);
          ImGui.Text(fps);
          ImGui.Text($"Entities: {m_escWorld.EntityCount}");
          ImGui.Text($"Picked Up: {Collected}");
          ImGui.Text($"Delivered: {Delivered}");

          ImGui.SetNextWindowBgAlpha(1.0f);

          ImGui.GetStyle().Colors[(int)ImGuiCol.SliderGrab] = new Vector4(1.0f, 0.0f, 0.0f, 1.0f);
          ImGui.GetStyle().Colors[(int)ImGuiCol.SliderGrabActive] = new Vector4(0.0f, 1.0f, 0.0f, 1.0f);

          //ImGui.GetStyle().Colors[(int)ImGuiCol.WindowBg] = new Vector4(0.0f, 1.0f, 0.0f, 1.0f);
          //ImGui.GetStyle().Colors[(int)ImGuiCol.ChildBg] = new Vector4(0.0f, 1.0f, 0.0f, 1.0f);
          ImGui.GetStyle().Colors[(int)ImGuiCol.FrameBg] = new Vector4(0.2f, 0.2f, 0.2f, 1.0f);
          ImGui.GetStyle().Colors[(int)ImGuiCol.FrameBgActive] = new Vector4(0.4f, 0.4f, 0.4f, 1.0f);
          ImGui.GetStyle().Colors[(int)ImGuiCol.FrameBgHovered] = new Vector4(0.3f, 0.3f, 0.3f, 1.0f);
          //ImGui.GetStyle().Colors[(int)ImGuiCol.ScrollbarBg] = new Vector4(0.0f, 1.0f, 0.0f, 1.0f);

          //ImGui.Begin("adad");
          //ImGui.GetStyle().Alpha = 1.0f;
          ImGui.SliderFloat("HarvesterSpeed", ref UpgradeManager.UG.HarvesterSpeed, 0, 5000.0f);
          ImGui.SliderFloat("CameraZoomScale", ref UpgradeManager.UG.CameraZoomScale, 0, 3.0f);


          ImGui.SliderFloat("HarvesterCollectionRange", ref UpgradeManager.UG.HarvesterCollectionRange, 0, 100);
          ImGui.SliderFloat("HomebaseCollectionRange", ref UpgradeManager.UG.HomebaseCollectionRange, 0, 100);

          ImGui.SliderInt("HarvesterCapacity", ref UpgradeManager.UG.HarvesterCapacity, 0, 5000);


          ImGui.SliderInt("MaxGemCount", ref UpgradeManager.UG.MaxGemCount, 0, 500000);
          ImGui.SliderInt("GemSpawnCooldown", ref UpgradeManager.UG.GemSpawnCooldown, 1, 1000);

          ImGui.SliderInt("HarvesterCount", ref UpgradeManager.UG.HarvesterCount, 0, 25);
          ImGui.SliderInt("GemSpawnRate", ref UpgradeManager.UG.GemSpawnRate, 0, 500);


          ImGui.SliderFloat("HarvesterMaximumFuel", ref UpgradeManager.UG.HarvesterMaxFuel, 0, 10000f);

          ImGui.SliderFloat("HarvesterRefuelSpeed", ref UpgradeManager.UG.HarvesterRefuelSpeed, 1, 1000f);

          ImGui.Checkbox("RefuelAtHomebase", ref UpgradeManager.UG.RefuelHomebase);
          ImGui.Checkbox("HomebaseCollector", ref UpgradeManager.UG.HomeBaseCollector);
          ImGui.Checkbox("AutoRefuel", ref UpgradeManager.UG.AutoRefuel);
          //ImGui.Combo("Test", ref Upgrades.HarvesterCollectionStrategyInt, Enum.GetNames<HarvesterStrategy>(), 10);

          if (ImGui.BeginCombo("HarvesterCollectionStrategy", Upgrades.HarvesterCollectionStrategy.ToString()))
          {
            for (int i = 0; i < Enum.GetValues(typeof(HarvesterStrategy)).Length; i++)
            {
              var projType = (HarvesterStrategy)i;
              bool isSelected = Upgrades.HarvesterCollectionStrategy == projType;
              if (ImGui.Selectable(projType.ToString(), isSelected))
              {
                Upgrades.HarvesterCollectionStrategy = projType;
              }

              if (isSelected)
                ImGui.SetItemDefaultFocus();
            }

            ImGui.EndCombo();
          }
          //ImGui.End();
        }
      });
    }

    public override void Draw(GameTime gameTime)
    {
      if (m_escWorld == null)
        return;

      m_escWorld.Draw(gameTime);

      var deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
      _frameCounter.Update(deltaTime);
      // var fps = string.Format("FPS: {0}", _frameCounter.AverageFramesPerSecond);

      // m_spriteBatch.Begin(SpriteSortMode.Immediate);
      // FontManager.RenderFieldFont(() => ContentDirectory.Fonts.Roboto_Regular_ttf, fps, new Vector2(10, 10), Color.Black, Color.White, 35);
      // FontManager.RenderFieldFont(() => ContentDirectory.Fonts.Roboto_Regular_ttf, $"Entities: {m_escWorld.EntityCount}", new Vector2(10, 40), Color.Black, Color.White, 35);

      // FontManager.GetTextRenderer().RenderStroke();
      // m_spriteBatch.End();



    }
  }
}
