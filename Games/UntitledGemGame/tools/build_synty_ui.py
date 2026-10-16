#!/usr/bin/env python3
"""
Builds the Synty InterfaceSciFiSoldierHUD texture set for UntitledGemGame's
Menus, HUD, and GUI panels/nodes.
Source pack: /home/johan/My project/Assets/Synty/InterfaceSciFiSoldierHUD/Sprites
"""

import os
import shutil
from PIL import Image, ImageChops, ImageEnhance

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


def tint_image(img: Image.Image, rgba: tuple[int, int, int, int]) -> Image.Image:
    """Multiply an RGBA image by a color + alpha tuple (like Unity's Image.color)."""
    r, g, b, a = rgba
    solid = Image.new("RGBA", img.size, (r, g, b, a))
    return ImageChops.multiply(img, solid)


def polish_painted_metal(img: Image.Image) -> Image.Image:
    """
    Preserves Synty's authentic hand-painted titanium/steel gray, dark gunmetal recesses,
    and warm orange/amber industrial accents (as used with Color.white in the Unity sample)
    without contrast-clipping highlights into off-white color casts.
    """
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
    - The top and bottom rails (x in frame..w-frame) are horizontally uniform, so
      horizontal stretching never smears bolts, notches, or bracket tips.
    - The left and right rails (y in frame..h-frame) are vertically uniform, so
      vertical stretching never smears side details.
    - The center patch (frame..w-frame, frame..h-frame) is 100% uniform, so
      large panels never show stretched streaks or inner double-lines.
    """
    out = img.copy()
    w, h = out.size
    if w <= frame * 2 + 4 or h <= frame * 2 + 4:
        return out

    mid_x = w // 2
    mid_y = h // 2
    center_px = out.getpixel((mid_x, mid_y))

    # Sample the clean rail profiles at the midpoint
    top_profile = [out.getpixel((mid_x, y)) for y in range(frame)]
    bot_profile = [out.getpixel((mid_x, y)) for y in range(h - frame, h)]
    left_profile = [out.getpixel((x, mid_y)) for x in range(frame)]
    right_profile = [out.getpixel((x, mid_y)) for x in range(w - frame, w)]

    # Top and bottom middle strips
    for x in range(frame, w - frame):
        for y in range(frame):
            out.putpixel((x, y), top_profile[y])
        for idx, y in enumerate(range(h - frame, h)):
            out.putpixel((x, y), bot_profile[idx])

    # Left and right middle strips
    for y in range(frame, h - frame):
        for x in range(frame):
            out.putpixel((x, y), left_profile[x])
        for idx, x in enumerate(range(w - frame, w)):
            out.putpixel((x, y), right_profile[idx])

    # Center patch
    center_patch = Image.new("RGBA", (w - frame * 2, h - frame * 2), center_px)
    out.paste(center_patch, (frame, frame))
    return out


def make_edge_sheen(
    size: tuple[int, int],
    band_h: int,
    top_rgba: tuple[int, int, int, int],
    bot_rgba: tuple[int, int, int, int] | None = None,
) -> Image.Image:
    """
    Creates a 1D vertical edge sheen strictly confined to `0 .. band_h - 1` (and
    optionally `h - band_h .. h - 1`), 100% uniform in X. Because `band_h <= c < frame`,
    the sheen reaches 0 before the 9-slice split line `y = frame`, guaranteeing
    zero rectangular seams at any 9-slice size.
    """
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


def build_button(
    size: tuple[int, int],
    active: bool,
    green: bool = False,
) -> Image.Image:
    """
    Constructs a crisp Synty SciFi Soldier button with a single-contour chamfered
    `SPR_HUD_SciFiSoldier_Box_02` fill and `SPR_HUD_SciFiSoldier_Frame_02` border
    matching the HUD's geometry (`CustomFrameTextureCoordinateWidth = 12`).
    """
    box_hud = load_hud("SPR_HUD_SciFiSoldier_Box_02.png")
    frame_hud = load_hud("SPR_HUD_SciFiSoldier_Frame_02.png")

    # Keep the entire chamfer, sheen, and border strictly within 11px so Gum's 12px 9-slice
    # never stretches any corner or produces any 9-slice seam.
    c = 11
    dst_b = (c, c, c, c)
    base_shape = nine_slice_resize(box_hud, size, (54, 54, 54, 54), dst_b)

    if green:
        fill_rgba = (12, 48, 38, 248) if active else (6, 26, 22, 240)
        sheen_rgba = (64, 210, 140, 56 if active else 24)
        rim_rgba = (104, 230, 152, 255) if active else (68, 178, 122, 225)
    else:
        fill_rgba = (14, 40, 66, 248) if active else (6, 20, 38, 238)
        sheen_rgba = (48, 175, 240, 60 if active else 26)
        rim_rgba = (82, 228, 242, 255) if active else (54, 138, 168, 225)

    out = tint_image(base_shape, fill_rgba)
    sheen = make_edge_sheen(size, c, sheen_rgba)
    out = alpha_composite_clipped(out, sheen, base_shape)

    border = nine_slice_resize(frame_hud, size, (54, 54, 54, 54), dst_b)
    out = Image.alpha_composite(out, tint_image(border, rim_rgba))

    return enforce_clean_9slice(out, frame=12)


def build_tab(size: tuple[int, int], active: bool) -> Image.Image:
    return build_button(size, active=active, green=False)


def build_panel_modal(size: tuple[int, int], corner_frame: int = 24) -> Image.Image:
    """
    Builds `modal_info_complete.png` and `Button Normal.png` using a clean single-contour
    Synty HUD chamfered frame (`Box_02` + `Frame_02`) with a seam-free top/bottom edge sheen.
    """
    c = min(14, corner_frame - 2)
    dst_b = (c, c, c, c)

    box_hud = load_hud("SPR_HUD_SciFiSoldier_Box_02.png")
    frame_hud = load_hud("SPR_HUD_SciFiSoldier_Frame_02.png")

    base_shape = nine_slice_resize(box_hud, size, (54, 54, 54, 54), dst_b)
    out = tint_image(base_shape, (5, 19, 35, 236))

    sheen = make_edge_sheen(size, c, (38, 155, 225, 48), (24, 110, 175, 32))
    out = alpha_composite_clipped(out, sheen, base_shape)

    rim = nine_slice_resize(frame_hud, size, (54, 54, 54, 54), dst_b)
    out = Image.alpha_composite(out, tint_image(rim, (52, 185, 245, 242)))

    return enforce_clean_9slice(out, frame=corner_frame)


def build_hud_bar(size: tuple[int, int], corner_frame: int = 24) -> Image.Image:
    """
    Builds `hud_bar_background.png` for the bottom and top in-game HUD bars using a
    semi-transparent Synty HUD chamfered box (`Box_02`), seam-free top/bottom glass sheen,
    and crisp border (`Frame_02`).
    """
    c = min(14, corner_frame - 2)
    dst_b = (c, c, c, c)

    box_hud = load_hud("SPR_HUD_SciFiSoldier_Box_02.png")
    frame_hud = load_hud("SPR_HUD_SciFiSoldier_Frame_02.png")

    base_shape = nine_slice_resize(box_hud, size, (54, 54, 54, 54), dst_b)
    out = tint_image(base_shape, (4, 15, 28, 178))

    sheen = make_edge_sheen(size, c, (28, 128, 195, 68), (18, 92, 148, 42))
    out = alpha_composite_clipped(out, sheen, base_shape)

    rim = nine_slice_resize(frame_hud, size, (54, 54, 54, 54), dst_b)
    out = Image.alpha_composite(out, tint_image(rim, (56, 168, 218, 215)))

    return enforce_clean_9slice(out, frame=corner_frame)


def build_header(
    size: tuple[int, int],
    complete: bool,
    corner_frame: int = 24,
    flat_bottom: bool = False,
    divider_only: bool = False,
) -> Image.Image:
    """
    Builds header and sub-heading surfaces:
    - `modal_title_complete.png` (`flat_bottom=True`): top corners have the 14px chamfer matching
      `modal_info_complete.png`, while the left/right edges run straight down into a horizontal
      bottom divider so panel headers never pinch inward at the sides.
    - `sub_heading.png` (`complete=True, flat_bottom=False`): clean 4-corner chamfered title badge.
    - `sub_heading2.png` (`divider_only=True`): clean bottom horizontal divider line for tooltips
      so the tooltip title does not draw a second box border inside the tooltip's top border.
    """
    w, h = size
    c = min(14 if corner_frame >= 24 else corner_frame - 1, h // 3)
    dst_b = (c, c, c, c)

    box_hud = load_hud("SPR_HUD_SciFiSoldier_Box_02.png")
    frame_hud = load_hud("SPR_HUD_SciFiSoldier_Frame_02.png")
    line02 = load_hud("SPR_HUD_SciFiSoldier_Line_Horizontal_02.png", crop_bbox=True)

    if divider_only:
        out = Image.new("RGBA", size, (0, 0, 0, 0))
        line_img = tint_image(line02.resize((w - 16, 3), Image.Resampling.LANCZOS), (52, 185, 245, 215))
        out.alpha_composite(line_img, (8, h - 5))
        return enforce_clean_9slice(out, frame=corner_frame)

    if not complete:
        base_shape = nine_slice_resize(box_hud, size, (54, 54, 54, 54), dst_b)
        out = tint_image(base_shape, (6, 24, 46, 224))
        sheen = make_edge_sheen(size, c, (34, 145, 215, 48))
        out = alpha_composite_clipped(out, sheen, base_shape)
        rim = nine_slice_resize(frame_hud, size, (54, 54, 54, 54), dst_b)
        out = Image.alpha_composite(out, tint_image(rim, (42, 162, 228, 225)))
        return enforce_clean_9slice(out, frame=corner_frame)

    if flat_bottom:
        # Render taller by `c + 8` and crop the top `h` pixels so the bottom corners are straight vertical walls
        tall_sz = (w, h + c + 8)
        base_tall = nine_slice_resize(box_hud, tall_sz, (54, 54, 54, 54), dst_b)
        out_tall = tint_image(base_tall, (7, 28, 54, 228))

        sheen_tall = make_edge_sheen(tall_sz, c, (42, 168, 240, 62))
        out_tall = alpha_composite_clipped(out_tall, sheen_tall, base_tall)

        rim_tall = nine_slice_resize(frame_hud, tall_sz, (54, 54, 54, 54), dst_b)
        out_tall = Image.alpha_composite(out_tall, tint_image(rim_tall, (64, 198, 255, 245)))

        out = out_tall.crop((0, 0, w, h))
        # Crisp horizontal bottom border connecting the left and right vertical walls
        div = Image.new("RGBA", (w, 2), (58, 195, 255, 235))
        out.alpha_composite(div, (0, h - 2))
        return enforce_clean_9slice(out, frame=corner_frame)

    base_shape = nine_slice_resize(box_hud, size, (54, 54, 54, 54), dst_b)
    out = tint_image(base_shape, (7, 28, 54, 224))
    sheen = make_edge_sheen(size, c, (42, 168, 240, 56))
    out = alpha_composite_clipped(out, sheen, base_shape)

    rim = nine_slice_resize(frame_hud, size, (54, 54, 54, 54), dst_b)
    out = Image.alpha_composite(out, tint_image(rim, (64, 198, 255, 242)))

    return enforce_clean_9slice(out, frame=corner_frame)


def build_option_background(size: tuple[int, int], corner_frame: int = 12) -> Image.Image:
    c = corner_frame - 1
    dst_b = (c, c, c, c)

    box_hud = load_hud("SPR_HUD_SciFiSoldier_Box_02.png")
    frame_hud = load_hud("SPR_HUD_SciFiSoldier_Frame_02.png")

    base_shape = nine_slice_resize(box_hud, size, (54, 54, 54, 54), dst_b)
    out = tint_image(base_shape, (6, 20, 38, 232))

    border = nine_slice_resize(frame_hud, size, (54, 54, 54, 54), dst_b)
    out = Image.alpha_composite(out, tint_image(border, (54, 134, 166, 215)))

    return enforce_clean_9slice(out, frame=corner_frame)


def build_toggle(size: tuple[int, int], is_on: bool) -> Image.Image:
    """
    Builds a crisp 104x63 Synty toggle switch track with an inset recessed slot so
    both ON and OFF hexagon knobs sit cleanly inside the track without overflowing.
    """
    w, h = size
    out = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    inset_y = 6
    track_size = (w, h - inset_y * 2)  # 104x51
    c = 12
    dst_b = (c, c, c, c)

    box_hud = load_hud("SPR_HUD_SciFiSoldier_Box_02.png")
    frame_hud = load_hud("SPR_HUD_SciFiSoldier_Frame_02.png")

    base_shape = nine_slice_resize(box_hud, track_size, (54, 54, 54, 54), dst_b)

    if is_on:
        fill = (10, 68, 86, 250)
        border_c = (78, 235, 215, 255)
    else:
        fill = (4, 18, 34, 246)
        border_c = (46, 118, 152, 225)

    track = tint_image(base_shape, fill)
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
    Using inner_size allows `toggle_on_knob.png` (59x59) and `toggle_off_knob.png` (47x47)
    to have the exact same visual knob diameter inside the 63px toggle track.
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


def build_tree_node_background(size: tuple[int, int]) -> Image.Image:
    """
    Builds `icon_background.png` (128x128) for upgrade/talent/system tree nodes.
    Tree nodes get their outer status border from `DrawButtonBorders` (SDF rect with 2px radius)
    and their bottom rank rail from `DrawUpgradeNodeStatus`. Using a full-bleed recessed Synty
    surface (`Box_04` + subtle inner frame) prevents diagonal corner cutouts and triple borders
    from clashing with the SDF border and rank rail.
    """
    box04 = load_hud("SPR_HUD_SciFiSoldier_Box_04.png")
    frame01 = load_hud("SPR_HUD_SciFiSoldier_Frame_01.png")

    base_shape = nine_slice_resize(box04, size, (35, 35, 35, 35), (4, 4, 4, 4))
    out = tint_image(base_shape, (6, 22, 42, 248))

    rim = nine_slice_resize(frame01, size, (8, 8, 8, 8), (4, 4, 4, 4))
    out = Image.alpha_composite(out, tint_image(rim, (32, 118, 172, 140)))
    return out


def build_node_slot(size: tuple[int, int], checked: bool = False, alt: bool = False, corner_frame: int = 24) -> Image.Image:
    """
    Builds HUD ability slots (`radio_unchecked.png`, `radio_checked_background.png` 100x100)
    using the same clean single-contour Synty HUD chamfer (`Box_02` + `Frame_02`)
    as `OrbitSkin.HudButton` and `OrbitSkin.FlatPanel`.
    """
    c = min(14, corner_frame - 2)
    dst_b = (c, c, c, c)

    box_hud = load_hud("SPR_HUD_SciFiSoldier_Box_02.png")
    frame_hud = load_hud("SPR_HUD_SciFiSoldier_Frame_02.png")

    base_shape = nine_slice_resize(box_hud, size, (54, 54, 54, 54), dst_b)
    fill_c = (12, 48, 84, 248) if checked else (6, 20, 38, 244)
    out = tint_image(base_shape, fill_c)

    sheen_c = (255, 210, 90, 48) if (checked and alt) else ((70, 220, 255, 48) if checked else (42, 165, 235, 28))
    sheen = make_edge_sheen(size, c, sheen_c)
    out = alpha_composite_clipped(out, sheen, base_shape)

    rim = nine_slice_resize(frame_hud, size, (54, 54, 54, 54), dst_b)
    rim_c = (255, 204, 82, 255) if (checked and alt) else ((95, 228, 255, 255) if checked else (54, 158, 218, 235))
    out = Image.alpha_composite(out, tint_image(rim, rim_c))

    return enforce_clean_9slice(out, frame=corner_frame)


def build_slider_track(size: tuple[int, int]) -> Image.Image:
    """Builds a 20px-tall recessed Synty bar track with a clear cyan/steel border."""
    w, h = size
    box_hud = load_hud("SPR_HUD_SciFiSoldier_Box_04.png")
    frame_hud = load_hud("SPR_HUD_SciFiSoldier_Frame_01.png")
    c = min(6, h // 2)
    base = nine_slice_resize(box_hud, size, (35, 35, 35, 35), (c, c, c, c))
    out = tint_image(base, (5, 24, 46, 246))
    rim = nine_slice_resize(frame_hud, size, (8, 8, 8, 8), (4, 4, 4, 4))
    out = Image.alpha_composite(out, tint_image(rim, (38, 142, 215, 235)))
    return enforce_clean_9slice(out, frame=6)


def build_radial_glow(size: tuple[int, int] = (256, 256)) -> Image.Image:
    """
    Builds a smooth radial glow texture (256x256, white RGB, smooth C2 radial alpha falloff
    that reaches 0 at r=1 with sub-LSB Bayer dithering so low-alpha tints never posterize).
    """
    import numpy as np

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

    # 1. Buttons (360x88 and 289x88, 9-slice frame=12)
    build_button((360, 88), active=False, green=False).save(os.path.join(OUT_MENU, "button_idle_blue.png"))
    build_button((360, 88), active=True, green=False).save(os.path.join(OUT_MENU, "button_active_blue.png"))
    build_button((360, 88), active=False, green=True).save(os.path.join(OUT_MENU, "button_idle_green.png"))
    build_button((360, 88), active=True, green=True).save(os.path.join(OUT_MENU, "button_active_green.png"))

    build_button((289, 88), active=False, green=False).save(os.path.join(OUT_MENU, "modal_button_left.png"))
    build_button((289, 88), active=True, green=False).save(os.path.join(OUT_MENU, "modal_button_right.png"))

    # 2. Tabs (frame=12)
    build_tab((375, 100), active=False).save(os.path.join(OUT_MENU, "tab_inactive.png"))
    build_tab((389, 106), active=True).save(os.path.join(OUT_MENU, "tab_active.png"))

    # 3. Modals, Panels, Headers, Option Rows
    build_panel_modal((600, 260), corner_frame=24).save(os.path.join(OUT_MENU, "modal_info_complete.png"))
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
    # `modal_title_dashes.png` is 9-sliced at height=16 with frame=4 in DrawTitleBanner;
    # center the 3px rule inside a 16px-tall canvas so top/bottom 4px slices don't duplicate the line.
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
        "synty_frame_techy_01.png": os.path.join(SCIFI_DIR, "SPR_SciFiSoldier_Frame_Techy_01.png"),
        "synty_frame_techydark_01.png": os.path.join(SCIFI_DIR, "SPR_SciFiSoldier_Frame_TechyDark_01.png"),
        "synty_frame_brackets_01.png": os.path.join(SCIFI_DIR, "SPR_SciFiSoldier_Frame_Brackets_01.png"),
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
        "synty_hud_frame_04.png": os.path.join(HUD_DIR, "SPR_HUD_SciFiSoldier_Frame_04.png"),
        "synty_hud_frame_06.png": os.path.join(HUD_DIR, "SPR_HUD_SciFiSoldier_Frame_06.png"),
        "synty_hud_glass_02.png": os.path.join(HUD_DIR, "SPR_HUD_SciFiSoldier_Box_Glass_02.png"),
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
        "synty_hud_ring_large_01.png": os.path.join(HUD_DIR, "SPR_HUD_SciFiSoldier_Ring_Large_01.png"),
        "synty_hud_ring_large_02.png": os.path.join(HUD_DIR, "SPR_HUD_SciFiSoldier_Ring_Large_02.png"),
        "synty_hud_ring_hex_med.png": os.path.join(HUD_DIR, "SPR_HUD_SciFiSoldier_Ring_Hexagon_Medium_01_Clean.png"),
        "synty_hud_hex_underlay.png": os.path.join(HUD_DIR, "SPR_HUD_SciFiSoldier_Ring_Hexagon_Medium_01_Underlay.png"),
        "synty_hud_pip_hex_active.png": os.path.join(HUD_DIR, "SPR_HUD_SciFiSoldier_Pip_Hexagon_01_Active.png"),
        "synty_hud_pip_hex_inactive.png": os.path.join(HUD_DIR, "SPR_HUD_SciFiSoldier_Pip_Hexagon_01_Inactive.png"),
        "synty_fx_hexpatch.png": os.path.join(FX_DIR, "SPR_FX_SciFiSoldier_HexPatch_01.png"),
        "synty_example_bg.png": "/home/johan/My project/Assets/Synty/InterfaceSciFiSoldierHUD/Samples/Sprites/SPR_HUD_SciFiSoldier_Example_Background_01.png",
    }
    for dst_name, src_path in raw_copies.items():
        shutil.copyfile(src_path, os.path.join(OUT_MENU, dst_name))

    build_radial_glow((256, 256)).save(os.path.join(OUT_MENU, "synty_fx_glow_01.png"))

    # Also save a horizontally flipped version of Frame_Detail_Large_01 for the right half of the screen
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
