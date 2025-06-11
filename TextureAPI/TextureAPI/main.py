import threading
import time
import json
import gi
from gi._option import GLib
import os
import io
import random
from multiprocessing import Process, Queue
import multiprocessing
from PIL import Image
import sys

gi.require_version('Gtk', '3.0')
gi.require_version('GdkPixbuf', '2.0')
from gi.repository import Gtk, Gio, GdkPixbuf
from converter import convert_to_pixel_art
from process_gen import generate_process_wrapper

TEXTURE_TYPES = ["wall", "floor", "bookcase"]

class PixelArtApp(Gtk.Window):
    def __init__(self, mode="cpu"):
        Gtk.Window.__init__(self, title="Pixel Art Stylizer")
        self.gen_mode = mode
        self.generated_types = set()
        self.gen_image_bookcase = None
        self.gen_image_wall = None
        self.gen_image_floor = None
        self.progress_pulse_timeout = None

        self.set_border_width(10)
        self.set_default_size(500, 600)

        print("Starting Pixel Art App GUI Version")

        # PROCES MODELU GENEROWANIA OBRAZU
        self.gen_queue = Queue()
        self.control_queue = Queue()
        self.gen_process = Process(
            target=generate_process_wrapper,
            args=(self.gen_queue, self.control_queue, self.gen_mode)
        )
        self.gen_process.start()

        # Odpal sprawdzanie wyników — tu będzie najpierw info, że model gotowy
        GLib.timeout_add(GLib.PRIORITY_DEFAULT, 100, self.check_generation_result)
        print(f"Generating process started? {self.gen_process.is_alive()}")
        #self.textureGen = TextureModel()

        self.notebook = Gtk.Notebook()
        self.add(self.notebook)
        self.current_page = 0  # zapamiętujemy aktywną kartę
        self.block_switch = False
        self.notebook.connect("switch-page", self.on_switch_page)

        # Strona 1 - generator tekstur
        self.page1_vbox = Gtk.Box(orientation=Gtk.Orientation.VERTICAL, spacing=10)
        self.page1_vbox.set_margin_top(10)
        self.page1_vbox.set_margin_bottom(5)
        self.page1_vbox.set_margin_start(5)
        self.page1_vbox.set_margin_end(5)
        self.entry = Gtk.Entry()
        self.entry.set_placeholder_text("Enter category (e.g. 'science')")

        self.page1_vbox.pack_start(self.entry, False, False, 0)

        # self.button = Gtk.Button(label="Choose Image")
        # self.button.connect("clicked", self.on_file_chosen)
        with open("category_prompts.json", "r", encoding="utf-8") as f:
            self.prompt_data = json.load(f)

        self.available_categories = list(self.prompt_data.keys())


        self.buttonRandom = Gtk.Button(label="Pick Random")
        self.buttonRandom.connect("clicked", self.on_rand_button_clicked)
        self.buttonRandom.set_sensitive(False)
        self.page1_vbox.pack_start(self.buttonRandom, False, False, 0)
        self.buttonGen = Gtk.Button(label="Generate")
        tooltip_text = "Available categories:\n" + '\n'.join(self.available_categories)
        self.buttonGen.set_tooltip_text(tooltip_text)
        self.buttonGen.connect("clicked", self.on_gen_button_clicked)
        self.buttonGen.set_sensitive(False)
        self.buttonGen.set_label("Loading generator...")
        self.page1_vbox.pack_start(self.buttonGen, False, False, 0)

        self.buttonCancel = Gtk.Button(label="Cancel")
        self.buttonCancel.connect("clicked", self.on_cancel_button_clicked)
        self.page1_vbox.pack_start(self.buttonCancel, False, False, 0)
        self.buttonCancel.set_sensitive(False)


        self.output_label = Gtk.Label()
        self.page1_vbox.pack_start(self.output_label, False, False, 0)

        """Progress Bar init"""
        self.progressBar = Gtk.ProgressBar()
        self.progressBar.set_show_text(False)
        self.progressBar.set_text("Generating...")
        self.progressBar.set_visible(False)
        self.progressBar.set_margin_top(10)
        self.page1_vbox.pack_start(self.progressBar, False, False, 5)

        self.notebook.append_page(self.page1_vbox, Gtk.Label(label="Generator"))

        # Druga karta – Wyniki
        self.page2_vbox = Gtk.Box(orientation=Gtk.Orientation.VERTICAL, spacing=6)
        self.hbox = Gtk.Box(orientation=Gtk.Orientation.HORIZONTAL, spacing=5)

        self.buttonW = Gtk.Button(label="WALL")
        self.buttonW.connect("clicked", self.on_wall_option)
        self.buttonF = Gtk.Button(label="FLOOR")
        self.buttonF.connect("clicked", self.on_floor_option)
        self.buttonB = Gtk.Button(label="BOOKCASE")
        self.buttonB.connect("clicked", self.on_bookcase_option)

        self.hbox.pack_start(self.buttonW, True, True, 0)
        self.hbox.pack_start(self.buttonF, True, True, 0)
        self.hbox.pack_start(self.buttonB, True, True, 0)

        self.hbox.set_margin_top(10)
        self.hbox.set_margin_bottom(10)
        self.hbox.set_margin_start(10)
        self.hbox.set_margin_end(10)
        self.page2_vbox.pack_start(self.hbox, False, False, 0)

        self.image_result = Gtk.Image()
        self.page2_vbox.pack_start(self.image_result, True, True, 0)

        self.notebook.append_page(self.page2_vbox, Gtk.Label(label="Results"))

        self.connect("destroy", self.on_destroy)

    def on_rand_button_clicked(self, widget):
        if self.available_categories:
            random_category = random.choice(self.available_categories)
            self.entry.set_text(random_category)
        else:
            self.entry.set_text("")

    def on_gen_button_clicked(self, widget):
        self.block_switch = True
        self.progressBar.set_visible(True)
        self.progressBar.set_show_text(True)
        self.progressBar.pulse()
        self.progress_pulse_timeout = GLib.timeout_add(
            GLib.PRIORITY_DEFAULT,
            100,
            self.progress_pulse
        )
        selected_category = self.entry.get_text() # lub jak pobierasz kategorię
        self.buttonGen.set_sensitive(False)
        self.buttonRandom.set_sensitive(False)
        self.output_label.set_text("Generating...")
        self.control_queue.put({"action": "generate", "category": selected_category})
        self.page2_vbox.set_sensitive(False)


    def progress_pulse(self, *args):
        self.progressBar.pulse()
        return True

    def on_switch_page(self, notebook, page, page_num):
        if self.block_switch and page_num == 1:
            print("Przełączanie zablokowane – trwa generacja")
            GLib.idle_add(GLib.PRIORITY_DEFAULT, lambda _: self.notebook.set_current_page(self.current_page))
            return True
        else:
            self.current_page = page_num
            return False

    def on_cancel_button_clicked(self, widget):
        if self.gen_process and self.gen_process.is_alive():
            self.output_label.set_text("Canceling texture generation...")
            print("Próba anulowania generacji...")

            for _ in TEXTURE_TYPES:
                self.control_queue.put({"action": "cancel"})

            # Ustaw timer 15 sekund – po tym sprawdzimy, czy dalej żyje
            GLib.timeout_add_seconds(GLib.PRIORITY_DEFAULT, 15, self.check_if_process_cancelled)

    def check_if_process_cancelled(self):
        if self.gen_process and self.gen_process.is_alive():
            print("Proces nadal żyje po 15 sekundach. Ubijam go.")
            try:
                self.gen_process.terminate()
                self.gen_process.join(timeout=5)  # timeout na wypadek zombie-procesu
            except Exception as e:
                print(f"Błąd przy zabijaniu procesu: {e}")
            finally:
                self.gen_process = None

            self.restart_generation_process()
            self.on_generation_failed("Process did not exit cleanly. Restarted generator.")
        else:
            print("Proces zakończył się poprawnie po anulowaniu.")
        return False  # zatrzymujemy timeout, nie chcemy go zapętlać

    def restart_generation_process(self):
        if self.gen_process and self.gen_process.is_alive():
            print("Zamykam stary proces generowania...")
            for _ in TEXTURE_TYPES:
                self.control_queue.put({"action": "exit"})
            self.gen_process.join()

        print("Restartuję proces generowania...")

        # Resetujemy kolejki (opcjonalne, ale bezpieczne)
        self.gen_queue = Queue()
        self.control_queue = Queue()
        self.buttonGen.set_sensitive(False)
        self.buttonRandom.set_sensitive(False)
        self.buttonCancel.set_sensitive(False)
        # Odpalamy nowy proces
        self.gen_process = Process(
            target=generate_process_wrapper,
            args=(self.gen_queue, self.control_queue, self.gen_mode)
        )
        self.gen_process.start()


    def check_generation_result(self):
        if not self.gen_queue.empty():
            msg = self.gen_queue.get()

            if isinstance(msg, dict) and "status" in msg:
                if msg["status"] == "model_ready":
                    self.buttonGen.set_sensitive(True)
                    self.buttonRandom.set_sensitive(True)
                    self.buttonGen.set_label("Generate")
                    print("Model gotowy do działania.")

            if isinstance(msg, dict) and "error" in msg:
                self.on_generation_failed(msg["error"])
                return True

            if msg == "first_step_done":
                self.buttonCancel.set_sensitive(True)

            if isinstance(msg, dict) and msg.get("status") == "progress":
                step = msg["step"]
                max_step = msg["max_steps"]
                type_texture = msg["type"]
                self.progressBar.set_text(f"Texture {type_texture} generating: {step}/{max_step}")

            elif isinstance(msg, dict) and "image" in msg and "type" in msg and "status" in msg:
                image = Image.open(io.BytesIO(msg["image"]))
                image_type = msg["type"]
                image_status = msg["status"]

                if image_type in TEXTURE_TYPES:
                    convert_to_pixel_art(image, image_type, image_status)
                    self.generated_types.add(image_type)

                if set(TEXTURE_TYPES).issubset(self.generated_types):
                    self.generated_types = set()
                    self.on_generation_finished()

                return True



        return True


    def on_generation_failed(self, e):
        if isinstance(self.progress_pulse_timeout, int) and self.progress_pulse_timeout > 0:
            GLib.source_remove(self.progress_pulse_timeout)
            self.progress_pulse_timeout = 0
        self.buttonCancel.set_sensitive(False)
        self.buttonRandom.set_sensitive(True)
        self.buttonGen.set_sensitive(True)
        self.output_label.set_text(str(e))
        self.progressBar.set_text("Generating...")
        self.progressBar.set_visible(False)
        self.page2_vbox.set_sensitive(True)
        self.block_switch = False
        self.notebook.set_current_page(0)
        return False

    def on_generation_finished(self):
        if isinstance(self.progress_pulse_timeout, int) and self.progress_pulse_timeout > 0:
            GLib.source_remove(self.progress_pulse_timeout)
            self.progress_pulse_timeout = 0
        self.buttonCancel.set_sensitive(False)
        self.buttonRandom.set_sensitive(True)
        self.buttonGen.set_sensitive(True)
        self.output_label.set_text("")
        self.progressBar.set_text("Generating...")
        self.progressBar.set_visible(False)
        self.buttonW.set_sensitive(True)
        self.buttonF.set_sensitive(True)
        self.buttonB.set_sensitive(True)
        self.page2_vbox.set_sensitive(True)
        self.block_switch = False
        self.notebook.set_current_page(1)
        return True

    def on_bookcase_option(self, widget):
        bookcase_path = "assets/bookcase.png"
        if os.path.exists(bookcase_path):
            self.load_image(bookcase_path)
        else:
            print("File bookcase.png not exist.")

    def on_wall_option(self, widget):
        wall_path = "assets/wall.png"
        if os.path.exists(wall_path):
            self.load_image(wall_path)
        else:
            print("File wall.png not exist.")

    def on_floor_option(self, widget):
        floor_path = "assets/floor.png"
        if os.path.exists(floor_path):
            self.load_image(floor_path)
        else:
            print("File floor.png not exist.")

    def load_image(self, image_path):
        """Ładowanie obrazu do aplikacji GTK"""
        try:
            pixbuf = GdkPixbuf.Pixbuf.new_from_file(image_path)
            # Skaluj obraz do większych rozmiarów (np. 100x100)
            scaled_pixbuf = pixbuf.scale_simple(400, 400, GdkPixbuf.InterpType.NEAREST)
            self.image_result.set_from_pixbuf(scaled_pixbuf)
            print("Image loaded successfully!")
            # self.output_label.set_text("Image loaded and scaled successfully!")
        except Exception as e:
            print(f"Error loading image: {e}")
            # self.output_label.set_text(f"Error loading image: {e}")

    def on_destroy(self, widget):
        print("Zamykanie aplikacji, kończę proces generowania...")
        if self.gen_process and self.gen_process.is_alive():
            self.control_queue.put({"action": "exit"})
            self.gen_process.join(timeout=3)  # czekaj max 3s na zamknięcie
            if self.gen_process.is_alive():
                print("Proces nie zakończył się, wymuszam terminate()")
                self.gen_process.terminate()
                self.gen_process.join()
        Gtk.main_quit()

