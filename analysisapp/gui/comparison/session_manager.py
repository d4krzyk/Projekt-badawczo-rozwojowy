"""
Session Manager

Manages session data loading and manipulation for comparison tab.
"""

import os
import tkinter as tk
from tkinter import ttk, filedialog, messagebox

from ..session.data_manager import DataManager
from ..styles import FONTS
from ...analyzers.session.comparison import calculate_session_metrics
from ...app_config import config


class SessionManager:
    """Manages session loading and list operations."""
    
    def __init__(self, parent_frame, session_tree, sessions_list, status_callback=None):
        """
        Initialize session manager.
        
        Args:
            parent_frame: Parent frame for dialogs
            session_tree: Treeview widget for session list
            sessions_list: List to store session data
            status_callback: Callback for status updates
        """
        self.parent_frame = parent_frame
        self.session_tree = session_tree
        self.sessions = sessions_list
        self.status_callback = status_callback
        self.on_sessions_changed = None  # Callback when sessions change

    def add_session(self):
        """Pokazuje dialog wyboru źródła danych (API lub JSON)."""
        dialog = tk.Toplevel(self.parent_frame)
        dialog.title("Wybierz źródło danych")
        dialog.geometry("300x120")
        dialog.transient(self.parent_frame)
        dialog.grab_set()
        
        # Centrowanie
        dialog.update_idletasks()
        x = self.parent_frame.winfo_rootx() + (self.parent_frame.winfo_width() - 300) // 2
        y = self.parent_frame.winfo_rooty() + (self.parent_frame.winfo_height() - 120) // 2
        dialog.geometry(f"+{x}+{y}")
        
        ttk.Label(dialog, text="Wybierz źródło danych:", font=FONTS.get("NORMAL", ("Arial", 10))).pack(pady=10)
        
        btn_frame = ttk.Frame(dialog)
        btn_frame.pack(pady=10)
        
        def on_api():
            dialog.destroy()
            self._add_from_api()
        
        def on_json():
            dialog.destroy()
            self._add_from_json()
        
        ttk.Button(btn_frame, text="📡 API", width=12, command=on_api).pack(side=tk.LEFT, padx=10)
        ttk.Button(btn_frame, text="📁 JSON", width=12, command=on_json).pack(side=tk.LEFT, padx=10)

    def _add_from_api(self):
        """Dodaje sesję z API."""
        try:
            dm = DataManager()
            data = dm.load_from_api()
            raw_sessions = data.get("sessions", [])
            
            if not raw_sessions:
                raise ValueError("Brak sesji w odpowiedzi API")
            
            for i, session_data in enumerate(raw_sessions):
                metrics = calculate_session_metrics(session_data)
                user_name = session_data.get("_user_name", data.get("user_name", "API"))
                
                self.sessions.append({
                    "file": f"API: {user_name} #{i+1}",
                    "data": session_data,
                    "metrics": metrics
                })
            
            self._refresh_list()
            self._notify_changed()
            
            if self.status_callback:
                self.status_callback(f"Pobrano {len(raw_sessions)} sesji z API")
                
        except Exception as e:
            messagebox.showerror("Błąd", f"Nie udało się pobrać z API: {e}")

    def _add_from_json(self):
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
            
            self._refresh_list()
            self._notify_changed()
            
        except Exception as e:
            messagebox.showerror("Błąd", f"Nie udało się wczytać: {e}")

    def remove_session(self):
        """Usuwa wybraną sesję z listy."""
        sel = self.session_tree.selection()
        if not sel:
            return
            
        idx = int(self.session_tree.item(sel[0], "values")[0]) - 1
        if 0 <= idx < len(self.sessions):
            self.sessions.pop(idx)
            self._refresh_list()
            self._notify_changed()

    def clear_sessions(self):
        """Czyści wszystkie sesje."""
        self.sessions.clear()
        self._refresh_list()
        self._notify_changed()

    def _refresh_list(self):
        """Odświeża widok listy sesji."""
        for item in self.session_tree.get_children():
            self.session_tree.delete(item)
            
        for i, s in enumerate(self.sessions):
            dur = f"{s['metrics']['duration']:.1f}s"
            self.session_tree.insert("", tk.END, values=(i+1, s["file"], dur))

    def _notify_changed(self):
        """Wywołuje callback po zmianie sesji."""
        if self.on_sessions_changed:
            self.on_sessions_changed()
