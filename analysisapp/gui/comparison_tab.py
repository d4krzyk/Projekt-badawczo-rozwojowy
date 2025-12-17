"""
Comparison Tab

Tab component for comparing multiple sessions with advanced visualization.
"""

import os
import tkinter as tk
from tkinter import ttk, filedialog, messagebox
import matplotlib
matplotlib.use('TkAgg')
from matplotlib.figure import Figure
from matplotlib.backends.backend_tkagg import FigureCanvasTkAgg
import numpy as np

from ..analyzers.session.core import load_json
from ..analyzers.session.comparison import calculate_session_metrics, aggregate_metrics_multi
from ..app_config import config
from .styles import FONTS
from .session.data_manager import DataManager

class RoomMappingDialog(tk.Toplevel):
    """Dialog for configuring visit mapping pairs (N-way) based on visit sequence."""
    def __init__(self, parent, session_names, sessions_visits, current_mapping, callback):
        super().__init__(parent)
        self.title("Konfiguracja Mapowania Wizyt (Przebieg)")
        self.geometry("900x550")
        self.transient(parent)
        self.grab_set()
        
        self.session_names = session_names
        self.sessions_visits_raw = sessions_visits # List of lists of visit dicts
        
        # Prepare display strings: "1. Kitchen", "2. Salon", etc.
        self.display_values = []
        for s_idx, visits in enumerate(sessions_visits):
            vals = []
            for i, v in enumerate(visits):
                vals.append(f"{i+1}. {v['name']}")
            self.display_values.append(vals)
            
        self.mapping = list(current_mapping) if current_mapping else []
        self.callback = callback
        self.combos = []
        
        self._create_ui()
        # center
        self.update_idletasks()
        x = parent.winfo_rootx() + (parent.winfo_width() - self.winfo_width()) // 2
        y = parent.winfo_rooty() + (parent.winfo_height() - self.winfo_height()) // 2
        self.geometry(f"+{x}+{y}")
        
    def _create_ui(self):
        # 1. Pairing Controls
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
        
        # 2. List
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
        
        # 3. Actions
        btn_frame = ttk.Frame(self)
        btn_frame.pack(fill=tk.X, padx=10, pady=10)
        
        ttk.Button(btn_frame, text="Usuń zaznaczone", command=self._remove_row).pack(side=tk.LEFT)
        ttk.Button(btn_frame, text="Auto (Po Kolei)", command=self._auto_map).pack(side=tk.LEFT, padx=10)
        ttk.Button(btn_frame, text="Wyczyść", command=self._clear_all).pack(side=tk.LEFT)
        
        ttk.Button(btn_frame, text="Zapisz i Zamknij", command=self._on_save).pack(side=tk.RIGHT)
        ttk.Button(btn_frame, text="Anuluj", command=self.destroy).pack(side=tk.RIGHT, padx=5)
        
        self._refresh_list()
        
    def _add_row(self):
        # We need to store INDICES (integers), but Combo has strings "1. Name"
        # We find index in display_values
        
        row_indices = []
        has_any = False
        
        for i, cb in enumerate(self.combos):
            val = cb.get()
            if val:
                # Find index of this string in self.display_values[i]
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
        sel = self.tree.selection()
        indices = []
        for item in sel:
            try:
                # idx column is 1-based index in table
                idx_in_list = int(self.tree.item(item, "values")[0]) - 1
                indices.append(idx_in_list)
            except: pass
            
        indices.sort(reverse=True)
        for idx in indices:
            if 0 <= idx < len(self.mapping):
                self.mapping.pop(idx)
        self._refresh_list()

    def _clear_all(self):
        self.mapping = []
        self._refresh_list()
        
    def _auto_map(self):
        # Sequential mapping by index
        self.mapping = []
        max_len = max((len(d) for d in self.display_values), default=0)
        
        for i in range(max_len):
            row = []
            for d_list in self.display_values:
                if i < len(d_list):
                    row.append(i)
                else:
                    row.append(None)
            self.mapping.append(tuple(row))
        self._refresh_list()
        
    def _refresh_list(self):
        for item in self.tree.get_children():
            self.tree.delete(item)
            
        for r_idx, idx_tuple in enumerate(self.mapping):
            # Convert indices back to strings for display
            row_vals = [r_idx+1]
            for sess_i, visit_idx in enumerate(idx_tuple):
                if visit_idx is not None and visit_idx < len(self.display_values[sess_i]):
                    row_vals.append(self.display_values[sess_i][visit_idx])
                else:
                    row_vals.append("-")
            self.tree.insert("", tk.END, values=row_vals)
            
    def _on_save(self):
        self.callback(self.mapping)
        self.destroy()

