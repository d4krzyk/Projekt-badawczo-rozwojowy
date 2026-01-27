"""
Session Comparison Logic

Handles calculation of metrics and comparison between two sessions.
"""

from datetime import datetime
from .core import parse_time

def calculate_session_metrics(session):
    """
    Calculate comprehensive metrics for a single session.
    Now includes 'visits' list for chronological analysis.
    """
    metrics = {
        "duration": 0.0,
        "rooms": {},      # Aggregated by name (Total time in Kitchen)
        "visits": [],     # Chronological sequence (Visit 1: Kitchen, Visit 2: Salon...)
        "books": {},
        "total_rooms": 0,
        "total_books_opened": 0,
        "total_links_clicked": 0,
        # Advanced Metrics
        "unique_rooms_count": 0,
        "room_sequence": [],
        "exploration_pace": 0.0, 
        "event_density": 0.0,    
        "avg_book_time": 0.0,
        "total_book_time": 0.0,
        "visited_rooms_set": set()
    }
    
    if not session:
        return metrics
        
    # 1. Total Duration
    try:
        st = parse_time(session.get("start_time"))
        et = parse_time(session.get("end_time"))
        metrics["duration"] = max(0.0, (et - st).total_seconds())
    except Exception:
        metrics["duration"] = 0.0
        
    rooms = session.get("rooms", [])
    metrics["total_rooms"] = len(rooms)
    
    # 2. Room Analysis
    total_book_time_sum = 0.0
    
    for i, room in enumerate(rooms):
        room_name = room.get("name", "unknown")
        metrics["room_sequence"].append(room_name)
        metrics["visited_rooms_set"].add(room_name)
        
        # Room duration
        try:
            enter = parse_time(room.get("enter_time"))
            exit_time = parse_time(room.get("exit_time"))
            duration = max(0.0, (exit_time - enter).total_seconds())
        except Exception:
            duration = 0.0
            
        # Init aggregate dict
        if room_name not in metrics["rooms"]:
             metrics["rooms"][room_name] = {
                "duration": 0.0,
                "books_opened": 0,
                "links_clicked": 0,
                "visits": 0
            }
        
        # Update Aggregate
        metrics["rooms"][room_name]["duration"] += duration
        metrics["rooms"][room_name]["visits"] += 1
        
        # Collect Visit Data
        visit_data = {
            "index": i,
            "name": room_name,
            "duration": duration,
            "books_opened": 0,
            "links_clicked": 0
        }
        
        # Room interactions
        # Books
        book_events = room.get("book_session_events", [])
        visit_data["books_opened"] = len(book_events)
        
        metrics["rooms"][room_name]["books_opened"] += len(book_events)
        metrics["total_books_opened"] += len(book_events)
        
        for be in book_events:
            book_name = be.get("book", {}).get("name", "unknown")
            if book_name not in metrics["books"]:
                metrics["books"][book_name] = {"opens": 0, "total_time": 0.0}
            
            metrics["books"][book_name]["opens"] += 1
            
            try:
                open_t = parse_time(be.get("open_time"))
                close_t = parse_time(be.get("close_time"))
                read_time = max(0.0, (close_t - open_t).total_seconds())
                metrics["books"][book_name]["total_time"] += read_time
                total_book_time_sum += read_time
            except Exception:
                pass
                
        # Links
        link_events = room.get("book_link_events", [])
        visit_data["links_clicked"] = len(link_events)
        
        metrics["rooms"][room_name]["links_clicked"] += len(link_events)
        metrics["total_links_clicked"] += len(link_events)
        
        # Add to visits
        metrics["visits"].append(visit_data)
        
    metrics["total_book_time"] = total_book_time_sum
    metrics["unique_rooms_count"] = len(metrics["visited_rooms_set"])
    
    # Derived advanced metrics
    duration_min = metrics["duration"] / 60.0 if metrics["duration"] > 0 else 0.0
    
    if duration_min > 0:
        metrics["exploration_pace"] = metrics["unique_rooms_count"] / duration_min
        total_events = metrics["total_books_opened"] + metrics["total_links_clicked"]
        metrics["event_density"] = total_events / duration_min
        
    if metrics["total_books_opened"] > 0:
        metrics["avg_book_time"] = total_book_time_sum / metrics["total_books_opened"]
        
    return metrics

