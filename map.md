# Дерево файлов AnonymousSurvey

> **Легенда:**
> ✅ реализовано и работает · 📝 требует заполнения для отчёта (коллекция запросов, PDF)
> 🔴 критически важно для защиты · 🟡 важно знать · ⚪ второстепенно
>
> **Источник структуры:** `AnonymousSurvey.sln`.
> Корень решения — `AnonymousSurvey/`.

---
## Корень решения

```
AnonymousSurvey/
├── AnonymousSurvey.sln              ✅ 🔴 решение
├── .env                             ✅ 🔴 секреты (в .gitignore)
├── .env.example                     ✅ 🔴 шаблон без секретов
├── .gitignore                       ✅ 🔴 в порядке
├── .gitattributes                   ✅ ⚪ в порядке
├── README.md                        ✅ 🔴 инструкция по запуску (Docker + VS)
├── docker-compose.infra.yml         ✅ 🔴 6 инфра-контейнеров (для отладки в VS)
└── docker-compose.yml               ✅ 🔴 полный стек (инфра + 7 C#-сервисов)
```

  **Важно про корень:**

  - 🔴 **`.env` и `.env.example`:** Содержат все необходимые пароли БД, Redis, RabbitMQ, MinIO, а также `JWT_SIGNING_KEY` и `ARGON2_PEPPER`. 
    Строки подключения (`ConnectionStrings__*`) собираются динамически внутри `docker-compose.yml` с использованием переменных из `.env`.

  - 🔴 **`README.md`:** Полностью переписан. Содержит инструкцию по быстрому запуску (Docker), таблицу портов, тестовые учётки, раздел про MailHog и инструкцию для локальной разработки в Visual Studio.

  - ⚪ **`.gitattributes`** — `*.cs eol=crlf` не мешает Docker-сборке. `*.sh eol=lf` — правильно.

  - ⚪ **`.gitignore`** — в порядке. `.env` игнорируется, `.env.example` — нет (`!.env.example`).

---

## db/ — инициализация БД

```
db/
├── init.sh                          ✅ 🔴 точка входа → /docker-entrypoint-initdb.d/
├── schema.sql                       ✅ 🔴 17 таблиц PostgreSQL 16
├── roles.sql                        ✅ 🔴 5 ролей + GRANT
└── seed.sql                         ✅ 🔴 7 пользователей + демо-данные
```

  **Важно про db/:**

  - **Порядок в `init.sh`:** `CREATE ROLE` (5 ролей) → `schema.sql` → 
    `roles.sql` (GRANT) → `seed.sql`. Менять порядок нельзя: GRANT 
    требует существующих таблиц, seed — существующих ролей.

  - **Роли (5 штук):** `auth_service`, `surveys_service`, 
    `analytics_service`, `notifications_service`, **`llm_orchestrator`** 
    (не `llm_service`!). Пароли — из `.env`.

  - **`schema.sql` — особенности относительно DDL ЛР2:**
    * `one_time_tokens` — **добавлена в ЛР3**, в DDL ЛР2 её не было. 
      Назначение: одноразовые токены для O-02 (password reset) (D-21).
    * `users.full_name` — D-11.
    * `ballots.submitted_date` — только дата, без времени. 
      Осознанное решение против деанонимизации по timestamp (D-22).
    * `llm_classifications.topics` — `jsonb` + GIN-индекс.
    * `audit_events.details` — `jsonb`.

  - **`seed.sql` — что важно знать:**
    * 7 пользователей: `student1..3`, `teacher1..2`, `admin1`, `tech1`.
    * `password_hash = 'PLACEHOLDER_HASH_...'` (длина 42 > 32 — 
      проходит `ck_users_hash_len`). Auth при первом старте заменяет 
      на Argon2id (`SeedPasswordsAutomatically` в `Program.cs`).
    * Демо-данные: 1 открытый период («Осень 2026»), 1 закрытый 
      («Весна 2026»), 3 шаблона (2 active, 1 closed), 9 вопросов, 
      3 бюллетеня + 3 voter_checkin (согласованы).

  - **`roles.sql` — что важно знать:**
    * **`surveys_service`:** `GRANT SELECT (id, login, full_name) ON users` (D-11). 
    * **`analytics_service`:** `GRANT INSERT ON audit_events` + `GRANT SELECT (id) ON audit_events` (D-08).
    * **`notifications_service`:** `GRANT INSERT ON audit_events` + `GRANT SELECT (id) ON audit_events` (D-08).
    * **`llm_orchestrator`:** `GRANT INSERT ON audit_events` + `GRANT SELECT (id)`, `GRANT INSERT ON llm_classifications` + `GRANT SELECT (id)`.
    * **`surveys_service`:** `GRANT INSERT, SELECT ON audit_events` — D-06, корректно.
    * **`auth_service`:** `GRANT SELECT, INSERT, UPDATE ON users` — DELETE нет.
    * **`REVOKE CREATE ON SCHEMA public FROM PUBLIC`** — обычные роли не могут создавать таблицы (требование ЛР3 п.2.2).

