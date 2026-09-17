#if !KNI_WEB
using System;
using System.Runtime.InteropServices;
using JapeFramework;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace UntitledGemGame;

// SDL presents the HUD at the window's pixel resolution in an independent renderer. SDL2's
// Wayland backend needs a renderer-backed buffer to make the window visible.
internal sealed class UpgradePopoutWindow : IDisposable
{
    private IntPtr window;
    private IntPtr renderer;
    private IntPtr texture;
    private const int InitialWidth = 1280, InitialHeight = 720;
    private int textureWidth, textureHeight;
    private readonly EventWatch watch;
    private readonly uint windowId;
    private RenderTarget2D preview;
    private Color[] pixels;
    private float opacity = 1f;
    private readonly bool usePixelOpacity;
    private static bool transparencyConfigured;
    public bool OpacitySupported { get; private set; }
    public Color BackgroundColor => HudLayout.PanelColor * (OpacitySupported ? opacity : 1f);
    public static bool UsesTransparentSurfaces => transparencyConfigured
        && Marshal.PtrToStringUTF8(SDL_GetCurrentVideoDriver()) is "wayland" or "offscreen";

    // SDL2 reads this when its Wayland video backend is initialized, before
    // either game window exists. The main window still renders opaque pixels.
    public static void ConfigureTransparency()
        => transparencyConfigured = SDL_SetHint("SDL_VIDEO_EGL_ALLOW_TRANSPARENCY", "1") != 0;

    public bool SetOpacity(float value)
    {
        if (!OpacitySupported) return false;
        opacity = Math.Clamp(value, 0f, 1f);
        return true;
    }
    public bool CloseRequested { get; private set; }
    public bool Focused => SDL_GetKeyboardFocus() == window;
    public bool PointerOver => SDL_GetMouseFocus() == window;

    public UpgradePopoutWindow()
    {
        var contextWindow = SDL_GL_GetCurrentWindow();
        var context = SDL_GL_GetCurrentContext();
        const int shareWithCurrentContext = 22;
        const int alphaSize = 3;
        string driver = Marshal.PtrToStringUTF8(SDL_GetCurrentVideoDriver());
        usePixelOpacity = driver is "wayland" or "offscreen";
        Check(SDL_GL_GetAttribute(alphaSize, out int previousAlpha), "Read GL alpha size");
        Check(SDL_GL_GetAttribute(shareWithCurrentContext, out int previousShare), "Read GL sharing mode");
        try
        {
            // MonoGame enables sharing for its background loader. SDL's renderer
            // must own its resources so disposing it cannot invalidate game shaders.
            Check(SDL_GL_SetAttribute(shareWithCurrentContext, 0), "Disable GL sharing");
            if (usePixelOpacity) Check(SDL_GL_SetAttribute(alphaSize, 8), "Enable window alpha");
            window = Require(SDL_CreateWindow("UntitledGemGame — Upgrades", 0x2FFF0000,
                0x2FFF0000, InitialWidth, InitialHeight, 0x2024), "Create upgrade window");
            renderer = Require(SDL_CreateRenderer(window, -1, 2), "Create upgrade renderer");
            if (usePixelOpacity)
            {
                Check(SDL_GL_GetAttribute(alphaSize, out int actualAlpha), "Read window alpha size");
                OpacitySupported = transparencyConfigured && actualAlpha > 0;
            }
            Check(SDL_RenderSetLogicalSize(renderer, HudLayout.Width, HudLayout.Bottom), "Set upgrade canvas size");
            Check(SDL_SetRenderDrawColor(renderer, 15, 13, 27, 255), "Set upgrade background");
            Check(SDL_RenderClear(renderer), "Clear upgrade window");
            SDL_RenderPresent(renderer);
            SDL_ShowWindow(window);
            SDL_RaiseWindow(window);
            SDL_SetWindowMinimumSize(window, 640, 360);
            windowId = SDL_GetWindowID(window);
            watch = WatchEvent;
            SDL_AddEventWatch(watch, IntPtr.Zero);
        }
        catch
        {
            DestroyNativeResources();
            throw;
        }
        finally
        {
            SDL_GL_SetAttribute(alphaSize, previousAlpha);
            SDL_GL_SetAttribute(shareWithCurrentContext, previousShare);
            SDL_GL_MakeCurrent(contextWindow, context);
        }
    }

