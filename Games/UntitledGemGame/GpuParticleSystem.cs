using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

#if KNI_WEB
// The unsupported browser constructor always throws before assigning desktop resources.
#pragma warning disable CS0649
#endif

namespace UntitledGemGame;

internal readonly record struct GpuParticleDrawRange(int Start, int Count);

/// <summary>
/// GPU particle state lives in two pairs of float textures. The CPU tracks bursts and texture
/// rows, never individual particles. Call Simulate before drawing the scene, then Draw in-world.
/// </summary>
public sealed class GpuParticleSystem : IDisposable
{
  public const int DefaultTextureSize = 128;
  public const int MaxPendingBursts = 64;
  private const float MaxFrameTime = 0.25f;
  private readonly GraphicsDevice graphics;
  private readonly Effect effect;
  private readonly RenderTarget2D[] positions = new RenderTarget2D[2];
  private readonly RenderTarget2D[] velocities = new RenderTarget2D[2];
  private readonly RenderTargetBinding[][] bindings = new RenderTargetBinding[2][];
  // GetRenderTargets(array) avoids allocating a new array for every simulation/draw.
  private readonly RenderTargetBinding[][] savedTargets = [[], new RenderTargetBinding[1],
    new RenderTargetBinding[2], new RenderTargetBinding[3], new RenderTargetBinding[4]];
  private readonly double[] rowExpiry;
  private readonly int[] rowCounts;
  private readonly bool[] clearRows;
  private readonly GpuParticleDrawRange[] drawRanges;
  private readonly PendingBurst[] pending = new PendingBurst[MaxPendingBursts];
  private readonly RasterizerState scissorState = new() { CullMode = CullMode.None, ScissorTestEnable = true };
  private readonly BlendState additive = new()
  {
    ColorSourceBlend = Blend.One, ColorDestinationBlend = Blend.One,
    AlphaSourceBlend = Blend.Zero, AlphaDestinationBlend = Blend.One
  };
  private readonly EffectTechnique clearTechnique, spawnTechnique, updateTechnique, bindTechnique;
  private readonly EffectParameter spawnRange, spawnPositionVelocity, spawnSpeedLifetime,
    spawnSizeShape, spawnSpreadColor, simulationStep, simulationAcceleration, positionMap, velocityMap;
#if !KNI_WEB
  private GpuParticleGlRenderer renderer;
  private bool rebuildRenderer;
#endif
  private int pendingCount, nextSlot, readIndex, rangeCount;
  private uint sequence;
  private float pendingTime;
  private double clock;
  private bool disposed;
  private readonly record struct PendingBurst(GpuParticleBurst Burst, int Start, int Count, float Seed);
  private static readonly VertexPositionTexture[] FullScreen =
  [
    new(new Vector3(-1, 1, 0), new Vector2(0, 0)), new(new Vector3(1, 1, 0), new Vector2(1, 0)),
    new(new Vector3(-1, -1, 0), new Vector2(0, 1)), new(new Vector3(1, -1, 0), new Vector2(1, 1))
  ];
  // Flush MonoGame's state and bind texture units, without drawing any pixels.
  private static readonly VertexPositionTexture[] BindingTriangle =
  [
    new(new Vector3(-3, -3, 0), Vector2.Zero), new(new Vector3(-3, -2, 0), Vector2.Zero),
    new(new Vector3(-2, -3, 0), Vector2.Zero)
  ];

  public int TextureSize { get; }
  public int Capacity => TextureSize * TextureSize;
  public int PendingBursts => pendingCount;
  public bool HasParticles => pendingCount > 0 || ActiveRows > 0;
  public int ActiveRows { get; private set; }
  public int ActiveSlotUpperBound { get; private set; }
  public int SimulatedSlotsLastFrame { get; private set; }
  public int DrawnSlotsLastFrame { get; private set; }
  public int SimulationPassesLastFrame { get; private set; }
  public int SpawnPassesLastFrame { get; private set; }
  public int DrawCallsLastFrame { get; private set; }
  public Vector2 Acceleration { get; set; }
  public float Drag { get; set; } = 1.5f;

