# itch.io page assets — 25 October 2026

[Review the shared footage](../../Steam/2026-10-25-refresh/review.html) ·
[Steam package and description copy](../../Steam/2026-10-25-refresh/STORE_PAGE.md) ·
[Gameplay trailer (1080p)](../../Trailers/03-gameplay-trailer/renders/btb_trailer_03_gameplay_v4.mp4)

This folder contains eleven looping GIFs and eleven 1920×1080 JPEG screenshots,
all under 2.9 MB each. They reuse the same 25 October 4K footage as the Steam
package (updated 3D planet and shell, 3D prerendered ships, faceted gem rendering,
GPU particles, and `GuiSkin` UI). GIFs trade resolution/frame rate for the
image upload budget; use the JPEGs for sharp UI details and the Steam MP4s for
higher-quality video. Nothing is published.

## Formats and sizes

| Page content | Supported route / limit evidence | Prepared assets |
|---|---|---|
| Screenshots / description images | PNG, JPEG/JPG and GIF are the reliable image route. A [public uploader report](https://itch.io/t/3235883/supported-image-format-mp4-not-actually-supported) quotes these formats plus MP4 and a 3 MB limit, while reporting that MP4 could not be selected. An [itch.io administrator](https://itch.io/post/66795) confirms screenshot size limits. | Eleven JPEGs, 1920×1080, each below 2.9 MB. |
| Animated feature images | Use GIF below the conservative 3 MB image budget. The [administrator's GIF guidance](https://itch.io/t/683238/problem-with-gif) confirms this budget for cover images. | Eleven looping GIFs, 480–640 px wide as needed, individually below 2.9 MB. |
| Trailer / video | The [official design guide](https://itch.io/docs/creators/design#video-url-trailer) documents publicly accessible YouTube/Vimeo links (also Sketchfab); video sits above the screenshots. A standalone MP4 upload is not required for this route. | The gameplay trailer (`btb_trailer_03_gameplay_v4.mp4`), music-only shorts and Steam MP4 clips can be hosted and reused; no video has been uploaded. |
| Cover image | [MonoGame's publishing guide](https://docs.monogame.net/articles/tutorials/building_2d_games/26_publish_to_itch/#adding-visual-assets) recommends 630×500, with 315×250 minimum. This is a separate asset from the screenshot gallery. | Keep the existing cover, or create a separate 630×500 cover when updating the page branding. |

The official itch.io design guide does not specify a mandatory screenshot pixel
size. 1920×1080 is our export choice, preserving a consistent 16:9 gallery and
sharp text. The 3 MB image budget is supported by uploader reports and older
staff guidance, rather than a comprehensive current specification in that guide;
all prepared files are kept below it. MP4 is explicitly mentioned in the reported
uploader help text, but its file-picker support is uncertain, so the ready-to-use
animation set uses GIF. The game-download upload limit is a separate limit.

## Page placement

Upload the JPEGs from `screenshots/` in the same order as the Steam gallery.
For a compact description, use four or five GIFs alongside headings: mining,
fleet, systems, modules and extraction. The remaining GIFs are available for
feature sections or devlogs. Put the feature copy in the page editor beside the
animation rather than burning text into the image. Use the official theme
editor's screenshot sidebar for downloadable builds or inline images for a
single-column page.

The matching Steam MP4s are in `../../Steam/2026-10-25-refresh/clips/`.
Their GIF counterparts have the same feature names in `gifs/`. The content is
real gameplay with staged progression; no invented play-time claims are used.
