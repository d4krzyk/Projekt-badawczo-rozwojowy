"""
Books Analytics Panel

Table and chart for book statistics.
"""

import tkinter as tk
from tkinter import ttk
import matplotlib
matplotlib.use('TkAgg')
from matplotlib.figure import Figure
from matplotlib.backends.backend_tkagg import FigureCanvasTkAgg

from ...styles import FONTS


class BooksPanel:
    """Books analytics panel with table and chart."""
    
    def __init__(self, parent):
        self.frame = ttk.Frame(parent)
        self.frame.pack(fill='both', expand=True)
        
        ttk.Label(self.frame, text="📚 Statystyki książek", 
                 font=FONTS.get("HEADER", ("Arial", 11, "bold"))).pack(anchor='w', padx=10, pady=5)
        
        content = ttk.PanedWindow(self.frame, orient='horizontal')
        content.pack(fill='both', expand=True, padx=5, pady=5)
        
        # Table
        table_frame = ttk.Frame(content)
        content.add(table_frame, weight=1)
        
        cols = ("book", "opens", "time")
        self.tree = ttk.Treeview(table_frame, columns=cols, show="headings", height=12)
        self.tree.heading("book", text="Książka")
        self.tree.heading("opens", text="Otwarć")
        self.tree.heading("time", text="Czas (s)")
        self.tree.column("book", width=150)
        self.tree.column("opens", width=60, anchor='center')
        self.tree.column("time", width=80, anchor='e')
        
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
        for item in self.tree.get_children():
            self.tree.delete(item)
        
        data = {}
        for idx, sess in sessions:
            books = sess.get("metrics", {}).get("books", {})
            for name, info in books.items():
                if name not in data:
                    data[name] = {"opens": 0, "time": 0}
                data[name]["opens"] += info.get("opens", 0)
                data[name]["time"] += info.get("total_time", 0)
        
        for name, info in sorted(data.items(), key=lambda x: -x[1]["opens"]):
            self.tree.insert("", 'end', values=(name, info['opens'], f"{info['time']:.1f}"))
        
        self.fig.clear()
        if data:
            ax = self.fig.add_subplot(111)
            top = sorted(data.items(), key=lambda x: -x[1]["opens"])[:8]
            names = [x[0][:20] for x in top]
            opens = [x[1]["opens"] for x in top]
            
            ax.barh(names, opens, color='#2196F3', edgecolor='white', height=0.6)
            ax.set_xlabel("Otwarć")
            ax.set_title("Najczęściej otwierane", fontweight='bold', fontsize=10)
            ax.invert_yaxis()
            ax.spines['top'].set_visible(False)
            ax.spines['right'].set_visible(False)
        
        self.canvas.draw()
