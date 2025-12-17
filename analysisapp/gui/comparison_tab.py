"""
Comparison Tab

Tab component for comparing multiple sessions with advanced visualization.
Orchestrator using ChartRenderer, GraphRenderer, and TableRenderer.
"""

import os
import tkinter as tk
from tkinter import ttk, filedialog, messagebox

from ..analyzers.session.comparison import calculate_session_metrics, aggregate_metrics_multi
from ..app_config import config
from .styles import FONTS
from .session.data_manager import DataManager
from .comparison import ChartRenderer, GraphRenderer, TableRenderer


class RoomMappingDialog(tk.Toplevel):
    """Dialog for configuring visit mapping pairs (N-way) based on visit sequence."""
    
    def __init__(self, parent, session_names, sessions_visits, current_mapping, callback):
        super().__init__(parent)
        self.title("Konfiguracja Mapowania Wizyt (Przebieg)")
        self.geometry("900x550")
        self.transient(parent)
        self.grab_set()
        
        self.session_names = session_names
        self.sessions_visits_raw = sessions_visits
        
        # Przygotowanie etykiet: "1. Kitchen", "2. Salon", etc.
        self.display_values = []
        for s_idx, visits in enumerate(sessions_visits):
            vals = [f"{i+1}. {v['name']}" for i, v in enumerate(visits)]
            self.display_values.append(vals)
            
        self.mapping = list(current_mapping) if current_mapping else []
        self.callback = callback
        self.combos = []
        
        self._create_ui()
        self._center_on_parent(parent)
        
    def _center_on_parent(self, parent):
        """Centruje okno dialogowe względem rodzica."""
        self.update_idletasks()
        x = parent.winfo_rootx() + (parent.winfo_width() - self.winfo_width()) // 2
        y = parent.winfo_rooty() + (parent.winfo_height() - self.winfo_height()) // 2
        self.geometry(f"+{x}+{y}")
        
    def _create_ui(self):
        # Panel kontrolny
        control_frame = ttk.LabelFrame(self, text="Dodaj Wiersz Mapowania (Wybierz wizytę)")
        control_frame.pack(fill=tk.X, padx=10, pady=5)
        
        inner_frame = ttk.Frame(control_frame)
        inner_frame.pack(side=tk.LEFT, fill=tk.X, expand=True)
        
        for i, name in enumerate(self.session_names):
            f = ttk.Frame(inner_frame)
            f.pack(side=tk.LEFT, padx=5)
            ttk.Label(f, text=name[:10]+"...").pack(anchor=tk.W)
            cb = ttk.Combobox(f, values=self.display_values[i], state="readonly", width=18)
            cb.pack()
            self.combos.append(cb)
        
        ttk.Button(control_frame, text="Dodaj", command=self._add_row).pack(side=tk.LEFT, padx=10)
        
        # Lista mapowań
        list_frame = ttk.Frame(self)
        list_frame.pack(fill=tk.BOTH, expand=True, padx=10, pady=5)
        
        cols = ["idx"] + [f"s_{i}" for i in range(len(self.session_names))]
        self.tree = ttk.Treeview(list_frame, columns=cols, show="headings", height=12)
        
        self.tree.heading("idx", text="#")
        self.tree.column("idx", width=40, anchor=tk.CENTER)
        
        for i, name in enumerate(self.session_names):
            col_id = f"s_{i}"
            self.tree.heading(col_id, text=name)
            self.tree.column(col_id, width=150)
            
        scr = ttk.Scrollbar(list_frame, orient=tk.VERTICAL, command=self.tree.yview)
        self.tree.configure(yscroll=scr.set)
        
        self.tree.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)
        scr.pack(side=tk.RIGHT, fill=tk.Y)
        
        # Przyciski akcji
        btn_frame = ttk.Frame(self)
        btn_frame.pack(fill=tk.X, padx=10, pady=10)
        
        ttk.Button(btn_frame, text="Usuń zaznaczone", command=self._remove_row).pack(side=tk.LEFT)
        ttk.Button(btn_frame, text="Auto (Po Kolei)", command=self._auto_map).pack(side=tk.LEFT, padx=10)
        ttk.Button(btn_frame, text="Wyczyść", command=self._clear_all).pack(side=tk.LEFT)
        
        ttk.Button(btn_frame, text="Zapisz i Zamknij", command=self._on_save).pack(side=tk.RIGHT)
        ttk.Button(btn_frame, text="Anuluj", command=self.destroy).pack(side=tk.RIGHT, padx=5)
        
        self._refresh_list()
        
    def _add_row(self):
        """Dodaje wiersz mapowania na podstawie wybranych wartości w comboboxach."""
        row_indices = []
        has_any = False
        
        for i, cb in enumerate(self.combos):
            val = cb.get()
            if val:
                try:
                    idx = self.display_values[i].index(val)
                    row_indices.append(idx)
                    has_any = True
                except ValueError:
                    row_indices.append(None)
            else:
                row_indices.append(None)
                
        if has_any:
            self.mapping.append(tuple(row_indices))
            self._refresh_list()
            
    def _remove_row(self):
        """Usuwa zaznaczone wiersze."""
        sel = self.tree.selection()
        indices = []
        for item in sel:
            try:
                idx_in_list = int(self.tree.item(item, "values")[0]) - 1
                indices.append(idx_in_list)
            except:
                pass
            
        indices.sort(reverse=True)
        for idx in indices:
            if 0 <= idx < len(self.mapping):
                self.mapping.pop(idx)
        self._refresh_list()

    def _clear_all(self):
        """Czyści wszystkie mapowania."""
        self.mapping = []
        self._refresh_list()
        
    def _auto_map(self):
        """Automatycznie mapuje wizyty sekwencyjnie po indeksie."""
        self.mapping = []
        max_len = max((len(d) for d in self.display_values), default=0)
        
        for i in range(max_len):
            row = [i if i < len(d_list) else None for d_list in self.display_values]
            self.mapping.append(tuple(row))
        self._refresh_list()
        
    def _refresh_list(self):
        """Odświeża widok listy mapowań."""
        for item in self.tree.get_children():
            self.tree.delete(item)
            
        for r_idx, idx_tuple in enumerate(self.mapping):
            row_vals = [r_idx + 1]
            for sess_i, visit_idx in enumerate(idx_tuple):
                if visit_idx is not None and visit_idx < len(self.display_values[sess_i]):
                    row_vals.append(self.display_values[sess_i][visit_idx])
                else:
                    row_vals.append("-")
            self.tree.insert("", tk.END, values=row_vals)
            
    def _on_save(self):
        """Zapisuje mapowanie i zamyka dialog."""
        self.callback(self.mapping)
        self.destroy()


