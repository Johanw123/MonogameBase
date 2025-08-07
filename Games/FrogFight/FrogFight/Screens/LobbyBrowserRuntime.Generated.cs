//Code for LobbyBrowser
using GumRuntime;
using MonoGameGum;
using MonoGameGum.GueDeriving;
using Gum.Converters;
using Gum.DataTypes;
using Gum.Managers;
using Gum.Wireframe;

using RenderingLibrary.Graphics;

using System.Linq;

using MonoGameGum.GueDeriving;
partial class LobbyBrowserRuntime : Gum.Wireframe.BindableGue
{
    [System.Runtime.CompilerServices.ModuleInitializer]
    public static void RegisterRuntimeType()
    {
        GumRuntime.ElementSaveExtensions.RegisterGueInstantiationType("LobbyBrowser", typeof(LobbyBrowserRuntime));
    }
    public ContainerRuntime Container { get; protected set; }
    public ButtonStandardRuntime ButtonJoinLobby { get; protected set; }
    public ButtonStandardRuntime ButtonCreateLobby { get; protected set; }
    public ButtonStandardRuntime ButtonRefreshLobbies { get; protected set; }
    public ButtonStandardRuntime ButtonBack { get; protected set; }
    public ButtonStandardRuntime ButtonLeaveLobby { get; protected set; }
    public TextRuntime TextLobbyNumPlayers { get; protected set; }
    public ListBoxRuntime ListBoxLobbies { get; protected set; }
    public ContainerRuntime NotInLobby { get; protected set; }
    public ContainerRuntime InLobby { get; protected set; }
    public ContainerRuntime obs { get; protected set; }
    public ListBoxRuntime ListBoxConnectedPlayers { get; protected set; }
    public TextRuntime TextLobbyName { get; protected set; }
    public LobbyChatComponentRuntime LobbyChatComponentInstance { get; protected set; }

    public LobbyBrowserRuntime(bool fullInstantiation = true, bool tryCreateFormsObject = true)
    {
        if(fullInstantiation)
        {
            var element = ObjectFinder.Self.GetElementSave("LobbyBrowser");
            element?.SetGraphicalUiElement(this, global::RenderingLibrary.SystemManagers.Default);
        }



    }
    public override void AfterFullCreation()
    {
        Container = this.GetGraphicalUiElementByName("Container") as ContainerRuntime;
        ButtonJoinLobby = this.GetGraphicalUiElementByName("ButtonJoinLobby") as ButtonStandardRuntime;
        ButtonCreateLobby = this.GetGraphicalUiElementByName("ButtonCreateLobby") as ButtonStandardRuntime;
        ButtonRefreshLobbies = this.GetGraphicalUiElementByName("ButtonRefreshLobbies") as ButtonStandardRuntime;
        ButtonBack = this.GetGraphicalUiElementByName("ButtonBack") as ButtonStandardRuntime;
        ButtonLeaveLobby = this.GetGraphicalUiElementByName("ButtonLeaveLobby") as ButtonStandardRuntime;
        TextLobbyNumPlayers = this.GetGraphicalUiElementByName("TextLobbyNumPlayers") as TextRuntime;
        ListBoxLobbies = this.GetGraphicalUiElementByName("ListBoxLobbies") as ListBoxRuntime;
        NotInLobby = this.GetGraphicalUiElementByName("NotInLobby") as ContainerRuntime;
        InLobby = this.GetGraphicalUiElementByName("InLobby") as ContainerRuntime;
        obs = this.GetGraphicalUiElementByName("obs") as ContainerRuntime;
        ListBoxConnectedPlayers = this.GetGraphicalUiElementByName("ListBoxConnectedPlayers") as ListBoxRuntime;
        TextLobbyName = this.GetGraphicalUiElementByName("TextLobbyName") as TextRuntime;
        LobbyChatComponentInstance = this.GetGraphicalUiElementByName("LobbyChatComponentInstance") as LobbyChatComponentRuntime;
        CustomInitialize();
    }
    //Not assigning variables because Object Instantiation Type is set to By Name rather than Fully In Code
    partial void CustomInitialize();
}
