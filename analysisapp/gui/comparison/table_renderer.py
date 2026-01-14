"""
Table Renderer

Odpowiada za rysowanie tabeli danych szczegółowych w zakładce porównania sesji.
"""

import tkinter as tk
from tkinter import ttk


class TableRenderer:
    """Renderuje tabelę Treeview z metrykami i wizytami."""
    
    def __init__(self, parent_frame):
        """
        Args:
            parent_frame: Ramka Tkinter, w której osadzona będzie tabela.
        """
        self.parent_frame = parent_frame
        
    def update(self, aggregated_data: dict, config: dict = None):
        """
        Aktualizuje tabelę na podstawie zagregowanych danych.
        
        Args:
            aggregated_data: Słownik z danymi.
            config: Nieużywane (zachowane dla kompatybilności API).
        """
        # Konfiguracja stylu
        style = ttk.Style()
        style.configure("Detailed.Treeview", rowheight=30, font=("Arial", 10))
        style.configure("Detailed.Treeview.Heading", font=("Arial", 10, "bold"), background="#d0d0d0")
        
        # Czyszczenie poprzedniej zawartości
        for widget in self.parent_frame.winfo_children():
            widget.destroy()
            
        num_sessions = aggregated_data["meta"]["count"]
        names = aggregated_data["meta"]["names"]
        
        # Tworzenie kolumn (bez Spread)
        cols = ["metric"] + [f"s_{i}" for i in range(num_sessions)]
            
        tree = ttk.Treeview(self.parent_frame, columns=cols, show="headings", style="Detailed.Treeview")
        
        tree.heading("metric", text="Wskaźnik")
        tree.column("metric", width=300, anchor=tk.W)
        
        for i, name in enumerate(names):
            col_id = f"s_{i}"
            tree.heading(col_id, text=name)
            tree.column(col_id, width=120, anchor=tk.CENTER)
            
        # Paski (Striped Rows)
        tree.tag_configure("odd", background="#f9f9f9")
        tree.tag_configure("even", background="#ffffff")
        tree.tag_configure("header_row", background="#e1e1e1", font=("Arial", 10, "bold"))
        tree.tag_configure("sub_header", background="#f0f0f0", font=("Arial", 10, "italic"))
        
        # Scrollbar
        scrollbar = ttk.Scrollbar(self.parent_frame, orient=tk.VERTICAL, command=tree.yview)
        tree.configure(yscroll=scrollbar.set)
        
        tree.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)
        scrollbar.pack(side=tk.RIGHT, fill=tk.Y)
        
        # Licznik wierszy do paskowania
        self._row_counter = 0
        
        # Wstawianie danych
        self._insert_section(tree, aggregated_data["advanced"], "ZAAWANSOWANE", 
                            ["exploration_pace", "event_density", "avg_book_time"], 
                            float_fmt="{:.2f}")
        
        self._insert_section(tree, aggregated_data["summary"], "PODSUMOWANIE", 
                            ["duration", "total_rooms", "total_books_opened"], 
                            float_fmt="{:.1f}")
        
        self._insert_visits_section(tree, aggregated_data, num_sessions)
        
    # Mapowanie nazw metryk na polski
    METRIC_NAMES_PL = {
        "exploration_pace": "Tempo eksploracji (pokoje/min)",
        "event_density": "Gęstość zdarzeń (akcje/min)",
        "avg_book_time": "Śr. czas z książką (s)",
        "duration": "Całkowity czas (s)",
        "total_rooms": "Liczba odwiedzonych pokoi",
        "total_books_opened": "Liczba otwarć książek"
    }

    def _insert_section(self, tree, category_data: dict, title: str, keys: list, float_fmt: str = "{:.2f}"):
        """Wstawia sekcję metryk do tabeli."""
        tree.insert("", tk.END, values=[title], tags=("header_row",))
        
        for key in keys:
            if key not in category_data:
                continue
                
            row_data = category_data[key]
            vals = [float_fmt.format(v) for v in row_data["values"]]
            
            # Tłumaczenie klucza
            label = self.METRIC_NAMES_PL.get(key, key)
            row_vals = [f"  {label}"] + vals
            
            tag = "even" if self._row_counter % 2 == 0 else "odd"
            tree.insert("", tk.END, values=row_vals, tags=(tag,))
            self._row_counter += 1
            
    def _insert_visits_section(self, tree, aggregated_data: dict, num_sessions: int):
        """Wstawia sekcję przebiegu wizyt do tabeli."""
        tree.insert("", tk.END, values=["PRZEBIEG (WIZYTY)"], tags=("header_row",))
        
        visits_data = aggregated_data.get("visits_comparison", {})
        
        for key, data in visits_data.items():
            label = data["label"]
            tree.insert("", tk.END, values=[f"Pokój: {label}"], tags=("sub_header",))
            
            # Czas trwania
            tag = "even" if self._row_counter % 2 == 0 else "odd"
            duration_vals = [f"{v:.1f}" for v in data["duration"]["values"]]
            row_dur = ["  Czas (s)"] + duration_vals
            tree.insert("", tk.END, values=row_dur, tags=(tag,))
            self._row_counter += 1
            
            # Książki
            tag = "even" if self._row_counter % 2 == 0 else "odd"
            books_vals = [f"{v}" for v in data["books_opened"]["values"]]
            row_books = ["  Książki"] + books_vals
            tree.insert("", tk.END, values=row_books, tags=(tag,))
            self._row_counter += 1