  public GpuParticleSystem(GraphicsDevice graphics, Effect sourceEffect, int textureSize = DefaultTextureSize)
  {
    ArgumentNullException.ThrowIfNull(graphics);
    ArgumentNullException.ThrowIfNull(sourceEffect);
#if KNI_WEB
    throw new PlatformNotSupportedException("GPU particles currently target MonoGame DesktopGL.");
#else
    if (textureSize < 1 || textureSize > 1024) throw new ArgumentOutOfRangeException(nameof(textureSize));
    this.graphics = graphics;
    TextureSize = textureSize;
    rowExpiry = new double[textureSize];
    rowCounts = new int[textureSize];
    clearRows = new bool[textureSize];
    drawRanges = new GpuParticleDrawRange[textureSize];
    effect = sourceEffect.Clone();
    clearTechnique = effect.Techniques["ClearState"];
    spawnTechnique = effect.Techniques["SpawnState"];
    updateTechnique = effect.Techniques["UpdateState"];
    bindTechnique = effect.Techniques["BindParticleTextures"];
    spawnRange = effect.Parameters["SpawnRange"];
    spawnPositionVelocity = effect.Parameters["SpawnPositionVelocity"];
    spawnSpeedLifetime = effect.Parameters["SpawnSpeedLifetime"];
    spawnSizeShape = effect.Parameters["SpawnSizeShape"];
    spawnSpreadColor = effect.Parameters["SpawnSpreadColor"];
    simulationStep = effect.Parameters["SimulationStep"];
    simulationAcceleration = effect.Parameters["SimulationAcceleration"];
    positionMap = effect.Parameters["PositionMap"];
    velocityMap = effect.Parameters["VelocityMap"];
    try
    {
      for (int i = 0; i < 2; i++)
      {
        positions[i] = CreateTarget();
        velocities[i] = CreateTarget();
        bindings[i] = [new(positions[i]), new(velocities[i])];
      }
      renderer = new GpuParticleGlRenderer();
      graphics.DeviceReset += OnDeviceReset;
    }
    catch { Dispose(); throw; }
#endif
  }

  private RenderTarget2D CreateTarget() => new(graphics, TextureSize, TextureSize, false,
    SurfaceFormat.Vector4, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);

  private void OnDeviceReset(object sender, EventArgs args)
  {
    Clear();
#if !KNI_WEB
    rebuildRenderer = true;
#endif
  }

  /// <summary>A bounded ring replaces the oldest slots on wrap. A full burst queue returns zero.</summary>
  public int Emit(in GpuParticleBurst burst)
  {
    ObjectDisposedException.ThrowIf(disposed, this);
    burst.Validate();
    if (burst.Count == 0 || pendingCount == MaxPendingBursts) return 0;
    int count = Math.Min(burst.Count, Capacity);
    pending[pendingCount++] = new(burst, nextSlot, count, (++sequence % 65536) + 1f);
    int left = count, slot = nextSlot;
    while (left > 0)
    {
      int row = slot / TextureSize;
      int end = Math.Min(TextureSize, slot % TextureSize + left);
      if (rowCounts[row] == 0) clearRows[row] = true;
      rowCounts[row] = Math.Max(rowCounts[row], end);
      rowExpiry[row] = Math.Max(rowExpiry[row], clock + burst.Lifetime.Y + 0.00001);
      int written = end - slot % TextureSize;
      slot = (slot + written) % Capacity;
      left -= written;
    }
    nextSlot = (nextSlot + count) % Capacity;
    return count;
  }

  public void Update(float elapsedSeconds)
  {
    ObjectDisposedException.ThrowIf(disposed, this);
    if (!float.IsFinite(elapsedSeconds) || elapsedSeconds < 0)
      throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
    if (HasParticles) pendingTime = Math.Min(MaxFrameTime, pendingTime + elapsedSeconds);
  }

  /// <summary>Clearing is CPU-only. Reused rows are initialized lazily, including after a reset.</summary>
  public void Clear()
  {
    ObjectDisposedException.ThrowIf(disposed, this);
    Array.Clear(rowExpiry);
    Array.Clear(rowCounts);
    Array.Clear(clearRows);
    pendingCount = nextSlot = readIndex = rangeCount = 0;
    pendingTime = 0;
    clock = 0;
    ActiveRows = ActiveSlotUpperBound = SimulatedSlotsLastFrame = DrawnSlotsLastFrame = 0;
    SimulationPassesLastFrame = SpawnPassesLastFrame = DrawCallsLastFrame = 0;
  }

