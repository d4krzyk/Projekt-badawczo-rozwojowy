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


class Hexbin:
    def __init__(self, heatmap, master=None):
        self.canvas = tk.Canvas(background='black', master=master)
        self.canvas.bind('<B1-Motion>', self.drag)
        self.canvas.bind('<ButtonRelease-1>', self.release)

        self.canvas.bind('<Button-4>', self.scroll)
        self.canvas.bind('<Button-5>', self.scroll)

        self.canvas.bind('<Configure>', self.set_canvas_size)

        self.current_mouse_position = None
        self.photo_img = None

        self.path = None

        self.translation = [0, 0]
        self.zoom = 1.0

    def scroll(self, event):
        if event.num == 5:
            self.zoom -= 0.1
        elif event.num == 4:
            self.zoom += 0.1

        self.redraw()

    def set_canvas_size(self, event):
        self.canvas.configure(width=event.width, height=event.height)
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
        ), (255, 255, 255))
        d = ImageDraw.Draw(image)
        d.regular_polygon(
            ((-256.0 + self.translation[0]) * self.zoom, (-256.0 + self.translation[1]) * self.zoom, HEX_SIZE * 2.0 * self.zoom), 6, fill='blue')

        if self.path:
            path = []
            for i, point in enumerate(self.path):
                path.append(
                    (point * HEX_SIZE + self.translation[i % 2]) * self.zoom)
            d.line(path, fill='red')

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

    root = tk.Tk()
    root.geometry("512x512")
    root.resizable(False, False)

    hexbin = Hexbin({})
    hexbin.redraw()
    hexbin.canvas.pack(fill='both', expand=True)

    with open('paths.txt', 'r') as file:
        for line in file:
            toks, xys_ = parse_path(line.rstrip())
            hexbin.set_path(xys_)

    root.mainloop()


if __name__ == "__main__":
    main()
