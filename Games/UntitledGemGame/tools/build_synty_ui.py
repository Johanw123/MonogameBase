#!/usr/bin/env python3
"""
Builds the Synty InterfaceSciFiSoldierHUD texture set for UntitledGemGame's
Menus, HUD, and GUI panels/nodes.
Source pack: /home/johan/My project/Assets/Synty/InterfaceSciFiSoldierHUD/Sprites
"""

import os
import shutil
import numpy as np
from PIL import Image, ImageChops, ImageDraw

SYNTY_ROOT = "/home/johan/My project/Assets/Synty/InterfaceSciFiSoldierHUD/Sprites"
SCIFI_DIR = os.path.join(SYNTY_ROOT, "SciFi")
HUD_DIR = os.path.join(SYNTY_ROOT, "HUD")
FX_DIR = os.path.join(SYNTY_ROOT, "FX")

OUT_MENU = "/home/johan/Dev/workspaces/MonogameBase/Games/UntitledGemGame/Content/Menu"
OUT_GUI = "/home/johan/Dev/workspaces/MonogameBase/Games/UntitledGemGame/Content/Textures/GUI"


def load_scifi(name: str, crop_bbox: bool = False) -> Image.Image:
    im = Image.open(os.path.join(SCIFI_DIR, name)).convert("RGBA")
    if crop_bbox:
        bbox = im.getbbox()
        if bbox:
            im = im.crop(bbox)
    return im


def load_hud(name: str, crop_bbox: bool = False) -> Image.Image:
    im = Image.open(os.path.join(HUD_DIR, name)).convert("RGBA")
    if crop_bbox:
        bbox = im.getbbox()
        if bbox:
            im = im.crop(bbox)
    return im


def load_fx(name: str, crop_bbox: bool = False) -> Image.Image:
    im = Image.open(os.path.join(FX_DIR, name)).convert("RGBA")
    if crop_bbox:
        bbox = im.getbbox()
        if bbox:
            im = im.crop(bbox)
    return im


def tint_image(img: Image.Image, rgba: tuple[int, int, int, int]) -> Image.Image:
    """Multiply an RGBA image by a color + alpha tuple (like Unity's Image.color)."""
    r, g, b, a = rgba
    solid = Image.new("RGBA", img.size, (r, g, b, a))
    return ImageChops.multiply(img, solid)


def colorize_metal(
    img: Image.Image,
    shadow_rgb: tuple[int, int, int],
    mid_rgb: tuple[int, int, int],
    highlight_rgb: tuple[int, int, int],
    alpha_scale: float = 1.0,
) -> Image.Image:
    """
    Maps a hand-painted Synty grayscale/metal sprite through a 3-point tactical ramp
    (shadow -> midtone -> highlight) while preserving warm orange industrial markings
    (where R - B > 35) and preserving the sprite's painted bevel contrast.
    """
    arr = np.array(img, dtype=np.float32)
    r, g, b, a = arr[..., 0], arr[..., 1], arr[..., 2], arr[..., 3]
    lum = (0.299 * r + 0.587 * g + 0.114 * b) / 255.0

    warm = np.clip(((r - b) - 28.0) / 48.0, 0.0, 1.0)

    t_low = np.clip(lum / 0.55, 0.0, 1.0)
    t_high = np.clip((lum - 0.55) / 0.45, 0.0, 1.0)

    out_rgb = np.zeros((arr.shape[0], arr.shape[1], 3), dtype=np.float32)
    for c_idx in range(3):
        s_val = float(shadow_rgb[c_idx])
        m_val = float(mid_rgb[c_idx])
        h_val = float(highlight_rgb[c_idx])
        mapped = np.where(lum < 0.55, s_val + (m_val - s_val) * t_low, m_val + (h_val - m_val) * t_high)
        out_rgb[..., c_idx] = mapped * (1.0 - warm) + arr[..., c_idx] * warm

    out = np.zeros_like(arr)
    out[..., :3] = np.clip(out_rgb, 0, 255)
    out[..., 3] = np.clip(a * alpha_scale, 0, 255)
    return Image.fromarray(out.astype(np.uint8), mode="RGBA")


def polish_painted_metal(img: Image.Image) -> Image.Image:
    return img.copy()


