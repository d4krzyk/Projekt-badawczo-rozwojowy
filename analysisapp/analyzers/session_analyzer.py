"""
Session Analyzer

Wrapper for session timeline visualization functionality.
"""

import tkinter as tk
from tkinter import filedialog

from .session.core import SessionVisualizer, load_json
from .base_analyzer import BaseAnalyzer
from ..app_config import config


class SessionAnalyzer(BaseAnalyzer):
    """Session timeline visualization analyzer."""
    
    @property
    def name(self) -> str:
        return "Session Analyzer"
    
    @property
    def description(self) -> str:
        return "Analyze and visualize session timeline data from JSON files"
    
    def run(self):
        """Launch the session analyzer."""
        # Open file dialog to select JSON file
        root = tk.Tk()
        root.withdraw()
        
        initial_dir = config.get_default_json_dir()
        path = filedialog.askopenfilename(
            title="Select Session JSON file",
            initialdir=initial_dir,
            filetypes=[("JSON files", "*.json"), ("All files", "*.*")]
        )
        
        root.destroy()
        
        if not path:
            print("No file selected. Exiting Session Analyzer.")
            return
        
        # Load and visualize data
        try:
            data = load_json(path)
            app = SessionVisualizer(data)
            app.mainloop()
        except Exception as e:
            print(f"Error loading or visualizing session data: {e}")
            import traceback
            traceback.print_exc()
