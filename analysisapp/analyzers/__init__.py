"""
Analyzers Package

Contains all data analysis modules.
"""

from .hexmap_analyzer import HexmapAnalyzer
from .session_analyzer import SessionAnalyzer

# Registry of all available analyzers
ANALYZERS = [
    HexmapAnalyzer,
    SessionAnalyzer,
]

__all__ = ['ANALYZERS', 'HexmapAnalyzer', 'SessionAnalyzer']
