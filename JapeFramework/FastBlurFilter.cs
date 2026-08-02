using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

public class FastBlurFilter
{
    private Effect _blurEffect;
    private RenderTarget2D _renderTarget1;
    private RenderTarget2D _renderTarget2;
    private GraphicsDevice _graphicsDevice;

    // Change this to 1 for full res, 2 for half res, 4 for quarter res (much faster)
    private const int DownsampleScale = 2; 

    public FastBlurFilter(GraphicsDevice graphicsDevice, Effect blurEffect)
    {
        _graphicsDevice = graphicsDevice;
        _blurEffect = blurEffect;
        
        int width = graphicsDevice.Viewport.Width / DownsampleScale;
        int height = graphicsDevice.Viewport.Height / DownsampleScale;

        // Initialize two smaller render targets for the ping-pong passes
        _renderTarget1 = new RenderTarget2D(graphicsDevice, width, height, false, SurfaceFormat.Color, DepthFormat.None);
        _renderTarget2 = new RenderTarget2D(graphicsDevice, width, height, false, SurfaceFormat.Color, DepthFormat.None);
        
        // Pass the texel size to the shader
        _blurEffect.Parameters["TexelSize"].SetValue(new Vector2(1f / width, 1f / height));
    }

    public void Draw(SpriteBatch spriteBatch, RenderTarget2D sourceTarget)
    {
        // PASS 1: Horizontal Blur (Draw source into RT1)
        _graphicsDevice.SetRenderTarget(_renderTarget1);
        _graphicsDevice.Clear(Color.Black);
        
        _blurEffect.CurrentTechnique = _blurEffect.Techniques["HorizontalBlur"];
        
        spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.LinearClamp, null, null, _blurEffect);
        // By drawing a full-screen target into a smaller one, MonoGame automatically downsamples it
        spriteBatch.Draw(sourceTarget, _renderTarget1.Bounds, Color.White);
        spriteBatch.End();

        // PASS 2: Vertical Blur (Draw RT1 into RT2)
        _graphicsDevice.SetRenderTarget(_renderTarget2);
        _graphicsDevice.Clear(Color.Black);
        
        _blurEffect.CurrentTechnique = _blurEffect.Techniques["VerticalBlur"];
        
        spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.LinearClamp, null, null, _blurEffect);
        spriteBatch.Draw(_renderTarget1, _renderTarget2.Bounds, Color.White);
        spriteBatch.End();

        // FINAL PASS: Draw the blurred result (RT2) back to the main screen/target
        _graphicsDevice.SetRenderTarget(null); // Or set to your final composite target
        
        // No effect needed here, we just scale the blurred image back up
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.LinearClamp);
        spriteBatch.Draw(_renderTarget2, _graphicsDevice.Viewport.Bounds, Color.White);
        spriteBatch.End();
    }
}
