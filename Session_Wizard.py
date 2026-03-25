import json
import sys
from datetime import datetime
import tkinter as tk
from tkinter import ttk, filedialog, messagebox
from urllib.parse import urlparse, unquote
import matplotlib
matplotlib.use('TkAgg')
from matplotlib.figure import Figure
from matplotlib.backends.backend_tkagg import FigureCanvasTkAgg

ISO_PARSE = datetime.fromisoformat

def parse_time(s):
    try:

        return ISO_PARSE(s)

    except Exception:

        if isinstance(s, str) and s.endswith("Z"):
            return ISO_PARSE(s[:-1])
        raise

def load_json(path):
    print(f"[INFO] Loading JSON file: {path}")

    with open(path, 'r', encoding='utf-8-sig') as f:
        lines = f.read()

    print("[INFO] Raw file read complete")

    lines = lines.splitlines()

    if len(lines) >= 2 and lines[0].strip() == "```" and lines[-1].strip() == "```":
        print("[INFO] Detected markdown-style code block, stripping ``` markers")
        lines = lines[1:-1]

    lines = "\n".join(lines)

    data = json.loads(lines)

    print("[INFO] JSON parsing successful")

    return data


def extract_link_label(url):
    try:
        p = urlparse(url)
        path = p.path or ""

        seg = path.split("/")[-1]

        seg = seg or (path.split("/")[-2] if "/" in path and len(path.split("/"))>1 else "")

        seg = unquote(seg.split("#")[0].split("?")[0])

        return seg or url

    except Exception:

        return url

def normalize_data(raw):
    print("[INFO] Normalizing data structure...")

    sessions = []
    total_groups = len(raw.get("groups", []))
    print(f"[INFO] Found {total_groups} group(s)")

    for g_idx, group in enumerate(raw.get("groups", [])):
        print(f"[INFO] Processing group {g_idx}: {group.get('group_name')}")

        for user in group.get("users", []):
            user_name = user.get("user_name")
            print(f"[INFO]  User: {user_name}")

            web_sessions = user.get("web_sessions", [])
            print(f"[INFO]   Found {len(web_sessions)} web session(s)")

            for ws in web_sessions:
                print(f"[INFO]    Session ID: {ws.get('id')}")

                session = {
                    "id": ws.get("id"),
                    "user_name": user_name,
                    "start_time": ws.get("start_time"),
                    "end_time": ws.get("end_time"),
                    "rooms": []
                }

                rooms = ws.get("rooms", [])
                print(f"[INFO]     Rooms/pages: {len(rooms)}")

                for r in rooms:
                    print(f"[INFO]      Page: {r.get('name')}")

                    room = {
                        "name": r.get("name"),
                        "enter_time": r.get("enter_time"),
                        "exit_time": r.get("exit_time"),
                        "book_session_events": [],
                        "book_link_events": []
                    }

                    for b in r.get("books", []):
                        for ev in b.get("session_events", []):
                            room["book_session_events"].append({
                                "open_time": ev.get("open_time"),
                                "close_time": ev.get("close_time"),
                                "book": {"name": b.get("name")}
                            })

                    for bl in r.get("book_links", []):
                        room["book_link_events"].append({
                            "click_time": bl.get("click_time"),
                            "link": bl.get("link")
                        })

                    print(f"[INFO]       Book events: {len(room['book_session_events'])}, Link clicks: {len(room['book_link_events'])}")

                    session["rooms"].append(room)

                sessions.append(session)

    print(f"[INFO] Normalization complete: {len(sessions)} session(s) ready")

    return {
        "user_name": sessions[0]["user_name"] if sessions else "Unknown",
        "sessions": sessions
    }

def build_user_session_index(raw):
    index = {}

    for group in raw.get("groups", []):
        for user in group.get("users", []):
            uname = user.get("user_name")
            if not uname:
                continue

            sessions = []

            for ws in user.get("web_sessions", []):
                sessions.append(ws)

            for aps in user.get("app_sessions", []):
                sessions.append(aps)

            if sessions:
                index[uname] = sessions

    return index

