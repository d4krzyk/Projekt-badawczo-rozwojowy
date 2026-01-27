"""
Hexmap Analyzer

Wrapper for hexagonal heatmap visualization functionality.
"""

import os
import tkinter as tk

from .hexmap.core import Heatmap, Hexbin, parse_path
from .base_analyzer import BaseAnalyzer
from ..app_config import config


class HexmapAnalyzer(BaseAnalyzer):
    """Hexagonal heatmap visualization analyzer."""
    
    @property
    def name(self) -> str:
        return "Hexmap Visualizer"
    
    @property
    def description(self) -> str:
        return "Visualize path data using hexagonal heatmaps"
    
    def run(self):
        """Launch the hexmap visualizer."""
        paths_file = config.get_paths_file()
        
        # Check if paths file exists
        if not os.path.exists(paths_file):
            print(f"Error: paths.txt not found at {paths_file}")
            print("Please create a paths.txt file in the project root directory.")
            return
        
        # Load data
        heatmap = Heatmap()
        paths = []
        
        try:
            with open(paths_file, 'r') as file:
                for line in file:
                    toks, xys_ = parse_path(line.rstrip())
                    heatmap.update_data(toks)
                    paths.append(toks)
        except Exception as e:
            print(f"Error loading paths file: {e}")
            return
        
        if not paths:
            print("No paths found in paths.txt")
            return
        
        # Create UI
        root = tk.Tk()
        root.title("Hexmap Visualizer")
        root.geometry(f"{config.hexmap_window_size}x{config.hexmap_window_size}")
        root.resizable(False, False)
        
        # Create hexmap widget
        hexbin = Hexbin(heatmap)
        hexbin.redraw()
        hexbin.set_path(paths[0])
        hexbin.canvas.pack(fill='both', expand=True)
        hexbin.canvas.configure(
            width=config.hexmap_window_size,
            height=config.hexmap_window_size - 64
        )
        
        # Create buttons for path selection
        for i, path in enumerate(paths[:5]):  # Limit to first 5 paths for UI space
            button = tk.Button(
                text=f"Path {i}",
                command=lambda p=path: hexbin.set_path(p)
            )
            button.pack(side='left')
        
        # Utility buttons
        button_save = tk.Button(
            text="Save to file",
            command=lambda: hexbin.save('out.png')
        )
        button_save.pack(side='left')
        
        button_toggle = tk.Button(
            text="Toggle hex grid",
            command=lambda: hexbin.toggle_draw_hex()
        )
        button_toggle.pack(side='left')
        
        # Start application
        print(f"Loaded {len(paths)} paths from {paths_file}")
        root.mainloop()

