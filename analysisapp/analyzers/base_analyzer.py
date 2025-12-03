"""
Base Analyzer Interface

Defines the common interface for all analysis tools.
"""

from abc import ABC, abstractmethod


class BaseAnalyzer(ABC):
    """Abstract base class for all analyzers."""
    
    @property
    @abstractmethod
    def name(self) -> str:
        """Return the display name of the analyzer."""
        pass
    
    @property
    @abstractmethod
    def description(self) -> str:
        """Return a brief description of what the analyzer does."""
        pass
    
    @abstractmethod
    def run(self):
        """Execute the analyzer. This should launch the analyzer's UI or process."""
        pass
    
    def __str__(self):
        return f"{self.name}: {self.description}"
