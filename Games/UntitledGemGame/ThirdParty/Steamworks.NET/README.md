# Steamworks.NET

Pinned official standalone release: [2025.164.1](https://github.com/rlabrecque/Steamworks.NET/releases/tag/2025.164.1).

Source archive: `Steamworks.NET-Standalone_2025.164.1.zip`.
SHA-256: `9412348cc404563be5a43a28347cfeda3c679ee044a14d87a507ed2d796a537d`.

The checked-in files are the unmodified x64 managed assemblies, matching native
Steam API libraries for Windows/Linux/macOS, and upstream MIT license from that
archive. Steamworks.NET is copyright Riley Labrecque. The native Steam API
libraries are Valve redistributables; their use is covered by Valve's Steamworks
SDK terms, not the wrapper's MIT license.

`Platform/Steamworks.targets` selects binaries using the publish runtime identifier
or, for ordinary local builds, the .NET SDK's runtime identifier. ARM and 32-bit
targets are not configured. Upgrade managed and native binaries together.
