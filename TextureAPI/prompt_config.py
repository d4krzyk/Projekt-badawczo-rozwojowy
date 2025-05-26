from diffusers import StableDiffusionPipeline
from diffusers import StableDiffusionXLPipeline
import torch
from lora_diffusion import inject_lora_weights
from torch.nn import Conv2d
import torch.nn.functional as F
from torch.nn.modules.utils import _pair
from types import MethodType


class TextureModel:
    def __init__(self):
        self.pipe = (StableDiffusionPipeline.from_single_file(
            "stable-diffusion-webui/models/Stable-diffusion/v15PrunedEmaonly_v15PrunedEmaonly.safetensors",
            torch_dtype=torch.float16,
            safety_checker=None,
        ))
        self.pipe.to("cuda")
        lora_path = "stable-diffusion-webui/models/Lora/Quake_Lora.safetensors"
        self.pipe.load_lora_weights(".", weight_name="Quake_Lora.safetensors")
        # Wstrzykujemy LoRA do UNET i tekst encoder

        inject_lora_weights(self.pipe.unet, lora_path, alpha=0.9)
        inject_lora_weights(self.pipe.text_encoder, lora_path, alpha=0.9)

        # Włączamy asymetryczny tiling (np. tile X = True, tile Y = False)
        self.patch_conv2d_asymmetric_tiling(self.pipe.unet, tileX=True, tileY=False)

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

    def generate(self, category, height=32, width=32, num_inference_steps=20):
        # Generowanie obrazu
        output = self.pipe(category, height=height, width=width, num_inference_steps=num_inference_steps)
        return output.images[0]
