# Sallevate — Улучшенная архитектура

> Документ написан на основе анализа текущего состояния проекта (апрель 2026).  
> Цель — описать путь эволюции монолита к масштабируемой, отказоустойчивой архитектуре.

---

## 1. Анализ текущей архитектуры

### 1.1 Что есть сейчас

Проект реализован как **монолит с feature-slice организацией** на ASP.NET Core 9.

```
Sallevate.Api (один процесс)
├── Features/Auth          — аутентификация, JWT, Google OAuth
├── Features/Onboarding    — профиль пользователя
├── Features/SkillTree     — дерево навыков
├── Features/Lessons       — уроки и упражнения
├── Features/Exercises     — оценка ответов (Multiple Choice, Fill Blank, Free Text + AI)
├── Features/Gamification  — XP, стрики
├── Features/League        — лиги и рейтинги
├── Features/Reference     — справочные материалы
├── Features/Profile       — статистика пользователя
├── Features/Transcription — транскрипция аудио (Whisper)
└── Features/Admin         — панель администратора (CRUD, сидер)
```

**Хранилища:**
- **PostgreSQL** — основная БД (пользователи, прогресс, лиги)
- **MongoDB** — используется (клиент зарегистрирован), но данные пока не мигрированы
- **Redis** — используется (клиент зарегистрирован), кеширование пока не применяется
- **Hangfire + PostgreSQL** — фоновые задачи (закрытие лиг по расписанию)

**Внешние API:**
- OpenAI Chat Completions — оценка free-text ответов
- OpenAI Whisper — транскрипция аудио
- Google OAuth2

### 1.2 Проблемы текущей архитектуры

| # | Проблема | Последствие |
|---|----------|-------------|
| 1 | Один процесс — единая точка отказа | Падение одного компонента валит всё |
| 2 | Redis и MongoDB подключены, но не используются | Лишняя сложность без выгоды |
| 3 | Нет кеширования — каждый запрос идёт в БД | Проблемы при росте нагрузки |
| 4 | AI-вызовы (OpenAI) синхронные, блокируют HTTP-запрос | Долгое время ответа, таймауты |
| 5 | Нет event-driven связи между доменами | Gamification и League знают о деталях Exercises |
| 6 | Нет очереди сообщений — нет отказоустойчивости | Потеря данных при сбое AI-сервиса |
| 7 | Все фичи масштабируются только вместе | Нельзя дать больше ресурсов только AI-части |

---

## 2. Целевая архитектура

### 2.1 Принципы

- **Domain-Driven Design** — границы сервисов = границы доменов
- **Event-Driven** — сервисы общаются через события, а не прямые вызовы
- **CQRS** — разделение команд (write) и запросов (read) там, где нагрузка асимметрична
- **Постепенная миграция** — монолит → выделение сервисов по одному

### 2.2 Схема целевой архитектуры

```
                              КЛИЕНТЫ
                    ┌──────────────────────────┐
                    │  Web (Next.js)  │  Mobile  │
                    └──────────┬───────────────┘
                               │ HTTPS
                    ┌──────────▼──────────────────┐
                    │       API Gateway / BFF       │
                    │   (Next.js Route Handlers     │
                    │    или отдельный сервис)       │
                    └──┬──────┬──────┬─────────┬───┘
                       │      │      │         │
          ┌────────────▼─┐ ┌──▼───┐ ┌▼──────┐ ┌▼──────────────┐
          │ Auth Service │ │Learn │ │Gamif. │ │  AI Service   │
          │              │ │Sук   │ │Service│ │  (Whisper +   │
          │ - register   │ │      │ │       │ │   GPT eval)   │
          │ - login      │ │- tree│ │- XP   │ │               │
          │ - Google     │ │- les.│ │- str. │ │- transcribe   │
          │ - JWT/Refresh│ │- ex. │ │- lead.│ │- evaluate     │
          └──────┬───────┘ └──┬───┘ └──┬────┘ └──────┬────────┘
                 │             │        │             │
                 │     ┌───────▼────────▼─────┐      │
                 │     │     Kafka / Redis     │      │
                 │     │    Message Broker     │      │
                 │     │                      │      │
                 │     │  Topics:             │      │
                 │     │  - exercise.completed│      │
                 │     │  - lesson.completed  │      │
                 │     │  - week.closed       │◄─────┘
                 │     └──────────────────────┘
                 │              │
      ┌──────────▼──────────────▼────────────────────────────┐
      │                     ХРАНИЛИЩА                         │
      │                                                       │
      │  ┌─────────────┐  ┌──────────────┐  ┌─────────────┐ │
      │  │ PostgreSQL  │  │   MongoDB    │  │    Redis    │ │
      │  │             │  │              │  │             │ │
      │  │ - users     │  │ - AI logs    │  │ - sessions  │ │
      │  │ - progress  │  │ - audit trail│  │ - skill tree│ │
      │  │ - leagues   │  │ - content    │  │ - leaderbd. │ │
      │  │ - hangfire  │  │   versions   │  │ - rate limit│ │
      │  └─────────────┘  └──────────────┘  └─────────────┘ │
      └───────────────────────────────────────────────────────┘
```

### 2.3 Описание сервисов

#### Auth Service
- Отвечает за: регистрацию, вход, Google OAuth, JWT, refresh tokens
- БД: PostgreSQL (users, refresh_tokens)
- Кеш: Redis (blacklist отозванных токенов)
- Не имеет зависимостей от других сервисов (только инфраструктура)

#### Learning Service (бывший SkillTree + Lessons + Exercises + Reference)
- Отвечает за: дерево навыков, уроки, упражнения, справочные материалы, прогресс
- БД: PostgreSQL (skills, lessons, exercises, user_*_progress)
- Кеш: Redis (skill tree — кеш на 5 минут, инвалидация по событию)
- Публикует события в Kafka:
  - `exercise.completed` `{ userId, exerciseId, score, isCorrect }`
  - `lesson.completed` `{ userId, lessonId, xpAwarded }`

