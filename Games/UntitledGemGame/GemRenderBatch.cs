using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using MonoGame.Extended.Graphics;

namespace UntitledGemGame;

// One gem as the shader draws it: the four quad corners are built from this in
// GemShader.fx (an instanced draw), so a gem costs one record instead of four vertices.
// A gem the shader animates also carries its flight, collection, swallow or chain pull
// (Entities/GemFlight.cs).
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct GemInstance : IVertexType
{
  // Centre (xy), rotation (z) and depth (w).
  public Vector4 Placement;
  // The quad around the centre: left, top, right, bottom.
  public Vector4 Extent;
  // Texture coordinates of the top-left (xy) and bottom-right (zw) corners.
  public Vector4 TextureRect;
  public Color Color;
  // The animation's own data; see GemShader.fx.
  public Vector4 Flight;
  // Start time on Gem.FlightClock and the animation kind (and its length); zero when settled.
  public Vector2 Timing;

  public static readonly VertexDeclaration VertexDeclaration = new(
    new VertexElement(0, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 1),
    new VertexElement(16, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 2),
    new VertexElement(32, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 3),
    new VertexElement(48, VertexElementFormat.Color, VertexElementUsage.Color, 0),
    new VertexElement(52, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 4),
    new VertexElement(68, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 5));
  VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;
}

// All world gems share one texture. Keep their instances on the GPU between frames;
// animation/hover changes update CPU copies and upload only the affected pages.
public sealed class GemRenderBatch : IDisposable
{
  private const int GemsPerPage = 4096;
  private sealed class Page
  {
    public readonly GemInstance[] Instances = new GemInstance[GemsPerPage];
    public DynamicVertexBuffer Buffer;
    public VertexBufferBinding[] Bindings;
    public bool Dirty = true;
  }
  private record struct Entry(int Id, Sprite Sprite, Transform2 Transform, Entities.Gem Gem)
  {
    public bool Dirty;
  }
  private readonly List<Entry> _entries = new();
  private readonly List<int> _dirtySlots = new();
  public int RebuiltQuadsLastFrame { get; private set; }
  // Render slot per entity id (ids are small and dense), -1 when the entity has none.
  private int[] _slotById = [];
  private int _count;
  private readonly List<Page> _pages = new();
  private readonly GraphicsDevice _graphics;
  private IndexBuffer _indices;
  // The corners every instance is drawn with (0..1 across the quad).
  private VertexBuffer _corners;
  private int _firstRemovedSlot = int.MaxValue;
  public int UploadedPagesLastFrame { get; private set; }
  public int Count => _count;

  public bool Contains(int id) => SlotOf(id) >= 0;

  private int SlotOf(int id) => (uint)id < (uint)_slotById.Length ? _slotById[id] : -1;

  private void SetSlot(int id, int slot)
  {
    if (id >= _slotById.Length)
    {
      int old = _slotById.Length;
      Array.Resize(ref _slotById, Math.Max(id + 1, old * 2));
      Array.Fill(_slotById, -1, old, _slotById.Length - old);
    }
    _slotById[id] = slot;
  }

  public GemRenderBatch(GraphicsDevice graphics) => _graphics = graphics;

  // Sizes the batch for a field while the game loads, instead of growing it during play.
  public void Reserve(int gems, int maxEntityId)
  {
    _entries.EnsureCapacity(gems);
    _dirtySlots.EnsureCapacity(gems);
    while (_pages.Count * GemsPerPage < gems) _pages.Add(new Page());
    if (maxEntityId >= _slotById.Length) SetSlot(maxEntityId, -1);
  }

  public void Add(int id, Sprite sprite, Transform2 transform, Entities.Gem gem = null)
  {
    if (SlotOf(id) >= 0) throw new ArgumentException($"Gem {id} is already in the render batch", nameof(id));
    int slot = _entries.Count;
    SetSlot(id, slot);
    ++_count;
    _entries.Add(new Entry(id, sprite, transform, gem));
    if (slot / GemsPerPage == _pages.Count) _pages.Add(new Page());
    MarkDirty(slot);
  }

  public void Update(int id)
  {
    int slot = SlotOf(id);
    if (slot >= 0) MarkDirty(slot);
  }

