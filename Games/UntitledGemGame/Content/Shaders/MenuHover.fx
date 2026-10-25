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

// The main menu's highlight, drawn as a ship terminal selection: the bar is scanned in
// from the left, brackets lock on to it and a click fires a confirm pulse.
// All lengths are in target pixels; Scale converts the 4K canvas units the look is tuned in.

float4x4 view_projection;
float2 Size;        // the button
float2 Pad;         // drawn around the button for the brackets and the click ring
float Scale;        // target pixels per canvas unit
float Intro;        // seconds since the highlight began
float Hover;        // 1 while highlighted, fading to 0 after
float Press;        // 0..1 while held down
float Click;        // seconds since the last click
float Time;
float4 Accent;
float2 TexSize;     // the bar artwork, a nine-slice
float Frame;        // its frame in texels

sampler2D Art : register(s0);

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

// Gum draws nine-slice corners at their texel size; only the middle stretches.
float NineSlice(float p, float size, float tex)
{
    float frame = min(Frame * Scale, size * 0.5);
    float middle = Frame + (p - frame) / max(size - 2.0 * frame, 1.0) * (tex - 2.0 * Frame);
    float texel = p < frame ? p / Scale : (p > size - frame ? tex - (size - p) / Scale : middle);
    return texel / tex;
}

float4 SampleArt(float2 p)
{
    float2 q = clamp(p, 0.0, Size);
    return tex2D(Art, float2(NineSlice(q.x, Size.x, TexSize.x), NineSlice(q.y, Size.y, TexSize.y)));
}

float4 MainPS(PixelInput input) : COLOR0
{
    float2 p = input.TexCoord * (Size + 2.0 * Pad) - Pad;
    float2 uv = p / Size;
    float inside = step(0.0, p.x) * step(p.x, Size.x) * step(0.0, p.y) * step(p.y, Size.y);

    float intro = saturate(Intro / 0.32);
    float introEase = 1.0 - pow(1.0 - intro, 4.0);
    float fade = Hover * Hover * (3.0 - 2.0 * Hover);
    float burst = exp(-Click * 9.0);

    // The scan front sweeps right on entry and retracts left when the highlight leaves.
    // Odd rows trail a little, so the bar builds up like an interlaced display.
    float row = floor(p.y / (3.0 * Scale));
    float lag = fmod(row, 2.0) * 0.12 * Size.x * (1.0 - intro);
    float front = (Size.x + 24.0 * Scale) * introEase * fade - lag;
    float reveal = saturate((front - p.x) / (8.0 * Scale)) * inside;

    // A click tears the bar into sideways bands with split colour channels.
    float band = floor(p.y / (7.0 * Scale));
    float tear = (Hash(band + floor(Time * 60.0)) - 0.5) * 36.0 * Scale * burst * step(Click, 0.22);
    float split = (2.0 + 9.0 * burst) * Scale;
    float4 art = SampleArt(float2(p.x - tear, p.y));
    art.r = SampleArt(float2(p.x - tear + split, p.y)).r;
    art.b = SampleArt(float2(p.x - tear - split, p.y)).b;

    float scan = 0.9 + 0.1 * sin(p.y / Scale * 0.785398 - Time * 6.0);
    float shade = scan * (1.0 - 0.22 * Press);
    float3 white = float3(0.88, 0.97, 1.0);
    float3 core = lerp(Accent.rgb, white, 0.55);
    float3 artColor = lerp(art.rgb * shade, white, burst * 0.65);
    float artAlpha = art.a * reveal;
    float4 color = float4(artColor * artAlpha, artAlpha);

    // The leading edge of the scan, bright while it travels.
    float edge = exp(-abs(p.x - front) / (3.0 * Scale)) * (1.0 - intro) * inside * fade;

    // A glass reflection crosses the bar now and then; a slow pulse runs along its left rim.
    float cycle = Time / 3.4;
    float phase = frac(cycle) * 1.95 - 0.35;
    float diagonal = uv.x + uv.y * 0.15;
    float sweep = exp2(-220.0 * (diagonal - phase) * (diagonal - phase)) * pow(saturate(1.0 - uv.x), 2.0);
    float rim = exp2(-p.x / Scale * 0.6) * (0.25 + 0.15 * sin(Time * 4.0)) * (1.0 + Press);

    // A click floods the whole button, even where the artwork has faded out.
    float flood = burst * (0.55 - 0.35 * uv.x) + Press * 0.08 * (1.0 - uv.x);

    float glow = (edge * 0.9 + (sweep * 0.3 + rim + flood) * reveal) * Accent.a;
    color.rgb += core * glow;
    color.a = saturate(color.a + glow * 0.35);

    // Targeting brackets close in on the button, flicker as they lock and kick on a click.
    float lockT = saturate(Intro / 0.24);
    float back = lockT - 1.0;
    float lockEase = 1.0 + 2.7 * back * back * back + 1.7 * back * back;
    float gap = (lerp(30.0, 7.0, lockEase) + 16.0 * burst - 3.0 * Press) * Scale;
    float2 lo = float2(-gap, -gap), hi = Size + float2(gap, gap);
    float2 corner = float2(min(p.x - lo.x, hi.x - p.x), min(p.y - lo.y, hi.y - p.y));
    float thick = 2.0 * Scale, arm = 18.0 * Scale;
    float vertical = (1.0 - smoothstep(thick * 0.5, thick, abs(corner.x))) * step(corner.y, arm) * step(-thick, corner.y);
    float horizontal = (1.0 - smoothstep(thick * 0.5, thick, abs(corner.y))) * step(corner.x, arm) * step(-thick, corner.x);
    float flicker = lockT < 1.0 ? step(0.35, Hash(floor(Intro * 45.0))) : 1.0;
    float bracket = saturate(vertical + horizontal) * flicker * fade * Accent.a * (0.8 + 0.2 * burst);
    color = lerp(color, float4(core, 1.0), bracket);

    // The confirm pulse: a ring that leaves the button's outline and fades as it spreads.
    float2 d = abs(p - Size * 0.5) - Size * 0.5;
    float outside = length(max(d, 0.0)) + min(max(d.x, d.y), 0.0);
    float radius = Click * 240.0 * Scale;
    float ring = exp(-abs(outside - radius) / (2.5 * Scale)) * saturate(1.0 - Click / 0.28) * step(0.0, outside);
    color.rgb += core * ring * Accent.a;
    color.a = saturate(color.a + ring * 0.5 * Accent.a);

    return color;
}

technique MenuHover
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
}
