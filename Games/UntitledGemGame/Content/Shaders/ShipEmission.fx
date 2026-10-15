// Composite a cyan light core and compact halo over prerendered ship hulls.

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

// Emission is premultiplied by silhouette coverage, but its alpha describes
// the entire ship. Derive light coverage from RGB so dark hull pixels stay clear.
sampler TextureSampler : register(s0);
float2 GlowStep;
struct VSOutput
{
    float4 position : SV_Position;
    float4 color : COLOR0;
    float2 texCoord : TEXCOORD0;
};
float4 MainPS(VSOutput input) : COLOR0
{
    float3 core = tex2D(TextureSampler, input.texCoord).rgb;
    float3 halo = core * 0.20;
    halo += tex2D(TextureSampler, input.texCoord + float2(GlowStep.x, 0)).rgb * 0.12;
    halo += tex2D(TextureSampler, input.texCoord - float2(GlowStep.x, 0)).rgb * 0.12;
    halo += tex2D(TextureSampler, input.texCoord + float2(0, GlowStep.y)).rgb * 0.12;
    halo += tex2D(TextureSampler, input.texCoord - float2(0, GlowStep.y)).rgb * 0.12;
    halo += tex2D(TextureSampler, input.texCoord + GlowStep).rgb * 0.08;
    halo += tex2D(TextureSampler, input.texCoord - GlowStep).rgb * 0.08;
    halo += tex2D(TextureSampler, input.texCoord + float2(GlowStep.x, -GlowStep.y)).rgb * 0.08;
    halo += tex2D(TextureSampler, input.texCoord + float2(-GlowStep.x, GlowStep.y)).rgb * 0.08;
    float coverage = saturate(max(core.r, max(core.g, core.b)) * 0.90);
    // The core replaces pale baked trim with cyan; the soft light adds above
    // the hull. Only actual light pixels occlude the existing surface.
    return float4((core * 0.90 + halo * 0.45) * input.color.rgb, coverage);
}
technique ShipEmission
{
    pass Pass1
    {
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
}
