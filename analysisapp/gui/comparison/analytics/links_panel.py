"""
Links Analytics Panel

Table and chart for link statistics.
"""

import tkinter as tk
from tkinter import ttk
import matplotlib
matplotlib.use('TkAgg')
from matplotlib.figure import Figure
from matplotlib.backends.backend_tkagg import FigureCanvasTkAgg

from ...styles import FONTS

SESSION_COLORS = ['#2196F3', '#F44336', '#4CAF50', '#FF9800', '#9C27B0', '#00BCD4', '#795548', '#607D8B']


class LinksPanel:
    """Links analytics panel with table and chart."""
    
    def __init__(self, parent):
        self.frame = ttk.Frame(parent)
        self.frame.pack(fill='both', expand=True)
        
        ttk.Label(self.frame, text="🔗 Statystyki linków", 
                 font=FONTS.get("HEADER", ("Arial", 11, "bold"))).pack(anchor='w', padx=10, pady=5)
        
        content = ttk.PanedWindow(self.frame, orient='horizontal')
        content.pack(fill='both', expand=True, padx=5, pady=5)
        
        # Table
        table_frame = ttk.Frame(content)
        content.add(table_frame, weight=1)
        
        cols = ("session", "clicks")
        self.tree = ttk.Treeview(table_frame, columns=cols, show="headings", height=12)
        self.tree.heading("session", text="Sesja")
        self.tree.heading("clicks", text="Kliknięć")
        self.tree.column("session", width=180)
        self.tree.column("clicks", width=80, anchor='center')
        
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
        
        data = []
        for idx, sess in sessions:
            clicks = sess.get("metrics", {}).get("total_links_clicked", 0)
            data.append((sess['file'], clicks, idx))
            self.tree.insert("", 'end', values=(sess['file'], clicks))
        
        self.fig.clear()
        if data:
            ax = self.fig.add_subplot(111)
            names = [d[0][:15] for d in data]
            clicks = [d[1] for d in data]
            colors = [SESSION_COLORS[d[2] % len(SESSION_COLORS)] for d in data]
            
            bars = ax.bar(range(len(names)), clicks, color=colors, edgecolor='white')
            ax.set_xticks(range(len(names)))
            ax.set_xticklabels(names, rotation=30, ha='right', fontsize=8)
            ax.set_ylabel("Kliknięć")
            ax.set_title("Linki per sesja", fontweight='bold', fontsize=10)
            ax.spines['top'].set_visible(False)
            ax.spines['right'].set_visible(False)
        
        self.canvas.draw()
