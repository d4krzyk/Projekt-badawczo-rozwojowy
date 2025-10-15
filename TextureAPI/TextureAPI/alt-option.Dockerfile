FROM nvidia/cuda:12.1.1-cudnn8-runtime-ubuntu22.04


# Instalacja Python 3.11 i ustawienie symlinka
RUN apt-get update && \
    apt-get install -y python3.11 python3.11-venv python3.11-distutils && \
    update-alternatives --install /usr/bin/python python /usr/bin/python3.11 1

RUN python -m pip install --upgrade pip

# Ustaw katalog roboczy
WORKDIR /opt/project
COPY . .

# Instaluj pakiety
RUN pip install --no-cache-dir -r requirements.txt

# Domy�lna komenda (zmienisz to w PyCharm i tak)
CMD ["python"]
