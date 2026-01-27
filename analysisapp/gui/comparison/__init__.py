"""
Comparison Module

Moduł zawierający komponenty do porównywania sesji.
"""

from .chart_renderer import ChartRenderer
from .graph_renderer import GraphRenderer
from .table_renderer import TableRenderer
from .dialogs import RoomMappingDialog
from .session_manager import SessionManager
from .sidebar_builder import SidebarBuilder
from .session_viewer import MultiSessionViewer

__all__ = [
    'ChartRenderer', 
    'GraphRenderer', 
    'TableRenderer',
    'RoomMappingDialog',
    'SessionManager',
    'SidebarBuilder',
    'MultiSessionViewer'
]
