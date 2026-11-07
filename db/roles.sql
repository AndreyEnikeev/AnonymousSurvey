-- ============================================================
-- Разграничение прав ролей приложения (PostgreSQL).
-- 
-- ВАЖНО: сами роли (auth_service и т.д.) создаются в файле init.sh 
-- (там же им задаются пароли из .env). 
-- Этот файл только ВЫДАЕТ им права (GRANT) и ЗАПРЕЩАЕТ лишнее.
--
-- Здесь реализован принцип наименьших привилегий (Least Privilege):
-- каждый сервис получает ровно те права, которые нужны ему для работы.
-- ============================================================

-- 1. Базовые права на подключение к базе и использование схемы public
-- (Без этого сервисы даже не смогут "зайти" в базу)
GRANT CONNECT ON DATABASE survey_db TO auth_service, surveys_service, analytics_service, notifications_service;
GRANT USAGE ON SCHEMA public TO auth_service, surveys_service, analytics_service, notifications_service;

-- По умолчанию в PostgreSQL 15+ обычные пользователи не могут создавать таблицы
-- в схеме public. Для надежности явно отзываем это право у всех.
-- Только суперпользователь (postgres) может менять структуру БД.
REVOKE CREATE ON SCHEMA public FROM PUBLIC;

-- ============================================================
-- СЕРВИС АУТЕНТИФИКАЦИИ
-- Ему нужно читать пользователей для проверки пароля, писать токены и логи.
-- Ему НЕ НУЖНО видеть ответы на анкеты (answers) или отчеты (exports).
-- Если хакер взломает этот сервис, он не получит доступ к данным анкет.
-- ============================================================
GRANT SELECT, INSERT, UPDATE ON users            TO auth_service;
GRANT SELECT, INSERT, UPDATE ON one_time_tokens  TO auth_service;
-- Журнал аудита: Auth пишет события (INSERT) и читает их для эндпоинта O-12 (SELECT).
-- UPDATE и DELETE намеренно НЕ выдаются: аудит — append-only лог,
-- никто не должен уметь подменять или удалять историю событий.
GRANT INSERT, SELECT ON audit_events TO auth_service;

-- Даем права на использование последовательностей (счетчиков автоинкремента)
-- Без этого сервис не сможет создавать новые записи (например, токены).
GRANT USAGE, SELECT ON SEQUENCE one_time_tokens_id_seq TO auth_service;
GRANT USAGE, SELECT ON SEQUENCE audit_events_id_seq    TO auth_service;

-- ============================================================
-- СЕРВИС АНКЕТИРОВАНИЯ
-- Ему нужно управлять периодами, шаблонами, вопросами и сохранять голоса.
-- Ему НЕ НУЖНО читать хеши паролей (users) или управлять выгрузками (exports).
-- ============================================================
-- Читает только справочники статусов
GRANT SELECT ON user_roles, period_statuses, survey_statuses, question_types TO surveys_service;

-- Управляет периодами и анкетами
GRANT SELECT, INSERT, UPDATE ON periods          TO surveys_service;
GRANT SELECT, INSERT, UPDATE ON survey_templates TO surveys_service;
GRANT SELECT, INSERT, UPDATE ON questions        TO surveys_service;

-- Сохраняет факты голосования (voter_checkins) и ответы (ballots, answers)
GRANT SELECT, INSERT, UPDATE ON voter_checkins   TO surveys_service;
GRANT SELECT, INSERT, UPDATE ON ballots          TO surveys_service;
GRANT SELECT, INSERT, UPDATE ON answers          TO surveys_service;

-- Читает информацию о преподах и дисциплинах (чтобы выдавать студентам нужные анкеты)
GRANT SELECT ON teachers, disciplines, teacher_disciplines TO surveys_service;

-- Пишет логи о смене периодов и отправке анкет
GRANT INSERT ON audit_events TO surveys_service;

-- Права на последовательности (для создания вопросов, бюллетеней и ответов)
GRANT USAGE, SELECT ON SEQUENCE questions_id_seq       TO surveys_service;
GRANT USAGE, SELECT ON SEQUENCE voter_checkins_id_seq  TO surveys_service;
GRANT USAGE, SELECT ON SEQUENCE answers_id_seq         TO surveys_service;
GRANT USAGE, SELECT ON SEQUENCE audit_events_id_seq    TO surveys_service;

-- ============================================================
-- СЕРВИС АНАЛИТИКИ
-- Ему нужно только ЧИТАТЬ данные для построения отчетов и 
-- сохранять информацию о выгрузках (exports).
-- Он не может ничего менять в ответах или анкетах (только SELECT).
-- ============================================================
GRANT SELECT ON ballots, answers, periods, survey_templates, questions, 
                disciplines, teachers, teacher_disciplines,
                llm_classifications, sentiment_types, period_statuses
    TO analytics_service;

-- Создает и обновляет записи о выгрузках (файлах в MinIO)
GRANT SELECT, INSERT, UPDATE ON exports TO analytics_service;
GRANT SELECT ON export_formats, export_statuses TO analytics_service;

GRANT INSERT ON audit_events TO analytics_service;
GRANT USAGE, SELECT ON SEQUENCE audit_events_id_seq TO analytics_service;

-- ============================================================
-- СЕРВИС УВЕДОМЛЕНИЙ
-- Самый ограниченный. Ему вообще не нужна база данных для работы!
-- Он только пишет логи о том, что письмо отправлено.
-- Даже если его взломают, хакер максимум испортит пару логов.
-- ============================================================
GRANT INSERT ON audit_events TO notifications_service;
GRANT USAGE, SELECT ON SEQUENCE audit_events_id_seq TO notifications_service;