"""
Timeline Panel for Session Analyzer.

Handles the visualization of session timelines, including rooms, books, and links.
"""

import json
import tkinter as tk
from tkinter import ttk
from ...analyzers.session.core import parse_time, extract_link_label
from ..styles import FONTS, COLORS

class TimelinePanel:
    """Manages the timeline visualization."""
    
    def __init__(self, parent, on_room_click_callback=None):
        """
        Initialize Timeline Panel.
        
        Args:
            parent: Parent widget
            on_room_click_callback: Function to call when a room is clicked
        """
        self.parent = parent
        self.on_room_click = on_room_click_callback
        self.canvas = None
        self.room_colors = {}
        
        self._setup_ui()

    def _setup_ui(self):
        """Setup the timeline UI components."""
        # Header
        header_frame = ttk.Frame(self.parent)
        header_frame.pack(fill=tk.X, padx=6, pady=(6,0))
        
        ttk.Label(header_frame, text="Oś Czasu", 
                 font=FONTS["HEADER"]).pack(anchor=tk.W)
        
        self.user_label = ttk.Label(header_frame, text="Użytkownik: -")
        self.user_label.pack(anchor=tk.W)
        
        # Legend
        self.legend_frame = ttk.Frame(self.parent)
        self.legend_frame.pack(fill=tk.X, padx=6, pady=(4, 6))
        
        # Canvas
        canvas_frame = ttk.Frame(self.parent)
        canvas_frame.pack(fill=tk.BOTH, expand=True)
        
        self.canvas = tk.Canvas(canvas_frame, background=COLORS["BG_WHITE"])
        
        vbar = ttk.Scrollbar(canvas_frame, orient=tk.VERTICAL, 
                           command=self.canvas.yview)
        hbar = ttk.Scrollbar(canvas_frame, orient=tk.HORIZONTAL, 
                           command=self.canvas.xview)
        self.canvas.configure(yscrollcommand=vbar.set, xscrollcommand=hbar.set)
        
        vbar.pack(side=tk.RIGHT, fill=tk.Y)
        hbar.pack(side=tk.BOTTOM, fill=tk.X)
        self.canvas.pack(fill=tk.BOTH, expand=True, side=tk.LEFT)
        
        # Bind events
        self.canvas.tag_bind("room", "<Button-1>", self._handle_click)
        self.canvas.bind("<Configure>", 
                        lambda e: self.canvas.configure(scrollregion=self.canvas.bbox("all")))

    def update_data(self, data_manager):
        """Update visualization with new data."""
        if not data_manager or not data_manager.session_data:
            return

        self.user_label.config(text=f"Użytkownik: {data_manager.get_user_name()}")
        self._update_legend(data_manager)
        self._draw_timeline(data_manager)

    def _update_legend(self, data_manager):
        """Update the room color legend."""
        for widget in self.legend_frame.winfo_children():
            widget.destroy()
            
        ttk.Label(self.legend_frame, text="Pokoje:", font=FONTS["HEADER"]).grid(
            row=0, column=0, sticky=tk.W, padx=(0, 6))
            
        # Extract unique rooms
        all_rooms = set()
        for s in data_manager.get_sessions():
            for r in s.get("rooms", []):
                if "name" in r:
                    all_rooms.add(r["name"])
        
        sorted_rooms = sorted(list(all_rooms))
        palette = COLORS["ROOM_PALETTE"]
        
        self.room_colors = {}
        for i, room_name in enumerate(sorted_rooms):
            color = palette[i % len(palette)]
            self.room_colors[room_name] = color
            
            c = tk.Canvas(self.legend_frame, width=12, height=12, highlightthickness=0)
            c.grid(row=0, column=1+i*2, padx=(0, 2))
            c.create_rectangle(1, 1, 11, 11, fill=color, outline="#333")
            ttk.Label(self.legend_frame, text=room_name, font=("Segoe UI", 8)).grid(
                row=0, column=2+i*2, padx=(0, 6))

    def _draw_timeline(self, data_manager):
        """Draw the timeline on canvas."""
        self.canvas.delete("all")
        
        sessions = data_manager.get_sessions()
        if not sessions:
            return
            
        parsed_sessions = self._parse_sessions(sessions)
        max_duration = max([s[3] for s in parsed_sessions]) if parsed_sessions else 1.0
        if max_duration <= 0: max_duration = 1.0
        
        # Layout constants
        top_margin = 20
        left_margin = 20
        width_per_session = 300
        bar_width = 48
        y_scale = 620.0 / max_duration
        x = left_margin
        
        for idx, (s, st, et, dur) in enumerate(parsed_sessions):
            self._draw_single_session(x, top_margin, s, st, et, dur, y_scale, bar_width, idx)
            x += width_per_session
            
        max_vis_height = top_margin + max(200, int(max_duration * y_scale) + 200)
        self.canvas.configure(scrollregion=(0, 0, x + 100, max_vis_height))

    def _parse_sessions(self, sessions):
        """Parse session times."""
        parsed = []
        for s in sessions:
            try:
                st = parse_time(s["start_time"])
                et = parse_time(s["end_time"])
                dur = max(0.0, (et - st).total_seconds())
                parsed.append((s, st, et, dur))
            except Exception:
                continue
        return parsed

    def _draw_single_session(self, x, y, session, st, et, dur, y_scale, width, idx):
        """Draw a single session column."""
        # Header
        header = f"Sesja {session.get('id', idx)}\n{st.strftime('%H:%M:%S')}\n→ {et.strftime('%H:%M:%S')}\n({dur:.1f}s)"
        self.canvas.create_text(x + width/2, y - 10, 
                              text=header, font=("Segoe UI", 9), anchor="s")
        
        sess_h = max(24, dur * y_scale)
        self.canvas.create_rectangle(x, y, x + width, y + sess_h,
                                   fill="#f8f8f8", outline="#d0d0d0")
                                   
        for r_idx, room in enumerate(session.get("rooms", [])):
            self._draw_room(x, y, room, st, y_scale, width, idx, r_idx, session.get("id"))

    def _draw_room(self, x, y, room, session_start, y_scale, width, s_idx, r_idx, session_id):
        """Draw a single room block."""
        try:
            ent = parse_time(room["enter_time"])
            ex = parse_time(room["exit_time"])
            
            rel_start = max(0.0, (ent - session_start).total_seconds())
            rel_end = max(0.0, (ex - session_start).total_seconds())
            if rel_end < rel_start: rel_end = rel_start
            
            y0 = y + rel_start * y_scale
            y1 = y + rel_end * y_scale
            
            color = self.room_colors.get(room.get("name"), "#cccccc")
            
            rect = self.canvas.create_rectangle(x - 6, y0, x + width + 6, y1,
                                              fill=color, outline="#222",
                                              tags=("room",))
            
            # Metadata for click handling
            meta = {
                "session_id": session_id,
                "session_idx": s_idx,
                "room_name": room.get("name"),
                "enter_time": room.get("enter_time"),
                "exit_time": room.get("exit_time"),
                "book_session_events": room.get("book_session_events", []),
                "book_link_events": room.get("book_link_events", [])
            }
            
            # Store meta in canvas tag/var
            tag = f"room_{s_idx}_{r_idx}"
            self.canvas.itemconfig(rect, tags=("room", tag))
            self.canvas.setvar(f"room_meta_{tag}", json.dumps(meta))
            
            # Label
            if (y1 - y0) > 18:
                self.canvas.create_text(x - 12, (y0 + y1) / 2,
                                      text=room.get("name", ""),
                                      font=("Segoe UI", 9), anchor="e")
                                      
            # Events
            self._draw_room_events(x, y, room, session_start, y_scale, width)
            
        except Exception:
            pass

    def _draw_room_events(self, x, y, room, session_start, y_scale, width):
        """Draw book and link events."""
        # Books
        for be in room.get("book_session_events", []):
            try:
                opent = parse_time(be["open_time"])
                rel = (opent - session_start).total_seconds()
                by = y + rel * y_scale
                
                self.canvas.create_line(x + width + 4, by, x + width + 14, by,
                                      width=2, dash=(2,2), fill="#666")
                
                book_name = be.get("book", {}).get("name", "")
                if book_name:
                    self.canvas.create_text(x - 18, by, text=book_name, anchor="e",
                                          font=("Segoe UI", 8), fill="#333")
            except: continue

        # Links
        for le in room.get("book_link_events", []):
            try:
                clickt = parse_time(le["click_time"])
                rel = (clickt - session_start).total_seconds()
                ly = y + rel * y_scale
                
                self.canvas.create_oval(x - 20, ly-4, x - 8, ly+4, fill="#000000")
                
                link = le.get("link", "")
                label = extract_link_label(link)
                display = label if len(label) <= 30 else label[:28] + "…"
                self.canvas.create_text(x - 26, ly, text=display, anchor="e",
                                      font=("Segoe UI", 8), fill="#000")
            except: continue

    def _handle_click(self, event):
        """Handle room click event."""
        if not self.on_room_click:
            return
            
        x = self.canvas.canvasx(event.x)
        y = self.canvas.canvasy(event.y)
        
        items = self.canvas.find_overlapping(x, y, x, y)
        for it in items:
            tags = self.canvas.gettags(it)
            for t in tags:
                if t.startswith("room_"):
                    try:
                        meta_json = self.canvas.getvar(f"room_meta_{t}")
                        meta = json.loads(meta_json)
                        self.on_room_click(meta)
                        return
                    except Exception:
                        continue
