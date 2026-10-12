"""Compile a real texture through the web builder with paths containing spaces.

On Linux, a test-only Wine shim runs MGCB with native .NET and FreeImage.
This exercises manifest/path handling; it does not test Wine, shaders or audio.
"""
import argparse
import os
from pathlib import Path
import shutil
import subprocess
import tempfile

root = Path(__file__).resolve().parents[2]
packages = Path.home() / ".nuget/packages"
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--mgcb", type=Path, default=packages / "nkast.xna.framework.content.pipeline.builder/4.2.9001/tools/MGCB.dll")
parser.add_argument("--freeimage", type=Path, default=packages / "monogame.library.freeimage/3.18.0.3/runtimes/linux-x64/native/libfreeimage.so")
args = parser.parse_args()
if not args.mgcb.is_file():
    parser.error("MGCB.dll is missing; restore the web project's dependencies first.")
if os.name != "nt" and not args.freeimage.is_file():
    parser.error("Pass --freeimage with the path to a Linux FreeImage shared library.")

subprocess.run(["dotnet", "build", str(root / "web/ContentBuilder/ContentBuilder.csproj"), "-c", "Release", "--no-restore", "-m:1"], check=True)
with tempfile.TemporaryDirectory(prefix="btb content check ") as scratch:
    game = Path(scratch) / "fixture game"
    source = game / "Content"
    (source / "Textures").mkdir(parents=True)
    (game / "ContentBuilder/Builder").mkdir(parents=True)
    (game / "ContentBuilder/Builder/Builder.cs").write_text(
        'contentCollection.Include<WildcardRule>("Textures/*.png");\n'
        'contentCollection.IncludeCopy<WildcardRule>("Textures/*.txt");\n'
    )
    shutil.copyfile(root / "Content/Textures/Platform/steam.png", source / "Textures/steam icon.png")
    (source / "Textures/raw asset.txt").write_text("raw asset")
    env = os.environ.copy()
    if os.name != "nt":
        shim = Path(scratch) / "tools"
        shim.mkdir()
        (shim / "libFreeImage.so").symlink_to(args.freeimage.resolve())
        wine = shim / "wine"
        wine.write_text('#!/bin/sh\nshift\nexec dotnet "$@"\n')
        wine.chmod(0o755)
        env["PATH"] = str(shim) + os.pathsep + env["PATH"]
        env["LD_LIBRARY_PATH"] = str(shim) + os.pathsep + env.get("LD_LIBRARY_PATH", "")
        env["MGFXC_WINE_PATH"] = str(Path(scratch) / "unused prefix")
    output = game / "output"
    subprocess.run([
        "dotnet", str(root / "web/ContentBuilder/bin/Release/net10.0/ContentBuilder.dll"),
        str(game), str(output), str(args.mgcb.resolve()),
    ], env=env, check=True)
    texture = output / "Textures/steam icon.xnb"
    assert texture.read_bytes().startswith(b"XNB"), "Texture was not compiled to XNB"
    assert (output / "Textures/raw asset.txt").read_text() == "raw asset"
    assert not (output / "Content").exists(), "Output unexpectedly gained a Content prefix"
    print("Content path check passed: external manifest, spaces, real texture compilation and raw copy.")

    import zipfile
    builder = root / "web/ContentBuilder/bin/Release/net10.0/ContentBuilder.dll"
    (output / "Music").mkdir()
    (output / "Music/test.mp3").write_bytes(b"streamed music")
    subprocess.run(["dotnet", str(builder), "--bundle-content", str(output)], check=True)
    with zipfile.ZipFile(output / "assets.zip") as bundle:
        assert bundle.read("Content/Textures/steam icon.xnb") == texture.read_bytes()
        assert bundle.read("Content/Textures/raw asset.txt") == b"raw asset"
        assert "Content/Music/test.mp3" not in bundle.namelist(), "Music must remain streamable"
    published = game / "published"
    shutil.copytree(output, published / "Content")
    (published / "index.html").write_text("game")
    package = game / "itch.zip"
    subprocess.run(["dotnet", str(builder), "--package-itch", str(published), str(package)], check=True)
    with zipfile.ZipFile(package) as upload:
        assert "Content/assets.zip" in upload.namelist()
        assert "Content/Music/test.mp3" in upload.namelist()
        assert "Content/Textures/steam icon.xnb" not in upload.namelist(), "Bundled files must not be duplicated in the upload"
    print("Bundle/package checks passed: compiled bytes and raw files preserved, music streamable, upload duplicates removed.")
