// using System;
// using System.Collections;
// using System.Collections.Generic;
// using System.Data;
// using System.IO;
// using System.Linq;
// using System.Net.Sockets;
// using System.Runtime.InteropServices;
// using System.Runtime.Serialization.Formatters.Binary;
// using System.Text;
// using System.Threading;
// using System.Threading.Tasks;
// using DungeonRun.Entities;
// using DungeonRun.Network;
// using GGPOSharp;
// using JapeEngine.Core;
// using Microsoft.Xna.Framework;
// using Microsoft.Xna.Framework.Graphics;
// using Microsoft.Xna.Framework.Graphics.PackedVector;
// using Microsoft.Xna.Framework.Input;
// using Nez;
// using Nez.Sprites;
// using Nez.Textures;
// using static System.Collections.Specialized.BitVector32;
// using static System.Formats.Asn1.AsnWriter;
//
// namespace DungeonRun.Scenes
// {
//     [Serializable]
//   public class GameState
//   {
//     public int frameCount = 0;
//     public int NumPlayers = 2;
//     //public Entity[] PlayerEntities = new Entity[2];
//   }
//
//   public class TestScene : Scene, IGGPOSession
//   {
//     string localhost = "127.0.0.1";
//
//     private int InputSize = 4;
//     private int PlayerCount = 2;
//
//     private GameState m_gameState = new();
//
//     public override void Initialize()
//     {
//       base.Initialize();
//       SocketHelper.SocketInit();
//       
//       ClearColor = Color.DarkGray;
//     }
//
//     private void AddLocalPlayer(int playerNumber)
//     {
//       var playerInfo = GGPO.CreateLocalPlayer(playerNumber);
//       m_ggpo.AddPlayer(ref playerInfo, out var handle);
//       
//       var e = CreateEntity("LocalPlayer");
//        // e.SetPosition(100, 100)
//         e.AddComponent(new LocalPlayer{ PlayerNumber = playerNumber, NetworkHandle = handle, NetworkPlayerInfo = playerInfo });
//
//         //ggpo_set_frame_delay(ggpo, handle, FRAME_DELAY);
//         m_ggpo.SetFrameDelay(handle, 2);
//         // m_gameState.PlayerEntities[0] = e;
//     }
//
//     private void AddRemotePlayer(int playerNumber, short remotePort)
//     {
//       var playerInfo = GGPO.CreateRemotePlayer(playerNumber, localhost, remotePort);
//       m_ggpo.AddPlayer(ref playerInfo, out var handle);
//
//       var e = CreateEntity("RemotePlayer");
//        // e.SetPosition(Screen.Center)
//         e.AddComponent(new RemotePlayer { PlayerNumber = playerNumber, NetworkHandle = handle, NetworkPlayerInfo = playerInfo });
//
//     //  m_gameState.PlayerEntities[1] = e;
//     }
//
//     private void InitGame(int localPort, short remotePort, bool syncTest = false)
//     {
//       if (syncTest)
//       {
//         //syncTesting = true;
//         var rtn = m_ggpo.StartSyncTest(CreateCallbacks(), "test", PlayerCount, InputSize, 1);
//         Console.WriteLine("Started synctest session: " + rtn);
//       }
//       else
//       {
//        // syncTesting = false;
//         var rtn = m_ggpo.StartSession(CreateCallbacks(), "test", PlayerCount, InputSize, localPort);
//         Console.WriteLine("Started session: " + rtn);
//       }
//
//       m_ggpo.SetDisconnectTimeout(3000);
//       m_ggpo.SetDisconnectNotifyStart(1000);
//
//       if (localPort == 9000)
//       {
//         AddLocalPlayer(1);
//         AddRemotePlayer(2, remotePort);
//       }
//       else
//       {
//         AddLocalPlayer(2);
//         AddRemotePlayer(1, remotePort);
//       }
//     }
//
//     public override void Update()
//     {
//       base.Update();
//
//       if (m_gameStarted)
//       {
//         m_ggpo.Idle(1000);
//         RunFrame();
//       }
//
//       //if (JfwInput.Instance.WasKeyPressed(Keys.A))
//       //{
//       //  InitGame(9000, 9001);
//       //}
//       //else if (JfwInput.Instance.WasKeyPressed(Keys.B))
//       //{
//       //  InitGame(9001, 9000);
//       //}
//       //else if (JfwInput.Instance.WasKeyPressed(Keys.C))
//       //{
//       //  InitGame(9000, 9001, true);
//       //}
//     }
//
//     //private bool syncTesting = false;
//
//     //From local Update loop
//     public void RunFrame()
//     {
//       byte i = JfwInput.Instance.IsKeyDown(Keys.G) ? (byte)1 : (byte)0;
//
//       byte[] input = new byte[InputSize];
//       input[0] = i;
//
//       var localPlayer = FindComponentOfType<LocalPlayer>();
//
//       if (localPlayer != null)
//       {
//         var result = m_ggpo.AddLocalInput(localPlayer.NetworkHandle, input);
//
//         if (result == GGPO.ErrorCode.GGPO_ERRORCODE_SUCCESS)
//         {
//           var b = new byte[PlayerCount * InputSize];
//           result = m_ggpo.SynchronizeInput(b, out var dcFlags);
//           if (result == GGPO.ErrorCode.GGPO_ERRORCODE_SUCCESS)
//           {
//             AdvanceFrame(b, dcFlags);
//           }
//         }
//       }
//     }
//
//     public void AdvanceFrame(byte[] inputs, int disconnectFlags)
//     {
//       UpdateState(inputs, disconnectFlags);
//
//       m_ggpo.AdvanceFrame();
//     }
//
//     private void UpdateState(byte[] inputs, int disconnectFlags)
//     {
//       var players = FindComponentsOfType<IPlayer>().OrderBy(player => player.PlayerNumber).ToArray();
//       var chunks = inputs.Chunk(InputSize).ToArray();
//
//       for (var i = 0; i < players.Length; i++)
//       {
//         var player = players[i];
//
//         //TODO check disconnects
//         player.Update(chunks[i]);
//       }
//     }
//
//     public override void Unload()
//     {
//       foreach (var handle in m_gcHandles)
//       {
//         handle.Free();
//       }
//
//       m_gcHandles.Clear();
//
//       m_ggpo.CloseSession();
//       SocketHelper.SocketFinish();
//     }
//
//
//
//
//
//     // ---------------------------------------------- GGPO specific code below ---------------------------------------------- //
//
//     GGPO m_ggpo = new();
//
//     private List<GCHandle> m_gcHandles;
//
//     public IntPtr GetCallback(object o)
//     {
//       GCHandle handle = GCHandle.Alloc(o);
//       IntPtr pCallback = Marshal.GetFunctionPointerForDelegate(o);
//
//       m_gcHandles.Add(handle);
//
//       return pCallback;
//     }
//
//     public GGPOSessionCallbacks CreateCallbacks()
//     {
//       m_gcHandles = new List<GCHandle>();
//
//       var cbs = new GGPOSessionCallbacks
//       {
//         AdvanceFrame = GetCallback(new AdvanceFrame(AdvanceFrame)),
//         BeginGame = GetCallback(new BeginGame(BeginGame)),
//         FreeBuffer = GetCallback(new FreeBuffer(FreeBuffer)),
//         LoadGameState = GetCallback(new LoadGameState(LoadGameState)),
//         LogGameState = GetCallback(new LogGameState(LogGameState)),
//         OnEvent = GetCallback(new OnEvent(OnEvent)),
//         SaveGameState = GetCallback(new SaveGameState(SaveGameState))
//       };
//
//       return cbs;
//     }
//
//     public bool AdvanceFrame(int flags)
//     {
//       Console.WriteLine("Advance Frame: " + flags);
//
//       //Will contain all players inputs
//       var b = new byte[PlayerCount * InputSize];
//
//       m_ggpo.SynchronizeInput(b, out var disconnectFlags);
//       AdvanceFrame(b, disconnectFlags);
//
//       return true;
//     }
//
//     private bool m_gameStarted = false;
//
//     public bool BeginGame(string game)
//     {
//       Console.WriteLine("Begin Game!");
//
//       m_gameStarted = true;
//
//       return true;
//     }
//
//     public bool LoadGameState(IntPtr buffer, int len)
//     {
//       Console.WriteLine("LoadGameState");
//
//       byte[] managedArray = new byte[len];
//       Marshal.Copy(buffer, managedArray, 0, len);
//
//       //memcpy(&gs, buffer, len);
//       m_gameState = Binary.ByteArrayToObject<GameState>(managedArray);
//
//       return true;
//     }
//
//     public bool LogGameState(string filename, byte[] buffer, int len)
//     {
//       Console.WriteLine("LogGameState");
//       return true;
//     }
//
//     public bool OnEvent(ref GGPOEvent info)
//     {
//       switch (info.code)
//       {
//         case GGPO.EventCode.GGPO_EVENTCODE_CONNECTED_TO_PEER:
//           break;
//         case GGPO.EventCode.GGPO_EVENTCODE_SYNCHRONIZING_WITH_PEER:
//           break;
//         case GGPO.EventCode.GGPO_EVENTCODE_SYNCHRONIZED_WITH_PEER:
//           break;
//         case GGPO.EventCode.GGPO_EVENTCODE_RUNNING:
//           break;
//         case GGPO.EventCode.GGPO_EVENTCODE_DISCONNECTED_FROM_PEER:
//           break;
//         case GGPO.EventCode.GGPO_EVENTCODE_TIMESYNC:
//           //Sleep(1000 * info->u.timesync.frames_ahead / 60);
//           Thread.Sleep(1000 * info.timeSync.framesAhead / 60);
//           break;
//         case GGPO.EventCode.GGPO_EVENTCODE_CONNECTION_INTERRUPTED:
//           break;
//         case GGPO.EventCode.GGPO_EVENTCODE_CONNECTION_RESUMED:
//           break;
//       }
//
//       Console.WriteLine("OnEvent: " + info.code);
//       return true;
//     }
//
//     public void FreeBuffer(IntPtr buffer)
//     {
//       Console.WriteLine("FreeBuffer: " + buffer);
//
//       //States[buffer].Handle.Free();
//       Marshal.FreeHGlobal(buffer);
//       States.Remove(buffer);
//     }
//
//     private Dictionary<IntPtr, Info> States = new Dictionary<IntPtr, Info>();
//
//     private class Info
//     {
//       public int frame;
//       //public GCHandle Handle;
//       public IntPtr Ptr;
//       public int Checksum;
//     }
//
//
//     unsafe int
//       fletcher32_checksum(short* data, int len)
//     {
//       int sum1 = 0xffff, sum2 = 0xffff;
//
//       while (len != 0)
//       {
//         int tlen = len > 360 ? 360 : len;
//         len -= tlen;
//         do
//         {
//           sum1 += *data++;
//           sum2 += sum1;
//         } while (--tlen != 0);
//         sum1 = (sum1 & 0xffff) + (sum1 >> 16);
//         sum2 = (sum2 & 0xffff) + (sum2 >> 16);
//       }
//
//       /* Second reduction step to reduce sums to 16 bits */
//       sum1 = (sum1 & 0xffff) + (sum1 >> 16);
//       sum2 = (sum2 & 0xffff) + (sum2 >> 16);
//       return sum2 << 16 | sum1;
//     }
//
//     // Convert an object to a byte array
//
//     public bool SaveGameState(ref IntPtr buffer, ref int len, ref int checksum, int frame)
//     {
//       //Console.WriteLine("SaveGameState: " + frame);
//
//       unsafe
//       {
//         var a = Binary.ObjectToByteArray(m_gameState);
//         len = a.Length;
//         IntPtr unmanagedPointer = Marshal.AllocHGlobal(a.Length);
//         Marshal.Copy(a, 0, unmanagedPointer, a.Length);
//
//         var cs = fletcher32_checksum((short*)unmanagedPointer, a.Length / 2);
//         States.Add(unmanagedPointer, new Info(){ frame = frame, Ptr = unmanagedPointer, Checksum =  cs});
//
//         buffer = unmanagedPointer;
//         checksum = cs;
//       }
//
//       return true;
//     }
//   }
// }
