#!/bin/bash
# ============================================================
# Инициализация PostgreSQL при первом старте контейнера.
# Запускается автоматически из /docker-entrypoint-initdb.d/
# ============================================================

# set -e означает: если любая команда упадет с ошибкой, 
# скрипт немедленно остановится (не будет создавать базу "наполовину").
set -e

echo "[init.sh] Ждём готовности PostgreSQL..."
# Хотя Docker запустил контейнер, сам Postgres внутри может загружаться пару секунд.
# Цикл ждет, пока утилита pg_isready не скажет, что база готова принимать подключения.
until pg_isready -U "$POSTGRES_USER" -d "$POSTGRES_DB"; do
  sleep 1
done
echo "[init.sh] PostgreSQL готов."

echo "[init.sh] Создаём роли сервисов..."
# Создаем отдельных пользователей для каждого микросервиса (принцип наименьших привилегий).
# Пароли подставляются из переменных окружения (файл .env).
psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" <<-EOSQL
    CREATE ROLE auth_service          LOGIN PASSWORD '${AUTH_DB_PASSWORD}';
    CREATE ROLE surveys_service       LOGIN PASSWORD '${SURVEYS_DB_PASSWORD}';
    CREATE ROLE analytics_service     LOGIN PASSWORD '${ANALYTICS_DB_PASSWORD}';
    CREATE ROLE notifications_service LOGIN PASSWORD '${NOTIFICATIONS_DB_PASSWORD}';
    CREATE ROLE llm_orchestrator WITH LOGIN PASSWORD '${LLM_DB_PASSWORD}';
EOSQL

echo "[init.sh] Применяем schema.sql..."
# Создаем структуру таблиц (DDL)
psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" -f /db/schema.sql

echo "[init.sh] Применяем roles.sql (GRANT)..."
# Раздаем права созданным ролям (GRANT). Это нужно делать ПОСЛЕ того, как таблицы созданы.
psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" -f /db/roles.sql

echo "[init.sh] Применяем seed.sql..."
# Заполняем базу тестовыми данными (пользователи, дисциплины)
psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" -f /db/seed.sql

echo "[init.sh] Готово."