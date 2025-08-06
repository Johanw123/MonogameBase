using Gum.Mvvm;
using MonoGame.Extended.Collections;
using NetworkShared;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrogFight
{
  public class JoinedLobbyInfo : ViewModel
  {
    public ObservableCollection<PlayerListItem> ConnectedPlayers { get; set; } = new();

    public int NumPlayers => ConnectedPlayers.Count;

    public static event Action PlayerNumChanged;


    [DependsOn(nameof(NumPlayers))]
    public string DebugNumPlayers => $"{NumPlayers} / 4";


    public string LobbyName
    {
      get => Get<string>();
      set => Set(value);
    }

    public string LobbyGuid { get; set; }

    public JoinedLobbyInfo(LobbyStruct lobbyStruct)
    {
      LobbyGuid = lobbyStruct.Guid;
      LobbyName = lobbyStruct.Name;

      foreach (var player in lobbyStruct.ConnectedPlayers)
      {
        ConnectedPlayers.Add(new PlayerListItem { PlayerId = player.PlayerId, PlayerName = player.PlayerName });
      }

      RaiseNumPlayers();
    }

    private void RaiseNumPlayers()
    {
      //PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ConnectedPlayers)));
      //PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NumPlayers)));
      //PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DebugNumPlayers)));


      PlayerNumChanged?.Invoke();
    }

    public JoinedLobbyInfo()
    {

    }

    public void AddPlayer(PlayerListItem player)
    {
      var curPlayer = ConnectedPlayers.FirstOrDefault(x => x.PlayerId == player.PlayerId);
      if (curPlayer == null)
      {
        ConnectedPlayers.Add(player);
      }
      else
      {
        curPlayer.PlayerName = player.PlayerName;
      }

      RaiseNumPlayers();
    }

    public bool RemovePlayer(int playerId)
    {
      var player = ConnectedPlayers.FirstOrDefault(x => x.PlayerId == playerId);
      ConnectedPlayers.Remove(player);

      RaiseNumPlayers();
      return true;
    }

    public void SetPlayers(IEnumerable<PlayerStruct> players)
    {
      ConnectedPlayers.Clear();
      foreach (var player in players)
      {
        ConnectedPlayers.Add(new PlayerListItem(player));
      }

      RaiseNumPlayers();
    }

    public void Clear()
    {
      ConnectedPlayers.Clear();
      RaiseNumPlayers();
    }


    public bool IsValid { get; set; } = true;
    public void Invalidate()
    {
      IsValid = false;
    }
  }
}
