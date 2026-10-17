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
    #define VS_SHADERMODEL vs_4_0
    #define PS_SHADERMODEL ps_4_0
#endif

// VTF particle simulation, inspired by the local XNA and Unity references.
// One particle per texel, point sampled at its centre; no filtering or mipmaps.
// Position: xy = world position, z = remaining life, w = initial life.
// Velocity: xy = world velocity, z = initial diameter, w = packed RGB (24-bit integer).
Texture2D PositionMap;
Texture2D VelocityMap;
sampler2D PositionSampler : register(s0) = sampler_state
{
    Texture = <PositionMap>; MinFilter = Point; MagFilter = Point; MipFilter = None;
    AddressU = Clamp; AddressV = Clamp;
};
sampler2D VelocitySampler : register(s1) = sampler_state
{
    Texture = <VelocityMap>; MinFilter = Point; MagFilter = Point; MipFilter = None;
    AddressU = Clamp; AddressV = Clamp;
};

// Whole float4 uniforms keep DXC/SPIRV-Cross constant layouts consistent across backends.
float4 SpawnRange; // first slot, count, texture side, seed
float4 SpawnPositionVelocity; // position.xy, inherited velocity.zw
float4 SpawnSpeedLifetime; // min/max speed, min/max lifetime
float4 SpawnSizeShape; // min/max diameter, spawn radius, direction
float4 SpawnSpreadColor; // angular spread, packed RGB, motion mode, inner radius
float4 SimulationStep; // seconds, velocity damping, velocity integral, acceleration integral
float4 SimulationAcceleration; // acceleration.xy

struct PassInput
{
    float4 Position : POSITION0;
    float2 UV : TEXCOORD0;
};
struct PassOutput
{
    float4 Position : SV_POSITION;
    float2 UV : TEXCOORD0;
};
PassOutput PassVS(PassInput input)
{
    PassOutput output;
    output.Position = input.Position;
    output.UV = input.UV;
    return output;
}

float Hash(float2 value)
{
    float3 p = frac(float3(value.xyx) * float3(0.1031, 0.1030, 0.0973));
    p += dot(p, p.yzx + 33.33);
    return frac((p.x + p.y) * p.z);
}
float2 SpawnTexel(float2 uv)
{
    float2 cell = floor(uv * SpawnRange.z);
    float id = cell.x + cell.y * SpawnRange.z;
    float offset = id - SpawnRange.x;
    offset += offset < 0.0 ? SpawnRange.z * SpawnRange.z : 0.0;
    clip(SpawnRange.y - offset - 0.5);
    return cell + SpawnRange.w * float2(0.71, 1.37);
}
struct ParticleState
{
    float4 Position : COLOR0;
    float4 Velocity : COLOR1;
};
ParticleState ClearStatePS(PassOutput input)
{
    ParticleState output;
    output.Position = 0;
    output.Velocity = 0;
    return output;
}
ParticleState SpawnStatePS(PassOutput input)
{
    float2 cell = SpawnTexel(input.UV);
    float spawnAngle = Hash(cell + 2.1) * 6.2831853;
    float radius = sqrt(lerp(SpawnSpreadColor.w * SpawnSpreadColor.w,
        SpawnSizeShape.z * SpawnSizeShape.z, Hash(cell + 5.7)));
    float life = lerp(SpawnSpeedLifetime.z, SpawnSpeedLifetime.w, Hash(cell + 8.3));
    float angle = SpawnSizeShape.w + (Hash(cell + 11.9) - 0.5) * SpawnSpreadColor.x;
    if (SpawnSpreadColor.z > 0.5)
        angle += spawnAngle + (SpawnSpreadColor.z < 1.5 ? 0.0 : SpawnSpreadColor.z < 2.5 ? 3.14159265 : 1.57079633);
    float speed = lerp(SpawnSpeedLifetime.x, SpawnSpeedLifetime.y, Hash(cell + 15.1));
    float size = lerp(SpawnSizeShape.x, SpawnSizeShape.y, Hash(cell + 19.3));
    ParticleState output;
    output.Position = float4(SpawnPositionVelocity.xy + float2(cos(spawnAngle), sin(spawnAngle)) * radius, life, life);
    output.Velocity = float4(SpawnPositionVelocity.zw + float2(cos(angle), sin(angle)) * speed, size, SpawnSpreadColor.y);
    return output;
}
ParticleState UpdateStatePS(PassOutput input)
{
    ParticleState output;
    float4 position = tex2D(PositionSampler, input.UV);
    if (position.z <= 0.0 || position.z <= SimulationStep.x)
    {
        output.Position = 0;
        output.Velocity = 0;
        return output;
    }
    float4 velocity = tex2D(VelocitySampler, input.UV);
    // Exact solution of dv/dt = acceleration - drag * velocity, integrated once per frame.
    position.xy += velocity.xy * SimulationStep.z + SimulationAcceleration.xy * SimulationStep.w;
    velocity.xy = velocity.xy * SimulationStep.y + SimulationAcceleration.xy * SimulationStep.z;
    position.z = max(0.0, position.z - SimulationStep.x);
    output.Position = position;
    output.Velocity = velocity;
    return output;
}

float4 BindTexturesPS(PassOutput input) : COLOR0
{
    // A fully clipped triangle uses this pass to bind pixel texture units 0/1.
    // GpuParticleGlRenderer uses those same units for native vertex texture fetch.
    return tex2D(PositionSampler, input.UV) + tex2D(VelocitySampler, input.UV);
}

technique ClearState
{
    pass P0 { VertexShader = compile VS_SHADERMODEL PassVS(); PixelShader = compile PS_SHADERMODEL ClearStatePS(); }
}
technique SpawnState
{
    pass P0 { VertexShader = compile VS_SHADERMODEL PassVS(); PixelShader = compile PS_SHADERMODEL SpawnStatePS(); }
}
technique UpdateState
{
    pass P0 { VertexShader = compile VS_SHADERMODEL PassVS(); PixelShader = compile PS_SHADERMODEL UpdateStatePS(); }
}
technique BindParticleTextures
{
    pass P0 { VertexShader = compile VS_SHADERMODEL PassVS(); PixelShader = compile PS_SHADERMODEL BindTexturesPS(); }
}
