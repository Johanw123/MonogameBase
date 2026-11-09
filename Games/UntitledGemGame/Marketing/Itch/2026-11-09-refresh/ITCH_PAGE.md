# itch.io page assets — 9 November 2026

[Review the shared footage](../../Steam/2026-11-09-refresh/review.html) ·
[Steam package and description copy](../../Steam/2026-11-09-refresh/STORE_PAGE.md) ·
[Previous refresh (25 October)](../2026-10-25-refresh/ITCH_PAGE.md)

This folder contains twelve looping GIFs and thirteen 1920×1080 JPEG
screenshots, each under 2.9 MB. They reuse the 9 November 4K footage of the
Steam package (build `a00bfd91`), including the new Weaponry panel and the
Tractor Belt. GIFs trade resolution and frame rate for the image upload budget.
Use the JPEGs for sharp UI details and the Steam MP4s for higher-quality video.
Nothing is published.

## Formats and sizes (checked 25 October 2026, not re-checked)

| Page content | Supported route / limit evidence | Prepared assets |
|---|---|---|
| Screenshots / description images | PNG, JPEG/JPG and GIF are the reliable image route. A [public uploader report](https://itch.io/t/3235883/supported-image-format-mp4-not-actually-supported) quotes these formats plus MP4 and a 3 MB limit, while reporting that MP4 could not be selected. An [itch.io administrator](https://itch.io/post/66795) confirms screenshot size limits. | Thirteen JPEGs, 1920×1080, each below 2.9 MB. |
| Animated feature images | Use GIF below the conservative 3 MB image budget. The [administrator's GIF guidance](https://itch.io/t/683238/problem-with-gif) confirms this budget for cover images. | Twelve looping GIFs, 480–640 px wide as needed, each below 2.9 MB. |
| Trailer / video | The [official design guide](https://itch.io/docs/creators/design#video-url-trailer) documents publicly accessible YouTube/Vimeo links (also Sketchfab); video sits above the screenshots. A standalone MP4 upload is not required for this route. | The music-only shorts and Steam MP4 clips can be hosted and reused; no video has been uploaded. The gameplay trailer was not recaptured in this refresh. |
| Cover image | [MonoGame's publishing guide](https://docs.monogame.net/articles/tutorials/building_2d_games/26_publish_to_itch/#adding-visual-assets) recommends 630×500, with 315×250 minimum. This is a separate asset from the screenshot gallery. | Keep the existing cover, or create a separate 630×500 cover when updating the page branding. |

The official itch.io design guide does not specify a mandatory screenshot pixel
size. 1920×1080 is our export choice, which keeps a consistent 16:9 gallery and
sharp text. The 3 MB image budget comes from uploader reports and older staff
guidance, not from a current specification, so all prepared files stay below it.

## Page placement

Upload the JPEGs from `screenshots/` in the same order as the Steam gallery
(Tractor Belt third, Weaponry after the upgrade tree). For a compact
description, use four or five GIFs alongside headings: mining, Tractor Belt,
Weaponry, modules and extraction. The remaining GIFs are available for feature
sections or devlogs. Put the feature copy in the page editor beside the
animation rather than burning text into the image.

The matching Steam MP4s are in `../../Steam/2026-11-09-refresh/clips/`, with
the same feature names as the GIFs in `gifs/`. The content is real gameplay with
staged progression, and makes no play-time claims.
