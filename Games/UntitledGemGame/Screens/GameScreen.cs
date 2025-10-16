using AsyncContent;
using Bloom_Sample;
using FontStashSharp;
using GUI.Shared.Helpers;
using Gum.Forms.DefaultVisuals;
using ImGuiNET;
using JapeFramework.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using MonoGame.Extended.ECS;
using MonoGame.Extended.Input;
using MonoGame.Extended.Screens;
using MonoGameGum;
using System;
using System.IO;
using System.Threading;
using Base;
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

    private World m_escWorld;
    private EntityFactory m_entityFactory;
    private OrthographicCamera m_camera;

    private FrameCounter _frameCounter = new FrameCounter();

    public static int Collected;
    public static int Delivered;
    public static int DeliveredUncounted;



    GumService Gum => GumService.Default;

    public UntitledGemGameGameScreen(Game game) : base(game)
    {
      game.IsMouseVisible = true;
    }

    public static Vector2 HomeBasePos;
    // private Texture2D spaceBackground;


    public override void LoadContent()
    {
      base.LoadContent();
      m_spriteBatch = new SpriteBatch(GraphicsDevice);

      m_camera = new OrthographicCamera(GraphicsDevice);

      m_escWorld = new WorldBuilder()
        .AddSystem(new HarvesterCollectionSystem(m_camera))
        .AddSystem(new UpdateSystem2())
        .AddSystem(new RenderGemSystem(m_spriteBatch, GraphicsDevice, m_camera))
        .AddSystem(new RenderSystem(m_spriteBatch, GraphicsDevice, m_camera))
        .Build();

      m_entityFactory = new EntityFactory(m_escWorld, GraphicsDevice);

      m_camera.Zoom = Upgrades.CameraZoomScale;

      HomeBasePos = m_camera.ScreenToWorld(new Vector2(GraphicsDevice.Viewport.Width / 2.0f, GraphicsDevice.Viewport.Height / 2.0f));
      m_entityFactory.CreateHomeBase(HomeBasePos);

      ImGuiContent();

      TextureCache.PreloadTextures();


      _renderTarget = new RenderTarget2D(GraphicsDevice, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height, true, BaseGame.SurfaceFormat, BaseGame.DepthFormat);



      // spaceBackground = AssetManager.Load<Texture2D>(ContentDirectory.Textures.SpaceBackground2_png);

      for (int i = 0; i < Upgrades.StartingGemCount; i++)
      {
        var a = m_camera.ScreenToWorld(RandomHelper.Vector2(new Vector2(50, 50), new Vector2(1900, 800)));
        m_entityFactory.CreateGem(a, GemTypes.Red);
      }
    }

    private int time = Upgrades.GemSpawnCooldown;

    public override void Update(GameTime gameTime)
    {
      var keyboardState = KeyboardExtended.GetState();

      time -= gameTime.ElapsedGameTime.Milliseconds;

      if (time < 0)
        time = 0;

      while (time <= 0 && HarvesterCollectionSystem.m_gems2.Count < Upgrades.MaxGemCount)
      {
        for (int i = 0; i < Upgrades.GemSpawnRate; i++)
        {
          var a = m_camera.ScreenToWorld(RandomHelper.Vector2(new Vector2(50, 50), new Vector2(1900, 800)));
          m_entityFactory.CreateGem(a, GemTypes.Red);
        }

        time += Upgrades.GemSpawnCooldown;
      }


      if (keyboardState.WasKeyPressed(Keys.B))
      {
        //var a = m_camera.ScreenToWorld(RandomHelper.Vector2(Vector2.Zero, new Vector2(1920, 900)));
        //m_entityFactory.CreateHarvester(a);

        ++Upgrades.HarvesterCount;
      }

      if (keyboardState.IsKeyDown(Keys.I))
      {
        //m_camera.ZoomIn(0.01f);

        Upgrades.CameraZoomScale += 0.01f;
      }

      if (keyboardState.IsKeyDown(Keys.O))
      {
        //m_camera.ZoomOut(0.01f);

        Upgrades.CameraZoomScale -= 0.01f;
      }

      //if (keyboardState.IsKeyDown(Keys.R))
      //{
      //}

      if (DeliveredUncounted > 0)
      {
        ++Delivered;
        --DeliveredUncounted;
      }

      m_camera.Zoom = Upgrades.CameraZoomScale;

      m_escWorld.Update(gameTime);

      var curHarvesters = m_entityFactory.Harvesters.Count;
      if (curHarvesters < Upgrades.HarvesterCount)
      {
        var a = m_camera.ScreenToWorld(RandomHelper.Vector2(Vector2.Zero, new Vector2(1920, 900)));
        m_entityFactory.CreateHarvester(a);
      }
      else if (curHarvesters > Upgrades.HarvesterCount)
      {
        m_entityFactory.RemoveRandomHarvester();
      }

      Gum.Update(gameTime);
    }

    private bool showDebugGUI = false;
    private RenderTarget2D _renderTarget;

    private void ImGuiContent()
    {
      GameMain.AddCustomImGuiContent(() =>
      {
        if (KeyboardExtended.GetState().WasKeyPressed(Keys.Tab))
        {
          showDebugGUI = !showDebugGUI;
        }

        if (showDebugGUI)
        {
          ImGuiNET.ImGui.SetNextWindowBgAlpha(1.0f);
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
          ImGui.SliderFloat("HarvesterSpeed", ref Upgrades.HarvesterSpeed, 0, 5000.0f);
          ImGui.SliderFloat("CameraZoomScale", ref Upgrades.CameraZoomScale, 0, 3.0f);


          ImGui.SliderInt("HarvesterCollectionRange", ref Upgrades.HarvesterCollectionRange, 0, 100);

          ImGui.SliderInt("HarvesterCapacity", ref Upgrades.HarvesterCapacity, 0, 5000);


          ImGui.SliderInt("MaxGemCount", ref Upgrades.MaxGemCount, 0, 500000);

          ImGui.SliderInt("HarvesterCount", ref Upgrades.HarvesterCount, 0, 25);
          ImGui.SliderInt("GemSpawnRate", ref Upgrades.GemSpawnRate, 0, 500);


          ImGui.SliderFloat("HarvesterMaximumFuel", ref Upgrades.HarvesterMaximumFuel, 0, 10000f);

          ImGui.SliderFloat("HarvesterRefuelSpeed", ref Upgrades.HarvesterRefuelSpeed, 1, 1000f);

          ImGui.Checkbox("RefuelAtHomebase", ref Upgrades.RefuelAtHomebase);
          ImGui.Checkbox("AutoRefuel", ref Upgrades.AutoRefuel);
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


        FontManager.RenderFieldFont(() => ContentDirectory.Fonts.Roboto_Regular_ttf, $"Picked Up: {Collected}", new Vector2(10, 70), Color.Yellow, Color.Black, 35);
        FontManager.RenderFieldFont(() => ContentDirectory.Fonts.Roboto_Regular_ttf, $"Delivered: {Delivered}", new Vector2(10, 100), Color.Yellow, Color.Black, 35);
        Gum.Draw();
      });
    }

    public override void Draw(GameTime gameTime)
    {


      //if (spaceBackground.IsLoaded)
      {
        // m_spriteBatch.Begin();
        // m_spriteBatch.Draw(spaceBackground, Vector2.Zero, Color.White);
        // m_spriteBatch.End();
      }

      //GraphicsDevice.SetRenderTarget(_renderTarget);

      m_escWorld.Draw(gameTime);

      //GraphicsDevice.SetRenderTarget(null);

      //Texture2D bloom = _bloomFilter.Draw(_renderTarget, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);

      //m_spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive);
      //m_spriteBatch.Draw(_renderTarget, new Rectangle(0, 0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height), Color.White);
      //m_spriteBatch.Draw(bloom, new Rectangle(0, 0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height), Color.White);
      //m_spriteBatch.End();


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
