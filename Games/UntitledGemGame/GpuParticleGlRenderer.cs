#if !KNI_WEB
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Xna.Framework;

namespace UntitledGemGame;

// MonoGame 3.8.5 DesktopGL does not bind vertex-stage texture samplers. Keep the
// workaround local: simulation and texture binding use normal MonoGame effects;
// this program fetches those bound textures in the vertex stage and draws quads.
// It restores the native program/VAO so MonoGame's cached state remains accurate.
internal sealed unsafe class GpuParticleGlRenderer : IDisposable
{
  private readonly Gl gl = new();
  private readonly IntPtr context = Gl.CurrentContext;
  private uint program, vao;
  private int matrixLocation, sizeLocation, targetYLocation, firstSlotLocation;

  public GpuParticleGlRenderer()
  {
    uint vertex = 0, fragment = 0;
    try
    {
      vertex = Compile(0x8B31, "vert");
      fragment = Compile(0x8B30, "frag");
      program = gl.CreateProgram();
      gl.AttachShader(program, vertex);
      gl.AttachShader(program, fragment);
      gl.LinkProgram(program);
      gl.GetProgramiv(program, 0x8B82, out int linked);
      if (linked == 0) throw new InvalidOperationException("GPU particle program link failed: " + Log(program, false));
      matrixLocation = Uniform("ViewProjection");
      sizeLocation = Uniform("StateSize");
      targetYLocation = Uniform("TargetY");
      firstSlotLocation = Uniform("FirstSlot");
      gl.GetIntegerv(0x8B8D, out int previousProgram);
      gl.UseProgram(program);
      try
      {
        gl.Uniform1i(Uniform("PositionMap"), 0);
        gl.Uniform1i(Uniform("VelocityMap"), 1);
      }
      finally { gl.UseProgram((uint)previousProgram); }
      gl.GenVertexArrays(1, out vao);
    }
    catch
    {
      Dispose();
      throw;
    }
    finally
    {
      if (vertex != 0) gl.DeleteShader(vertex);
      if (fragment != 0) gl.DeleteShader(fragment);
    }
  }

  private uint Compile(uint kind, string stage)
  {
    using var stream = typeof(GpuParticleGlRenderer).Assembly.GetManifestResourceStream(
      $"UntitledGemGame.Content.Shaders.GpuParticles.{stage}.glsl")
      ?? throw new InvalidOperationException("Missing embedded GPU particle shader: " + stage);
    using var reader = new StreamReader(stream);
    byte[] source = Encoding.UTF8.GetBytes(reader.ReadToEnd());
    uint shader = gl.CreateShader(kind);
    try
    {
      fixed (byte* pointer = source)
      {
        byte* p = pointer;
        int length = source.Length;
        gl.ShaderSource(shader, 1, &p, &length);
      }
      gl.CompileShader(shader);
      gl.GetShaderiv(shader, 0x8B81, out int compiled);
      if (compiled == 0) throw new InvalidOperationException("GPU particle " + stage + " shader failed: " + Log(shader, true));
      return shader;
    }
    catch { gl.DeleteShader(shader); throw; }
  }

  private string Log(uint handle, bool shader)
  {
    byte* buffer = stackalloc byte[4096];
    int length;
    if (shader) gl.GetShaderInfoLog(handle, 4096, out length, buffer);
    else gl.GetProgramInfoLog(handle, 4096, out length, buffer);
    return Encoding.UTF8.GetString(buffer, Math.Clamp(length, 0, 4096));
  }

  private int Uniform(string name)
  {
    byte[] utf8 = Encoding.UTF8.GetBytes(name + '\0');
    fixed (byte* pointer = utf8)
    {
      int location = gl.GetUniformLocation(program, pointer);
      if (location < 0) throw new InvalidOperationException("Missing GPU particle uniform: " + name);
      return location;
    }
  }

  public void Draw(Matrix viewProjection, int textureSize, bool offscreen, ReadOnlySpan<GpuParticleDrawRange> ranges)
  {
    gl.GetIntegerv(0x8B8D, out int previousProgram); // GL_CURRENT_PROGRAM
    gl.GetIntegerv(0x85B5, out int previousVao); // GL_VERTEX_ARRAY_BINDING
    try
    {
      gl.UseProgram(program);
      gl.BindVertexArray(vao);
      // XNA matrices are row-major; GL interprets these as the transposed column-major matrix.
      gl.UniformMatrix4fv(matrixLocation, 1, 0, (float*)&viewProjection);
      gl.Uniform1i(sizeLocation, textureSize);
      gl.Uniform1f(targetYLocation, offscreen ? -1f : 1f);
      foreach (var range in ranges)
      {
        gl.Uniform1i(firstSlotLocation, range.Start);
        gl.DrawArraysInstanced(0x0005, 0, 4, range.Count); // GL_TRIANGLE_STRIP
      }
    }
    finally
    {
      gl.BindVertexArray((uint)previousVao);
      gl.UseProgram((uint)previousProgram);
    }
  }

