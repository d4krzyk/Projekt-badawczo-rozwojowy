```
BEGIN;

-- Aktualizacja referencji w user_sessions
UPDATE public.user_sessions SET group_id = 4 WHERE group_id = 5;
UPDATE public.user_sessions SET group_id = 6 WHERE group_id = 10;

-- Usunięcie starych grup
DELETE FROM public.groups WHERE id IN (5, 10);

COMMIT;
```