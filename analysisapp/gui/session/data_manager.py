"""
Data Manager for Session Analyzer.

Handles loading, parsing, and transforming session data.
"""

import os
from ...analyzers.session.core import load_json, parse_time

class DataManager:
    """Manages session data loading and processing."""
    
    def __init__(self):
        self.session_data = None
        self.filename = None

    def load_from_file(self, path):
        """
        Load session data from JSON file.
        
        Args:
            path: Path to JSON file
            
        Returns:
            dict: Loaded and processed data
            
        Raises:
            ValueError: If path is invalid
            Exception: If loading fails
        """
        if not path:
            raise ValueError("File path cannot be empty")
            
        if not os.path.exists(path):
            raise ValueError(f"File not found: {path}")
            
        try:
            data = load_json(path)
            self._process_data(data)
            self.session_data = data
            self.filename = os.path.basename(path)
            return data
        except Exception as e:
            raise Exception(f"Failed to load session data: {str(e)}")

    def _process_data(self, data):
        """
        Process and transform raw session data.
        
        Standardizes book events and link events structure.
        """
        if not data or "sessions" not in data:
            return

        for session in data.get("sessions", []):
            for room in session.get("rooms", []):
                self._process_room_books(room)
                self._process_room_links(room)

    def _process_room_books(self, room):
        """Transform book events structure."""
        if "books" not in room or "book_session_events" in room:
            return

        book_session_events = []
        for book in room.get("books", []):
            book_name = book.get("name")
            for event in book.get("session_events", []):
                book_session_events.append({
                    "book": {"name": book_name},
                    "open_time": event.get("open_time"),
                    "close_time": event.get("close_time")
                })
        room["book_session_events"] = book_session_events

    def _process_room_links(self, room):
        """Transform link events structure."""
        if "book_links" not in room or "book_link_events" in room:
            return

        book_link_events = []
        for link_event in room.get("book_links", []):
            book_link_events.append({
                "link": link_event.get("link"),
                "click_time": link_event.get("click_time")
            })
        room["book_link_events"] = book_link_events

    def get_user_name(self):
        """Get user name from data."""
        if not self.session_data:
            return "-"
        return self.session_data.get("user_name", "-")

    def get_sessions(self):
        """Get list of sessions."""
        if not self.session_data:
            return []
        return self.session_data.get("sessions", [])
