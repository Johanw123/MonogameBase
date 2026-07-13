#if OPENGL
#define SV_POSITION POSITION
#define VS_SHADERMODEL vs_3_0
#define PS_SHADERMODEL ps_3_0
#else
#define VS_SHADERMODEL vs_4_0_level_9_1
#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

Texture2D SpriteTexture;
float4x4 view_projection;
float4x4 mvp;
float grayFactor;
float4 _OutlineColor;
float _OutlineSize;
float _Outline;
float2 TexelSize;

float _DeltaTime;

sampler2D SpriteTextureSampler = sampler_state
{
    Texture = <SpriteTexture>;
    MagFilter = LINEAR;
    MinFilter = LINEAR;
    Mipfilter = LINEAR;

    AddressU = clamp;
    AddressV = clamp;
};

struct VertexInput {
  float4 Position : POSITION0;
  float4 Color : COLOR0;
  float2 TexCoord : TEXCOORD0;
};
struct PixelInput {
  float4 Position : SV_Position0;
  float4 Color : COLOR0;
  float2 TexCoord : TEXCOORD0;
};

PixelInput SpriteVertexShader(VertexInput v) {
  PixelInput output;

  output.Position = mul(v.Position, view_projection);
  output.Color = v.Color;
  output.TexCoord = v.TexCoord;

  return output;
}

float avg_alpha(PixelInput input)
{
  int dist = 1;
  float result = 0.0;
  for (int i = -dist; i<=dist; i++){
    for (int j = -dist; j<=dist; j++){
      result += tex2D(SpriteTextureSampler, input.TexCoord + float2(float(i),float(j))*TexelSize).a;
    }
  }
  float d = (1.0+float(2*dist));
  return result/(d*d);
}

float4 MainPS(PixelInput input) : COLOR
{
    float4 TexColor = tex2D(SpriteTextureSampler, input.TexCoord);
    float4 col = float4(0,0,0,0);

    //input.TexCoord.y *= input.Color.a;

    float4 col2 = avg_alpha(input);

    if (TexColor.a != 0 && input.Color.a < 1.0f)
    {
      //float4 pixelUp = tex2D(SpriteTextureSampler, input.TexCoord + float2(0, TexelSize.y));
      //float4 pixelDown = tex2D(SpriteTextureSampler, input.TexCoord - float2(0, TexelSize.y));
      //float4 pixelRight = tex2D(SpriteTextureSampler, input.TexCoord + float2(TexelSize.x, 0));
      //float4 pixelLeft = tex2D(SpriteTextureSampler, input.TexCoord - float2(TexelSize.x, 0));

      //if ( pixelUp.a != 0 || pixelDown.a != 0  || pixelRight.a != 0  || pixelLeft.a != 0)
      //{
      //  ResultColor.rgba = _OutlineColor;
      //}
      float totalAlpha = 1.0;
      for (int i = 1; i < 3; i++) 
      {
        float4 pixelUp = tex2D(SpriteTextureSampler, input.TexCoord + float2(0, i * TexelSize.y));
        float4 pixelDown = tex2D(SpriteTextureSampler, input.TexCoord - float2(0, i *TexelSize.y));
        float4 pixelRight = tex2D(SpriteTextureSampler, input.TexCoord + float2(i * TexelSize.x, 0));
        float4 pixelLeft = tex2D(SpriteTextureSampler, input.TexCoord - float2(i * TexelSize.x, 0));
        totalAlpha = totalAlpha * pixelUp.a * pixelDown.a * pixelRight.a * pixelLeft.a;
      }  

      if (totalAlpha == 0) {
        //TexColor.rgba = float4(1, 1, 1, 1) * _OutlineColor;
        col = float4(1, 1, 1, 1) * _OutlineColor;
      }
    }

    float pulse = 1.0f;
    if(input.Color.a <= 0.0f)
    {
      pulse = (1.35f - sin(_DeltaTime * 2.0f));
      return col2 * col * pulse * 0.8f + TexColor;
    }

    float a = 1.0f / input.Color.a;
    return (a * a * 0.01f) * col2 * col + TexColor;
    //return TexColor + col * (1.0f - input.Color.a) * ( 1.35f - sin(_DeltaTime * 2.0f));
}

technique SpriteDrawing
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL SpriteVertexShader();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
};