  /// <summary>Call before drawing scene pixels: simulation temporarily switches render targets.</summary>
  public void Simulate()
  {
    ObjectDisposedException.ThrowIf(disposed, this);
    SimulatedSlotsLastFrame = SimulationPassesLastFrame = SpawnPassesLastFrame = 0;
    if (pendingCount == 0 && (pendingTime == 0 || ActiveRows == 0)) return;
    if (!float.IsFinite(Drag) || Drag < 0 || !float.IsFinite(Acceleration.X) || !float.IsFinite(Acceleration.Y))
      throw new InvalidOperationException("Acceleration must be finite and drag finite and nonnegative.");
#if !KNI_WEB
    if (rebuildRenderer)
    {
      renderer.Dispose();
      renderer = new GpuParticleGlRenderer();
      rebuildRenderer = false;
    }
#endif
    using var state = new DeviceState(this, true);
    graphics.BlendState = BlendState.Opaque;
    graphics.DepthStencilState = DepthStencilState.None;
    graphics.RasterizerState = scissorState;
    graphics.SetVertexBuffer(null);
    graphics.Indices = null;
    BindTargets(readIndex);
    // Only newly occupied rows need initialization. No full-pool clear/copy pass.
    for (int row = 0; row < TextureSize; row++)
    {
      if (!clearRows[row]) continue;
      int start = row;
      do { clearRows[row++] = false; } while (row < TextureSize && clearRows[row]);
      Apply(clearTechnique);
      DrawRect(new Rectangle(0, start, TextureSize, row - start));
      row--;
    }
    for (int i = 0; i < pendingCount; i++)
    {
      ref readonly var item = ref pending[i];
      var burst = item.Burst;
      spawnRange.SetValue(new Vector4(item.Start, item.Count, TextureSize, item.Seed));
      spawnPositionVelocity.SetValue(new Vector4(burst.Position.X, burst.Position.Y, burst.Velocity.X, burst.Velocity.Y));
      spawnSpeedLifetime.SetValue(new Vector4(burst.Speed.X, burst.Speed.Y, burst.Lifetime.X, burst.Lifetime.Y));
      float sign = burst.Streak ? -1 : 1;
      spawnSizeShape.SetValue(new Vector4(burst.Size.X * sign, burst.Size.Y * sign, burst.Radius, burst.Direction));
      var color = burst.Color * (burst.Color.A / 255f);
      spawnSpreadColor.SetValue(new Vector4(burst.Spread, color.R + color.G * 256 + color.B * 65536,
        (float)burst.Motion, burst.InnerRadius));
      Apply(spawnTechnique);
      // At most three rectangles per non-wrapped span: partial first/last rows and full middle rows.
      int first = Math.Min(item.Count, Capacity - item.Start);
      DrawSlots(item.Start, first);
      if (first < item.Count) DrawSlots(0, item.Count - first);
      SpawnPassesLastFrame++;
    }
    pendingCount = 0;
    BuildRanges();
    if (pendingTime > 0 && rangeCount > 0)
    {
      // Exact integration for constant acceleration and linear drag: one GPU pass per draw,
      // independent of rendering frame rate, with no CPU substeps or duplicate texture reads.
      double dt = pendingTime;
      double damping = Math.Exp(-Drag * dt);
      double travel, accelerationTravel;
      if (Drag * dt < 0.001)
      {
        // Stable series near zero, where 1-exp(-x) and dt-travel lose precision.
        double x = Drag * dt;
        travel = dt * (1 - x / 2 + x * x / 6 - x * x * x / 24);
        accelerationTravel = dt * dt * (0.5 - x / 6 + x * x / 24 - x * x * x / 120);
      }
      else
      {
        travel = (1 - damping) / Drag;
        accelerationTravel = (dt - travel) / Drag;
      }
      simulationStep.SetValue(new Vector4(pendingTime, (float)damping, (float)travel, (float)accelerationTravel));
      simulationAcceleration.SetValue(new Vector4(Acceleration, 0, 0));
      int write = 1 - readIndex;
      BindTargets(write, positions[readIndex], velocities[readIndex]);
      Apply(updateTechnique);
      for (int i = 0; i < rangeCount; i++)
      {
        var range = drawRanges[i];
        int firstRow = range.Start / TextureSize;
        int rows = (range.Count + TextureSize - 1) / TextureSize;
        DrawRect(new Rectangle(0, firstRow, TextureSize, rows));
        SimulatedSlotsLastFrame += rows * TextureSize;
        SimulationPassesLastFrame++;
      }
      readIndex = write;
      clock += dt;
      BuildRanges();
    }
    pendingTime = 0;
    // Once empty, start the next burst at row zero rather than walking through a large pool.
    if (ActiveRows == 0) nextSlot = 0;
  }

  private void BuildRanges()
  {
    rangeCount = ActiveRows = ActiveSlotUpperBound = 0;
    for (int row = 0; row < TextureSize; row++)
    {
      if (rowCounts[row] != 0 && rowExpiry[row] <= clock)
      {
        rowCounts[row] = 0;
        rowExpiry[row] = 0;
      }
      if (rowCounts[row] == 0) continue;
      ActiveRows++;
      ActiveSlotUpperBound += rowCounts[row];
      // Merge adjacent occupied rows into one draw, retaining at most one row of dead padding per row.
      if (rangeCount > 0 && (drawRanges[rangeCount - 1].Start + drawRanges[rangeCount - 1].Count - 1) / TextureSize == row - 1)
      {
        int start = drawRanges[rangeCount - 1].Start;
        drawRanges[rangeCount - 1] = new(start, row * TextureSize + rowCounts[row] - start);
      }
      else drawRanges[rangeCount++] = new(row * TextureSize, rowCounts[row]);
    }
  }

