import threading
import time

import gi
from gi._option import GLib

gi.require_version('Gtk', '3.0')
gi.require_version('GdkPixbuf', '2.0')
from gi.repository import Gtk, Gio, GdkPixbuf
from converter import convert_to_pixel_art
from styler import apply_style


class PixelArtApp(Gtk.Window):
    def __init__(self):
        Gtk.Window.__init__(self, title="Pixel Art Stylizer")
        self.progress_pulse_timeout = None
        self.set_border_width(10)
        self.set_default_size(500, 600)

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
        self.button = Gtk.Button(label="Generate")
        self.button.connect("clicked", self.on_gen_button_clicked)
        self.page1_vbox.pack_start(self.button, False, False, 0)

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

        self.notebook.append_page(self.page2_vbox, Gtk.Label(label="Wyniki"))

    def on_gen_button_clicked(self, widget):
        self.output_label.set_text(f"Generated texture in category {self.entry.get_text()} style")
        self.block_switch = True
        self.progressBar.set_visible(True)
        self.progressBar.set_show_text(True)
        self.progressBar.pulse()
        self.progress_pulse_timeout = GLib.timeout_add(
            GLib.PRIORITY_DEFAULT,
            100,
            self.progress_pulse
        )
        # Opcjonalnie, jeśli generacja trwa długo, użyj GLib.idle_add lub Thread:
        # GLib.idle_add(self.generate_texture)
        self.page2_vbox.set_sensitive(False)
        threading.Thread(target=self.generate_texture).start()

    def progress_pulse(self, *args):
        self.progressBar.pulse()
        return True

    def on_switch_page(self, notebook, page, page_num):
        if self.block_switch and page_num == 1:
            print("Przełączanie zablokowane – trwa generacja")
            GLib.idle_add(GLib.PRIORITY_DEFAULT, lambda user_data: self.notebook.set_current_page(self.current_page),
                          None)
            return True
        else:
            self.current_page = page_num
            return False

    def generate_texture(self):
        time.sleep(5)
        # aktualizacja GUI po zakończeniu
        GLib.idle_add(GLib.PRIORITY_DEFAULT, self.on_generation_finished)

    def on_generation_finished(self):
        GLib.source_remove(self.progress_pulse_timeout)
        self.progressBar.set_visible(False)
        self.block_switch = False
        self.notebook.set_current_page(1) # przełącz na kartę „Wynik”

        # załaduj przykładowy obrazek (lub ustaw etykietę)
        # self.image.set_from_icon_name("image-x-generic", Gtk.IconSize.DIALOG)
        return False  # zakończ idle_add

    def on_bookcase_option(self, widget):
        pass

    def on_wall_option(self, widget):
        pass

    def on_floor_option(self, widget):
        pass

    def load_image(self, image_path):
        """Ładowanie obrazu do aplikacji GTK"""
        try:
            pixbuf = GdkPixbuf.Pixbuf.new_from_file(image_path)
            # Skaluj obraz do większych rozmiarów (np. 100x100)
            scaled_pixbuf = pixbuf.scale_simple(400, 400, GdkPixbuf.InterpType.NEAREST)
            self.image.set_from_pixbuf(scaled_pixbuf)
            self.output_label.set_text("Image loaded and scaled successfully!")
        except Exception as e:
            self.output_label.set_text(f"Error loading image: {e}")

        # def on_file_chosen(self, widget):
    #     dialog = Gtk.FileChooserDialog(
    #         title="Please choose a file", parent=self,
    #         action=Gtk.FileChooserAction.OPEN,
    #         buttons=(Gtk.STOCK_CANCEL, Gtk.ResponseType.CANCEL,
    #                  Gtk.STOCK_OPEN, Gtk.ResponseType.OK))
    #     filter_images = Gtk.FileFilter()
    #     filter_images.set_name("Image files")
    #     filter_images.add_pattern("*.png")  # Dodaj PNG
    #     filter_images.add_pattern("*.jpg")  # Dodaj JPG
    #     filter_images.add_pattern("*.jpeg")  # Dodaj JPEG
    #     filter_images.add_pattern("*.gif")  # Dodaj GIF
    #
    #     dialog.add_filter(filter_images)
    #     response = dialog.run()
    #     if response == Gtk.ResponseType.OK:
    #         filepath = dialog.get_filename()
    #         prompt = self.entry.get_text()
    #         styled_image = apply_style(filepath, prompt)
    #         output_path = convert_to_pixel_art(styled_image)
    #         self.output_label.set_text(f"Saved to {output_path}")
    #         # Ładowanie obrazu
    #         # self.load_image(output_path)
    #     dialog.destroy()


win = PixelArtApp()
win.connect("destroy", Gtk.main_quit)
win.show_all()
Gtk.main()