    private static IntPtr Require(IntPtr value, string operation)
    {
        if (value == IntPtr.Zero) throw new InvalidOperationException($"{operation}: {Marshal.PtrToStringUTF8(SDL_GetError())}");
        return value;
    }

    private static void Check(int result, string operation)
    {
        if (result < 0) throw new InvalidOperationException($"{operation}: {Marshal.PtrToStringUTF8(SDL_GetError())}");
    }

    private int WatchEvent(IntPtr user, IntPtr ev)
    {
        // SDL callbacks can run during event pumping. Defer all destruction to Update.
        if (Marshal.ReadInt32(ev) == 0x200 && (uint)Marshal.ReadInt32(ev, 8) == windowId
            && Marshal.ReadByte(ev, 12) == 14) CloseRequested = true;
        return 1;
    }

    public Matrix InputTransform()
    {
        if (!PointerOver || !Focused) return Matrix.CreateTranslation(-100000, -100000, 0);
        SDL_GetMouseState(out int x, out int y);
        SDL_GetWindowSize(window, out int width, out int height);
        var main = Microsoft.Xna.Framework.Input.Mouse.GetState();
        float scale = Math.Min(width / (float)HudLayout.Width, height / (float)HudLayout.Bottom);
        float left = (width - HudLayout.Width * scale) / 2;
        float top = (height - HudLayout.Bottom * scale) / 2;
        return Matrix.CreateTranslation(x - main.X - left, y - main.Y - top, 0)
            * Matrix.CreateScale(1 / scale, 1 / scale, 1);
    }

    public unsafe void Present(GraphicsDevice graphics, SpriteBatch batch, Texture2D hud)
    {
        var contextWindow = SDL_GL_GetCurrentWindow();
        var context = SDL_GL_GetCurrentContext();
        int outputWidth, outputHeight;
        try
        {
            // Window coordinates are logical units on high-DPI monitors.
            Check(SDL_GetRendererOutputSize(renderer, out outputWidth, out outputHeight), "Read upgrade pixel size");
        }
        finally { SDL_GL_MakeCurrent(contextWindow, context); }
        if (outputWidth <= 0 || outputHeight <= 0) return;
        // Match the letterboxed content's pixels, up to the source HUD resolution.
        // This avoids the old 720p downsample followed by an enlarged second copy.
        float scale = Math.Min(1f, Math.Min(outputWidth / (float)hud.Width, outputHeight / (float)hud.Height));
        int width = Math.Max(1, (int)MathF.Round(hud.Width * scale));
        int height = Math.Max(1, (int)MathF.Round(hud.Height * scale));
        if (preview == null || preview.Width != width || preview.Height != height)
        {
            preview?.Dispose();
            preview = new RenderTarget2D(graphics, width, height, false, SurfaceFormat.Color, DepthFormat.None);
            pixels = new Color[width * height];
        }
        graphics.SetRenderTarget(preview);
        graphics.Clear(BackgroundColor);
        batch.Begin(blendState: BlendState.Opaque, samplerState: SamplerState.LinearClamp);
        batch.Draw(hud, new Rectangle(0, 0, width, height), Color.White);
        batch.End();
        graphics.SetRenderTarget(null);
        preview.GetData(pixels);
        try
        {
            if (texture == IntPtr.Zero || textureWidth != width || textureHeight != height)
            {
                // ABGR8888 stores XNA Color's RGBA bytes on little-endian desktops.
                var replacement = Require(SDL_CreateTexture(renderer, 0x16762004, 1, width, height), "Create upgrade texture");
                try
                {
                    Check(SDL_SetTextureBlendMode(replacement, 0), "Copy premultiplied window pixels");
                }
                catch { SDL_DestroyTexture(replacement); throw; }
                if (texture != IntPtr.Zero) SDL_DestroyTexture(texture);
                texture = replacement;
                textureWidth = width;
                textureHeight = height;
            }
            fixed (Color* data = pixels)
                Check(SDL_UpdateTexture(texture, IntPtr.Zero, (IntPtr)data, width * 4), "Upload upgrade frame");
            var background = BackgroundColor;
            Check(SDL_SetRenderDrawColor(renderer, background.R, background.G, background.B, background.A), "Set frame opacity");
            Check(SDL_RenderClear(renderer), "Clear upgrade frame");
            Check(SDL_RenderCopy(renderer, texture, IntPtr.Zero, IntPtr.Zero), "Draw upgrade frame");
            SDL_RenderPresent(renderer);
        }
        finally { SDL_GL_MakeCurrent(contextWindow, context); }
    }

