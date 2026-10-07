using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using MonoGame.Extended.Graphics;

namespace UntitledGemGame;

// All world gems share one texture. Keep their quads on the GPU between frames;
// animation/hover changes update CPU vertices and upload only the affected pages.
public sealed class GemRenderBatch : IDisposable
{
  private const int GemsPerPage = 4096;
  private sealed class Page
  {
    public readonly VertexPositionColorTexture[] Vertices = new VertexPositionColorTexture[GemsPerPage * 4];
    public DynamicVertexBuffer Buffer;
    public bool Dirty = true;
  }
  private record struct Entry(int Id, Sprite Sprite, Transform2 Transform)
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

  public void Add(int id, Sprite sprite, Transform2 transform)
  {
    if (SlotOf(id) >= 0) throw new ArgumentException($"Gem {id} is already in the render batch", nameof(id));
    int slot = _entries.Count;
    SetSlot(id, slot);
    ++_count;
    _entries.Add(new Entry(id, sprite, transform));
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
      // Survivors between removals move together: their quads are copied as one run
      // (split at page boundaries) instead of recalculating their geometry.
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
      Array.Copy(_pages[from / GemsPerPage].Vertices, from % GemsPerPage * 4,
        destination.Vertices, to % GemsPerPage * 4, chunk * 4);
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
    var page = _pages[slot / GemsPerPage];
    int vertex = slot % GemsPerPage * 4;
    var origin = sprite.Origin;
    var scale = transform.Scale;
    float left = -origin.X * scale.X;
    float top = -origin.Y * scale.Y;
    float right = (region.Width - origin.X) * scale.X;
    float bottom = (region.Height - origin.Y) * scale.Y;
    if (!sprite.IsVisible) right = left; // A degenerate quad matches SpriteBatch's hidden sprite.
    float rotation = transform.Rotation;
    float sin = rotation == 0f ? 0f : MathF.Sin(rotation);
    float cos = rotation == 0f ? 1f : MathF.Cos(rotation);
    var position = transform.Position;
    var color = sprite.Color;
    float depth = sprite.Depth;
    float u0 = region.LeftUV, u1 = region.RightUV, v0 = region.TopUV, v1 = region.BottomUV;
    if ((sprite.Effect & SpriteEffects.FlipHorizontally) != 0) (u0, u1) = (u1, u0);
    if ((sprite.Effect & SpriteEffects.FlipVertically) != 0) (v0, v1) = (v1, v0);
    page.Vertices[vertex] = MakeVertex(left, top, u0, v0);
    page.Vertices[vertex + 1] = MakeVertex(right, top, u1, v0);
    page.Vertices[vertex + 2] = MakeVertex(left, bottom, u0, v1);
    page.Vertices[vertex + 3] = MakeVertex(right, bottom, u1, v1);
    page.Dirty = true;

    VertexPositionColorTexture MakeVertex(float x, float y, float u, float v)
      => new(rotation == 0f
        ? new Vector3(position.X + x, position.Y + y, depth)
        : new Vector3(position.X + x * cos - y * sin, position.Y + x * sin + y * cos, depth),
        color, new Vector2(u, v));
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
      var indices = new ushort[GemsPerPage * 6];
      for (int i = 0; i < GemsPerPage; ++i)
      {
        int v = i * 4, offset = i * 6;
        indices[offset] = (ushort)v;
        indices[offset + 1] = (ushort)(v + 1);
        indices[offset + 2] = (ushort)(v + 2);
        indices[offset + 3] = (ushort)(v + 1);
        indices[offset + 4] = (ushort)(v + 3);
        indices[offset + 5] = (ushort)(v + 2);
      }
      _indices = new IndexBuffer(_graphics, IndexElementSize.SixteenBits, indices.Length, BufferUsage.WriteOnly);
      _indices.SetData(indices);
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
        page.Buffer?.Dispose();
        page.Buffer = new DynamicVertexBuffer(_graphics, VertexPositionColorTexture.VertexDeclaration,
          GemsPerPage * 4, BufferUsage.WriteOnly);
        page.Dirty = true;
      }
      if (page.Dirty)
      {
        page.Buffer.SetData(page.Vertices, 0, count * 4, SetDataOptions.Discard);
        page.Dirty = false;
        ++UploadedPagesLastFrame;
      }
      _graphics.SetVertexBuffer(page.Buffer);
      foreach (var pass in effect.CurrentTechnique.Passes)
      {
        pass.Apply();
        _graphics.Textures[0] = texture;
        _graphics.SamplerStates[0] = SamplerState.LinearClamp;
        _graphics.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, count * 2);
      }
      remaining -= count;
    }
    _graphics.SetVertexBuffer(null);
    _graphics.Indices = null;
  }

  public void Dispose()
  {
    foreach (var page in _pages) page.Buffer?.Dispose();
    _indices?.Dispose();
    _indices = null;
    _pages.Clear();
    _entries.Clear();
    _dirtySlots.Clear();
    _slotById = [];
    _count = 0;
    _firstRemovedSlot = int.MaxValue;
  }
}
