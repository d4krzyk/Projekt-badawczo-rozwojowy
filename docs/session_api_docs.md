# Dokumentacja API: Tworzenie Sesji Użytkownika

Dokument opisuje strukturę zapytań JSON oraz logikę przetwarzania danych przy tworzeniu nowej sesji w systemie.

## Endpoint
*   **Adres:** `/session/`
*   **Metoda:** `POST`
*   **Status sukcesu:** `201 Created`

## Nagłówki (Headers)
| Klucz | Wartość | Opis |
| :--- | :--- | :--- |
| `Content-Type` | `application/json` | Wymagany format danych. |
| `X-Web` | `true` lub `false` | (Opcjonalny) Określa typ sesji. Domyślnie `false` (aplikacja desktopowa). Jeśli `true`, sesja oznaczana jest jako webowa. |

---

## Struktura JSON (Request Body)

Głównym obiektem jest `FullSessionRequest`, który zawiera dane o użytkowniku oraz listę logów z pokojów.

### 1. Obiekt główny (`FullSessionRequest`)
| Pole | Typ | Opis |
| :--- | :--- | :--- |
| `user_name` | `string` | Unikalna nazwa użytkownika (identyfikator). |
| `session_logs` | `list[RoomLog]` | Lista aktywności pogrupowana według pokojów (kategorii). |

### 2. Log pokoju (`RoomLog`)
| Pole | Typ | Opis |
| :--- | :--- | :--- |
| `roomName` | `string` | Nazwa pokoju (np. kategoria Wikipedii). |
| `enterTime` | `float` | Offset czasu wejścia do pokoju w sekundach względem początku sesji. |
| `exitTime` | `float` | Offset czasu wyjścia z pokoju w sekundach względem początku sesji. |
| `bookLogs` | `list[BookLog]` | Lista otwieranych artykułów w danym pokoju. |
| `linkLogs` | `list[LinkLog]` | Lista klikniętych linków w danym pokoju. |

### 3. Log książki/artykułu (`BookLog`)
| Pole | Typ | Opis |
| :--- | :--- | :--- |
| `bookName` | `string` | Tytuł otwieranego artykułu. |
| `openTime` | `float` | Offset czasu otwarcia (sekundy od startu sesji). |
| `closeTime` | `float` | Offset czasu zamknięcia (sekundy od startu sesji). |

### 4. Log linku (`LinkLog`)
| Pole | Typ | Opis |
| :--- | :--- | :--- |
| `linkName` | `string` | Adres URL lub nazwa klikniętego odnośnika. |
| `clickTime` | `float` | Offset czasu kliknięcia (sekundy od startu sesji). |

---

## Logika Przetwarzania (Business Logic)

Podczas wysłania zapytania, serwer wykonuje następujące kroki:

1.  **Identyfikacja użytkownika:** System sprawdza, czy użytkownik o podanej nazwie (`user_name`) istnieje w bazie. Jeśli nie – tworzy nowy rekord dla tego użytkownika.
2.  **Ustalenie czasu bazowego:** Jako czas rozpoczęcia sesji (`start_time`) przyjmowany jest aktualny moment na serwerze w formacie **UTC**.
3.  **Obliczanie czasu zakończenia:** System bierze `exitTime` z ostatniego wpisu na liście `session_logs` i dodaje go do czasu startu sesji.
4.  **Konwersja offsetów na daty:** Wszystkie wartości `float` (enterTime, exitTime, openTime, clickTime itp.) są konwertowane na obiekty daty poprzez dodanie liczby sekund do `start_time` sesji.
5.  **Hierarchiczny zapis:**
    *   Tworzony jest rekord sesji (`UserSession`).
    *   Dla każdego wpisu w `session_logs` tworzony jest rekord pokoju (`Room`).
    *   Wewnątrz pokoju przypisywane są rekordy zdarzeń dla artykułów (`Book`) oraz linków (`Link`).
6.  **Transakcyjność:** Wszystkie operacje są wykonywane w ramach jednej transakcji bazodanowej. Jeśli cokolwiek pójdzie nie tak, cała sesja nie zostanie zapisana.

---

## Pełny przykład zapytania (JSON)

```json
{
    "user_name": "tester_01",
    "session_logs": [
        {
            "roomName": "Historia",
            "enterTime": 0,
            "exitTime": 120.5,
            "bookLogs": [
                {
                    "bookName": "Bitwa pod Grunwaldem",
                    "openTime": 10.0,
                    "closeTime": 110.0
                }
            ],
            "linkLogs": [
                {
                    "linkName": "https://pl.wikipedia.org/wiki/Władysław_II_Jagiełło",
                    "clickTime": 115.2
                }
            ]
        }
    ]
}
```
