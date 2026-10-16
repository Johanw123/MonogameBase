using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using AsyncContent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using UntitledGemGame.Entities;

namespace UntitledGemGame;

/// <summary>
/// Real-time 3D showroom renderer for the Shipyard preview bay, driven by mesh and
/// Synty material data exported by <c>export_showcase_meshes</c> in
/// <c>tools/blender/prerender.py</c>. Rendering the actual 3D mesh at the exact
/// continuous turntable angle every frame avoids both the stepping of discrete
/// turntable frames and the double-image ghosting of frame crossfading.
/// </summary>
public static class ShipShowcase
{
  /// <summary>Camera elevation above the horizon in degrees (matches SHOWCASE in prerender.py).</summary>
  public const float Elevation = 20f;
  private const float SinElevation = 0.34202015f;
  private const float CosElevation = 0.93969262f;

  // Supersample factor for 4x SSAA when downsampled into the preview bay.
  private const int Supersample = 2;

  internal readonly record struct MaterialSwatch(
    float BaseR, float BaseG, float BaseB,
    float EmR, float EmG, float EmB,
    float Accent);

  internal readonly record struct PolygonFace(
    float Nx, float Ny, float Nz,
    ushort MaterialIndex, float Ao,
    int FirstVertex, int VertexCount);

  public sealed class ShowcaseMesh
  {
    public int Width { get; init; }
    public int Height { get; init; }
    public float OrthoWidth { get; init; }
    public float OrthoHeight { get; init; }
    public float Radius { get; init; }
    internal MaterialSwatch[] Materials { get; init; }
    internal PolygonFace[] Polygons { get; init; }
    internal VertexPositionColorTexture[] Vertices { get; init; }
    internal float[] VertexSpill { get; init; }
    internal short[] HullIndices { get; init; }
    internal short[] AccentIndices { get; init; }
  }

  private static bool loaded;
  private static readonly Dictionary<Harvester.HarvesterType, ShowcaseMesh> meshes = new();
  private static Texture2D detailTexture;
  private static Texture2D accentDetailTexture;
  private static BasicEffect basicEffect;
  private static RenderTarget2D glowTarget;

  private static readonly RasterizerState ShowcaseRasterizer = new()
  {
    CullMode = CullMode.None,
    MultiSampleAntiAlias = true,
  };

  private static readonly BlendState AdditiveGlowBlend = new()
  {
    ColorSourceBlend = Blend.One,
    ColorDestinationBlend = Blend.InverseSourceColor,
    AlphaSourceBlend = Blend.Zero,
    AlphaDestinationBlend = Blend.One,
  };

  private static string ShipKey(Harvester.HarvesterType type) => type switch
  {
    Harvester.HarvesterType.Harvester => "harvester_bomber01",
    Harvester.HarvesterType.AdvancedHarvester => "harvester_fighterheavy01",
    Harvester.HarvesterType.ExpertHarvester => "harvester_stealth01",
    Harvester.HarvesterType.UltimateHarvester => "harvester_fighterheavy04",
    Harvester.HarvesterType.PerimeterHarvester => "harvester_stealth05",
    _ => null
  };

  private static void EnsureLoaded(GraphicsDevice device)
  {
    if (loaded) return;
    loaded = true;

    detailTexture = AssetManager.Load<Texture2D>("Textures/Prerender/Showcase/ship_detail.png");
    if (detailTexture != null && detailTexture.Width >= 64)
    {
      int w = detailTexture.Width, h = detailTexture.Height;
      var pixels = new Color[w * h];
      detailTexture.GetData(pixels);
      for (int i = 0; i < pixels.Length; i++)
      {
        // Orange accent stripes use detail_fac = 0.45 in ship_material (prerender.py).
        byte v = (byte)Math.Clamp((int)MathF.Round(255f * 0.55f + pixels[i].R * 0.45f), 0, 255);
        pixels[i] = new Color(v, v, v, (byte)255);
      }
      accentDetailTexture = new Texture2D(device, w, h, false, SurfaceFormat.Color);
      accentDetailTexture.SetData(pixels);
    }

    string json = AssetManager.Load<string>("Data/showcase_ships.json");
    if (string.IsNullOrEmpty(json)) return;

    using var doc = JsonDocument.Parse(json);
    if (!doc.RootElement.TryGetProperty("ships", out var shipsElem)) return;

    foreach (var type in new[]
    {
      Harvester.HarvesterType.Harvester,
      Harvester.HarvesterType.AdvancedHarvester,
      Harvester.HarvesterType.ExpertHarvester,
      Harvester.HarvesterType.UltimateHarvester,
      Harvester.HarvesterType.PerimeterHarvester,
    })
    {
      string key = ShipKey(type);
      if (key == null || !shipsElem.TryGetProperty(key, out var elem)) continue;
      meshes[type] = ParseMesh(elem);
    }
  }

