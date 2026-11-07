-- ============================================================================
-- Лабораторная работа №2 -> ЛР №3
-- Система анонимного анкетирования по качеству преподавания
-- Скрипт создания схемы БД (PostgreSQL 16+)
-- 
-- Отличия от исходного варианта MSSQL (см. docs/discrepancies.md):
--   1. UNIQUEIDENTIFIER -> uuid, NVARCHAR -> varchar/text, DATETIME2 -> timestamptz
--   2. NEWSEQUENTIALID() -> gen_random_uuid()
--   3. ISJSON CHECK -> тип jsonb (встроенный бинарный JSON в PostgreSQL)
--   4. Добавлена таблица one_time_tokens (для восстановления пароля O-02)
-- ============================================================================

SET client_encoding = 'UTF8';
SET timezone = 'UTC';

-- ============================================================================
-- 1. СПРАВОЧНИКИ (Справочные таблицы, редко меняются)
-- ============================================================================

CREATE TABLE user_roles (
    id    smallint     NOT NULL,
    code  varchar(32)  NOT NULL, -- 'student', 'teacher', 'admin', 'tech'
    CONSTRAINT pk_user_roles PRIMARY KEY (id),
    CONSTRAINT uq_user_roles_code UNIQUE (code)
);

CREATE TABLE period_statuses (
    id    smallint     NOT NULL,
    code  varchar(32)  NOT NULL, -- 'draft', 'open', 'closed', 'archived'
    CONSTRAINT pk_period_statuses PRIMARY KEY (id),
    CONSTRAINT uq_period_statuses_code UNIQUE (code)
);

CREATE TABLE survey_statuses (
    id    smallint     NOT NULL,
    code  varchar(32)  NOT NULL, -- 'draft', 'active', 'closed'
    CONSTRAINT pk_survey_statuses PRIMARY KEY (id),
    CONSTRAINT uq_survey_statuses_code UNIQUE (code)
);

CREATE TABLE question_types (
    id    smallint     NOT NULL,
    code  varchar(32)  NOT NULL, -- 'scale' (оценка 1-5), 'text' (свободный текст)
    CONSTRAINT pk_question_types PRIMARY KEY (id),
    CONSTRAINT uq_question_types_code UNIQUE (code)
);

CREATE TABLE export_formats (
    id    smallint     NOT NULL,
    code  varchar(32)  NOT NULL, -- 'csv', 'xlsx'
    CONSTRAINT pk_export_formats PRIMARY KEY (id),
    CONSTRAINT uq_export_formats_code UNIQUE (code)
);

CREATE TABLE export_statuses (
    id    smallint     NOT NULL,
    code  varchar(32)  NOT NULL, -- 'processing', 'ready', 'failed'
    CONSTRAINT pk_export_statuses PRIMARY KEY (id),
    CONSTRAINT uq_export_statuses_code UNIQUE (code)
);

CREATE TABLE sentiment_types (
    id    smallint     NOT NULL,
    code  varchar(32)  NOT NULL, -- 'positive', 'neutral', 'negative'
    CONSTRAINT pk_sentiment_types PRIMARY KEY (id),
    CONSTRAINT uq_sentiment_types_code UNIQUE (code)
);

-- ============================================================================
-- 2. ЗАПОЛНЕНИЕ СПРАВОЧНИКОВ НАЧАЛЬНЫМИ ДАННЫМИ
-- ============================================================================

INSERT INTO user_roles (id, code) VALUES
    (1, 'student'), (2, 'teacher'), (3, 'admin'), (4, 'tech');

INSERT INTO period_statuses (id, code) VALUES
    (1, 'draft'), (2, 'open'), (3, 'closed'), (4, 'archived');

INSERT INTO survey_statuses (id, code) VALUES
    (1, 'draft'), (2, 'active'), (3, 'closed');

INSERT INTO question_types (id, code) VALUES
    (1, 'scale'), (2, 'text');

INSERT INTO export_formats (id, code) VALUES
    (1, 'csv'), (2, 'xlsx');

INSERT INTO export_statuses (id, code) VALUES
    (1, 'processing'), (2, 'ready'), (3, 'failed');

INSERT INTO sentiment_types (id, code) VALUES
    (1, 'positive'), (2, 'neutral'), (3, 'negative');

-- ============================================================================
-- 3. ОСНОВНЫЕ ТАБЛИЦЫ
-- ============================================================================

