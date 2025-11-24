import numpy as np
import tkinter as tk
import matplotlib.pyplot as plt
import math
import time

from PIL import Image, ImageDraw, ImageTk

SQRT3 = 1.73205080757
HEX_SIZE = 10.0
HEX_WIDTH = SQRT3 * HEX_SIZE
HEX_HEIGHT = 2.0 * HEX_SIZE

SCALING_FACTOR = 2


def axial_to_pixel(position):
    x, y = position
    x_out = (SQRT3 * x + SQRT3 / 2.0 * y) * HEX_SIZE * SCALING_FACTOR
    y_out = (3.0 / 2.0 * y) * HEX_SIZE * SCALING_FACTOR

    return (x_out, y_out)


def color_lerp(color_a, color_b, t):
    return (
        int(color_b[0] * t + (1.0 - t) * color_a[0]),
        int(color_b[1] * t + (1.0 - t) * color_a[1]),
        int(color_b[2] * t + (1.0 - t) * color_a[2]),
    )


class Heatmap:
    def __init__(self):
        self.data = {}
        self.max = 0

    def update_data(self, data):
        for e in data:
            idx = (e[0], e[1])
            if idx not in self.data:
                self.data[idx] = 0
            self.data[idx] += 1
            if self.data[idx] > self.max:
                self.max = self.data[idx]

    def get_value(self, idx):
        if idx not in self.data:
            return 0.0
        else:
            return self.data[idx] / self.max


class Hexbin:
    def __init__(self, heatmap, master=None):
        self.canvas = tk.Canvas(background='black', master=master)
        self.canvas.bind('<B1-Motion>', self.drag)
        self.canvas.bind('<ButtonRelease-1>', self.release)

        self.canvas.bind('<Button-4>', self.scroll)
        self.canvas.bind('<Button-5>', self.scroll)

        self.canvas.bind('<Configure>', self.set_canvas_size)

        self.current_mouse_position = None
        self.image = None
        self.photo_img = None

        self.path = None

        self.translation = [0, 0]
        self.zoom = 1.0

        self.heatmap = heatmap

    def save(self, path):
        self.image.save(path)

    def scroll(self, event):
        if event.num == 5:
            self.zoom -= 0.1
        elif event.num == 4:
            self.zoom += 0.1

        self.redraw()

    def set_canvas_size(self, event):
        # self.canvas.configure(width=event.width, height=event.height)
        print(event)
        self.redraw()

    def release(self, event):
        self.current_mouse_position = None

    def drag(self, event):
        if not self.current_mouse_position:
            self.current_mouse_position = [event.x, event.y]

        self.translation[0] -= 2 * \
            (self.current_mouse_position[0] - event.x) / self.zoom
        self.translation[1] -= 2 * \
            (self.current_mouse_position[1] - event.y) / self.zoom
        self.current_mouse_position[0] = event.x
        self.current_mouse_position[1] = event.y

        self.redraw()

    def mouse_pos(self, event):
        self.current_mouse_position[0] = event.x
        self.current_mouse_position[1] = event.y

    def set_path(self, path):
        self.path = path
        self.redraw()

    def redraw(self):
        image = Image.new("RGB", (
            self.canvas.winfo_reqwidth() * SCALING_FACTOR,
            self.canvas.winfo_reqheight() * SCALING_FACTOR
        ), (0, 0, 0))
        d = ImageDraw.Draw(image)

        for i in range(-20, 20):
            for j in range(-20, 20):
                x, y = axial_to_pixel((i, j))
                d.regular_polygon(
                    (
                        (x + self.translation[0]) * self.zoom,
                        (y + self.translation[1]) * self.zoom,
                        HEX_SIZE * SCALING_FACTOR * self.zoom
                    ),
                    6,
                    fill=color_lerp((0, 0, 0), (255, 255, 255),
                                    self.heatmap.get_value((i, j))),
                    rotation=30
                )

        if self.path:
            path = []
            for i, point in enumerate(self.path):
                path.append(
                    (point * HEX_SIZE * SCALING_FACTOR + self.translation[i % 2]) * self.zoom)
            d.line(path, fill='red')

        self.image = d._image
        self.photo_img = ImageTk.PhotoImage(
            d._image.resize((self.canvas.winfo_reqwidth(), self.canvas.winfo_reqheight())))
        self.canvas.create_image(0, 0, image=self.photo_img, anchor='nw')


def parse_path(path_string):
    tokens = path_string.split(' ')

    tokens_grouped = [tokens[k:k+5] for k in range(0, len(tokens), 5)]

    xys = []

    for group in tokens_grouped:
        group[0] = int(group[0])
        group[1] = int(group[1])
        group[2] = float(group[2])
        group[3] = float(group[3])
        xys.extend([group[2], group[3]])
        group[4] = float(group[4])

    tokens_final = list(map(lambda x: tuple(x), tokens_grouped))

    return (tokens_final, xys)


def main():
    heatmap = Heatmap()

    paths = []
    with open('paths.txt', 'r') as file:
        for line in file:
            toks, xys_ = parse_path(line.rstrip())
            heatmap.update_data(toks)
            paths.append((toks, xys_))

    print(heatmap.get_value((-3, 12)))

    root = tk.Tk()
    root.geometry("512x512")
    root.resizable(False, False)

    hexbin = Hexbin(heatmap)
    hexbin.redraw()
    hexbin.set_path(paths[0][1])
    hexbin.canvas.pack(fill='both', expand=True)
    hexbin.canvas.configure(width=512, height=512-64)

    button1 = tk.Button(
        text="Path 0", command=lambda: hexbin.set_path(paths[0][1]))
    button1.pack(side='left')
    button2 = tk.Button(
        text="Path 1", command=lambda: hexbin.set_path(paths[1][1]))
    button2.pack(side='left')
    button3 = tk.Button(
        text="Path 2", command=lambda: hexbin.set_path(paths[2][1]))
    button3.pack(side='left')

    button4 = tk.Button(
        text="Save to file", command=lambda: hexbin.save('out.png'))
    button4.pack(side='left')

    root.mainloop()


if __name__ == "__main__":
    main()
