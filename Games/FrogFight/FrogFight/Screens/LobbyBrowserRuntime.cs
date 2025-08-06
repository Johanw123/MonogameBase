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
    };

    //ViewModel.JoinedLobbyInfo.LobbyName.

    BindingContext = ViewModel;
  }
}