    private void DestroyNativeResources()
    {
        if (texture != IntPtr.Zero) SDL_DestroyTexture(texture);
        if (renderer != IntPtr.Zero) SDL_DestroyRenderer(renderer);
        if (window != IntPtr.Zero) SDL_DestroyWindow(window);
        texture = renderer = window = IntPtr.Zero;
    }

    public void Dispose()
    {
        if (window == IntPtr.Zero) return;
        if (watch != null) SDL_DelEventWatch(watch, IntPtr.Zero);
        var contextWindow = SDL_GL_GetCurrentWindow();
        var context = SDL_GL_GetCurrentContext();
        try { DestroyNativeResources(); }
        finally { SDL_GL_MakeCurrent(contextWindow, context); }
        window = IntPtr.Zero;
        preview?.Dispose();
    }

    // Use the same bundled SDL library as MonoGame on each desktop platform.
    private const string Library = "GemPopoutSDL";
    static UpgradePopoutWindow()
    {
        NativeLibrary.SetDllImportResolver(typeof(UpgradePopoutWindow).Assembly, (name, assembly, path) =>
        {
            if (name != Library) return IntPtr.Zero;
            string file = OperatingSystem.IsWindows() ? "SDL2.dll" : OperatingSystem.IsMacOS() ? "libSDL2-2.0.0.dylib" : "libSDL2-2.0.so.0";
            return NativeLibrary.Load(file, typeof(Game).Assembly, path);
        });
    }
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int EventWatch(IntPtr user, IntPtr ev);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr SDL_CreateWindow([MarshalAs(UnmanagedType.LPUTF8Str)] string title, int x, int y, int w, int h, uint flags);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern void SDL_DestroyWindow(IntPtr window);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr SDL_CreateRenderer(IntPtr window, int index, uint flags);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern void SDL_DestroyRenderer(IntPtr renderer);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr SDL_CreateTexture(IntPtr renderer, uint format, int access, int width, int height);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern void SDL_DestroyTexture(IntPtr texture);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int SDL_UpdateTexture(IntPtr texture, IntPtr rect, IntPtr data, int pitch);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int SDL_GetRendererOutputSize(IntPtr renderer, out int width, out int height);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int SDL_RenderSetLogicalSize(IntPtr renderer, int width, int height);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int SDL_SetRenderDrawColor(IntPtr renderer, byte r, byte g, byte b, byte a);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int SDL_RenderClear(IntPtr renderer);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int SDL_RenderCopy(IntPtr renderer, IntPtr texture, IntPtr source, IntPtr destination);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern void SDL_RenderPresent(IntPtr renderer);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int SDL_GL_GetAttribute(int attribute, out int value);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int SDL_GL_SetAttribute(int attribute, int value);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern void SDL_ShowWindow(IntPtr window);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern void SDL_RaiseWindow(IntPtr window);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int SDL_SetHint([MarshalAs(UnmanagedType.LPUTF8Str)] string name, [MarshalAs(UnmanagedType.LPUTF8Str)] string value);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr SDL_GetCurrentVideoDriver();
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int SDL_SetTextureBlendMode(IntPtr texture, int blendMode);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr SDL_GetError();
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr SDL_GL_GetCurrentWindow();
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr SDL_GL_GetCurrentContext();
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int SDL_GL_MakeCurrent(IntPtr window, IntPtr context);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern uint SDL_GetWindowID(IntPtr window);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern void SDL_AddEventWatch(EventWatch watch, IntPtr user);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern void SDL_DelEventWatch(EventWatch watch, IntPtr user);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr SDL_GetKeyboardFocus();
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr SDL_GetMouseFocus();
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern uint SDL_GetMouseState(out int x, out int y);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern void SDL_GetWindowSize(IntPtr window, out int w, out int h);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern void SDL_SetWindowMinimumSize(IntPtr window, int w, int h);
}
#endif