---

## docs/ — документация

```
docs/
├── discrepancies.md                 ✅ 🔴 D-01…D-24 (журнал расхождений)
├── PROJECT_MAP.md                   ✅ 🔴 карта проекта
├── FILE_TREE.md                     ✅ 🔴 этот файл
└── REPORT_LR3.pdf                   📝 ⚪ итоговый отчёт для сдачи
```

  **Важно про docs/:**
  - `discrepancies.md` — на первой странице PDF-отчёта — реестр изменений (summary из него).

---

## requests/ — коллекция запросов

```
requests/
├── README.md                        ✅ ⚪ описание коллекции
├── auth.http                        📝 🟡 login, refresh, logout, password-reset, confirm
├── surveys.http                     📝 🟡 active, get, submit, my-surveys
├── reports.http                     📝 🟡 aggregated, export
├── admin.http                       📝 🟡 periods, surveys (CRUD D-19), audit
└── security-checks.http             📝 🔴 3 проверочных запроса из ЛР2 п.7.2
```

  **Важно про requests/:**
  - Формат `.http` — запускается в VS/VS Code без Postman.
  - `security-checks.http` — артефакт для отчёта: запрос + ответ + фрагмент кода, который отбил.

---

## services/ — 6 микросервисов

```
services/
├── Auth/                            🔴 C-03, порт 5135 → 8080 в Docker
├── Gateway/                         🔴 C-02, порт 8080
├── Surveys/                         🔴 C-04, порт 5136 → 8080
├── Notifications/                   🔴 C-07, Worker (без порта)
├── Analytics/                       🔴 C-05, порт 5137 → 8080
└── LlmOrchestrator/                 🔴 C-06, порт 5138 → 8080 (заглушка 503)
```

  **Важно про services/:**
  - **WebClient сюда не входит** — он в корне (см. ниже).
  - У каждого сервиса — свой `.csproj`, `Program.cs`, `appsettings*.json`, `Dockerfile`, `.dockerignore`.

---

### services/Auth/ — C-03

```
services/Auth/
├── Auth.csproj                      ✅ 🔴
├── Program.cs                       ✅ 🔴 
├── appsettings.json                 ✅ 🔴 без секретов (только Jwt Issuer/Audience)
├── Dockerfile                       ✅ 🔴 создан
├── Properties/
│   └── launchSettings.json          ✅ ⚪ порт 5135
├── Contracts/
│   └── PasswordResetRequested.cs    ✅ 🔴 [EntityName], [MessageUrn]
├── Controllers/
│   ├── AuthController.cs            ✅ 🔴 O-01, O-02, refresh, logout, confirm
│   └── AuditController.cs           ✅ 🟡 O-12, фильтры, пагинация
├── Services/
│   ├── PasswordHasher.cs            ✅ 🔴 Argon2id (OWASP), Pepper
│   └── TokenService.cs              ✅ 🔴 JWT HMAC-SHA256, refresh в Redis (7 дней)
├── DataDb/
│   └── AppDbContext.cs              ✅ 🔴 Users, AuditEvents, OneTimeTokens
└── Models/
    ├── AuditEventDto.cs             ✅ 🟡
    ├── LoginRequest.cs              ✅ 🔴
    ├── LogoutRequest.cs             ✅ 🟡
    ├── PasswordResetRequest.cs      ✅ 🟡
    ├── RefreshRequest.cs            ✅ 🟡
    └── ResetPasswordRequest.cs      ✅ 🟡
```

  **Реализованные эндпоинты (все под `/api`):**
  *   `POST /auth/login` (O-01) — доступ: все, без токена.
  *   `POST /auth/refresh` (D-13) — доступ: все, без токена (требуется refresh-токен в теле).
  *   `POST /auth/logout` — доступ: все.
  *   `POST /auth/password-reset` (O-02) — доступ: все, без токена.
  *   `POST /auth/password-reset/confirm` — доступ: все, без токена (требуется токен сброса).
  *   `GET /audit/events` (O-12) — доступ: admin (3), tech (4).

  **User Secrets маппинг в Docker (env):**
  *   `Jwt:SigningKey` → `Jwt__SigningKey`
  *   `Argon2:Pepper` → `Argon2__Pepper`
  *   `ConnectionStrings:AuthDb` → `ConnectionStrings__AuthDb` (`Host=postgres`)
  *   `ConnectionStrings:Redis` → `ConnectionStrings__Redis` (`redis:6379`)
  *   `ConnectionStrings:RabbitMq` → `ConnectionStrings__RabbitMq` (`amqp://survey:...@rabbitmq:5672/`)

  **Важно знать:**
  - 🔴 **Seed паролей:** `SeedPasswordsAutomatically` работает без `#if DEBUG`. При старте в Docker он генерирует Argon2id-хэши для тестовых учёток.
  - 🔴 **Аудит:** `AuthController` пишет аудит через EF Core. Подтверждает необходимость `GRANT SELECT (id) ON audit_events` для `auth_service`.

