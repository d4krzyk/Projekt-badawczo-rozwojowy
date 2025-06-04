FROM python:3.11-slim

# Instalacja GTK, PyGObject, cairo itp.
RUN apt-get update && apt-get install -y --no-install-recommends \
    python3-gi \
    gir1.2-gtk-3.0 \
    libcairo2-dev \
    libgtk-3-dev \
    pkg-config \
    build-essential \
    python3-dev \
    libglib2.0-dev \
    libgirepository1.0-dev \
    cmake \
    git \
 && rm -rf /var/lib/apt/lists/*

# Ustaw zmienn¹ œrodowiskow¹ do X11
ENV DISPLAY=host.docker.internal:0.0

WORKDIR /app

# Instalacja pycairo i PyGObject osobno, ¿eby unikn¹æ problemów z binarkami
RUN pip install --upgrade pip && \
    pip install pycairo && \
    pip install PyGObject==3.44.1


# Instaluj torch i torchvision z CUDA 12.1 z oficjalnego repozytorium PyTorch
RUN pip install --no-cache-dir torch==2.1.0+cu121 torchvision==0.16.0+cu121 --extra-index-url https://download.pytorch.org/whl/cu121


# Kopiuj i instaluj zale¿noœci z pliku requirements.txt
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# Skopiuj resztê projektu
COPY . /app

# Domyœlny punkt wejœcia
CMD ["python", "main.py"]