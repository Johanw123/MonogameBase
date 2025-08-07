using Gum.Mvvm;
using LiteNetLib;
using LiteNetLib.Utils;
using MonoGameGum;
using NetworkShared;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrogFight.Menu.Viewmodels
{
  internal partial class LobbyBrowserViewModel : ViewModel
  {
    public Dictionary<string, LobbyBrowserListItem> LobbyDictionary { get; set; } = new();

    public ObservableCollection<LobbyBrowserListItem> LobbyList
    {
      get => Get<ObservableCollection<LobbyBrowserListItem>>();
      set => Set(value);
    }


    private readonly NetPacketProcessor m_netPacketProcessor = new();

    private static Random random;

    public JoinedLobbyInfo JoinedLobbyInfo
    {
      get => Get<JoinedLobbyInfo>();
      private set => Set(value);
    }

    private static string m_name = RandomString(10);
    private static string m_lobbyName = RandomString(10);

    public bool IsInLobby
    {
      get => Get<bool>();
      private set => Set(value);
    }

    [DependsOn(nameof(IsInLobby))] public bool IsNotInLobby => !IsInLobby;

    private EventBasedNetListener m_listener;
    private NetManager m_netManager;
    private NetPeer m_server;

    private bool m_initialized;
    private bool m_runningPollingThread;

    private DateTime m_lastRefreshTime;


    public string LobbyChatText { get; set; }
    public string LobbyChatInputText { get; set; }



    public LobbyBrowserViewModel()
    {
      random = new Random();

      JoinedLobbyInfo = new JoinedLobbyInfo() { IsValid = false };

      LobbyList = new ObservableCollection<LobbyBrowserListItem>();

      InitNetwork();
    }

    private void HandleLobbyListChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
      


    }

    public static string RandomString(int length)
    {
      random ??= new Random();

      const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
      return new string(Enumerable.Repeat(chars, length)
        .Select(s => s[random.Next(s.Length)]).ToArray());
    }

    private void InitNetwork()
    {
      if (!m_initialized)
      {
        m_listener = new EventBasedNetListener();
        m_netManager = new NetManager(m_listener);

        SetupResponses();

        m_listener.NetworkReceiveEvent += (peer, reader, deliveryMethod, channel) =>
        {
          m_netPacketProcessor.ReadAllPackets(reader, peer);
        };

        m_initialized = true;
      }

      ConnectServer();
    }

    private void SetupResponses()
    {
      m_netPacketProcessor.RegisterNestedType<PlayerStruct>();
      m_netPacketProcessor.RegisterNestedType<LobbyStruct>();

      m_netPacketProcessor.SubscribeReusable<HelloResponse, NetPeer>(OnHelloResponse);
      m_netPacketProcessor.SubscribeReusable<SetNameResponse, NetPeer>(OnSetNameResponse);

      m_netPacketProcessor.SubscribeReusable<AvailableLobbiesResponse, NetPeer>(OnAvailableLobbiesResponse);
      m_netPacketProcessor.SubscribeReusable<JoinedLobbyResponse, NetPeer>(OnJoinedLobbyResponse);
      m_netPacketProcessor.SubscribeReusable<CreateLobbyResponse, NetPeer>(OnCreateLobbyResponse);

      m_netPacketProcessor.SubscribeReusable<ChatMessageResponse, NetPeer>(OnLobbyChatMessageResponse);

      m_netPacketProcessor.SubscribeReusable<LobbyClientKickedResponse, NetPeer>(OnLobbyClientKicked);
      m_netPacketProcessor.SubscribeReusable<LobbyClientLeftResponse, NetPeer>(OnLobbyClientLeft);
      m_netPacketProcessor.SubscribeReusable<LobbyClientJoinedResponse, NetPeer>(OnLobbyClientJoined);
    }

    public void RequestCreateLobby()
    {
      string name = m_lobbyName;
      IsInLobby = true;
      var joinedLobby = new LobbyStruct { Name = name };
      JoinedLobbyInfo = new JoinedLobbyInfo(joinedLobby);
      JoinedLobbyInfo.AddPlayer(new PlayerListItem { PlayerName = m_name });

      SendRequest(new CreateLobbyRequest { Name = name });


      //GumService.Default.Root.Children.Clear();

      //var screen = GameMain.GumProject.GetScreenSave("Lobby");
      //var screenRuntime = screen.ToGraphicalUiElement();
      //screenRuntime.AddToRoot();
    }

    private void SendRequest<T>(T request) where T : class, new()
    {
      Console.WriteLine("Sending a request: " + request.GetType().ToString());
      NetDataWriter writer = new NetDataWriter();

      m_netPacketProcessor.Write(writer, request);
      m_server.Send(writer, DeliveryMethod.ReliableOrdered);
    }

    private void ConnectServer()
    {
      if (!m_netManager.IsRunning)
      {
        Console.WriteLine("Starting Net Manager...");
        var success = m_netManager.Start();
        Console.WriteLine("NetManager: " + success);
      }

      if (m_server is not { ConnectionState: ConnectionState.Connected })
      {
        Console.WriteLine("Connecting to server... ");
        m_server = m_netManager.Connect("localhost", 9050, "SomeConnectionKey");
        Console.WriteLine($"Connection: {m_server.Address}: {m_server.ConnectionState}");
      }

      if (!m_runningPollingThread)
      {
        m_runningPollingThread = true;
        Task.Factory.StartNew(PollEvents);
      }
    }

    private async Task PollEvents()
    {
      Console.WriteLine("Starting listener loop");

      while (m_runningPollingThread)
      {
        m_netManager.PollEvents();
        await Task.Delay(10);

        RequestLobbiesRefresh();

        await Task.Delay(10);
      }
    }

    public void RequestLobbiesRefresh()
    {
      var now = DateTime.Now;

      var diffInSeconds = (now - m_lastRefreshTime).TotalSeconds;

      if (diffInSeconds > 5 && !IsInLobby)
      {
        SendRequest(new GetLobbiesRequest());
        m_lastRefreshTime = DateTime.Now;
      }
    }

    private void OnLobbyChatMessageResponse(ChatMessageResponse response, NetPeer peer)
    {
      //  m_guiDispatcher.Invoke(() =>
      //{
      //TODO: skip own message and send own message instantly
      LobbyChatText += $"{response.PlayerName}: {response.Message}\n";
      //});

      Console.WriteLine($"Received: {response} - {response.PlayerName}: {response.Message} - {peer.Address}");
    }

    private void OnLobbyClientJoined(LobbyClientJoinedResponse response, NetPeer peer)
    {
      ////  m_guiDispatcher.Invoke(() =>
      //{
      JoinedLobbyInfo.AddPlayer(new PlayerListItem { PlayerId = response.PlayerId, PlayerName = response.PlayerName });
      // });

      Console.WriteLine($"Received: {response} - {response.PlayerName} - {peer.Address}");
    }

    private void OnSetNameResponse(SetNameResponse response, NetPeer peer)
    {
      var tag = peer.Tag;
      Console.WriteLine($"Received: {response} - {response.AcceptedName} - {peer.Address}");
    }

    private void OnLobbyClientLeft(LobbyClientLeftResponse response, NetPeer peer)
    {
      //m_guiDispatcher.Invoke(() =>
      // {
      JoinedLobbyInfo?.RemovePlayer(response.PlayerId);
      //});

      Console.WriteLine($"Received: {response} - {response.LobbyId} - {peer.Address}");
    }

    private void OnLobbyClientKicked(LobbyClientKickedResponse response, NetPeer peer)
    {
      //m_guiDispatcher.Invoke(() =>
      //{
      OnLobbyLeft();
      //});

      Console.WriteLine($"Received: {response} - {response.LobbyId} - {response.Reason} - {peer.Address}");
    }

    private void OnCreateLobbyResponse(CreateLobbyResponse response, NetPeer peer)
    {
      if (!response.AllowedCreate)
      {
        Console.WriteLine("Failed to create lobby");
        //m_guiDispatcher.Invoke(() =>
        //{
        OnLobbyLeft();
        //});

        return;
      }

      //m_guiDispatcher.Invoke(() =>
      // {
      JoinedLobbyInfo = new JoinedLobbyInfo(response.Lobby);
      JoinedLobbyInfo.AddPlayer(new PlayerListItem { PlayerId = -1, PlayerName = m_name });
      //});

      Console.WriteLine($"Received: {response} - {response.AllowedCreate} - {peer.Address}");
    }

    private void OnJoinedLobbyResponse(JoinedLobbyResponse response, NetPeer peer)
    {
      if (!response.SucessfullyJoined)
      {
        //m_guiDispatcher.Invoke(() =>
        //{
        OnLobbyLeft();
        //});

        Console.WriteLine("Failed to join lobby");
        return;
      }

      // m_guiDispatcher.Invoke(() =>
      //{
      JoinedLobbyInfo = new JoinedLobbyInfo(response.Lobby);
      //});

      Console.WriteLine($"Received: {response} - {peer.Address}");
    }

    private void OnAvailableLobbiesResponse(AvailableLobbiesResponse response, NetPeer peer)
    {
      Console.WriteLine($"Received: {response} - {peer.Address}");

      // m_guiDispatcher.Invoke(() =>
      //{
      foreach (var lobbylistitem in LobbyList)
      {
        lobbylistitem.WasRefreshed = false;
      }

      foreach (var lobby in response.Lobbies)
      {
        var found = LobbyDictionary.TryGetValue(lobby.Guid, out var lobbylistitem);

        if (found)
        {
          lobbylistitem.RefreshLobby(lobby);
        }
        else
        {
          var lli = new LobbyBrowserListItem(lobby);
          LobbyList.Add(lli);
          LobbyDictionary.Add(lobby.Guid, lli);
        }
      }

      foreach (var stalelobby in LobbyList.Where(x => !x.WasRefreshed).ToArray())
      {
        LobbyList.Remove(stalelobby);
        LobbyDictionary.Remove(stalelobby.LobbyInfo.LobbyGuid);
      }
      //});
    }

    private void OnLobbyLeft()
    {
      IsInLobby = false;
      JoinedLobbyInfo.Invalidate();
      LobbyChatText = "";

      //GumService.Default.Root.Children.Clear();

      //var screen = GameMain.GumProject.GetScreenSave("LobbyBrowser");
      //var screenRuntime = screen.ToGraphicalUiElement();
      //screenRuntime.AddToRoot();
    }

    private void OnHelloResponse(HelloResponse response, NetPeer peer)
    {
      Console.WriteLine($"Received: {response} - {peer.Address}");

      SendRequest(new SetNameRequest { Name = m_name });
    }

    public void RequestLeaveLobby()
    {
      if (!IsInLobby || JoinedLobbyInfo == null) return;

      var lobbyGuid = JoinedLobbyInfo.LobbyGuid;

      OnLobbyLeft();

      SendRequest(new LeaveLobbyRequest { LobbyGuid = lobbyGuid });
      SendRequest(new GetLobbiesRequest());
    }

    //public void RequestJoinLobby()
    //{
    //  if (SelectedLobbyItem == null) return;

    //  IsInLobby = true;

    //  JoinedLobbyInfo = new JoinedLobbyInfo
    //  {
    //    LobbyGuid = SelectedLobbyItem.LobbyInfo.LobbyGuid,
    //    LobbyName = SelectedLobbyItem.LobbyInfo.LobbyName,
    //    ConnectedPlayers = SelectedLobbyItem.LobbyInfo.ConnectedPlayers
    //  };

    //  SendRequest(new JoinLobbyRequest { LobbyGuid = SelectedLobbyItem.LobbyInfo.LobbyGuid });
    //}

  }
}
