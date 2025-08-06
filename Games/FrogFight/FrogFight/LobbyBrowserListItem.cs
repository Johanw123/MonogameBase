using NetworkShared;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrogFight
{
  public class LobbyBrowserListItem : INotifyPropertyChanged
  {
#pragma warning disable CS0067
    public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore CS0067

    //public int NumPlayerCount { get; set; }

    //public string Name { get; set; }
    //public string PlayerCountString => $"{NumPlayerCount} / 4";
    //public string LobbyId { get; set; }

    public LobbyBrowserListItem(LobbyStruct lobby)
    {
      LobbyInfo = new JoinedLobbyInfo(lobby);
      WasRefreshed = true;
    }

    //public void RefreshLobby(JoinedLobbyInfo lobby)
    //{
    //  LobbyInfo.LobbyName = lobby.LobbyName;
    //  LobbyInfo.ConnectedPlayers = lobby.ConnectedPlayers;

    //  WasRefreshed = true;
    //}

    public void RefreshLobby(LobbyStruct lobby)
    {
      LobbyInfo.LobbyName = lobby.Name;
      LobbyInfo.SetPlayers(lobby.ConnectedPlayers);

      WasRefreshed = true;
    }

    public JoinedLobbyInfo LobbyInfo { get; set; }

    public bool WasRefreshed { get; set; }
  }
}
