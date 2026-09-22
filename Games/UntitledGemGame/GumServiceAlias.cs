// Gum.KNI still exposes the service in its earlier namespace.
#if KNI_WEB
global using GumService = MonoGameGum.GumService;
#else
global using GumService = Gum.GumService;
#endif
