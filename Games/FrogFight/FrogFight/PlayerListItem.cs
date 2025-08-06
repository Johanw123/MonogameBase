using NetworkShared;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrogFight
{
  public class PlayerListItem : INotifyPropertyChanged
  {
#pragma warning disable CS0067
    public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore CS0067

    public int PlayerId { get; set; }
    public string PlayerName { get; set; }

    public PlayerListItem()
    {

    }

    public PlayerListItem(PlayerStruct playerStruct)
    {
      PlayerId = playerStruct.PlayerId;
      PlayerName = playerStruct.PlayerName;
    }
  }
}
