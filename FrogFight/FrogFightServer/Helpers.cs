using LiteNetLib;
using LiteNetLib.Utils;

public static class Helpers
{
  public static NetPacketProcessor NetPacketProcessor;

  public static void SendResponse<T>(NetPeer peer, T response) where T : class, new()
  {
    Console.WriteLine("Sending response: " + response);

    NetDataWriter writer = new NetDataWriter();

    NetPacketProcessor.Write(writer, response);
    peer.Send(writer, DeliveryMethod.ReliableOrdered);
  }

  public static void SendResponse<T>(IEnumerable<NetPeer> peers, T response) where T : class, new()
  {
    Console.WriteLine($"broadcasting: " + response);

    NetDataWriter writer = new NetDataWriter();

    NetPacketProcessor.Write(writer, response);
    foreach (var peer in peers)
    {
      peer.Send(writer, DeliveryMethod.ReliableOrdered);
    }
  }
}
