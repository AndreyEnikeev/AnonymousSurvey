# Карта проекта AnonymousSurvey (ЛР №3)

> **Назначение.** Единая точка входа в проект: что за система, из каких
> компонентов состоит, как они связаны, что уже готово. Нужен, чтобы:
> (а) ввести в контекст нового человека (или ИИ-ассистента) одной ссылкой;
> (б) не потерять нить между сессиями разработки;
> (в) служить чек-листом перед защитой.
>
> **Не дублирует:** `discrepancies.md` (обоснование отклонений от ЛР2) и
> `FILE_TREE.md` (детальная структура файлов и эндпоинты).
>
> **Статус:** проект полностью разворачивается через `docker compose up -d --build`.

---

## 1. Паспорт проекта

| Поле | Значение |
|------|----------|
| Вариант | 7: Анонимное анкетирование по качеству преподавания |
| Студент | Еникеев А.В., группа 5140901/61501 |
| Дисциплина | Основы разработки безопасных цифровых систем |
| Работа | ЛР №3 — реализация и развёртывание системы из ЛР №2 |
| Дедлайн | 11.11.2026 |
| Стек | .NET 8, PostgreSQL 16, Redis 7, RabbitMQ 3, MinIO, MailHog |
| Архитектура | Микросервисы за YARP Gateway, клиент — Razor Pages (BFF) |
| Документы | `docs/discrepancies.md`, `docs/FILE_TREE.md`, этот файл |

---

## 2. Что готово (всё)

| Этап | Что | Статус |
|------|-----|--------|
| 0. Инфраструктура | `docker-compose.infra.yml`, `db/`, `.env` | ✅ |
| 1. Auth (C-03) | O-01, O-02, refresh, logout, O-12 | ✅ |
| 2. Gateway (C-02) | YARP, JWT, rate-limiting | ✅ |
| 3. Surveys (C-04) | O-04, O-05, O-06, O-08, O-09, O-13, D-19, D-20 | ✅ |
| 4. Notifications (C-07) | 2 консьюмера, MailKit → MailHog | ✅ |
| 5. Analytics (C-05) | O-07, O-10 (Excel + MinIO) | ✅ |
| 6. WebClient (C-01) | BFF: HttpOnly cookie + Redis | ✅ |
| 7. LlmOrchestrator (C-06) | Заглушка 503 (D-01) | ✅ |
| 8. Docker Compose | `docker-compose.yml` — 6 инфра + 7 C#-сервисов | ✅ |

---

## 3. Архитектура: контейнеры, порты

### 3.1. C#-сервисы

| ID | Сервис | Порт VS | Порт Docker | Каталог |
|----|--------|---------|-------------|---------|
| C-01 | WebClient | 5087 | 8080 → **5000** (наружу) | `WebClient/` |
| C-02 | Gateway | 8080 | 8080 → **8080** (наружу) | `services/Gateway/` |
| C-03 | Auth | 5135 | 8080 (внутри) | `services/Auth/` |
| C-04 | Surveys | 5136 | 8080 (внутри) | `services/Surveys/` |
| C-05 | Analytics | 5137 | 8080 (внутри) | `services/Analytics/` |
| C-06 | LlmOrchestrator | 5138 | 8080 (внутри) | `services/LlmOrchestrator/` |
| C-07 | Notifications | — (Worker) | — | `services/Notifications/` |

### 3.2. Инфраструктура

| Сервис | Порт | Назначение |
|--------|------|-----------|
| `postgres` | 5432 | БД `survey_db`, 5 ролей |
| `redis` | 6379 | Сессии, refresh, кэш, rate-limit, idempotency |
| `rabbitmq` | 5672 / 15672 | События MassTransit |
| `minio` | 9000 / 9001 | Отчёты (bucket `reports`) |
| `mailhog` | 1025 / 8025 | SMTP-заглушка + UI |

### 3.3. Связи

```
Браузер → WebClient (5000) → Gateway (8080) → Auth, Surveys, Analytics, LLM
                                               ↓
                                    PostgreSQL, Redis, RabbitMQ, MinIO
                                               ↑
                                          Notifications (Worker)
```

WebClient обращается **только к Gateway**, никогда — к сервисам напрямую.

---

## 4. Ключевые архитектурные решения

