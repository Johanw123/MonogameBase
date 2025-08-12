using FrogFight.Components;
using FrogFight.Entities;
using FrogFight.Network;
using FrogFight.Systems;
using GGPOSharp;
using MemoryPack;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Graphics.PackedVector;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using MonoGame.Extended.ECS;
using MonoGame.Extended.Input;
using MonoGame.Extended.Screens;
using MonoGame.Extended.Tiled;
using MonoGame.Extended.Tiled.Renderers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Base;
using static System.Collections.Specialized.BitVector32;
using static System.Formats.Asn1.AsnWriter;


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
    private OrthographicCamera _camera;
    private World _world;


    public override void Initialize()
    {
      base.Initialize();
      SocketHelper.SocketInit();

      _camera = new OrthographicCamera(GraphicsDevice);
    }

    private Entity localPlayer;

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

      localPlayer = _entityFactory.CreatePlayer(new Vector2(playerNumber * 100, 0), playerNumber, handle, playerInfo, true);
      m_gameState.PlayerEntities[0] = localPlayer.Get<Player>();
    }

    private void AddRemotePlayer(int playerNumber, short remotePort)
    {
      var playerInfo = GGPO.CreateRemotePlayer(playerNumber, localhost, remotePort);
      m_ggpo.AddPlayer(ref playerInfo, out var handle);

      var entity = _entityFactory.CreatePlayer(new Vector2(playerNumber * 100, 0), playerNumber, handle, playerInfo, false);
      m_gameState.PlayerEntities[1] = entity.Get<Player>();
    }

    private void InitGame(int localPort, short remotePort, bool syncTest = false)
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

    public override void Update(GameTime gameTime)
    {
      // base.Update(gameTime);

      if (m_gameStarted)
      {
        m_ggpo.Idle(1000);
        RunFrame(gameTime);

        _renderer?.Update(gameTime);
      }

      var keyboardState = KeyboardExtended.GetState();

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
    }

    public override void Draw(GameTime gameTime)
    {
      //_renderer.Draw(_camera.GetViewMatrix());

      _world.Draw(gameTime);
    }

    //private bool syncTesting = false;

    //From local Update loop
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
      // var players = FindComponentsOfType<IPlayer>().OrderBy(player => player.PlayerNumber).ToArray();
      // var chunks = inputs.Chunk(InputSize).ToArray();
      //
      // for (var i = 0; i < players.Length; i++)
      // {
      //   var player = players[i];
      //
      //   //TODO check disconnects
      //   player.Update(chunks[i]);
      // }
      GlobalInputs = inputs;

      string s = "";

      foreach (var input in inputs)
      {
        s += input + ", ";
      }

      //Console.WriteLine($"fromLocalAdvance: {fromLocalAdvance} - {s}");

      _world.Update(BaseGame.Time);
    }

    public static Thread ContentThread;

    public override void LoadContent()
    {
      _world = new WorldBuilder()
        .AddSystem(new WorldSystem())
        .AddSystem(new PlayerSystem())
        //.AddSystem(new EnemySystem())
        .AddSystem(new RenderSystem(new SpriteBatch(GraphicsDevice), _camera))
        .Build();

      //Game.Components.Add(_world);
      _entityFactory = new EntityFactory(_world, Content);

      var id = Thread.CurrentThread.ManagedThreadId;
      var name = Thread.CurrentThread.Name;

      ContentThread = Thread.CurrentThread;
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
