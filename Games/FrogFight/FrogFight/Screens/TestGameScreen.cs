using AsepriteDotNet;
using Base;
using CppNet;
using FrogFight.Collisions;
using FrogFight.Components;
using FrogFight.Graphics;
using FrogFight.Network;
using FrogFight.Physics;
using FrogFight.Systems;
using GGPOSharp;
using MemoryPack;
using Microsoft.Toolkit.HighPerformance;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using MonoGame.Extended.ECS;
using MonoGame.Extended.Input;
using MonoGame.Extended.Screens;
using MonoGame.Extended.Tiled;
using MonoGame.Extended.Tiled.Renderers;
using nkast.Aether.Physics2D.Collision.Shapes;
using nkast.Aether.Physics2D.Dynamics;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Threading;
using AsyncContent;
using BodyType = nkast.Aether.Physics2D.Dynamics.BodyType;
using Color = System.Drawing.Color;
using Matrix3x2 = System.Numerics.Matrix3x2;
using OrthographicCamera = MonoGame.Extended.OrthographicCamera;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = System.Numerics.Vector3;
using Viewport = FrogFight.Graphics.Viewport;
using World = MonoGame.Extended.ECS.World;


namespace FrogFight.Scenes
{
  [Serializable]
  [MemoryPackable]
  public partial class GameState
  {
    public int frameCount = 0;
    public int NumPlayers = 2;
    public Player[] PlayerEntities = new Player[2];
  }

  public class TestScene : GameScreen, IGGPOSession
  {
    string localhost = "127.0.0.1";

    private int InputSize = 4;
    private int PlayerCount = 2;

    private GameState m_gameState = new();

    private TiledMap _map;
    private TiledMapRenderer _renderer;
    private EntityFactory _entityFactory;

    private Graphics.OrthographicCamera xna_camera = new();
    private World ecs_world;

    private nkast.Aether.Physics2D.Dynamics.World _world;
    private nkast.Aether.Physics2D.Dynamics.Body _playerBody;
    private nkast.Aether.Physics2D.Dynamics.Body _groundBody;

    private float _playerBodyRadius = 1.5f / 2f;
    private Vector2 _groundBodySize = new(500050f, 1f);

    private bool useNetwork = true;

    public override void Initialize()
    {
      base.Initialize();
      SocketHelper.SocketInit();
    }

    private Entity localPlayer;
    public static AssetCreator m_assetCreator;

    private void AddLocalPlayer(int playerNumber)
    {
      var playerInfo = GGPO.CreateLocalPlayer(playerNumber);
      m_ggpo.AddPlayer(ref playerInfo, out var handle);
      
      // var e = CreateEntity("LocalPlayer");
      // e.SetPosition(100, 100)
      // e.AddComponent(new LocalPlayer { PlayerNumber = playerNumber, NetworkHandle = handle, NetworkPlayerInfo = playerInfo });

      //ggpo_set_frame_delay(ggpo, handle, FRAME_DELAY);
      m_ggpo.SetFrameDelay(handle, 2);
      // m_gameState.PlayerEntities[0] = e;

      localPlayer = _entityFactory.CreatePlayer(new Vector2(playerNumber * _playerBodyRadius, 0), playerNumber, handle, playerInfo, true);
      m_gameState.PlayerEntities[playerNumber - 1] = localPlayer.Get<Player>();
    }

    private void AddRemotePlayer(int playerNumber, short remotePort)
    {
      var playerInfo = GGPO.CreateRemotePlayer(playerNumber, localhost, remotePort);
      m_ggpo.AddPlayer(ref playerInfo, out var handle);

      var entity = _entityFactory.CreatePlayer(new Vector2(playerNumber * 10, 0), playerNumber, handle, playerInfo, false);
      m_gameState.PlayerEntities[playerNumber - 1] = entity.Get<Player>();
    }