---

### services/Gateway/ — C-02

```
services/Gateway/
├── Gateway.csproj                   ✅ 🔴 YARP + JWT Bearer
├── Program.cs                       ✅ 🔴 
├── appsettings.json                 ✅ 🔴 YARP-маршруты (для local dev)
├── Dockerfile                       ✅ 🔴 создан
├── Properties/
│   └── launchSettings.json          ✅ ⚪ порт 8080
└── Middleware/
    └── RedisRateLimitMiddleware.cs  ✅ 🟡 Fail-Open при падении Redis
```

  **Важно про Gateway/:**
  - 🔴 **Маршруты:** в `appsettings.json` настроены на `http://localhost:5135`. В Docker они переопределяются через переменные окружения `ReverseProxy__Clusters__...` внутри `docker-compose.yml`.
  - 🔴 **User Secrets маппинг в Docker:**
    * `Jwt:SigningKey` → `Jwt__SigningKey`
    * `ConnectionStrings:Redis` → `ConnectionStrings__Redis`
  - 🟡 **Rate Limiting:** собственный middleware (Fail-Open). 
    Лимит по умолчанию 60 в минуту, login — 10, password-reset — 5. 
    Ключ партиции — `userId` из JWT.

---

### services/Surveys/ — C-04

```
services/Surveys/
├── Surveys.csproj                   ✅ 🔴
├── Program.cs                       ✅ 🔴 
├── appsettings.json                 ✅ 🔴 без секретов
├── Dockerfile                       ✅ 🔴 создан
├── Properties/
│   └── launchSettings.json          ✅ ⚪ порт 5136
├── Contracts/
│   ├── LlmClassificationRequested.cs ✅ 🟡 [EntityName], [MessageUrn]
│   └── PeriodStatusChanged.cs       ✅ 🟡 [EntityName], [MessageUrn]
├── Controllers/
│   ├── SurveysController.cs         ✅ 🔴 O-04, O-05, O-06, O-08, O-09, O-13
│   └── AdminSurveysController.cs    ✅ 🟡 D-19 (CRUD)
├── DataDb/
│   └── AppDbContext.cs              ✅ 🔴 HasDefaultSchema, jsonb, связи
└── Models/
    ├── Dtos/                        ✅ 🔴 (все DTO)
    └── Entities/                    ✅ 🔴 (все сущности, включая Ballot, VoterCheckin)
```

  **Реализованные эндпоинты (все под `/api`):**
  *   **Студенты:**
      *   `GET /surveys/active` (O-04) — доступ: student (1).
      *   `GET /surveys/{surveyId}` (O-13) — доступ: student (1).
      *   `POST /surveys/{surveyId}/submit` (O-05) — доступ: student (1), требуется `Idempotency-Key`.
      *   `GET /my-surveys` (O-06) — доступ: student (1).
  *   **Администраторы (Periods):**
      *   `GET /admin/periods` (O-08) — доступ: admin (3).
      *   `POST /admin/periods` (D-20) — доступ: admin (3).
      *   `PATCH /admin/periods/{periodId}` (O-09) — доступ: admin (3).
  *   **Администраторы (Surveys CRUD - D-19):**
      *   `GET /admin/surveys` — доступ: admin (3).
      *   `GET /admin/surveys/{id}` — доступ: admin (3).
      *   `POST /admin/surveys` — доступ: admin (3).
      *   `PUT /admin/surveys/{id}` — доступ: admin (3).
      *   `GET /admin/surveys/dictionaries` — доступ: admin (3).
      *   `PATCH /admin/surveys/{id}/status` — доступ: admin (3).

  **User Secrets маппинг в Docker (env):**
  *   `Jwt:SigningKey` → `Jwt__SigningKey`
  *   `ConnectionStrings:SurveysDb` → `ConnectionStrings__SurveysDb` (`Host=postgres`)
  *   `ConnectionStrings:Redis` → `ConnectionStrings__Redis` (`redis:6379`)
  *   `RabbitMq:Host` → `RabbitMq__Host` (`rabbitmq`)
  *   `RabbitMq:Username` → `RabbitMq__Username` (из `.env`)
  *   `RabbitMq:Password` → `RabbitMq__Password` (из `.env`)

  **Важно знать:**
  - 🔴 **Анонимность:** `Ballot` не имеет `StudentId`. `VoterCheckin` имеет `StudentId`, но не имеет `BallotId`. Никаких FK между ними.
  - 🔴 **Idempotency:** Реализована через Redis в `SubmitSurvey`. Ключ `idemp:submit:{studentId}:{key}`.
  - 🔴 **Аудит:** Пишется через EF Core, нужен `GRANT SELECT (id) ON audit_events`.

