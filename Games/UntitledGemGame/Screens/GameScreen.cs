using System;
using Apos.Shapes;
using Apos.Tweens;
using AsyncContent;
using ImGuiNET;
using JapeFramework;
using JapeFramework.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using MonoGame.Extended.ECS;
using MonoGame.Extended.Input;
using MonoGame.Extended.Screens;
using MonoGame.Extended.Tweening;
using MonoGame.Extended.ViewportAdapters;
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

    public static ulong Collected;
    public static ulong Delivered;
    public static ulong DeliveredUncounted;

    Tween preGameTween;
    private bool GameStarted = false;

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

      // TextureCache.PreloadTextures();
      // EffectCache.PreloadEffects();
      //
      // AssetManager.BatchLoaded += PostInit;
      PostInit();
    }

    private void PostInit()
    {
      Log.Information("UntitledGemGameGameScreen PostInit");

      m_camera.Zoom = UpgradeManager.UG.CameraZoomScale;

      m_shapeBatch = new ShapeBatch(GraphicsDevice, Content, EffectCache.ShapeFx);
      _renderGuiSystem = new RenderGuiSystem(m_spriteBatch, m_shapeBatch, GraphicsDevice,
          m_gui_camera, GameMain.GumServiceUpgrades);

      m_escWorld = new WorldBuilder()
        .AddSystem(new HarvesterCollectionSystem(m_camera, m_shapeBatch))
        .AddSystem(new UpdateSystem2())
        .AddSystem(new RenderGemSystem(m_spriteBatch, GraphicsDevice, m_camera))
        .AddSystem(new RenderSystem(m_spriteBatch, m_shapeBatch, GraphicsDevice, m_camera))
        // .AddSystem(new RenderGuiSystem(m_spriteBatch, GraphicsDevice, m_gui_camera, GameMain.GumServiceUpgrades))
        .Build();

      m_entityFactory = new EntityFactory(m_escWorld, GraphicsDevice);

      InitImGuiContent();
      InitHudContent();

      // m_camera.Zoom = 1.0f;

      // var width = GameMain.Instance.Window.ClientBounds.Width;
      // var height = GameMain.Instance.Window.ClientBounds.Height;
      // var width = GraphicsDevice.PresentationParameters.BackBufferWidth;
      // var height = GraphicsDevice.PresentationParameters.BackBufferHeight;
      var width = GraphicsDevice.Viewport.Width;
      var height = GraphicsDevice.Viewport.Height;

      m_upgradeManager.OnUpgradeRoot += () =>
      {
        // UpgradeManager.UG.HarvesterCount += 1;
      };

      // HomeBasePos = m_camera.ScreenToWorld(new Vector2(width / 2.0f, height / 2.0f));
      HomeBasePos = m_camera.ScreenToWorld(BaseGame.ViewportCenter);
      // m_homeBaseEntity = m_entityFactory.CreateHomeBase(new Vector2(HomeBasePos.X, m_camera.ScreenToWorld(new Vector2(0, height + 300)).Y));
      m_homeBaseEntity = m_entityFactory.CreateHomeBase(new Vector2(HomeBasePos.X, HomeBasePos.Y + 100.0f));

      m_upgradeManager.Init(m_gameState);

      // var position = new Vector2Tween(new Vector2(50, 50), new Vector2(200, 200), 2000, Easing.SineIn);

      m_homeBaseEntity.Get<HomeBase>().StartShake(2.5f, 3.0f);
      preGameTween = _tweenerPreGame.TweenTo(m_homeBaseEntity.Get<Transform2>(), t => t.Position, HomeBasePos, duration: 3.0f).OnEnd((a) =>
      {
        GameStart();
      }).Easing(EasingFunctions.CubicOut);
    }

    private void GameStart()
    {
      GameStarted = true;
    }

    public override void Initialize()
    {
      m_spriteBatch = new SpriteBatch(GraphicsDevice);
      // m_camera = new OrthographicCamera(GraphicsDevice);
      // m_camera = new OrthographicCamera(new BoxingViewportAdapter();
      // m_camera = new OrthographicCamera(JapeFramework.BaseGame.BoxingViewportAdapter);
      // m_gui_camera = new OrthographicCamera(GraphicsDevice);
      // m_gui_camera = new OrthographicCamera(JapeFramework.BaseGame.BoxingViewportAdapter);

      // m_camera = JapeFramework.BaseGame.Camera;
      // m_gui_camera = JapeFramework.BaseGame.HudCamera;

      m_camera = new OrthographicCamera(BaseGame.BoxingViewportAdapter);
      m_gui_camera = new OrthographicCamera(BaseGame.BoxingViewportAdapter);

      FontStashSharpText.m_camera = m_camera;

      base.Initialize();
    }

    private int time = UpgradeManager.UG.GemSpawnCooldown;

    public override void Update(GameTime gameTime)
    {
      if (m_escWorld == null)
        return;

      if (!preGameTween.IsComplete)
      {
        _tweenerPreGame.Update((float)gameTime.ElapsedGameTime.TotalSeconds);
        m_homeBaseEntity.Get<HomeBase>()?.Update(gameTime);
        return;
      }

      m_upgradeManager.Update(gameTime);
      m_homeBaseEntity?.Get<HomeBase>()?.Update(gameTime);
      var keyboardState = KeyboardExtended.GetState();

      time -= gameTime.ElapsedGameTime.Milliseconds;

      if (time < 0)
        time = 0;

      var vp = BaseGame.BoxingViewportAdapter.Viewport;
      var p0 = m_camera.ScreenToWorld(new Vector2(vp.X, vp.Y));
      var p1 = m_camera.ScreenToWorld(new Vector2(vp.X + vp.Width, vp.Y + vp.Height));

      Vector2 spriteSize = new Vector2(32, 32);
      Vector2 halfSpriteSize = spriteSize / 2.0f;

      while (time <= 0 && HarvesterCollectionSystem.m_gems2.Count < UpgradeManager.UG.MaxGemCount)
      {
        for (int i = 0; i < UpgradeManager.UG.GemSpawnRate; i++)
        {
          var a = RandomHelper.Vector2(p0 + halfSpriteSize, p1 - halfSpriteSize);

          var type = RandomHelper.Int(0, 1000) == 0 ? GemTypes.LightGreen : GemTypes.Red;
          m_entityFactory.CreateGem(a, type);

          if (HarvesterCollectionSystem.m_gems2.Count >= UpgradeManager.UG.MaxGemCount)
            break;
        }

        if (Delivered == 0 && Collected == 0)
        {
          time += 1;
        }
        else
        {
          time += UpgradeManager.UG.GemSpawnCooldown;
        }
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
        ulong toDeliver = Math.Clamp((uint)(DeliveredUncounted * dt), 1, DeliveredUncounted);

        if (DeliveredUncounted > 100)
        // if (toDeliver > 10)
        {
          // if (_gemCountTween == null)
          // {
          //
          //   _gemCountTween = _tweener.TweenTo(target: this, expression: t => t.gemCountFontSize, toValue: 65f, duration: 0.15f)
          //                    .Easing(EasingFunctions.BounceInOut).AutoReverse();
          // }
          // else
          // {
          //   if (_gemCountTween.IsComplete)
          //   {
          //     _gemCountTween.CancelAndComplete();
          //     gemCountFontSize = 55f;
          //     _gemCountTween = _tweener.TweenTo(target: this, expression: t => t.gemCountFontSize, toValue: 65f, duration: 0.15f)
          //                      .Easing(EasingFunctions.BounceInOut).AutoReverse();
          //   }
          // }

          toDeliver = (ulong)(DeliveredUncounted * 0.8f);
        }

        var scale = new Vector2(0.001f, 0.001f) * toDeliver;

        gemCountFontSize += scale.X * 15.0f;

        if (EntityFactory.Instance.HomeBase != null)
        {
          EntityFactory.Instance.HomeBase.Entity.Get<Transform2>().Scale += scale;
          //Clamp scale for HomeBase
          EntityFactory.Instance.HomeBase.Entity.Get<Transform2>().Scale =
            Vector2.Clamp(EntityFactory.Instance.HomeBase.Entity.Get<Transform2>().Scale, new Vector2(1.0f, 1.0f), new Vector2(10.0f, 10.0f));
        }

        Delivered += toDeliver;
        DeliveredUncounted -= toDeliver;

        m_gameState.CurrentRedGemCount += (ulong)(toDeliver * (uint)UpgradeManager.UG.GemValue);

        gemCountFontSize = MathHelper.Clamp(gemCountFontSize, 55f, 100f);
        var diff = gemCountFontSize - 55f;
        gemCountFontSize = MathHelper.Lerp(gemCountFontSize, 55f, (float)gameTime.ElapsedGameTime.TotalSeconds * diff);
        // Console.WriteLine($"ToDeliver: {toDeliver}");

        m_upgradeManager.UpdateTooltipContent();
      }

      // m_camera.Zoom = UpgradeManager.UG.CameraZoomScale;
      // m_camera.Zoom = MathHelper.Lerp(m_camera.Zoom, UpgradeManager.UG.CameraZoomScale, (float)gameTime.ElapsedGameTime.TotalSeconds);
      //TODO: find better lerp or an easing function
      m_camera.Zoom = MathHelper.Lerp(m_camera.Zoom, UpgradeManager.UG.CameraZoomScale, (float)gameTime.ElapsedGameTime.TotalSeconds);
      // m_camera.Zoom = MathHelper.Lerp(m_camera.Zoom, 1.0f, (float)gameTime.ElapsedGameTime.TotalSeconds);

      m_escWorld.Update(gameTime);

      var curHarvesters = m_entityFactory.Harvesters.Count;
      if (curHarvesters < UpgradeManager.UG.HarvesterCount)
      {
        m_entityFactory.CreateHarvester(HomeBasePos + RandomHelper.Vector2(new Vector2(-25, -25), new Vector2(25, 25)));
        Console.WriteLine("Added harvester due to upgrade.");
      }
      else if (curHarvesters > UpgradeManager.UG.HarvesterCount)
      {
        m_entityFactory.RemoveRandomHarvester();
        Console.WriteLine("Removed excess harvester due to downgrade.");
      }

      if (!UpgradeManager.UpdatingButtons)
        _renderGuiSystem?.Update(gameTime);

      _tweener?.Update((float)gameTime.ElapsedGameTime.TotalSeconds);

      // Gum.Update(gameTime);
    }

    private bool showDebugGUI = false;

    public float gemCountFontSize { get; set; } = 55f;
    private readonly Tweener _tweener = new();
    private readonly Tweener _tweenerPreGame = new();
    private Tween? _gemCountTween;

    private void InitHudContent()
    {
      GameMain.AddCustomHudContent(() =>
      {
        if (!GameStarted)
          return;

        if (!UpgradeManager.UpdatingButtons)
          _renderGuiSystem?.Draw();


        var red = TextureCache.HudRedGem.Value;
        var blue = TextureCache.HudBlueGem.Value;

        m_spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
        m_spriteBatch.Draw(red, new Rectangle(10, 33, red.Bounds.Width, red.Bounds.Height), Color.White);
        m_spriteBatch.Draw(blue, new Rectangle(10, 110, blue.Bounds.Width, blue.Bounds.Height), Color.White);
        m_spriteBatch.End();

        ulong gemCount = m_gameState.CurrentRedGemCount;
        var s = NumberFormatter.AbbreviateBigNumber(gemCount);
        FontManager.RenderFieldFont(() => ContentDirectory.Fonts.Roboto_Regular_ttf, $"{s}", new Vector2(50, 20), Color.Yellow, Color.Black, gemCountFontSize);


        FontManager.RenderFieldFont(() => ContentDirectory.Fonts.Roboto_Regular_ttf, $"{m_gameState.CurrentBlueGemCount}", new Vector2(50, 90), Color.Yellow, Color.Black, 55f);


        FontManager.RenderFieldFont(() => ContentDirectory.Fonts.Roboto_Regular_ttf, $"{gemCount}", new Vector2(50, 150), Color.Yellow, Color.Black, gemCountFontSize);

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


          ImGui.SliderInt("GemValue", ref UpgradeManager.UG.GemValue, 0, 5000);


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

      var effect = EffectCache.BackgroundEffect.Value;

      var zoom = m_camera.Zoom;
      m_camera.Zoom = 0.5f * zoom;
      effect.Parameters["view_projection"]?.SetValue(m_camera.GetBoundingFrustum().Matrix);
      m_camera.Zoom = zoom;

      var bkg = TextureCache.SpaceBackground.Value;
      var bounds = new Rectangle(TextureCache.SpaceBackground.Value.Bounds.X, TextureCache.SpaceBackground.Value.Bounds.Y,
        TextureCache.SpaceBackground.Value.Bounds.Width * 5, TextureCache.SpaceBackground.Value.Bounds.Height * 5);
      Rectangle size = new Rectangle(-bkg.Width * 5, -bkg.Height * 5, bkg.Width * 10, bkg.Height * 10);

      m_spriteBatch.Begin(effect: effect, depthStencilState: DepthStencilState.Default, samplerState: SamplerState.AnisotropicWrap);
      m_spriteBatch.Draw(TextureCache.SpaceBackground, size, bounds,
          Color.White, 0, new Vector2(0, 0), SpriteEffects.None, 0);
      m_spriteBatch.Draw(TextureCache.SpaceBackground2, size, bounds,
          Color.White, 0, new Vector2(0, 0), SpriteEffects.None, 0);
      m_spriteBatch.Draw(TextureCache.SpaceBackground3, size, bounds,
          Color.White, 0, new Vector2(0, 0), SpriteEffects.None, 0);
      m_spriteBatch.End();

      m_escWorld.Draw(gameTime);

      var deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
      _frameCounter.Update(deltaTime);
    }
  }
}
