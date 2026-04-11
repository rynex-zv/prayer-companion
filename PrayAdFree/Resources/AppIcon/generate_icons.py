from __future__ import annotations

import json
from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parent
PROJECT_ROOT = ROOT.parent.parent
SOURCE = ROOT / "appicon.png"
ANDROID_RES = PROJECT_ROOT / "Platforms" / "Android" / "Resources"
IOS_ASSET = (
    PROJECT_ROOT / "Platforms" / "iOS" / "Resources" / "Assets.xcassets" / "appicon.appiconset"
)
MAC_ASSET = (
    PROJECT_ROOT
    / "Platforms"
    / "MacCatalyst"
    / "Resources"
    / "Assets.xcassets"
    / "appicon.appiconset"
)
WINDOWS_ASSETS = PROJECT_ROOT / "Platforms" / "Windows" / "Assets"
PLAY_STORE = ROOT / "PlayStore"


def resize_contain(base: Image.Image, size: tuple[int, int]) -> Image.Image:
    src = base.convert("RGBA")
    dst_w, dst_h = size
    scale = min(dst_w / src.width, dst_h / src.height)
    new_w = max(1, int(round(src.width * scale)))
    new_h = max(1, int(round(src.height * scale)))
    resized = src.resize((new_w, new_h), Image.Resampling.LANCZOS)
    canvas = Image.new("RGBA", size, (0, 0, 0, 0))
    canvas.paste(resized, ((dst_w - new_w) // 2, (dst_h - new_h) // 2), resized)
    return canvas


def resize_cover(base: Image.Image, size: tuple[int, int]) -> Image.Image:
    src = base.convert("RGBA")
    dst_w, dst_h = size
    scale = max(dst_w / src.width, dst_h / src.height)
    new_w = max(1, int(round(src.width * scale)))
    new_h = max(1, int(round(src.height * scale)))
    resized = src.resize((new_w, new_h), Image.Resampling.LANCZOS)
    left = max(0, (new_w - dst_w) // 2)
    top = max(0, (new_h - dst_h) // 2)
    return resized.crop((left, top, left + dst_w, top + dst_h))


def trim_transparency(base: Image.Image) -> Image.Image:
    src = base.convert("RGBA")
    alpha = src.getchannel("A")
    bbox = alpha.getbbox()
    if not bbox:
        return src
    return src.crop(bbox)


def save_icon(base: Image.Image, path: Path, size: tuple[int, int]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    resize_contain(base, size).save(path, format="PNG", optimize=True)


def save_icon_cover(base: Image.Image, path: Path, size: tuple[int, int]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    resize_cover(base, size).save(path, format="PNG", optimize=True)


def flatten_opaque(base: Image.Image, color: tuple[int, int, int] = (12, 22, 24)) -> Image.Image:
    src = base.convert("RGBA")
    bg = Image.new("RGBA", src.size, (*color, 255))
    bg.alpha_composite(src)
    return bg


def generate_android(base: Image.Image) -> None:
    # Trim transparent margins so launcher icons do not look zoomed out.
    android_base = trim_transparency(base)
    sizes = {
        "mipmap-mdpi": 48,
        "mipmap-hdpi": 72,
        "mipmap-xhdpi": 96,
        "mipmap-xxhdpi": 144,
        "mipmap-xxxhdpi": 192,
    }
    for folder, px in sizes.items():
        icon = resize_cover(android_base, (px, px))
        icon_opaque = flatten_opaque(icon)
        (ANDROID_RES / folder).mkdir(parents=True, exist_ok=True)
        icon_opaque.save(ANDROID_RES / folder / "appicon.png", format="PNG", optimize=True)
        icon_opaque.save(ANDROID_RES / folder / "appicon_round.png", format="PNG", optimize=True)
    play = flatten_opaque(resize_cover(android_base, (512, 512)))
    PLAY_STORE.mkdir(parents=True, exist_ok=True)
    play.save(PLAY_STORE / "appicon-512.png", format="PNG", optimize=True)


def generate_ios(base: Image.Image) -> None:
    appiconset = IOS_ASSET
    definitions = [
        ("Icon-20@2x.png", 40, "iphone", "20x20", "2x"),
        ("Icon-20@3x.png", 60, "iphone", "20x20", "3x"),
        ("Icon-29@2x.png", 58, "iphone", "29x29", "2x"),
        ("Icon-29@3x.png", 87, "iphone", "29x29", "3x"),
        ("Icon-40@2x.png", 80, "iphone", "40x40", "2x"),
        ("Icon-40@3x.png", 120, "iphone", "40x40", "3x"),
        ("Icon-60@2x.png", 120, "iphone", "60x60", "2x"),
        ("Icon-60@3x.png", 180, "iphone", "60x60", "3x"),
        ("Icon-20.png", 20, "ipad", "20x20", "1x"),
        ("Icon-20@2x-ipad.png", 40, "ipad", "20x20", "2x"),
        ("Icon-29.png", 29, "ipad", "29x29", "1x"),
        ("Icon-29@2x-ipad.png", 58, "ipad", "29x29", "2x"),
        ("Icon-40.png", 40, "ipad", "40x40", "1x"),
        ("Icon-40@2x-ipad.png", 80, "ipad", "40x40", "2x"),
        ("Icon-76.png", 76, "ipad", "76x76", "1x"),
        ("Icon-76@2x.png", 152, "ipad", "76x76", "2x"),
        ("Icon-83.5@2x.png", 167, "ipad", "83.5x83.5", "2x"),
        ("Icon-1024.png", 1024, "ios-marketing", "1024x1024", "1x"),
    ]
    images = []
    for file_name, px, idiom, size, scale in definitions:
        save_icon(base, appiconset / file_name, (px, px))
        images.append(
            {
                "filename": file_name,
                "idiom": idiom,
                "scale": scale,
                "size": size,
            }
        )
    contents = {"images": images, "info": {"author": "xcode", "version": 1}}
    (appiconset / "Contents.json").write_text(
        json.dumps(contents, indent=2) + "\n", encoding="utf-8"
    )


def generate_windows(base: Image.Image) -> None:
    windows_assets = {
        "Square44x44Logo": (44, 44),
        "Square71x71Logo": (71, 71),
        "Square150x150Logo": (150, 150),
        "Wide310x150Logo": (310, 150),
        "Square310x310Logo": (310, 310),
        "StoreLogo": (50, 50),
    }
    scales = [100, 125, 150, 200, 400]
    for asset_name, (w, h) in windows_assets.items():
        for scale in scales:
            sw = int(round(w * scale / 100))
            sh = int(round(h * scale / 100))
            file_name = f"{asset_name}.scale-{scale}.png"
            save_icon(base, WINDOWS_ASSETS / file_name, (sw, sh))


def ensure_windows_base_icons() -> None:
    base_names = [
        "Square44x44Logo",
        "Square71x71Logo",
        "Square150x150Logo",
        "Wide310x150Logo",
        "Square310x310Logo",
        "StoreLogo",
    ]
    for name in base_names:
        scale_100 = WINDOWS_ASSETS / f"{name}.scale-100.png"
        base_file = WINDOWS_ASSETS / f"{name}.png"
        if scale_100.exists() and not base_file.exists():
            base_file.write_bytes(scale_100.read_bytes())


def copy_ios_assets_to_mac() -> None:
    if not IOS_ASSET.exists():
        return
    for item in IOS_ASSET.rglob("*"):
        if item.is_dir():
            continue
        rel = item.relative_to(IOS_ASSET)
        target = MAC_ASSET / rel
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(item.read_bytes())


def main() -> None:
    if not SOURCE.exists():
        raise FileNotFoundError(f"Missing source icon: {SOURCE}")
    PLAY_STORE.mkdir(parents=True, exist_ok=True)
    with Image.open(SOURCE) as base:
        generate_android(base)
        generate_ios(base)
        generate_windows(base)
    copy_ios_assets_to_mac()
    ensure_windows_base_icons()
    print("Generated icons into platform folders.")


if __name__ == "__main__":
    main()
