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
        self.draw_hex = False

        self.path = None

        self.translation = [0, 0]
        self.zoom = 1.0

        self.event = None
        self.event_update = False
        self.event_cached_position = None
        self.heatmap = heatmap

    # Event is (time, message)
    def set_event(self, event):
        self.event = event
        self.event_update = True
        self.redraw()

    def save(self, path):
        self.image.save(path)

    def scroll(self, event):
        if event.num == 5:
            self.zoom = math.exp(math.log(self.zoom) - 0.1)
        elif event.num == 4:
            self.zoom = math.exp(math.log(self.zoom) + 0.1)

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
        self.event = None
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
            if not self.draw_hex:
                for point in self.path:
                    path.extend([
                        (point[2] * HEX_SIZE * SCALING_FACTOR +
                         self.translation[0]) * self.zoom,
                        (point[3] * HEX_SIZE * SCALING_FACTOR +
                         self.translation[1]) * self.zoom
                    ])

            else:
                for point in self.path:
                    x, y = axial_to_pixel((point[0], point[1]))
                    path.extend([
                        (x + self.translation[0]) * self.zoom,
                        (y + self.translation[1]) * self.zoom
                    ])

            d.line(path, fill='red')

            if self.event:
                if self.event_update:
                    self.event_update = False
                    self.event_cached_position = [
                        (self.path[0][2] * HEX_SIZE * SCALING_FACTOR +
                         self.translation[0]) * self.zoom,
                        (self.path[0][3] * HEX_SIZE * SCALING_FACTOR +
                         self.translation[1]) * self.zoom,
                    ]
                    for i, point in enumerate(self.path):
                        if point[4] > self.event[0]:
                            if i - 1 >= 0:
                                previous = self.path[i - 1]
                                t = (self.event[0] - previous[4]
                                     ) / (point[4] - previous[4])

                                x, y = (
                                    (1.0 - t) * previous[2] + t * point[2],
                                    (1.0 - t) * previous[3] + t * point[3]
                                )

                                self.event_cached_position = [
                                    (x * HEX_SIZE * SCALING_FACTOR),
                                    (y * HEX_SIZE * SCALING_FACTOR),
                                ]
                                break
                            else:
                                break
                event_pos = ((self.event_cached_position[0] + self.translation[0]) * self.zoom,
                             (self.event_cached_position[1] + self.translation[1]) * self.zoom)
                d.circle(event_pos, 4.0 * SCALING_FACTOR, fill='blue')
                d.text(
                    (event_pos[0] + 10, event_pos[1]), self.event[1], fill='yellow', font_size=20 * SCALING_FACTOR,  anchor='lm')

        self.image = d._image
        self.photo_img = ImageTk.PhotoImage(
            d._image.resize((self.canvas.winfo_reqwidth(), self.canvas.winfo_reqheight())))
        self.canvas.create_image(0, 0, image=self.photo_img, anchor='nw')

    def set_draw_hex(self, draw_hex):
        self.draw_hex = draw_hex

    def toggle_draw_hex(self):
        self.draw_hex = not self.draw_hex
        self.redraw()


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
            paths.append(toks)

    print(heatmap.get_value((-3, 12)))

    root = tk.Tk()
    root.geometry("512x512")
    root.resizable(False, False)

    hexbin = Hexbin(heatmap)
    hexbin.redraw()
    hexbin.set_path(paths[0])
    hexbin.canvas.pack(fill='both', expand=True)
    hexbin.canvas.configure(width=512, height=512-64)

    button1 = tk.Button(
        text="Path 0", command=lambda: hexbin.set_path(paths[0]))
    button1.pack(side='left')
    button2 = tk.Button(
        text="Path 1", command=lambda: hexbin.set_path(paths[1]))
    button2.pack(side='left')
    button3 = tk.Button(
        text="Path 2", command=lambda: hexbin.set_path(paths[2]))
    button3.pack(side='left')

    button4 = tk.Button(
        text="Save to file", command=lambda: hexbin.save('out.png'))
    button4.pack(side='left')

    button5 = tk.Button(
        text="Toggle draw hex", command=lambda: hexbin.toggle_draw_hex())
    button5.pack(side='left')

    hexbin.set_event((20.4, 'Test Event'))
    button6 = tk.Button(
        text="Clear event", command=lambda: hexbin.set_event(None))
    button6.pack(side='left')

    root.mainloop()


if __name__ == "__main__":
    main()
