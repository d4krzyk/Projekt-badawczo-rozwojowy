"""
Rooms Analytics Panel

Table and chart for room statistics.
"""

import tkinter as tk
from tkinter import ttk
import matplotlib
matplotlib.use('TkAgg')
from matplotlib.figure import Figure
from matplotlib.backends.backend_tkagg import FigureCanvasTkAgg

from ...styles import FONTS

ROOM_COLORS = ['#1976D2', '#D32F2F', '#388E3C', '#F57C00', '#7B1FA2', '#0097A7', '#5D4037', '#455A64']


class RoomsPanel:
    """Rooms analytics panel with table and chart."""
    
    def __init__(self, parent):
        self.frame = ttk.Frame(parent)
        self.frame.pack(fill='both', expand=True)
        
        # Header
        ttk.Label(self.frame, text="📊 Statystyki pokojów", 
                 font=FONTS.get("HEADER", ("Arial", 11, "bold"))).pack(anchor='w', padx=10, pady=5)
        
        # Content - horizontal split
        content = ttk.PanedWindow(self.frame, orient='horizontal')
        content.pack(fill='both', expand=True, padx=5, pady=5)
        
        # Table
        table_frame = ttk.Frame(content)
        content.add(table_frame, weight=1)
        
        cols = ("room", "time", "visits")
        self.tree = ttk.Treeview(table_frame, columns=cols, show="headings", height=12)
        self.tree.heading("room", text="Pokój")
        self.tree.heading("time", text="Czas (s)")
        self.tree.heading("visits", text="Wizyt")
        self.tree.column("room", width=120)
        self.tree.column("time", width=80, anchor='e')
        self.tree.column("visits", width=60, anchor='center')
        
        scroll = ttk.Scrollbar(table_frame, orient='vertical', command=self.tree.yview)
        self.tree.configure(yscrollcommand=scroll.set)
        self.tree.pack(side='left', fill='both', expand=True)
        scroll.pack(side='right', fill='y')
        
        # Chart
        chart_frame = ttk.Frame(content)
        content.add(chart_frame, weight=2)
        
        self.fig = Figure(figsize=(5, 4), dpi=100, facecolor='#FAFAFA')
        self.canvas = FigureCanvasTkAgg(self.fig, master=chart_frame)
        self.canvas.get_tk_widget().pack(fill='both', expand=True)
        
    def update(self, sessions):
        """Update with session data."""
        # Clear
        for item in self.tree.get_children():
            self.tree.delete(item)
        
        # Aggregate
        data = {}
        for idx, sess in sessions:
            rooms = sess.get("metrics", {}).get("rooms", {})
            for name, info in rooms.items():
                if name not in data:
                    data[name] = {"time": 0, "visits": 0}
                data[name]["time"] += info.get("duration", 0)
                data[name]["visits"] += info.get("visits", 0)
        
        for name, info in sorted(data.items()):
            self.tree.insert("", 'end', values=(name, f"{info['time']:.1f}", info['visits']))
        
        # Chart
        self.fig.clear()
        if data:
            ax = self.fig.add_subplot(111)
            names = list(data.keys())
            times = [data[n]["time"] for n in names]
            colors = [ROOM_COLORS[i % len(ROOM_COLORS)] for i in range(len(names))]
            
            ax.barh(names, times, color=colors, edgecolor='white', height=0.6)
            ax.set_xlabel("Czas (s)")
            ax.set_title("Czas w pokojach", fontweight='bold', fontsize=10)
            ax.invert_yaxis()
            ax.spines['top'].set_visible(False)
            ax.spines['right'].set_visible(False)
        
        self.canvas.draw()