#### Gamification Service
- Отвечает за: XP, стрики, ачивменты, лиги, рейтинги
- БД: PostgreSQL (xp_records, streaks, leagues, memberships)
- Кеш: Redis (weekly leaderboard — Sorted Set)
- Потребляет события из Kafka: `exercise.completed`, `lesson.completed`
- Публикует: `week.closed`, `rank.changed`
- **Hangfire остаётся здесь** для еженедельного закрытия лиг

#### AI Service (новый выделенный сервис)
- Отвечает за: транскрипцию (Whisper), оценку free-text (GPT), будущие AI-фичи
- Не имеет своей БД; логи AI-запросов → MongoDB
- Работает **асинхронно**: принимает задачу, кладёт в очередь, возвращает job_id
- Результат публикуется в Kafka: `ai.evaluation.completed`
- Rate limiting через Redis
- **Почему отдельный сервис:** AI-вызовы медленные (1–10с), нужна независимая масштабируемость

#### Admin Service / BFF
- Отвечает за: CRUD контента (навыки, уроки, упражнения), управление пользователями, сидер
- Может оставаться частью основного монолита на первом этапе миграции

---

## 3. Использование Redis

| Назначение | Тип структуры | TTL |
|-----------|---------------|-----|
| Кеш skill tree пользователя | Hash | 5 минут |
| Weekly leaderboard | Sorted Set | до закрытия недели |
| Blacklist отозванных JWT | Set | время жизни токена |
| Rate limit AI-запросов | Counter (INCR + EXPIRE) | 1 минута |
| Distributed lock (закрытие лиги) | String (SET NX EX) | 30 секунд |

---

## 4. Использование MongoDB

| Назначение | Коллекция | Описание |
|-----------|-----------|---------|
| Логи AI-запросов | `ai_logs` | prompt, response, latency, cost |
| История версий контента | `content_versions` | snapshot упражнений/уроков при изменении |
| Audit trail | `audit_events` | кто изменил что в админке |

---

## 5. Kafka — топики и события

```
exercise.completed
  → Gamification Service (начислить XP, обновить стрик, обновить лигу)

lesson.completed  
  → Gamification Service (начислить XP за урок)
  → Learning Service (обновить skill progress)

ai.evaluation.completed
  → Learning Service (сохранить результат оценки в exercise_attempt)

week.closed
  → Gamification Service (пересчитать ранги, создать новую лигу)
  → Notification Service (TODO: уведомить пользователей о результате)
```

---

## 6. План поэтапной миграции

### Этап 0 — Сейчас (монолит ✓)
Текущее состояние: feature-slice монолит, всё в одном процессе.

### Этап 1 — Активировать Redis (2–3 дня)
- Кешировать skill tree: при `GET /skill-tree` читать из Redis, при изменении — инвалидировать
- Кешировать leaderboard: weekly leaderboard хранить в Redis Sorted Set
- Blacklist токенов в Redis при logout

### Этап 2 — Активировать MongoDB (1–2 дня)
- Перенести логирование AI-вызовов (OpenAI) в MongoDB коллекцию `ai_logs`
- Добавить версионирование контента (content_versions) при изменении упражнений через Admin

### Этап 3 — Выделить AI Service (3–5 дней)
- Поднять отдельный ASP.NET Core процесс `Sallevate.AiService`
- Перенести `FreeTextEvaluationStrategy` и `WhisperTranscriptionService`
- Добавить очередь (сначала Redis Streams, потом Kafka)
- Монолит вызывает AI Service через HTTP или публикует задачу в очередь

### Этап 4 — Ввести Kafka (1 неделя)
- Развернуть Kafka в Docker Compose
- Learning Service публикует `exercise.completed`
- Gamification Service потребляет события вместо прямых вызовов
- Это разрывает прямую зависимость между доменами

### Этап 5 — Выделить Gamification Service (1–2 недели)
- Отдельный процесс с собственной БД (или схемой в PostgreSQL)
- Взаимодействие только через Kafka

---

## 7. Docker Compose (целевой)

```yaml
services:
  # --- Приложения ---
  api:           # Learning + Auth + Admin (монолит на переходный период)
  ai-service:    # AI Service (Whisper, GPT eval)
  frontend:      # Next.js

  # --- Инфраструктура ---
  postgres:      # основная БД
  mongo:         # логи AI, версии контента
  redis:         # кеш, leaderboard, rate limit
  kafka:         # message broker
  zookeeper:     # для Kafka

  # --- Observability ---
  loki:
  prometheus:
  grafana:
```

---

## 8. Что НЕ стоит делать преждевременно

- **Не разбивать Auth и Learning на микросервисы** пока нет реальной нагрузки — это создаёт операционную сложность без выгоды
- **Не переходить на gRPC** — REST достаточен для текущего масштаба
- **Не вводить Service Mesh (Istio/Linkerd)** — overhead не оправдан до 10+ сервисов
- **Не переходить на Kubernetes** — Docker Compose справляется до ~10k DAU

---

## 9. Приоритеты внедрения (ROI)

| Приоритет | Изменение | Сложность | Выгода |
|-----------|-----------|-----------|--------|
| 1 | Активировать Redis кеш | Низкая | Высокая (производительность) |
| 2 | AI Service async + очередь | Средняя | Высокая (UX, надёжность) |
| 3 | MongoDB для AI логов | Низкая | Средняя (observability) |
| 4 | Kafka events для Gamification | Высокая | Высокая (развязка доменов) |
| 5 | Выделить Gamification Service | Высокая | Средняя (масштабируемость) |
