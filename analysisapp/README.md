# Analysis Application

Modułowa aplikacja do analizy danych z możliwością łatwego rozszerzania o nowe narzędzia analityczne.

## Architektura

Aplikacja wykorzystuje wzorzec modularny typu plugin, gdzie każde narzędzie analityczne jest osobnym modułem z jednolitym interfejsem.

```
analysisapp/
├── __init__.py              # Inicjalizacja pakietu
├── main.py                  # Punkt wejścia z systemem menu
├── app_config.py            # Zarządzanie konfiguracją
├── analyzers/               # Katalog modułów analizy
│   ├── __init__.py         # Rejestr analizatorów
│   ├── base_analyzer.py    # Klasa bazowa dla analizatorów
│   ├── hexmap_analyzer.py  # Wizualizacja hexmap
│   └── session_analyzer.py # Analiza sesji
└── README.md               # Dokumentacja
```

## Instalacja

Aplikacja wymaga następujących zależności:
- Python 3.7+
- tkinter
- matplotlib
- numpy
- PIL (Pillow)

## Użycie

### Tryb interaktywny (menu)

Uruchom aplikację z menu:

```bash
cd analysisapp
python main.py
```

Zostanie wyświetlone menu z dostępnymi narzędziami:

```
============================================================
  DATA ANALYSIS APPLICATION
============================================================

Available Analysis Tools:

  1. Hexmap Visualizer
     Visualize path data using hexagonal heatmaps

  2. Session Analyzer
     Analyze and visualize session timeline data from JSON files

  0. Exit
============================================================
```

### Bezpośrednie uruchomienie narzędzia

Możesz również uruchomić konkretne narzędzie bezpośrednio:

```bash
cd analysisapp
python main.py hexmap
```

lub

```bash
cd analysisapp
python main.py session
```

## Dostępne narzędzia

### 1. Hexmap Visualizer

Wizualizuje dane ścieżek przy użyciu mapy cieplnej z siatką heksagonalną.

**Wymagania:**
- Plik `paths.txt` w katalogu głównym projektu

**Funkcje:**
- Wyświetlanie mapy cieplnej z danymi ścieżek
- Przełączanie między różnymi ścieżkami
- Przełączanie widoczności siatki heksagonalnej
- Zapisywanie wizualizacji do pliku PNG

### 2. Session Analyzer

Analizuje i wizualizuje dane sesji z plików JSON.

**Wymagania:**
- Plik JSON z danymi sesji

**Funkcje:**
- Wizualizacja timeline sesji
- Interaktywne wykresy
- Szczegółowe informacje o eventach i pokojach

## Dodawanie nowych analizatorów

Aby dodać nowy analizator:

1. **Utwórz nowy plik** w katalogu `analyzers/`:
   ```python
   # analyzers/my_analyzer.py
   from .base_analyzer import BaseAnalyzer
   
   class MyAnalyzer(BaseAnalyzer):
       @property
       def name(self) -> str:
           return "My Analyzer"
       
       @property
       def description(self) -> str:
           return "Description of what my analyzer does"
       
       def run(self):
           # Implementation here
           print("Running my analyzer...")
   ```

2. **Zarejestruj analizator** w `analyzers/__init__.py`:
   ```python
   from .my_analyzer import MyAnalyzer
   
   ANALYZERS = [
       HexmapAnalyzer,
       SessionAnalyzer,
       MyAnalyzer,  # Dodaj tutaj
   ]
   ```

3. **Gotowe!** Nowy analizator automatycznie pojawi się w menu.

## Konfiguracja

Konfigurację można dostosować w pliku `app_config.py`:

- `paths_file` - Ścieżka do pliku paths.txt
- `default_json_dir` - Domyślny katalog dla plików JSON
- `window_width`, `window_height` - Domyślne rozmiary okien
- `hexmap_window_size` - Rozmiar okna dla hexmap visualizer

## Powiązane pliki

Aplikacja korzysta z następujących istniejących modułów:
- `visualizer.py` - Implementacja wizualizacji hexmap
- `Session_Wizard.py` - Implementacja analizy sesji