  private void MarkDirty(int slot)
  {
    ref var entry = ref CollectionsMarshal.AsSpan(_entries)[slot];
    if (entry.Dirty) return;
    entry.Dirty = true;
    _dirtySlots.Add(slot);
  }

  private void FlushGeometry()
  {
    RebuiltQuadsLastFrame = 0;
    foreach (int slot in _dirtySlots)
    {
      ref var entry = ref CollectionsMarshal.AsSpan(_entries)[slot];
      if (entry.Sprite == null) continue;
      WriteQuad(slot);
      entry.Dirty = false;
      ++RebuiltQuadsLastFrame;
    }
    _dirtySlots.Clear();
  }

  public void Remove(int id)
  {
    int slot = SlotOf(id);
    if (slot < 0) return;
    _slotById[id] = -1;
    --_count;
    // Alpha-blended gems must keep their relative drawing order. Swapping the
    // last gem into this slot can hide it behind an unrelated overlapping gem.
    _entries[slot] = default;
    _firstRemovedSlot = Math.Min(_firstRemovedSlot, slot);
  }

  private void CompactRemovedSlots()
  {
    if (_firstRemovedSlot == int.MaxValue) return;
    var entries = CollectionsMarshal.AsSpan(_entries);
    int write = _firstRemovedSlot;
    int read = write + 1;
    while (read < entries.Length)
    {
      if (entries[read].Sprite == null) { ++read; continue; }
      // Survivors between removals move together: their instances are copied as one run
      // (split at page boundaries) instead of being rebuilt.
      int run = read;
      while (run < entries.Length && entries[run].Sprite != null)
      {
        entries[write + run - read] = entries[run];
        _slotById[entries[run].Id] = write + run - read;
        ++run;
      }
      CopyQuads(read, write, run - read);
      write += run - read;
      read = run;
    }
    _entries.RemoveRange(write, _entries.Count - write);
    _firstRemovedSlot = int.MaxValue;
  }

  private void CopyQuads(int from, int to, int count)
  {
    while (count > 0)
    {
      int chunk = Math.Min(count, Math.Min(GemsPerPage - from % GemsPerPage, GemsPerPage - to % GemsPerPage));
      var destination = _pages[to / GemsPerPage];
      Array.Copy(_pages[from / GemsPerPage].Instances, from % GemsPerPage,
        destination.Instances, to % GemsPerPage, chunk);
      destination.Dirty = true;
      from += chunk;
      to += chunk;
      count -= chunk;
    }
  }

  private void WriteQuad(int slot)
  {
    var entry = _entries[slot];
    var sprite = entry.Sprite;
    var transform = entry.Transform;
    var region = sprite.TextureRegion;
    var origin = sprite.Origin;
    var scale = transform.Scale;
    var position = transform.Position;
    float left = -origin.X * scale.X;
    float top = -origin.Y * scale.Y;
    float right = (region.Width - origin.X) * scale.X;
    float bottom = (region.Height - origin.Y) * scale.Y;
    if (!sprite.IsVisible) right = left; // A degenerate quad matches SpriteBatch's hidden sprite.
    float u0 = region.LeftUV, u1 = region.RightUV, v0 = region.TopUV, v1 = region.BottomUV;
    if ((sprite.Effect & SpriteEffects.FlipHorizontally) != 0) (u0, u1) = (u1, u0);
    if ((sprite.Effect & SpriteEffects.FlipVertically) != 0) (v0, v1) = (v1, v0);

    // What the shader animates, from the gem's centre (GemShader.fx):
    // chain pull: Timing.y = 2 + duration, Flight.xy = displacement to the target;
    // swallowed: Timing.y = -(2 + duration), Flight.xy = the point below the surface;
    // collecting: Timing.y = -1, Flight.xy = collector slot and distance;
    // spawning: Timing.y = starting scale share, Flight.xy = the glide from the launch point.
    var gem = entry.Gem;
    Vector4 flight = Vector4.Zero;
    Vector2 timing = Vector2.Zero;
    if (gem != null && gem.PullStart >= 0f)
    {
      flight = new Vector4(gem.PullTo - gem.PullFrom, 0f, 0f);
      timing = new Vector2(gem.PullStart, Entities.Gem.PullTimingBase + gem.PullDuration);
    }
    else if (gem != null && gem.CollectStart >= 0f && gem.SwallowDuration > 0f)
    {
      flight = new Vector4(gem.SwallowTo, 0f, 0f);
      timing = new Vector2(gem.CollectStart, -(Entities.Gem.SwallowTimingBase + gem.SwallowDuration));
    }
    else if (gem != null && gem.CollectStart >= 0f)
    {
      flight = new Vector4(gem.CollectSlot, gem.CollectDistance, 0f, 0f);
      timing = new Vector2(gem.CollectStart, -1f);
    }
    else if (gem != null && gem.FlightStart >= 0f)
    {
      if (!gem.FlightGlideTaken) flight = new Vector4(position - gem.FlightFrom, 0f, 0f);
      timing = new Vector2(gem.FlightStart, Math.Max(gem.FlightGrowFrom, 0.0001f));
    }

    var page = _pages[slot / GemsPerPage];
    ref var instance = ref page.Instances[slot % GemsPerPage];
    instance.Placement = new Vector4(position, transform.Rotation, sprite.Depth);
    instance.Extent = new Vector4(left, top, right, bottom);
    instance.TextureRect = new Vector4(u0, v0, u1, v1);
    instance.Color = sprite.Color;
    instance.Flight = flight;
    instance.Timing = timing;
    page.Dirty = true;
  }

