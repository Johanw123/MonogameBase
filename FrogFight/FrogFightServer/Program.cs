using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using LiteNetLib;
using LiteNetLib.Utils;
using NetworkShared;
using static Helpers;

namespace FrogFightServer
{
  public class Program
  {
    private static readonly NetPacketProcessor m_netPacketProcessor = new();
    private static readonly List<NetPeer> m_clients = [];
    private static NetManager? m_server;

    private static readonly LobbyHandler m_lobbyHandler = new(m_netPacketProcessor);

    public static async Task Main(string[] args)
    {
      Helpers.NetPacketProcessor = m_netPacketProcessor;

      EventBasedNetListener listener = new();
      m_server = new NetManager(listener);

      Console.WriteLine("Starting server on port: " + 9050);

      m_server.Start(9050);

      listener.ConnectionRequestEvent += request =>
      {
        if (m_server.ConnectedPeersCount < 100)
          request.AcceptIfKey("SomeConnectionKey");
        else
          request.Reject();
      };

      listener.PeerConnectedEvent += peer =>
      {
        Console.WriteLine("We got connection: {0}", peer.Address);

        m_clients.Add(peer);
        SendResponse(peer, new HelloResponse());
      };

      listener.PeerDisconnectedEvent += PeerDisconnected;

      listener.NetworkReceiveEvent += (peer, reader, channel, method) =>
      {
        //Console.WriteLine("We got: {0}", reader.GetString(100 /* max length of string */));
        //reader.Recycle();
        Console.WriteLine("received event");
        m_netPacketProcessor.ReadAllPackets(reader, peer);
        //NetDataWriter writer = new NetDataWriter();
        //writer.Put("Hello client!");
        //peer.Send(writer, DeliveryMethod.ReliableOrdered);
      };

      await ListenerLoop();
    }

    private static void PeerDisconnected(NetPeer peer, DisconnectInfo info)
    {
      Console.WriteLine($"Peer disconnected: {peer.Address} - {info.Reason}");
      m_clients.Remove(peer);

      m_lobbyHandler.RemovePeerFromLobbies(peer);
    }


    private static async Task ListenerLoop()
    {
      Console.WriteLine("Press ESC to stop");
      do
      {
        while (!Console.KeyAvailable)
        {
          m_server?.PollEvents();
          await Task.Delay(15);
        }
      } while (Console.ReadKey(true).Key != ConsoleKey.Escape);

      m_server?.Stop();
      Console.WriteLine("Server stopped...");
    }
  }
}
