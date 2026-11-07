# anon-survey — система анонимного анкетирования

Реализация ЛР №3 (СПбПУ, вариант 7). Проект реализует систему из ЛР №2.

## Требования
- Docker Desktop 4+ (WSL2 backend на Windows)
- .NET SDK 8.0
- Visual Studio 2022
- Git

## Быстрый старт
    cp .env.example .env
    # Отредактируйте .env, замените все change_me_* на свои значения
    docker compose -f docker-compose.infra.yml up -d

| Сервис          | Адрес                     |
| --------------- | ------------------------- |
| PostgreSQL      | localhost:5432            |
| Redis           | localhost:6379            |
| RabbitMQ (AMQP) | localhost:5672            |
| RabbitMQ (UI)   | http://localhost:15672    |
| MinIO (S3 API)  | http://localhost:9000     |
| MinIO (Console) | http://localhost:9001     |
| MailHog (SMTP)  | localhost:1025            |
| MailHog (UI)    | http://localhost:8025     |

## Тестовые учётные записи
| Логин    | Пароль        | Роль    |
| -------- | ------------- | ------- |
| student1 | Student1!Pass | student |
| student2 | Student2!Pass | student |
| teacher1 | Teacher1!Pass | teacher |
| admin1   | Admin1!Pass   | admin   |
| tech1    | Tech1!Pass    | tech    |

## Остановка
    docker compose -f docker-compose.infra.yml down

## Сброс базы
    docker compose -f docker-compose.infra.yml down -v
    docker compose -f docker-compose.infra.yml up -d

## Структура
- `services/Gateway/` — API Gateway
- `services/Auth/` — Сервис аутентификации
- `services/Surveys/` — Сервис анкетирования
- `services/Analytics/` — Сервис аналитики
- `services/Notifications/` — Сервис уведомлений
- `services/Contracts/` — общие DTO и .proto
- `web/` — веб-клиент
- `db/` — схема БД, роли, seed-данные
- `docs/discrepancies.md` — журнал расхождений

## Журнал расхождений
См. `docs/discrepancies.md`. На данный момент — два расхождения (D-01, D-02), оба следствие требований ЛР №3.