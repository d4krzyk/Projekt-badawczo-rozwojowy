FROM nvidia/cuda:12.1.1-cudnn8-runtime-ubuntu22.04

# X11 + system deps for GTK
ENV DEBIAN_FRONTEND=noninteractive
RUN apt-get update && apt-get install -y \
    python3.11 python3.11-venv python3.11-dev \
    python3-pip build-essential libgtk-3-dev libglib2.0-dev \
    libsm6 libxrender1 libxext6 libx11-6 libgirepository1.0-dev \
    gir1.2-gtk-3.0 libcairo2-dev pkg-config \
    && rm -rf /var/lib/apt/lists/*

# Symlink Python
RUN update-alternatives --install /usr/bin/python python /usr/bin/python3.11 1
RUN python -m pip install --upgrade pip

# Ustaw katalog roboczy
WORKDIR /opt/project
COPY . .

# Instaluj pakiety
RUN pip install --no-cache-dir -r requirements.txt

# Domyœlna komenda (zmienisz to w PyCharm i tak)
CMD ["python"]
