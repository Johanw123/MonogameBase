//Code for Controls/LobbyChatComponent (Container)
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
partial class LobbyChatComponentRuntime : ContainerRuntime
{
    [System.Runtime.CompilerServices.ModuleInitializer]
    public static void RegisterRuntimeType()
    {
        GumRuntime.ElementSaveExtensions.RegisterGueInstantiationType("Controls/LobbyChatComponent", typeof(LobbyChatComponentRuntime));
    }
    public ContainerRuntime obs { get; protected set; }
    public TextBoxRuntime TextBoxChatInput { get; protected set; }
    public ButtonStandardRuntime ButtonSendChat { get; protected set; }
    public TextBoxRuntime TextBoxChat { get; protected set; }

    public LobbyChatComponentRuntime(bool fullInstantiation = true, bool tryCreateFormsObject = true)
    {
        if(fullInstantiation)
        {
            var element = ObjectFinder.Self.GetElementSave("Controls/LobbyChatComponent");
            element?.SetGraphicalUiElement(this, global::RenderingLibrary.SystemManagers.Default);
        }



    }
    public override void AfterFullCreation()
    {
        obs = this.GetGraphicalUiElementByName("obs") as ContainerRuntime;
        TextBoxChatInput = this.GetGraphicalUiElementByName("TextBoxChatInput") as TextBoxRuntime;
        ButtonSendChat = this.GetGraphicalUiElementByName("ButtonSendChat") as ButtonStandardRuntime;
        TextBoxChat = this.GetGraphicalUiElementByName("TextBoxChat") as TextBoxRuntime;
        CustomInitialize();
    }
    //Not assigning variables because Object Instantiation Type is set to By Name rather than Fully In Code
    partial void CustomInitialize();
}
