"""
Application Configuration

Manages configuration settings for the analysis application.
"""

import os
from pathlib import Path


class AppConfig:
    """Application configuration manager."""
    
    def __init__(self):
        # Get the project root directory (parent of analysisapp)
        self.project_root = Path(__file__).parent.parent
        
        # Default paths
        self.paths_file = self.project_root / "paths.txt"
        self.default_json_dir = self.project_root
        
        # UI Settings
        self.window_width = 800
        self.window_height = 600
        
        # Hexmap settings
        self.hexmap_window_size = 512
        
    def get_paths_file(self):
        """Get the path to the paths.txt file for hexmap analyzer."""
        return str(self.paths_file)
    
    def get_default_json_dir(self):
        """Get the default directory for JSON files."""
        return str(self.default_json_dir)


# Global configuration instance
config = AppConfig()