-- 3.1. Учётные записи
CREATE TABLE users (
    id            uuid          NOT NULL DEFAULT gen_random_uuid(),
    login         varchar(64)   NOT NULL,
    -- Храним ТОЛЬКО хеш пароля (Argon2id) и соль. Никакого открытого текста!
    password_hash varchar(255)  NOT NULL,
    password_salt varchar(64)   NOT NULL,
    role_id       smallint      NOT NULL,
    email         varchar(255)  NOT NULL,
    CONSTRAINT pk_users PRIMARY KEY (id),
    CONSTRAINT uq_users_login UNIQUE (login),
    CONSTRAINT uq_users_email UNIQUE (email),
    CONSTRAINT fk_users_role FOREIGN KEY (role_id) REFERENCES user_roles(id),
    -- Защита на уровне БД: хеш не может быть короче 32 символов, соль - 16.
    CONSTRAINT ck_users_hash_len  CHECK (char_length(password_hash) >= 32),
    CONSTRAINT ck_users_salt_len  CHECK (char_length(password_salt) >= 16)
);

-- 3.2. Профили преподавателей
CREATE TABLE teachers (
    id         uuid         NOT NULL DEFAULT gen_random_uuid(),
    user_id    uuid         NOT NULL,
    department varchar(255) NOT NULL,
    CONSTRAINT pk_teachers PRIMARY KEY (id),
    CONSTRAINT uq_teachers_user_id UNIQUE (user_id),
    -- Если удалить юзера, его профиль препода удалится каскадно
    CONSTRAINT fk_teachers_user FOREIGN KEY (user_id)
        REFERENCES users(id) ON DELETE CASCADE
);

-- 3.3. Дисциплины
CREATE TABLE disciplines (
    id   uuid         NOT NULL DEFAULT gen_random_uuid(),
    code varchar(32)  NOT NULL, -- Например, 'CS101'
    name varchar(255) NOT NULL,
    CONSTRAINT pk_disciplines PRIMARY KEY (id),
    CONSTRAINT uq_disciplines_code UNIQUE (code)
);

-- 3.4. Связь M:N (Один препод может вести несколько дисциплин, одну дисциплину - несколько преподов)
CREATE TABLE teacher_disciplines (
    teacher_id    uuid NOT NULL,
    discipline_id uuid NOT NULL,
    CONSTRAINT pk_teacher_disciplines PRIMARY KEY (teacher_id, discipline_id),
    CONSTRAINT fk_td_teacher FOREIGN KEY (teacher_id)
        REFERENCES teachers(id) ON DELETE CASCADE,
    CONSTRAINT fk_td_discipline FOREIGN KEY (discipline_id)
        REFERENCES disciplines(id)
);

-- 3.5. Периоды анкетирования (Например, "Осень 2026")
CREATE TABLE periods (
    id         uuid         NOT NULL DEFAULT gen_random_uuid(),
    name       varchar(255) NOT NULL,
    start_date date         NOT NULL,
    end_date   date         NOT NULL,
    status_id  smallint     NOT NULL,
    created_by uuid         NOT NULL, -- Кто из админов создал период
    CONSTRAINT pk_periods PRIMARY KEY (id),
    CONSTRAINT fk_periods_status FOREIGN KEY (status_id) REFERENCES period_statuses(id),
    CONSTRAINT fk_periods_author FOREIGN KEY (created_by) REFERENCES users(id),
    CONSTRAINT ck_periods_dates CHECK (end_date >= start_date) -- Дата конца не может быть раньше начала
);

-- 3.6. Шаблоны анкет (Связка: Период + Дисциплина + Преподаватель)
CREATE TABLE survey_templates (
    id            uuid     NOT NULL DEFAULT gen_random_uuid(),
    period_id     uuid     NOT NULL,
    discipline_id uuid     NOT NULL,
    teacher_id    uuid     NOT NULL,
    status_id     smallint NOT NULL,
    CONSTRAINT pk_survey_templates PRIMARY KEY (id),
    CONSTRAINT fk_st_period FOREIGN KEY (period_id)     REFERENCES periods(id),
    CONSTRAINT fk_st_discipline FOREIGN KEY (discipline_id) REFERENCES disciplines(id),
    CONSTRAINT fk_st_teacher FOREIGN KEY (teacher_id)   REFERENCES teachers(id),
    CONSTRAINT fk_st_status FOREIGN KEY (status_id)     REFERENCES survey_statuses(id)
);

-- 3.7. Вопросы внутри шаблона анкеты
CREATE TABLE questions (
    id                 bigserial   NOT NULL, -- Автоинкрементный ID (1, 2, 3...)
    survey_template_id uuid        NOT NULL,
    text               text        NOT NULL,
    type_id            smallint    NOT NULL,
    CONSTRAINT pk_questions PRIMARY KEY (id),
    CONSTRAINT fk_questions_tmpl FOREIGN KEY (survey_template_id)
        REFERENCES survey_templates(id) ON DELETE CASCADE,
    CONSTRAINT fk_questions_type FOREIGN KEY (type_id)
        REFERENCES question_types(id)
);

-- ============================================================================
-- ⚠️ КЛЮЧЕВЫЕ ТАБЛИЦЫ ДЛЯ ОБЕСПЕЧЕНИЯ АНОНИМНОСТИ ⚠️
-- ============================================================================