def choose_processing_mode():
    dialog = Gtk.Dialog(title="Select Mode")
    dialog.set_default_size(300, 150)
    content_area = dialog.get_content_area()


    vbox = Gtk.Box(orientation=Gtk.Orientation.VERTICAL, spacing=20)
    vbox.set_border_width(20)
    content_area.add(vbox)

    label = Gtk.Label(label="Do you want to use GPU (CUDA) or just CPU?")
    label.set_justify(Gtk.Justification.CENTER)
    vbox.pack_start(label, True, True, 0)

    dialog.add_buttons(
        "Use CUDA", Gtk.ResponseType.YES,
        "Use CPU", Gtk.ResponseType.NO,
    )

    dialog.show_all()

    response = dialog.run()
    dialog.destroy()

    if response == Gtk.ResponseType.YES:
        return "cuda"
    elif response == Gtk.ResponseType.NO:
        return "cpu"
    else:
        print("Exit clicked - closing program.")
        sys.exit(0)


if __name__ == "__main__":
    # np. z jakiegoś wyboru albo argumentu
    chosen_mode = choose_processing_mode()
    multiprocessing.set_start_method('spawn', force=True)
    app = PixelArtApp(mode=chosen_mode)
    app.connect("destroy", Gtk.main_quit)
    app.show_all()
    Gtk.main()