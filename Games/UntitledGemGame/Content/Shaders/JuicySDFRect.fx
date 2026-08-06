#if OPENGL
#define SV_POSITION POSITION
#define VS_SHADERMODEL vs_3_0
#define PS_SHADERMODEL ps_3_0
#else
#define VS_SHADERMODEL vs_4_0_level_9_1
#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

float4x4 MatrixTransform;
float Time;
float RippleProgress;

struct VertexShaderInput
{
  float4 Position : POSITION0;
  float2 LocalPos : TEXCOORD0;
  float2 HalfSize : TEXCOORD1;
  float  CornerRadius : TEXCOORD2;
  float  Thickness : TEXCOORD3;
  float  RippleProgress : TEXCOORD4;
  float  HoverState : TEXCOORD5;
  float4 CoreColor : COLOR0;
  float4 GlowColor : COLOR1;
};

struct VertexShaderOutput
{
  float4 Position : SV_POSITION;
  float2 LocalPos : TEXCOORD0;
  float2 HalfSize : TEXCOORD1;
  float  CornerRadius : TEXCOORD2;
  float  Thickness : TEXCOORD3;
  float  RippleProgress : TEXCOORD4;
  float  HoverState : TEXCOORD5;
  float4 CoreColor : COLOR0;
  float4 GlowColor : COLOR1;
};

VertexShaderOutput MainVS(VertexShaderInput input)
{
  VertexShaderOutput output;
  output.Position = mul(input.Position, MatrixTransform);
  output.LocalPos = input.LocalPos;
  output.HalfSize = input.HalfSize;
  output.CornerRadius = input.CornerRadius;
  output.Thickness = input.Thickness;
  output.RippleProgress = input.RippleProgress;
  output.HoverState = input.HoverState;
  output.CoreColor = input.CoreColor;
  output.GlowColor = input.GlowColor;
  return output;
}

float4 MainPS(VertexShaderOutput input) : COLOR
{
  // SDF Box Math
  float2 q = abs(input.LocalPos) - input.HalfSize + input.CornerRadius;
  float d = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - input.CornerRadius;
  float dist = input.Thickness > 0.0 ? abs(d) - input.Thickness : d;

  float core = 1.0 - smoothstep(-0.7, 0.7, dist);
  float baseGlow = exp(-max(dist, 0.0) * 0.35);

  float breath = (sin(Time * 3.0) * 0.5) + 2.0;
  float finalGlow = (baseGlow * 0.5) * breath;

  float angle = atan2(input.LocalPos.y, input.LocalPos.x);
  float normalizedAngle = (angle / 6.2831853) + 0.5;
  float cometWave = frac(normalizedAngle - (Time * 0.4));
  float cometIntensity = pow(cometWave, 5.0) * exp(-max(dist, 0.0) * 0.8) * 2.0;

  // Only apply comet intensity if hovered
  finalGlow += (cometIntensity * input.HoverState);

// --- NEW CRISP SDF SHOCKWAVE ---
 /* float ripple = 0.0;
  if (input.RippleProgress > 0.0 && input.RippleProgress < 1.0)
  {
    // 1. Expand along the SDF (d) instead of a circle, perfectly matching the rounded rectangle
    // Starts inside the stroke and bursts outward past the glow
    float startD = -input.Thickness - 2.0;
    float endD = input.Thickness + 16.0;
    float currentWavePos = lerp(startD, endD, input.RippleProgress);

    // 2. Main sharp shockwave line (1.5 pixels wide for a crisp edge)
    float waveDist = abs(d - currentWavePos);
    float mainWave = 1.0 - smoothstep(0.0, 1.5, waveDist);

    // 3. Inner "echo" ring trailing slightly behind for a complex, techy look
    float echoDist = abs(d - (currentWavePos - 4.0));
    float echoWave = (1.0 - smoothstep(0.0, 1.0, echoDist)) * 0.4;

    // 4. Modulate with the angle to give it "energy bands" (like the comet's dynamic feel)
    // This breaks up the solid line into brighter and dimmer segments
    float energyBands = (sin(angle * 12.0) * 0.25) + 0.75;

    // 5. Fade out curve (stays bright during the initial burst, then drops quickly)
    float fade = 1.0 - pow(input.RippleProgress, 2.0);

    ripple = (mainWave + echoWave) * energyBands * fade;
  }*/
// --- GLOWY SDF SHOCKWAVE ---
  float ripple = 0.0;
  if (input.RippleProgress > 0.0 && input.RippleProgress < 1.0)
  {
    float startD = -input.Thickness - 2.0;
    float endD = input.Thickness + 10.0; // Increased to give the glow room to travel
    float currentWavePos = lerp(startD, endD, input.RippleProgress);

    // 1. Main Wave: Solid core + Exponential light scattering (Glow)
    float waveDist = abs(d - currentWavePos);
    float mainCore = 1.0 - smoothstep(0.0, 1.0, waveDist);
    float mainGlow = exp(-waveDist * 0.3) * 0.8; // 0.3 controls how far the glow bleeds
    float mainWave = mainCore + mainGlow;

    // 2. Echo Wave: Slightly further back, dimmer, but still glowing
    float echoDist = abs(d - (currentWavePos - 6.0));
    float echoCore = 1.0 - smoothstep(0.0, 1.0, echoDist);
    float echoGlow = exp(-echoDist * 0.4) * 0.5;
    float echoWave = (echoCore + echoGlow) * 0.4;

    float energyBands = (sin(angle * 12.0) * 0.25) + 0.75;
    
    // Fade out a bit slower so the glow lingers slightly longer
    float fade = 1.0 - pow(input.RippleProgress, 1.5);

    ripple = (mainWave + echoWave) * energyBands * fade;
  }

  /*float4 finalColor = (input.CoreColor * core) + (input.GlowColor * finalGlow);
  finalColor.rgb += (input.CoreColor.rgb * ripple * 2.5);
  finalColor.a = saturate(core + finalGlow + ripple);

  return finalColor;*/
  float4 finalColor = (input.CoreColor * core) + (input.GlowColor * finalGlow);
  
  // Boost the ripple brightness multiplier to make the click pop
  finalColor.rgb += (input.CoreColor.rgb * ripple * 3.5);
  finalColor.a = saturate(core + finalGlow + ripple);

  return finalColor;
}

technique JuicySDF
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
}
