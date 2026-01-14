"""
Details Panel for Session Analyzer.

Handles the textual display of selected room details.
"""

import tkinter as tk
from tkinter import ttk
from ...analyzers.session.core import extract_link_label
from ..styles import FONTS
from ..utils import format_time

class DetailsPanel:
    """Manages the details text panel."""
    
    def __init__(self, parent):
        """Initialize Details Panel."""
        self.parent = parent
        self.details = None
        self._setup_ui()

    def _setup_ui(self):
        """Setup the details UI components."""
        ttk.Label(self.parent, text="Szczegóły", 
                 font=FONTS["HEADER"]).pack(anchor=tk.W, padx=6, pady=(6,0))
        
        self.details = tk.Text(self.parent, wrap=tk.WORD, width=48)
        self.details.pack(fill=tk.BOTH, expand=True, padx=6, pady=6)

    def update_details(self, meta):
        """
        Update details view with room metadata.
        
        Args:
            meta: Room metadata dictionary
        """
        self.details.delete("1.0", tk.END)
        
        if not meta:
            return

        lines = self._build_details_text(meta)
        self.details.insert(tk.END, "\n".join(lines))

    def _build_details_text(self, meta):
        """Build formatted text lines."""
        lines = [
            f"ID Sesji: {meta.get('session_id')}",
            f"Pokój:    {meta.get('room_name')}",
            f"Wejście:  {format_time(meta.get('enter_time'))}",
            f"Wyjście:  {format_time(meta.get('exit_time'))}",
            "-" * 40,
            "Zdarzenia Książki:"
        ]
        
        for be in meta.get("book_session_events", []):
            name = be.get("book", {}).get("name", "(nieznana)")
            lines.append(f"  • {name}")
            lines.append(f"    {format_time(be.get('open_time'))} -> {format_time(be.get('close_time'))}")
        
        if not meta.get("book_session_events"):
            lines.append("  (brak)")
        
        lines.append("\nKliknięcia Linków:")
        for le in meta.get("book_link_events", []):
            link = le.get("link", "")
            label = extract_link_label(link)
            lines.append(f"  • {label}")
            lines.append(f"    @ {format_time(le.get('click_time'))}")
        
        if not meta.get("book_link_events"):
            lines.append("  (brak)")
            
        return lines

    def update_details_from_event(self, event_meta):
        """
        Update details view with specific event info.
        
        Args:
            event_meta: Metadata of the selected event
        """
        self.details.delete("1.0", tk.END)
        if not event_meta:
            return
            
        lines = [
            "Szczegóły Zdarzenia",
            "-" * 20,
            f"Typ:     {event_meta.get('type', 'nieznany')}",
            f"Etykieta: {event_meta.get('label', '-')}",
            f"Czas:    {format_time(event_meta.get('time'))}",
        ]
        
        # Add extra meta fields if available
        if "meta" in event_meta:
            m = event_meta["meta"]
            if isinstance(m, dict):
                for k, v in m.items():
                    if k not in ["book", "link", "open_time", "close_time", "click_time"]:
                        lines.append(f"{k}: {v}")
                        
        self.details.insert(tk.END, "\n".join(lines))
