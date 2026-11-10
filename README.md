# AnonymousSurvey — Система анонимного анкетирования

Реализация лабораторной работы №3 (СПбПУ, вариант 7). Проект представляет собой микросервисную систему для анонимного анкетирования по качеству преподавания.

Система полностью контейнеризирована и запускается одной командой через Docker Compose.

## 📋 Требования

Для запуска системы вам понадобятся:
- **Docker Desktop** (версия 4.0 или выше). 
  - *Для Windows/Mac: обязательно включите WSL2 backend в настройках Docker.*
- **Git** (для клонирования репозитория).
- *Опционально для разработки:* .NET SDK 8.0 и Visual Studio 2022.

## 🚀 Быстрый старт (Запуск за 2 минуты)

1. **Клонируйте репозиторий:**
   ```bash
   git clone <URL_ВАШЕГО_РЕПОЗИТОРИЯ>
   cd AnonymousSurvey
   ```

2. **Создайте файл конфигурации `.env`:**
   В корне проекта лежит файл `.env.example`. Скопируйте его и переименуйте в `.env`.
   
   *Для Windows (PowerShell):*
   ```powershell
   Copy-Item .env.example .env
   ```
   *Для Linux/macOS:*
   ```bash
   cp .env.example .env
   ```

3. **Настройте секреты в `.env`:**
   Откройте созданный файл `.env` и замените все заглушки `change_me_*` на свои значения. 
   **Обязательно** задайте пароли для баз данных, Redis, RabbitMQ, а также секреты `JWT_SIGNING_KEY` и `ARGON2_PEPPER`.

4. **Запустите систему:**
   Выполните команду в терминале из корня проекта:
   ```bash
   docker compose up -d --build
   ```
   *Первый запуск может занять 2–5 минут, так как Docker скачает образы и соберёт C#-сервисы.*

5. **Дождитесь инициализации:**
   При первом запуске PostgreSQL автоматически создаст таблицы и тестовые данные, а сервис `Auth` сгенерирует реальные Argon2id-хэши для паролей. Это занимает около 30 секунд. Проверить статус можно командой `docker compose ps` (все контейнеры должны быть `Up`, кроме `minio-init`, который завершится со статусом `Exited (0)`).

## 🌐 Адреса и порты

После успешного запуска сервисы доступны по следующим адресам:

| Сервис | Адрес | Примечание |
|--------|-------|------------|
| **WebClient (UI)** | http://localhost:5000 | Главная точка входа для пользователей (BFF) |
| **API Gateway** | http://localhost:8080 | Для прямых запросов к API (например, Postman) |
| **MailHog (UI)** | http://localhost:8025 | Веб-интерфейс для чтения писем |
| **RabbitMQ (UI)** | http://localhost:15672 | Логин/пароль из `.env` (по умолчанию `survey`) |
| **MinIO (Console)** | http://localhost:9001 | Хранилище файлов отчётов (логин/пароль из `.env`) |
| **PostgreSQL** | localhost:5432 | База данных (для pgAdmin или DBeaver) |
| **Redis** | localhost:6379 | Кэш и сессии |

## 🔑 Тестовые учётные записи

Система предварительно заполнена тестовыми пользователями (см. `db/seed.sql`). Пароли хэшируются при первом запуске.

| Логин | Пароль | Роль |
|-------|--------|------|
| `student1` | `Student1!Pass` | Студент |
| `student2` | `Student2!Pass` | Студент |
| `student3` | `Student3!Pass` | Студент |
| `teacher1` | `Teacher1!Pass` | Преподаватель |
| `teacher2` | `Teacher2!Pass` | Преподаватель |
| `admin1` | `Admin1!Pass` | Администратор |
| `tech1` | `Tech1!Pass` | Технический специалист |

## 🛠️ Полезные команды

**Остановить систему (данные сохраняются):**
```bash
docker compose down
```

**Полностью сбросить систему (удалить БД и все данные):**
```bash
docker compose down -v
docker compose up -d
```
*Флаг `-v` удаляет именованные тома (volumes), при следующем запуске БД создастся с нуля.*

