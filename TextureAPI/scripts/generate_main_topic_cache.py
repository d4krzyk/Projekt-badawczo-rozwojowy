#!/usr/bin/env python3
"""
Generate and append cache entries for the category "Main topic articles".

This script generates N texture sets (wall/floor/bookcase) and writes them
into TextureAPI/cache/cached_textures.json.

Usage examples:
  python generate_main_topic_cache.py --mode cuda --count 30
  python generate_main_topic_cache.py --mode cpu --count 30 --overwrite
"""

from __future__ import annotations

import argparse
import json
import sys
import uuid
from pathlib import Path


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Generate cached textures for 'Main topic articles'"
    )
    parser.add_argument(
        "--mode",
        choices=["cpu", "cuda"],
        default="cuda",
        help="Generation mode for TextureModel",
    )
    parser.add_argument(
        "--count",
        type=int,
        default=30,
        help="How many texture sets to generate",
    )
    parser.add_argument(
        "--category",
        default="Main topic articles",
        help="Category name to generate",
    )
    parser.add_argument(
        "--cache-file",
        default=None,
        help="Optional custom path to cached_textures.json",
    )
    parser.add_argument(
        "--overwrite",
        action="store_true",
        help="Replace existing entries in this category instead of appending",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()

    scripts_dir = Path(__file__).resolve().parent
    textureapi_dir = scripts_dir.parent / "TextureAPI"
    default_cache_file = scripts_dir.parent / "cache" / "cached_textures.json"
    cache_file = Path(args.cache_file) if args.cache_file else default_cache_file

    # Make local TextureAPI modules importable (generator_api.py, prompt_config.py).
    sys.path.insert(0, str(textureapi_dir))

    from prompt_config import TextureModel  # pylint: disable=import-error
    from generator_api import generate_all_images  # pylint: disable=import-error

    if not cache_file.exists():
        print(f"[ERROR] Cache file not found: {cache_file}")
        return 1

    with cache_file.open("r", encoding="utf-8") as f:
        cache_data = json.load(f)

    category = args.category
    existing = cache_data.get(category, [])
    if not isinstance(existing, list):
        print(f"[ERROR] Category '{category}' exists but is not a list.")
        return 1

    if args.overwrite:
        cache_data[category] = []
    else:
        cache_data.setdefault(category, [])

    print(f"[INFO] Loading model in {args.mode.upper()} mode...")
    model = TextureModel(mode=args.mode)
    print("[INFO] Model loaded.")

    start_index = len(cache_data[category])
    total = args.count

    for i in range(total):
        print(f"[INFO] Generating {i + 1}/{total}...")
        images = generate_all_images(
            types=["wall", "floor", "bookcase"],
            category=category,
            model=model,
        )

        texture_entry = {
            "texture_id": f"{category.replace(' ', '_')}_{start_index + i}_{uuid.uuid4().hex[:8]}",
            "texture_wall": images["wall"],
            "texture_floor": images["floor"],
            "texture_bookcase": images["bookcase"],
        }
        cache_data[category].append(texture_entry)

    with cache_file.open("w", encoding="utf-8") as f:
        json.dump(cache_data, f, indent=2, ensure_ascii=False)

    print(
        f"[OK] Added {total} entries to '{category}'. "
        f"Current total: {len(cache_data[category])}."
    )
    print(f"[OK] Saved: {cache_file}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