class ComparisonTab:
    """
    Orchestrator zakładki porównania sesji.
    
    Deleguje renderowanie do:
    - ChartRenderer (wykresy słupkowe)
    - GraphRenderer (graf przejść)
    - TableRenderer (tabela metryk)
    """
    
    def __init__(self, parent, status_callback=None):
        self.parent = parent
        self.status_callback = status_callback
        
        # Dane sesji
        self.sessions = []
        self.mapped_pairs = None
        
        # Tworzenie głównej ramki
        self.frame = ttk.Frame(parent)
        parent.add(self.frame, text="Porównanie Sesji")
        
        # Renderery (inicjalizowane w _create_ui)
        self.chart_renderer = None
        self.graph_renderer = None
        self.table_renderer = None
        
        self._create_ui()
        
    def _create_ui(self):
        """Buduje interfejs użytkownika zakładki (Layout: Sidebar + Content)."""
        # Główny kontener z podziałem poziomym
        self.paned = ttk.PanedWindow(self.frame, orient=tk.HORIZONTAL)
        self.paned.pack(fill=tk.BOTH, expand=True, padx=5, pady=5)
        
        # 1. Sidebar (Lewa strona)
        self.sidebar = ttk.Frame(self.paned, width=300, padding=10)
        self.paned.add(self.sidebar, weight=0) # weight=0 aby nie rozciągał się nadmiernie
        
        # 2. Content (Prawa strona)
        self.content_area = ttk.Frame(self.paned, padding=5)
        self.paned.add(self.content_area, weight=4)
        
        self._build_sidebar()
        self._build_content_area()
        
    def _build_sidebar(self):
        """Tworzy zawartość paska bocznego (Notebook)."""
        # Główny Notebook Sidebaru
        self.sidebar_notebook = ttk.Notebook(self.sidebar)
        self.sidebar_notebook.pack(fill=tk.BOTH, expand=True, pady=5)
        
        # Tab 1: Sesje (Zarządzanie + Akcje)
        self.tab_sessions = ttk.Frame(self.sidebar_notebook, padding=5)
        self.sidebar_notebook.add(self.tab_sessions, text="Sesje")
        self._build_sessions_tab(self.tab_sessions)
        
        # Tab 2: Wykresy (Konfiguracja)
        self.tab_charts_config = ttk.Frame(self.sidebar_notebook, padding=5)
        self.sidebar_notebook.add(self.tab_charts_config, text="Wykresy")
        self._build_charts_config(self.tab_charts_config)
        
        # Tab 3: Graf (Konfiguracja)
        self.tab_graph_config = ttk.Frame(self.sidebar_notebook, padding=5)
        self.sidebar_notebook.add(self.tab_graph_config, text="Graf")
        self._build_graph_config(self.tab_graph_config)
        
        # Tab 4: Dane (Konfiguracja)
        self.tab_table_config = ttk.Frame(self.sidebar_notebook, padding=5)
        self.sidebar_notebook.add(self.tab_table_config, text="Dane")
        self._build_table_config(self.tab_table_config)

    def _build_sessions_tab(self, parent):
        """Buduje zawartość zakładki 'Sesje'."""
        # Lista sesji
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
        
        ttk.Button(btn_frame, text="+", width=3, command=self._add_session).pack(side=tk.LEFT, padx=(0, 2))
        ttk.Button(btn_frame, text="-", width=3, command=self._remove_session).pack(side=tk.LEFT, padx=2)
        ttk.Button(btn_frame, text="Wyczyść", command=self._clear_sessions).pack(side=tk.LEFT, fill=tk.X, expand=True, padx=(2, 0))
        
        ttk.Separator(parent, orient=tk.HORIZONTAL).pack(fill=tk.X, pady=10)
        
        # Akcje
        ttk.Button(parent, text="🛠 Mapowanie", command=self._open_mapping_dialog).pack(fill=tk.X, pady=5)
        
        # Główny przycisk
        btn_compare = ttk.Button(parent, text="▶ PORÓWNAJ", command=self._perform_comparison)
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
        self.combo_chart1.bind("<<ComboboxSelected>>", self._on_chart_config_change)

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
        self.combo_chart2.bind("<<ComboboxSelected>>", self._on_chart_config_change)
        
    def _build_graph_config(self, parent):
        """Konfiguracja dla grafu."""
        ttk.Label(parent, text="Układ węzłów:").pack(anchor=tk.W)
        self.combo_layout = ttk.Combobox(parent, state="readonly", values=["Spring", "Circular", "Shell", "Kamada-Kawai"])
        self.combo_layout.current(0)
        self.combo_layout.pack(fill=tk.X, pady=(0, 5))
        self.combo_layout.bind("<<ComboboxSelected>>", self._on_graph_config_change)
        
        self.var_show_order = tk.BooleanVar(value=True)
        cb = ttk.Checkbutton(parent, text="Pokaż kolejność przejść", variable=self.var_show_order, command=self._on_graph_config_change)
        cb.pack(anchor=tk.W, pady=(5, 0))
        
    def _build_table_config(self, parent):
        """Konfiguracja dla tabeli."""
        # Usunięto kolumnę zakres, brak konfiguracji w tym momencie
        ttk.Label(parent, text="Styl tabeli został zaktualizowany dla lepszej czytelności.", wraplength=180).pack(anchor=tk.W)

    def _build_content_area(self):
        """Tworzy obszar zawartości z zakładkami."""
        # Notebook z zakładkami wizualizacji
        self.content_notebook = ttk.Notebook(self.content_area)
        self.content_notebook.pack(fill=tk.BOTH, expand=True)
        
        # Zakładka 1: Wykresy
        charts_frame = ttk.Frame(self.content_notebook)
        self.content_notebook.add(charts_frame, text=" 📊 Wykresy ")
        self.chart_renderer = ChartRenderer(charts_frame)
        
        # Zakładka 2: Graf
        graph_frame = ttk.Frame(self.content_notebook)
        self.content_notebook.add(graph_frame, text=" 🕸 Graf Przejść ")
        self.graph_renderer = GraphRenderer(graph_frame)
        
        # Zakładka 3: Tabela
        table_frame = ttk.Frame(self.content_notebook)
        self.content_notebook.add(table_frame, text=" 📋 Dane Szczegółowe ")
        self.table_renderer = TableRenderer(table_frame)
        
        # Wiadomość początkowa
        self._show_initial_message(table_frame)
        
    def _show_initial_message(self, frame):
        """Wyświetla wiadomość początkową w zakładce tabeli."""
        msg_label = ttk.Label(
            frame,
            text="Dodaj co najmniej jedną sesję i kliknij 'Porównaj'.",
            font=FONTS.get("NORMAL", ("Arial", 10))
        )
        msg_label.pack(expand=True)

    # --- Zarządzanie sesjami ---
    
    def _add_session(self):
        """Dodaje sesję z pliku JSON."""
        initial_dir = config.get_default_json_dir()
        path = filedialog.askopenfilename(
            title="Wybierz plik sesji",
            initialdir=initial_dir,
            filetypes=[("Pliki JSON", "*.json"), ("Wszystkie pliki", "*.*")]
        )
        
        if not path:
            return
            
        try:
            dm = DataManager()
            data = dm.load_from_file(path)
            raw_sessions = data.get("sessions", [])
            
            if not raw_sessions:
                raise ValueError("Brak sesji w pliku")
                
            session_data = raw_sessions[0]
            metrics = calculate_session_metrics(session_data)
            
            filename = os.path.basename(path)
            self.sessions.append({
                "file": filename,
                "data": session_data,
                "metrics": metrics
            })
            
            self._refresh_session_list()
            self.mapped_pairs = None
            
        except Exception as e:
            messagebox.showerror("Błąd", f"Nie udało się wczytać: {e}")
            
    def _remove_session(self):
        """Usuwa wybraną sesję z listy."""
        sel = self.session_tree.selection()
        if not sel:
            return
            
        idx = int(self.session_tree.item(sel[0], "values")[0]) - 1
        if 0 <= idx < len(self.sessions):
            self.sessions.pop(idx)
            self._refresh_session_list()
            self.mapped_pairs = None

    def _clear_sessions(self):
        """Czyści wszystkie sesje."""
        self.sessions = []
        self._refresh_session_list()
        self.mapped_pairs = None
        
    def _refresh_session_list(self):
        """Odświeża widok listy sesji."""
        for item in self.session_tree.get_children():
            self.session_tree.delete(item)
            
        for i, s in enumerate(self.sessions):
            dur = f"{s['metrics']['duration']:.1f}s"
            self.session_tree.insert("", tk.END, values=(i+1, s["file"], dur))
            
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

    def _on_chart_config_change(self, event=None):
        """Obsługuje zmianę konfiguracji wykresów."""
        if hasattr(self, "_last_aggregated_data") and self._last_aggregated_data:
            self._update_charts_only()

    def _on_graph_config_change(self, event=None):
        """Obsługuje zmianę konfiguracji grafu."""
        if hasattr(self, "_last_aggregated_data") and self._last_aggregated_data:
            self._update_graph_only()

    def _on_table_config_change(self):
        """Obsługuje zmianę konfiguracji tabeli."""
        if hasattr(self, "_last_aggregated_data") and self._last_aggregated_data:
            self._update_table_only()

    def _update_charts_only(self):
        """Aktualizuje tylko renderer wykresów."""
        config = {
            "chart1": self.combo_chart1.get(),
            "chart2": self.combo_chart2.get()
        }
        self.chart_renderer.update(self._last_aggregated_data, config)

    def _update_graph_only(self):
        """Aktualizuje tylko renderer grafu."""
        config = {
            "layout": self.combo_layout.get(),
            "show_order": self.var_show_order.get()
        }
        self.graph_renderer.update(self.sessions, self._last_aggregated_data, config)

    def _update_table_only(self):
        """Aktualizuje tylko renderer tabeli."""
        self.table_renderer.update(self._last_aggregated_data)

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
            "chart1": self.combo_chart1.get(),
            "chart2": self.combo_chart2.get()
        }
        graph_config = {
            "layout": self.combo_layout.get(),
            "show_order": self.var_show_order.get()
        }
        
        # Aktualizacja rendererów
        self.chart_renderer.update(aggregated_data, chart_config)
        self.graph_renderer.update(self.sessions, aggregated_data, graph_config)
        self.table_renderer.update(aggregated_data)