  private static ShowcaseMesh ParseMesh(JsonElement elem)
  {
    int width = elem.GetProperty("width").GetInt32();
    int height = elem.GetProperty("height").GetInt32();
    float orthoW = elem.GetProperty("ortho_w").GetSingle();
    float orthoH = elem.GetProperty("ortho_h").GetSingle();
    float radius = elem.GetProperty("radius").GetSingle();

    var matArr = elem.GetProperty("materials");
    var materials = new MaterialSwatch[matArr.GetArrayLength()];
    int mi = 0;
    foreach (var m in matArr.EnumerateArray())
    {
      materials[mi++] = new MaterialSwatch(
        m[0].GetSingle(), m[1].GetSingle(), m[2].GetSingle(),
        m[3].GetSingle(), m[4].GetSingle(), m[5].GetSingle(),
        m[6].GetSingle());
    }

    byte[] raw = Convert.FromBase64String(elem.GetProperty("data").GetString());
    using var ms = new MemoryStream(raw, writable: false);
    using var br = new BinaryReader(ms);

    int polyCount = br.ReadInt32();
    var polygons = new PolygonFace[polyCount];
    var verts = new List<VertexPositionColorTexture>(polyCount * 4);
    var spills = new List<float>(polyCount * 4);
    var hullIndices = new List<short>(polyCount * 6);
    var accentIndices = new List<short>(polyCount * 3);

    for (int pi = 0; pi < polyCount; pi++)
    {
      float nx = br.ReadSingle();
      float ny = br.ReadSingle();
      float nz = br.ReadSingle();
      ushort matIdx = br.ReadUInt16();
      float ao = br.ReadUInt16() / 65535f;
      int vCount = br.ReadUInt16();

      int firstVert = verts.Count;
      polygons[pi] = new PolygonFace(nx, ny, nz, matIdx, ao, firstVert, vCount);

      for (int vi = 0; vi < vCount; vi++)
      {
        float x = br.ReadSingle();
        float y = br.ReadSingle();
        float z = br.ReadSingle();
        float u = br.ReadSingle();
        float v = br.ReadSingle();
        float spill = br.ReadSingle();
        verts.Add(new VertexPositionColorTexture(new Vector3(x, y, z), Color.White, new Vector2(u, v)));
        spills.Add(spill);
      }

      var targetIndices = materials[matIdx].Accent >= 0.2f ? accentIndices : hullIndices;
      for (int ti = 1; ti < vCount - 1; ti++)
      {
        targetIndices.Add((short)firstVert);
        targetIndices.Add((short)(firstVert + ti));
        targetIndices.Add((short)(firstVert + ti + 1));
      }
    }

    return new ShowcaseMesh
    {
      Width = width,
      Height = height,
      OrthoWidth = orthoW,
      OrthoHeight = orthoH,
      Radius = radius,
      Materials = materials,
      Polygons = polygons,
      Vertices = verts.ToArray(),
      VertexSpill = spills.ToArray(),
      HullIndices = hullIndices.ToArray(),
      AccentIndices = accentIndices.ToArray(),
    };
  }

  public static ShowcaseMesh Get(GraphicsDevice device, Harvester.HarvesterType type)
  {
    EnsureLoaded(device);
    return meshes.TryGetValue(type, out var mesh) ? mesh : null;
  }

