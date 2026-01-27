"""
Timeline Chart

Timeline visualization for session comparison.
"""

import matplotlib
matplotlib.use('TkAgg')
from matplotlib.figure import Figure
from matplotlib.backends.backend_tkagg import FigureCanvasTkAgg
import matplotlib.patches as mpatches

SESSION_COLORS = ['#2196F3', '#F44336', '#4CAF50', '#FF9800', '#9C27B0', '#00BCD4', '#795548', '#607D8B']
ROOM_COLORS = ['#1976D2', '#D32F2F', '#388E3C', '#F57C00', '#7B1FA2', '#0097A7', '#5D4037', '#455A64']


class TimelineChart:
    """Timeline visualization component."""
    
    def __init__(self, parent):
        self.fig = Figure(figsize=(10, 5), dpi=100, facecolor='white')
        self.canvas = FigureCanvasTkAgg(self.fig, master=parent)
        self.canvas.get_tk_widget().pack(fill='both', expand=True)
        
    def update(self, sessions):
        """Update timeline with selected sessions."""
        self.fig.clear()
        
        if not sessions:
            self._show_msg("Wybierz sesje")
            return
        
        n = len(sessions)
        max_dur = max(s.get("metrics", {}).get("duration", 100) for _, s in sessions)
        max_dur = max(max_dur, 10)
        
        all_rooms = set()
        for _, s in sessions:
            for v in s.get("metrics", {}).get("visits", []):
                all_rooms.add(v.get("name", "?"))
        room_list = sorted(list(all_rooms))
        room_color_map = {r: ROOM_COLORS[i % len(ROOM_COLORS)] for i, r in enumerate(room_list)}
        
        ax = self.fig.add_subplot(111)
        y_positions = list(range(n-1, -1, -1))
        
        for row, (idx, sess) in enumerate(sessions):
            y = y_positions[row]
            color = SESSION_COLORS[idx % len(SESSION_COLORS)]
            visits = sess.get("metrics", {}).get("visits", [])
            dur = sess.get("metrics", {}).get("duration", 0)
            
            label = sess['file'][:18] + "..." if len(sess['file']) > 18 else sess['file']
            ax.text(-max_dur * 0.02, y, f"{label}\n({dur:.0f}s)", 
                   ha='right', va='center', fontsize=9, fontweight='bold', color=color)
            
            ax.plot([0, dur], [y, y], color='#E0E0E0', linewidth=3, zorder=1)
            
            cumulative = 0
            for v in visits:
                name = v.get("name", "?")
                d = v.get("duration", 10)
                rc = room_color_map.get(name, '#666')
                
                ax.plot([cumulative, cumulative + d], [y, y], color=rc, linewidth=8, zorder=2, alpha=0.7)
                ax.scatter(cumulative + d/2, y, s=120, c=rc, marker='o', edgecolors='white', linewidths=1.5, zorder=4)
                cumulative += d
        
        handles = [mpatches.Patch(facecolor=room_color_map[r], label=r) for r in room_list]
        ax.legend(handles=handles, loc='upper right', fontsize=8, title="Pokoje")
        
        ax.set_xlim(-max_dur * 0.2, max_dur * 1.05)
        ax.set_ylim(-0.5, n - 0.5)
        ax.set_yticks([])
        ax.set_xlabel("Czas (sekundy)")
        ax.spines['top'].set_visible(False)
        ax.spines['right'].set_visible(False)
        ax.spines['left'].set_visible(False)
        ax.grid(axis='x', alpha=0.3, linestyle='--')
        ax.set_title("Timeline porównania sesji", fontweight='bold')
        
        self.canvas.draw()
        
    def _show_msg(self, text):
        ax = self.fig.add_subplot(111)
        ax.text(0.5, 0.5, text, ha='center', va='center', fontsize=14, color='#666')
        ax.axis('off')
        self.canvas.draw()
