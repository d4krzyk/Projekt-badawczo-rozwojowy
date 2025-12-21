"""
Session Analyzer Tab

Tab component for session timeline visualization and analysis.
Refactored to use modular components.
"""

import os
import tkinter as tk
from tkinter import ttk, filedialog, messagebox
import matplotlib
matplotlib.use('TkAgg')
from matplotlib.figure import Figure
from matplotlib.backends.backend_tkagg import FigureCanvasTkAgg

from ..analyzers.session.core import load_json, parse_time, extract_link_label
from ..app_config import config
from .styles import FONTS
from .session.data_manager import DataManager
from .session.timeline_panel import TimelinePanel
from .session.graph_panel import GraphPanel
from .session.details_panel import DetailsPanel

class SessionTab:
    """
    Session analyzer tab component.
    
    Orchestrates the interaction between DataManager, TimelinePanel,
    GraphPanel, and DetailsPanel.
    """
    
    def __init__(self, parent, status_callback=None):
        """
        Initialize session tab.
        
        Args:
            parent: Parent notebook widget
            status_callback: Callback function to update status bar
        """
        self.parent = parent
        self.status_callback = status_callback
        
        # Components
        self.data_manager = DataManager()
        self.timeline_panel = None
        self.graph_panel = None
        self.details_panel = None
        
        # Create tab frame
        self.frame = ttk.Frame(parent)
        parent.add(self.frame, text="Analizator Sesji")
        
        # Build UI
        self._create_ui()
    
    def _create_ui(self):
        """Create the session tab UI."""
        # Control panel
        control_panel = ttk.Frame(self.frame)
        control_panel.pack(fill=tk.X, padx=5, pady=5)
        
        ttk.Label(control_panel, text="Analiza Sesji", 
                 font=FONTS["HEADER"]).pack(side=tk.LEFT, padx=5)
        
        load_btn = ttk.Button(control_panel, text="Wczytaj JSON", 
                             command=self.load_data)
        load_btn.pack(side=tk.RIGHT, padx=5)
        
        api_btn = ttk.Button(control_panel, text="Pobierz z API", 
                            command=self.load_from_api)
        api_btn.pack(side=tk.RIGHT, padx=5)
        
        # Container for session visualizer
        self.content_frame = ttk.Frame(self.frame)
        self.content_frame.pack(fill=tk.BOTH, expand=True, padx=5, pady=5)
        
        # Initial message
        self.msg_label = ttk.Label(self.content_frame, 
                       text="Kliknij 'Pobierz z API' lub 'Wczytaj JSON' aby rozpocząć analizę",
                       font=FONTS["NORMAL"])
        self.msg_label.pack(expand=True)
    
    def load_from_api(self):
        """Load session data from API."""
        try:
            self.data_manager.load_from_api()
            self._initialize_visualizer()
            
            # Update status
            if self.status_callback:
                self.status_callback("Pobrano dane sesji z API")
            
        except Exception as e:
            messagebox.showerror("Błąd", f"Nie udało się pobrać danych z API:\\n{e}")
            import traceback
            traceback.print_exc()
    
    def load_data(self):
        """Load and display session data."""
        initial_dir = config.get_default_json_dir()
        path = filedialog.askopenfilename(
            title="Wybierz plik JSON sesji",
            initialdir=initial_dir,
            filetypes=[("Pliki JSON", "*.json"), ("Wszystkie pliki", "*.*")]
        )
        
        if not path:
            return
        
        try:
            self.data_manager.load_from_file(path)
            self._initialize_visualizer()
            
            # Update status
            if self.status_callback:
                self.status_callback(f"Wczytano dane sesji z {os.path.basename(path)}")
            
        except Exception as e:
            messagebox.showerror("Błąd", f"Nie udało się wczytać danych sesji:\n{e}")
            import traceback
            traceback.print_exc()
    
    def _initialize_visualizer(self):
        """Initialize visualizer components if not already created."""
        # Clear initial message if present
        if self.msg_label.winfo_exists():
            self.msg_label.destroy()
            
        # Clear existing content
        for widget in self.content_frame.winfo_children():
            widget.destroy()

        # Create paned window
        paned = ttk.PanedWindow(self.content_frame, orient=tk.HORIZONTAL)
        paned.pack(fill=tk.BOTH, expand=True)
        
        # Create frames for panels
        left_frame = ttk.Frame(paned)
        middle_frame = ttk.Frame(paned, width=380)
        right_frame = ttk.Frame(paned, width=480)
        
        paned.add(left_frame, weight=3)
        paned.add(middle_frame, weight=1)
        paned.add(right_frame, weight=1)
        
        # Instantiate panels
        self.timeline_panel = TimelinePanel(left_frame, self._on_room_selected)
        self.graph_panel = GraphPanel(middle_frame, self._on_plot_point_selected)
        self.details_panel = DetailsPanel(right_frame)
        
        # Initial update
        self.timeline_panel.update_data(self.data_manager)

    def _on_room_selected(self, room_meta):
        """
        Handle room selection from timeline.
        
        Args:
            room_meta: Metadata of the selected room
        """
        if not room_meta:
            return
            
        self.details_panel.update_details(room_meta)
        self.graph_panel.update_graph(room_meta)
    
    def _on_plot_point_selected(self, event_meta):
        """
        Handle plot point selection from graph.
        
        Args:
            event_meta: Metadata of the selected event
        """
        if not event_meta:
            return
        self.details_panel.update_details_from_event(event_meta)
