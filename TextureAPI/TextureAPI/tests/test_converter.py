import os
import shutil
import pytest
from PIL import Image
from converter import make_power_of_two, convert_to_pixel_art

@pytest.mark.parametrize("x, expected", [
    (0, 1),
    (1, 1),
    (2, 2),
    (3, 2),
    (4, 4),
    (5, 4),
    (8, 8),
    (9, 8),
    (16, 16),
    (31, 16),
    (32, 32),
    (33, 32),
    (64, 64),
    (1000, 512),
    (1024, 1024),
    (2047, 1024),
    (4096, 4096),
])
def test_make_power_of_two(x, expected):
    assert make_power_of_two(x) == expected

@pytest.fixture
def sample_image():
    # tworzenie sampla jako czerwony obrazek 130x130
    img = Image.new("RGB", (130, 130), color="red")
    return img

@pytest.fixture(autouse=True)
def cleanup_assets():
    # Przed każdym testem - usuwa się assets i assets/canceled (jeśli istnieją)
    yield
    if os.path.exists("assets"):
        shutil.rmtree("assets")


# TESTY convert_to_pixel_art

@pytest.mark.parametrize("status, expected_folder", [
    ("active", "assets"),
    ("cancelled", "assets/canceled"),
])
def test_convert_to_pixel_art_creates_file(sample_image, status, expected_folder):
    type_name = "test_image"

    convert_to_pixel_art(sample_image, type_name, status)

    expected_path = os.path.join(expected_folder, f"{type_name}.png")
    assert os.path.isfile(expected_path), f"File {expected_path} was not created."

    # Sprawdzamy wymiary i tryb obrazka
    result_image = Image.open(expected_path)
    assert result_image.size == (32, 32), "Output image is not 32x32."
    assert result_image.mode == "RGB", "Output image is not RGB."