-- 3.8. Реестр права голоса
-- ВАЖНО: эта таблица знает, КТО голосовал (student_id).
-- Но она НЕ ЗНАЕТ, что именно он ответил. Связи с ballots НЕТ.
CREATE TABLE voter_checkins (
    id                 bigserial NOT NULL,
    student_id         uuid      NOT NULL,
    survey_template_id uuid      NOT NULL,
    has_voted          boolean   NOT NULL DEFAULT false,
    CONSTRAINT pk_voter_checkins PRIMARY KEY (id),
    -- Студент может голосовать в одной анкете только один раз
    CONSTRAINT uq_voter_checkins_pair UNIQUE (student_id, survey_template_id),
    CONSTRAINT fk_vci_student FOREIGN KEY (student_id) REFERENCES users(id),
    CONSTRAINT fk_vci_template FOREIGN KEY (survey_template_id) REFERENCES survey_templates(id)
);

-- 3.9. Обезличенные отправки (Бюллетени)
-- ВАЖНО: эта таблица знает, ЧТО было отвечено (ответы привязываются к ballot_id).
-- Но она НЕ ЗНАЕТ, кто это сделал. Здесь намеренно нет student_id!
CREATE TABLE ballots (
    id                 uuid NOT NULL DEFAULT gen_random_uuid(),
    survey_template_id uuid NOT NULL,
    submitted_date     date NOT NULL, -- Храним только дату, без точного времени (защита от деанонимизации)
    CONSTRAINT pk_ballots PRIMARY KEY (id),
    CONSTRAINT fk_ballots_template FOREIGN KEY (survey_template_id) REFERENCES survey_templates(id)
);

-- 3.10. Ответы на вопросы (Привязаны к обезличенному бюллетеню)
CREATE TABLE answers (
    id          bigserial NOT NULL,
    ballot_id   uuid      NOT NULL,
    question_id bigint    NOT NULL,
    value_int   smallint  NULL, -- Для оценок (1-5)
    value_text  text      NULL, -- Для свободных комментариев
    CONSTRAINT pk_answers PRIMARY KEY (id),
    CONSTRAINT uq_answers_pair UNIQUE (ballot_id, question_id), -- На один вопрос в бюллетене - один ответ
    CONSTRAINT fk_answers_ballot FOREIGN KEY (ballot_id) REFERENCES ballots(id) ON DELETE CASCADE,
    CONSTRAINT fk_answers_question FOREIGN KEY (question_id) REFERENCES questions(id),
    -- Ответ не может быть пустым (должна быть либо цифра, либо текст)
    CONSTRAINT ck_answers_not_empty CHECK (value_int IS NOT NULL OR value_text IS NOT NULL),
    CONSTRAINT ck_answers_scale_range CHECK (value_int IS NULL OR value_int BETWEEN 1 AND 5)
);

-- ============================================================================
-- 4. СЛУЖЕБНЫЕ И ВСПОМОГАТЕЛЬНЫЕ ТАБЛИЦЫ
-- ============================================================================

-- 4.1. Результаты классификации LLM (Оставлено для совместимости с ЛР2, 
-- но в коде ЛР3 не используется, т.к. LLM исключен из задания)
CREATE TABLE llm_classifications (
    id            bigserial      NOT NULL,
    answer_id     bigint         NOT NULL,
    sentiment_id  smallint       NOT NULL,
    topics        jsonb          NOT NULL, -- Массив тем в формате JSON
    confidence    numeric(3,2)   NOT NULL, -- Уверенность модели от 0.00 до 1.00
    classified_at timestamptz(3) NOT NULL DEFAULT now(),
    CONSTRAINT pk_llm_classifications PRIMARY KEY (id),
    CONSTRAINT uq_llm_classifications_answer UNIQUE (answer_id),
    CONSTRAINT fk_llm_answer FOREIGN KEY (answer_id) REFERENCES answers(id) ON DELETE CASCADE,
    CONSTRAINT fk_llm_sentiment FOREIGN KEY (sentiment_id) REFERENCES sentiment_types(id),
    CONSTRAINT ck_llm_confidence CHECK (confidence BETWEEN 0 AND 1)
);

