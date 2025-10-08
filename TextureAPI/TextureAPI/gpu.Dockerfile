FROM nvidia/cuda:12.1.1-cudnn8-runtime-ubuntu22.04

RUN apt-get update && apt-get install -y --no-install-recommends \
    python3-pip \
    python3-dev \
    git \
    cmake \
    build-essential \
    libglib2.0-dev \
    libgirepository1.0-dev \
    libcairo2-dev \
    libgtk-3-dev \
    pkg-config \
 && rm -rf /var/lib/apt/lists/*

# Ustaw zmienn¹ œrodowiskow¹ do X11
ARG DISPLAY_HOST=host.docker.internal:0.0
ENV DISPLAY=$DISPLAY_HOST

WORKDIR /app

RUN pip install --upgrade pip && \
    pip install --extra-index-url https://download.pytorch.org/whl/cu121 torch torchvision && \
    pip install pycairo PyGObject==3.44.1

COPY requirements.txt .  
RUN pip install --no-cache-dir -r requirements.txt

COPY . .

CMD ["python3", "main.py"]

