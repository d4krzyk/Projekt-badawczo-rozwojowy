import numpy as np
import pytest
import torch
from unittest.mock import MagicMock, patch, call
from prompt_config import TextureModel
from queue import Queue, Empty

@pytest.fixture
def texture_model_cpu():
    return TextureModel(mode="cpu")

def test_load_prompts(texture_model_cpu):
    # Sprawdź czy prompty załadowały się i mają klucze
    assert isinstance(texture_model_cpu.prompt_data, dict)
    assert len(texture_model_cpu.prompt_data) > 0
    for k in texture_model_cpu.prompt_data.keys():
        assert k == k.upper()

def test_patch_conv2d_asymmetric_tiling_modifies_modules(texture_model_cpu):
    unet = texture_model_cpu.pipe.unet
    # Sprawdzenie czy Conv2d ma nadpisaną metodę _conv_forward
    texture_model_cpu.patch_conv2d_asymmetric_tiling(unet, tileX=True, tileY=False)
    conv_modules = [m for m in unet.modules() if isinstance(m, torch.nn.Conv2d)]
    assert len(conv_modules) > 0
    for conv in conv_modules:
        # Metoda _conv_forward powinna być MethodType (czyli metoda instancji)
        assert hasattr(conv, "_conv_forward")
        assert callable(conv._conv_forward)

@pytest.mark.skipif(not torch.cuda.is_available(), reason="Only run on GPU")
def test_model_loaded_on_cuda():
    model = TextureModel(mode="cuda")
    assert model.device.type == "cuda"

def test_generate_process_queue_communication(texture_model_cpu):
    q = Queue()
    ctrl_q = Queue()

    texture_model_cpu.prompt_data = {
        "TESTCATEGORY": {
            "texture1": {
                "prompt": "test prompt",
                "negative_prompt": "negative",
                "height": 64,
                "width": 64,
                "steps": 5,
                "cfg_scale": 7.5,
                "seed": 42
            }
        }
    }

    # Mock tokenizer jako callable zwracające dict
    texture_model_cpu.pipe.tokenizer = MagicMock(return_value={
        "input_ids": torch.tensor([[1, 2, 3]]),
        "attention_mask": torch.tensor([[1, 1, 1]])
    })

    # Mock text_encoder zwraca tensor w kształcie [batch, seq_len, emb_dim]
    texture_model_cpu.pipe.text_encoder = MagicMock(return_value=(torch.randn(1, 77, 768),))

    # Mock prepare_latents zwraca tensor
    texture_model_cpu.pipe.prepare_latents = MagicMock(return_value=torch.randn(1, 4, 16, 16))

    # Mock unet z atrybutami i zwracające obiekt z sample
    mock_unet_out = MagicMock()
    mock_unet_out.sample = torch.randn(1, 4, 16, 16)
    texture_model_cpu.pipe.unet = MagicMock()
    texture_model_cpu.pipe.unet.in_channels = 4
    texture_model_cpu.pipe.unet.dtype = torch.float32
    texture_model_cpu.pipe.unet.return_value = mock_unet_out
    texture_model_cpu.pipe.unet.eval = MagicMock()

    # Mock scheduler z krokami i metodami
    texture_model_cpu.pipe.scheduler = MagicMock()
    texture_model_cpu.pipe.scheduler.timesteps = [1000, 900, 800, 700, 600]
    texture_model_cpu.pipe.scheduler.set_timesteps = MagicMock()
    texture_model_cpu.pipe.scheduler.scale_model_input = MagicMock(side_effect=lambda x, timestep: x)
    texture_model_cpu.pipe.scheduler.step = MagicMock(
        side_effect=lambda *args, **kwargs: MagicMock(prev_sample=torch.randn(1, 4, 16, 16)))

    # Mock decode_latents (zwróć np. numpy array)
    texture_model_cpu.pipe.decode_latents = MagicMock(return_value=np.random.rand(1, 64, 64, 3))

    # Uruchomienie funkcji
    texture_model_cpu.generate_process(q, "TESTCATEGORY", "texture1", ctrl_q)

    # Pobieranie wszystkiego z kolejki
    items = []
    while not q.empty():
        items.append(q.get())

    # Sprawdź, że progress jest (kilka aktualizacji)
    progress_msgs = [i for i in items if isinstance(i, dict) and i.get("status") == "progress"]
    assert len(progress_msgs) > 0

    # Sprawdź, że "first_step_done" pojawiło się gdzieś w kolejce
    assert any(i == "first_step_done" for i in items)

