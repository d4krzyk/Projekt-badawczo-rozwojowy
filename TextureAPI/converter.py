from PIL import Image, ImageFilter


def convert_to_pixel_art(image):
    image = image.convert("RGB")  # Upewniamy się, że obraz jest w trybie RGB

    # Przed zmniejszeniem: Wykonaj filtr Sobela do detekcji krawędzi (zmienia obraz na L, czyli grayscale)
    #image = image.filter(ImageFilter.EDGE_ENHANCE)
    #image = image.filter(ImageFilter.FIND_EDGES)

    # Zmniejsz obraz stopniowo (np. do 64x64, a potem do 32x32)
    image_resized = image.resize((512, 512), Image.BILINEAR)  # Łagodniejsze zmniejszenie do 128x128
    image_resized = image_resized.resize((256, 256), Image.BILINEAR)  # Łagodniejsze zmniejszenie do 128x128
    image_resized = image_resized.resize((128, 128), Image.BILINEAR)  # Łagodniejsze zmniejszenie do 128x128
    image_resized = image_resized.resize((64, 64), Image.NEAREST)
    #image_resized = image_resized.resize((32, 32), Image.NEAREST)  # Używamy 'NEAREST' dla pixel artu

    if image_resized.mode == 'P':
        image_resized = image_resized.convert('RGB')
    # Dodaj wyostrzenie (dla zachowania detali)
    image_sharpened = image_resized.filter(ImageFilter.SHARPEN)

    # Zapisz wynikowy obraz
    result = image_resized.copy()
    result.save("assets/output.png")
    return "assets/output.png"