    public void CreateWorld()
    {
      _world = new nkast.Aether.Physics2D.Dynamics.World(new Vector2(0, -9.8f));

      Vector2 groundPosition = new Vector2(0, -(_groundBodySize.Y / 2f));

      // Create the ground fixture
      _groundBody = _world.CreateBody(groundPosition, 0, BodyType.Static);
      var gfixture = _groundBody.CreateRectangle(_groundBodySize.X, _groundBodySize.Y, 1f, Vector2.Zero);

      var tex = m_assetCreator.TextureFromShape(gfixture.Shape, MaterialType.Blank, Microsoft.Xna.Framework.Color.White, 1f);
      _groundBody.Tag = tex;

      gfixture.Restitution = 0.3f;
      gfixture.Friction = 0.5f;
    }

    private void InitGame(int localPort, short remotePort, bool syncTest = false, bool withNetwork = true)
    {
      useNetwork = withNetwork;

      if (withNetwork)
      {
        if (syncTest)
        {
          //syncTesting = true;
          var rtn = m_ggpo.StartSyncTest(CreateCallbacks(), "test", PlayerCount, InputSize, 1);
          Console.WriteLine("Started synctest session: " + rtn);
        }
        else
        {
          // syncTesting = false;
          var rtn = m_ggpo.StartSession(CreateCallbacks(), "test", PlayerCount, InputSize, localPort);
          Console.WriteLine("Started session: " + rtn);
        }

        m_ggpo.SetDisconnectTimeout(3000);
        m_ggpo.SetDisconnectNotifyStart(1000);

        if (localPort == 9000)
        {
          AddLocalPlayer(1);
          AddRemotePlayer(2, remotePort);
        }
        else
        {
          AddLocalPlayer(2);
          AddRemotePlayer(1, remotePort);
        }
      }
      else
      {
        localPlayer = _entityFactory.CreatePlayer(new Vector2(0, _playerBodyRadius), 1, 0, null, true);
        m_gameState.PlayerEntities[0] = localPlayer.Get<Player>();

        localPlayer = _entityFactory.CreatePlayer(new Vector2(3.0f, _playerBodyRadius), 2, 1, null, false);
        m_gameState.PlayerEntities[1] = localPlayer.Get<Player>();

        //localPlayer = _entityFactory.CreatePlayer(new Vector2(1000, 300), 3, 1, null, false);
      }


      //CreateWorld();
    }

    public override void Update(GameTime gameTime)
    {
      if (m_gameStarted)
      {
        if (useNetwork)
        {
          m_ggpo.Idle(1000);
          RunFrame(gameTime);
        }
        else
        {
          GlobalInputs = new byte[InputSize * PlayerCount];
          ecs_world.Update(BaseGame.Time);
        }

        _renderer?.Update(gameTime);
        //StepPhysics(gameTime);

        float totalSeconds = (float)gameTime.ElapsedGameTime.TotalSeconds;

        //We update the world
        //_world.Step((float)gameTime.ElapsedGameTime.TotalSeconds);

        _world.Step(1 / 60.0f);
      }

      var keyboardState = KeyboardExtended.GetState();

      var mouseState = MouseExtended.GetState();

      Console.WriteLine(mouseState.DeltaScrollWheelValue);

      if (mouseState.DeltaScrollWheelValue > 0)
      {
        xna_camera.ViewHeight += 10;
      }

      if (mouseState.DeltaScrollWheelValue < 0)
      {
        xna_camera.ViewHeight -= 10;
      }

      if (keyboardState.WasKeyPressed(Keys.A))
      {
        InitGame(9000, 9001);
      }
      else if (keyboardState.WasKeyPressed(Keys.B))
      {
        InitGame(9001, 9000);
      }
      else if (keyboardState.WasKeyPressed(Keys.C))
      {
        InitGame(9000, 9001, true);
      }
      else if (keyboardState.WasKeyPressed(Keys.D))
      {
        InitGame(9001, 9000, false, false);
        m_gameStarted = true;
      }
    }
    public readonly Mesh Mesh = new();
    public override void Draw(GameTime gameTime)
    {
      if (m_gameStarted)
      {
        ecs_world.Draw(gameTime);
      }
    }

