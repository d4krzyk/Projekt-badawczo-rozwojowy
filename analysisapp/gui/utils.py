"""
Shared utility functions for GUI components.
"""

from ..analyzers.session.core import parse_time

def format_time(t_val):
    """
    Format time value to HH:MM:SS string.
    
    Args:
        t_val: Time string or datetime object
        
    Returns:
        Formatted time string or original string on error
    """
    try:
        return parse_time(t_val).strftime('%H:%M:%S')
    except:
        return str(t_val)