  /// <summary>
  /// Renders the ship at <paramref name="heading"/> degrees clockwise into a 2x
  /// supersampled <see cref="RenderTarget2D"/> with depth testing and orange accent glow.
  /// </summary>
  public static void Render(GraphicsDevice device, ShowcaseMesh mesh, float heading,
    ref RenderTarget2D showcaseTarget, ref SpriteBatch showcaseBatch)
  {
    int targetW = mesh.Width * Supersample;
    int targetH = mesh.Height * Supersample;
    if (showcaseTarget == null || showcaseTarget.IsDisposed || showcaseTarget.Width != targetW || showcaseTarget.Height != targetH)
    {
      showcaseTarget?.Dispose();
      showcaseTarget = new RenderTarget2D(device, targetW, targetH, false, SurfaceFormat.Color, DepthFormat.Depth24);
    }

    int glowW = Math.Max(64, mesh.Width / 2);
    int glowH = Math.Max(64, mesh.Height / 2);
    if (glowTarget == null || glowTarget.IsDisposed || glowTarget.Width != glowW || glowTarget.Height != glowH)
    {
      glowTarget?.Dispose();
      glowTarget = new RenderTarget2D(device, glowW, glowH, false, SurfaceFormat.Color, DepthFormat.Depth24);
    }

    UpdateVertexLighting(mesh, heading);

    var effect = basicEffect ??= new BasicEffect(device) { LightingEnabled = false };
    float dist = mesh.Radius * 4f + 20f;
    float rad = MathHelper.ToRadians(heading);
    effect.World = Matrix.CreateRotationZ(-rad);
    effect.View = Matrix.CreateLookAt(
      new Vector3(0f, -dist * CosElevation, dist * SinElevation),
      Vector3.Zero,
      new Vector3(0f, SinElevation, CosElevation));
    effect.Projection = Matrix.CreateOrthographic(mesh.OrthoWidth, mesh.OrthoHeight, 0.1f, dist * 2f);

    // 1. Render depth-tested orange accent mask into the quarter-res glow target.
    if (mesh.AccentIndices.Length > 0)
    {
      device.SetRenderTarget(glowTarget);
      device.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, Color.Transparent, 1f, 0);
      device.BlendState = BlendState.Opaque;
      device.DepthStencilState = DepthStencilState.Default;
      device.RasterizerState = ShowcaseRasterizer;

      effect.TextureEnabled = false;
      effect.VertexColorEnabled = false;
      if (mesh.HullIndices.Length > 0)
      {
        effect.DiffuseColor = Vector3.Zero;
        effect.Alpha = 0f;
        foreach (var pass in effect.CurrentTechnique.Passes)
        {
          pass.Apply();
          device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList,
            mesh.Vertices, 0, mesh.Vertices.Length,
            mesh.HullIndices, 0, mesh.HullIndices.Length / 3);
        }
      }

