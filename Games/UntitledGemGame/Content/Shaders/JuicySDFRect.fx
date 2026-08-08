#if OPENGL
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

  // COMET
  /*float angle = atan2(input.LocalPos.y, input.LocalPos.x);
  float normalizedAngle = (angle / 6.2831853) + 0.5;
  float cometWave = frac(normalizedAngle - (Time * 0.4));
  float cometIntensity = pow(cometWave, 5.0) * exp(-max(dist, 0.0) * 0.8) * 2.0;
  finalGlow += (cometIntensity * input.HoverState);
*/

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

// --- UNIFORM PIXEL-PERFECT EMITTING PARTICLES ---
  float particleEffect = 0.0;
  
  float EnableParticles = 1.0 * input.HoverState; 
  float pSpacingX = 14.0;      // Pixel spacing between streams along the border
  float pSpacingY = 12.0;      // Pixel spacing along outward travel path
  float pSpeed = 16.0;         // Outward particle speed (pixels per second)
  float pMaxDist = 28.0;       // Max distance particles travel before fully vanishing

  if (EnableParticles > 0.0 && d > 0.0 && d < pMaxDist)
  {
    // 1. Calculate exact arc-length S along the perimeter in real pixel units
    float2 b = max(input.HalfSize - input.CornerRadius, 0.0);
    float2 pC = clamp(input.LocalPos, -b, b);
    float2 dC = input.LocalPos - pC;
    float cornerArc = 1.57079632 * input.CornerRadius;
    float S = 0.0;

    if (dC.x > 0.0 && dC.y > 0.0) {
      S = 2.0 * b.x + (1.57079632 - atan2(dC.y, dC.x)) * input.CornerRadius;
    } else if (dC.x > 0.0 && dC.y < 0.0) {
      S = 2.0 * b.x + cornerArc + 2.0 * b.y + atan2(-dC.y, dC.x) * input.CornerRadius;
    } else if (dC.x < 0.0 && dC.y < 0.0) {
      S = 4.0 * b.x + 2.0 * cornerArc + 2.0 * b.y + (1.57079632 - atan2(-dC.y, -dC.x)) * input.CornerRadius;
    } else if (dC.x < 0.0 && dC.y > 0.0) {
      S = 4.0 * b.x + 3.0 * cornerArc + 2.0 * b.y + atan2(dC.y, -dC.x) * input.CornerRadius;
    } else if (pC.y == b.y) {
      S = pC.x + b.x;
    } else if (pC.x == b.x) {
      S = 2.0 * b.x + cornerArc + (b.y - pC.y);
    } else if (pC.y == -b.y) {
      S = 2.0 * b.x + cornerArc + 2.0 * b.y + (b.x - pC.x);
    } else {
      S = 4.0 * b.x + 3.0 * cornerArc + 2.0 * b.y + (pC.y + b.y);
    }

    // 2. Map coordinates to a 1:1 isotropic grid (pixels)
    // NOTE: Changed + to - below to make particles travel OUTWARD
    float2 pGridCoords = float2(S / pSpacingX, (d - Time * pSpeed) / pSpacingY);
    float2 pId = floor(pGridCoords);
    float2 pCellLocal = frac(pGridCoords) - 0.5;

    // 3. Convert local cell space into actual screen pixel offsets
    float2 pPixelOffset = pCellLocal * float2(pSpacingX, pSpacingY);

    // 4. Jitter particle origins per cell
    float hash1 = frac(sin(dot(pId, float2(12.9898, 78.233))) * 43758.5453);
    float hash2 = frac(sin(dot(pId, float2(39.346, 11.135))) * 22462.123);
    pPixelOffset += (float2(hash1, hash2) - 0.5) * float2(pSpacingX * 0.5, pSpacingY * 0.5);

    // 5. Measure distance in pixels (guarantees round dots everywhere)
    float pDistPx = length(pPixelOffset);

    // 6. Hardcoded dot radius in pixels (0.8px to 1.6px)
    float pRadiusPx = 0.8 + hash1 * 0.8;
    float pCore = smoothstep(pRadiusPx, 0.0, pDistPx);
    float pGlow = exp(-pDistPx * 0.7) * 0.4;

    float twinkle = (sin(Time * 8.0 + hash1 * 60.0) * 0.4 + 0.6);
    float particle = (pCore + pGlow) * twinkle * hash1;

    // 7. Distance & Lifetime Dissolve
    float lifeProgress = saturate(d / pMaxDist);
    float fadeOut = pow(1.0 - lifeProgress, 2.5); // Accelerated fadeout as distance increases
    float fadeIn = smoothstep(0.0, 2.5, d);        // Smooth birth line at the border

    particleEffect = particle * fadeOut * fadeIn * EnableParticles;
  }


  float4 finalColor = (input.CoreColor * core) + (input.GlowColor * finalGlow);

  finalColor.rgb += (input.GlowColor.rgb * particleEffect * 4.0);
  
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