class SessionVisualizer(tk.Tk):
    def __init__(self, data):

        super().__init__()

        self.title("Session Wizard")
        self.geometry("1600x900")

        self.raw_data = data
        self.user_index = build_user_session_index(data)

        self.current_user = None
        self.current_session = None

        self.selected_room_meta = None

        self._prepare_ui()

    def _prepare_ui(self):
        paned = ttk.PanedWindow(self, orient=tk.HORIZONTAL)
        paned.pack(fill=tk.BOTH, expand=True)

        left_frame = ttk.Frame(paned)   # timeline
        middle_frame = ttk.Frame(paned, width=380)  # event graph
        right_frame = ttk.Frame(paned, width=480)   # details

        paned.add(left_frame, weight=3)
        paned.add(middle_frame, weight=1)
        paned.add(right_frame, weight=1)

        # TOP CONTROL BAR

        ctrl = ttk.Frame(left_frame)
        ctrl.pack(fill=tk.X, pady=4)

        # USER SELECTOR
        ttk.Label(ctrl, text="User:").pack(side=tk.LEFT, padx=(6, 2))

        self.user_var = tk.StringVar()
        self.user_dropdown = ttk.Combobox(
            ctrl, textvariable=self.user_var, state="readonly", width=10
        )
        self.user_dropdown.pack(side=tk.LEFT, padx=4)

        # SESSION SELECTOR
        ttk.Label(ctrl, text="Session:").pack(side=tk.LEFT, padx=(10, 2))

        self.session_var = tk.StringVar()
        self.session_dropdown = ttk.Combobox(
            ctrl, textvariable=self.session_var, state="readonly", width=8
        )
        self.session_dropdown.pack(side=tk.LEFT, padx=4)

        # RELOAD BUTTON
        ttk.Button(ctrl, text="Reload JSON...", command=self._reload_json)\
            .pack(side=tk.RIGHT, padx=8)

        # TIMELINE LABEL

        ttk.Label(
            left_frame,
            text="Timeline",
            font=("Segoe UI", 12, "bold")
        ).pack(anchor=tk.W, padx=6, pady=(6, 0))

        # CANVAS AREA

        canvas_frame = ttk.Frame(left_frame)
        canvas_frame.pack(fill=tk.BOTH, expand=True)

        self.canvas = tk.Canvas(canvas_frame, background="#ffffff")

        self.vbar = ttk.Scrollbar(canvas_frame, orient=tk.VERTICAL, command=self.canvas.yview)
        self.hbar = ttk.Scrollbar(canvas_frame, orient=tk.HORIZONTAL, command=self.canvas.xview)

        self.canvas.configure(
            yscrollcommand=self.vbar.set,
            xscrollcommand=self.hbar.set
        )

        self.vbar.pack(side=tk.RIGHT, fill=tk.Y)
        self.hbar.pack(side=tk.BOTTOM, fill=tk.X)
        self.canvas.pack(fill=tk.BOTH, expand=True, side=tk.LEFT)

        # DETAILS PANEL

        ttk.Label(
            right_frame,
            text="Details",
            font=("Segoe UI", 12, "bold")
        ).pack(anchor=tk.W, padx=6, pady=(6, 0))

        self.details = tk.Text(right_frame, wrap=tk.WORD, width=48)
        self.details.pack(fill=tk.BOTH, expand=True, padx=6, pady=6)

        # EVENT GRAPH

        ttk.Label(
            middle_frame,
            text="Event Graph",
            font=("Segoe UI", 12, "bold")
        ).pack(anchor=tk.W, padx=6, pady=(6, 0))

        self.fig = Figure(figsize=(4.5, 6), tight_layout=True)
        self.ax = self.fig.add_subplot(111)

        self.canvas_fig = FigureCanvasTkAgg(self.fig, master=middle_frame)
        self.canvas_fig.get_tk_widget().pack(fill=tk.BOTH, expand=True, padx=6, pady=6)

        # EVENT STORAGE

        self._plot_points_meta = []

        # BINDINGS

        self.canvas.tag_bind("room", "<Button-1>", self._on_room_click)

        self.canvas.bind(
            "<Configure>",
            lambda e: self.canvas.configure(scrollregion=self.canvas.bbox("all"))
        )

        self.fig.canvas.mpl_connect('pick_event', self._on_plot_pick)

        # INITIALIZE DROPDOWNS

        self._populate_users()

        self.user_dropdown.bind("<<ComboboxSelected>>", self._on_user_change)
        self.session_dropdown.bind("<<ComboboxSelected>>", self._on_session_change)

    def _populate_users(self):
        users = sorted(self.user_index.keys())
        self.user_dropdown['values'] = users

        if users:
            self.user_var.set(users[0])
            self._on_user_change()

    def _on_user_change(self, event=None):
        user = self.user_var.get()
        self.current_user = user

        sessions = self.user_index.get(user, [])
        session_ids = [str(s.get("id")) for s in sessions]

        self.session_dropdown['values'] = session_ids

        if session_ids:
            self.session_var.set(session_ids[0])
            self._on_session_change()

    def _on_session_change(self, event=None):
        user = self.current_user
        session_id = self.session_var.get()

        sessions = self.user_index.get(user, [])

        for s in sessions:
            if str(s.get("id")) == session_id:
                self.current_session = s
                break

        self._load_selected_session()

    def _load_selected_session(self):
        if not self.current_session:
            return

        print(f"[INFO] Rendering user={self.current_user}, session={self.current_session.get('id')}")

        # Convert ONE session to expected format
        normalized = {
            "user_name": self.current_user,
            "sessions": [self._normalize_single_session(self.current_session)]
        }

        self.data = normalized

        self.canvas.delete("all")
        self.ax.clear()
        self._plot_points_meta.clear()

        self._draw_timeline()
        self.canvas_fig.draw()

    def _reload_json(self):
        path = filedialog.askopenfilename(
            filetypes=[("JSON files","*.json"),("All files","*.*")]
        )

        if not path:
            return

        try:
            print(f"[INFO] Reloading JSON: {path}")

            data = load_json(path)

            # rebuild index
            self.raw_data = data
            self.user_index = build_user_session_index(data)

            # reset state
            self.current_user = None
            self.current_session = None
            self.selected_room_meta = None

            # clear UI
            self.canvas.delete("all")
            self.ax.clear()
            self._plot_points_meta.clear()

            # repopulate dropdowns (this triggers redraw)
            self._populate_users()

            self.canvas_fig.draw()

            print("[INFO] Reload complete")

        except Exception as e:
            messagebox.showerror("Error", f"Failed to load JSON: {e}")

    def _normalize_single_session(self, ws):
        session = {
            "id": ws.get("id"),
            "start_time": ws.get("start_time"),
            "end_time": ws.get("end_time"),
            "rooms": []
        }

        for r in ws.get("rooms", []):
            room = {
                "name": r.get("name"),
                "enter_time": r.get("enter_time"),
                "exit_time": r.get("exit_time"),
                "book_session_events": [],
                "book_link_events": []
            }

            for b in r.get("books", []):
                for ev in b.get("session_events", []):
                    room["book_session_events"].append({
                        "open_time": ev.get("open_time"),
                        "close_time": ev.get("close_time"),
                        "book": {"name": b.get("name")}
                    })

            for bl in r.get("book_links", []):
                room["book_link_events"].append({
                    "click_time": bl.get("click_time"),
                    "link": bl.get("link")
                })

            session["rooms"].append(room)

        return session

    def _draw_timeline(self):
        sessions = self.data.get("sessions", [])

        if not sessions:
            return


        parsed_sessions = []

        max_duration = 0.0
        for s in sessions:
            try:

                st_original = parse_time(s["start_time"])
                et = parse_time(s["end_time"])

                # --- Find first actual activity time ---
                activity_times = []

                for r in s.get("rooms", []):
                    try:
                        activity_times.append(parse_time(r["enter_time"]))
                    except Exception:
                        pass

                    for be in r.get("book_session_events", []):
                        try:
                            activity_times.append(parse_time(be["open_time"]))
                        except Exception:
                            pass

                    for le in r.get("book_link_events", []):
                        try:
                            activity_times.append(parse_time(le["click_time"]))
                        except Exception:
                            pass

                # Use earliest activity if available
                if activity_times:
                    st = min(activity_times)
                else:
                    st = st_original

            except Exception:

                continue

            dur = max(0.0, (et - st).total_seconds())

            parsed_sessions.append((s, st, et, dur))
            if dur > max_duration:
                max_duration = dur

        if max_duration <= 0:
            max_duration = 1.0

        top_margin = 100
        left_margin = 220
        width_per_session = 300
        bar_width = 48

        y_scale = 620.0 / max_duration 
        x = left_margin
        color_palette = [
            "#ff9999","#99ccff","#99ff99","#ffcc99",
            "#ccccff","#ffb3e6","#c2f0c2","#ffd9b3"
        ]
        first_room_set = False


        for idx, (s, st, et, dur) in enumerate(parsed_sessions):

            session_left = x
            session_top = top_margin

            header = f"Session {s.get('id', idx)}\n{st.isoformat()}\n→ {et.isoformat()}\n({int(dur)}s)"
            self.canvas.create_text(session_left + bar_width/2, 18, text=header, font=("Segoe UI", 9), anchor="n")

            sess_h = max(24, dur * y_scale)
     
            self.canvas.create_rectangle(session_left, session_top, session_left + bar_width, session_top + sess_h,
                                         fill="#f8f8f8", outline="#d0d0d0")

            tick_times = set()

            tick_times.add(st)

            tick_times.add(et)

            rooms = s.get("rooms", [])

            for r in rooms:

                try:

                    tick_times.add(parse_time(r["enter_time"]))
                    tick_times.add(parse_time(r["exit_time"]))

                except Exception:

                    pass

                for be in r.get("book_session_events", []):

                    try:

                        tick_times.add(parse_time(be["open_time"]))
                        tick_times.add(parse_time(be["close_time"]))

                    except Exception:

                        pass

                for le in r.get("book_link_events", []):

                    try:

                        tick_times.add(parse_time(le["click_time"]))

                    except Exception:

                        pass

            tick_list = sorted(list(tick_times))

            for r_idx, r in enumerate(rooms):

                try:
    
                    ent = parse_time(r["enter_time"])
                    ex = parse_time(r["exit_time"])
                except Exception:

                    continue

                rel_start = max(0.0, (ent - st).total_seconds())
                rel_end = max(0.0, (ex - st).total_seconds())
                if rel_end < rel_start:
                    rel_end = rel_start

                y0 = session_top + rel_start * y_scale
                y1 = session_top + rel_end * y_scale

                x0 = session_left - 6
                x1 = session_left + bar_width + 6

                color = color_palette[r_idx % len(color_palette)]
                rect = self.canvas.create_rectangle(x0, y0, x1, y1, fill=color, outline="#222", tags=("room",))

                meta = {
                    "session_id": s.get("id"),
                    "room_name": r.get("name"),
                    "enter_time": r.get("enter_time"),
                    "exit_time": r.get("exit_time"),
                    "book_session_events": r.get("book_session_events", []),
                    "book_link_events": r.get("book_link_events", [])
                }
                self.canvas.itemconfig(rect, tags=("room", f"room_{idx}_{r_idx}"))
                self.canvas.setvar(f"room_meta_{idx}_{r_idx}", json.dumps(meta))

                height = y1 - y0
                if height > 18:
                    self.canvas.create_text(session_left - 12, (y0 + y1) / 2,
                                            text=r.get("name", ""), font=("Segoe UI", 9), anchor="e")

                for be in r.get("book_session_events", []):
                    try:
                        opent = parse_time(be["open_time"])
                        rel = (opent - st).total_seconds()

                        by = session_top + rel * y_scale

                        self.canvas.create_line(session_left + bar_width + 4, by, session_left + bar_width + 14, by, width=2, dash=(2,2))

                        self.canvas.create_text(session_left - 18, by,
                                                text=be.get("book", {}).get("name", ""), anchor="e", font=("Segoe UI", 8))
                    except Exception:
                        continue

                for le in r.get("book_link_events", []):
                    try:
                        clickt = parse_time(le["click_time"])
                        rel = (clickt - st).total_seconds()
                        ly = session_top + rel * y_scale

                        self.canvas.create_oval(session_left - 20, ly-4, session_left - 8, ly+4, fill="#000000")
                        link = le.get("link", "")
                        label = extract_link_label(link)
                        display = label if len(label) <= 30 else label[:28] + "…"
                        self.canvas.create_text(session_left - 26, ly, text=display, anchor="e", font=("Segoe UI", 8))
                    except Exception:
                        continue

            for ttime in tick_list:

                rel = max(0.0, (ttime - st).total_seconds())
                ty = session_top + rel * y_scale

                self.canvas.create_line(session_left + bar_width, ty, session_left + bar_width + 6, ty, width=1)

                ts_col_x = session_left + bar_width + 70

                min_sep = 18
                last_label_y = None
                zig = 1
                for ttime in tick_list:
                    rel = max(0.0, (ttime - st).total_seconds())
                    ty = session_top + rel * y_scale

                    label = ttime.isoformat(timespec='seconds')

                    desired_y = ty
                    if last_label_y is None:
                        label_y = desired_y
                    else:
                        label_y = desired_y
                        if label_y - last_label_y < min_sep:
                            label_y = last_label_y + min_sep
                    last_label_y = label_y

                    txt = self.canvas.create_text(ts_col_x, label_y, text=label, anchor="w", font=("Segoe UI", 9))

                    bbox = self.canvas.bbox(txt)
                    if bbox:
                        text_left = bbox[0]
                    else:
                        text_left = ts_col_x

                    tick_x = session_left + bar_width + 3

                    min_offset = 20
                    base_offset = 12
                    offset = base_offset * zig
                    if offset < 0:
                        offset = -offset
                    zig = -zig

                    mid_x = tick_x + max(offset, min_offset)
                    max_mid = ts_col_x - 10
                    if mid_x > max_mid:
                        mid_x = max_mid

                    self.canvas.create_line(tick_x, ty, mid_x, ty, mid_x, label_y, text_left - 6, label_y, width=1)

                    self.canvas.create_oval(tick_x-3, ty-3, tick_x+3, ty+3, outline="#444", fill="#444")

            x += width_per_session

        max_vis_height = top_margin + max(200, int(max_duration * y_scale) + 200)
        self.canvas.configure(scrollregion=(0, 0, x + 400, max_vis_height))

    def _collect_event_points(self, room_meta):
        events = []
        for s_idx, s in enumerate(self.data.get("sessions", [])):
            rooms = s.get("rooms", [])
            for r in rooms:

                if r.get("name") != room_meta.get("room_name") or s.get("id") != room_meta.get("session_id"):
                    continue

                for be in r.get("book_session_events", []):

                    try:

                        t = parse_time(be.get("open_time"))
                    except Exception:

                        continue

                    label = be.get("book", {}).get("name", "(book)")
                    events.append({"time": t, "label": label, "session_idx": s_idx, "type": "book", "meta": {"session_id": s.get("id"), "room": r.get("name"), **be}})

                for le in r.get("book_link_events", []):

                    try:
                        t = parse_time(le.get("click_time"))

                    except Exception:
                        continue

                    label = extract_link_label(le.get("link", "(link)"))

                    events.append({"time": t, "label": label, "session_idx": s_idx, "type": "link", "meta": {"session_id": s.get("id"), "room": r.get("name"), **le}})

        events.sort(key=lambda e: e["time"])
        return events

    def _draw_graph(self):

        self.ax.clear()

        self._plot_points_meta.clear()

        if not self.selected_room_meta:
            return
        events = self._collect_event_points(self.selected_room_meta)

        if not events:
            self.ax.text(0.5, 0.5, 'No events to plot', ha='center', va='center')
            self.canvas_fig.draw()
            return

        t0 = events[0]["time"]

        xs = [ e["session_idx"] for e in events ]
        ys = [ (e["time"] - t0).total_seconds() / 60.0 for e in events ]

        color_palette = ["#ff9999","#99ccff","#99ff99","#ffcc99","#ccccff","#ffb3e6","#c2f0c2","#ffd9b3"]
        line_palette = ["#1f77b4", "#ff7f0e", "#2ca02c", "#d62728", "#9467bd", "#8c564b", "#e377c2", "#7f7f7f"]

        session_groups = {}

        for i, e in enumerate(events):
            session_groups.setdefault(e["session_idx"], []).append((i, e))

        for s_idx, points in session_groups.items():

            xi = [ xs[i] for i, _ in points ]
            yi = [ ys[i] for i, _ in points ]

            color = line_palette[s_idx % len(line_palette)]
            self.ax.plot(xi, yi, '-', linewidth=1, color=color, alpha=0.7)

        for i, e in enumerate(events):

            color = color_palette[e["session_idx"] % len(color_palette)]

            m = self.ax.scatter(xs[i], ys[i], s=50, color=color, picker=5, zorder=3)

            self._plot_points_meta.append({'artist': m, 'meta': e})

        labels = []

        for i, e in enumerate(events):

                lab = e["label"] if len(e["label"]) <= 40 else e["label"][:38] + '…'
                labels.append({"idx": i, "x": xs[i], "y": ys[i], "text": lab})

        trans = self.ax.transData

        disp_pts = []

        for item in labels:

            dx, dy = trans.transform((item["x"], item["y"]))
            disp_pts.append({"idx": item["idx"], "dx": dx, "dy": dy, "text": item["text"], "data_x": item["x"], "data_y": item["y"]})
        
        disp_pts.sort(key=lambda p: p["dy"])
        min_sep_px = 14
        placed_dy = []
        
        for p in disp_pts:

            label_dy = p["dy"]

            for py in placed_dy:

                if label_dy - py < min_sep_px:
                    label_dy = py + min_sep_px

            placed_dy.append(label_dy)

            p["label_dy"] = label_dy
            p["label_dx"] = p["dx"] + 14 

        inv = self.ax.transData.inverted()

        for p in disp_pts:

            label_data_x, label_data_y = inv.transform((p["label_dx"], p["label_dy"]))

            self.ax.plot([p["data_x"], label_data_x], [p["data_y"], label_data_y], '-', linewidth=0.6, color="#666", zorder=2, alpha=0.6)
            self.ax.text(label_data_x, label_data_y, p["text"], fontsize=8, va='center', ha='left', zorder=4)
        
        self.ax.set_ylabel('Minutes from first event')
        self.ax.set_xticks(sorted(set(xs)))
        self.ax.set_xticklabels([str(i) for i in sorted(set(xs))])
        self.ax.grid(True, linestyle=':', linewidth=0.5, alpha=0.6)
        for spine in self.ax.spines.values():
            spine.set_visible(False)
        self.fig.tight_layout(pad=1.2)
        self.ax.invert_yaxis()
        self.canvas_fig.draw()

    def _on_plot_pick(self, event):
        picked_artist = event.artist
        for entry in self._plot_points_meta:
            if entry['artist'] == picked_artist:
                meta = entry['meta']
                self._show_event_details(meta)
                return

    def _show_event_details(self, meta):
        self.details.delete("1.0", tk.END)

        lines = [
            f"Session ID: {meta.get('meta', {}).get('session_id') if isinstance(meta.get('meta', {}), dict) else meta.get('session_id')}",
            f"Session idx: {meta.get('session_idx')}",
            f"Type: {meta.get('type')}",
            f"Label: {meta.get('label')}",
            f"Time: {meta.get('time').isoformat()}",
            f"Room: {meta.get('meta', {}).get('room', '(unknown)')}\n",
            json.dumps(meta.get('meta', {}), indent=2)
        ]

        self.details.insert(tk.END, "\n".join(lines))

    def _on_room_click(self, event):

        x = self.canvas.canvasx(event.x)
        y = self.canvas.canvasy(event.y)

        items = self.canvas.find_overlapping(x, y, x, y)

        for it in items:
            tags = self.canvas.gettags(it)
            for t in tags:
                if t.startswith("room_"):
                    try:
                        parts = t.split("_")
                        meta_json = self.canvas.getvar(f"room_meta_{parts[1]}_{parts[2]}")
                        meta = json.loads(meta_json)
                    except Exception:
                        meta = {}
                    self.selected_room_meta = meta
                    self._show_room_details(meta)
                    self._draw_graph()
                    return

    def _show_room_details(self, meta):
        self.details.delete("1.0", tk.END)
        lines = [
            f"Session ID: {meta.get('session_id')}",
            f"Room: {meta.get('room_name')}",
            f"Enter: {meta.get('enter_time')}",
            f"Exit:  {meta.get('exit_time')}\n",
            "Book events:"
        ]
        for be in meta.get("book_session_events", []):
            name = be.get("book", {}).get("name", "(unknown)")
            lines.append(f"  - {name}: {be.get('open_time')} → {be.get('close_time')}")
        if not meta.get("book_session_events"):
            lines.append("  (none)")
        lines.append("\nLink clicks:")
        for le in meta.get("book_link_events", []):
            link = le.get("link", "")
            label = extract_link_label(link)
            lines.append(f"  - {label}  @ {le.get('click_time')}")
        if not meta.get("book_link_events"):
            lines.append("  (none)")
        self.details.insert(tk.END, "\n".join(lines))

def main():
    print("[INFO] Session Visualizer starting...")

    if len(sys.argv) > 1:
        path = sys.argv[1]
    else:
        root = tk.Tk()
        root.withdraw()
        path = filedialog.askopenfilename(
            title="Select JSON file",
            filetypes=[("JSON files", "*.json"), ("All files", "*.*")]
        )
        root.destroy()

        if not path:
            print("[WARN] No file chosen. Exiting.")
            return

    try:
        data = load_json(path)
    except Exception as e:
        print("[ERROR] Failed to read JSON:", e)
        return

    print("[INFO] Launching UI...")
    app = SessionVisualizer(data)
    app.mainloop()

if __name__ == "__main__":
    main()