"""
Sidebar Builder

Builds sidebar UI components for comparison tab.
"""

import tkinter as tk
from tkinter import ttk

from ..styles import FONTS


class SidebarBuilder:
    """Builds sidebar UI components."""
    
    def __init__(self, parent, callbacks):
        """
        Initialize sidebar builder.
        
        Args:
            parent: Parent frame for sidebar
            callbacks: Dict with callback functions:
                - add_session
                - remove_session
                - clear_sessions
                - open_mapping_dialog
                - perform_comparison
                - on_chart_config_change
                - on_graph_config_change
        """
        self.parent = parent
        self.callbacks = callbacks
        
        # UI Components (will be set during build)
        self.session_tree = None
        self.combo_chart1 = None
        self.combo_chart2 = None
        self.combo_layout = None
        self.var_show_order = None

    def build(self):
        """Build the complete sidebar with tabs."""
        notebook = ttk.Notebook(self.parent)
        notebook.pack(fill=tk.BOTH, expand=True, pady=5)
        
        # Tab 1: Sesje
        tab_sessions = ttk.Frame(notebook, padding=5)
        notebook.add(tab_sessions, text="Sesje")
        self._build_sessions_tab(tab_sessions)
        
        # Tab 2: Wykresy
        tab_charts = ttk.Frame(notebook, padding=5)
        notebook.add(tab_charts, text="Wykresy")
        self._build_charts_config(tab_charts)
        
        # Tab 3: Graf
        tab_graph = ttk.Frame(notebook, padding=5)
        notebook.add(tab_graph, text="Graf")
        self._build_graph_config(tab_graph)
        
        # Tab 4: Dane
        tab_table = ttk.Frame(notebook, padding=5)
        notebook.add(tab_table, text="Dane")
        self._build_table_config(tab_table)
        
        return notebook

    def _build_sessions_tab(self, parent):
        """Buduje zawartość zakładki 'Sesje'."""
        lbl = ttk.Label(parent, text="Dostępne Sesje:", font=FONTS.get("HEADER", ("Arial", 10, "bold")))
        lbl.pack(anchor=tk.W, pady=(0, 5))
        
        self.session_tree = ttk.Treeview(
            parent, 
            columns=("idx", "file", "duration"), 
            show="headings", 
            height=15
        )
        self.session_tree.heading("idx", text="#")
        self.session_tree.heading("file", text="Plik")
        self.session_tree.heading("duration", text="Czas")
        
        self.session_tree.column("idx", width=30, anchor=tk.CENTER)
        self.session_tree.column("file", width=120)
        self.session_tree.column("duration", width=60, anchor=tk.E)
        
        self.session_tree.pack(fill=tk.BOTH, expand=True, pady=5)
        
        # Przyciski zarządzania
        btn_frame = ttk.Frame(parent)
        btn_frame.pack(fill=tk.X, pady=5)
        
        ttk.Button(btn_frame, text="+", width=3, 
                   command=self.callbacks.get("add_session")).pack(side=tk.LEFT, padx=(0, 2))
        ttk.Button(btn_frame, text="-", width=3, 
                   command=self.callbacks.get("remove_session")).pack(side=tk.LEFT, padx=2)
        ttk.Button(btn_frame, text="Wyczyść", 
                   command=self.callbacks.get("clear_sessions")).pack(side=tk.LEFT, fill=tk.X, expand=True, padx=(2, 0))
        
        ttk.Separator(parent, orient=tk.HORIZONTAL).pack(fill=tk.X, pady=10)
        
        # Akcje
        ttk.Button(parent, text="🛠 Mapowanie", 
                   command=self.callbacks.get("open_mapping_dialog")).pack(fill=tk.X, pady=5)
        
        # Główny przycisk
        btn_compare = ttk.Button(parent, text="▶ PORÓWNAJ", 
                                 command=self.callbacks.get("perform_comparison"))
        btn_compare.pack(fill=tk.X, pady=10, ipady=5)

    def _build_charts_config(self, parent):
        """Konfiguracja dla wykresów."""
        ttk.Label(parent, text="Wykres 1 (Wizyty):").pack(anchor=tk.W)
        self.combo_chart1 = ttk.Combobox(parent, state="readonly", values=[
            "Czas trwania (s)", 
            "Liczba otwarć książek", 
            "Kliknięcia w linki"
        ])
        self.combo_chart1.current(0)
        self.combo_chart1.pack(fill=tk.X, pady=(0, 5))
        self.combo_chart1.bind("<<ComboboxSelected>>", self.callbacks.get("on_chart_config_change"))

        ttk.Label(parent, text="Wykres 2 (Globalne):").pack(anchor=tk.W)
        self.combo_chart2 = ttk.Combobox(parent, state="readonly", values=[
            "Podsumowanie Aktywności",
            "Czas trwania sesji",
            "Tempo eksploracji",
            "Gęstość zdarzeń",
            "Śr. czas z książką"
        ])
        self.combo_chart2.current(0)
        self.combo_chart2.pack(fill=tk.X, pady=(0, 10))
        self.combo_chart2.bind("<<ComboboxSelected>>", self.callbacks.get("on_chart_config_change"))

    def _build_graph_config(self, parent):
        """Konfiguracja dla grafu."""
        ttk.Label(parent, text="Układ węzłów:").pack(anchor=tk.W)
        self.combo_layout = ttk.Combobox(parent, state="readonly", values=["Spring", "Circular", "Shell", "Kamada-Kawai"])
        self.combo_layout.current(0)
        self.combo_layout.pack(fill=tk.X, pady=(0, 5))
        self.combo_layout.bind("<<ComboboxSelected>>", self.callbacks.get("on_graph_config_change"))
        
        self.var_show_order = tk.BooleanVar(value=True)
        cb = ttk.Checkbutton(parent, text="Pokaż kolejność przejść", 
                             variable=self.var_show_order, 
                             command=self.callbacks.get("on_graph_config_change"))
        cb.pack(anchor=tk.W, pady=(5, 0))

    def _build_table_config(self, parent):
        """Konfiguracja dla tabeli."""
        ttk.Label(parent, text="Styl tabeli został zaktualizowany dla lepszej czytelności.", 
                  wraplength=180).pack(anchor=tk.W)