---

### services/Analytics/ — C-05

```
services/Analytics/
├── Analytics.csproj                 ✅ 🔴 ClosedXML, Minio SDK
├── Program.cs                       ✅ 🔴 
├── appsettings.json                 ✅ 🔴 без секретов
├── Dockerfile                       ✅ 🔴 создан
├── Properties/
│   └── launchSettings.json          ✅ ⚪ порт 5137
├── Controllers/
│   └── ReportsController.cs         ✅ 🔴 O-07, O-10
├── Services/
│   └── MinioService.cs              ✅ 🔴 pre-signed URL, загрузка
├── DataDb/
│   └── AnalyticsDbContext.cs        ✅ 🔴
└── Models/
    ├── Dtos/                        ✅ 🔴
    └── Entities/                    ✅ 🔴
```

  **Реализованные эндпоинты (все под `/api`):**
  *   `GET /reports/aggregated` (O-07) — доступ: teacher (2), admin (3). 
      *   Преподаватель видит только свои отчёты (forcedTeacherId).
      *   Кэширование в Redis (TTL 5 мин, ключ включает userId).
  *   `POST /admin/export` (O-10) — доступ: admin (3). Требуется `Idempotency-Key`.
      *   Генерирует Excel через ClosedXML, загружает в MinIO, возвращает pre-signed URL.

  **User Secrets маппинг в Docker (env):**
  *   `Jwt:SigningKey` → `Jwt__SigningKey`
  *   `Jwt:Issuer` → `Jwt__Issuer`
  *   `Jwt:Audience` → `Jwt__Audience`
  *   `ConnectionStrings:AnalyticsDb` → `ConnectionStrings__AnalyticsDb` (`Host=postgres`)
  *   `ConnectionStrings:Redis` → `ConnectionStrings__Redis` (`redis:6379`)
  *   `MinIO:Endpoint` → `MinIO__Endpoint` (`http://minio:9000`)
  *   `MinIO:AccessKey` → `MinIO__AccessKey` (из `.env`)
  *   `MinIO:SecretKey` → `MinIO__SecretKey` (из `.env`)
  *   `MinIO:BucketName` → `MinIO__BucketName` (`reports`)

  **Важно знать:**
  - 🟡 **Аудит:** В коде нет записи в `audit_events` (в `AnalyticsDbContext` нет `AuditEvent`). 
    Права `GRANT INSERT ON audit_events` (D-08) выданы, но не используются. Это не ошибка, 
    просто задел на будущее.
  - 🔴 **MinIO:** Endpoint `http://localhost:9000` меняется на `http://minio:9000` в Docker.

---

### services/LlmOrchestrator/ — C-06

