"""
Session Analyzer Tab

Tab component for session timeline visualization and analysis.
"""

import os
import json
import tkinter as tk
from tkinter import ttk, filedialog, messagebox
import matplotlib
matplotlib.use('TkAgg')
from matplotlib.figure import Figure
from matplotlib.backends.backend_tkagg import FigureCanvasTkAgg

from ..analyzers.session.core import load_json, parse_time, extract_link_label
from ..app_config import config



class SessionTab:
    """Session analyzer tab component."""
    
    def __init__(self, parent, status_callback=None):
        """
        Initialize session tab.
        
        Args:
            parent: Parent notebook widget
            status_callback: Callback function to update status bar
        """
        self.parent = parent
        self.status_callback = status_callback
        
        # Data storage
        self.session_data = None
        self.selected_room_meta = None
        self.session_plot_points_meta = []
        
        # Create tab frame
        self.frame = ttk.Frame(parent)
        parent.add(self.frame, text="Session Analyzer")
        
        # Build UI
        self._create_ui()
    
    def _create_ui(self):
        """Create the session tab UI."""
        # Control panel
        control_panel = ttk.Frame(self.frame)
        control_panel.pack(fill=tk.X, padx=5, pady=5)
        
        ttk.Label(control_panel, text="Session Analysis", 
                 font=("Segoe UI", 12, "bold")).pack(side=tk.LEFT, padx=5)
        
        load_btn = ttk.Button(control_panel, text="Load JSON", 
                             command=self.load_data)
        load_btn.pack(side=tk.RIGHT, padx=5)
        
        # Container for session visualizer
        self.content_frame = ttk.Frame(self.frame)
        self.content_frame.pack(fill=tk.BOTH, expand=True, padx=5, pady=5)
        
        # Initial message
        msg = ttk.Label(self.content_frame, 
                       text="Click 'Load JSON' to analyze session data",
                       font=("Segoe UI", 10))
        msg.pack(expand=True)
    
    def load_data(self):
        """Load and display session data."""
        initial_dir = config.get_default_json_dir()
        path = filedialog.askopenfilename(
            title="Select Session JSON file",
            initialdir=initial_dir,
            filetypes=[("JSON files", "*.json"), ("All files", "*.*")]
        )
        
        if not path:
            return
        
        try:
            data = load_json(path)
            
            for session in data.get("sessions", []):
                for room in session.get("rooms", []):
                    if "books" in room and "book_session_events" not in room:
                        book_session_events = []
                        for book in room.get("books", []):
                            book_name = book.get("name")
                            for event in book.get("session_events", []):
                                book_session_events.append({
                                    "book": {"name": book_name},
                                    "open_time": event.get("open_time"),
                                    "close_time": event.get("close_time")
                                })
                        room["book_session_events"] = book_session_events
                    
                    if "book_links" in room and "book_link_events" not in room:
                        book_link_events = []
                        for link_event in room.get("book_links", []):
                            book_link_events.append({
                                "link": link_event.get("link"),
                                "click_time": link_event.get("click_time")
                            })
                        room["book_link_events"] = book_link_events
            
            for widget in self.content_frame.winfo_children():
                widget.destroy()
            
            # Store data
            self.session_data = data
            self.selected_room_meta = None
            
            # Create visualizer UI
            self._create_visualizer_ui()
            
            # Update status
            if self.status_callback:
                self.status_callback(f"Loaded session data from {os.path.basename(path)}")
            
        except Exception as e:
            messagebox.showerror("Error", f"Failed to load session data:\n{e}")
            import traceback
            traceback.print_exc()
    
    def _create_visualizer_ui(self):
        """Create session visualizer UI components."""
        # Create paned window
        paned = ttk.PanedWindow(self.content_frame, orient=tk.HORIZONTAL)
        paned.pack(fill=tk.BOTH, expand=True)
        
        # Left: Timeline
        left_frame = ttk.Frame(paned)
        
        # Middle: Event graph
        middle_frame = ttk.Frame(paned, width=380)
        
        # Right: Details
        right_frame = ttk.Frame(paned, width=480)
        
        paned.add(left_frame, weight=3)
        paned.add(middle_frame, weight=1)
        paned.add(right_frame, weight=1)
        
        # Timeline setup
        self._setup_timeline(left_frame)
        
        # Event graph setup
        self._setup_event_graph(middle_frame)
        
        # Details panel setup
        self._setup_details_panel(right_frame)
        
        # Draw initial timeline
        self._draw_timeline()
    
    def _setup_timeline(self, parent):
        """Setup timeline canvas."""
        ttk.Label(parent, text="Timeline", 
                 font=("Segoe UI", 12, "bold")).pack(anchor=tk.W, padx=6, pady=(6,0))
        
        ttk.Label(parent, text=f"User: {self.session_data.get('user_name','-')}").pack(
            anchor=tk.W, padx=6)
        
        canvas_frame = ttk.Frame(parent)
        canvas_frame.pack(fill=tk.BOTH, expand=True)
        
        self.canvas = tk.Canvas(canvas_frame, background="#ffffff")
        
        vbar = ttk.Scrollbar(canvas_frame, orient=tk.VERTICAL, 
                           command=self.canvas.yview)
        hbar = ttk.Scrollbar(canvas_frame, orient=tk.HORIZONTAL, 
                           command=self.canvas.xview)
        self.canvas.configure(yscrollcommand=vbar.set, xscrollcommand=hbar.set)
        
        vbar.pack(side=tk.RIGHT, fill=tk.Y)
        hbar.pack(side=tk.BOTTOM, fill=tk.X)
        self.canvas.pack(fill=tk.BOTH, expand=True, side=tk.LEFT)
        
        # Bind events
        self.canvas.tag_bind("room", "<Button-1>", self._on_room_click)
        self.canvas.bind("<Configure>", 
                        lambda e: self.canvas.configure(scrollregion=self.canvas.bbox("all")))
    
    def _setup_event_graph(self, parent):
        """Setup event graph panel."""
        ttk.Label(parent, text="Event Graph", 
                 font=("Segoe UI", 12, "bold")).pack(anchor=tk.W, padx=6, pady=(6,0))
        
        self.fig = Figure(figsize=(4.5, 6), tight_layout=True)
        self.ax = self.fig.add_subplot(111)
        self.canvas_fig = FigureCanvasTkAgg(self.fig, master=parent)
        self.canvas_fig.get_tk_widget().pack(fill=tk.BOTH, expand=True, padx=6, pady=6)
        
        self.fig.canvas.mpl_connect('pick_event', self._on_plot_pick)
    
    def _setup_details_panel(self, parent):
        """Setup details text panel."""
        ttk.Label(parent, text="Details", 
                 font=("Segoe UI", 12, "bold")).pack(anchor=tk.W, padx=6, pady=(6,0))
        
        self.details = tk.Text(parent, wrap=tk.WORD, width=48)
        self.details.pack(fill=tk.BOTH, expand=True, padx=6, pady=6)
    
    def _draw_timeline(self):
        """Draw session timeline."""
        sessions = self.session_data.get("sessions", [])
        if not sessions:
            return
        
        parsed_sessions = []
        max_duration = 0.0
        
        for s in sessions:
            try:
                st = parse_time(s["start_time"])
                et = parse_time(s["end_time"])
            except Exception:
                continue
            
            dur = max(0.0, (et - st).total_seconds())
            parsed_sessions.append((s, st, et, dur))
            if dur > max_duration:
                max_duration = dur
        
        if max_duration <= 0:
            max_duration = 1.0
        
        # Layout constants
        top_margin = 100
        left_margin = 220
        width_per_session = 300
        bar_width = 48
        y_scale = 620.0 / max_duration
        x = left_margin
        
        color_palette = [
            "#ff9999","#99ccff","#99ff99","#ffcc99",
            "#ccccff","#ffb3e6","#c2f0c2","#ffd9b3"
        ]
        
        # Draw sessions
        for idx, (s, st, et, dur) in enumerate(parsed_sessions):
            session_left = x
            session_top = top_margin
            
            # Session header
            header = f"Session {s.get('id', idx)}\n{st.isoformat()}\n→ {et.isoformat()}\n({int(dur)}s)"
            self.canvas.create_text(session_left + bar_width/2, 18, 
                                  text=header, font=("Segoe UI", 9), anchor="n")
            
            sess_h = max(24, dur * y_scale)
            self.canvas.create_rectangle(session_left, session_top, 
                                        session_left + bar_width, 
                                        session_top + sess_h,
                                        fill="#f8f8f8", outline="#d0d0d0")
            
            # Draw rooms
            rooms = s.get("rooms", [])
            for r_idx, r in enumerate(rooms):
                try:
                    ent = parse_time(r["enter_time"])
                    ex = parse_time(r["exit_time"])
                except Exception:
                    continue
                
                rel_start = max(0.0, (ent - st).total_seconds())
                rel_end = max(0.0, (ex - st).total_seconds())
                if rel_end < rel_start:
                    rel_end = rel_start
                
                y0 = session_top + rel_start * y_scale
                y1 = session_top + rel_end * y_scale
                
                x0 = session_left - 6
                x1 = session_left + bar_width + 6
                
                color = color_palette[r_idx % len(color_palette)]
                rect = self.canvas.create_rectangle(x0, y0, x1, y1, 
                                                   fill=color, outline="#222", 
                                                   tags=("room",))
                
                # Store metadata
                meta = {
                    "session_id": s.get("id"),
                    "room_name": r.get("name"),
                    "enter_time": r.get("enter_time"),
                    "exit_time": r.get("exit_time"),
                    "book_session_events": r.get("book_session_events", []),
                    "book_link_events": r.get("book_link_events", [])
                }
                
                self.canvas.itemconfig(rect, tags=("room", f"room_{idx}_{r_idx}"))
                self.canvas.setvar(f"room_meta_{idx}_{r_idx}", json.dumps(meta))
                
                height = y1 - y0
                if height > 18:
                    self.canvas.create_text(session_left - 12, (y0 + y1) / 2,
                                          text=r.get("name", ""), 
                                          font=("Segoe UI", 9), anchor="e")
                
                for be in r.get("book_session_events", []):
                    try:
                        opent = parse_time(be["open_time"])
                        rel = (opent - st).total_seconds()
                        by = session_top + rel * y_scale
                        
                        self.canvas.create_line(session_left + bar_width + 4, by, 
                                              session_left + bar_width + 14, by, 
                                              width=2, dash=(2,2), fill="#666")
                        
                        book_name = be.get("book", {}).get("name", "")
                        if book_name:
                            self.canvas.create_text(session_left - 18, by,
                                                  text=book_name, anchor="e", 
                                                  font=("Segoe UI", 8), fill="#333")
                    except Exception:
                        continue
                
                for le in r.get("book_link_events", []):
                    try:
                        clickt = parse_time(le["click_time"])
                        rel = (clickt - st).total_seconds()
                        ly = session_top + rel * y_scale
                        
                        self.canvas.create_oval(session_left - 20, ly-4, 
                                              session_left - 8, ly+4, 
                                              fill="#000000")
                        
                        link = le.get("link", "")
                        label = extract_link_label(link)
                        display = label if len(label) <= 30 else label[:28] + "…"
                        self.canvas.create_text(session_left - 26, ly, 
                                              text=display, anchor="e", 
                                              font=("Segoe UI", 8), fill="#000")
                    except Exception:
                        continue
            
            x += width_per_session
        
        max_vis_height = top_margin + max(200, int(max_duration * y_scale) + 200)
        self.canvas.configure(scrollregion=(0, 0, x + 400, max_vis_height))
    
    def _on_room_click(self, event):
        """Handle room click in timeline."""
        x = self.canvas.canvasx(event.x)
        y = self.canvas.canvasy(event.y)
        
        items = self.canvas.find_overlapping(x, y, x, y)
        
        for it in items:
            tags = self.canvas.gettags(it)
            for t in tags:
                if t.startswith("room_"):
                    try:
                        parts = t.split("_")
                        meta_json = self.canvas.getvar(f"room_meta_{parts[1]}_{parts[2]}")
                        meta = json.loads(meta_json)
                    except Exception:
                        meta = {}
                    
                    self.selected_room_meta = meta
                    self._show_room_details(meta)
                    self._draw_graph()
                    return
    
    def _show_room_details(self, meta):
        """Show room details in details panel."""
        self.details.delete("1.0", tk.END)
        lines = [
            f"Session ID: {meta.get('session_id')}",
            f"Room: {meta.get('room_name')}",
            f"Enter: {meta.get('enter_time')}",
            f"Exit:  {meta.get('exit_time')}\n",
            "Book events:"
        ]
        
        for be in meta.get("book_session_events", []):
            name = be.get("book", {}).get("name", "(unknown)")
            lines.append(f"  - {name}: {be.get('open_time')} → {be.get('close_time')}")
        
        if not meta.get("book_session_events"):
            lines.append("  (none)")
        
        lines.append("\nLink clicks:")
        for le in meta.get("book_link_events", []):
            link = le.get("link", "")
            label = extract_link_label(link)
            lines.append(f"  - {label}  @ {le.get('click_time')}")
        
        if not meta.get("book_link_events"):
            lines.append("  (none)")
        
        self.details.insert(tk.END, "\n".join(lines))
    
    def _draw_graph(self):
        """Draw event graph for selected room."""
        self.ax.clear()
        self.session_plot_points_meta.clear()
        
        if not self.selected_room_meta:
            return
        
        # Collect events
        events = []
        for s_idx, s in enumerate(self.session_data.get("sessions", [])):
            rooms = s.get("rooms", [])
            for r in rooms:
                if (r.get("name") != self.selected_room_meta.get("room_name") or 
                    s.get("id") != self.selected_room_meta.get("session_id")):
                    continue
                
                # Collect book session events
                for be in r.get("book_session_events", []):
                    try:
                        t = parse_time(be.get("open_time"))
                        label = be.get("book", {}).get("name", "(book)")
                        events.append({
                            "time": t, 
                            "label": label, 
                            "session_idx": s_idx, 
                            "type": "book",
                            "meta": {"session_id": s.get("id"), "room": r.get("name"), **be}
                        })
                    except Exception:
                        continue
                
                # Collect book link events
                for le in r.get("book_link_events", []):
                    try:
                        t = parse_time(le.get("click_time"))
                        label = extract_link_label(le.get("link", "(link)"))
                        events.append({
                            "time": t,
                            "label": label,
                            "session_idx": s_idx,
                            "type": "link",
                            "meta": {"session_id": s.get("id"), "room": r.get("name"), **le}
                        })
                    except Exception:
                        continue
        
        if not events:
            self.ax.text(0.5, 0.5, 'No events to plot', ha='center', va='center')
            self.canvas_fig.draw()
            return
        
        events.sort(key=lambda e: e["time"])
        t0 = events[0]["time"]
        
        xs = [e["session_idx"] for e in events]
        ys = [(e["time"] - t0).total_seconds() / 60.0 for e in events]
        
        color_palette = ["#ff9999","#99ccff","#99ff99","#ffcc99"]
        
        for i, e in enumerate(events):
            color = color_palette[e["session_idx"] % len(color_palette)]
            m = self.ax.scatter(xs[i], ys[i], s=50, color=color, picker=5, zorder=3)
            self.session_plot_points_meta.append({'artist': m, 'meta': e})
        
        self.ax.set_ylabel('Minutes from first event')
        self.ax.grid(True, linestyle=':', linewidth=0.5, alpha=0.6)
        self.canvas_fig.draw()
    
    def _on_plot_pick(self, event):
        """Handle plot point pick event."""
        picked_artist = event.artist
        for entry in self.session_plot_points_meta:
            if entry['artist'] == picked_artist:
                meta = entry['meta']
                self.details.delete("1.0", tk.END)
                self.details.insert(tk.END, 
                    f"Type: {meta.get('type')}\n"
                    f"Label: {meta.get('label')}\n"
                    f"Time: {meta.get('time').isoformat()}")
                return
