from diffusers import StableDiffusionPipeline, EulerAncestralDiscreteScheduler
import torch
import torch.nn.functional as F
from torch.nn.modules.utils import _pair
from torch.nn import Conv2d
from types import MethodType
import random
import json
import io
import os


class TextureModel:
    def __init__(self, mode="cuda"):

        # Ładowanie promptów z JSONa
        with open("category_prompts.json", "r", encoding="utf-8") as f:
            self.prompt_data = json.load(f)

        #self.category_list = list(self.prompt_data.keys())
        print(torch.cuda.is_available())
        # Kategorie są kluczami w dict, np. {"CamelCaseCategory": ...}
        self.prompt_data = {k.upper(): v for k, v in self.prompt_data.items()}

        # Ładowanie modelu i wybór trybu generowania (cpu or gpu)
        try:
            cc_tuple = torch.cuda.get_device_capability()
            cc_version = cc_tuple[0] + cc_tuple[1] / 10
            print(torch.cuda.get_device_capability())
            if mode == "cpu":
                self.device = torch.device("cpu")
                dtype = torch.float32
            elif mode == "cuda" and torch.cuda.is_available():
                self.device = torch.device("cuda")
                if cc_version > 8:
                    dtype = torch.float16
                else:
                    dtype = torch.float32
            else:
                self.device = torch.device("cpu")
                dtype = torch.float32

            # SLOWER VERSION OF MODEL LOADING
            # self.alt_model_id = "runwayml/stable-diffusion-v1-5"
            # self.pipe = StableDiffusionPipeline.from_pretrained(
            #     self.alt_model_id,
            #     safety_checker=None,
            #     torch_dtype=dtype,
            #     scheduler=EulerAncestralDiscreteScheduler.from_pretrained(self.alt_model_id, subfolder="scheduler")
            # )

            # sheduler euler a
            scheduler = EulerAncestralDiscreteScheduler.from_pretrained("runwayml/stable-diffusion-v1-5", subfolder="scheduler")
            # ścieżka mojego modelu checkpoint
            self.model_id = "models/Stable-diffusion/v15PrunedEmaonly.safetensors"
            # Ładowanie modelu ze ścieżki i przypisanie parametrów wcześniej zdefiniowanych
            print(self.model_id)
            self.pipe = StableDiffusionPipeline.from_single_file(
                self.model_id,
                torch_dtype=dtype,
                safety_checker=None,
                scheduler = scheduler

            )
            # ustalenie trybu dla pipeline cuda/cpu
            self.pipe.to(self.device)
            self.pipe.text_encoder.to(self.device)
            self.pipe.unet.to(self.device)
            print("UNet dtype:", self.pipe.unet.dtype)
            print("Device:", self.device)
            print(f"Pipeline loaded on {mode.upper()}")
        except Exception as e:
            print(f"Błąd przy ładowaniu modelu w trybie {mode.upper()}: {e}")
            raise

        # Ładowanie modelu Lora do modelu w celu sprecyzowania obrazów pod tekstury w stylu pixel art

        try:
            # lora_path = "models/Lora/Quake_Lora.safetensors"
            #self.pipe.load_lora_weights(lora_path, adapter_name="quake")
            self.pipe.to(self.device)

            # Metoda patch conv2d asymmetric tilling do zapętlenia się tekstury
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


    # Generowanie obrazu jako proces
    def generate_process(self, category, type_texture):


        # wybór prompta z pliku category_prompts.json na podstawie nazwy kategorii
        data = self.prompt_data[category]
        data_texture = data[type_texture]
        print(data_texture)

        # Obsługa seeda
        seed = data_texture.get("seed", -1)
        if seed == -1:
            seed = random.randint(0, 2 ** 32 - 1)


        # tworzenie generatora na podstawie seeda
        generator = torch.Generator(device=self.device).manual_seed(seed)
        print("Seed:", seed)
        print("Generator device:", generator.device)


        # Parametry wejściowe do prompta
        prompt = data_texture["prompt"]
        negative_prompt = data_texture["negative_prompt"]
        height = int(data_texture["height"])
        width = int(data_texture["width"])
        steps = min(int(data_texture["steps"]), len(self.pipe.scheduler.timesteps)) # zabezpieczenie w razie wyjścia poza ilość kroków
        guidance_scale = float(data_texture["cfg_scale"])

        # temp size
        # width = 256
        # height = 256


        # trub cuda
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


model = TextureModel("cuda")

model.generate_process("CULTURE","wall")

