using System;
using System.Collections.Generic;
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
  private readonly record struct Entry(int Id, Sprite Sprite, Transform2 Transform);
  private readonly List<Entry> _entries = new();
  private readonly Dictionary<int, int> _slots = new();
  private readonly List<Page> _pages = new();
  private readonly GraphicsDevice _graphics;
  private IndexBuffer _indices;
  private int _firstRemovedSlot = int.MaxValue;
  public int UploadedPagesLastFrame { get; private set; }
  public int Count => _slots.Count;

  public GemRenderBatch(GraphicsDevice graphics) => _graphics = graphics;

  public void Add(int id, Sprite sprite, Transform2 transform)
  {
    int slot = _entries.Count;
    _slots.Add(id, slot);
    _entries.Add(new Entry(id, sprite, transform));
    if (slot / GemsPerPage == _pages.Count) _pages.Add(new Page());
    WriteQuad(slot);
  }

  public void Update(int id)
  {
    if (_slots.TryGetValue(id, out int slot)) WriteQuad(slot);
  }

  public void Remove(int id)
  {
    if (!_slots.Remove(id, out int slot)) return;
    // Alpha-blended gems must keep their relative drawing order. Swapping the
    // last gem into this slot can hide it behind an unrelated overlapping gem.
    _entries[slot] = default;
    _firstRemovedSlot = Math.Min(_firstRemovedSlot, slot);
  }

  private void CompactRemovedSlots()
  {
    if (_firstRemovedSlot == int.MaxValue) return;
    int write = _firstRemovedSlot;
    for (int read = write + 1; read < _entries.Count; ++read)
    {
      var entry = _entries[read];
      if (entry.Sprite == null) continue;
      _entries[write] = entry;
      _slots[entry.Id] = write;
      var destination = _pages[write / GemsPerPage];
      // Keep the already-updated quad instead of recalculating its geometry.
      Array.Copy(_pages[read / GemsPerPage].Vertices, read % GemsPerPage * 4,
        destination.Vertices, write % GemsPerPage * 4, 4);
      destination.Dirty = true;
      ++write;
    }
    _entries.RemoveRange(write, _entries.Count - write);
    _firstRemovedSlot = int.MaxValue;
  }

  private void WriteQuad(int slot)
  {
    var entry = _entries[slot];
    var sprite = entry.Sprite;
    var transform = entry.Transform;
    var region = sprite.TextureRegion;
    var page = _pages[slot / GemsPerPage];
    int vertex = slot % GemsPerPage * 4;
    float left = -sprite.Origin.X * transform.Scale.X;
    float top = -sprite.Origin.Y * transform.Scale.Y;
    float right = (region.Width - sprite.Origin.X) * transform.Scale.X;
    float bottom = (region.Height - sprite.Origin.Y) * transform.Scale.Y;
    if (!sprite.IsVisible) right = left; // A degenerate quad matches SpriteBatch's hidden sprite.
    float sin = MathF.Sin(transform.Rotation), cos = MathF.Cos(transform.Rotation);
    float u0 = region.LeftUV, u1 = region.RightUV, v0 = region.TopUV, v1 = region.BottomUV;
    if ((sprite.Effect & SpriteEffects.FlipHorizontally) != 0) (u0, u1) = (u1, u0);
    if ((sprite.Effect & SpriteEffects.FlipVertically) != 0) (v0, v1) = (v1, v0);
    page.Vertices[vertex] = MakeVertex(left, top, u0, v0);
    page.Vertices[vertex + 1] = MakeVertex(right, top, u1, v0);
    page.Vertices[vertex + 2] = MakeVertex(left, bottom, u0, v1);
    page.Vertices[vertex + 3] = MakeVertex(right, bottom, u1, v1);
    page.Dirty = true;

    VertexPositionColorTexture MakeVertex(float x, float y, float u, float v)
      => new(new Vector3(transform.Position.X + x * cos - y * sin,
        transform.Position.Y + x * sin + y * cos, sprite.Depth), sprite.Color, new Vector2(u, v));
  }

  public void Draw(Effect effect, Texture2D texture)
  {
    UploadedPagesLastFrame = 0;
    // Batch all removals since the last draw into one stable compaction pass.
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
    _slots.Clear();
    _firstRemovedSlot = int.MaxValue;
  }
}
