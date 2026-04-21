@echo off
echo.
echo 🔥 Aktualizacja PyTorch do wersji z pełnym support RTX 5070 Ti
echo.

cd /d "%~dp0"

if not exist venv (
    echo ❌ Venv nie istnieje!
    pause
    exit /b 1
)

echo Odinstalowanie PyTorch 2.5...
venv\Scripts\python.exe -m pip uninstall torch torchvision torchaudio -y

echo.
echo Instalacja PyTorch 2.6+ (nightly) z pełnym support RTX 5070 Ti...
venv\Scripts\python.exe -m pip install --pre torch torchvision torchaudio --index-url https://download.pytorch.org/whl/cu130

echo.
echo 🧪 Test CUDA...
venv\Scripts\python.exe -c "import torch; print('PyTorch:', torch.__version__); print('CUDA:', torch.cuda.is_available()); print('GPU:', torch.cuda.get_device_name(0) if torch.cuda.is_available() else 'BRAK')"

echo.
echo ✅ Aktualizacja zakończona!
echo.
pause

