from PIL import Image, ImageDraw
import os
import random
import base64
import converter
from io import BytesIO
from prompt_config import TextureModel

def generate_image_base64(category: str, type, model) -> str:
    # Czyszczenie kategorii
    category_clean = category.replace(" ", "").lower()

    # Wywołanie procesu generowania
    img_generated = model.generate_api(category=category.upper(), type_texture=type)

    # # Ścieżka do wygenerowanego pliku
    # image_path = os.path.join("assets", f"{type}.png")
    #
    # if not os.path.exists(image_path):
    #     raise FileNotFoundError(f"Nie znaleziono pliku: {image_path}")
    #
    # # 🛠️ Właściwa część — już PO sprawdzeniu istnienia pliku
    # img = Image.open(image_path)
    pixel_art_img = converter.convert_to_pixel_art(image=img_generated, type=type)

    # Zapisz do bufora pamięci i zakoduj
    buffered = BytesIO()
    pixel_art_img.save(buffered, format="PNG")
    img_base64 = base64.b64encode(buffered.getvalue()).decode("utf-8")
    return img_base64

def generate_all_images(types: list[str], category, model) -> dict:
    return {type: generate_image_base64(category, type, model) for type in types}
