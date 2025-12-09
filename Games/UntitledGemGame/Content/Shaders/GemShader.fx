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
float4x4 view_matrix;
float4x4 mvp;
float4x4 inv_view_matrix;

// float4 _Color;
// float _Radius;

sampler2D SpriteTextureSampler = sampler_state
{
    Texture = <SpriteTexture>;
    MagFilter = LINEAR;
    MinFilter = LINEAR;
    Mipfilter = LINEAR;

    AddressU = clamp;
    AddressV = clamp;
};

//sampler BaseSampler : register(s1)
//{
//  Texture = (BaseTexture);
//  Filter = Linear;
//  AddressU = clamp;
//  AddressV = clamp;
//};

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

  # output.Position = mul(v.Position, view_projection);
  output.Position = v.Position;
  output.Color = v.Color;
  output.TexCoord = v.TexCoord;

  return output;
}

float4 MainPS(PixelInput input) : COLOR
{
    float4 TexColor = tex2D(SpriteTextureSampler,input.TexCoord);
    float4 ResultColor = TexColor;
    float4 Shade = float4(0.5f,0.5f,0.5f,0.5f);
    //check here if a colour is truly "grayscale", otherwise return original colour
    if (TexColor.r == TexColor.g && TexColor.g == TexColor.b)
    {
      //this uses additive blending for colours brighter than middle gray and multiplication for darker colours
          ResultColor = input.Color + (TexColor - Shade) * 2; //if texture colour ranges 0.5..1, output colour is input + texture colour ranging 0..1
          //output colour ranges between input colour and white
          if (TexColor.r < 0.5f)                        //if texture colour ranges 0..0.5, output colour is input multiplied by texture colour ranging 0...1 of input
          { //output colour ranges between black and input colour
              ResultColor = input.Color * (TexColor) * 2;
          }
      }
      //return  ResultColor;
       int width = 18;
       int height = 30;
      float _Distance = 0.8f;
      float4 _Color = float4(0,0,0,2) * 2.2f;
      //float4 _Color = float4(1,0.4f,0.4f,1) * 2.0f;
      float4 _MainTex_TexelSize = float4(1.0 / width, 1.0 / height, width, height);

        // Simple sobel filter for the alpha channel.
        float d = _MainTex_TexelSize.xy * _Distance;

        half a1 = tex2D(SpriteTextureSampler, input.TexCoord + d * float2(-1, -1)).a;
        half a2 = tex2D(SpriteTextureSampler, input.TexCoord + d * float2( 0, -1)).a;
        half a3 = tex2D(SpriteTextureSampler, input.TexCoord + d * float2(+1, -1)).a;

        half a4 = tex2D(SpriteTextureSampler, input.TexCoord + d * float2(-1,  0)).a;
        half a6 = tex2D(SpriteTextureSampler, input.TexCoord + d * float2(+1,  0)).a;

        half a7 = tex2D(SpriteTextureSampler, input.TexCoord + d * float2(-1, +1)).a;
        half a8 = tex2D(SpriteTextureSampler, input.TexCoord + d * float2( 0, +1)).a;
        half a9 = tex2D(SpriteTextureSampler, input.TexCoord + d * float2(+1, +1)).a;

        float gx = - a1 - a2*2 - a3 + a7 + a8*2 + a9;
        float gy = - a1 - a4*2 - a7 + a3 + a6*2 + a9;

        float w = sqrt(gx * gx + gy * gy) / 4;

        // Mix the contour color.
        half4 source = tex2D(SpriteTextureSampler, input.TexCoord);
        return half4(lerp(ResultColor.rgb, _Color.rgb, w), ResultColor.a);


      // int width = 18;
      // int height = 30;
      // float _Radius = 2;
      // float4 _Color = float4(0,0,0,1);
      // float4 _MainTex_TexelSize = float4(1.0 / width, 1.0 / height, width, height);

      // float na = 0;
      // float r = _Radius;

      // for (int nx = -r; nx <= r; nx++)
      // {
      //     for (int ny = -r; ny <= r; ny++)
      //     {
      //         if (nx*nx+ny*ny <= r)
      //         {
      //             float4 nc = tex2D(SpriteTextureSampler, input.TexCoord + float2(_MainTex_TexelSize.x*nx, _MainTex_TexelSize.y*ny));
      //             na+=ceil(nc.a);
      //         }
      //     }
      // }

      // na = clamp(na,0,1);

      // //float4 c = tex2D(SpriteTextureSampler, input.TexCoord);
      // na-=ceil(TexColor.a);

      // float4 outline = lerp(ResultColor, _Color, na);

      // return outline;
}

technique SpriteDrawing
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL SpriteVertexShader();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
};
