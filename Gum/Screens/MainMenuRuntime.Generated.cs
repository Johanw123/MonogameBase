//Code for MainMenu
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
partial class MainMenuRuntime : Gum.Wireframe.BindableGue
{
    [System.Runtime.CompilerServices.ModuleInitializer]
    public static void RegisterRuntimeType()
    {
        GumRuntime.ElementSaveExtensions.RegisterGueInstantiationType("MainMenu", typeof(MainMenuRuntime));
    }
    public ButtonStandardRuntime ButtonPlay { get; protected set; }
    public ButtonStandardRuntime ButtonOptions { get; protected set; }
    public ContainerRuntime Container { get; protected set; }
    public ButtonStandardRuntime ButtonExit { get; protected set; }

    public MainMenuRuntime(bool fullInstantiation = true, bool tryCreateFormsObject = true)
    {
        if(fullInstantiation)
        {
            var element = ObjectFinder.Self.GetElementSave("MainMenu");
            element?.SetGraphicalUiElement(this, global::RenderingLibrary.SystemManagers.Default);
        }



    }
    public override void AfterFullCreation()
    {
        ButtonPlay = this.GetGraphicalUiElementByName("ButtonPlay") as ButtonStandardRuntime;
        ButtonOptions = this.GetGraphicalUiElementByName("ButtonOptions") as ButtonStandardRuntime;
        Container = this.GetGraphicalUiElementByName("Container") as ContainerRuntime;
        ButtonExit = this.GetGraphicalUiElementByName("ButtonExit") as ButtonStandardRuntime;
        CustomInitialize();
    }
    //Not assigning variables because Object Instantiation Type is set to By Name rather than Fully In Code
    partial void CustomInitialize();
}
