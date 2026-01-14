"""
Chart Renderer

Odpowiada za rysowanie wykresów słupkowych w zakładce porównania sesji.
"""

import numpy as np
import matplotlib
from matplotlib.figure import Figure
from matplotlib.backends.backend_tkagg import FigureCanvasTkAgg


class ChartRenderer:
    """Renderuje wykresy słupkowe (czas wizyt, aktywność) na płótnie Matplotlib."""
    
    def __init__(self, parent_frame):
        """
        Args:
            parent_frame: Ramka Tkinter, w której osadzony będzie wykres.
        """
        self.parent_frame = parent_frame
        self.fig = None
        self.ax1 = None
        self.ax2 = None
        self.canvas = None
        
        self._init_figure()
        
    def _init_figure(self):
        """Inicjalizuje figurę Matplotlib z dwoma subplotami."""
        self.fig = Figure(figsize=(10, 6), dpi=100)
        self.ax1 = self.fig.add_subplot(121)
        self.ax2 = self.fig.add_subplot(122)
        self.fig.tight_layout(pad=3.0)
        
        self.canvas = FigureCanvasTkAgg(self.fig, master=self.parent_frame)
        self.canvas.draw()
        self.canvas.get_tk_widget().pack(fill='both', expand=True)
        
    def update(self, aggregated_data: dict, config: dict = None):
        """
        Aktualizuje wykresy na podstawie zagregowanych danych i konfiguracji.
        
        Args:
            aggregated_data: Słownik z danymi.
            config: Opcjonalny słownik z kluczami 'chart1' i 'chart2'.
        """
        self.ax1.clear()
        self.ax2.clear()
        
        num_sessions = aggregated_data["meta"]["count"]
        names = aggregated_data["meta"]["names"]
        
        cmap = matplotlib.cm.get_cmap('tab10')
        colors = [cmap(i) for i in range(num_sessions)]
        
        # Domyślna konfiguracja
        if not config:
            config = {
                "chart1": "Czas trwania (s)",
                "chart2": "Podsumowanie Aktywności"
            }
            
        self._draw_visits_chart(aggregated_data, num_sessions, names, colors, config.get("chart1"))
        self._draw_global_chart(aggregated_data, num_sessions, names, colors, config.get("chart2"))
        
        self.fig.tight_layout()
        self.canvas.draw()
        
    def _draw_visits_chart(self, agg, num_sessions, names, colors, metric_name):
        """Rysuje wykres dla poszczególnych wizyt (Wykres 1)."""
        visits_data = agg.get("visits_comparison", {})
        
        # Mapowanie nazwy na klucz danych
        metric_key = "duration"
        title = "Czas trwania wizyt (s)"
        
        if metric_name == "Liczba otwarć książek":
            metric_key = "books_opened"
            title = "Liczba otwarć książek na wizytę"
        elif metric_name == "Kliknięcia w linki":
            metric_key = "links_clicked"
            title = "Liczba kliknięć w linki na wizytę"
            
        visit_labels = []
        rows = []
        for k, v in visits_data.items():
            rows.append(v)
            visit_labels.append(v["label"])
            
        if not rows:
            self.ax1.text(0.5, 0.5, "Brak danych wizyt", ha='center', transform=self.ax1.transAxes)
            return
            
        x = np.arange(len(rows))
        bar_width = 0.8 / num_sessions
        
        for i in range(num_sessions):
            vals = [r_data[metric_key]["values"][i] for r_data in rows]
            offset = (i - num_sessions/2 + 0.5) * bar_width
            self.ax1.bar(x + offset, vals, bar_width, label=names[i], color=colors[i % 10])
            
        self.ax1.set_title(title)
        self.ax1.set_xticks(x)
        
        short_labels = [r[:15]+"..." if len(r) > 18 else r for r in visit_labels]
        self.ax1.set_xticklabels(short_labels, rotation=45, ha='right')
        
    def _draw_global_chart(self, agg, num_sessions, names, colors, metric_name):
        """Rysuje wykres globalny (Wykres 2)."""
        
        if metric_name == "Podsumowanie Aktywności":
            metrics = ["total_books_opened", "total_links_clicked", "unique_rooms_count"]
            metric_labels = ["Książki (Razem)", "Linki (Razem)", "Unikalne Pokoje"]
            vals_list = [
                agg["summary"]["total_books_opened"]["values"],
                agg["summary"]["total_links_clicked"]["values"],
                agg["advanced"]["unique_rooms_count"]["values"]
            ]
            title = "Podsumowanie Aktywności"
            
        elif metric_name == "Czas trwania sesji":
            metrics = ["duration"]
            metric_labels = ["Czas (s)"]
            vals_list = [agg["summary"]["duration"]["values"]]
            title = "Całkowity czas trwania sesji (s)"
            
        elif metric_name == "Tempo eksploracji":
            metrics = ["exploration_pace"]
            metric_labels = ["Pokoje / min"]
            vals_list = [agg["advanced"]["exploration_pace"]["values"]]
            title = "Tempo eksploracji (Pokoje na minutę)"
            
        elif metric_name == "Gęstość zdarzeń":
            metrics = ["event_density"]
            metric_labels = ["Zdarzenia / min"]
            vals_list = [agg["advanced"]["event_density"]["values"]]
            title = "Gęstość zdarzeń (Akcje na minutę)"
            
        elif metric_name == "Śr. czas z książką":
            metrics = ["avg_book_time"]
            metric_labels = ["Sekundy"]
            vals_list = [agg["advanced"]["avg_book_time"]["values"]]
            title = "Średni czas spędzony przy książce"
            
        else:
             # Fallback
             return

        x = np.arange(len(metrics))
        bar_width = 0.8 / num_sessions
        
        for i in range(num_sessions):
            # Dla każdej metryki pobieramy wartość dla i-tej sesji
            session_vals = [v_list[i] for v_list in vals_list]
            
            offset = (i - num_sessions/2 + 0.5) * bar_width
            self.ax2.bar(x + offset, session_vals, bar_width, label=names[i], color=colors[i % 10])
            
        self.ax2.set_title(title)
        self.ax2.set_xticks(x)
        self.ax2.set_xticklabels(metric_labels)
        self.ax2.legend()
