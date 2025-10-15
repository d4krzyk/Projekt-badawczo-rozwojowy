FROM nvidia/cuda:12.1.1-cudnn8-runtime-ubuntu22.04


# Symlink Python
RUN update-alternatives --install /usr/bin/python python /usr/bin/python3.11 1
RUN python -m pip install --upgrade pip

# Ustaw katalog roboczy
WORKDIR /opt/project
COPY . .

# Instaluj pakiety
RUN pip install --no-cache-dir -r requirements.txt

# Domy�lna komenda (zmienisz to w PyCharm i tak)
CMD ["python"]
