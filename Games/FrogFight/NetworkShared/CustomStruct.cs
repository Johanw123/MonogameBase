using LiteNetLib.Utils;

namespace NetworkShared
{
  public enum ClientRequestTypes
  {
    Null = 0,
    RequestName,
    CreateLobby,
    //DestroyLobby,
    GetLobbies,
    JoinLobby,
    LeaveLobby,

    ChatMessage,
    StartGame,


  }

  public enum ServerResponseTypes
  {
    Null = 0,
    Hello,
    SetName,

    AvailableLobbies,
    LobbyJoined,
    CreateLobby,

    ChatMessage,

    LobbyClientKicked,
    LobbyClientLeft,
    LobbyClientJoined,

    //Update/Refresh/Ping lobby status
  }

  public struct LobbyStruct : INetSerializable
  {
    public PlayerStruct[] ConnectedPlayers { get; set; } = [];

    public string Name = "";
    public string Guid = "";
    public string LobbyOwnerToken = "";

    public LobbyStruct()
    {

    }

    public void Serialize(NetDataWriter writer)
    {
      writer.Put(Name);
      writer.Put(Guid);
      writer.Put(LobbyOwnerToken);

      if (ConnectedPlayers == null || !ConnectedPlayers.Any())
      {
        writer.Put(0);
      }
      else
      {
        writer.Put(ConnectedPlayers.Length);
        foreach (var player in ConnectedPlayers)
        {
          writer.Put(player);
        }
      }
    }

    public void Deserialize(NetDataReader reader)
    {
      Name = reader.GetString();
      Guid = reader.GetString();
      LobbyOwnerToken = reader.GetString();

      int numPlayers = reader.GetInt();
      ConnectedPlayers = new PlayerStruct[numPlayers];

      for (int i = 0; i < numPlayers; i++)
      {
        // ConnectedPlayers.Add(reader.Get<PlayerStruct>());
        ConnectedPlayers[i] = reader.Get<PlayerStruct>();
      }
    }
  }

  public struct PlayerStruct : INetSerializable
  {
    public string PlayerName;
    public int PlayerId;
    public int LobbyPosition;

    public void Serialize(NetDataWriter writer)
    {
      writer.Put(PlayerName);
      writer.Put(PlayerId);
      writer.Put(LobbyPosition);
    }

    public void Deserialize(NetDataReader reader)
    {
      PlayerName = reader.GetString();
      PlayerId = reader.GetInt();
      LobbyPosition = reader.GetInt();
    }
  }

  public abstract class ServerResponseBase
  {
    public ServerResponseTypes ResponseType { get; set; }

    protected ServerResponseBase(ServerResponseTypes responseType)
    {
      ResponseType = responseType;

      Console.WriteLine("Creating a reponse: " + ResponseType);
    }
  }

  public class HelloResponse : ServerResponseBase
  {
    public HelloResponse() : base(ServerResponseTypes.Hello)
    {

    }
  }

  public class SetNameResponse : ServerResponseBase
  {
    public string AcceptedName { get; set; } = "";
    public SetNameResponse() : base(ServerResponseTypes.SetName)
    {

    }
  }

  public class AvailableLobbiesResponse : ServerResponseBase
  {
    public LobbyStruct[] Lobbies { get; set; }
    public int LobbyCount { get; set; }
    public AvailableLobbiesResponse() : base(ServerResponseTypes.AvailableLobbies)
    {

    }
  }

  public class JoinedLobbyResponse : ServerResponseBase
  {
    public bool SucessfullyJoined { get; set; }
    public bool IsOwner { get; set; }
    public string OwnerToken { get; set; }

    public LobbyStruct Lobby { get; set; }

    public JoinedLobbyResponse() : base(ServerResponseTypes.LobbyJoined)
    {

    }
  }

  public class CreateLobbyResponse : ServerResponseBase
  {
    public bool AllowedCreate { get; set; }
    public LobbyStruct Lobby { get; set; }

    public CreateLobbyResponse() : base(ServerResponseTypes.CreateLobby)
    {

    }
  }

  public class LobbyClientKickedResponse : ServerResponseBase
  {
    public string Reason { get; set; }
    public string LobbyId { get; set; }
    public LobbyClientKickedResponse() : base(ServerResponseTypes.LobbyClientKicked)
    {

    }
  }

  public class LobbyClientLeftResponse : ServerResponseBase
  {
    public string LobbyId { get; set; }
    public int PlayerId { get; set; }
    public LobbyClientLeftResponse() : base(ServerResponseTypes.LobbyClientLeft)
    {

    }
  }

  public class LobbyClientJoinedResponse : ServerResponseBase
  {
    public string LobbyId { get; set; }
    public int PlayerId { get; set; }
    public string PlayerName { get; set; }
    public LobbyClientJoinedResponse() : base(ServerResponseTypes.LobbyClientJoined)
    {

    }
  }

  public class ChatMessageResponse : ServerResponseBase
  {
    public string Message { get; set; }
    public int PlayerId { get; set; }
    public string PlayerName { get; set; }
    public ChatMessageResponse() : base(ServerResponseTypes.ChatMessage)
    {

    }
  }


  // CLIENT REQUESTS

  public abstract class ClientRequestBase
  {
    public ClientRequestTypes RequestType { get; set; }

    protected ClientRequestBase(ClientRequestTypes requestType)
    {
      RequestType = requestType;

      Console.WriteLine("Creating a request: " + RequestType);
    }
  }

  public class SetNameRequest : ClientRequestBase
  {
    public string Name { get; set; }
    public SetNameRequest() : base(ClientRequestTypes.RequestName)
    {

    }
  }



  public class CreateLobbyRequest : ClientRequestBase
  {
    public string Name { get; set; }
    public CreateLobbyRequest() : base(ClientRequestTypes.CreateLobby)
    {
    }
  }

  public class LeaveLobbyRequest : ClientRequestBase
  {
    public string LobbyGuid { get; set; }
    public LeaveLobbyRequest() : base(ClientRequestTypes.LeaveLobby)
    {
    }
  }

  public class JoinLobbyRequest : ClientRequestBase
  {
    public string LobbyGuid { get; set; }
    public JoinLobbyRequest() : base(ClientRequestTypes.JoinLobby)
    {
    }
  }

  public class GetLobbiesRequest : ClientRequestBase
  {
    public GetLobbiesRequest() : base(ClientRequestTypes.GetLobbies)
    {
    }
  }

  public class ChatMessageRequest : ClientRequestBase
  {
    public string LobbyGuid { get; set; }
    public string Message { get; set; }
    public ChatMessageRequest() : base(ClientRequestTypes.ChatMessage)
    {
    }
  }
}
