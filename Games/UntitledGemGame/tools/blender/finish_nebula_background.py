"""Compress the rendered sky's highlights before its 56/255 in-game tint.

Run once after export_nebula_background.py, before building content. This
keeps dim cloud details visible without letting bright cloud cores bloom.
"""
from pathlib import Path
from PIL import Image

path = Path(__file__).resolve().parents[2] / 'Content/Textures/Backgrounds/nebula-3.png'
image = Image.open(path).convert('RGB')
curve = [round(255 * value / (value + 32)) for value in range(256)]
image.point(curve * 3).save(path)
print('Finished nebula highlights:', path)
