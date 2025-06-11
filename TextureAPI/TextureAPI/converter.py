from PIL import Image, ImageFilter
import os

def make_power_of_two(x):
    # zwraca największą potęgę dwójki <= x
    power = 1
    while power * 2 <= x:
        power *= 2
    return power

def convert_to_pixel_art(image, type, status):

    folder = "assets/canceled" if status == "cancelled" else "assets"
    os.makedirs(folder, exist_ok=True)  # tworzy folder, jeśli nie ma

    image = image.convert("RGB")

    # filtr Sobela do detekcji krawędzi (zmienia obraz na L, [grayscale]) opcjonalnie

    #image = image.filter(ImageFilter.EDGE_ENHANCE)
    #image = image.filter(ImageFilter.FIND_EDGES)

    # sprawdzamy rozmiar obrazu
    original_size = image.size
    w, h = original_size
    min_size = 64

    # tworzenie tablicy transformacji wymiarów obrazu
    sizes = []
    factor = 0.5
    current_w, current_h = w, h

    while current_w > min_size and current_h > min_size:
        current_w = make_power_of_two(int(current_w * factor))
        current_h = make_power_of_two(int(current_h * factor))
        sizes.append((current_w, current_h))

    # Stopniowe zmniejszanie obrazu poprzez tablice sizes
    for size in sizes:
        image = image.resize(size, Image.BILINEAR)

    # Na koniec pixel artowy nearest do 32x32
    image = image.resize((32, 32), Image.NEAREST)

    if image.mode == 'P':
        image = image.convert('RGB')

    # opcjonalne dodanie wyostrzenia (dla zachowania detali)
    # image_sharpened = image.filter(ImageFilter.SHARPEN)

    # Zapisz wynikowy obraz
    result = image.copy()
    result.save(f"{folder}/{type}.png")


