@echo off
cd /d "%~dp0"
if not exist venv (
    echo Venv nie istnieje! Uruchom setup_venv.bat
    pause
    exit /b 1
)
call venv\Scripts\activate.bat
python preload_texture_generator.py --mode cuda --num-per-category 30 --output output/textures_30.json
pause

