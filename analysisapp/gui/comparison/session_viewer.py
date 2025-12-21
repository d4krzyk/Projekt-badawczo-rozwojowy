"""
Multi-Session Comparison Viewer

Orchestrator using modular analytics components.
"""

import tkinter as tk
from tkinter import ttk

from ..styles import FONTS
from .analytics import TimelineChart, RoomsPanel, BooksPanel, LinksPanel

SESSION_COLORS = ['#2196F3', '#F44336', '#4CAF50', '#FF9800', '#9C27B0', '#00BCD4', '#795548', '#607D8B']


class MultiSessionViewer:
    """Multi-tab session analysis viewer (orchestrator)."""
    
    def __init__(self, parent):
        self.parent = parent
        self.sessions_data = []
        self.selected_sessions = []
        
        self._create_ui()
        
    def _create_ui(self):
        # Main layout: sidebar left, content right
        main = ttk.PanedWindow(self.parent, orient='horizontal')
        main.pack(fill='both', expand=True, padx=5, pady=5)
        
        # Left sidebar
        sidebar = ttk.Frame(main, width=180)
        main.add(sidebar, weight=0)
        self._create_sidebar(sidebar)
        
        # Right content area with tabs
        content = ttk.Frame(main)
        main.add(content, weight=4)
        self._create_tabs(content)
        
    def _create_sidebar(self, parent):
        """Create session selection sidebar."""
        ttk.Label(parent, text="Wybierz sesje:", 
                 font=FONTS.get("HEADER", ("Arial", 10, "bold"))).pack(anchor='w', pady=(5, 2))
        ttk.Label(parent, text="(CTRL+klik = multi)", 
                 font=("Arial", 8), foreground='#666').pack(anchor='w')
        
        # Listbox
        list_frame = ttk.Frame(parent)
        list_frame.pack(fill='both', expand=True, pady=5)
        
        scroll = ttk.Scrollbar(list_frame)
        scroll.pack(side='right', fill='y')
        
        self.listbox = tk.Listbox(
            list_frame, selectmode='extended', exportselection=False,
            yscrollcommand=scroll.set, font=("Segoe UI", 9), height=12
        )
        self.listbox.pack(side='left', fill='both', expand=True)
        scroll.config(command=self.listbox.yview)
        self.listbox.bind('<<ListboxSelect>>', lambda e: self._refresh())
        
        # Buttons
        btns = ttk.Frame(parent)
        btns.pack(fill='x', pady=5)
        ttk.Button(btns, text="Wszystkie", command=self._select_all, width=10).pack(side='left', padx=2)
        ttk.Button(btns, text="Odznacz", command=self._clear, width=10).pack(side='left', padx=2)
        
    def _create_tabs(self, parent):
        """Create content tabs."""
        self.notebook = ttk.Notebook(parent)
        self.notebook.pack(fill='both', expand=True)
        
        # Tab 1: Timeline
        tab1 = ttk.Frame(self.notebook, padding=5)
        self.notebook.add(tab1, text=" ⏱ Timeline ")
        self.timeline = TimelineChart(tab1)
        
        # Tab 2: Rooms
        tab2 = ttk.Frame(self.notebook, padding=5)
        self.notebook.add(tab2, text=" 🏠 Pokoje ")
        self.rooms = RoomsPanel(tab2)
        
        # Tab 3: Books
        tab3 = ttk.Frame(self.notebook, padding=5)
        self.notebook.add(tab3, text=" 📚 Książki ")
        self.books = BooksPanel(tab3)
        
        # Tab 4: Links
        tab4 = ttk.Frame(self.notebook, padding=5)
        self.notebook.add(tab4, text=" 🔗 Linki ")
        self.links = LinksPanel(tab4)

    def update(self, sessions):
        """Update with new session data."""
        self.sessions_data = sessions
        self.listbox.delete(0, 'end')
        
        for i, s in enumerate(sessions):
            self.listbox.insert('end', s['file'])
            self.listbox.itemconfig(i, fg=SESSION_COLORS[i % len(SESSION_COLORS)])
        
        if len(sessions) <= 4:
            self._select_all()

    def _select_all(self):
        self.listbox.select_set(0, 'end')
        self._refresh()

    def _clear(self):
        self.listbox.selection_clear(0, 'end')
        self.selected_sessions = []
        self.timeline.update([])

    def _refresh(self):
        """Refresh all panels with selected sessions."""
        sel = self.listbox.curselection()
        self.selected_sessions = [(i, self.sessions_data[i]) for i in sel]
        
        self.timeline.update(self.selected_sessions)
        self.rooms.update(self.selected_sessions)
        self.books.update(self.selected_sessions)
        self.links.update(self.selected_sessions)
