"""Animated end cards (call to action) for fullscreen shorts.

Styles (end_card "style"):
  pop    words bounce in one by one, then breathe gently
  slam   the title drops in big and lands with a shockwave ring and a short shake
  type   typewriter with a caret, then one light sweep
  shine  the title fades in and a light band sweeps across it now and then
  rise   the title floats up out of a blur
All of them fade in a dim over the gameplay, then the logo (above) and the
sub line (below). Rendered frame by frame into an alpha clip.
"""
from __future__ import annotations

import math
import re
import subprocess
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageFont

from common import FONT_BOLD, LOGO

ACCENT = (115, 232, 235)
WHITE = (255, 255, 255)
OUTLINE = (5, 10, 18)
STYLES = ('pop', 'slam', 'type', 'shine', 'rise')


def ease_out_back(x: float, overshoot: float = 1.7) -> float:
    x = min(max(x, 0.0), 1.0) - 1
    return 1 + (overshoot + 1) * x ** 3 + overshoot * x ** 2


def ease_out(x: float) -> float:
    x = min(max(x, 0.0), 1.0)
    return 1 - (1 - x) ** 3


def clamp01(x: float) -> float:
    return min(max(x, 0.0), 1.0)


def styled_text(text: str, size: int, color=WHITE) -> Image.Image:
    """One word or line with outline and soft shadow, cropped with a margin."""
    font = ImageFont.truetype(FONT_BOLD, size)
    stroke = max(3, size // 11)
    left, top, right, bottom = font.getbbox(text, stroke_width=stroke)
    pad = size // 3
    image = Image.new('RGBA', (right - left + 2 * pad, bottom - top + 2 * pad))
    ImageDraw.Draw(image).text((pad - left, pad - top), text, font=font, fill=color, stroke_width=stroke,
                               stroke_fill=OUTLINE)
    shadow = Image.new('RGBA', image.size, (0, 0, 0, 0))
    shadow.putalpha(image.getchannel('A').filter(ImageFilter.GaussianBlur(size // 7)).point(lambda a: int(a * 0.75)))
    return Image.alpha_composite(shadow, image)


def parse_words(title: str) -> list[list[tuple[str, bool]]]:
    """Lines of (word, accented); *word* is accented."""
    lines = []
    for line in title.split('\n'):
        words = []
        for word in line.split():
            accent = word.startswith('*') and word.endswith('*')
            words.append((word.strip('*'), accent))
        lines.append(words)
    return lines


class Layout:
    """Title words placed in the middle of the frame; logo above, sub line below."""

    def __init__(self, spec: dict, w: int, h: int):
        self.w, self.h = w, h
        lines = parse_words(spec.get('title', 'WISHLIST\nNOW'))
        size = int(spec.get('size', 150) * w / 1080)
        font = ImageFont.truetype(FONT_BOLD, size)
        space = font.getlength(' ')
        while size > 40 and max(sum(font.getlength(word) for word, _ in line) + space * (len(line) - 1)
                                for line in lines) > w * 0.86:
            size -= 6
            font = ImageFont.truetype(FONT_BOLD, size)
            space = font.getlength(' ')
        line_h = int(size * 1.1)
        top = int(h * spec.get('title_y', 0.42)) - line_h * len(lines) // 2
        self.words = []  # (image, centre x, centre y, index)
        for row, line in enumerate(lines):
            width = sum(font.getlength(word) for word, _ in line) + space * (len(line) - 1)
            x = (w - width) / 2
            for word, accent in line:
                image = styled_text(word, size, ACCENT if accent else WHITE)
                advance = font.getlength(word)
                self.words.append((image, x + advance / 2, top + row * line_h + line_h / 2, len(self.words)))
                x += advance + space
        self.title_bottom = top + line_h * len(lines)
        logo = Image.open(LOGO).convert('RGBA')
        logo = logo.crop(logo.getbbox())
        logo.thumbnail((int(w * spec.get('logo_width', 0.62)), int(h * 0.12)), Image.Resampling.LANCZOS)
        self.logo = logo if spec.get('logo', True) else None
        self.logo_y = top - int(h * 0.05) - logo.height
        sub = spec.get('sub', '*Beyond the Belt* on Steam')
        self.sub = None
        if sub:
            parts = [(p.strip('*'), p.startswith('*')) for p in re.split(r'(\*[^*]+\*)', sub) if p]
            # Render the sub line as one image with an accented part.
            sub_size = int(size * 0.42)
            sub_font = ImageFont.truetype(FONT_BOLD, sub_size)
            text = ''.join(t for t, _ in parts)
            stroke = max(2, sub_size // 10)
            left, top_, right, bottom = sub_font.getbbox(text, stroke_width=stroke)
            pad = sub_size // 2
            image = Image.new('RGBA', (right - left + 2 * pad, bottom - top_ + 2 * pad))
            draw = ImageDraw.Draw(image)
            x = pad - left
            for t, accent in parts:
                draw.text((x, pad - top_), t, font=sub_font, fill=ACCENT if accent else WHITE, stroke_width=stroke,
                          stroke_fill=OUTLINE)
                x += sub_font.getlength(t)
            self.sub = image
        self.sub_y = self.title_bottom + int(h * 0.035)


def paste_scaled(canvas: Image.Image, image: Image.Image, cx: float, cy: float, scale: float, alpha: float) -> None:
    if alpha <= 0.003 or scale <= 0.01:
        return
    if abs(scale - 1) > 0.002:
        image = image.resize((max(1, int(image.width * scale)), max(1, int(image.height * scale))), Image.Resampling.BICUBIC)
    if alpha < 0.999:
        image = image.copy()
        image.putalpha(image.getchannel('A').point(lambda a: int(a * alpha)))
    canvas.alpha_composite(image, (int(cx - image.width / 2), int(cy - image.height / 2)))


def sweep(canvas: Image.Image, mask_source: Image.Image, progress: float, strength: float = 0.55) -> None:
    """A diagonal light band across the title pixels (mask_source alpha), progress 0..1."""
    if not 0 < progress < 1:
        return
    w, h = mask_source.size
    band = Image.new('L', (w, h), 0)
    draw = ImageDraw.Draw(band)
    centre = -0.3 * w + progress * 1.6 * w
    width = w * 0.08
    for i in range(-3, 4):
        alpha = int(255 * strength * math.exp(-(i / 2.2) ** 2))
        x = centre + i * width / 3
        draw.polygon([(x - width / 6, 0), (x + width / 6, 0), (x + width / 6 - h * 0.35, h), (x - width / 6 - h * 0.35, h)],
                     fill=alpha)
    title_alpha = mask_source.getchannel('A').point(lambda a: 255 if a > 200 else 0)
    light = Image.new('RGBA', (w, h), (255, 255, 255, 0))
    light.putalpha(Image.composite(band, Image.new('L', (w, h), 0), title_alpha))
    canvas.alpha_composite(light)


def ring(canvas: Image.Image, cx: float, cy: float, radius: float, alpha: float, width: int) -> None:
    if alpha <= 0:
        return
    layer = Image.new('RGBA', canvas.size)
    ImageDraw.Draw(layer).ellipse((cx - radius, cy - radius, cx + radius, cy + radius),
                                  outline=ACCENT + (int(255 * alpha),), width=width)
    canvas.alpha_composite(layer.filter(ImageFilter.GaussianBlur(2)))


def frame(layout: Layout, style: str, t: float, dim: float) -> Image.Image:
    w, h = layout.w, layout.h
    canvas = Image.new('RGBA', (w, h), (2, 6, 12, int(255 * dim * ease_out(t / 0.35))))
    # Title first (its timing drives the rest).
    words_layer = Image.new('RGBA', (w, h))
    shake = (0, 0)
    if style == 'pop':
        for image, cx, cy, i in layout.words:
            local = (t - 0.15 - i * 0.14) / 0.38
            breathe = 1 + 0.022 * math.sin(max(0.0, t - 1.2) * 2 * math.pi / 1.7) if t > 1.2 else 1
            paste_scaled(words_layer, image, cx, cy, ease_out_back(local) * breathe, clamp01(local * 2.5))
    elif style == 'slam':
        local = (t - 0.12) / 0.17
        scale = 2.6 - 1.6 * clamp01(local) ** 2 if local < 1 else 1.0
        after = t - 0.29
        if 0 < after < 0.3:
            amp = 14 * (1 - after / 0.3)
            shake = (amp * math.sin(after * 90), amp * math.cos(after * 70))
        for image, cx, cy, i in layout.words:
            paste_scaled(words_layer, image, cx + shake[0], cy + shake[1], scale, clamp01(local * 1.5))
        if after > 0:
            cy = sum(cy for _, _, cy, _ in layout.words) / len(layout.words)
            ring(canvas, w / 2, cy, w * 0.25 + after * w * 1.6, clamp01(1 - after / 0.55) * 0.9, max(4, w // 140))
    elif style == 'type':
        # Reveal each word's letters left to right by cropping.
        per_char = 0.06
        elapsed = max(0.0, t - 0.15)
        shown = elapsed / per_char
        counted = 0
        caret = None
        for image, cx, cy, i in layout.words:
            letters = max(1, round(image.width / (image.height * 0.62)))
            visible = clamp01((shown - counted) / letters)
            counted += letters + 1
            if visible <= 0:
                continue
            crop = image.crop((0, 0, max(1, int(image.width * visible)), image.height))
            words_layer.alpha_composite(crop, (int(cx - image.width / 2), int(cy - image.height / 2)))
            if visible < 1:
                caret = (int(cx - image.width / 2 + image.width * visible), int(cy), image.height)
        if caret is None and shown < counted + 6 and elapsed > 0:
            last = layout.words[-1]
            caret = (int(last[1] + last[0].width / 2 - last[0].height * 0.2), int(last[2]), last[0].height)
        if caret and int(t * 4) % 2 == 0:
            x, y, hgt = caret
            ImageDraw.Draw(words_layer).rectangle((x, y - hgt * 0.28, x + hgt * 0.08, y + hgt * 0.28), fill=ACCENT + (230,))
        sweep_t = (t - 0.15 - counted * per_char - 0.2) / 0.7
        if 0 < sweep_t < 1:
            sweep(words_layer, words_layer.copy(), sweep_t)
    elif style == 'shine':
        for image, cx, cy, i in layout.words:
            paste_scaled(words_layer, image, cx, cy, 1 + 0.12 * (1 - ease_out((t - 0.1) / 0.5)), clamp01((t - 0.1) / 0.35))
        cycle = (t - 0.55) % 1.6 / 0.75
        if t > 0.55 and cycle < 1:
            sweep(words_layer, words_layer.copy(), cycle)
    elif style == 'rise':
        for image, cx, cy, i in layout.words:
            local = ease_out((t - 0.1 - i * 0.08) / 0.6)
            blurred = image.filter(ImageFilter.GaussianBlur(10 * (1 - local))) if local < 0.98 else image
            paste_scaled(words_layer, blurred, cx, cy + 90 * (1 - local), 1, local)
    canvas.alpha_composite(words_layer)
    # Logo and sub line follow the title.
    appear = {'pop': 0.45, 'slam': 0.4, 'type': 0.3, 'shine': 0.5, 'rise': 0.55}[style]
    if layout.logo:
        local = ease_out((t - appear) / 0.45)
        logo = layout.logo
        if local < 1:
            logo = logo.copy()
            logo.putalpha(logo.getchannel('A').point(lambda a: int(a * local)))
        canvas.alpha_composite(logo, ((w - logo.width) // 2 + int(shake[0] * 0.3), layout.logo_y - int(30 * (1 - local))))
    if layout.sub:
        local = ease_out((t - appear - 0.2) / 0.45)
        paste_scaled(canvas, layout.sub, w / 2, layout.sub_y + layout.sub.height / 2 + 20 * (1 - local), 1, local)
    return canvas


def render(spec: dict, w: int, h: int, fps: int, duration: float, folder: Path) -> Path:
    style = spec.get('style', 'pop')
    if style not in STYLES:
        raise ValueError(f'end_card style {style} unknown; use {", ".join(STYLES)}, card or flash')
    folder.mkdir(parents=True, exist_ok=True)
    layout = Layout(spec, w, h)
    frames = max(1, round(duration * fps))
    for k in range(frames):
        frame(layout, style, k / fps, spec.get('dim', 0.5)).save(folder / f'{k:04d}.png', compress_level=1)
    clip = folder.with_suffix('.mov')
    subprocess.run(['ffmpeg', '-v', 'error', '-y', '-framerate', str(fps), '-i', str(folder / '%04d.png'),
                    '-c:v', 'png', '-pix_fmt', 'rgba', str(clip)], check=True)
    return clip
