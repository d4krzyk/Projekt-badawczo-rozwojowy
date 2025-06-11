import multiprocessing
import random
import time
import gi
import pytest
import main

gi.require_version('Gtk', '3.0')
gi.require_version('GdkPixbuf', '2.0')
from gi.repository import Gtk, Gio, GdkPixbuf


@pytest.fixture
def app():
    multiprocessing.set_start_method('spawn', force=True)
    app_instance = main.PixelArtApp(mode="cpu")
    app_instance.show_all()
    yield app_instance
    app_instance.destroy()

def test_buttons_ready_after_model_ready(app):
    # Na początku przyciski powinny być zablokowane
    assert not app.buttonGen.get_sensitive()
    assert not app.buttonRandom.get_sensitive()

    # Symulujemy, że model jest gotowy
    app.gen_queue.put({"status": "model_ready"})
    app.check_generation_result()

    # wymuszamy przetworzenie eventów
    for _ in range(10):
        while Gtk.events_pending():
            Gtk.main_iteration_do(False)
        time.sleep(0.01)

    # przyciski powinny być odblokowane
    assert app.buttonGen.get_sensitive()
    assert app.buttonRandom.get_sensitive()


def test_rand_button_sets_entry(app):
    # Upewnijmy się, że model jest "ready", bo bez tego button nie działa
    app.gen_queue.put({"status": "model_ready"})
    app.check_generation_result()

    # Entry puste na start
    app.entry.set_text("")
    assert app.entry.get_text() == ""

    # Klikamy "Pick Random"
    app.on_rand_button_clicked(app.buttonRandom)

    # Teraz powinno być coś w Entry
    text_after = app.entry.get_text()
    assert text_after in app.available_categories

def test_generate_with_category_no_gui(app):
    random_category = random.choice(app.available_categories)
    app.entry.set_text(random_category)

    # Symulujemy kliknięcie "Generate"
    app.on_gen_button_clicked(None)

    time.sleep(1)
    # Teraz sprawdzamy, czy control_queue dostała poprawną wiadomość
    msg = app.control_queue.get(timeout=5)
    assert msg["action"] == "generate"
    assert msg["category"] == random_category

