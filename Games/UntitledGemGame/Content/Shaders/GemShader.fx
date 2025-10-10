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

sampler2D SpriteTextureSampler = sampler_state
{
    Texture = <SpriteTexture>;
};

struct VertexInput {
  float4 Position : POSITION0;
  float4 Color : COLOR0;
  float4 TexCoord : TEXCOORD0;
};
struct PixelInput {
  float4 Position : SV_Position0;
  float4 Color : COLOR0;
  float4 TexCoord : TEXCOORD0;
};

PixelInput SpriteVertexShader(VertexInput v) {
  PixelInput output;

  output.Position = mul(v.Position, view_projection);
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
      return  ResultColor;
}

technique SpriteDrawing
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL SpriteVertexShader();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
};