  private void BindTargets(int index, Texture2D position = null, Texture2D velocity = null)
  {
    graphics.Textures[0] = graphics.Textures[1] = null;
    graphics.SetRenderTargets(bindings[index]);
    positionMap.SetValue(position);
    velocityMap.SetValue(velocity);
  }
  private void Apply(EffectTechnique technique)
  {
    effect.CurrentTechnique = technique;
    technique.Passes[0].Apply();
    graphics.SamplerStates[0] = graphics.SamplerStates[1] = SamplerState.PointClamp;
  }
  private void DrawRect(Rectangle bounds)
  {
    graphics.ScissorRectangle = bounds;
    graphics.DrawUserPrimitives(PrimitiveType.TriangleStrip, FullScreen, 0, 2);
  }
  private void DrawSlots(int start, int count)
  {
    if (count == 0) return;
    int column = start % TextureSize, row = start / TextureSize;
    if (column != 0)
    {
      int first = Math.Min(count, TextureSize - column);
      DrawRect(new Rectangle(column, row++, first, 1));
      count -= first;
    }
    int rows = count / TextureSize;
    if (rows > 0) DrawRect(new Rectangle(0, row, TextureSize, rows));
    row += rows;
    if (count % TextureSize != 0) DrawRect(new Rectangle(0, row, count % TextureSize, 1));
  }

  public void Draw(Matrix viewProjection)
  {
    ObjectDisposedException.ThrowIf(disposed, this);
    DrawnSlotsLastFrame = DrawCallsLastFrame = 0;
    if (rangeCount == 0) return;
    using var state = new DeviceState(this, false);
    graphics.BlendState = additive;
    graphics.DepthStencilState = DepthStencilState.None;
    graphics.RasterizerState = RasterizerState.CullNone;
    positionMap.SetValue(positions[readIndex]);
    velocityMap.SetValue(velocities[readIndex]);
    Apply(bindTechnique);
    graphics.DrawUserPrimitives(PrimitiveType.TriangleList, BindingTriangle, 0, 1);
#if !KNI_WEB
    renderer.Draw(viewProjection, TextureSize, graphics.RenderTargetCount > 0, drawRanges.AsSpan(0, rangeCount));
#endif
    DrawCallsLastFrame = rangeCount;
    for (int i = 0; i < rangeCount; i++) DrawnSlotsLastFrame += drawRanges[i].Count;
  }

  public void Dispose()
  {
    if (disposed) return;
    disposed = true;
    graphics.DeviceReset -= OnDeviceReset;
    foreach (var target in positions) target?.Dispose();
    foreach (var target in velocities) target?.Dispose();
#if !KNI_WEB
    renderer?.Dispose();
#endif
    effect?.Dispose();
    additive.Dispose();
    scissorState.Dispose();
  }

  private readonly struct DeviceState : IDisposable
  {
    private readonly GraphicsDevice device;
    private readonly RenderTargetBinding[] targets;
    private readonly Viewport viewport;
    private readonly Rectangle scissor;
    private readonly BlendState blend;
    private readonly DepthStencilState depth;
    private readonly RasterizerState rasterizer;
    private readonly IndexBuffer index;
    private readonly Texture texture0, texture1;
    private readonly SamplerState sampler0, sampler1;
    public DeviceState(GpuParticleSystem owner, bool changesTargets)
    {
      device = owner.graphics;
#if KNI_WEB
      targets = changesTargets ? device.GetRenderTargets() : null;
#else
      targets = changesTargets ? owner.savedTargets[device.RenderTargetCount] : null;
#endif
      if (changesTargets) device.GetRenderTargets(targets);
      viewport = device.Viewport;
      scissor = device.ScissorRectangle;
      blend = device.BlendState;
      depth = device.DepthStencilState;
      rasterizer = device.RasterizerState;
      index = device.Indices;
      texture0 = device.Textures[0]; texture1 = device.Textures[1];
      sampler0 = device.SamplerStates[0]; sampler1 = device.SamplerStates[1];
    }
    public void Dispose()
    {
      device.Textures[0] = device.Textures[1] = null;
      if (targets != null) device.SetRenderTargets(targets);
      device.Viewport = viewport;
      device.ScissorRectangle = scissor;
      device.BlendState = blend;
      device.DepthStencilState = depth;
      device.RasterizerState = rasterizer;
      device.SetVertexBuffer(null);
      device.Indices = index;
      device.Textures[0] = texture0; device.Textures[1] = texture1;
      device.SamplerStates[0] = sampler0; device.SamplerStates[1] = sampler1;
    }
  }
}
