# FastAPI + Alembic + Docker Compose – Quick Start


## Przydatne linki

- [OpenAPI](http://localhost/docs)
- [Redoc](http://localhost/redoc)
- [pgAdmin](http://localhost:5050)

---

## 1. Uruchomienie środowiska

```bash
docker compose up -d
```
> Uruchomi aplikację (FastAPI) i bazę danych (PostgreSQL) w trybie detached.

```bash
docker compose down
```
> Wyłączy kontenery

## 2. Tworzenie migracji (Alembic)

**Dodaj nowe migracje automatycznie na podstawie zmian w modelach:**
```bash
alembic revision --autogenerate -m "Opis migracji"
```

## 3. Wykonywanie migracji (upgrade)

**Zaktualizuj bazę do najnowszego schematu:**
```bash
alembic upgrade head
```

## 4. Przywracanie do poprzedniej migracji (downgrade)

**Przywróć bazę do poprzedniej wersji:**
```bash
alembic downgrade -1
```

**Protip:**  
Gdy zmieniasz modele – pamiętaj o ```alembic revision --autogenerate``` i ```alembic upgrade head```.
