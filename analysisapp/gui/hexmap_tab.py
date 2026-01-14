"""
Hexmap Visualizer Tab

Tab component for hexagonal heatmap visualization.
"""

import os
import tkinter as tk
from tkinter import ttk, messagebox

from ..analyzers.hexmap.core import Heatmap, Hexbin, parse_path
from ..app_config import config
from .styles import FONTS


class HexmapTab:
    """Hexmap visualizer tab component."""
    
    def __init__(self, parent, status_callback=None):
        """
        Initialize hexmap tab.
        
        Args:
            parent: Parent notebook widget
            status_callback: Callback function to update status bar
        """
        self.parent = parent
        self.status_callback = status_callback
        
        # Data storage
        self.hexmap_data = None
        self.hexmap_widget = None
        self.hexmap_paths = []
        
        # Create tab frame
        self.frame = ttk.Frame(parent)
        parent.add(self.frame, text="Mapa Cieplna")
        
        # Build UI
        self._create_ui()
    
    def _create_ui(self):
        """Create the hexmap tab UI."""
        # Control panel
        control_panel = ttk.Frame(self.frame)
        control_panel.pack(fill=tk.X, padx=5, pady=5)
        
        ttk.Label(control_panel, text="Wizualizacja Mapy Cieplnej", 
                 font=FONTS["HEADER"]).pack(side=tk.LEFT, padx=5)
        
        load_btn = ttk.Button(control_panel, text="Wczytaj paths.txt", 
                             command=self.load_data)
        load_btn.pack(side=tk.RIGHT, padx=5)
        
        # Canvas frame
        self.canvas_frame = ttk.Frame(self.frame)
        self.canvas_frame.pack(fill=tk.BOTH, expand=True, padx=5, pady=5)
        
        # Initial message
        msg = ttk.Label(self.canvas_frame, 
                       text="Kliknij 'Wczytaj paths.txt' aby zwizualizować dane",
                       font=FONTS["NORMAL"])
        msg.pack(expand=True)
    
    def load_data(self):
        """Load and display hexmap data."""
        paths_file = config.get_paths_file()
        
        if not os.path.exists(paths_file):
            messagebox.showerror("Błąd", 
                                f"Nie znaleziono paths.txt w {paths_file}\n\n"
                                "Proszę utworzyć plik paths.txt w katalogu głównym projektu.")
            return
        
        try:
            # Clear existing widgets
            for widget in self.canvas_frame.winfo_children():
                widget.destroy()
            
            # Load data
            heatmap = Heatmap()
            paths = []
            
            with open(paths_file, 'r') as file:
                for line in file:
                    toks, xys_ = parse_path(line.rstrip())
                    heatmap.update_data(toks)
                    paths.append(toks)
            
            if not paths:
                messagebox.showwarning("Ostrzeżenie", "Nie znaleziono ścieżek w paths.txt")
                return
            
            # Create hexmap widget
            hexbin = Hexbin(heatmap, master=self.canvas_frame)
            hexbin.redraw()
            hexbin.set_path(paths[0])
            hexbin.canvas.pack(fill=tk.BOTH, expand=True, side=tk.TOP)
            
            # Button panel
            button_panel = ttk.Frame(self.canvas_frame)
            button_panel.pack(fill=tk.X, pady=5)
            
            # Path selection buttons (max 5)
            for i, path in enumerate(paths[:5]):
                btn = ttk.Button(button_panel, text=f"Ścieżka {i}",
                                command=lambda p=path: hexbin.set_path(p))
                btn.pack(side=tk.LEFT, padx=2)
            
            # Utility buttons
            ttk.Separator(button_panel, orient=tk.VERTICAL).pack(side=tk.LEFT, 
                                                                 fill=tk.Y, padx=10)
            
            ttk.Button(button_panel, text="Zapisz PNG",
                      command=lambda: self._save_hexmap(hexbin)).pack(
                          side=tk.LEFT, padx=2)
            
            ttk.Button(button_panel, text="Przełącz Siatkę",
                      command=lambda: hexbin.toggle_draw_hex()).pack(
                          side=tk.LEFT, padx=2)
            
            # Store references
            self.hexmap_widget = hexbin
            self.hexmap_paths = paths
            self.hexmap_data = heatmap
            
            # Update status
            if self.status_callback:
                self.status_callback(f"Wczytano {len(paths)} ścieżek z {paths_file}")
            
        except Exception as e:
            messagebox.showerror("Błąd", f"Nie udało się wczytać danych mapy:\n{e}")
            import traceback
            traceback.print_exc()
    
    def _save_hexmap(self, hexbin):
        """Save hexmap to file."""
        hexbin.save('hexmap_output.png')
        if self.status_callback:
            self.status_callback("Zapisano mapę do hexmap_output.png")