      effect.DiffuseColor = new Vector3(1.0f, 0.36f, 0.02f);
      effect.Alpha = 1f;
      foreach (var pass in effect.CurrentTechnique.Passes)
      {
        pass.Apply();
        device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList,
          mesh.Vertices, 0, mesh.Vertices.Length,
          mesh.AccentIndices, 0, mesh.AccentIndices.Length / 3);
      }
    }

    // 2. Render full 2x supersampled ship mesh with box-projected panel detail.
    device.SetRenderTarget(showcaseTarget);
    device.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, Color.Transparent, 1f, 0);
    device.BlendState = BlendState.Opaque;
    device.DepthStencilState = DepthStencilState.Default;
    device.RasterizerState = ShowcaseRasterizer;
    device.SamplerStates[0] = SamplerState.LinearWrap;

    effect.DiffuseColor = Vector3.One;
    effect.Alpha = 1f;
    effect.VertexColorEnabled = true;
    effect.TextureEnabled = detailTexture != null;

    if (mesh.HullIndices.Length > 0)
    {
      effect.Texture = detailTexture;
      foreach (var pass in effect.CurrentTechnique.Passes)
      {
        pass.Apply();
        device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList,
          mesh.Vertices, 0, mesh.Vertices.Length,
          mesh.HullIndices, 0, mesh.HullIndices.Length / 3);
      }
    }

    if (mesh.AccentIndices.Length > 0)
    {
      effect.Texture = accentDetailTexture ?? detailTexture;
      foreach (var pass in effect.CurrentTechnique.Passes)
      {
        pass.Apply();
        device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList,
          mesh.Vertices, 0, mesh.Vertices.Length,
          mesh.AccentIndices, 0, mesh.AccentIndices.Length / 3);
      }

      // 3. Soft warm orange bloom halo around visible orange stripes.
      var batch = showcaseBatch ??= new SpriteBatch(device);
      batch.Begin(blendState: AdditiveGlowBlend, samplerState: SamplerState.LinearClamp);
      ReadOnlySpan<(float dx, float dy, float weight)> taps =
      [
        (-2.5f, -2.5f, 0.045f), (2.5f, -2.5f, 0.045f), (-2.5f, 2.5f, 0.045f), (2.5f, 2.5f, 0.045f),
        (-6.0f,  0.0f, 0.030f), (6.0f,  0.0f, 0.030f), ( 0.0f, -6.0f, 0.030f), (0.0f, 6.0f, 0.030f),
      ];
      foreach (var (dx, dy, weight) in taps)
      {
        var dest = new Rectangle((int)MathF.Round(dx), (int)MathF.Round(dy), targetW, targetH);
        batch.Draw(glowTarget, dest, Color.White * weight);
      }
      batch.End();
    }
  }

  private static void UpdateVertexLighting(ShowcaseMesh mesh, float heading)
  {
    // Exact directional lights & Principled BSDF parameters from CONFIG in prerender.py.
    // Key: color (1.0, 0.82, 0.62) * 3.2, rot (35, 0, -35)
    const float L0x = -0.3289900f, L0y = -0.4698500f, L0z = 0.8191500f;
    const float H0x = -0.1772900f, H0y = -0.7596000f, H0z = 0.6257600f;
    const float C0r = 3.2000f, C0g = 2.6240f, C0b = 1.9840f;
    const float VdH0Pow5 = 0.00215f;

    // Rim: color (0.45, 0.65, 1.0) * 4.0, rot (120, 0, 150)
    const float L1x = 0.4330100f, L1y = 0.7500000f, L1z = -0.5000000f;
    const float H1x = 0.8687400f, H1y = -0.3805700f, H1z = -0.3169500f;
    const float C1r = 1.8000f, C1g = 2.6000f, C1b = 4.0000f;
    const float VdH1Pow5 = 0.23930f;

    // Fill: color (0.75, 0.45, 1.0) * 0.6, rot (60, 0, 120)
    const float L2x = 0.7500000f, L2y = 0.4330100f, L2z = 0.5000000f;
    const float H2x = 0.6066900f, H2y = -0.4098600f, H2z = 0.6811300f;
    const float C2r = 0.4500f, C2g = 0.2700f, C2b = 0.6000f;
    const float VdH2Pow5 = 0.00795f;

    // Ambient: color (0.34, 0.26, 0.5) * 0.9
    const float AmbR = 0.3060f, AmbG = 0.2340f, AmbB = 0.4500f;

    // Roughness = 0.55 -> alpha = 0.3025, alpha^2 = 0.09150625, Smith k = 0.15125
    const float Alpha2 = 0.09150625f;
    const float SmithK = 0.15125f;
    const float InvPi = 1f / MathF.PI;

    float rad = MathHelper.ToRadians(heading);
    float cos = MathF.Cos(rad);
    float sin = MathF.Sin(rad);

    var polys = mesh.Polygons;
    var mats = mesh.Materials;
    var verts = mesh.Vertices;
    var spills = mesh.VertexSpill;

    for (int pi = 0; pi < polys.Length; pi++)
    {
      ref readonly var p = ref polys[pi];
      ref readonly var mat = ref mats[p.MaterialIndex];

      float nx = p.Nx * cos + p.Ny * sin;
      float ny = -p.Nx * sin + p.Ny * cos;
      float nz = p.Nz;

      float ndotv = MathF.Max(1e-4f, -ny * CosElevation + nz * SinElevation);
      float g1v = ndotv / (ndotv * (1f - SmithK) + SmithK);
      float f0 = 0.08f * (0.5f * (1f - mat.Accent) + 0.05f * mat.Accent);
      float ao = p.Ao;
      float aoDiff = 0.40f + 0.60f * ao;

      float diffR = AmbR * ao, diffG = AmbG * ao, diffB = AmbB * ao;
      float specR = 0f, specG = 0f, specB = 0f;

      // Key light
      float nl0 = nx * L0x + ny * L0y + nz * L0z;
      if (nl0 > 0f)
      {
        float d = nl0 * aoDiff * InvPi;
        diffR += d * C0r; diffG += d * C0g; diffB += d * C0b;
        float nh0 = nx * H0x + ny * H0y + nz * H0z;
        if (nh0 > 0f)
        {
          float denom = nh0 * nh0 * (Alpha2 - 1f) + 1f;
          float D = Alpha2 * InvPi / (denom * denom);
          float F = f0 + (1f - f0) * VdH0Pow5;
          float g1l = nl0 / (nl0 * (1f - SmithK) + SmithK);
          float s = (D * F * g1v * g1l) / (4f * ndotv) * aoDiff;
          specR += s * C0r; specG += s * C0g; specB += s * C0b;
        }
      }

      // Rim light
      float nl1 = nx * L1x + ny * L1y + nz * L1z;
      if (nl1 > 0f)
      {
        float d = nl1 * aoDiff * InvPi;
        diffR += d * C1r; diffG += d * C1g; diffB += d * C1b;
        float nh1 = nx * H1x + ny * H1y + nz * H1z;
        if (nh1 > 0f)
        {
          float denom = nh1 * nh1 * (Alpha2 - 1f) + 1f;
          float D = Alpha2 * InvPi / (denom * denom);
          float F = f0 + (1f - f0) * VdH1Pow5;
          float g1l = nl1 / (nl1 * (1f - SmithK) + SmithK);
          float s = (D * F * g1v * g1l) / (4f * ndotv) * aoDiff;
          specR += s * C1r; specG += s * C1g; specB += s * C1b;
        }
      }

      // Fill light
      float nl2 = nx * L2x + ny * L2y + nz * L2z;
      if (nl2 > 0f)
      {
        float d = nl2 * aoDiff * InvPi;
        diffR += d * C2r; diffG += d * C2g; diffB += d * C2b;
        float nh2 = nx * H2x + ny * H2y + nz * H2z;
        if (nh2 > 0f)
        {
          float denom = nh2 * nh2 * (Alpha2 - 1f) + 1f;
          float D = Alpha2 * InvPi / (denom * denom);
          float F = f0 + (1f - f0) * VdH2Pow5;
          float g1l = nl2 / (nl2 * (1f - SmithK) + SmithK);
          float s = (D * F * g1v * g1l) / (4f * ndotv) * aoDiff;
          specR += s * C2r; specG += s * C2g; specB += s * C2b;
        }
      }

      // Fresnel environment reflection from the ambient world background
      float omv = 1f - ndotv;
      float omv5 = omv * omv * omv * omv * omv;
      float fEnv = (f0 + (1f - f0) * omv5 * 0.45f) * ao;
      specR += fEnv * AmbR;
      specG += fEnv * AmbG;
      specB += fEnv * AmbB;

      float linR = mat.BaseR * diffR * 0.96f + specR + mat.EmR;
      float linG = mat.BaseG * diffG * 0.96f + specG + mat.EmG;
      float linB = mat.BaseB * diffB * 0.96f + specB + mat.EmB;

      float srgbR = LinToSrgb(linR);
      float srgbG = LinToSrgb(linG);
      float srgbB = LinToSrgb(linB);

      if (mat.Accent > 0.1f)
      {
        srgbR = MathF.Min(1f, srgbR + 0.08f * mat.Accent);
        srgbG = MathF.Min(1f, srgbG + 0.12f * mat.Accent * (0.5f + 0.5f * srgbG));
        srgbB = MathF.Min(1f, srgbB + 0.02f * mat.Accent);
      }

      float nonOrange = 1f - mat.Accent * 0.85f;
      int endVert = p.FirstVertex + p.VertexCount;
      for (int vi = p.FirstVertex; vi < endVert; vi++)
      {
        float r = srgbR, g = srgbG, b = srgbB;
        float sp = spills[vi] * nonOrange;
        if (sp > 1e-4f)
        {
          r = MathF.Min(1f, r + sp * 0.80f * (1f - r * 0.45f));
          g = MathF.Min(1f, g + sp * 0.28f * (1f - g * 0.45f));
          b = MathF.Min(1f, b + sp * 0.02f * (1f - b * 0.45f));
        }
        verts[vi].Color = new Color(
          (byte)(r * 255f + 0.5f),
          (byte)(g * 255f + 0.5f),
          (byte)(b * 255f + 0.5f),
          (byte)255);
      }
    }
  }

  private static float LinToSrgb(float c)
  {
    if (c <= 0f) return 0f;
    if (c >= 1f) return 1f;
    return c <= 0.0031308f ? c * 12.92f : 1.055f * MathF.Pow(c, 1f / 2.4f) - 0.055f;
  }
}
