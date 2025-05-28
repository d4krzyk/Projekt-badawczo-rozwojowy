# FastAPI + Alembic + Docker Compose – Quick Start


## Przydatne linki

- [OpenAPI](http://localhost/docs)
- [Redoc](http://localhost/redoc)
- [pgAdmin](http://localhost:5050)

## Najważniejsze informacje
1. Gdy zmieniasz modele – pamiętaj o 
```bash
alembic revision --autogenerate
alembic upgrade head
```

2. Gdy aplikacja Ci się nie uruchamia, spróbuj:
```bash
docker images
```
```bash
REPOSITORY                       TAG       IMAGE ID       CREATED         SIZE
projekt-badawczo-rozwojowy-api   latest    [...]            2 minutes ago   265MB
```
```bash
docker image rm [...]
docker compose up -d
docker ps
```
```bash
CONTAINER ID   IMAGE                            COMMAND                  CREATED         STATUS         PORTS                           NAMES
[...]            projekt-badawczo-rozwojowy-api   "uvicorn main:app --…"   5 minutes ago   Up 5 minutes   0.0.0.0:80->80/tcp              fastapi_backend
1e8672e25a7f   dpage/pgadmin4                   "/entrypoint.sh"         5 minutes ago   Up 5 minutes   443/tcp, 0.0.0.0:5050->80/tcp   pgadmin
c3ffe92bdac8   postgres:15                      "docker-entrypoint.s…"   5 minutes ago   Up 5 minutes   0.0.0.0:5432->5432/tcp          postgres_db
```
```bash
docker exec -it [...] bash
alembic upgrade head
exit
```
3. jak chcesz zobaczyć dlaczego nie działa Ci aplikacja to:
```bash
docker ps
```
```bash
CONTAINER ID   IMAGE                            COMMAND                  CREATED         STATUS         PORTS                           NAMES
[...]            projekt-badawczo-rozwojowy-api   "uvicorn main:app --…"   5 minutes ago   Up 5 minutes   0.0.0.0:80->80/tcp              fastapi_backend
1e8672e25a7f   dpage/pgadmin4                   "/entrypoint.sh"         5 minutes ago   Up 5 minutes   443/tcp, 0.0.0.0:5050->80/tcp   pgadmin
c3ffe92bdac8   postgres:15                      "docker-entrypoint.s…"   5 minutes ago   Up 5 minutes   0.0.0.0:5432->5432/tcp          postgres_db
```
```bash
docker logs -f [...]
```
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

