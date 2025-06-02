import lora_diffusion
from diffusers import StableDiffusionPipeline, EulerAncestralDiscreteScheduler, StableDiffusionXLPipeline
import torch
from lora_diffusion import patch_pipe
import torch.nn.functional as F
from torch.nn.modules.utils import _pair
from torch.nn import Conv2d
from types import MethodType
import random
import json
import multiprocessing
from PIL import Image
import numpy as np
import io
import gi
import os
from gi._option import GLib
gi.require_version('Gtk', '3.0')
from queue import Empty, Full


class TextureModel:
    def __init__(self, mode="cpu"):

        self.on_first_step_callback = None
        self.first_step_done = False

        # Ładowanie promptów z JSONa
        with open("category_prompts.json", "r", encoding="utf-8") as f:
            self.prompt_data = json.load(f)

        self.category_list = list(self.prompt_data.keys())
        # Kategorie są kluczami w dict, np. {"CamelCaseCategory": ...}
        self.prompt_data = {k.upper(): v for k, v in self.prompt_data.items()}

        # Ładowanie modelu z .safetensors
        try:
            if mode == "cpu":
                device = torch.device("cpu")
                dtype = torch.float32
            elif mode == "cuda" and torch.cuda.is_available():
                device = torch.device("cuda")
                dtype = torch.float16
            else:
                device = torch.device("cpu")
                dtype = torch.float32

            self.alt_model_id = "runwayml/stable-diffusion-v1-5"
            self.pipe = StableDiffusionPipeline.from_pretrained(
                self.alt_model_id,
                safety_checker=None,
                torch_dtype=dtype,
                scheduler=EulerAncestralDiscreteScheduler.from_pretrained(self.model_id, subfolder="scheduler")
            )

            # self.model_id = "models/Stable-diffusion/v15PrunedEmaonly_v15PrunedEmaonly.safetensors"
            # self.pipe = StableDiffusionPipeline.from_single_file(
            #     self.model_id,
            #     torch_dtype=dtype,
            #     safety_checker=None,
            #     scheduler = EulerAncestralDiscreteScheduler.from_pretrained(self.model_id, subfolder="scheduler")
            #
            # )
            self.pipe.to(device)
            self.pipe.text_encoder.to(device)
            self.pipe.unet.to(device)
            self.device = device
            print("UNet dtype:", self.pipe.unet.dtype)
            print("Device:", self.device)
            print(f"Pipeline loaded on {mode.upper()}")
        except Exception as e:
            print(f"Błąd przy ładowaniu modelu w trybie {mode.upper()}: {e}")
            raise

        try:
            #self.pipe.scheduler = EulerAncestralDiscreteScheduler.from_config(self.pipe.scheduler.config)
            lora_path = "models/Lora/Quake_Lora.safetensors"
            self.pipe.load_lora_weights(lora_path, adapter_name="quake")
            self.pipe.to(self.device)
            #self.patch_conv2d_asymmetric_tiling(self.pipe.unet, tileX=True, tileY=False)
            print("Model załadowany z LoRA i tilingiem.")
        except Exception as e:
            print(f"Błąd przy dalszej konfiguracji modelu: {e}")
            raise



    # Metoda z rozszerzenia asyemtric-tilling do wersji webui stable diffusion
    def patch_conv2d_asymmetric_tiling(self, model, tileX=True, tileY=False):
        def replacement_conv_forward(self, input, weight, bias=None):
            paddingX = (self._reversed_padding_repeated_twice[0], self._reversed_padding_repeated_twice[1], 0, 0)
            paddingY = (0, 0, self._reversed_padding_repeated_twice[2], self._reversed_padding_repeated_twice[3])

            modeX = 'circular' if tileX else 'constant'
            modeY = 'circular' if tileY else 'constant'

            working = F.pad(input, paddingX, mode=modeX)
            working = F.pad(working, paddingY, mode=modeY)
            return F.conv2d(working, weight, bias, self.stride, _pair(0), self.dilation, self.groups)

        for module in model.modules():
            if isinstance(module, Conv2d):
                module._conv_forward = MethodType(replacement_conv_forward, module)



    def generate_process(self, queue, category, type_texture , control_queue):
        # Generowanie obrazu

        data = self.prompt_data[category]
        data_texture = data[type_texture]
        print(data_texture)

        # Obsługa seeda
        seed = data_texture.get("seed", -1)
        if seed == -1:
            seed = random.randint(0, 2 ** 32 - 1)

        generator = torch.Generator(device=self.device).manual_seed(seed)

        print("Seed:", seed)
        print("Generator device:", generator.device)
        # Parametry wejściowe
        prompt = data_texture["prompt"]
        negative_prompt = data_texture["negative_prompt"]
        height = int(data_texture["height"])
        width = int(data_texture["width"])
        steps = min(int(data_texture["steps"]), len(self.pipe.scheduler.timesteps))
        guidance_scale = float(data_texture["cfg_scale"])

        # temp size
        # width = 64
        # height = 64

        if self.device == torch.device("cuda"):

            image_out = self.pipe(
                prompt=prompt,
                negative_prompt=negative_prompt,
                num_inference_steps=steps,
                guidance_scale=guidance_scale,
                generator=generator,
                height=height,
                width=width
            ).images[0]



            print("obrazek: ", image_out)
            buf = io.BytesIO()
            print(">> Saving image...", flush=True)
            image_out.save(buf, format='PNG')
            # Zapisz wynikowy obraz

            result = image_out.copy()
            folder = "assets/test"
            os.makedirs(folder, exist_ok=True)  # TO tworzy folder, jeśli nie ma
            result.save(f"{folder}/{type_texture}.png")

            print(">> Saved, putting to queue...", flush=True)
            try:
                queue.put({
                    "type": type_texture,
                    "image": buf.getvalue(),
                    "status": "done"
                }, timeout=5)
                print(">> Done!", flush=True)
                buf.close()
            except queue.Full:
                print("Kolejka pełna!")

            return


        try:
            # Tokenizacja promptów
            with torch.no_grad():

                # Tokenizacja promptów

                # Tokenizacja promptów
                text_input = self.pipe.tokenizer(
                    [prompt],
                    padding="max_length",
                    max_length=self.pipe.tokenizer.model_max_length,
                    truncation=True,
                    return_tensors="pt"
                )
                uncond_input = self.pipe.tokenizer(
                    [negative_prompt],
                    padding="max_length",
                    max_length=self.pipe.tokenizer.model_max_length,
                    truncation=True,
                    return_tensors="pt"
                )
                # cond_embeddings = self.pipe._encode_prompt(
                #     prompt,
                #     device=self.device,
                #     num_images_per_prompt=1,
                #     do_classifier_free_guidance=True
                # )[0]
                #
                # uncond_embeddings = self.pipe._encode_prompt(
                #     negative_prompt,
                #     device=self.device,
                #     num_images_per_prompt=1,
                #     do_classifier_free_guidance=True
                # )[0]
                #
                # # 2) Przygotuj latenty:
                # latents = self.pipe.prepare_latents(
                #     batch_size=1,
                #     num_channels_latents=self.pipe.unet.in_channels,
                #     height=height,
                #     width=width,
                #     dtype=self.pipe.unet.dtype,
                #     device=self.device,
                #     generator=generator
                # )

                cond_embeddings = self.pipe.text_encoder(
                    text_input["input_ids"],
                    attention_mask=text_input.get("attention_mask", None)
                )[0]

                uncond_embeddings = self.pipe.text_encoder(
                    uncond_input["input_ids"],
                    attention_mask=uncond_input.get("attention_mask", None)
                )[0]

                # Przygotowanie latentów
                latents = self.pipe.prepare_latents(
                    batch_size=1,
                    num_channels_latents=self.pipe.unet.in_channels,
                    height=height,
                    width=width,
                    dtype=self.pipe.unet.dtype,
                    device=self.device,
                    generator=generator
                )

                # Pętla kroków generowania z możliwością anulowania
                for step in range(steps):

                    try:
                        ctrl_msg = control_queue.get_nowait()
                        if ctrl_msg["action"] == "cancel":
                            print("Generowanie anulowane na kroku:", step)

                            image_out = self.pipe.decode_latents(latents)
                            # Jeśli decode_latents zwraca tensor, odłącz go:
                            if isinstance(image_out, torch.Tensor):
                                image_out = image_out.detach().cpu().numpy()
                                image_out = (image_out[0].transpose(1, 2, 0) * 255).astype(np.uint8)
                                image_out = Image.fromarray(image_out)
                            elif isinstance(image_out, np.ndarray):
                                image_out = Image.fromarray((image_out[0] * 255).astype(np.uint8))

                            # Przekaż obraz jako bajty do głównego procesu
                            buf = io.BytesIO()
                            image_out.save(buf, format='PNG')
                            queue.put({
                                "type": type_texture,  # lub "floor", "bookcase"
                                "image": buf.getvalue(),  # bajty obrazu
                                "status": "cancelled"
                            })
                            return
                    except Empty:
                        pass  # nie było komunikatu, lecimy dalej
                    if step == 1:
                        queue.put("first_step_done")

                    current_timestep = self.pipe.scheduler.timesteps[step]
                    print(f"Generowanie obrazu dla {type_texture}: {step}/{steps}")
                    queue.put({"status": "progress", "step": step, "max_steps": steps, "type": type_texture})
                    latents_input = self.pipe.scheduler.scale_model_input(latents, current_timestep)

                    model_output_uncond = self.pipe.unet(
                        latents_input,
                        current_timestep,
                        encoder_hidden_states=uncond_embeddings
                    ).sample

                    model_output_cond = self.pipe.unet(
                        latents_input,
                        current_timestep,
                        encoder_hidden_states=cond_embeddings
                    ).sample

                    # print(f"Model output uncond mean: {model_output_uncond.mean().item()}")
                    # print(f"Model output cond mean: {model_output_cond.mean().item()}")

                    model_output = model_output_uncond + guidance_scale * (model_output_cond - model_output_uncond)

                    latents = self.pipe.scheduler.step(
                        model_output,
                        current_timestep,
                        latents,
                        generator=generator
                    ).prev_sample


                # Postprocessing latentów
                latents = latents.clamp(min=-10.0, max=10.0)

                latents = latents.detach().to(self.device).to(self.pipe.unet.dtype)
                print("Latents min/max:", latents.min().item(), latents.max().item())

                # Check na NaNy – bo jak będą, to decode_latents rozwali się od razu
                if torch.isnan(latents).any():
                    print("Uwaga! Latents zawiera NaNy!")
                    latents = latents.clamp(min=-4.0, max=4.0)
                #print("check latents")
                try:
                    image_np = self.pipe.decode_latents(latents)
                    print("Użyto decode_latents(), shape:", image_np.shape)
                except Exception as e:
                    print("decode_latents() failed:", e)
                    raise RuntimeError("Nie udało się zdekodować latentów")

                print("check obrazu", image_np)
                if isinstance(image_np, torch.Tensor):
                    image_np = image_np.detach().cpu().numpy()
                    print("Zmieniono tensor na numpy")

                # Dodatkowy check
                print("Finalny shape image_np:", image_np.shape, "dtype:", image_np.dtype)



                if image_np.ndim == 4:
                    if image_np.shape[1] in [3, 4]:  # CHW
                        image_np = image_np[0].transpose(1, 2, 0)
                    else:  # NHWC
                        image_np = image_np[0]

                # Sanitizacja
                image_np = np.nan_to_num(image_np, nan=0.0, posinf=1.0, neginf=0.0)
                image_np = np.clip(image_np, 0, 1)

                image_out = Image.fromarray((image_np * 255).astype("uint8")).convert("RGB")
                # Przekaż obraz jako bajty do głównego procesu
                buf = io.BytesIO()
                print(">> Saving image...", flush=True)
                image_out.save(buf, format='PNG')

                print(">> Saved, putting to queue...", flush=True)
                try:
                    queue.put({
                        "type": type_texture,
                        "image": buf.getvalue(),
                        "status": "done"
                    }, timeout=5)
                    print(">> Done!", flush=True)
                    buf.close()
                except queue.Full:
                    print("Kolejka pełna!")

        except Exception as e:
            queue.put(("error", str(e)))

