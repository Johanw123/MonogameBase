#if __KNIFX__
    #define SV_POSITION SV_Position
    #define VS_SHADERMODEL vs_4_0
    #define PS_SHADERMODEL ps_4_0
#elif OPENGL
    #define SV_POSITION POSITION
    #define VS_SHADERMODEL vs_3_0
    #define PS_SHADERMODEL ps_3_0
#elif VULKAN
    #define SV_POSITION SV_Position
    #define VS_SHADERMODEL vs_6_0
    #define PS_SHADERMODEL ps_6_0
#else
    #define SV_POSITION SV_Position
    #define VS_SHADERMODEL vs_4_0_level_9_1
    #define PS_SHADERMODEL ps_4_0_level_9_1
#endif

// Changes the page of the title screen's left column like a ship terminal: the old page
// tears into glitching bands and drops out, then a scan beam writes the next one in.
// Page holds the column as Gum drew it (premultiplied by its blend), backdrop excluded.

float4x4 view_projection;
float2 Resolution;  // target pixels
float Scale;        // target pixels per canvas unit
float4 Region;      // the page's content in target pixels: left, top, right, bottom
float Progress;     // 0..1 through the current half
float Incoming;     // 0 = old page powering down, 1 = new page scanning in
float Time;
float3 Accent;

sampler2D Page : register(s0);

struct VertexInput
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 TexCoord : TEXCOORD0;
};

struct PixelInput
{
    float4 Position : SV_POSITION;
    float2 TexCoord : TEXCOORD0;
};

PixelInput MainVS(VertexInput input)
{
    PixelInput output;
    output.Position = mul(input.Position, view_projection);
    output.TexCoord = input.TexCoord;
    return output;
}

float Hash(float n)
{
    return frac(sin(n * 12.9898) * 43758.5453);
}

// The page shifted sideways, with its red and blue channels pulled apart.
float4 Torn(float2 uv, float shift, float split)
{
    float2 o = float2(shift / Resolution.x, 0.0);
    float2 s = float2(split / Resolution.x, 0.0);
    float4 c = tex2D(Page, uv + o);
    c.r = tex2D(Page, uv + o + s).r;
    c.b = tex2D(Page, uv + o - s).b;
    return c;
}

float4 PowerDown(float2 uv, float2 px, float p)
{
    // Bands of uneven height slide apart, lose their colour to the accent and blink out.
    float coarse = floor(px.y / (36.0 * Scale));
    float band = floor(px.y / ((6.0 + 18.0 * Hash(coarse)) * Scale)) + coarse * 7.0;
    float jolt = Hash(band + floor(Time * 50.0)) - 0.5;
    float4 c = Torn(uv, jolt * 90.0 * Scale * p * p, 12.0 * Scale * p);
    c.rgb = lerp(c.rgb, Accent * c.a, p * 0.7);
    c.rgb += Accent * c.a * (1.0 - p) * 0.18;
    float gone = step(Hash(band * 1.7 + 3.1), p * 1.3 - 0.15);
    float lines = step(frac(px.y / (4.0 * Scale)), 1.0 - p * 0.75);
    return c * (1.0 - gone) * lines * (1.0 - p * p * 0.5);
}

float4 ScanIn(float2 uv, float2 px, float p)
{
    float e = 1.0 - pow(1.0 - p, 3.0);
    float top = Region.y - 90.0 * Scale;
    float beam = top + (Region.w + 90.0 * Scale - top) * e;
    float row = floor(px.y / (4.0 * Scale));
    float lag = fmod(row, 2.0) * 30.0 * Scale * (1.0 - p);
    float d = beam - lag - px.y;
    float written = smoothstep(-2.0 * Scale, 2.0 * Scale, d);

    // Just behind the beam the page is still settling: overexposed, jittering and split.
    float wake = exp(-max(d, 0.0) / (80.0 * Scale)) * written;
    float jitter = (Hash(row + floor(Time * 45.0)) - 0.5) * 34.0 * Scale * wake;
    float4 c = Torn(uv, jitter, (6.0 * wake + 3.0 * (1.0 - p)) * Scale);
    c.rgb = lerp(c.rgb, c.a * float3(1.0, 1.0, 1.0), wake * 0.3);
    c.rgb += Accent * c.a * wake * 0.8;

    float scan = 1.0 - 0.2 * (1.0 - p) * (0.5 + 0.5 * sin(px.y / Scale * 1.047198));
    float flicker = lerp(1.0, 0.78 + 0.22 * Hash(floor(Time * 40.0)), 1.0 - p);
    c *= written * scan * flicker;

    // Not yet written: a faint wireframe of the page in every third row.
    float ghost = tex2D(Page, uv).a * 0.08 * (1.0 - p) * step(fmod(row, 3.0), 0.5) * (1.0 - written);
    c.rgb += Accent * ghost;
    c.a += ghost * 0.5;

    // The beam spans the column and fades out once it has passed the content.
    float across = smoothstep(Region.x - 120.0 * Scale, Region.x, px.x)
        * (1.0 - smoothstep(Region.z, Region.z + 120.0 * Scale, px.x));
    float glow = exp(-abs(px.y - beam) / (2.5 * Scale)) + 0.35 * exp(-abs(px.y - beam) / (22.0 * Scale));
    glow *= across * (1.0 - smoothstep(Region.w - 20.0 * Scale, Region.w + 70.0 * Scale, beam));
    c.rgb += lerp(Accent, float3(1.0, 1.0, 1.0), 0.45) * glow;
    c.a = saturate(c.a + glow * 0.4);
    return c;
}

float4 MainPS(PixelInput input) : COLOR0
{
    float2 px = input.TexCoord * Resolution;
    return Incoming > 0.5 ? ScanIn(input.TexCoord, px, Progress) : PowerDown(input.TexCoord, px, Progress);
}

technique MenuTransition
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
}