  public void Dispose()
  {
    // A reset may have replaced the context; those object names now belong to the old one.
    if (context != Gl.CurrentContext) { vao = program = 0; return; }
    if (vao != 0) { gl.DeleteVertexArrays(1, ref vao); vao = 0; }
    if (program != 0) { gl.DeleteProgram(program); program = 0; }
  }

  // Resolve through MonoGame's bundled SDL so this uses the current context on Linux,
  // Windows and macOS, with no extra OpenGL package or framework fork.
  private sealed class Gl
  {
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr GetProcAddress(byte* name);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr GetContext();
    private static readonly IntPtr sdl = LoadSdl();
    private static readonly GetProcAddress getProc = Marshal.GetDelegateForFunctionPointer<GetProcAddress>(
      NativeLibrary.GetExport(sdl, "SDL_GL_GetProcAddress"));
    private static readonly GetContext getContext = Marshal.GetDelegateForFunctionPointer<GetContext>(
      NativeLibrary.GetExport(sdl, "SDL_GL_GetCurrentContext"));
    public static IntPtr CurrentContext => getContext();
    private static IntPtr LoadSdl()
    {
      string name = OperatingSystem.IsWindows() ? "SDL2.dll"
        : OperatingSystem.IsMacOS() ? "libSDL2-2.0.0.dylib" : "libSDL2-2.0.so.0";
      return NativeLibrary.Load(name, typeof(Game).Assembly, null);
    }
    private static T Load<T>(string name) where T : Delegate
    {
      byte[] bytes = Encoding.ASCII.GetBytes("gl" + name + '\0');
      fixed (byte* pointer = bytes)
      {
        IntPtr function = getProc(pointer);
        if (function == IntPtr.Zero) throw new PlatformNotSupportedException("OpenGL function unavailable: " + name);
        return Marshal.GetDelegateForFunctionPointer<T>(function);
      }
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)] public delegate uint CreateShaderCall(uint type);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] public delegate void ShaderSourceCall(uint shader, int count, byte** source, int* lengths);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] public delegate void HandleCall(uint handle);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] public delegate void GetStatusCall(uint handle, uint parameter, out int value);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] public delegate void GetLogCall(uint handle, int maximum, out int length, byte* text);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] public delegate uint CreateProgramCall();
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] public delegate void AttachShaderCall(uint program, uint shader);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] public delegate void GenVertexArraysCall(int count, out uint handle);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] public delegate void DeleteVertexArraysCall(int count, ref uint handle);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] public delegate void GetIntegerCall(uint parameter, out int value);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] public delegate int GetUniformCall(uint program, byte* name);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] public delegate void UniformIntCall(int location, int value);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] public delegate void UniformFloatCall(int location, float value);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] public delegate void UniformMatrixCall(int location, int count, byte transpose, float* values);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] public delegate void DrawCall(uint mode, int first, int vertices, int instances);

    public readonly CreateShaderCall CreateShader = Load<CreateShaderCall>("CreateShader");
    public readonly ShaderSourceCall ShaderSource = Load<ShaderSourceCall>("ShaderSource");
    public readonly HandleCall CompileShader = Load<HandleCall>("CompileShader");
    public readonly GetStatusCall GetShaderiv = Load<GetStatusCall>("GetShaderiv");
    public readonly GetLogCall GetShaderInfoLog = Load<GetLogCall>("GetShaderInfoLog");
    public readonly HandleCall DeleteShader = Load<HandleCall>("DeleteShader");
    public readonly CreateProgramCall CreateProgram = Load<CreateProgramCall>("CreateProgram");
    public readonly AttachShaderCall AttachShader = Load<AttachShaderCall>("AttachShader");
    public readonly HandleCall LinkProgram = Load<HandleCall>("LinkProgram");
    public readonly GetStatusCall GetProgramiv = Load<GetStatusCall>("GetProgramiv");
    public readonly GetLogCall GetProgramInfoLog = Load<GetLogCall>("GetProgramInfoLog");
    public readonly HandleCall DeleteProgram = Load<HandleCall>("DeleteProgram");
    public readonly GenVertexArraysCall GenVertexArrays = Load<GenVertexArraysCall>("GenVertexArrays");
    public readonly DeleteVertexArraysCall DeleteVertexArrays = Load<DeleteVertexArraysCall>("DeleteVertexArrays");
    public readonly HandleCall BindVertexArray = Load<HandleCall>("BindVertexArray");
    public readonly GetIntegerCall GetIntegerv = Load<GetIntegerCall>("GetIntegerv");
    public readonly HandleCall UseProgram = Load<HandleCall>("UseProgram");
    public readonly GetUniformCall GetUniformLocation = Load<GetUniformCall>("GetUniformLocation");
    public readonly UniformIntCall Uniform1i = Load<UniformIntCall>("Uniform1i");
    public readonly UniformFloatCall Uniform1f = Load<UniformFloatCall>("Uniform1f");
    public readonly UniformMatrixCall UniformMatrix4fv = Load<UniformMatrixCall>("UniformMatrix4fv");
    public readonly DrawCall DrawArraysInstanced = Load<DrawCall>("DrawArraysInstanced");
  }
}
#endif