def nine_slice_resize(
    img: Image.Image,
    target_size: tuple[int, int],
    src_borders: tuple[int, int, int, int],
    dst_borders: tuple[int, int, int, int] | None = None,
) -> Image.Image:
    """
    9-slice an image from src_borders (left, top, right, bottom) into target_size
    with dst_borders (left, top, right, bottom).
    """
    tw, th = target_size
    sl, st, sr, sb = src_borders
    if dst_borders is None:
        dl, dt, dr, db = sl, st, sr, sb
    else:
        dl, dt, dr, db = dst_borders

    dl = min(dl, tw // 2)
    dr = min(dr, tw - dl)
    dt = min(dt, th // 2)
    db = min(db, th - dt)

    w, h = img.size
    out = Image.new("RGBA", (tw, th), (0, 0, 0, 0))

    sx = [0, sl, max(sl + 1, w - sr), w]
    sy = [0, st, max(st + 1, h - sb), h]
    dx = [0, dl, max(dl, tw - dr), tw]
    dy = [0, dt, max(dt, th - db), th]

    for iy in range(3):
        for ix in range(3):
            sw = sx[ix + 1] - sx[ix]
            sh = sy[iy + 1] - sy[iy]
            dw = dx[ix + 1] - dx[ix]
            dh = dy[iy + 1] - dy[iy]
            if sw <= 0 or sh <= 0 or dw <= 0 or dh <= 0:
                continue
            patch = img.crop((sx[ix], sy[iy], sx[ix + 1], sy[iy + 1]))
            if (sw, sh) != (dw, dh):
                patch = patch.resize((dw, dh), Image.Resampling.LANCZOS)
            out.paste(patch, (dx[ix], dy[iy]))
    return out


def alpha_composite_clipped(base: Image.Image, overlay: Image.Image, mask_source: Image.Image) -> Image.Image:
    """Alpha-composite overlay onto base, clipped by the alpha channel of mask_source."""
    _, _, _, mask_a = mask_source.split()
    ov_r, ov_g, ov_b, ov_a = overlay.split()
    clipped_a = ImageChops.multiply(ov_a, mask_a)
    clipped_ov = Image.merge("RGBA", (ov_r, ov_g, ov_b, clipped_a))
    return Image.alpha_composite(base, clipped_ov)


def enforce_clean_9slice(img: Image.Image, frame: int) -> Image.Image:
    """
    Guarantees that when Gum's NineSliceRuntime(frame) or OrbitSkin.NineSlice(frame)
    stretches this texture to ANY width or height:
    - The 4 corners (frame x frame) are preserved 1:1 with zero distortion.
    - The top and bottom rails (x in frame..w-frame) are horizontally uniform.
    - The left and right rails (y in frame..h-frame) are vertically uniform.
    - The center patch (frame..w-frame, frame..h-frame) is uniform.
    """
    out = img.copy()
    w, h = out.size
    if w <= frame * 2 + 4 or h <= frame * 2 + 4:
        return out

    mid_x = w // 2
    mid_y = h // 2
    center_px = out.getpixel((mid_x, mid_y))

    top_profile = [out.getpixel((mid_x, y)) for y in range(frame)]
    bot_profile = [out.getpixel((mid_x, y)) for y in range(h - frame, h)]
    left_profile = [out.getpixel((x, mid_y)) for x in range(frame)]
    right_profile = [out.getpixel((x, mid_y)) for x in range(w - frame, w)]

    for x in range(frame, w - frame):
        for y in range(frame):
            out.putpixel((x, y), top_profile[y])
        for idx, y in enumerate(range(h - frame, h)):
            out.putpixel((x, y), bot_profile[idx])

    for y in range(frame, h - frame):
        for x in range(frame):
            out.putpixel((x, y), left_profile[x])
        for idx, x in enumerate(range(w - frame, w)):
            out.putpixel((x, y), right_profile[idx])

    center_patch = Image.new("RGBA", (w - frame * 2, h - frame * 2), center_px)
    out.paste(center_patch, (frame, frame))
    return out


def enforce_horizontal_stretch_9slice(img: Image.Image, frame: int = 28) -> Image.Image:
    """
    For overlays used on fixed-height (88px) Gum controls (`OrbitFrame` on buttons/comboboxes):
    - Preserves the full 28px left and right ends (`0..frame` and `w-frame..w`) 1:1.
    - Makes the horizontal stretch zone (`frame..w-frame`) uniform along X for each row Y,
      so horizontal scanlines, top/bottom double bevel rails, and visor glass reflection
      remain 100% intact across the entire button without smearing when stretched horizontally!
    """
    arr = np.array(img, dtype=np.uint8)
    h, w, _ = arr.shape
    if w <= frame * 2 + 4:
        return img.copy()
    mid_x = w // 2
    col_profile = arr[:, mid_x : mid_x + 1, :]
    arr[:, frame : w - frame, :] = np.repeat(col_profile, w - 2 * frame, axis=1)
    return Image.fromarray(arr, mode="RGBA")


def make_edge_sheen(
    size: tuple[int, int],
    band_h: int,
    top_rgba: tuple[int, int, int, int],
    bot_rgba: tuple[int, int, int, int] | None = None,
) -> Image.Image:
    """Creates a 1D vertical edge sheen strictly within `band_h` pixels of top/bottom."""
    w, h = size
    out = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    if band_h <= 1:
        return out
    tr, tg, tb, ta = top_rgba
    for y in range(min(band_h, h // 2)):
        t = 1.0 - (y / float(band_h))
        a = int(round(ta * (t * t)))
        if a > 0:
            row = Image.new("RGBA", (w, 1), (tr, tg, tb, a))
            out.paste(row, (0, y))
    if bot_rgba is not None:
        br, bg, bb, ba = bot_rgba
        for idx in range(min(band_h, h // 2)):
            y = h - 1 - idx
            t = 1.0 - (idx / float(band_h))
            a = int(round(ba * (t * t)))
            if a > 0:
                row = Image.new("RGBA", (w, 1), (br, bg, bb, a))
                out.paste(row, (0, y))
    return out


def draw_box02_inner_bezel_and_brackets(
    size: tuple[int, int],
    chamfer_c: int,
    inset: int,
    inner_rgba: tuple[int, int, int, int],
    bracket_rgba: tuple[int, int, int, int],
    accent_pip_rgba: tuple[int, int, int, int] | None,
    arm_len: int,
    bracket_thick: int = 3,
) -> Image.Image:
    """
    Draws a precision-aligned tactical inner bezel and corner armor bracket overlay
    that matches `SPR_HUD_SciFiSoldier_Box_02` / `Frame_02`'s exact asymmetric geometry:
    - Top-Left & Bottom-Right: 90-degree square corners
    - Top-Right & Bottom-Left: 45-degree diagonal chamfers of size `chamfer_c`.
    Because all corner features stay within `arm_len < slice_frame`, 9-slicing at `slice_frame`
    preserves every corner bracket, chamfer plate, and inner bezel corner 1:1 with zero distortion!
    """
    w, h = size
    scale = 2
    sw, sh = w * scale, h * scale
    sc = chamfer_c * scale
    sinset = inset * scale
    sarm = arm_len * scale
    sthick = bracket_thick * scale

    canvas = Image.new("RGBA", (sw, sh), (0, 0, 0, 0))
    draw = ImageDraw.Draw(canvas)

    # 1. Inner parallel contour matching Box_02 (90° at TL & BR, 45° chamfer at TR & BL)
    if inner_rgba[3] > 0:
        # Outer chamfer plane in 2x coords: x + (sh - 1 - y) >= sw - 1 - sc + ...
        # For a uniform offset of `sinset` inward from the edges and 45° chamfers:
        # Chamfer cut at TR: from (sw - 1 - sc - sinset//2, sinset) to (sw - 1 - sinset, sc + sinset//2)
        c_off = max(2, sc - int(round(sinset * 0.6)))
        pts = [
            (sinset, sinset),  # TL (90°)
            (sw - 1 - sinset - c_off, sinset),  # TR chamfer start
            (sw - 1 - sinset, sinset + c_off),  # TR chamfer end
            (sw - 1 - sinset, sh - 1 - sinset),  # BR (90°)
            (sinset + c_off, sh - 1 - sinset),  # BL chamfer start
            (sinset, sh - 1 - sinset - c_off),  # BL chamfer end
        ]
        draw.polygon(pts, outline=inner_rgba, width=max(2, scale))

    # 2. Reinforced Corner Brackets on Top-Left (90°) and Bottom-Right (90°)
    if bracket_rgba[3] > 0:
        pad = 2 * scale
        # Top-Left L-bracket with angled tips
        tl_poly = [
            (pad, pad),
            (sarm, pad),
            (sarm - sthick, pad + sthick),
            (pad + sthick, pad + sthick),
            (pad + sthick, sarm - sthick),
            (pad, sarm),
        ]
        draw.polygon(tl_poly, fill=bracket_rgba)

        # Bottom-Right L-bracket with angled tips
        br_poly = [
            (sw - 1 - pad, sh - 1 - pad),
            (sw - 1 - sarm, sh - 1 - pad),
            (sw - 1 - sarm + sthick, sh - 1 - pad - sthick),
            (sw - 1 - pad - sthick, sh - 1 - pad - sthick),
            (sw - 1 - pad - sthick, sh - 1 - sarm + sthick),
            (sw - 1 - pad, sh - 1 - sarm),
        ]
        draw.polygon(br_poly, fill=bracket_rgba)

        # 3. Reinforced 45° Chamfer Armor Plates on Top-Right and Bottom-Left
        # Top-Right chamfer runs from (sw - 1 - sc, pad) to (sw - 1 - pad, sc)
        tr_poly = [
            (sw - 1 - sarm, pad),
            (sw - 1 - sc, pad),
            (sw - 1 - pad, sc),
            (sw - 1 - pad, sarm),
            (sw - 1 - pad - sthick, sarm - sthick),
            (sw - 1 - pad - sthick, sc + sthick // 2),
            (sw - 1 - sc - sthick // 2, pad + sthick),
            (sw - 1 - sarm + sthick, pad + sthick),
        ]
        draw.polygon(tr_poly, fill=bracket_rgba)

        # Bottom-Left chamfer runs from (pad, sh - 1 - sc) to (sc, sh - 1 - pad)
        bl_poly = [
            (pad, sh - 1 - sarm),
            (pad, sh - 1 - sc),
            (sc, sh - 1 - pad),
            (sarm, sh - 1 - pad),
            (sarm - sthick, sh - 1 - pad - sthick),
            (sc + sthick // 2, sh - 1 - pad - sthick),
            (pad + sthick, sh - 1 - sc - sthick // 2),
            (pad + sthick, sh - 1 - sarm + sthick),
        ]
        draw.polygon(bl_poly, fill=bracket_rgba)

        # 4. Crisp orthogonal rivet dots nestled inside TL and BR 90° L-brackets
    if accent_pip_rgba is not None and accent_pip_rgba[3] > 0:
        p0 = (inset + 2) * scale
        psize = 3 * scale
        draw.rectangle([p0, p0, p0 + psize, p0 + psize], fill=accent_pip_rgba)
        draw.rectangle(
            [sw - 1 - p0 - psize, sh - 1 - p0 - psize, sw - 1 - p0, sh - 1 - p0],
            fill=accent_pip_rgba,
        )

    return canvas.resize((w, h), Image.Resampling.LANCZOS)


def slice_box02_metal_bevel(
    metal_src: Image.Image,
    size: tuple[int, int],
    src_border: int,
    dst_border: int,
    rim_width: int,
) -> Image.Image:
    """
    9-slices a 4-chamfer Synty painted metal box (`Box_Techy_02` or `Box_Basic_01`) and
    miters its Top-Left and Bottom-Right corners into true 90° square bevels (`x == y` miter)
    while preserving the native 45° chamfered bevels at Top-Right and Bottom-Left,
    matching `SPR_HUD_SciFiSoldier_Box_02` geometry with 100% precision.
    """
    w, h = size
    sliced = nine_slice_resize(
        metal_src,
        size,
        (src_border, src_border, src_border, src_border),
        (dst_border, dst_border, dst_border, dst_border),
    )
    arr = np.array(sliced, dtype=np.uint8)
    b = min(dst_border, w // 2, h // 2)
    xm = w // 2
    ym = h // 2

    # Miter Top-Left (90° corner): top profile where y <= x, left profile where x < y
    for y in range(b):
        for x in range(b):
            if y <= x:
                arr[y, x] = arr[y, xm]
            else:
                arr[y, x] = arr[ym, x]

    # Miter Bottom-Right (90° corner): bottom profile where dy <= dx, right profile where dx < dy
    for dy in range(b):
        y = h - 1 - dy
        for dx in range(b):
            x = w - 1 - dx
            if dy <= dx:
                arr[y, x] = arr[y, xm]
            else:
                arr[y, x] = arr[ym, x]

    # Mask out interior beyond rim_width so only the sculpted perimeter bevel remains
    arr[rim_width : h - rim_width, rim_width : w - rim_width, 3] = 0
    return Image.fromarray(arr, mode="RGBA")


def build_button(
    size: tuple[int, int],
    active: bool,
    green: bool = False,
) -> Image.Image:
    """
    Constructs the 12px-sliced base button texture (`button_idle_blue.png`, `button_active_blue.png`,
    `button_idle_green.png`, `button_active_green.png`) with:
    - `SPR_HUD_SciFiSoldier_Box_02` chamfered silhouette (`c = 11`)
    - Contour-matched 3D painted metal bevels from `SPR_SciFiSoldier_Box_Techy_02` around the perimeter
    - Precision-matched inner parallel bezel at `inset = 4px`
    - Reinforced corner brackets within the 12px corner zone
    - Clean 9-slice center so arbitrary vertical stretching (e.g. 240px or 920px cards) never bands.
    """
    w, h = size
    box_hud = load_hud("SPR_HUD_SciFiSoldier_Box_02.png")
    frame_hud = load_hud("SPR_HUD_SciFiSoldier_Frame_02.png")
    box_techy02 = load_scifi("SPR_SciFiSoldier_Box_Techy_02.png", crop_bbox=True)

    c = 11
    dst_b = (c, c, c, c)
    base_shape = nine_slice_resize(box_hud, size, (54, 54, 54, 54), dst_b)

    if green:
        fill_rgba = (12, 50, 40, 252) if active else (6, 26, 22, 248)
        sheen_top = (68, 225, 155, 82 if active else 44)
        sheen_bot = (32, 135, 92, 48 if active else 26)
        metal_shadow = (6, 28, 22)
        metal_mid = (38, 138, 98) if active else (24, 88, 66)
        metal_hi = (135, 248, 188) if active else (82, 182, 134)
        inner_rgba = (92, 228, 162, 145 if active else 82)
        bracket_rgba = (135, 250, 182, 250 if active else 200)
        rim_rgba = (112, 238, 162, 255) if active else (70, 182, 126, 242)
    else:
        fill_rgba = (13, 42, 70, 252) if active else (6, 20, 38, 248)
        sheen_top = (54, 188, 250, 86 if active else 46)
        sheen_bot = (28, 112, 175, 50 if active else 28)
        metal_shadow = (5, 18, 34)
        metal_mid = (36, 128, 192) if active else (22, 76, 118)
        metal_hi = (120, 235, 255) if active else (72, 162, 210)
        inner_rgba = (72, 204, 250, 150 if active else 86)
        bracket_rgba = (120, 238, 255, 250 if active else 200)
        rim_rgba = (92, 232, 248, 255) if active else (58, 146, 182, 242)

    out = tint_image(base_shape, fill_rgba)

    # Top and bottom bevel sheen strictly within `c = 11` pixels so 12px 9-slice is seam-free
    sheen = make_edge_sheen(size, c, sheen_top, sheen_bot)
    out = alpha_composite_clipped(out, sheen, base_shape)

    # Contour-matched 3D painted metal bevel rim from Box_Techy_02
    bevel_src = colorize_metal(box_techy02, metal_shadow, metal_mid, metal_hi, alpha_scale=0.85 if active else 0.68)
    bevel_sliced = slice_box02_metal_bevel(bevel_src, size, src_border=121, dst_border=10, rim_width=8)
    out = alpha_composite_clipped(out, bevel_sliced, base_shape)

    # Precision-aligned inner bezel at inset=4px + 11px corner brackets
    bezel_and_brackets = draw_box02_inner_bezel_and_brackets(
        size,
        chamfer_c=c,
        inset=4,
        inner_rgba=inner_rgba,
        bracket_rgba=bracket_rgba,
        accent_pip_rgba=None,
        arm_len=11,
        bracket_thick=2,
    )
    out = alpha_composite_clipped(out, bezel_and_brackets, base_shape)

    border = nine_slice_resize(frame_hud, size, (54, 54, 54, 54), dst_b)
    out = Image.alpha_composite(out, tint_image(border, rim_rgba))

    return enforce_clean_9slice(out, frame=12)


def build_button_frame_overlay(size: tuple[int, int], active: bool, include_scanlines: bool = True) -> Image.Image:
    """
    Builds the 28px-corner tactical overlay (`button_frame_idle_blue.png`, `button_frame_active_blue.png`,
    `hud_button_corners_idle_blue.png`, `hud_button_corners_active_blue.png`) with `frame = 28`:
    - All 4 corners (`28x28`) are rendered 1:1 with zero horizontal or vertical stretching!
    - The 90° Top-Left & Bottom-Right corners feature heavy 24px L-brackets and `+` registration crosses.
    - The 45° Top-Right & Bottom-Left chamfers feature precision-aligned 45° armor plates.
    - The interior is 100% transparent so Gum's NonPremultiplied blend never reduces destination alpha or creates a 28px seam.
    """
    _ = include_scanlines
    w, h = size
    c = 11
    box_hud = load_hud("SPR_HUD_SciFiSoldier_Box_02.png")
    base_mask = nine_slice_resize(box_hud, size, (54, 54, 54, 54), (c, c, c, c))

    corners = Image.new("RGBA", size, (0, 0, 0, 0))

    # 1. Orthogonal registration crosses (+) nestled inside the 90° Top-Left and Bottom-Right corners
    crosses = load_hud("SPR_HUD_SciFiSoldier_Button_Crosses_01.png")
    cross_patch = crosses.crop((0, 0, 18, 18)).resize((9, 9), Image.Resampling.LANCZOS)
    cross_c = (135, 242, 255, 245 if active else 210)
    corners.alpha_composite(tint_image(cross_patch, cross_c), (8, 8))
    corners.alpha_composite(tint_image(cross_patch, cross_c), (w - 17, h - 17))

    # 2. Precision-matched 24px corner armor brackets (90° L-brackets at TL/BR, 45° chamfer plates at TR/BL)
    bracket_c = (130, 245, 255, 255) if active else (76, 188, 232, 245)
    brackets = draw_box02_inner_bezel_and_brackets(
        size,
        chamfer_c=c,
        inset=4,
        inner_rgba=(0, 0, 0, 0),
        bracket_rgba=bracket_c,
        accent_pip_rgba=None,
        arm_len=24,
        bracket_thick=3,
    )
    corners = Image.alpha_composite(corners, brackets)

    # Strictly keep only the four 26x26 corner boxes so rails and interior are 100% transparent
    corner_mask = Image.new("L", size, 0)
    mdraw = ImageDraw.Draw(corner_mask)
    cs = 26
    mdraw.rectangle([0, 0, cs, cs], fill=255)
    mdraw.rectangle([w - 1 - cs, 0, w - 1, cs], fill=255)
    mdraw.rectangle([0, h - 1 - cs, cs, h - 1], fill=255)
    mdraw.rectangle([w - 1 - cs, h - 1 - cs, w - 1, h - 1], fill=255)
    r, g, b, a = corners.split()
    corners = Image.merge("RGBA", (r, g, b, ImageChops.multiply(a, corner_mask)))

    out = alpha_composite_clipped(Image.new("RGBA", size, (0, 0, 0, 0)), corners, base_mask)
    return enforce_clean_9slice(out, frame=28)


def build_panel_frame_overlay(size: tuple[int, int] = (600, 260)) -> Image.Image:
    """
    Builds a 48px-corner tactical frame overlay (`panel_frame_overlay_blue.png`) for
    large panels, HUD bars, and menu windows (`frame = 48`).
    Contains ONLY 48x48 corner details (zero middle rail bars so vertical edges never thicken):
    - Precision-matched 42px corner armor brackets (90° at TL/BR, 45° chamfer at TR/BL)
    - Orthogonal `+` registration crosses (`SPR_HUD_SciFiSoldier_Button_Crosses_01`) inside the 90° TL/BR corners.
    """
    w, h = size
    c = 18
    box_hud = load_hud("SPR_HUD_SciFiSoldier_Box_02.png")
    base_mask = nine_slice_resize(box_hud, size, (54, 54, 54, 54), (c, c, c, c))

    out = Image.new("RGBA", size, (0, 0, 0, 0))

    # 1. Orthogonal corner registration crosses (+) nestled inside the 90° Top-Left and Bottom-Right corners
    crosses = load_hud("SPR_HUD_SciFiSoldier_Button_Crosses_01.png")
    cross_patch = crosses.crop((0, 0, 18, 18)).resize((11, 11), Image.Resampling.LANCZOS)
    cross_c = (125, 238, 255, 185)
    out.alpha_composite(tint_image(cross_patch, cross_c), (11, 11))
    out.alpha_composite(tint_image(cross_patch, cross_c), (w - 22, h - 22))

    # 2. Precision-aligned 42px corner armor brackets (90° at TL/BR, 45° chamfer at TR/BL)
    brackets = draw_box02_inner_bezel_and_brackets(
        size,
        chamfer_c=c,
        inset=7,
        inner_rgba=(0, 0, 0, 0),
        bracket_rgba=(96, 228, 255, 240),
        accent_pip_rgba=None,
        arm_len=42,
        bracket_thick=3,
    )
    out = Image.alpha_composite(out, brackets)

    # Strictly mask out everything outside the four 46x46 corner boxes so the rails and center are 100% transparent!
    corner_mask = Image.new("L", size, 0)
    mdraw = ImageDraw.Draw(corner_mask)
    cs = 46
    mdraw.rectangle([0, 0, cs, cs], fill=255)
    mdraw.rectangle([w - 1 - cs, 0, w - 1, cs], fill=255)
    mdraw.rectangle([0, h - 1 - cs, cs, h - 1], fill=255)
    mdraw.rectangle([w - 1 - cs, h - 1 - cs, w - 1, h - 1], fill=255)
    r, g, b, a = out.split()
    out = Image.merge("RGBA", (r, g, b, ImageChops.multiply(a, corner_mask)))

    out = alpha_composite_clipped(Image.new("RGBA", size, (0, 0, 0, 0)), out, base_mask)
    return enforce_clean_9slice(out, frame=48)


def build_tab(size: tuple[int, int], active: bool) -> Image.Image:
    return build_button(size, active=active, green=False)


def build_panel_modal(size: tuple[int, int], corner_frame: int = 24) -> Image.Image:
    """
    Builds `modal_info_complete.png` and `Button Normal.png` with:
    - `SPR_HUD_SciFiSoldier_Box_02` chamfered silhouette (`c = 18`)
    - Contour-matched 3D painted metal bevels (`SPR_SciFiSoldier_Box_Basic_01`) along the outer perimeter
    - Precision-matched inner parallel bezel at `inset = 6px`
    - Reinforced corner armor brackets within the 24px corner frame.
    """
    w, h = size
    c = min(18, corner_frame - 2)
    dst_b = (c, c, c, c)

    box_hud = load_hud("SPR_HUD_SciFiSoldier_Box_02.png")
    frame_hud = load_hud("SPR_HUD_SciFiSoldier_Frame_02.png")
    box_basic01 = load_scifi("SPR_SciFiSoldier_Box_Basic_01.png", crop_bbox=True)

    base_shape = nine_slice_resize(box_hud, size, (54, 54, 54, 54), dst_b)
    out = tint_image(base_shape, (5, 18, 34, 242))

    sheen = make_edge_sheen(size, c, (42, 168, 238, 62), (26, 118, 186, 42))
    out = alpha_composite_clipped(out, sheen, base_shape)

    metal_src = colorize_metal(box_basic01, (6, 20, 38), (28, 96, 148), (88, 198, 245), alpha_scale=0.76)
    metal_sliced = slice_box02_metal_bevel(metal_src, size, src_border=115, dst_border=16, rim_width=12)
    out = alpha_composite_clipped(out, metal_sliced, base_shape)

    bezel_and_brackets = draw_box02_inner_bezel_and_brackets(
        size,
        chamfer_c=c,
        inset=6,
        inner_rgba=(54, 175, 235, 105),
        bracket_rgba=(88, 220, 255, 225),
        accent_pip_rgba=None,
        arm_len=min(22, corner_frame - 2),
        bracket_thick=3,
    )
    out = alpha_composite_clipped(out, bezel_and_brackets, base_shape)

    rim02 = nine_slice_resize(frame_hud, size, (54, 54, 54, 54), dst_b)
    out = Image.alpha_composite(out, tint_image(rim02, (62, 196, 250, 248)))

    return enforce_clean_9slice(out, frame=corner_frame)


def build_hud_bar(size: tuple[int, int], corner_frame: int = 24) -> Image.Image:
    """
    Builds `hud_bar_background.png` for the bottom and top in-game HUD bars using a
    semi-transparent Synty HUD chamfered box (`Box_02`), contour-matched painted `Box_Techy_02` armor rails,
    precision-matched inner parallel bezel, and crisp border (`Frame_02`).
    """
    w, h = size
    c = min(18, corner_frame - 2)
    dst_b = (c, c, c, c)

    box_hud = load_hud("SPR_HUD_SciFiSoldier_Box_02.png")
    frame_hud = load_hud("SPR_HUD_SciFiSoldier_Frame_02.png")
    box_techy02 = load_scifi("SPR_SciFiSoldier_Box_Techy_02.png", crop_bbox=True)

    base_shape = nine_slice_resize(box_hud, size, (54, 54, 54, 54), dst_b)
    out = tint_image(base_shape, (4, 15, 28, 192))

    sheen = make_edge_sheen(size, c, (34, 145, 215, 76), (22, 102, 165, 48))
    out = alpha_composite_clipped(out, sheen, base_shape)

    metal_src = colorize_metal(box_techy02, (5, 18, 34), (26, 92, 142), (82, 192, 240), alpha_scale=0.72)
    metal_sliced = slice_box02_metal_bevel(metal_src, size, src_border=121, dst_border=16, rim_width=12)
    out = alpha_composite_clipped(out, metal_sliced, base_shape)

    bezel = draw_box02_inner_bezel_and_brackets(
        size,
        chamfer_c=c,
        inset=6,
        inner_rgba=(52, 168, 225, 95),
        bracket_rgba=(82, 212, 252, 215),
        accent_pip_rgba=None,
        arm_len=min(22, corner_frame - 2),
        bracket_thick=3,
    )
    out = alpha_composite_clipped(out, bezel, base_shape)

    rim = nine_slice_resize(frame_hud, size, (54, 54, 54, 54), dst_b)
    out = Image.alpha_composite(out, tint_image(rim, (64, 184, 236, 232)))

    return enforce_clean_9slice(out, frame=corner_frame)


def build_header(
    size: tuple[int, int],
    complete: bool,
    corner_frame: int = 24,
    flat_bottom: bool = False,
    divider_only: bool = False,
) -> Image.Image:
    """
    Builds header and sub-heading surfaces (`modal_title_complete.png`, `sub_heading.png`, `sub_heading2.png`)
    with bevelled Synty metal rails, precision-matched inner bezel, and double-line bottom divider.
    """
    w, h = size
    c = min(18 if corner_frame >= 24 else corner_frame - 1, h // 3)
    dst_b = (c, c, c, c)

    box_hud = load_hud("SPR_HUD_SciFiSoldier_Box_02.png")
    frame_hud = load_hud("SPR_HUD_SciFiSoldier_Frame_02.png")
    box_techy02 = load_scifi("SPR_SciFiSoldier_Box_Techy_02.png", crop_bbox=True)
    line02 = load_hud("SPR_HUD_SciFiSoldier_Line_Horizontal_02.png", crop_bbox=True)

    if divider_only:
        out = Image.new("RGBA", size, (0, 0, 0, 0))
        line_img = tint_image(line02.resize((w - 16, 3), Image.Resampling.LANCZOS), (52, 185, 245, 215))
        out.alpha_composite(line_img, (8, h - 5))
        return enforce_clean_9slice(out, frame=corner_frame)

    if not complete:
        base_shape = nine_slice_resize(box_hud, size, (54, 54, 54, 54), dst_b)
        out = tint_image(base_shape, (6, 24, 46, 228))
        sheen = make_edge_sheen(size, c, (38, 155, 225, 56))
        out = alpha_composite_clipped(out, sheen, base_shape)
        rim = nine_slice_resize(frame_hud, size, (54, 54, 54, 54), dst_b)
        out = Image.alpha_composite(out, tint_image(rim, (48, 172, 238, 232)))
        return enforce_clean_9slice(out, frame=corner_frame)

    if flat_bottom:
        tall_sz = (w, h + c + 12)
        base_tall = nine_slice_resize(box_hud, tall_sz, (54, 54, 54, 54), dst_b)
        out_tall = tint_image(base_tall, (8, 30, 58, 238))

        sheen_tall = make_edge_sheen(tall_sz, c, (48, 178, 248, 72))
        out_tall = alpha_composite_clipped(out_tall, sheen_tall, base_tall)

        metal_src = colorize_metal(box_techy02, (6, 22, 42), (32, 112, 172), (98, 215, 255), alpha_scale=0.82)
        metal_tall = slice_box02_metal_bevel(metal_src, tall_sz, src_border=121, dst_border=16, rim_width=12)
        out_tall = alpha_composite_clipped(out_tall, metal_tall, base_tall)

        bezel_tall = draw_box02_inner_bezel_and_brackets(
            tall_sz,
            chamfer_c=c,
            inset=6,
            inner_rgba=(68, 198, 252, 120),
            bracket_rgba=(105, 232, 255, 235),
            accent_pip_rgba=None,
            arm_len=min(22, corner_frame - 2),
            bracket_thick=3,
        )
        out_tall = alpha_composite_clipped(out_tall, bezel_tall, base_tall)

        rim_tall = nine_slice_resize(frame_hud, tall_sz, (54, 54, 54, 54), dst_b)
        out_tall = Image.alpha_composite(out_tall, tint_image(rim_tall, (72, 208, 255, 250)))

        out = out_tall.crop((0, 0, w, h))
        div_top = Image.new("RGBA", (w - 14, 1), (48, 162, 225, 175))
        div_bot = Image.new("RGBA", (w, 2), (68, 205, 255, 242))
        out.alpha_composite(div_top, (7, h - 6))
        out.alpha_composite(div_bot, (0, h - 2))
        return enforce_clean_9slice(out, frame=corner_frame)

    base_shape = nine_slice_resize(box_hud, size, (54, 54, 54, 54), dst_b)
    out = tint_image(base_shape, (8, 30, 58, 232))
    sheen = make_edge_sheen(size, c, (46, 175, 245, 62))
    out = alpha_composite_clipped(out, sheen, base_shape)

    bezel = draw_box02_inner_bezel_and_brackets(
        size,
        chamfer_c=c,
        inset=4,
        inner_rgba=(62, 188, 245, 110),
        bracket_rgba=(92, 222, 255, 225),
        accent_pip_rgba=None,
        arm_len=corner_frame - 1,
        bracket_thick=2,
    )
    out = alpha_composite_clipped(out, bezel, base_shape)
    rim = nine_slice_resize(frame_hud, size, (54, 54, 54, 54), dst_b)
    out = Image.alpha_composite(out, tint_image(rim, (68, 202, 255, 245)))

    return enforce_clean_9slice(out, frame=corner_frame)


def build_option_background(size: tuple[int, int], corner_frame: int = 12) -> Image.Image:
    """Builds `option_background.png` (used on 88px checkbox rows in Settings) with 1:1 scanlines and corner brackets."""
    w, h = size
    c = corner_frame - 1
    dst_b = (c, c, c, c)

    box_hud = load_hud("SPR_HUD_SciFiSoldier_Box_02.png")
    frame_hud = load_hud("SPR_HUD_SciFiSoldier_Frame_02.png")
    box_techy02 = load_scifi("SPR_SciFiSoldier_Box_Techy_02.png", crop_bbox=True)

    base_shape = nine_slice_resize(box_hud, size, (54, 54, 54, 54), dst_b)
    out = tint_image(base_shape, (6, 20, 38, 236))

    # Scanlines + vignette
    scan = load_hud("SPR_HUD_SciFiSoldier_Background_ScanLines_01.png")
    scan_crop = scan.crop((0, 0, min(w, scan.width), min(h, scan.height))).resize((w, h), Image.Resampling.NEAREST)
    out = alpha_composite_clipped(out, tint_image(scan_crop, (56, 185, 245, 15)), base_shape)

    vig = load_fx("SPR_FX_SciFiSoldier_Vignette_01.png").resize((w, h), Image.Resampling.LANCZOS)
    out = alpha_composite_clipped(out, tint_image(vig, (2, 6, 14, 145)), base_shape)

    sheen = make_edge_sheen(size, c, (42, 162, 228, 36), (24, 98, 156, 24))
    out = alpha_composite_clipped(out, sheen, base_shape)

    metal_src = colorize_metal(box_techy02, (5, 18, 34), (22, 74, 116), (68, 156, 204), alpha_scale=0.68)
    metal_sliced = slice_box02_metal_bevel(metal_src, size, src_border=121, dst_border=10, rim_width=8)
    out = alpha_composite_clipped(out, metal_sliced, base_shape)

    bezel = draw_box02_inner_bezel_and_brackets(
        size,
        chamfer_c=c,
        inset=4,
        inner_rgba=(52, 165, 218, 72),
        bracket_rgba=(74, 185, 228, 195),
        accent_pip_rgba=None,
        arm_len=11,
        bracket_thick=2,
    )
    out = alpha_composite_clipped(out, bezel, base_shape)

    border = nine_slice_resize(frame_hud, size, (54, 54, 54, 54), dst_b)
    out = Image.alpha_composite(out, tint_image(border, (58, 146, 182, 225)))

    return enforce_horizontal_stretch_9slice(out, frame=corner_frame)


def build_toggle(size: tuple[int, int], is_on: bool) -> Image.Image:
    """
    Builds a 104x63 Synty toggle switch track with a bevelled `Box_Techy_02` metal housing
    and recessed inner slot so ON and OFF hexagon knobs sit cleanly inside.
    """
    w, h = size
    out = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    inset_y = 6
    tw, th = w, h - inset_y * 2  # 104x51
    track_size = (tw, th)
    c = 12
    dst_b = (c, c, c, c)

    box_hud = load_hud("SPR_HUD_SciFiSoldier_Box_02.png")
    frame_hud = load_hud("SPR_HUD_SciFiSoldier_Frame_02.png")
    box_techy02 = load_scifi("SPR_SciFiSoldier_Box_Techy_02.png", crop_bbox=True)

    base_shape = nine_slice_resize(box_hud, track_size, (54, 54, 54, 54), dst_b)

    if is_on:
        fill = (10, 68, 86, 250)
        border_c = (86, 242, 220, 255)
        metal_mid = (36, 142, 156)
        metal_hi = (120, 245, 232)
    else:
        fill = (4, 18, 34, 246)
        border_c = (54, 134, 172, 232)
        metal_mid = (22, 72, 108)
        metal_hi = (68, 152, 196)

    track = tint_image(base_shape, fill)
    vig = load_fx("SPR_FX_SciFiSoldier_Vignette_01.png").resize(track_size, Image.Resampling.LANCZOS)
    track = alpha_composite_clipped(track, tint_image(vig, (2, 6, 14, 175)), base_shape)

    metal_src = colorize_metal(box_techy02, (4, 14, 28), metal_mid, metal_hi, alpha_scale=0.82)
    metal_sliced = slice_box02_metal_bevel(metal_src, track_size, src_border=121, dst_border=10, rim_width=7)
    track = alpha_composite_clipped(track, metal_sliced, base_shape)

    bezel = draw_box02_inner_bezel_and_brackets(
        track_size,
        chamfer_c=c,
        inset=4,
        inner_rgba=(border_c[0], border_c[1], border_c[2], 95),
        bracket_rgba=border_c,
        accent_pip_rgba=None,
        arm_len=14,
        bracket_thick=2,
    )
    track = alpha_composite_clipped(track, bezel, base_shape)

    border = nine_slice_resize(frame_hud, track_size, (54, 54, 54, 54), dst_b)
    track = Image.alpha_composite(track, tint_image(border, border_c))

    out.alpha_composite(track, (0, inset_y))
    return out


def build_knob(
    canvas_size: tuple[int, int],
    active: bool,
    color: tuple[int, int, int, int],
    inner_size: tuple[int, int] | None = None,
    use_dial: bool = False,
) -> Image.Image:
    """
    Builds a crisp Synty hexagon pip or tactical dial knob centered in canvas_size.
    """
    cw, ch = canvas_size
    iw, ih = inner_size if inner_size is not None else canvas_size
    out = Image.new("RGBA", (cw, ch), (0, 0, 0, 0))

    pip = load_hud(
        "SPR_HUD_SciFiSoldier_Pip_Hexagon_01_Active.png"
        if active
        else "SPR_HUD_SciFiSoldier_Pip_Hexagon_01_Inactive.png"
    )
    ring = load_hud("SPR_HUD_SciFiSoldier_Ring_Medium_04_Clean.png")

    knob = tint_image(pip.resize((iw, ih), Image.Resampling.LANCZOS), color)
    if use_dial:
        dial = polish_painted_metal(load_scifi("SPR_SciFiSoldier_Dial_Basic_02.png", crop_bbox=True))
        dial_s = dial.resize((iw, ih), Image.Resampling.LANCZOS)
        knob = Image.alpha_composite(knob, dial_s)
    ring_s = tint_image(ring.resize((iw, ih), Image.Resampling.LANCZOS), (230, 252, 255, 245 if active else 185))
    knob = Image.alpha_composite(knob, ring_s)

    ox = (cw - iw) // 2
    oy = (ch - ih) // 2
    out.alpha_composite(knob, (ox, oy))
    return out


def build_hex_socket_textures() -> tuple[Image.Image, Image.Image]:
    """
    Builds `synty_hud_ring_hex_med.png` and `synty_hud_hex_underlay.png` (256x256)
    using the sleeker `SPR_HUD_SciFiSoldier_Ring_Hexagon_Large_01_Clean.png` border
    cropped to its tight square bounds so the interior opening maximizes icon clearance,
    with an interior underlay clipped strictly inside the ring contour.
    """
    raw_ring = load_hud("SPR_HUD_SciFiSoldier_Ring_Hexagon_Large_01_Clean.png")
    cropped = raw_ring.crop((12, 12, 244, 244)).resize((252, 252), Image.Resampling.LANCZOS)
    ring = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    ring.alpha_composite(cropped, (2, 2))

    r_arr = np.array(ring)
    alpha = r_arr[:, :, 3]
    underlay_arr = np.zeros((256, 256, 4), dtype=np.uint8)
    for y in range(256):
        xs = np.where(alpha[y, :] > 24)[0]
        if len(xs) >= 2:
            x0, x1 = int(xs[0]) + 1, int(xs[-1]) - 1
            if x1 >= x0:
                underlay_arr[y, x0 : x1 + 1, 0] = 255
                underlay_arr[y, x0 : x1 + 1, 1] = 255
                underlay_arr[y, x0 : x1 + 1, 2] = 255
                underlay_arr[y, x0 : x1 + 1, 3] = 240
    underlay = Image.fromarray(underlay_arr)
    return ring, underlay


def build_tree_node_background(size: tuple[int, int]) -> Image.Image:
    """
    Builds `icon_background.png` (128x128) for upgrade/talent/system tree nodes
    with a recessed Synty vignette, subtle corner tech stripes, and inner bezel.
    """
    box04 = load_hud("SPR_HUD_SciFiSoldier_Box_04.png")
    frame01 = load_hud("SPR_HUD_SciFiSoldier_Frame_01.png")
    frame03 = load_hud("SPR_HUD_SciFiSoldier_Frame_03.png")

    base_shape = nine_slice_resize(box04, size, (35, 35, 35, 35), (4, 4, 4, 4))
    out = tint_image(base_shape, (6, 22, 42, 250))

    vig = load_fx("SPR_FX_SciFiSoldier_Vignette_01.png").resize(size, Image.Resampling.LANCZOS)
    out = alpha_composite_clipped(out, tint_image(vig, (2, 6, 14, 170)), base_shape)

    stepped = load_hud("SPR_HUD_SciFiSoldier_Gradient_Corner_Stepped_01.png", crop_bbox=True).resize(
        (36, 36), Image.Resampling.LANCZOS
    )
    out.alpha_composite(tint_image(stepped, (56, 185, 245, 42)), (6, 6))

    rim03 = nine_slice_resize(frame03, size, (80, 80, 80, 80), (14, 14, 14, 14))
    out = alpha_composite_clipped(out, tint_image(rim03, (48, 152, 215, 145)), base_shape)

    rim = nine_slice_resize(frame01, size, (8, 8, 8, 8), (4, 4, 4, 4))
    out = Image.alpha_composite(out, tint_image(rim, (42, 138, 198, 165)))
    return out


def build_node_slot(size: tuple[int, int], checked: bool = False, alt: bool = False, corner_frame: int = 24) -> Image.Image:
    """
    Builds HUD ability slots (`radio_unchecked.png`, `radio_checked_background.png` 100x100)
    with `Box_Techy_02` painted bevels, precision-matched inner bezel, and corner brackets.
    """
    w, h = size
    c = min(14, corner_frame - 2)
    dst_b = (c, c, c, c)

    box_hud = load_hud("SPR_HUD_SciFiSoldier_Box_02.png")
    frame_hud = load_hud("SPR_HUD_SciFiSoldier_Frame_02.png")
    box_techy02 = load_scifi("SPR_SciFiSoldier_Box_Techy_02.png", crop_bbox=True)

    base_shape = nine_slice_resize(box_hud, size, (54, 54, 54, 54), dst_b)
    fill_c = (12, 48, 84, 248) if checked else (6, 20, 38, 244)
    out = tint_image(base_shape, fill_c)

    vig = load_fx("SPR_FX_SciFiSoldier_Vignette_01.png").resize(size, Image.Resampling.LANCZOS)
    out = alpha_composite_clipped(out, tint_image(vig, (2, 6, 14, 165)), base_shape)

    sheen_c = (255, 210, 90, 56) if (checked and alt) else ((70, 220, 255, 56) if checked else (42, 165, 235, 32))
    sheen = make_edge_sheen(size, c, sheen_c)
    out = alpha_composite_clipped(out, sheen, base_shape)

    metal_src = colorize_metal(
        box_techy02,
        (5, 18, 34),
        (165, 125, 38) if (checked and alt) else ((36, 132, 195) if checked else (24, 78, 122)),
        (255, 215, 110) if (checked and alt) else ((115, 232, 255) if checked else (72, 164, 212)),
        alpha_scale=0.82,
    )
    metal_sliced = slice_box02_metal_bevel(metal_src, size, src_border=121, dst_border=14, rim_width=10)
    out = alpha_composite_clipped(out, metal_sliced, base_shape)

    rim_c = (255, 204, 82, 255) if (checked and alt) else ((95, 228, 255, 255) if checked else (58, 168, 224, 238))
    bezel = draw_box02_inner_bezel_and_brackets(
        size,
        chamfer_c=c,
        inset=5,
        inner_rgba=(rim_c[0], rim_c[1], rim_c[2], 110),
        bracket_rgba=rim_c,
        accent_pip_rgba=None,
        arm_len=min(22, corner_frame - 2),
        bracket_thick=3,
    )
    out = alpha_composite_clipped(out, bezel, base_shape)

    rim = nine_slice_resize(frame_hud, size, (54, 54, 54, 54), dst_b)
    out = Image.alpha_composite(out, tint_image(rim, rim_c))

    return enforce_clean_9slice(out, frame=corner_frame)


def build_slider_track(size: tuple[int, int]) -> Image.Image:
    """Builds a 20px-tall recessed Synty bar track with bevelled shading and cyan/steel border."""
    w, h = size
    box_hud = load_hud("SPR_HUD_SciFiSoldier_Box_04.png")
    frame_hud = load_hud("SPR_HUD_SciFiSoldier_Frame_01.png")
    frame03_hud = load_hud("SPR_HUD_SciFiSoldier_Frame_03.png")
    c = min(6, h // 2)
    base = nine_slice_resize(box_hud, size, (35, 35, 35, 35), (c, c, c, c))
    out = tint_image(base, (4, 18, 36, 248))
    sheen = make_edge_sheen(size, c, (12, 48, 86, 120), (48, 168, 235, 65))
    out = alpha_composite_clipped(out, sheen, base)
    rim03 = nine_slice_resize(frame03_hud, size, (80, 80, 80, 80), (6, 6, 6, 6))
    out = alpha_composite_clipped(out, tint_image(rim03, (64, 185, 245, 225)), base)
    rim = nine_slice_resize(frame_hud, size, (8, 8, 8, 8), (4, 4, 4, 4))
    out = Image.alpha_composite(out, tint_image(rim, (48, 158, 228, 242)))
    return enforce_clean_9slice(out, frame=6)


def build_radial_glow(size: tuple[int, int] = (256, 256)) -> Image.Image:
    """
    Builds a smooth radial glow texture (256x256, white RGB, smooth C2 radial alpha falloff
    that reaches 0 at r=1 with sub-LSB Bayer dithering so low-alpha tints never posterize).
    """
    w, h = size
    y, x = np.ogrid[:h, :w]
    nx = (x - (w - 1) / 2.0) / ((w - 1) / 2.0)
    ny = (y - (h - 1) / 2.0) / ((h - 1) / 2.0)
    r2 = np.clip(nx * nx + ny * ny, 0.0, 1.0)
    alpha_f = ((1.0 - r2) ** 2.5) * 255.0
    bayer4 = (
        np.array(
            [
                [0, 8, 2, 10],
                [12, 4, 14, 6],
                [3, 11, 1, 9],
                [15, 7, 13, 5],
            ],
            dtype=np.float32,
        )
        / 16.0
        - 0.5
    )
    dither = np.tile(bayer4, (h // 4 + 1, w // 4 + 1))[:h, :w]
    alpha = np.where(r2 >= 0.985, 0, np.clip(np.round(alpha_f + dither), 0, 255)).astype(np.uint8)
    rgba = np.zeros((h, w, 4), dtype=np.uint8)
    rgba[..., 0] = 255
    rgba[..., 1] = 255
    rgba[..., 2] = 255
    rgba[..., 3] = alpha
    return Image.fromarray(rgba, mode="RGBA")


def main() -> None:
    os.makedirs(OUT_MENU, exist_ok=True)
    os.makedirs(OUT_GUI, exist_ok=True)

    # 1. Buttons (360x88 and 289x88, 9-slice frame=12) + 28px-corner tactical frame overlays
    build_button((360, 88), active=False, green=False).save(os.path.join(OUT_MENU, "button_idle_blue.png"))
    build_button((360, 88), active=True, green=False).save(os.path.join(OUT_MENU, "button_active_blue.png"))
    build_button((360, 88), active=False, green=True).save(os.path.join(OUT_MENU, "button_idle_green.png"))
    build_button((360, 88), active=True, green=True).save(os.path.join(OUT_MENU, "button_active_green.png"))

    build_button_frame_overlay((360, 88), active=False, include_scanlines=True).save(
        os.path.join(OUT_MENU, "button_frame_idle_blue.png")
    )
    build_button_frame_overlay((360, 88), active=True, include_scanlines=True).save(
        os.path.join(OUT_MENU, "button_frame_active_blue.png")
    )
    # Corner-only overlay (no scanlines in stretch zone) for variable-height SpriteBatch buttons/cards
    build_button_frame_overlay((360, 88), active=False, include_scanlines=False).save(
        os.path.join(OUT_MENU, "hud_button_corners_idle_blue.png")
    )
    build_button_frame_overlay((360, 88), active=True, include_scanlines=False).save(
        os.path.join(OUT_MENU, "hud_button_corners_active_blue.png")
    )

    build_button((289, 88), active=False, green=False).save(os.path.join(OUT_MENU, "modal_button_left.png"))
    build_button((289, 88), active=True, green=False).save(os.path.join(OUT_MENU, "modal_button_right.png"))

    # 2. Tabs (frame=12)
    build_tab((375, 100), active=False).save(os.path.join(OUT_MENU, "tab_inactive.png"))
    build_tab((389, 106), active=True).save(os.path.join(OUT_MENU, "tab_active.png"))

    # 3. Modals, Panels, Headers, Option Rows
    build_panel_modal((600, 260), corner_frame=24).save(os.path.join(OUT_MENU, "modal_info_complete.png"))
    build_panel_frame_overlay((600, 260)).save(os.path.join(OUT_MENU, "panel_frame_overlay_blue.png"))
    build_hud_bar((600, 236), corner_frame=24).save(os.path.join(OUT_MENU, "hud_bar_background.png"))
    build_header((600, 90), complete=False, corner_frame=12).save(os.path.join(OUT_MENU, "modal_title_background.png"))
    build_header((600, 98), complete=True, corner_frame=24, flat_bottom=True).save(
        os.path.join(OUT_MENU, "modal_title_complete.png")
    )
    build_header((750, 78), complete=True, corner_frame=12).save(os.path.join(OUT_MENU, "sub_heading.png"))
    build_header((750, 78), complete=False, corner_frame=12, divider_only=True).save(
        os.path.join(OUT_MENU, "sub_heading2.png")
    )
    build_option_background((748, 120), corner_frame=12).save(os.path.join(OUT_MENU, "option_background.png"))

    # 4. Dividers / Lines
    line02 = load_hud("SPR_HUD_SciFiSoldier_Line_Horizontal_02.png", crop_bbox=True)
    line02.save(os.path.join(OUT_MENU, "synty_hud_line_02.png"))
    tint_image(line02.resize((578, 3), Image.Resampling.LANCZOS), (70, 205, 255, 230)).save(
        os.path.join(OUT_MENU, "modal_title_line.png")
    )
    dashes_canvas = Image.new("RGBA", (580, 16), (0, 0, 0, 0))
    dashes_line = tint_image(line02.resize((580, 3), Image.Resampling.LANCZOS), (60, 185, 240, 215))
    dashes_canvas.alpha_composite(dashes_line, (0, 6))
    dashes_canvas.save(os.path.join(OUT_MENU, "modal_title_dashes.png"))

    tint_image(line02.resize((750, 3), Image.Resampling.LANCZOS), (55, 165, 220, 195)).save(
        os.path.join(OUT_MENU, "option_line.png")
    )

    # 5. Progress & Sliders
    bar_angled = load_hud("SPR_HUD_SciFiSoldier_Bar_Angled_01.png", crop_bbox=True)
    bar_techy03 = polish_painted_metal(load_scifi("SPR_SciFiSoldier_Bar_Techy_03.png", crop_bbox=True))
    glow = load_hud("SPR_HUD_SciFiSoldier_Light_Glow_01.png", crop_bbox=True)

    build_slider_track((750, 20)).save(os.path.join(OUT_MENU, "progress_background.png"))
    build_slider_track((477, 20)).save(os.path.join(OUT_MENU, "slider_background.png"))

    prog_fg_base = nine_slice_resize(bar_angled, (287, 20), (50, 0, 50, 0), (6, 0, 6, 0))
    prog_fg = tint_image(prog_fg_base, (42, 205, 255, 255))
    prog_fg_glow = tint_image(glow.resize((287, 20), Image.Resampling.LANCZOS), (155, 245, 255, 145))
    prog_fg = Image.alpha_composite(prog_fg, prog_fg_glow)
    prog_fg.save(os.path.join(OUT_MENU, "progress_foreground.png"))
    prog_fg.save(os.path.join(OUT_MENU, "slider_foreground.png"))

    prog_bar = nine_slice_resize(bar_techy03, (613, 25), (183, 0, 154, 0), (28, 0, 28, 0))
    prog_bar.save(os.path.join(OUT_MENU, "progress_bar.png"))

    build_knob((64, 64), active=True, color=(56, 215, 255, 255), inner_size=(48, 48), use_dial=True).save(
        os.path.join(OUT_MENU, "slider_knob.png")
    )

    # 6. Toggles (104x63 track, 42x42 visual knob inside both 47x47 off and 59x59 on canvases)
    build_toggle((104, 63), is_on=False).save(os.path.join(OUT_MENU, "toggle_off_background.png"))
    build_toggle((104, 63), is_on=True).save(os.path.join(OUT_MENU, "toggle_on_background.png"))
    build_knob((47, 47), active=False, color=(92, 148, 192, 245), inner_size=(40, 40)).save(
        os.path.join(OUT_MENU, "toggle_off_knob.png")
    )
    build_knob((59, 59), active=True, color=(82, 245, 212, 255), inner_size=(42, 42)).save(
        os.path.join(OUT_MENU, "toggle_on_knob.png")
    )

    # 7. Radio / Slot frames (100x100 for 1:1 ability slot rendering)
    build_node_slot((100, 100), checked=False, alt=False, corner_frame=24).save(
        os.path.join(OUT_MENU, "radio_unchecked.png")
    )
    build_node_slot((100, 100), checked=True, alt=False, corner_frame=24).save(
        os.path.join(OUT_MENU, "radio_checked_background.png")
    )
    build_node_slot((100, 100), checked=True, alt=True, corner_frame=24).save(
        os.path.join(OUT_MENU, "radio_checked2_background.png")
    )
    build_knob((39, 32), active=True, color=(78, 228, 255, 245)).save(os.path.join(OUT_MENU, "radio_checked_check.png"))
    build_knob((46, 46), active=True, color=(90, 245, 210, 245)).save(os.path.join(OUT_MENU, "radio_checked2_check.png"))

    # 8. Copy raw Synty sprites into Content/Menu/ for OrbitSkin's layered 9-slice & animated backdrop rendering
    raw_copies = {
        "synty_box_techy_01.png": os.path.join(SCIFI_DIR, "SPR_SciFiSoldier_Box_Techy_01.png"),
        "synty_box_techy_02.png": os.path.join(SCIFI_DIR, "SPR_SciFiSoldier_Box_Techy_02.png"),
        "synty_box_techy_03.png": os.path.join(SCIFI_DIR, "SPR_SciFiSoldier_Box_Techy_03.png"),
        "synty_box_techy_04.png": os.path.join(SCIFI_DIR, "SPR_SciFiSoldier_Box_Techy04.png"),
        "synty_box_basic_01.png": os.path.join(SCIFI_DIR, "SPR_SciFiSoldier_Box_TechyGrey_01.png"),
        "synty_box_basic_02.png": os.path.join(SCIFI_DIR, "SPR_SciFiSoldier_Box_Basic_02.png"),
        "synty_box_steel_01.png": os.path.join(SCIFI_DIR, "SPR_SciFiSoldier_Box_Basic_01.png"),
        "synty_frame_techy_01.png": os.path.join(SCIFI_DIR, "SPR_SciFiSoldier_Frame_Techy_01.png"),
        "synty_frame_techy_02.png": os.path.join(SCIFI_DIR, "SPR_SciFiSoldier_Frame_Techy_02.png"),
        "synty_frame_techydark_01.png": os.path.join(SCIFI_DIR, "SPR_SciFiSoldier_Frame_TechyDark_01.png"),
        "synty_frame_techydark_03.png": os.path.join(SCIFI_DIR, "SPR_SciFiSoldier_Frame_TechyDark_03.png"),
        "synty_frame_brackets_01.png": os.path.join(SCIFI_DIR, "SPR_SciFiSoldier_Frame_Brackets_01.png"),
        "synty_frame_brackets_02.png": os.path.join(SCIFI_DIR, "SPR_SciFiSoldier_Frame_Brackets_02.png"),
        "synty_bar_techy_01.png": os.path.join(SCIFI_DIR, "SPR_SciFiSoldier_Bar_Techy_01.png"),
        "synty_bar_techy_03.png": os.path.join(SCIFI_DIR, "SPR_SciFiSoldier_Bar_Techy_03.png"),
        "synty_bar_techy_04.png": os.path.join(SCIFI_DIR, "SPR_SciFiSoldier_Bar_Techy_04.png"),
        "synty_greeble_top_01.png": os.path.join(SCIFI_DIR, "SPR_SciFiSoldier_Greeble_BoxTop_01.png"),
        "synty_greeble_barstart_04.png": os.path.join(SCIFI_DIR, "SPR_SciFiSoldier_Greeble_BarStart04.png"),
        "synty_greeble_barend_05.png": os.path.join(SCIFI_DIR, "SPR_SciFiSoldier_Greeble_BarEnd_05.png"),
        "synty_hud_box_01.png": os.path.join(HUD_DIR, "SPR_HUD_SciFiSoldier_Box_01.png"),
        "synty_hud_box_02.png": os.path.join(HUD_DIR, "SPR_HUD_SciFiSoldier_Box_02.png"),
        "synty_hud_box_04.png": os.path.join(HUD_DIR, "SPR_HUD_SciFiSoldier_Box_04.png"),
        "synty_hud_frame_01.png": os.path.join(HUD_DIR, "SPR_HUD_SciFiSoldier_Frame_01.png"),
        "synty_hud_frame_02.png": os.path.join(HUD_DIR, "SPR_HUD_SciFiSoldier_Frame_02.png"),
        "synty_hud_frame_03.png": os.path.join(HUD_DIR, "SPR_HUD_SciFiSoldier_Frame_03.png"),
        "synty_hud_frame_04.png": os.path.join(HUD_DIR, "SPR_HUD_SciFiSoldier_Frame_04.png"),
        "synty_hud_frame_06.png": os.path.join(HUD_DIR, "SPR_HUD_SciFiSoldier_Frame_06.png"),
        "synty_hud_frame_07.png": os.path.join(HUD_DIR, "SPR_HUD_SciFiSoldier_Frame_07.png"),
        "synty_hud_frame_08.png": os.path.join(HUD_DIR, "SPR_HUD_SciFiSoldier_Frame_08.png"),
        "synty_hud_frame_detail_03.png": os.path.join(HUD_DIR, "SPR_HUD_SciFiSoldier_Frame_Detail_03.png"),
        "synty_hud_frame_detail_06.png": os.path.join(HUD_DIR, "SPR_HUD_SciFiSoldier_Frame_Detail_06.png"),
        "synty_hud_frame_detail_07.png": os.path.join(HUD_DIR, "SPR_HUD_SciFiSoldier_Frame_Detail_07.png"),
        "synty_hud_glass_02.png": os.path.join(HUD_DIR, "SPR_HUD_SciFiSoldier_Box_Glass_02.png"),
        "synty_hud_glass_04.png": os.path.join(HUD_DIR, "SPR_HUD_SciFiSoldier_Box_Glass_04.png"),
        "synty_hud_glass_05.png": os.path.join(HUD_DIR, "SPR_HUD_SciFiSoldier_Box_Glass_05.png"),
        "synty_hud_corner_stepped.png": os.path.join(HUD_DIR, "SPR_HUD_SciFiSoldier_Gradient_Corner_Stepped_01.png"),
        "synty_hud_corner_graphic.png": os.path.join(HUD_DIR, "SPR_HUD_SciFiSoldier_Gradient_Corner_Graphic_01.png"),
        "synty_hud_button_crosses.png": os.path.join(HUD_DIR, "SPR_HUD_SciFiSoldier_Button_Crosses_01.png"),
        "synty_hud_gametime.png": os.path.join(HUD_DIR, "SPR_HUD_SciFiSoldier_Box_GameTime_01.png"),
        "synty_hud_frame_horiz.png": os.path.join(HUD_DIR, "SPR_HUD_SciFiSoldier_Frame_Detail_Horizontal_01.png"),
        "synty_hud_frame_horiz_02.png": os.path.join(HUD_DIR, "SPR_HUD_SciFiSoldier_Frame_Detail_Horizontal_02.png"),
        "synty_hud_frame_large_01.png": os.path.join(HUD_DIR, "SPR_HUD_SciFiSoldier_Frame_Detail_Large_01.png"),
        "synty_hud_line_01.png": os.path.join(HUD_DIR, "SPR_HUD_SciFiSoldier_Line_Horizontal_01.png"),
        "synty_hud_triangle.png": os.path.join(HUD_DIR, "SPR_HUD_SciFiSoldier_Frame_Detail_Triangle_01.png"),
        "synty_hud_triangle_02.png": os.path.join(HUD_DIR, "SPR_HUD_SciFiSoldier_Triangle_02.png"),
        "synty_hud_bar_angled_01.png": os.path.join(HUD_DIR, "SPR_HUD_SciFiSoldier_Bar_Angled_01.png"),
        "synty_hud_scanlines.png": os.path.join(HUD_DIR, "SPR_HUD_SciFiSoldier_Background_ScanLines_01.png"),
        "synty_hud_hexagons.png": os.path.join(HUD_DIR, "SPR_HUD_SciFiSoldier_Background_Hexagons_01.png"),
        "synty_hud_crosses.png": os.path.join(HUD_DIR, "SPR_HUD_SciFiSoldier_Background_Crosses_01.png"),
        "synty_hud_ring_calibration.png": os.path.join(HUD_DIR, "SPR_HUD_SciFiSoldier_Ring_Medium_02_Clean.png"),
        "synty_hud_calibration_ticks.png": os.path.join(HUD_DIR, "SPR_HUD_SciFiSoldier_Startup_Bars_01.png"),
        "synty_map_ship.png": os.path.join(SYNTY_ROOT, "Icons_Map", "ICON_SciFiSoldier_Map_Ship_01.png"),
        "synty_hud_ring_large_01.png": os.path.join(HUD_DIR, "SPR_HUD_SciFiSoldier_Ring_Large_01.png"),
        "synty_hud_ring_large_02.png": os.path.join(HUD_DIR, "SPR_HUD_SciFiSoldier_Ring_Large_02.png"),
        "synty_hud_pip_hex_active.png": os.path.join(HUD_DIR, "SPR_HUD_SciFiSoldier_Pip_Hexagon_01_Active.png"),
        "synty_hud_pip_hex_inactive.png": os.path.join(HUD_DIR, "SPR_HUD_SciFiSoldier_Pip_Hexagon_01_Inactive.png"),
        "synty_fx_hexpatch.png": os.path.join(FX_DIR, "SPR_FX_SciFiSoldier_HexPatch_01.png"),
        "synty_fx_vignette.png": os.path.join(FX_DIR, "SPR_FX_SciFiSoldier_Vignette_01.png"),
        "synty_fx_grad_hex.png": os.path.join(FX_DIR, "SPR_FX_SciFiSoldier_Gradient_Hexagons_01.png"),
        "synty_example_bg.png": "/home/johan/My project/Assets/Synty/InterfaceSciFiSoldierHUD/Samples/Sprites/SPR_HUD_SciFiSoldier_Example_Background_01.png",
    }
    for dst_name, src_path in raw_copies.items():
        shutil.copyfile(src_path, os.path.join(OUT_MENU, dst_name))

    hex_ring, hex_underlay = build_hex_socket_textures()
    hex_ring.save(os.path.join(OUT_MENU, "synty_hud_ring_hex_med.png"))
    hex_underlay.save(os.path.join(OUT_MENU, "synty_hud_hex_underlay.png"))

    # Clip glass overlays to their matching chamfer masks (Box_04 for glass_02, Box_02 for glass_04)
    # so 9-sliced glass highlights never bleed outside 45° chamfered corners!
    box04_mask = load_hud("SPR_HUD_SciFiSoldier_Box_04.png")
    box02_mask = load_hud("SPR_HUD_SciFiSoldier_Box_02.png")
    glass02_raw = load_hud("SPR_HUD_SciFiSoldier_Box_Glass_02.png")
    alpha_composite_clipped(Image.new("RGBA", glass02_raw.size, (0, 0, 0, 0)), glass02_raw, box04_mask).save(
        os.path.join(OUT_MENU, "synty_hud_glass_02.png")
    )
    alpha_composite_clipped(Image.new("RGBA", glass02_raw.size, (0, 0, 0, 0)), glass02_raw, box02_mask).save(
        os.path.join(OUT_MENU, "synty_hud_glass_04.png")
    )

    build_radial_glow((256, 256)).save(os.path.join(OUT_MENU, "synty_fx_glow_01.png"))

    frame_large = load_hud("SPR_HUD_SciFiSoldier_Frame_Detail_Large_01.png")
    frame_large.transpose(Image.Transpose.FLIP_LEFT_RIGHT).save(
        os.path.join(OUT_MENU, "synty_hud_frame_large_01_r.png")
    )

    # 9. Upgrade Tree / GUI textures in Content/Textures/GUI/
    build_tree_node_background((128, 128)).save(os.path.join(OUT_GUI, "icon_background.png"))
    frame01 = load_hud("SPR_HUD_SciFiSoldier_Frame_01.png").resize((128, 128), Image.Resampling.LANCZOS)
    tint_image(frame01, (200, 235, 255, 255)).save(os.path.join(OUT_GUI, "border.png"))
    build_panel_modal((256, 128), corner_frame=24).save(os.path.join(OUT_GUI, "Button Normal.png"))

    print("Synty UI textures generated successfully.")


if __name__ == "__main__":
    main()
