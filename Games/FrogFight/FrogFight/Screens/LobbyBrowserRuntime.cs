using FrogFight;
using FrogFight.Menu.Viewmodels;
using Gum.Converters;
using Gum.DataTypes;
using Gum.Forms.Controls;
using Gum.Managers;
using Gum.Mvvm;
using Gum.Wireframe;
using LiteNetLib;
using LiteNetLib.Utils;
using MonoGame.Extended.Collections;
using MonoGameGum;
using MonoGameGum.GueDeriving;
using NetworkShared;
using RenderingLibrary.Graphics;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;

class WeaponDisplayingListBoxItem : ListBoxItem
{
  public WeaponDisplayingListBoxItem(InteractiveGue gue) : base(gue) { }
  public override void UpdateToObject(object o)
  {
    var idAsInt = (int)o;
    // assuming this has access to Weapons:

    coreText.RawText = "abo";
  }
}

partial class LobbyBrowserRuntime
{


  LobbyBrowserViewModel ViewModel;

  partial void CustomInitialize()
  {
    ViewModel = new LobbyBrowserViewModel();

    ButtonBack.FormsControl.Click += (_, _) =>
    {
      GumService.Default.Root.Children.Clear();

      var screen = GameMain.GumProject.GetScreenSave("MainMenu");
      var screenRuntime = screen.ToGraphicalUiElement();
      screenRuntime.AddToRoot();
    };

    ButtonCreateLobby.FormsControl.Click += (sender, args) =>
    {
      ViewModel.RequestCreateLobby();
    };

    ButtonRefreshLobbies.FormsControl.Click += (sender, args) =>
    {
      ViewModel.RequestLobbiesRefresh();
    };

    ButtonLeaveLobby.FormsControl.Click += (sender, args) =>
    {
      ViewModel.RequestLeaveLobby();
    };

    ButtonJoinLobby.FormsControl.Click += (sender, args) =>
    {
      if (ListBoxLobbies.FormsControl.SelectedObject is JoinedLobbyInfo selectedLobby)
      {
        ViewModel.SelectedLobby = selectedLobby;
        ViewModel.RequestJoinLobby();
      }
    };

    NotInLobby.SetBinding(
      nameof(NotInLobby.Visible),
      nameof(ViewModel.IsNotInLobby));

    InLobby.SetBinding(
      nameof(InLobby.Visible),
      nameof(ViewModel.IsInLobby));

    //TextLobbyNumPlayers.SetBinding(nameof(TextLobbyNumPlayers.Text), nameof(ViewModel.JoinedLobbyInfo.DebugNumPlayers));
    //TextLobbyName.SetBinding(nameof(TextLobbyName.Text), nameof(ViewModel.JoinedLobbyInfo.LobbyName));

    //ViewModel.JoinedLobbyInfo.PropertyChanged += (sender, args) =>
    //{
    //  Console.WriteLine("");
    //};

    JoinedLobbyInfo.PlayerNumChanged += () =>
    {
      TextLobbyName.Text = ViewModel.JoinedLobbyInfo.LobbyName;
      TextLobbyNumPlayers.Text = ViewModel.JoinedLobbyInfo.DebugNumPlayers;

      ListBoxConnectedPlayers.FormsControl.Items.Clear();

      foreach (var connectedPlayer in ViewModel.JoinedLobbyInfo.ConnectedPlayers)
      {
        ListBoxConnectedPlayers.FormsControl.Items.Add(connectedPlayer.PlayerName);
      }
    };

    ViewModel.LobbyList.CollectionChanged += HandleLobbyListChanged;


    // ListBoxLobbies.SetBinding(
    //   nameof(ListBoxLobbies.FormsControl.SelectedObject),
    //   nameof(ViewModel.SelectedLobby));
    //ViewModel.JoinedLobbyInfo.LobbyName.
    //
    //

    ListBoxLobbies.FormsControl.SelectionChanged += (sender, args) =>
    {
      if (ListBoxLobbies.FormsControl.SelectedObject is JoinedLobbyInfo selectedLobby)
      {
        ViewModel.SelectedLobby = selectedLobby;
      }
    };

    BindingContext = ViewModel;
  }

  private void HandleLobbyListChanged(object sender, NotifyCollectionChangedEventArgs e)
  {
    Console.WriteLine("LobbyList Changed");

    ListBoxLobbies.FormsControl.Items.Clear();

    foreach (var lobby in ViewModel.LobbyList)
    {
      ListBoxLobbies.FormsControl.Items.Add(lobby.LobbyInfo); ;
    }
  }
}