    public void RunFrame(GameTime gameTime)
    {
      // byte i = JfwInput.Instance.IsKeyDown(Keys.G) ? (byte)1 : (byte)0;
      var keyboardState = KeyboardExtended.GetState();
      byte i = keyboardState.IsKeyDown(Keys.G) ? (byte)1 : (byte)0;


      byte[] input = new byte[InputSize];
      input[0] = i;

      //if (i == 1)
      //{
      //  Console.WriteLine($"Player: {localPlayer.Get<Player>().PlayerNumber} pressing button!");
      //}

      var lp = localPlayer.Get<Player>();

      if (lp != null)
      {
        var result = m_ggpo.AddLocalInput(lp.NetworkHandle, input);

        if (result == GGPO.ErrorCode.GGPO_ERRORCODE_SUCCESS)
        {
          var b = new byte[PlayerCount * InputSize];
          result = m_ggpo.SynchronizeInput(b, out var dcFlags);
          if (result == GGPO.ErrorCode.GGPO_ERRORCODE_SUCCESS)
          {
            AdvanceFrame(b, dcFlags, true);
          }
        }
      }
    }

    public void AdvanceFrame(byte[] inputs, int disconnectFlags, bool fromLocalAdvance)
    {
      UpdateState(inputs, disconnectFlags, fromLocalAdvance);

      m_ggpo.AdvanceFrame();
    }


    public static byte[] GlobalInputs;

    private void UpdateState(byte[] inputs, int disconnectFlags, bool fromLocalAdvance)
    {
      GlobalInputs = inputs;

      string s = "";

      foreach (var input in inputs)
      {
        s += input + ", ";
      }

      //Console.WriteLine($"fromLocalAdvance: {fromLocalAdvance} - {s}");

      ecs_world.Update(BaseGame.Time);
    }

    private SpriteBatch m_spriteBatch;

    public override void LoadContent()
    {
      m_spriteBatch = new SpriteBatch(GraphicsDevice);

      m_assetCreator = new AssetCreator(GraphicsDevice);
      m_assetCreator.LoadContent(Content);

      CreateWorld();

      ecs_world = new WorldBuilder()
        .AddSystem(new PlayerSystem())
        .AddSystem(new RenderSystem(m_spriteBatch, xna_camera, GraphicsDevice))
        .AddSystem(new DebugPhysicsRenderSystem(m_spriteBatch, xna_camera, _world, GraphicsDevice))
        .Build();

      _entityFactory = new EntityFactory(ecs_world, _world);

      xna_camera.FarPlane = 20;
      xna_camera.NearPlane = 0;
      xna_camera.Position = System.Numerics.Vector2.Zero.ToVector3(10);
      xna_camera.LookAt(System.Numerics.Vector2.Zero.ToVector3(0), Vector3.UnitY);
      xna_camera.Origin = OriginPosition.Center;
      xna_camera.ViewHeight = 250.0f;
    }

    public override void UnloadContent()
    {
      if (m_gcHandles != null)
      {
        foreach (var handle in m_gcHandles)
        {
          handle.Free();
        }

        m_gcHandles.Clear();
      }


      m_ggpo.CloseSession();
      SocketHelper.SocketFinish();
    }







    // ---------------------------------------------- GGPO specific code below ---------------------------------------------- //

    GGPO m_ggpo = new();

    private List<GCHandle> m_gcHandles;

    public IntPtr GetCallback(object o)
    {
      GCHandle handle = GCHandle.Alloc(o);
      IntPtr pCallback = Marshal.GetFunctionPointerForDelegate(o);

      m_gcHandles.Add(handle);

      return pCallback;
    }

    public GGPOSessionCallbacks CreateCallbacks()
    {
      m_gcHandles = new List<GCHandle>();

      var cbs = new GGPOSessionCallbacks
      {
        AdvanceFrame = GetCallback(new AdvanceFrame(AdvanceFrame)),
        BeginGame = GetCallback(new BeginGame(BeginGame)),
        FreeBuffer = GetCallback(new FreeBuffer(FreeBuffer)),
        LoadGameState = GetCallback(new LoadGameState(LoadGameState)),
        LogGameState = GetCallback(new LogGameState(LogGameState)),
        OnEvent = GetCallback(new OnEvent(OnEvent)),
        SaveGameState = GetCallback(new SaveGameState(SaveGameState))
      };

      return cbs;
    }

