"""
Main Application Window

Main window with tabbed interface integrating all analyzer tabs.
"""

import tkinter as tk
from tkinter import ttk, messagebox

from .hexmap_tab import HexmapTab
from .session_tab import SessionTab



class AnalysisApp(tk.Tk):
    """Main application window with tabbed interface for all analyzers."""
    
    def __init__(self):
        super().__init__()
        
        self.title("Aplikacja Analityczna")
        self.geometry("1400x900")
        
        # Create menu bar
        self._create_menu()
        
        # Create notebook (tabbed interface)
        self.notebook = ttk.Notebook(self)
        self.notebook.pack(fill=tk.BOTH, expand=True, padx=5, pady=5)
        
        # Create status bar
        self.status_bar = ttk.Label(self, text="Gotowy", relief=tk.SUNKEN, anchor=tk.W)
        self.status_bar.pack(side=tk.BOTTOM, fill=tk.X)
        
        # Create tabs
        self._create_tabs()
    
    def _create_menu(self):
        """Create the application menu bar."""
        menubar = tk.Menu(self)
        self.config(menu=menubar)
        
        # File menu
        file_menu = tk.Menu(menubar, tearoff=0)
        menubar.add_cascade(label="Plik", menu=file_menu)
        file_menu.add_command(label="Wyjście", command=self.quit)
        
        # Help menu
        help_menu = tk.Menu(menubar, tearoff=0)
        menubar.add_cascade(label="Pomoc", menu=help_menu)
        help_menu.add_command(label="O programie", command=self._show_about)
    
    def _create_tabs(self):
        """Create all analyzer tabs."""
        # Create hexmap tab
        self.hexmap_tab = HexmapTab(self.notebook, status_callback=self._update_status)
        
        # Create session tab
        self.session_tab = SessionTab(self.notebook, status_callback=self._update_status)
    
    def _update_status(self, message):
        """Update status bar with message."""
        self.status_bar.config(text=message)
    
    def _show_about(self):
        """Show about dialog."""
        messagebox.showinfo("O programie", 
                          "Aplikacja Analityczna v1.0\n\n"
                          "Modułowe narzędzia analizy danych:\n"
                          "- Wizualizacja Mapy Cieplnej\n"
                          "- Analizator Sesji")


def run_gui():
    """Run the main GUI application."""
    app = AnalysisApp()
    app.mainloop()