  public void Draw(Effect effect, Texture2D texture)
  {
    UploadedPagesLastFrame = 0;
    // Slots are stable until compaction; rebuild only surviving dirty entries first.
    FlushGeometry();
    CompactRemovedSlots();
    if (_entries.Count == 0) return;
    if (_indices == null)
    {
      _indices = new IndexBuffer(_graphics, IndexElementSize.SixteenBits, 6, BufferUsage.WriteOnly);
      _indices.SetData(new ushort[] { 0, 1, 2, 1, 3, 2 });
      _corners = new VertexBuffer(_graphics, CornerDeclaration, 4, BufferUsage.WriteOnly);
      _corners.SetData(new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) });
    }

    _graphics.BlendState = BlendState.AlphaBlend;
    _graphics.DepthStencilState = DepthStencilState.None;
    _graphics.RasterizerState = RasterizerState.CullNone;
    _graphics.Indices = _indices;
    effect.Parameters["SpriteTexture"]?.SetValue(texture);
    int remaining = _entries.Count;
    for (int i = 0; remaining > 0; ++i)
    {
      var page = _pages[i];
      int count = Math.Min(GemsPerPage, remaining);
      if (page.Buffer == null)
      {
        page.Buffer = new DynamicVertexBuffer(_graphics, GemInstance.VertexDeclaration,
          GemsPerPage, BufferUsage.WriteOnly);
        page.Bindings = [new VertexBufferBinding(_corners), new VertexBufferBinding(page.Buffer, 0, 1)];
        page.Dirty = true;
      }
      if (page.Dirty)
      {
        page.Buffer.SetData(page.Instances, 0, count, SetDataOptions.Discard);
        page.Dirty = false;
        ++UploadedPagesLastFrame;
      }
      _graphics.SetVertexBuffers(page.Bindings);
      foreach (var pass in effect.CurrentTechnique.Passes)
      {
        pass.Apply();
        _graphics.Textures[0] = texture;
        _graphics.SamplerStates[0] = SamplerState.LinearClamp;
        _graphics.DrawInstancedPrimitives(PrimitiveType.TriangleList, 0, 0, 2, count);
      }
      remaining -= count;
    }
    _graphics.SetVertexBuffer(null);
    _graphics.Indices = null;
  }

  private static readonly VertexDeclaration CornerDeclaration = new(
    new VertexElement(0, VertexElementFormat.Vector2, VertexElementUsage.Position, 0));

  public void Dispose()
  {
    foreach (var page in _pages) page.Buffer?.Dispose();
    _indices?.Dispose();
    _indices = null;
    _corners?.Dispose();
    _corners = null;
    _pages.Clear();
    _entries.Clear();
    _dirtySlots.Clear();
    _slotById = [];
    _count = 0;
    _firstRemovedSlot = int.MaxValue;
  }
}