```
services/LlmOrchestrator/
├── LlmOrchestrator.csproj           ✅ 🔴 .NET 8.0
├── Program.cs                       ✅ 🔴 HTTP + MassTransit Consumer
├── appsettings.json                 ✅ 🔴 без секретов
├── Dockerfile                       ✅ 🔴 создан
├── Properties/
│   └── launchSettings.json          ✅ ⚪ порт 5138
├── Consumers/
│   └── LlmClassificationConsumer.cs ✅ 🔴 Обрабатывает LlmClassificationRequested, логирует сбой, retry
├── Controllers/
│   └── LlmController.cs             ✅ 🔴 O-11 (POST /api/internal/llm/classify)
├── Services/
│   └── LlmClient.cs                 ✅ 🔴 HttpClient, шлёт запрос на порт 9999 (падёт)
├── DataDb/
│   └── LlmDbContext.cs              ✅ 🔴 llm_classifications, audit_events
├── Contracts/
│   └── LlmClassificationRequested.cs ✅ 🔴 [EntityName], [MessageUrn]
└── Models/
    ├── Dtos/
    │   ├── LlmRequestDto.cs         ✅ 🟡
    │   └── LlmResponseDto.cs        ✅ 🟡
    ├── AuditEvent.cs                ✅ 🟡
    └── LlmClassification.cs         ✅ 🟡
```

  **Реализованные эндпоинты:**
  *   `POST /api/internal/llm/classify` (O-11) — доступ: без авторизации (внутренний). 
      Возвращает **503 Service Unavailable**, если внешний LLM недоступен (что и происходит всегда по D-01).

  **Фоновые обработчики (MassTransit):**
  *   `LlmClassificationConsumer` — слушает очередь `llm-classification-queue`. 
      При получении сообщения пытается вызвать LLM, ловит `HttpRequestException`, 
      пишет `llm_classification_failed` в `audit_events`, пробрасывает ошибку дальше, 
      чтобы MassTransit сделал ретрай (5 сек, 10 сек) и отправил в DLQ.

  **User Secrets маппинг в Docker (env):**
  *   `ConnectionStrings:LlmDb` → `ConnectionStrings__LlmDb` (`Host=postgres`)
  *   `RabbitMq:Host` → `RabbitMq__Host` (`rabbitmq`)
  *   `RabbitMq:VirtualHost` → `RabbitMq__VirtualHost` (`/`)
  *   `RabbitMq:Username` → `RabbitMq__Username` (из `.env`)
  *   `RabbitMq:Password` → `RabbitMq__Password` (из `.env`)
  *   `LlmProvider:ApiUrl` → `LlmProvider__ApiUrl` (оставляем `http://localhost:9999/api/classify`, чтобы гарантированно падало)

  **Важно знать:**
  - 🔴 **D-01:** Реальная интеграция исключена. Сервис работает как заглушка, 
    но *архитектурно* всё готово: консьюмер, клиент, БД, аудит.
  - 🔴 **Падение в Docker:** Запрос на `localhost:9999` внутри контейнера упадёт с `Connection refused`. 
    Это ожидаемое поведение, оно демонстрирует устойчивость системы (ошибка логируется, 
    сообщение ретраится, основной поток анкет не страдает).

---

### services/Notifications/ — C-07

```
services/Notifications/
├── Notifications.csproj             ✅ 🔴 MassTransit, MailKit
├── Program.cs                       ✅ 🔴 Worker, 2 консьюмера, ретраи
├── appsettings.json                 ✅ 🔴 WebClient:BaseUrl
├── Dockerfile                       ✅ 🔴 создан (на базе dotnet/runtime)
├── Properties/
│   └── launchSettings.json          ✅ ⚪
├── Consumers/
│   ├── PeriodStatusChangedConsumer.cs ✅ 🔴 Рассылка по ролям, аудит
│   └── PasswordResetRequestedConsumer.cs ✅ 🔴 Отправка письма со ссылкой
├── Contracts/
│   ├── PasswordResetRequested.cs    ✅ 🔴 [EntityName], [MessageUrn]
│   └── PeriodStatusChanged.cs       ✅ 🔴 [EntityName], [MessageUrn]
├── Services/
│   ├── IEmailSender.cs              ✅ 🟡 Интерфейс
│   └── SmtpEmailSender.cs           ✅ 🔴 MailKit
├── DataDb/
│   └── NotificationsDbContext.cs    ✅ 🔴 users, audit_events
└── Models/
    ├── AuditEvent.cs                ✅ 🟡
    └── User.cs                      ✅ 🟡
```

  **Реализованные эндпоинты (HTTP):**
  *   **Отсутствуют.** Это фоновый сервис (Worker).

  **Фоновые обработчики (MassTransit):**
  *   Слушает очередь `notifications-period-status-changed` (`PeriodStatusChangedConsumer`).
  *   Слушает очередь `notifications-password-reset-requested` (`PasswordResetRequestedConsumer`).
  *   Использует ретраи: 3 попытки (5с, 15с, 30с) при сбоях SMTP.

  **User Secrets маппинг в Docker (env):**
  *   `ConnectionStrings:NotificationsDb` → `ConnectionStrings__NotificationsDb`
  *   `RabbitMq:Host`, `Username`, `Password` → `RabbitMq__Host`, `RabbitMq__Username`, `RabbitMq__Password`
  *   `Smtp:Host` → `Smtp__Host` (`mailhog`)
  *   `Smtp:Port` → `Smtp__Port` (`1025`)
  *   `Smtp:FromAddress`, `FromName`, `UseSsl` → `Smtp__FromAddress`, `Smtp__FromName`, `Smtp__UseSsl`
  *   `WebClient:BaseUrl` → `WebClient__BaseUrl` (в Docker будет `http://localhost:5000`)

  **Важно знать:**
  - 🔴 **Аудит:** Пишет `email_notification_sent` и `password_reset_notification_sent`. 
    Подтверждает необходимость `GRANT SELECT (id) ON audit_events` (D-08).

