#!/usr/bin/env python3
"""
Generator tekstur do preloadowania
Generuje tekstury dla każdej kategorii z category_prompts.json
i zapisuje je w JSON z base64 encodingiem.

Użycie:
    python preload_texture_generator.py [--mode cpu|cuda] [--output OUTPUT_FILE] [--num-per-category N]
"""

import argparse
import json
import os
import sys
import uuid
from pathlib import Path
from datetime import datetime

# Dodaj TextureAPI do PATH
sys.path.insert(0, os.path.join(os.path.dirname(__file__), "TextureAPI"))

from TextureAPI.prompt_config import TextureModel
from TextureAPI.generator_api import generate_all_images


def generate_textures_for_category(category: str, num_textures: int, model: TextureModel) -> list:
    """
    Generuje tekstury dla danej kategorii.
    
    Args:
        category: Nazwa kategorii (np. "Academic disciplines")
        num_textures: Liczba zestawów tekstur do wygenerowania
        model: Załadowany model TextureModel
    
    Returns:
        Lista słowników zawierających texture_id i base64 tekstury
    """
    results = []
    
    for i in range(num_textures):
        try:
            print(f"  Generowanie zestawu {i + 1}/{num_textures}...", end=" ", flush=True)
            
            # Generuj wszystkie tekstury dla tej kategorii
            images = generate_all_images(
                types=["wall", "floor", "bookcase"],
                category=category,
                model=model
            )
            
            # Utwórz unikalny ID dla tego zestawu tekstur
            texture_id = f"{category.replace(' ', '_')}_{i}_{uuid.uuid4().hex[:8]}"
            
            # Przygotuj wpis w JSON
            texture_entry = {
                "texture_id": texture_id,
                "texture_wall": images["wall"],
                "texture_floor": images["floor"],
                "texture_bookcase": images["bookcase"]
            }
            
            results.append(texture_entry)
            print("✓")
            
        except Exception as e:
            print(f"✗ Błąd: {e}")
            continue
    
    return results


def main():
    parser = argparse.ArgumentParser(
        description="Generator tekstur dla wszystkich kategorii z base64 encodingiem"
    )
    parser.add_argument(
        "--mode",
        choices=["cpu", "cuda"],
        default="cuda",
        help="Tryb generowania (cpu lub cuda)"
    )
    parser.add_argument(
        "--output",
        default="TextureAPI/generated_textures.json",
        help="Ścieżka do pliku wyjściowego JSON"
    )
    parser.add_argument(
        "--num-per-category",
        type=int,
        default=3,
        help="Liczba zestawów tekstur na kategorię"
    )
    parser.add_argument(
        "--categories",
        nargs="+",
        default=None,
        help="Konkretne kategorie do wygenerowania (jeśli nie podane, generuje wszystkie)"
    )
    
    args = parser.parse_args()
    
    # Zmień na katalog TextureAPI
    os.chdir(os.path.join(os.path.dirname(__file__), "TextureAPI"))
    
    print(f"🎨 Texture Generator - {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    print(f"Tryb: {args.mode.upper()}")
    print(f"Tekstur na kategorię: {args.num_per_category}")
    print(f"Plik wyjściowy: {args.output}")
    print()
    
    try:
        # Załaduj kategorie z JSON
        with open("category_prompts.json", "r", encoding="utf-8") as f:
            prompt_data = json.load(f)
        
        categories = args.categories if args.categories else list(prompt_data.keys())
        
        print(f"Znalezione {len(categories)} kategorii(ach):")
        for cat in categories:
            print(f"  - {cat}")
        print()
        
        # Załaduj model
        print(f"Ładowanie modelu ({args.mode.upper()})...")
        model = TextureModel(mode=args.mode)
        print("✓ Model załadowany\n")
        
        # Generuj tekstury dla każdej kategorii
        all_textures = {}
        
        for category in categories:
            if category not in prompt_data:
                print(f"⚠️  Kategoria '{category}' nie znaleziona, pomijam")
                continue
            
            print(f"Generowanie tekstur dla: {category}")
            textures = generate_textures_for_category(
                category,
                args.num_per_category,
                model
            )
            
            if textures:
                all_textures[category] = textures
                print(f"✓ Wygenerowano {len(textures)} zestawów tekstur\n")
            else:
                print(f"✗ Nie udało się wygenerować tekstur dla {category}\n")
        
        # Zapisz wynik do JSON
        output_path = args.output
        os.makedirs(os.path.dirname(output_path) or ".", exist_ok=True)
        
        with open(output_path, "w", encoding="utf-8") as f:
            json.dump(all_textures, f, indent=2, ensure_ascii=False)
        
        print(f"✓ Tekstury zapisane do: {output_path}")
        print(f"✓ Operacja zakończona pomyślnie!")
        
        # Podsumowanie
        total_textures = sum(len(v) for v in all_textures.values())
        print(f"\n📊 Podsumowanie:")
        print(f"  Kategorie: {len(all_textures)}")
        print(f"  Łącznie zestawów: {total_textures}")
        print(f"  Rozmiar pliku: {os.path.getsize(output_path) / (1024**2):.2f} MB")
        
    except KeyboardInterrupt:
        print("\n⚠️  Generator został przerwany przez użytkownika")
        sys.exit(1)
    except Exception as e:
        print(f"\n❌ Błąd: {e}")
        import traceback
        traceback.print_exc()
        sys.exit(1)


if __name__ == "__main__":
    main()

