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


class Hexbin:
    def __init__(self, heatmap):
        self.canvas = tk.Canvas(background='black')
        self.canvas.bind('<B1-Motion>', self.drag)
        self.canvas.bind('<ButtonRelease-1>', self.release)

        self.canvas.bind('<Button-4>', self.scroll)
        self.canvas.bind('<Button-5>', self.scroll)

        self.current_mouse_position = None
        self.photo_img = None
        self.translation = [0, 0]
        self.zoom = 1.0

    def scroll(self, event):
        print(event.num)
        if event.num == 5:
            self.zoom -= 0.1
        elif event.num == 4:
            self.zoom += 0.1

        self.draw()

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

        self.draw()

    def mouse_pos(self, event):
        self.current_mouse_position[0] = event.x
        self.current_mouse_position[1] = event.y

    def draw(self, color='blue'):
        image = Image.new("RGB", (2048, 2048), (255, 255, 255))
        d = ImageDraw.Draw(image)
        d.regular_polygon(
            ((256.0 + self.translation[0]) * self.zoom, (256.0 + self.translation[1]) * self.zoom, HEX_SIZE * 2.0 * self.zoom), 6, fill=color)

        self.photo_img = ImageTk.PhotoImage(
            d._image.resize((1024, 1024)))
        self.canvas.create_image(0, 0, image=self.photo_img, anchor='nw')


def parse_path(path_string):
    tokens = path_string.split(' ')

    tokens_grouped = [tokens[k:k+5] for k in range(0, len(tokens), 5)]

    xs = []
    ys = []

    for group in tokens_grouped:
        group[0] = int(group[0])
        group[1] = int(group[1])
        group[2] = float(group[2])
        group[3] = float(group[3])
        xs.append(group[2])
        ys.append(group[3])
        group[4] = float(group[4])

    tokens_final = list(map(lambda x: tuple(x), tokens_grouped))

    return (tokens_final, xs, ys)


def main():
    with open('paths.txt', 'r') as file:
        for line in file:
            toks, xs_, ys_ = parse_path(line.rstrip())

    root = tk.Tk()
    root.geometry("1024x1024")
    root.resizable(False, False)

    hexbin = Hexbin({})
    hexbin.draw()
    hexbin.canvas.pack(fill='both', expand=True)

    button = tk.Button(command=lambda: hexbin.draw(color='red'))
    button.pack()

    root.mainloop()


if __name__ == "__main__":
    main()
