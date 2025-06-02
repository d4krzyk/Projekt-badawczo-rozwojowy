from PIL import Image
import io
from prompt_config import TextureModel

from queue import Empty  # <- to potrzebne do obsługi wyjątku

def generate_process_wrapper(queue, control_queue, mode):
    try:
        model = TextureModel(mode)
        queue.put({"status": "model_ready"})  # Informacja dla GUI

        while True:
            msg = control_queue.get()
            if msg["action"] == "generate":
                category = msg["category"]
                cat_upper = category.upper()
                if cat_upper not in model.prompt_data:
                    queue.put({"error": f"Invalid category: '{category}'"})
                    continue

                for texture_type in ["wall", "floor", "bookcase"]:
                    try:
                        cancel_msg = control_queue.get_nowait()
                        if cancel_msg.get("action") == "cancel":
                            queue.put({"error": "Generating process cancelled."})
                            break
                    except Empty:
                        pass  # wszystko OK, nic nie anulowane

                    model.generate_process(queue, cat_upper, texture_type, control_queue)


            elif msg["action"] == "exit":
                break  # kończymy proces

    except Exception as e:
        queue.put({"error": str(e)})