-- 4.2. Реестр выгрузок отчетов (Кто, когда и какой отчет скачал)
CREATE TABLE exports (
    id                 uuid           NOT NULL DEFAULT gen_random_uuid(),
    created_by         uuid           NOT NULL,
    period_id          uuid           NOT NULL,
    survey_template_id uuid           NULL,
    format_id          smallint       NOT NULL,
    status_id          smallint       NOT NULL,
    -- Ключ идемпотентности (защита от двойного нажатия кнопки "Скачать")
    idempotency_key    uuid           NOT NULL,
    object_key         varchar(512)   NOT NULL, -- Ссылка на файл в MinIO
    expires_at         timestamptz(3) NOT NULL, -- Время жизни ссылки на скачивание
    CONSTRAINT pk_exports PRIMARY KEY (id),
    CONSTRAINT uq_exports_idempotency UNIQUE (idempotency_key),
    CONSTRAINT fk_exports_author FOREIGN KEY (created_by) REFERENCES users(id),
    CONSTRAINT fk_exports_period FOREIGN KEY (period_id) REFERENCES periods(id),
    CONSTRAINT fk_exports_template FOREIGN KEY (survey_template_id) REFERENCES survey_templates(id),
    CONSTRAINT fk_exports_format FOREIGN KEY (format_id) REFERENCES export_formats(id),
    CONSTRAINT fk_exports_status FOREIGN KEY (status_id) REFERENCES export_statuses(id)
);

-- 4.3. Журнал аудита (Логи безопасности)
CREATE TABLE audit_events (
    id          bigserial      NOT NULL,
    user_id     uuid           NULL, -- Может быть NULL (например, попытка входа с несуществующим логином)
    event_type  varchar(64)    NOT NULL, -- 'login_success', 'login_failed', 'period_changed'...
    entity_type varchar(32)    NOT NULL, -- С какой сущностью работали ('user', 'period')
    entity_id   varchar(64)    NULL,
    details     jsonb          NULL, -- Детали события (IP, User-Agent и т.д.)
    ip_address  varchar(45)    NOT NULL, -- IPv4 или IPv6
    created_at  timestamptz(3) NOT NULL DEFAULT now(),
    CONSTRAINT pk_audit_events PRIMARY KEY (id),
    CONSTRAINT fk_audit_user FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE SET NULL
);

-- 4.4. Одноразовые токены (Добавлено для восстановления пароля O-02)
CREATE TABLE one_time_tokens (
    id         bigserial      NOT NULL,
    user_id    uuid           NOT NULL,
    purpose    varchar(32)    NOT NULL, -- 'password_reset' или 'email_verify'
    -- В базе храним ХЕШ токена, а не сам токен! Чтобы при утечке БД никто не смог сбросить пароли.
    token_hash varchar(255)   NOT NULL,
    expires_at timestamptz(3) NOT NULL,
    used_at    timestamptz(3) NULL, -- Если NULL - токен еще не использован
    created_at timestamptz(3) NOT NULL DEFAULT now(),
    CONSTRAINT pk_one_time_tokens PRIMARY KEY (id),
    CONSTRAINT uq_one_time_tokens_hash UNIQUE (token_hash),
    CONSTRAINT fk_one_time_tokens_user FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
    CONSTRAINT ck_one_time_tokens_purpose CHECK (purpose IN ('password_reset', 'email_verify'))
);

-- ============================================================================
-- 5. ИНДЕКСЫ (Для ускорения поиска и проверок)
-- ============================================================================

-- Ускоряет расчет средних оценок по вопросам
CREATE INDEX ix_answers_question_id ON answers (question_id);

-- Частичный индекс: ускоряет поиск ТОЛЬКО текстовых ответов (для будущей аналитики)
CREATE INDEX ix_answers_text ON answers (question_id) WHERE value_text IS NOT NULL;

-- Быстрый поиск отправленных анкет по шаблону и дате
CREATE INDEX ix_ballots_template_date ON ballots (survey_template_id, submitted_date);

-- Быстрая проверка "голосовал ли студент" (для метода O-05)
CREATE INDEX ix_voter_checkins_student ON voter_checkins (student_id);
CREATE INDEX ix_voter_checkins_template ON voter_checkins (survey_template_id);

-- Быстрый поиск в журнале аудита
CREATE INDEX ix_audit_events_created ON audit_events (created_at DESC);
CREATE INDEX ix_audit_events_user ON audit_events (user_id) WHERE user_id IS NOT NULL;

-- Поиск выгрузок по автору
CREATE INDEX ix_exports_author ON exports (created_by);

-- Фильтрация активных периодов и шаблонов
CREATE INDEX ix_periods_status ON periods (status_id);
CREATE INDEX ix_survey_templates_status ON survey_templates (status_id);

-- Проверка прав преподавателя
CREATE INDEX ix_teacher_disciplines_discipline ON teacher_disciplines (discipline_id);

-- Индексы для таблицы токенов (поиск активных токенов пользователя)
CREATE INDEX ix_one_time_tokens_user ON one_time_tokens (user_id);
CREATE INDEX ix_one_time_tokens_active ON one_time_tokens (expires_at) WHERE used_at IS NULL;

-- GIN индекс для быстрого поиска внутри JSONB (если пригодится)
CREATE INDEX ix_llm_classifications_topics ON llm_classifications USING gin (topics);