---

## WebClient/ — C-01 (BFF)

```
WebClient/
├── WebClient.csproj                 ✅ 🔴
├── Program.cs                       ✅ 🔴 BFF (Cookie auth), Session middleware (D-16)
├── appsettings.json                 ✅ 🔴 Gateway:BaseUrl
├── Dockerfile                       ✅ 🔴 создан
├── Properties/
│   └── launchSettings.json          ✅ ⚪ порт 5087
├── Services/
│   ├── TokenStore.cs                ✅ 🔴 Redis `web:session:{sessionId}`
│   ├── GatewayApiClient.cs          ✅ 🔴 HTTP-клиент к Gateway
│   └── RefreshTokenHandler.cs       ✅ 🔴 DelegatingHandler, 401 → refresh
├── Pages/
│   ├── Account/
│   │   ├── Login.cshtml(.cs)        ✅ 🔴 O-01
│   │   ├── Logout.cshtml(.cs)       ✅ 🟡
│   │   ├── ForgotPassword.cshtml(.cs) ✅ 🟡 O-02
│   │   ├── ResetPassword.cshtml(.cs)  ✅ 🟡 O-02
│   │   └── Denied.cshtml(.cs)       ✅ ⚪
│   ├── Student/
│   │   ├── Surveys.cshtml(.cs)      ✅ 🔴 O-04
│   │   ├── Survey.cshtml(.cs)       ✅ 🔴 O-13 + O-05
│   │   └── MySurveys.cshtml(.cs)    ✅ 🟡 O-06
│   ├── Teacher/
│   │   └── Reports.cshtml(.cs)      ✅ 🔴 O-07
│   ├── Admin/
│   │   ├── Periods.cshtml(.cs)      ✅ 🔴 O-08, O-09, D-20
│   │   ├── Export.cshtml(.cs)       ✅ 🟡 O-10
│   │   ├── Audit.cshtml(.cs)        ✅ 🟡 O-12
│   │   └── Surveys/
│   │       ├── Index.cshtml(.cs)    ✅ 🟡 D-19
│   │       └── Edit.cshtml(.cs)     ✅ 🟡 D-19
│   ├── Shared/
│   │   ├── _Layout.cshtml           ✅ 🔴 меню по ролям
│   │   └── ...
│   └── ...
├── Models/
│   └── Dtos/                        ✅ 🔴
└── wwwroot/                         ✅ ⚪
```

  **Реализованные страницы (UI):**
  *   **Account:** Login, Logout, ForgotPassword, ResetPassword, Denied.
  *   **Student:** Surveys (O-04), Survey (O-13 + O-05), MySurveys (O-06).
  *   **Teacher:** Reports (O-07).
  *   **Admin:** Periods (O-08, O-09, D-20), Export (O-10), Audit (O-12), Surveys (Index, Edit - D-19).
  *   **System:** Index (главная), Error (404/500).

  **User Secrets маппинг в Docker (env):**
  *   `Redis:ConnectionString` → `Redis__ConnectionString` (`redis:6379,password=...`)
  *   `Gateway:BaseUrl` → `Gateway__BaseUrl` (`http://gateway:8080/`)

  **Важно знать:**
  - 🔴 **BFF-паттерн:** Токены хранятся в Redis, в браузере только HttpOnly cookie.
  - 🔴 **RefreshTokenHandler:** Для refresh использует `GatewayClientRaw` без handler'а (защита от рекурсии).
  - 🟡 **Cookie SecurePolicy:** `SameAsRequest`. В Docker (HTTP) будет `false`. 
    Если выкатывать в интернет — нужен Reverse Proxy (Nginx/Caddy) с HTTPS.
```