    public bool AdvanceFrame(int flags)
    {
      Console.WriteLine("Advance Frame: " + flags);

      //Will contain all players inputs
      var b = new byte[PlayerCount * InputSize];

      m_ggpo.SynchronizeInput(b, out var disconnectFlags);

      AdvanceFrame(b, disconnectFlags, false);

      return true;
    }

    private bool m_gameStarted = false;

    public bool BeginGame(string game)
    {
      Console.WriteLine("Begin Game!");

      m_gameStarted = true;

      return true;
    }

    public bool LoadGameState(IntPtr buffer, int len)
    {
      Console.WriteLine("LoadGameState");

      byte[] managedArray = new byte[len];
      Marshal.Copy(buffer, managedArray, 0, len);

      //memcpy(&gs, buffer, len);
      m_gameState = Binary.ByteArrayToObject<GameState>(managedArray);

      return true;
    }

    public bool LogGameState(string filename, byte[] buffer, int len)
    {
      Console.WriteLine("LogGameState");
      return true;
    }

    public bool OnEvent(ref GGPOEvent info)
    {
      switch (info.code)
      {
        case GGPO.EventCode.GGPO_EVENTCODE_CONNECTED_TO_PEER:
          break;
        case GGPO.EventCode.GGPO_EVENTCODE_SYNCHRONIZING_WITH_PEER:
          break;
        case GGPO.EventCode.GGPO_EVENTCODE_SYNCHRONIZED_WITH_PEER:
          break;
        case GGPO.EventCode.GGPO_EVENTCODE_RUNNING:
          break;
        case GGPO.EventCode.GGPO_EVENTCODE_DISCONNECTED_FROM_PEER:
          break;
        case GGPO.EventCode.GGPO_EVENTCODE_TIMESYNC:
          //Sleep(1000 * info->u.timesync.frames_ahead / 60);
          Thread.Sleep(1000 * info.timeSync.framesAhead / 60);
          break;
        case GGPO.EventCode.GGPO_EVENTCODE_CONNECTION_INTERRUPTED:
          break;
        case GGPO.EventCode.GGPO_EVENTCODE_CONNECTION_RESUMED:
          break;
      }

      Console.WriteLine("OnEvent: " + info.code);
      return true;
    }

    public void FreeBuffer(IntPtr buffer)
    {
      //Console.WriteLine("FreeBuffer: " + buffer);

      //States[buffer].Handle.Free();
      Marshal.FreeHGlobal(buffer);
      States.Remove(buffer);
    }

    private Dictionary<IntPtr, Info> States = new Dictionary<IntPtr, Info>();

    // private b2WorldId m_worldId;
    //private B2WorldId m_worldId;

    private class Info
    {
      public int frame;
      //public GCHandle Handle;
      public IntPtr Ptr;
      public int Checksum;
    }


    unsafe int
      fletcher32_checksum(short* data, int len)
    {
      int sum1 = 0xffff, sum2 = 0xffff;

      while (len != 0)
      {
        int tlen = len > 360 ? 360 : len;
        len -= tlen;
        do
        {
          sum1 += *data++;
          sum2 += sum1;
        } while (--tlen != 0);
        sum1 = (sum1 & 0xffff) + (sum1 >> 16);
        sum2 = (sum2 & 0xffff) + (sum2 >> 16);
      }

      /* Second reduction step to reduce sums to 16 bits */
      sum1 = (sum1 & 0xffff) + (sum1 >> 16);
      sum2 = (sum2 & 0xffff) + (sum2 >> 16);
      return sum2 << 16 | sum1;
    }

    // Convert an object to a byte array

    public bool SaveGameState(ref IntPtr buffer, ref int len, ref int checksum, int frame)
    {
      unsafe
      {
        var a = Binary.ObjectToByteArray(m_gameState);
        len = a.Length;
        IntPtr unmanagedPointer = Marshal.AllocHGlobal(a.Length);
        Marshal.Copy(a, 0, unmanagedPointer, a.Length);

        var cs = fletcher32_checksum((short*)unmanagedPointer, a.Length / 2);
        States.Add(unmanagedPointer, new Info { frame = frame, Ptr = unmanagedPointer, Checksum = cs });

        buffer = unmanagedPointer;
        checksum = cs;
      }

      return true;
    }

    public TestScene(Game game) : base(game)
    {
    }
  }
}