**Посмотреть логи конкретного сервиса (например, Auth):**
```bash
docker compose logs -f auth
```

## 📧 Как проверить отправку писем

В учебных целях реальная почта не используется. Все письма (например, при восстановлении пароля) перехватываются локальным сервером **MailHog**.
1. Зайдите на http://localhost:5000/Account/ForgotPassword и введите логин (например, `student1`).
2. Откройте http://localhost:8025 (MailHog).
3. В списке писем появится сообщение со ссылкой для сброса пароля. Ссылка будет вести на `http://localhost:5000/...`.

## 📂 Структура проекта

- `services/` — микросервисы на .NET 8:
  - `Gateway/` — API Gateway (YARP, JWT, Rate Limiting).
  - `Auth/` — Аутентификация (Argon2id, JWT, аудит).
  - `Surveys/` — Управление анкетами и приём ответов.
  - `Analytics/` — Агрегация данных и экспорт в Excel (MinIO).
  - `Notifications/` — Фоновый worker для отправки писем (MassTransit).
  - `LlmOrchestrator/` — Заглушка для интеграции с LLM (отдаёт 503).
- `WebClient/` — Клиентское приложение (Razor Pages, BFF-паттерн).
- `db/` — Скрипты инициализации БД (`schema.sql`, `roles.sql`, `seed.sql`).
- `docs/` — Документация, включая `discrepancies.md` (журнал расхождений с ЛР2).
- `docker-compose.yml` — Файл сборки всей системы.
- `.env.example` — Шаблон переменных окружения.

## 📝 Для локальной разработки (Visual Studio)

Если вы хотите отлаживать C#-сервисы в Visual Studio, а инфраструктуру держать в Docker:

1. **Запустите инфраструктуру** через отдельный файл (только Postgres, Redis, RabbitMQ, MinIO, MailHog):
   ```powershell
   docker compose -f docker-compose.infra.yml up -d
   ```

2. **Настройте User Secrets** (правой кнопкой по проекту → Manage User Secrets). Строки подключения берите из `docker-compose.yml`, но замените имена контейнеров на `localhost` (например, `Host=localhost;Port=5432...`). 
   *   `services/Auth`: `Jwt:SigningKey`, `Argon2:Pepper`, строки подключения (`AuthDb`, `Redis`, `RabbitMq`).
   *   `services/Gateway`: `Jwt:SigningKey`, `Redis`.
   *   `services/Surveys`: `Jwt:SigningKey`, `SurveysDb`, `Redis`, `RabbitMq` (Host, Username, Password).
   *   `services/Analytics`: `Jwt:SigningKey`, `Jwt:Issuer`, `Jwt:Audience`, `AnalyticsDb`, `Redis`, `MinIO` (Endpoint, AccessKey, SecretKey, BucketName).
   *   `services/Notifications`: `NotificationsDb`, `RabbitMq` (Host, Username, Password), `Smtp` (Host, Port, FromAddress, FromName, UseSsl).
   *   `services/LlmOrchestrator`: `LlmDb`, `RabbitMq` (Host, Username, Password), `LlmProvider:ApiUrl`.
   *   `WebClient`: `Redis:ConnectionString`.

3. **Настройте мультизапуск** (правой кнопкой по решению → Configure Startup Projects → Multiple startup projects):
   *   Установите **Start** для всех проектов.
   *   **Порядок:** сначала `Auth` (он генерирует пароли), затем `Surveys`, `Analytics`, `LlmOrchestrator`, `Notifications`. `Gateway` — предпоследним, `WebClient` — последним.
   *   **Порты:** Auth (5135), Surveys (5136), Analytics (5137), LlmOrchestrator (5138), Gateway (8080), WebClient (5087).

> ⚠️ **Важно:** Нельзя одновременно запускать полный `docker compose up -d` (все контейнеры) и проекты в Visual Studio — возникнет конфликт портов (5432, 6379, 8080). Выбирайте что-то одно.