using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LiteNetLib;

namespace FrogFightServer
{
  public class Lobby
  {
    public Guid Id { get; private set; }
    private readonly ConcurrentDictionary<int, NetPeer> m_clients = new();
    public NetPeer Owner { get; private set; }

    public int NumClients => m_clients.Count;
    public string LobbyName { get; set; }


    public Lobby(Guid guid, NetPeer owner, string lobbyName)
    {
      Id = guid;
      Owner = owner;
      LobbyName = lobbyName;

      m_clients.TryAdd(owner.Id, owner);
    }

    public bool ContainsPeer(NetPeer peer)
    {
      return m_clients.ContainsKey(peer.Id);
    }

    public bool AddPeer(NetPeer peer, bool owner = false)
    {
      if (owner)
      {
        Owner = peer;
      }

      /*return */
      m_clients.TryAdd(peer.Id, peer);
      return true;
    }

    public bool RemovePeer(NetPeer peer)
    {
      return m_clients.TryRemove(peer.Id, out _);
    }

    public ICollection<NetPeer> GetClients()
    {
      return m_clients.Values;
    }

    public List<NetPeer> FinishLobby()
    {
      var clients = m_clients.Values;
      m_clients.Clear();
      return clients.ToList();
    }
  }
}
