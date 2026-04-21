FROM nvidia/cuda:12.1.1-cudnn8-runtime-ubuntu22.04

# Instalacja Python 3.11 i pip
RUN apt-get update && \
    apt-get install -y python3.11 python3.11-venv python3.11-distutils curl && \
    update-alternatives --install /usr/bin/python python /usr/bin/python3.11 1 && \
    curl -sS https://bootstrap.pypa.io/get-pip.py -o get-pip.py && \
    python3.11 get-pip.py && \
    rm get-pip.py

# Ustaw katalog roboczy
WORKDIR /app

# Skopiuj tylko requirements.txt najpierw (szybszy build przy zmianach kodu)
COPY requirements.txt .

# Uaktualnij pip i zainstaluj paczki
RUN python3.11 -m pip install --upgrade pip
RUN python3.11 -m pip install --default-timeout=200 -r requirements.txt

# Teraz kopiuj cały projekt
COPY . .


# Domyślna komenda
CMD ["python3.11", "-m", "uvicorn", "main_api:app", "--host", "0.0.0.0", "--port", "8000"]