class ComparisonTab:
    """Session comparison tab component with charts and advanced metrics (Multi-Session)."""
    
    def __init__(self, parent, status_callback=None):
        self.parent = parent
        self.status_callback = status_callback
        
        # Data
        self.sessions = []
        self.mapped_pairs = None # List of tuples of INDICES (int)
        
        # Create tab frame
        self.frame = ttk.Frame(parent)
        parent.add(self.frame, text="Porównanie Sesji")
        
        self._create_ui()
        
    def _create_ui(self):
        # Main Layout: Top Control, Content
        
        # 1. Control Panel
        control_panel = ttk.LabelFrame(self.frame, text="Zarządzanie Sesjami")
        control_panel.pack(fill=tk.X, padx=5, pady=5)
        
        # Left: Session List
        list_frame = ttk.Frame(control_panel)
        list_frame.pack(side=tk.LEFT, fill=tk.X, expand=True, padx=5, pady=5)
        
        self.session_tree = ttk.Treeview(list_frame, columns=("idx", "file", "duration"), show="headings", height=4)
        self.session_tree.heading("idx", text="#")
        self.session_tree.heading("file", text="Plik")
        self.session_tree.heading("duration", text="Czas trwania")
        self.session_tree.column("idx", width=30)
        self.session_tree.column("file", width=200)
        self.session_tree.column("duration", width=100)
        
        self.session_tree.pack(side=tk.LEFT, fill=tk.X, expand=True)
        
        btn_box = ttk.Frame(control_panel)
        btn_box.pack(side=tk.RIGHT, padx=10, fill=tk.Y)
        
        ttk.Button(btn_box, text="Dodaj Sesję", command=self._add_session).pack(fill=tk.X, pady=2)
        ttk.Button(btn_box, text="Usuń Wybraną", command=self._remove_session).pack(fill=tk.X, pady=2)
        ttk.Button(btn_box, text="Wyczyść Wszystkie", command=self._clear_sessions).pack(fill=tk.X, pady=2)
        
        ttk.Separator(control_panel, orient=tk.VERTICAL).pack(side=tk.RIGHT, fill=tk.Y, padx=10)
        
        # Action Buttons
        act_box = ttk.Frame(control_panel)
        act_box.pack(side=tk.RIGHT, padx=10)
        
        ttk.Button(act_box, text="Konfiguruj Mapowanie Wizyt", command=self._open_mapping_dialog).pack(fill=tk.X, pady=2)
        ttk.Button(act_box, text="PORÓWNAJ", command=self._perform_comparison).pack(fill=tk.X, pady=5)
        
        # 2. Content Area (Tabs)
        self.content_notebook = ttk.Notebook(self.frame)
        self.content_notebook.pack(fill=tk.BOTH, expand=True, padx=5, pady=5)
        
        # Tab 1: Charts
        self.charts_frame = ttk.Frame(self.content_notebook)
        self.content_notebook.add(self.charts_frame, text="Wykresy")
        
        # Tab 2: Graph
        self.graph_frame = ttk.Frame(self.content_notebook)
        self.content_notebook.add(self.graph_frame, text="Graf Przejść")
        
        # Tab 3: Metrics Table
        self.table_frame = ttk.Frame(self.content_notebook)
        self.content_notebook.add(self.table_frame, text="Dane Szczegółowe")
        
        # Initialize Chart
        self._init_charts()
        self._init_graph()
        
        # Initial Message
        self.msg_label = ttk.Label(self.table_frame, 
                       text="Dodaj co najmniej jedną sesję i kliknij 'Porównaj'.",
                       font=FONTS["NORMAL"])
        self.msg_label.pack(expand=True)
        
    def _init_charts(self):
        self.fig = Figure(figsize=(10, 6), dpi=100)
        self.ax1 = self.fig.add_subplot(121) 
        self.ax2 = self.fig.add_subplot(122) 
        self.fig.tight_layout(pad=3.0)
        self.canvas = FigureCanvasTkAgg(self.fig, master=self.charts_frame)
        self.canvas.draw()
        self.canvas.get_tk_widget().pack(fill=tk.BOTH, expand=True)
        
    def _init_graph(self):
        self.graph_fig = Figure(figsize=(8, 6), dpi=100)
        self.ax_graph = self.graph_fig.add_subplot(111)
        self.graph_canvas = FigureCanvasTkAgg(self.graph_fig, master=self.graph_frame)
        self.graph_canvas.draw()
        self.graph_canvas.get_tk_widget().pack(fill=tk.BOTH, expand=True)

    def _add_session(self):
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
            
            # Add to list
            filename = os.path.basename(path)
            self.sessions.append({
                "file": filename,
                "data": session_data,
                "metrics": metrics
            })
            
            self._refresh_session_list()
            # Invalidate map
            self.mapped_pairs = None
            
        except Exception as e:
            messagebox.showerror("Błąd", f"Nie udało się wczytać: {e}")
            
    def _remove_session(self):
        sel = self.session_tree.selection()
        if not sel:
            return
        # Remove by index
        idx = int(self.session_tree.item(sel[0], "values")[0]) - 1
        if 0 <= idx < len(self.sessions):
            self.sessions.pop(idx)
            self._refresh_session_list()
            self.mapped_pairs = None

    def _clear_sessions(self):
        self.sessions = []
        self._refresh_session_list()
        self.mapped_pairs = None
        
    def _refresh_session_list(self):
        for item in self.session_tree.get_children():
            self.session_tree.delete(item)
        for i, s in enumerate(self.sessions):
            dur = f"{s['metrics']['duration']:.1f}s"
            self.session_tree.insert("", tk.END, values=(i+1, s["file"], dur))
            
    def _open_mapping_dialog(self):
        if not self.sessions:
            messagebox.showwarning("Uwaga", "Brak sesji.")
            return

        names = [s["file"] for s in self.sessions]
        # Pass list of visit dicts to dialog
        visits_list = [s["metrics"].get("visits", []) for s in self.sessions]
        
        RoomMappingDialog(self.frame, names, visits_list, self.mapped_pairs, self._set_mapping)
        
    def _set_mapping(self, mapping):
        self.mapped_pairs = mapping
        if self.status_callback:
            self.status_callback(f"Zaktualizowano mapowanie ({len(mapping)} wierszy).")
            
    def _perform_comparison(self):
        if not self.sessions:
            return
            
        # Prepare list for backend
        sessions_input = []
        for s in self.sessions:
            sessions_input.append((s["file"], s["metrics"]))
            
        agg = aggregate_metrics_multi(sessions_input, room_mapping=self.mapped_pairs)
        
        self._update_charts(agg)
        self._update_graph(agg)  # Call graph update
        self._update_table(agg)
        
    def _update_graph(self, agg):
        self.ax_graph.clear()
        
        if not self.sessions:
            return
            
        import networkx as nx
        
        G = nx.MultiDiGraph()
        
        num_sessions = agg["meta"]["count"]
        # names = agg["meta"]["names"]
        
        cmap = matplotlib.cm.get_cmap('tab10')
        colors = [cmap(i) for i in range(num_sessions)]
        
        # Build Edges
        # Iterate over each session's visits to finding transitions
        edge_colors = []
        
        any_nodes = False
        
        for i, s in enumerate(self.sessions):
            visits = s["metrics"].get("visits", [])
            color = colors[i % 10]
            
            prev_node = None
            
            for v in visits:
                curr_node = v["name"]
                G.add_node(curr_node)
                any_nodes = True
                
                if prev_node:
                     # Add edge
                     # Key is useful to distinguish multiple edges, logic handles MultiDiGraph automatically
                     # We store color as attribute to retrieve later? 
                     # Drawing in NetworkX with variable edge colors is a bit manual.
                     pass
                     
                prev_node = curr_node
                
        # To draw with colors properly:
        # We will iterate again and draw edges one by one or collects lists
        
        pos = nx.spring_layout(G, seed=42) # Consistent layout
        if not any_nodes:
            self.ax_graph.text(0.5, 0.5, "Brak danych do grafu", ha='center')
            self.graph_canvas.draw()
            return
            
        # Draw all nodes
        nx.draw_networkx_nodes(G, pos, ax=self.ax_graph, node_color='lightgray', node_size=500)
        nx.draw_networkx_labels(G, pos, ax=self.ax_graph, font_size=8)
        
        # Prepare legend handles manually
        legend_handles = []
        from matplotlib.lines import Line2D
        
        for i, s in enumerate(self.sessions):
            visits = s["metrics"].get("visits", [])
            color = colors[i % 10]
            name = self.sessions[i]["file"][:20] # truncate if long
            
            # Add proxy handle for legend
            legend_handles.append(Line2D([0], [0], color=color, lw=2, label=name))
            
            edges_list = []
            prev_node = None
            for v in visits:
                curr_node = v["name"]
                if prev_node:
                    edges_list.append((prev_node, curr_node))
                prev_node = curr_node
                
            if edges_list:
                # rad = 0.1 + (i * 0.1) # Shift curves
                nx.draw_networkx_edges(G, pos, ax=self.ax_graph, 
                                       edgelist=edges_list, 
                                       edge_color=[color], 
                                       connectionstyle=f"arc3,rad={0.1 + 0.1*i}",
                                       arrowstyle='-|>', arrowsize=15)
                                       
        self.ax_graph.set_title("Graf Przejść (Kolory = Sesje)")
        self.ax_graph.legend(handles=legend_handles)
        # Avoid strict tight_layout which warns if margins are tight
        self.graph_fig.subplots_adjust(left=0.05, right=0.95, top=0.9, bottom=0.05)
        self.graph_canvas.draw()
        
    def _update_charts(self, agg):
        self.ax1.clear()
        self.ax2.clear()
        
        num_sessions = agg["meta"]["count"]
        names = agg["meta"]["names"]
        cmap = matplotlib.cm.get_cmap('tab10')
        colors = [cmap(i) for i in range(num_sessions)]
        
        # 1. VISITS Duration Chart
        # Use agg["visits_comparison"]
        # Note: Keys are row IDs e.g. "row_0_Kitchen vs Salon"
        # We need to sort them by chronological row index roughly? 
        # The dict keys insertion order is usually preserved in recent python, so we rely on that or row index.
        
        visits_data = agg.get("visits_comparison", {})
        if not visits_data:
             # Fallback if empty?
             pass
             
        # Create labels
        visit_labels = []
        rows = []
        for k, v in visits_data.items():
            rows.append(v)
            visit_labels.append(v["label"]) # e.g. "1. Kitchen"
            
        x = np.arange(len(rows))
        bar_width = 0.8 / num_sessions
        
        for i in range(num_sessions):
            vals = []
            for r_data in rows:
                vals.append(r_data["duration"]["values"][i])
            
            offset = (i - num_sessions/2 + 0.5) * bar_width
            self.ax1.bar(x + offset, vals, bar_width, label=names[i], color=colors[i % 10])
            
        self.ax1.set_title("Czas trwania wizyt (s)")
        self.ax1.set_xticks(x)
        
        # Short labels
        short = [r[:15]+"..." if len(r)>18 else r for r in visit_labels]
        self.ax1.set_xticklabels(short, rotation=45, ha='right')
        
        # 2. Activity Chart (Global Stats)
        metrics = ["total_books_opened", "total_links_clicked", "unique_rooms_count"]
        metric_labels = ["Książki (Razem)", "Linki (Razem)", "Unikalne Pokoje"]
        
        x2 = np.arange(len(metrics))
        
        for i in range(num_sessions):
            vals = []
            vals.append(agg["summary"]["total_books_opened"]["values"][i])
            vals.append(agg["summary"]["total_links_clicked"]["values"][i])
            vals.append(agg["advanced"]["unique_rooms_count"]["values"][i])
            
            offset = (i - num_sessions/2 + 0.5) * bar_width
            self.ax2.bar(x2 + offset, vals, bar_width, label=names[i], color=colors[i % 10])
            
        self.ax2.set_title("Podsumowanie Aktywności")
        self.ax2.set_xticks(x2)
        self.ax2.set_xticklabels(metric_labels)
        self.ax2.legend()
        
        self.fig.tight_layout()
        self.canvas.draw()
        
    def _update_table(self, agg):
        for w in self.table_frame.winfo_children():
            w.destroy()
            
        num_sessions = agg["meta"]["count"]
        names = agg["meta"]["names"]
        
        cols = ["metric"] + [f"s_{i}" for i in range(num_sessions)] + ["spread"]
        tree = ttk.Treeview(self.table_frame, columns=cols, show="headings")
        
        tree.heading("metric", text="Wskaźnik / Wizyta")
        tree.column("metric", width=250)
        
        for i, n in enumerate(names):
            cid = f"s_{i}"
            tree.heading(cid, text=n)
            tree.column(cid, width=80, anchor=tk.CENTER)
            
        tree.heading("spread", text="Zakres")
        tree.column("spread", width=100, anchor=tk.CENTER)
        
        scr = ttk.Scrollbar(self.table_frame, orient=tk.VERTICAL, command=tree.yview)
        tree.configure(yscroll=scr.set)
        
        tree.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)
        scr.pack(side=tk.RIGHT, fill=tk.Y)
        
        # Insert Data
        # Advanced
        self._insert_section(tree, agg["advanced"], "ZAAWANSOWANE", ["exploration_pace", "event_density", "avg_book_time"], float_fmt="{:.2f}")
        
        # Summary
        self._insert_section(tree, agg["summary"], "PODSUMOWANIE", ["duration", "total_rooms", "total_books_opened"], float_fmt="{:.1f}")
        
        # Visits Sequence
        tree.insert("", tk.END, values=["-- PRZEBIEG (WIZYTY) --"] + [""]*(num_sessions+1), tags=("header",))
        tree.tag_configure("header", background="#e1e1e1")
        
        visits_data = agg.get("visits_comparison", {})
        
        # Iterate over stored keys (usually valid order)
        for key, data in visits_data.items():
            label = data["label"]
            tree.insert("", tk.END, values=[f"[{label}]"] + [""]*(num_sessions+1), tags=("header",))
            
            d_vals = [f"{v:.1f}" for v in data["duration"]["values"]]
            d_spread = f"{data['duration']['spread']:.1f}"
            tree.insert("", tk.END, values=["  Czas (s)"] + d_vals + [d_spread])
            
            b_vals = [f"{v}" for v in data["books_opened"]["values"]]
            b_spread = f"{data['books_opened']['spread']}"
            tree.insert("", tk.END, values=["  Książki"] + b_vals + [b_spread])
            
    def _insert_section(self, tree, category_data, title, keys, float_fmt="{:.2f}"):
        tree.insert("", tk.END, values=[title], tags=("header",))
        for k in keys:
            if k not in category_data: continue
            
            row_data = category_data[k]
            vals = [float_fmt.format(v) for v in row_data["values"]]
            try:
                spr = float_fmt.format(row_data["spread"])
            except:
                spr = "-"
            
            tree.insert("", tk.END, values=[k] + vals + [spr])
