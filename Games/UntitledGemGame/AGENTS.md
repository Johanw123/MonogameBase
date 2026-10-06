# Save compatibility

The game has not been released, and existing saves belong only to developers.
Until a demo or 1.0 is released to playtesters, save changes do not need backward
compatibility. Do not add migrations, compatibility flags, or special handling
to preserve older saves. Keep saving and loading the current format working.

# AI-generated content

Whenever you add or replace an AI-generated asset (sprite, icon, texture,
sound or music), record it in AI_GENERATED_CONTENT.txt in the same change:
its path, what it is, how it was made and the date. If a person redraws an
asset, remove its entry.
