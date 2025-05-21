import gi

gi.require_version('Gtk', '3.0')
gi.require_version('GdkPixbuf', '2.0')
from gi.repository import Gtk, Gio, GdkPixbuf
from converter import convert_to_pixel_art
from styler import apply_style


class PixelArtApp(Gtk.Window):
    def __init__(self):
        Gtk.Window.__init__(self, title="Pixel Art Stylizer")
        self.set_border_width(10)
        self.set_default_size(500, 600)

        vbox = Gtk.Box(orientation=Gtk.Orientation.VERTICAL, spacing=10)
        self.add(vbox)

        self.entry = Gtk.Entry()
        self.entry.set_placeholder_text("Enter category (e.g. 'science')")
        vbox.pack_start(self.entry, False, False, 0)

        self.hbox = Gtk.Box(orientation=Gtk.Orientation.HORIZONTAL, spacing=5)

        #self.buttonW = Gtk.Button(label="WALL")
        #self.buttonW.connect("clicked", self.on_wall_option)
        #self.buttonF = Gtk.Button(label="FLOOR")
        #self.buttonF.connect("clicked", self.on_floor_option)
        #self.buttonB = Gtk.Button(label="BOOKCASE")
        #self.buttonB.connect("clicked", self.on_bookcase_option)

        #self.hbox.pack_start(self.buttonW, True, True, 0)
        #self.hbox.pack_start(self.buttonF, True, True, 0)
        #self.hbox.pack_start(self.buttonB, True, True, 0)

        vbox.pack_start(self.hbox, False, False, 0)

        self.button = Gtk.Button(label="Choose Image")
        self.button.connect("clicked", self.on_file_chosen)
        vbox.pack_start(self.button, False, False, 0)

        self.output_label = Gtk.Label()
        vbox.pack_start(self.output_label, False, False, 0)

        # Tworzymy kontener do wyświetlania obrazka
        self.image = Gtk.Image()
        vbox.pack_start(self.image, True, True, 0)

    def load_image(self, image_path):
        """Ładowanie obrazu do aplikacji GTK"""
        try:
            pixbuf = GdkPixbuf.Pixbuf.new_from_file(image_path)
            # Skaluj obraz do większych rozmiarów (np. 100x100)
            scaled_pixbuf = pixbuf.scale_simple(500, 500, GdkPixbuf.InterpType.NEAREST)
            self.image.set_from_pixbuf(scaled_pixbuf)
            self.output_label.set_text("Image loaded and scaled successfully!")
        except Exception as e:
            self.output_label.set_text(f"Error loading image: {e}")

    def on_file_chosen(self, widget):
        dialog = Gtk.FileChooserDialog(
            title="Please choose a file", parent=self,
            action=Gtk.FileChooserAction.OPEN,
            buttons=(Gtk.STOCK_CANCEL, Gtk.ResponseType.CANCEL,
                     Gtk.STOCK_OPEN, Gtk.ResponseType.OK))
        filter_images = Gtk.FileFilter()
        filter_images.set_name("Image files")
        filter_images.add_pattern("*.png")  # Dodaj PNG
        filter_images.add_pattern("*.jpg")  # Dodaj JPG
        filter_images.add_pattern("*.jpeg")  # Dodaj JPEG
        filter_images.add_pattern("*.gif")  # Dodaj GIF

        dialog.add_filter(filter_images)
        response = dialog.run()
        if response == Gtk.ResponseType.OK:
            filepath = dialog.get_filename()
            prompt = self.entry.get_text()
            styled_image = apply_style(filepath, prompt)
            output_path = convert_to_pixel_art(styled_image)
            self.output_label.set_text(f"Saved to {output_path}")
            # Ładowanie obrazu
            self.load_image(output_path)
        dialog.destroy()


win = PixelArtApp()
win.connect("destroy", Gtk.main_quit)
win.show_all()
Gtk.main()
