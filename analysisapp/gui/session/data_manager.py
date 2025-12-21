"""
Data Manager for Session Analyzer.

Handles loading, parsing, and transforming session data.
"""

import os
import requests
from ...analyzers.session.core import load_json, parse_time

class DataManager:
    """Manages session data loading and processing."""
    
    def __init__(self):
        self.session_data = None
        self.filename = None
        self.api_base_url = "http://localhost:80"

    def load_from_api(self, user_name: str = None):
        """
        Load session data from API.
        
        Args:
            user_name: Optional user name to filter sessions
            
        Returns:
            dict: Loaded and processed data
            
        Raises:
            Exception: If API call fails
        """
        try:
            if user_name:
                # Load sessions for specific user
                response = requests.get(f"{self.api_base_url}/session/{user_name}")
            else:
                # Load all sessions grouped by user
                response = requests.get(f"{self.api_base_url}/session/all")
            
            response.raise_for_status()
            api_data = response.json()
            
            # Transform API response to expected format
            data = self._transform_api_response(api_data, user_name)
            self._process_data(data)
            self.session_data = data
            self.filename = f"API: {user_name or 'all'}"
            return data
            
        except requests.RequestException as e:
            raise Exception(f"Failed to fetch data from API: {str(e)}")

    def _transform_api_response(self, api_data, user_name=None):
        """
        Transform API response to the format expected by the visualizer.
        
        Args:
            api_data: Response from API
            user_name: User name if filtering by user
            
        Returns:
            dict: Transformed data
        """
        if user_name:
            # Response from /session/{user_name}
            return {
                "user_name": api_data.get("user_name", user_name),
                "sessions": api_data.get("sessions", [])
            }
        else:
            # Response from /session/all - grouped by user
            # Take first user's sessions for now or combine
            users = api_data.get("users", [])
            if not users:
                return {"user_name": "-", "sessions": []}
            
            # Combine all sessions from all users
            all_sessions = []
            for user in users:
                user_name_val = user.get("user_name", "-")
                for session in user.get("app_sessions", []) + user.get("web_sessions", []):
                    session["_user_name"] = user_name_val
                    all_sessions.append(session)
            
            return {
                "user_name": "Wszyscy użytkownicy",
                "sessions": all_sessions
            }

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