def aggregate_metrics_multi(sessions_data, room_mapping=None):
    """
    Aggregate metrics from multiple sessions (Visit-Aware).
    
    Args:
        sessions_data (list): List of tuples (session_name, metrics_dict).
        room_mapping (list of tuple, optional): List of tuples of INDICES (int). e.g. (0, 0, 1).
                                               Points to index in metrics['visits'].
                                               If None, naive sequential matching by visit index (0-0, 1-1).
                                               
    Returns:
        dict: Aggregated data structure.
        - 'visits_comparison': Main comparison data row-by-row.
    """
    if not sessions_data:
        return {}
        
    num_sessions = len(sessions_data)
    session_names = [s[0] for s in sessions_data]
    metrics_list = [s[1] for s in sessions_data]
    
    aggregated = {
        "meta": {"names": session_names, "count": num_sessions},
        "summary": {},
        "advanced": {},
        "visits_comparison": {} # Primary visualization now
    }
    
    # 1. Scalar Metrics
    summary_keys = ["duration", "total_rooms", "total_books_opened", "total_links_clicked"]
    advanced_keys = ["exploration_pace", "event_density", "avg_book_time", "unique_rooms_count"]
    
    for category, keys in [("summary", summary_keys), ("advanced", advanced_keys)]:
        for key in keys:
            values = []
            for m in metrics_list:
                values.append(m.get(key, 0))
            try:
                spread = max(values) - min(values)
            except:
                spread = 0
            aggregated[category][key] = {"values": values, "spread": spread}
            
    # Jaccard
    sets = [m.get("visited_rooms_set", set()) for m in metrics_list]
    if sets:
        intersection = set.intersection(*sets)
        union = set.union(*sets)
        jaccard = len(intersection) / len(union) if len(union) > 0 else 0.0
    else:
        jaccard = 0.0
    aggregated["advanced"]["jaccard_index"] = {"value": jaccard, "values": [jaccard], "spread": 0}

    # 2. Visits Aggregation (Comparison Table)
    
    # We need to build rows.
    # If mapping provided, it contains tuples of integers (indices in 'visits' list).
    
    visit_rows = []
    
    if room_mapping:
         for row_tuple in room_mapping:
             clean_row = []
             for i in range(num_sessions):
                 if i < len(row_tuple):
                     clean_row.append(row_tuple[i])
                 else:
                     clean_row.append(None)
             visit_rows.append(clean_row)
    else:
        # Sequential by Visit Index 0 to Max
        lengths = [len(m.get("visits", [])) for m in metrics_list]
        max_len = max(lengths, default=0)
        
        for i in range(max_len):
            row = []
            for m in metrics_list:
                visits = m.get("visits", [])
                if i < len(visits):
                    row.append(i) # Index i
                else:
                    row.append(None)
            if any(r is not None for r in row):
                visit_rows.append(row)
                
    # Process Rows
    for row_idx, indices_tuple in enumerate(visit_rows):
        # Build Label and Data
        
        # Get visit objects
        visits_data = []
        names_in_row = []
        
        for session_idx, visit_idx in enumerate(indices_tuple):
            if visit_idx is not None and visit_idx < len(metrics_list[session_idx]["visits"]):
                v = metrics_list[session_idx]["visits"][visit_idx]
                visits_data.append(v)
                names_in_row.append(v["name"])
            else:
                visits_data.append(None)
                names_in_row.append(None)
                
        # Generate Label
        # e.g. "Kitchen" (if all same) or "Kitchen vs Salon"
        non_none_names = [n for n in names_in_row if n]
        if not non_none_names:
            continue
            
        first = non_none_names[0]
        if all(n == first for n in non_none_names) and len(non_none_names) == len(indices_tuple):
             label = f"{first}" # maybe prepend index?
             # Since it might be visit 1 for A but visit 2 for B, simple index might be misleading if we force it.
             # But usually it's chronological.
        else:
             label = " vs ".join([str(n) if n else "-" for n in names_in_row])
             
        # Differentiate collisions in dict keys?
        # Use row index in key to ensure uniqueness
        key = f"row_{row_idx}_{label}"
        
        # Collect Metrics
        durations = []
        books = []
        links = []
        
        for v in visits_data:
            if v:
                durations.append(v["duration"])
                books.append(v["books_opened"])
                links.append(v["links_clicked"])
            else:
                durations.append(0)
                books.append(0)
                links.append(0)
                
        aggregated["visits_comparison"][key] = {
            "label": label,
            "duration": {"values": durations, "spread": max(durations)-min(durations)},
            "books_opened": {"values": books, "spread": max(books)-min(books)},
            "links_clicked": {"values": links, "spread": max(links)-min(links)},
            "_meta_indices": indices_tuple # Store indices
        }
        
    return aggregated

# Keeping compare_metrics for compatibility if needed, but aggregate_metrics_multi is superior.
def compare_metrics(metrics_a, metrics_b, room_pairs=None):
    return {} # Legacy stub or remove if fully migrated. (Keeping stub logic to avoid breaking old imports if any)
    # Actually let's just make it call aggregate
    # But return format differs. Better to leave separate or unused.
    pass
