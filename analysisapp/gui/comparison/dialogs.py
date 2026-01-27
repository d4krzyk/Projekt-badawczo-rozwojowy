"""
Room Mapping Dialog

Dialog for configuring visit mapping pairs (N-way) based on visit sequence.
"""

import tkinter as tk
from tkinter import ttk


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
