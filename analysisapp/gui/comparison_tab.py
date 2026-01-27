"""
Comparison Tab

Tab component for comparing multiple sessions with advanced visualization.
Orchestrator using modular components.
"""

import tkinter as tk
from tkinter import ttk, messagebox

from ..analyzers.session.comparison import aggregate_metrics_multi
from .styles import FONTS
from .comparison import (
    ChartRenderer, GraphRenderer, TableRenderer,
    RoomMappingDialog, SessionManager, SidebarBuilder,
    MultiSessionViewer
)


class ComparisonTab:
    """
    Orchestrator zakładki porównania sesji.
    
    Deleguje renderowanie do:
    - ChartRenderer (wykresy słupkowe)
    - GraphRenderer (graf przejść)
    - TableRenderer (tabela metryk)
    - SidebarBuilder (budowa sidebaru)
    - SessionManager (zarządzanie sesjami)
    """
    
    def __init__(self, parent, status_callback=None):
        self.parent = parent
        self.status_callback = status_callback
        
        # Dane sesji
        self.sessions = []
        self.mapped_pairs = None
        self._last_aggregated_data = None
        
        # Tworzenie głównej ramki
        self.frame = ttk.Frame(parent)
        parent.add(self.frame, text="Porównanie Sesji")
        
        # Komponenty (inicjalizowane w _create_ui)
        self.sidebar_builder = None
        self.session_manager = None
        self.chart_renderer = None
        self.graph_renderer = None
        self.table_renderer = None
        self.session_viewer = None
        
        self._create_ui()
        
    def _create_ui(self):
        """Buduje interfejs użytkownika zakładki (Layout: Sidebar + Content)."""
        # Główny kontener z podziałem poziomym
        paned = ttk.PanedWindow(self.frame, orient=tk.HORIZONTAL)
        paned.pack(fill=tk.BOTH, expand=True, padx=5, pady=5)
        
        # 1. Sidebar (Lewa strona)
        sidebar = ttk.Frame(paned, width=300, padding=10)
        paned.add(sidebar, weight=0)
        
        # 2. Content (Prawa strona)
        content_area = ttk.Frame(paned, padding=5)
        paned.add(content_area, weight=4)
        
        # Budowa sidebaru
        callbacks = {
            "add_session": self._add_session,
            "remove_session": self._remove_session,
            "clear_sessions": self._clear_sessions,
            "open_mapping_dialog": self._open_mapping_dialog,
            "perform_comparison": self._perform_comparison,
            "on_chart_config_change": self._on_chart_config_change,
            "on_graph_config_change": self._on_graph_config_change
        }
        self.sidebar_builder = SidebarBuilder(sidebar, callbacks)
        self.sidebar_builder.build()
        
        # Budowa obszaru zawartości
        self._build_content_area(content_area)
        
        # Inicjalizacja session managera
        self.session_manager = SessionManager(
            self.frame,
            self.sidebar_builder.session_tree,
            self.sessions,
            self.status_callback
        )
        self.session_manager.on_sessions_changed = self._on_sessions_changed

    def _build_content_area(self, content_area):
        """Tworzy obszar zawartości z zakładkami."""
        content_notebook = ttk.Notebook(content_area)
        content_notebook.pack(fill=tk.BOTH, expand=True)
        
        # Zakładka 1: Wykresy
        charts_frame = ttk.Frame(content_notebook)
        content_notebook.add(charts_frame, text=" 📊 Wykresy ")
        self.chart_renderer = ChartRenderer(charts_frame)
        
        # Zakładka 2: Graf
        graph_frame = ttk.Frame(content_notebook)
        content_notebook.add(graph_frame, text=" 🕸 Graf Przejść ")
        self.graph_renderer = GraphRenderer(graph_frame)
        
        # Zakładka 3: Tabela
        table_frame = ttk.Frame(content_notebook)
        content_notebook.add(table_frame, text=" 📋 Dane Szczegółowe ")
        self.table_renderer = TableRenderer(table_frame)
        
        # Zakładka 4: Porównanie Sesji
        session_frame = ttk.Frame(content_notebook)
        content_notebook.add(session_frame, text=" 🔍 Porównanie Sesji ")
        self.session_viewer = MultiSessionViewer(session_frame)
        
        # Wiadomość początkowa
        msg_label = ttk.Label(
            table_frame,
            text="Dodaj co najmniej jedną sesję i kliknij 'Porównaj'.",
            font=FONTS.get("NORMAL", ("Arial", 10))
        )
        msg_label.pack(expand=True)

    # --- Zarządzanie sesjami (delegacja do SessionManager) ---
    
    def _add_session(self):
        """Deleguje dodawanie sesji do sesjon managera."""
        self.session_manager.add_session()

    def _remove_session(self):
        """Deleguje usuwanie sesji do session managera."""
        self.session_manager.remove_session()

    def _clear_sessions(self):
        """Deleguje czyszczenie sesji do session managera."""
        self.session_manager.clear_sessions()

    def _on_sessions_changed(self):
        """Callback wywoływany po zmianie sesji."""
        self.mapped_pairs = None

    # --- Mapowanie ---
    
    def _open_mapping_dialog(self):
        """Otwiera dialog konfiguracji mapowania wizyt."""
        if not self.sessions:
            messagebox.showwarning("Uwaga", "Brak sesji.")
            return

        names = [s["file"] for s in self.sessions]
        visits_list = [s["metrics"].get("visits", []) for s in self.sessions]
        
        RoomMappingDialog(self.frame, names, visits_list, self.mapped_pairs, self._set_mapping)
        
    def _set_mapping(self, mapping):
        """Ustawia mapowanie wizyt."""
        self.mapped_pairs = mapping
        if self.status_callback:
            self.status_callback(f"Zaktualizowano mapowanie ({len(mapping)} wierszy).")

    # --- Konfiguracja wykresów ---

    def _on_chart_config_change(self, event=None):
        """Obsługuje zmianę konfiguracji wykresów."""
        if self._last_aggregated_data:
            self._update_charts_only()

    def _on_graph_config_change(self, event=None):
        """Obsługuje zmianę konfiguracji grafu."""
        if self._last_aggregated_data:
            self._update_graph_only()

    def _update_charts_only(self):
        """Aktualizuje tylko renderer wykresów."""
        chart_config = {
            "chart1": self.sidebar_builder.combo_chart1.get(),
            "chart2": self.sidebar_builder.combo_chart2.get()
        }
        self.chart_renderer.update(self._last_aggregated_data, chart_config)

    def _update_graph_only(self):
        """Aktualizuje tylko renderer grafu."""
        graph_config = {
            "layout": self.sidebar_builder.combo_layout.get(),
            "show_order": self.sidebar_builder.var_show_order.get()
        }
        self.graph_renderer.update(self.sessions, self._last_aggregated_data, graph_config)

    # --- Porównanie ---
    
    def _perform_comparison(self):
        """Wykonuje porównanie i aktualizuje wszystkie wizualizacje."""
        if not self.sessions:
            return
            
        # Przygotowanie danych
        sessions_input = [(s["file"], s["metrics"]) for s in self.sessions]
        aggregated_data = aggregate_metrics_multi(sessions_input, room_mapping=self.mapped_pairs)
        self._last_aggregated_data = aggregated_data
        
        # Konfiguracje
        chart_config = {
            "chart1": self.sidebar_builder.combo_chart1.get(),
            "chart2": self.sidebar_builder.combo_chart2.get()
        }
        graph_config = {
            "layout": self.sidebar_builder.combo_layout.get(),
            "show_order": self.sidebar_builder.var_show_order.get()
        }
        
        # Aktualizacja rendererów
        self.chart_renderer.update(aggregated_data, chart_config)
        self.graph_renderer.update(self.sessions, aggregated_data, graph_config)
        self.table_renderer.update(aggregated_data)
        self.session_viewer.update(self.sessions)
