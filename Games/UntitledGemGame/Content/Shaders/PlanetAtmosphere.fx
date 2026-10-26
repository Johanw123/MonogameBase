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
    #define VS_SHADERMODEL vs_4_0_level_9_3
    #define PS_SHADERMODEL ps_4_0_level_9_3
#endif

float4x4 view_projection;
struct VertexInput { float4 Position : POSITION0; float4 Color : COLOR0; float2 TexCoord : TEXCOORD0; };
struct PixelInput { float4 Position : SV_Position0; float4 Color : COLOR0; float2 TexCoord : TEXCOORD0; };
PixelInput SpriteVertexShader(VertexInput v)
{
    PixelInput o;
    o.Position = mul(v.Position, view_projection);
    o.Color = v.Color;
    o.TexCoord = v.TexCoord;
    return o;
}

// One small planet quad. Surface light travels with the rotating sphere;
// the atmospheric scattering is view-dependent and stays on its silhouette.
float4 Hits[8];       // xyz: view-space unit normal, w: normalized lifetime
float4 HitColors[8];  // rgb: light color, w: energy (zero = inactive)
float3 AtmosphereColor;
float AtmosphereStrength;
float AtmosphereWidth;
float AuroraStrength;
float Time;
float ImpactLightStrength;
float ImpactLightRadius;
float ImpactRippleStrength;
float HitCount;

float4 MainPS(PixelInput input) : COLOR0
{
    float2 p = (input.TexCoord - 0.5) * 3.0;
    float r = length(p);
    float3 color = 0;
    if (AtmosphereStrength > 0)
    {
        float outside = max(0, r - 1.0);
        float inside = max(0, 1.0 - r);
        float limb = exp(-outside / max(AtmosphereWidth, 0.001)) * exp(-inside * 60.0);
        limb *= 1.0 - smoothstep(1.35, 1.49, r);
        float2 direction = p / max(r, 0.001);
        float sun = 0.38 + 0.62 * saturate(dot(direction, float2(-0.65,-0.76)) * 0.5 + 0.5);
        // Cartesian harmonics avoid a discontinuity where an angular UV would wrap.
        float bands = sin(direction.x * 18.0 + direction.y * 11.0 + Time);
        bands += sin(direction.y * 31.0 - direction.x * 7.0 - Time * 0.71) * 0.45;
        float wisp = pow(saturate(bands * 0.4 + 0.45), 3.0);
        float curtain = exp(-abs(outside - 0.045 - wisp * 0.06) / max(AtmosphereWidth * 0.5, 0.001));
        curtain *= smoothstep(0.995, 1.02, r) * (1.0 - smoothstep(1.25, 1.45, r));
        color = AtmosphereColor * limb * sun * AtmosphereStrength;
        color += lerp(AtmosphereColor, float3(0.12,0.85,1.0), 0.55)
            * curtain * wisp * AuroraStrength * AtmosphereStrength;
    }
    if (r < 1.0 && HitCount > 0)
    {
        float3 normal = float3(p, sqrt(saturate(1.0 - dot(p,p))));
        float3 light = 0;
        for (int i = 0; i < 8; i++)
        {
            if (i < HitCount && HitColors[i].w > 0)
            {
                // Chord distance on the sphere makes ripples foreshorten at the limb.
                float distance = length(normal - Hits[i].xyz);
                float age = Hits[i].w;
                float energy = HitColors[i].w;
                float visibility = smoothstep(-0.12, 0.15, Hits[i].z);
                float flash = exp(-distance * distance / (ImpactLightRadius * ImpactLightRadius))
                    * exp(-age * 7.0) * (1.0 - age);
                float ring = exp(-abs(distance - age * 1.2) * 85.0) * (1.0 - age) * (1.0 - age);
                // Continuous laser contacts illuminate the surface without constant rings.
                ring *= smoothstep(0.5, 1.0, energy) * smoothstep(0.02, 0.08, age);
                light += HitColors[i].rgb * energy * visibility
                    * (flash * ImpactLightStrength + ring * ImpactRippleStrength);
            }
        }
        // Bound overlapping explosions without a clipped white disc.
        color += light / (1.0 + light) * smoothstep(1.0, 0.99, r);
    }
    return float4(color * input.Color.rgb, input.Color.a);
}
technique Atmosphere
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL SpriteVertexShader();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
}
