
#if OPENGL
#define SV_POSITION POSITION
#define VS_SHADERMODEL vs_3_0
#define PS_SHADERMODEL ps_3_0
#else
#define VS_SHADERMODEL vs_4_0_level_9_1
#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

Texture2D SpriteTexture;
Texture2D DepthTexture;
float4x4 view_projection;
float2 u_mouse;

sampler2D SpriteTextureSampler = sampler_state
{
    Texture = <SpriteTexture>;
};

 sampler2D DepthTextureSampler = sampler_state
 {
     Texture = <DepthTexture>;
//     MagFilter = LINEAR;
//     MinFilter = LINEAR;
//     Mipfilter = LINEAR;

//     AddressU = clamp;
//     AddressV = clamp;
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
  //output.Position = v.Position;
  output.Color = v.Color;
  output.TexCoord = v.TexCoord;

  return output;
}

float4 MainPS(PixelInput input) : COLOR
{
    float4 TexColor = tex2D(SpriteTextureSampler,input.TexCoord);
    //float4 DepthColor = tex2D(DepthTextureSampler,input.TexCoord);

    //float parallaxMult = DepthColor.r;
    //float2 parallax = (u_mouse) * parallaxMult;
    //float4 TexColor = tex2D(SpriteTextureSampler, input.TexCoord + parallax);
    

    // 	 Vec4 depthDistortion = texture2D(u_mapImage, v_texcoord);
		//  float parallaxMult = depthDistortion.r;

		//  vec2 parallax = (u_mouse) * parallaxMult;

		//  vec4 original = texture2D(u_originalImage, (v_texcoord + parallax));
		//  gl_FragColor = original;


    return TexColor;
}
technique SpriteDrawing
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL SpriteVertexShader();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
};


//WINEPREFIX=$HOME/.winemonogame wine mgfxc /Users/johanwangsell/Dev/MonogameBase/Games/UntitledGemGame/Content/Shaders/BackgroundShader.fx /Users/johanwangsell/Dev/MonogameBase/Games/UntitledGemGame/Content/Shaders/GeneratedShaders/BackgroundShader.mgfx /Profile:OpenGL
