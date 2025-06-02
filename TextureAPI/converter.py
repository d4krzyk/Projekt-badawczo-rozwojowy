from PIL import Image, ImageFilter
import os

def convert_to_pixel_art(image, type, status):

    folder = "assets/canceled" if status == "cancelled" else "assets"
    os.makedirs(folder, exist_ok=True)  # TO tworzy folder, jeśli nie ma

    image = image.convert("RGB")  # Upewniamy się, że obraz jest w trybie RGB

    # Przed zmniejszeniem: Wykonaj filtr Sobela do detekcji krawędzi (zmienia obraz na L, czyli grayscale)
    #image = image.filter(ImageFilter.EDGE_ENHANCE)
    #image = image.filter(ImageFilter.FIND_EDGES)

    # Stopniowe zmniejszanie
    sizes = [(512, 512), (256, 256), (128, 128), (64, 64)]
    for size in sizes:
        image = image.resize(size, Image.BILINEAR)
    # Na koniec pixel artowy nearest do 32x32
    image = image.resize((32, 32), Image.NEAREST)


    if image.mode == 'P':
        image = image.convert('RGB')
    # Dodaj wyostrzenie (dla zachowania detali)
    # image_sharpened = image.filter(ImageFilter.SHARPEN)

    # Zapisz wynikowy obraz
    result = image.copy()

    result.save(f"{folder}/{type}.png")


