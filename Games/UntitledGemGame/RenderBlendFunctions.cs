using Microsoft.Xna.Framework.Graphics;

internal static class RenderBlendFunctions
{
  // KNI's WebGL blend-state mapper supports Add/Subtract/ReverseSubtract,
  // but throws for Max. Additive glows can be brighter where they overlap.
#if KNI_WEB
  public const BlendFunction MaxGlow = BlendFunction.Add;
#else
  public const BlendFunction MaxGlow = BlendFunction.Max;
#endif
}
