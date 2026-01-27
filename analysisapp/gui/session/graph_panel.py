"""
Graph Panel for Session Analyzer.

Handles the Matplotlib event graph visualization.
"""

import tkinter as tk
from tkinter import ttk
import matplotlib
matplotlib.use('TkAgg')
from matplotlib.figure import Figure
from matplotlib.backends.backend_tkagg import FigureCanvasTkAgg
from ...analyzers.session.core import parse_time, extract_link_label
from ..styles import FONTS, COLORS

class GraphPanel:
    """Manages the event graph visualization."""
    
    def __init__(self, parent, on_event_click_callback=None):
        """
        Initialize Graph Panel.
        
        Args:
            parent: Parent widget
            on_event_click_callback: Function to call when an event point is clicked
        """
        self.parent = parent
        self.on_event_click = on_event_click_callback
        self.fig = None
        self.ax = None
        self.canvas_fig = None
        self.session_plot_points_meta = []
        
        self._setup_ui()

    def _setup_ui(self):
        """Setup the graph UI components."""
        ttk.Label(self.parent, text="Wykres Zdarzeń", 
                 font=FONTS["HEADER"]).pack(anchor=tk.W, padx=6, pady=(6,0))
        
        self.fig = Figure(figsize=(4.5, 6), tight_layout=True)
        self.ax = self.fig.add_subplot(111)
        self.canvas_fig = FigureCanvasTkAgg(self.fig, master=self.parent)
        self.canvas_fig.get_tk_widget().pack(fill=tk.BOTH, expand=True, padx=6, pady=6)
        
        # Connect pick event
        self.fig.canvas.mpl_connect('pick_event', self._on_plot_pick)

    def update_graph(self, room_meta):
        """
        Update graph for selected room.
        
        Args:
            room_meta: Metadata of selected room (must contain session_idx)
        """
        self.ax.clear()
        self.session_plot_points_meta.clear()
        
        if not room_meta:
            return
            
        events = self._collect_events(room_meta)
        
        if not events:
            self.ax.text(0.5, 0.5, 'Brak zdarzeń do wyświetlenia', ha='center', va='center')
            self.canvas_fig.draw()
            return

        self._plot_events(events)
        self.canvas_fig.draw()

    def _collect_events(self, target_meta):
        """Collect all events from the target room metadata."""
        events = []
        s_idx = target_meta.get("session_idx", 0)
        
        # Books
        for be in target_meta.get("book_session_events", []):
            try:
                t = parse_time(be.get("open_time"))
                label = be.get("book", {}).get("name", "(książka)")
                events.append({
                    "time": t, "label": label, "session_idx": s_idx, 
                    "type": "książka", "meta": be
                })
            except: continue
            
        # Links
        for le in target_meta.get("book_link_events", []):
            try:
                t = parse_time(le.get("click_time"))
                label = extract_link_label(le.get("link", "(link)"))
                events.append({
                    "time": t, "label": label, "session_idx": s_idx, 
                    "type": "link", "meta": le
                })
            except: continue
            
        return events

    def _plot_events(self, events):
        """Plot the collected events."""
        if not events: return
        
        # Sort by time
        events.sort(key=lambda x: x["time"])
        base_time = events[0]["time"]
        
        # Separate types
        book_x, book_y, book_meta = [], [], []
        link_x, link_y, link_meta = [], [], []
        
        for e in events:
            delta = (e["time"] - base_time).total_seconds() / 60.0
            meta_entry = {'meta': e} # Wrap to store artist later
            
            if e["type"] == "książka":
                book_x.append(e["session_idx"])
                book_y.append(delta)
                book_meta.append(meta_entry)
            else:
                link_x.append(e["session_idx"])
                link_y.append(delta)
                link_meta.append(meta_entry)
        
        # Plot books
        if book_x:
            sc = self.ax.scatter(book_x, book_y, c='blue', marker='o', 
                               label='Zdarzenia Książki', picker=5)
            # Matplotlib scatter returns a PathCollection. 
            # We can't easily map individual points to metadata with a single scatter call 
            # if we want precise picking of individual points in a simple way without complex index mapping.
            # However, for simplicity in this refactor, let's store the collection and the list of metas.
            # A better approach for individual picking is plotting one by one or using index mapping.
            # Given the previous code did: self.session_plot_points_meta.append({'artist': m, 'meta': e})
            # It seems it was plotting individually or handling it differently.
            # Let's revert to plotting individually to maintain the exact behavior of the original code
            # which allows easy artist-to-meta mapping.
            pass

        # Re-implementing _plot_events to match original behavior for picking
        self.ax.clear()
        
        # Sort by time
        events.sort(key=lambda x: x["time"])
        if not events: return
        t0 = events[0]["time"]
        
        xs = [e["session_idx"] for e in events]
        ys = [(e["time"] - t0).total_seconds() / 60.0 for e in events]
        
        color_palette = COLORS["ROOM_PALETTE"]
        
        for i, e in enumerate(events):
            color = color_palette[e["session_idx"] % len(color_palette)]
            # Use different markers for types
            marker = 'o' if e["type"] == "książka" else 'x'
            c = 'blue' if e["type"] == "książka" else 'red'
            
            m = self.ax.scatter(xs[i], ys[i], s=50, c=c, marker=marker, picker=5, zorder=3)
            self.session_plot_points_meta.append({'artist': m, 'meta': e})
            
        self.ax.set_xlabel("Indeks Sesji")
        self.ax.set_ylabel("Czas (min)")
        self.ax.set_title(f"Zdarzenia w {events[0]['label']}")
        self.ax.grid(True)

    def _on_plot_pick(self, event):
        """Handle click on plot point."""
        if not self.on_event_click:
            return
            
        picked_artist = event.artist
        for entry in self.session_plot_points_meta:
            if entry['artist'] == picked_artist:
                self.on_event_click(entry['meta'])
                return
