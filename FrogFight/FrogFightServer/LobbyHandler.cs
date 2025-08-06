using System.Collections.Concurrent;
using LiteNetLib;
using LiteNetLib.Utils;
using NetworkShared;
using static Helpers;

namespace FrogFightServer
{

  public class LobbyHandler
  {
    private readonly NetPacketProcessor m_netPacketProcessor;
    private readonly ConcurrentDictionary<Guid, Lobby> m_lobbies = new();

    private readonly Dictionary<Guid, Lobby> m_lobbyDictionary = [];


    public LobbyHandler(NetPacketProcessor netPacketProcessor)
    {
      m_netPacketProcessor = netPacketProcessor;


      m_netPacketProcessor.RegisterNestedType<PlayerStruct>();
      m_netPacketProcessor.RegisterNestedType<LobbyStruct>();

      m_netPacketProcessor.SubscribeReusable<CreateLobbyRequest, NetPeer>(OnCreateLobbyRequest);
      m_netPacketProcessor.SubscribeReusable<JoinLobbyRequest, NetPeer>(OnJoinLobbyRequest);
      m_netPacketProcessor.SubscribeReusable<LeaveLobbyRequest, NetPeer>(OnleaveLobbyRequest);
      m_netPacketProcessor.SubscribeReusable<GetLobbiesRequest, NetPeer>(OnGetLobbiesRequest);
      m_netPacketProcessor.SubscribeReusable<SetNameRequest, NetPeer>(OnSetNameRequest);
      m_netPacketProcessor.SubscribeReusable<ChatMessageRequest, NetPeer>(OnLobbyChatMessageRequest);
    }

    public void RemovePeerFromLobbies(NetPeer peer)
    {
      foreach (var lobby in m_lobbies.Values)
      {
        bool wasRemoved = lobby.RemovePeer(peer);
        if (wasRemoved)
        {
          Console.Write($"Removing peer from lobby {lobby.LobbyName}({lobby.Id}): {peer.Address}");
          LobbyLeft(peer, lobby.Id);
        }
      }
    }

    //Request handling
    private void OnCreateLobbyRequest(CreateLobbyRequest request, NetPeer peer)
    {
      Console.WriteLine("OnCreateLobbyRequest::" + request + " - " + peer.Address);

      var isInLobby = m_lobbies.Any(x => x.Value.ContainsPeer(peer) || x.Value.Owner == peer);

      if (isInLobby)
      {
        SendResponse(peer, new CreateLobbyResponse { AllowedCreate = false }); //Add failure reason perhaps
        return;
      }

      var newGuid = Guid.NewGuid();

      var lobby = new Lobby(newGuid, peer, request.Name ?? "Null");

      m_lobbyDictionary.Add(newGuid, lobby);

      var success = m_lobbies.TryAdd(newGuid, lobby);

      var lobbyStruct = new LobbyStruct { Guid = newGuid.ToString(), LobbyOwnerToken = "token", Name = request.Name ?? "null" };

      SendResponse(peer,
        success
          ? new CreateLobbyResponse { AllowedCreate = true, Lobby = lobbyStruct }
          : new CreateLobbyResponse { AllowedCreate = false }); //Add failure reason perhaps
    }

    public void OnleaveLobbyRequest(LeaveLobbyRequest request, NetPeer peer)
    {
      Console.WriteLine("OnleaveLobbyRequest::" + request + " - " + peer.Address);

      LobbyLeft(peer, new Guid(request.LobbyGuid ?? "null"));
    }

    public void OnJoinLobbyRequest(JoinLobbyRequest request, NetPeer peer)
    {
      Console.WriteLine("OnJoinLobbyRequest::" + request + " - " + peer.Address);

      var guid = new Guid(request.LobbyGuid ?? "null");

      m_lobbyDictionary.TryGetValue(guid, out var lobby);
      if (lobby != null)
      {
        var success = lobby.AddPeer(peer);

        var clients = lobby.GetClients();
        var players = clients.Select(x => new PlayerStruct() { PlayerId = x.Id, PlayerName = x.Tag?.ToString() ?? "null" });

        var lobbyStruct = new LobbyStruct { Guid = request.LobbyGuid ?? "null", ConnectedPlayers = players.ToArray(), Name = lobby.LobbyName };

        //Add list of currently connected clients
        SendResponse(peer, new JoinedLobbyResponse { SucessfullyJoined = success, IsOwner = false, Lobby = lobbyStruct });

        // Broadcast player joining
        SendResponse(lobby.GetClients(), new LobbyClientJoinedResponse { LobbyId = lobby.Id.ToString(), PlayerName = peer.Tag.ToString(), PlayerId = peer.Id });
      }
    }

    public void OnGetLobbiesRequest(GetLobbiesRequest request, NetPeer peer)
    {
      var lobbies = new List<LobbyStruct>();

      foreach (var lobby in m_lobbies.Values)
      {
        var clients = lobby.GetClients();
        var players = clients.Select(x => new PlayerStruct() { PlayerId = x.Id, PlayerName = x.Tag?.ToString() ?? "null" });

        var lobbyStruct = new LobbyStruct { Guid = lobby.Id.ToString(), Name = lobby.LobbyName, ConnectedPlayers = players.ToArray() };
        lobbies.Add(lobbyStruct);
      }

      var response = new AvailableLobbiesResponse
      {
        Lobbies = lobbies.ToArray()
      };
      SendResponse(peer, response);
    }

    public void OnLobbyChatMessageRequest(ChatMessageRequest request, NetPeer peer)
    {
      var success = m_lobbies.TryGetValue(new Guid(request.LobbyGuid ?? "null"), out var lobby);

      if (success && lobby != null)
      {
        SendResponse(lobby.GetClients(), new ChatMessageResponse { PlayerId = peer.Id, PlayerName = peer.Tag.ToString(), Message = request.Message });
      }
    }

    public void OnSetNameRequest(SetNameRequest request, NetPeer peer)
    {
      //TODO: check name
      string acceptedName = request.Name ?? "null";

      peer.Tag = acceptedName;
      SendResponse(peer, new SetNameResponse { AcceptedName = acceptedName });
    }

    private void LobbyLeft(NetPeer peer, Guid lobbyId)
    {
      var foundLobby = m_lobbyDictionary.TryGetValue(lobbyId, out var lobby);

      if (lobby == null)
      {
        Console.WriteLine("LobbyLeft::Lobby was null here for some reason...");
        return;
      }

      if (foundLobby)
      {
        lobby.RemovePeer(peer);

        bool ownsIt = lobby.Owner == peer;
        Console.WriteLine($"LobbyLeft: {peer.Address} - {lobby.LobbyName}({lobby.Id}) - Owner: {ownsIt}");

        if (ownsIt)
        {
          // Close lobby and kick clients
          var success = m_lobbies.Remove(lobby.Id, out _);
          Console.WriteLine($"Remove Lobby: {lobby.LobbyName}({lobby.Id}) - Success: {success}");

          var clients = lobby.FinishLobby();
          SendResponse(clients, new LobbyClientKickedResponse { LobbyId = lobby.Id.ToString(), Reason = "Host has left the lobby" });
        }
        else
        {
          //Notify people in lobby that somebody left
          var clients = lobby.GetClients();

          SendResponse(clients, new LobbyClientLeftResponse() { LobbyId = lobby.Id.ToString(), PlayerId = peer.Id });
        }
      }
    }
  }
}
