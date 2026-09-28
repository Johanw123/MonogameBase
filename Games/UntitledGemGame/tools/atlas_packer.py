"""Deterministic untrimmed atlas packing, shared by the asset build scripts."""
from PIL import Image


def pack(images, padding):
    # Deterministic shelf packing, retaining full image dimensions and transparent margins.
    ordered = sorted(images, key=lambda path: (-images[path].height, -images[path].width, path))
    width = 256
    while True:
        x = y = row_height = 0
        frames = {}
        for path in ordered:
            image = images[path]
            w, h = image.width + 2 * padding, image.height + 2 * padding
            if w > width:
                break
            if x + w > width:
                x, y, row_height = 0, y + row_height, 0
            frames[path] = [x + padding, y + padding, image.width, image.height]
            x += w
            row_height = max(row_height, h)
        else:
            if y + row_height <= width:
                break
        width *= 2
        if width > 4096:
            raise ValueError('Images exceed a 4096px atlas; add page support before growing further.')
    height = 1
    while height < y + row_height:
        height *= 2
    atlas = Image.new('RGBA', (width, height))
    for path, (x, y, w, h) in frames.items():
        image = images[path]
        # Extrude edges, including corners, for safe linear filtering (no mipmaps).
        for dy in range(-padding, h + padding):
            source_y = min(h - 1, max(0, dy))
            row = image.crop((0, source_y, w, source_y + 1))
            atlas.paste(row, (x, y + dy))
            atlas.paste(row.crop((0, 0, 1, 1)).resize((padding, 1)), (x - padding, y + dy))
            atlas.paste(row.crop((w - 1, 0, w, 1)).resize((padding, 1)), (x + w, y + dy))
    return atlas, frames
