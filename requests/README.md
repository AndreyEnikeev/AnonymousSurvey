# Коллекция запросов к AnonymousSurvey

Запросы ко всем реализованным эндпоинтам. Работают на тестовых данных из `db/seed.sql`.

## Как запустить

1. Установите расширение:
   - **Visual Studio 2022+:** `.http` файлы поддерживаются из коробки.
   - **VS Code:** установите расширение `REST Client` (Huachao Mao).
2. Откройте `auth.http`, отправьте запрос **«Login (Student)»**. Скопируйте `accessToken` из ответа.
3. Вставьте токен в переменную `@token` в нужных файлах (`surveys.http`, `admin.http`, `analytics.http`).
4. Для админских запросов: отправьте **«Login (Admin)»** в `auth.http`, скопируйте новый токен, вставьте в `admin.http`.
5. Запускайте остальные запросы по одному.

## Тестовые учётки

| Логин | Пароль | Роль |
|-------|--------|------|
| student1 | Student1!Pass | student |
| student2 | Student2!Pass | student |
| teacher1 | Teacher1!Pass | teacher |
| admin1 | Admin1!Pass | admin |
| tech1 | Tech1!Pass | tech |

## Покрытие эндпоинтов

| # | Метод | Путь | Файл |
|---|-------|------|------|
| 1 | POST | `/api/auth/login` | auth.http |
| 2 | POST | `/api/auth/refresh` | auth.http |
| 3 | POST | `/api/auth/logout` | auth.http |
| 4 | POST | `/api/auth/password-reset` | auth.http |
| 5 | POST | `/api/auth/password-reset/confirm` | auth.http |
| 6 | GET | `/api/audit/events` | admin.http |
| 7 | GET | `/api/surveys/active` | surveys.http |
| 8 | GET | `/api/surveys/{id}` | surveys.http |
| 9 | POST | `/api/surveys/{id}/submit` | surveys.http |
| 10 | GET | `/api/my-surveys` | surveys.http |
| 11 | GET | `/api/admin/periods` | admin.http |
| 12 | POST | `/api/admin/periods` | admin.http |
| 13 | PATCH | `/api/admin/periods/{id}` | admin.http |
| 14 | GET | `/api/admin/surveys` | admin.http |
| 15 | GET | `/api/admin/surveys/{id}` | admin.http |
| 16 | POST | `/api/admin/surveys` | admin.http |
| 17 | PUT | `/api/admin/surveys/{id}` | admin.http |
| 18 | GET | `/api/admin/surveys/dictionaries` | admin.http |
| 19 | PATCH | `/api/admin/surveys/{id}/status` | admin.http |
| 20 | GET | `/api/reports/aggregated` | analytics.http |
| 21 | POST | `/api/admin/export` | analytics.http |
| 22 | POST | `/api/internal/llm/classify` | llm.http (внутренний) |

## Позитив и негатив

В `security-checks.http` — три обязательных проверочных запроса из ЛР2 п.7.2:
1. Запрос объекта другого пользователя (403).
2. Создание объекта с лишними полями (игнорируются сервером).
3. Привилегированная операция обычным токеном (403).

В остальных файлах есть примеры на 400, 401, 404, 409 — они помечены комментарием.