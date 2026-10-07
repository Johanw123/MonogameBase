# Apos.Shapes

Source of the official [v0.6.8](https://github.com/Apostolique/Apos.Shapes/tree/v0.6.8) release
(commit `e3b867bfe176126b2ef38547d1c89e12c9bec671`): `Source/Gradient.cs`, `Source/ShapeBatch.cs`,
`Source/ShapeVertex.cs` and the upstream MIT `LICENSE`. Apos.Shapes is copyright Jean-David Moisan.
The game keeps its own copy of the matching shader in `Content/Shaders/Shapes/apos-shapes.fx`.

Local changes, all in `ShapeBatch.cs` and marked `UntitledGemGame:`:

- `Flush()` uploads only the batch's vertices with `SetDataOptions.Discard` instead of
  `SetData(_vertices)`, which rewrote the whole vertex array (up to its largest-ever size) in place
  on every `End()` and made the driver wait for the GPU. Upstream made the same change in 0.7.0.
  Measured on the endgame benchmark scene: 16.6 ms to 12.1 ms per frame.
- `Flush()` caches the `view_projection` effect parameter instead of looking it up by name.
- Solid colours (`Gradient.Shape.None`) skip the conversion of their gradient points into the
  shape's local space: the shader never reads them, so the pixels are the same.
- `DrawLine` normalises its direction once instead of four times, and `DrawLine` and `DrawCircle`
  build one vertex and copy it to the four corners. The game draws thousands of lines a frame.

Upgrading to 0.7 or later means adapting drawing calls whose signatures changed (rounded corners
became `CornerRadii`, among others) and replacing the shader with that version's.
