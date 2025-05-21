from PIL import Image


def apply_style(filepath, prompt):
    # Tutaj można w przyszłości użyć CLIP lub modelu
    image = Image.open(filepath)
    # Później tu dodasz modyfikacje obrazu zależnie od promptu
    return image
