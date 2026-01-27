"""
Graph Renderer

Odpowiada za rysowanie grafu przejść (NetworkX) w zakładce porównania sesji.
"""

import matplotlib
from matplotlib.figure import Figure
from matplotlib.backends.backend_tkagg import FigureCanvasTkAgg
from matplotlib.lines import Line2D


class GraphRenderer:
    """Renderuje graf przejść między pokojami na płótnie Matplotlib."""
    
    def __init__(self, parent_frame):
        """
        Args:
            parent_frame: Ramka Tkinter, w której osadzony będzie graf.
        """
        self.parent_frame = parent_frame
        self.fig = None
        self.ax = None
        self.canvas = None
        
        self._init_figure()
        
    def _init_figure(self):
        """Inicjalizuje figurę Matplotlib."""
        self.fig = Figure(figsize=(8, 6), dpi=100)
        self.ax = self.fig.add_subplot(111)
        
        self.canvas = FigureCanvasTkAgg(self.fig, master=self.parent_frame)
        self.canvas.draw()
        self.canvas.get_tk_widget().pack(fill='both', expand=True)
        
    def update(self, sessions: list, aggregated_data: dict, config: dict = None):
        """
        Aktualizuje graf na podstawie sesji i zagregowanych danych.
        
        Args:
            sessions: Lista słowników sesji.
            aggregated_data: Słownik z metadanymi.
            config: Opcjonalna konfiguracja (np. {'layout': 'Circular'}).
        """
        self.ax.clear()
        
        if not sessions:
            self.ax.text(0.5, 0.5, "Brak danych do grafu", ha='center', transform=self.ax.transAxes)
            self.canvas.draw()
            return
            
        import networkx as nx
        
        G = nx.MultiDiGraph()
        
        num_sessions = aggregated_data["meta"]["count"]
        cmap = matplotlib.cm.get_cmap('tab10')
        colors = [cmap(i) for i in range(num_sessions)]
        
        # Zbieranie węzłów
        any_nodes = False
        for s in sessions:
            for v in s["metrics"].get("visits", []):
                G.add_node(v["name"])
                any_nodes = True
                
        if not any_nodes:
            self.ax.text(0.5, 0.5, "Brak danych do grafu", ha='center', transform=self.ax.transAxes)
            self.canvas.draw()
            return
            
        # Wymuszenie proporcji 1:1, aby obliczenia geometryczne (offsety normalne) były poprawne wizualnie
        self.ax.set_aspect('equal')
            
        # Wybór algorytmu układu (Layout)
        layout_name = config.get("layout", "Spring") if config else "Spring"
        
        if layout_name == "Circular":
            pos = nx.circular_layout(G)
        elif layout_name == "Shell":
            pos = nx.shell_layout(G)
        elif layout_name == "Kamada-Kawai":
            try:
                pos = nx.kamada_kawai_layout(G)
            except:
                pos = nx.spring_layout(G, seed=42) # Fallback if scipy missing/error
        else: # Spring (default)
            pos = nx.spring_layout(G, seed=42)
        
        # Rysowanie węzłów
        nx.draw_networkx_nodes(G, pos, ax=self.ax, node_color='lightgray', node_size=500)
        nx.draw_networkx_labels(G, pos, ax=self.ax, font_size=8)
        
        # Rysowanie krawędzi z legendą
        legend_handles = []
        
        for i, s in enumerate(sessions):
            visits = s["metrics"].get("visits", [])
            color = colors[i % 10]
            name = s["file"][:20]
            
            legend_handles.append(Line2D([0], [0], color=color, lw=2, label=name))
            
            edges_list = []
            prev_node = None
            for v in visits:
                curr_node = v["name"]
                if prev_node:
                    edges_list.append((prev_node, curr_node))
                prev_node = curr_node
                
            if edges_list:
                nx.draw_networkx_edges(
                    G, pos, ax=self.ax,
                    edgelist=edges_list,
                    edge_color=[color],
                    connectionstyle=f"arc3,rad={0.1 + 0.1*i}",
                    arrowstyle='-|>', arrowsize=15
                )

        # Rysowanie etykiet kolejności nad węzłami (jeśli włączone)
        if config and config.get("show_order", True):
            # Słownik: node -> list of (session_index, [visit_indices])
            node_visits = {}
            for i, s in enumerate(sessions):
                visits = s["metrics"].get("visits", [])
                for idx, v in enumerate(visits):
                    name = v["name"]
                    if name not in node_visits:
                        node_visits[name] = []
                    
                    # Sprawdź czy już mamy wpis dla tej sesji
                    found = False
                    for entry in node_visits[name]:
                        if entry[0] == i:
                            entry[1].append(idx + 1)
                            found = True
                            break
                    if not found:
                        node_visits[name].append((i, [idx + 1]))

            # Rysowanie
            for node, visit_data in node_visits.items():
                if node not in pos: continue
                
                x, y = pos[node]
                
                # Offset startowy nad węzłem
                # Zakładamy rozmiar węzła ok. 500 (radius sqrt(500)/pi ~ 12 'points'?)
                # W koordynatach danych zależy to od skali.
                # Przyjmijmy stały offset y.
                
                y_offset = 0.1
                
                for session_idx, indices in visit_data:
                    color = colors[session_idx % 10]
                    indices_str = ",".join(map(str, indices))
                    if len(indices_str) > 10: # Skracanie długich list
                         indices_str = indices_str[:8] + "..."
                         
                    self.ax.text(
                        x, y + y_offset, 
                        indices_str,
                        color='white', fontsize=7, fontweight='bold',
                        ha='center', va='bottom',
                        bbox=dict(boxstyle="round,pad=0.2", fc=color, ec=color, alpha=0.8)
                    )
                    y_offset += 0.08 # Stackowanie w górę dla kolejnych sesji
                
        self.ax.set_title(f"Graf Przejść ({layout_name})")
        self.ax.legend(handles=legend_handles)
        self.fig.subplots_adjust(left=0.05, right=0.95, top=0.9, bottom=0.05)
        self.canvas.draw()