| ID | Решение | Где отражено |
|----|---------|--------------|
| D-01 | LLM исключён (сервис-заглушка 503) | `LlmOrchestrator/` |
| D-02 | MailHog вместо реального SMTP | `docker-compose.yml` |
| D-03 | HTTP вместо gRPC внутри Docker | `docker-compose.yml` |
| D-11 | `full_name` в `users` | `db/schema.sql` |
| D-13 | `POST /api/auth/refresh` | `Auth/Controllers/AuthController.cs` |
| D-14 | Password reset через RabbitMQ | `Auth` → `Notifications` |
| D-15 | `full_name` в JWT claim | `TokenService.cs` |
| D-16 | Проверка сессии в Redis | `WebClient/Program.cs` |
| D-17 | `periodName` в O-04/O-06 | `SurveyListItemDto` |
| D-18 | Названия сущностей в O-07 | `AggregatedReportItemDto` |
| D-19 | CRUD шаблонов анкет | `AdminSurveysController.cs` |
| D-20 | Создание периодов | `SurveysController.cs` |
| D-21 | Таблица `one_time_tokens` | `db/schema.sql` |
| D-22 | `ballots.submitted_date` — только дата | `db/schema.sql` |
| D-23 | `full_name` для `surveys_service` | `db/roles.sql` |
| D-24 | `GRANT SELECT (id)` на `audit_events` | `db/roles.sql` |

Полные обоснования — в `discrepancies.md`.

---

## 5. Анонимность

- `voter_checkins` знает **кто** голосовал (без содержания).
- `ballots` знает **что** отвечено (без автора).
- **FK между ними нет намеренно.**
- `ballots.submitted_date` — только дата (D-22).
- В логи и аудит **не пишется** содержание ответов.

---

## 6. Роли и доступ

| `role_id` | Роль | Тестовые учётки |
|-----------|------|-----------------|
| 1 | student | `student1..3` / `StudentN!Pass` |
| 2 | teacher | `teacher1..2` / `TeacherN!Pass` |
| 3 | admin | `admin1` / `Admin1!Pass` |
| 4 | tech | `tech1` / `Tech1!Pass` |

Проверка роли — `[Authorize(Roles = "3")]` и аналоги. В JWT claim — `role` (D-15).

---

## 7. Эндпоинты по контрактам

Полный список — в `FILE_TREE.md`. Краткая карта:

| O-код | Метод | Путь | Доступ |
|-------|-------|------|--------|
| O-01 | POST | `/api/auth/login` | все |
| O-02 | POST | `/api/auth/password-reset` + `/confirm` | все |
| O-04 | GET | `/api/surveys/active` | student |
| O-05 | POST | `/api/surveys/{id}/submit` | student |
| O-06 | GET | `/api/my-surveys` | student |
| O-07 | GET | `/api/reports/aggregated` | teacher, admin |
| O-08 | GET | `/api/admin/periods` | admin |
| O-09 | PATCH | `/api/admin/periods/{id}` | admin |
| O-10 | POST | `/api/admin/export` | admin |
| O-12 | GET | `/api/audit/events` | admin, tech |
| O-13 | GET | `/api/surveys/{id}` | student |
| D-13 | POST | `/api/auth/refresh` | все (refresh) |
| D-19 | CRUD | `/api/admin/surveys/*` | admin |
| D-20 | POST | `/api/admin/periods` | admin |

---

## 8. Как перезапустить ассистента

Если начинаешь новую сессию с ИИ-ассистентом:

1. Приложи **этот файл** (`docs/PROJECT_MAP.md`) целиком.
2. Приложи **`docs/discrepancies.md`** — там обоснования.
3. Скажи, на какой задаче остановился (актуально: остались `requests/`, PDF-отчёт, git-история).
4. Если правил код — приложи изменённые файлы целиком (не кусками).

**Ключевые фразы для контекста:**
- «Стиль: XML-комментарии на каждом методе, файлы целиком, объясняй каждую строку — я буду отвечать на защите сам.»
- «DTO: `init` для ответов, `set` для запросов. Entity: `set` везде.»
- «Секреты только в `.env` / User Secrets.»
- «Анонимность: `voter_checkins` / `ballots` без FK — не добавлять.»

---

## 9. Что осталось до сдачи

- `requests/` — коллекция `.http`-файлов (Задача B).
- `docs/REPORT_LR3.pdf` — итоговый отчёт (Задача E).
- git-история — осмысленные коммиты (Задача F).
- Финальный прогон по чек-листу.
