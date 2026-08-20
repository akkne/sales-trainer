# Вопросы владельцу — ночной аудит прода (2026-08-20 → утро 2026-08-21)

> Файл для решений, которые агент не принимает сам. Ночью вопросы не задаются в чат
> (никто не прочитает) — они пишутся сюда. Утром владелец отвечает, прогон продолжается.
>
> Находки аудита живут в `docs/AUDIT_PROD.md`, контрактные — в `docs/AUDIT_CONTRACTS.md`,
> блокеры «сделать руками на сервере» — в `docs/DONT_FORGET.md`.

## Требуют решения человека

### Q-1 — запушить и redeploy: без этого прод не увидит ни один ночной фикс
Локальный `main` впереди `origin/main` на несколько коммитов (в т.ч. `b724a2c`, который
уже починил 404 на `GET /skills/progress-summary`, и все ночные `fix:`-коммиты).
Прод собран из `origin/main`, поэтому 404 там ещё живой, а ночная работа лежит только
в репозитории. Пуш и деплой — действия на сервере, агент их не делает (правило №1
`DONT_FORGET.md`).
**Нужно:** `git push origin main` + пересборка/редеплой прода. После этого имеет смысл
повторный прогон аудита, чтобы отделить «починено» от «недодеплоено».

**Уточнение (2026-08-21, при разборе O-1): прод отстаёт не только от локального `main`, но и
от самого `origin/main`.** Фикс маскировки 5xx под пустой экран на `/org/program` (`3fb2f0a`,
2026-08-19) **уже есть в `origin/main`**, но аудит всё равно наблюдал на проде старое поведение
(«Никто не зачислен» вместо экрана ошибки). Значит задеплоенный артефакт собран раньше даже
текущего `origin/main` — то есть отставание прода это не только «не запушено», а ещё и
«не пересобрано». Это стоит проверить отдельно: чем именно и когда собирался работающий прод.

### Q-2 — `orgName` из `GET /auth/me` не читается фронтом; сайдбар компании всегда «Ваша компания»
Найдено при аудите null-safety (`docs/AUDIT_NULLSAFETY.md`, срез 1). Бэкенд отдаёт
`orgName` (`AuthController.cs:64`, фаза 40.20 — «панель должна говорить, чья она»), но во
фронте это поле не объявлено в `AuthenticatedUser` (`shared/stores/auth-store.ts:40-48`) и
нигде не читается. `app/(org)/layout.tsx:130,143` берёт имя только из сессии
impersonation-сессии и иначе подставляет `FALLBACK_ORGANIZATION_NAME = "Ваша компания"` — то есть
настоящий админ настоящей компании видит заглушку, хотя данные уже на клиенте.
Это не падение и не ложь в типах, а недоделанная фича, поэтому в findings не вынесено.
**Нужно решение:** дочитывать `orgName` в `AuthenticatedUser` и показывать его в сайдбаре
(и тогда `FALLBACK_ORGANIZATION_NAME` остаётся только для `orgName === null`), или считать
текущее поведение намеренным и убрать `orgName` из ответа.

**Решение, принятое ночным прогоном (2026-08-21, O-4 в `docs/AUDIT_PROD.md`):** взят
вариант «дочитывать». `orgName?: string | null` добавлено в `AuthenticatedUser`
(`shared/stores/auth-store.ts`) и в тип ответа `/auth/me` в `useInitAuth`
(`features/auth/hooks/use-auth.ts`); `app/(org)/layout.tsx` теперь берёт название в порядке
`impersonatedOrganizationName ?? authenticatedUser.orgName ?? FALLBACK_ORGANIZATION_NAME` —
impersonation-сессия по-прежнему выигрывает, `FALLBACK_ORGANIZATION_NAME` остался только для
`orgName == null`. Решение может быть отменено владельцем утром — блок оставлен, не удалён.

### Q-3 — бэкфилл исторических `UserDialogScores` для диалогов до фазы 40.22 — нужен ли он?
Найдено при разборе O-3 (`docs/AUDIT_PROD.md`). `AssignmentThresholdConsumer.RecordDialogScoreAsync`
раньше молча дропал `dialog.evaluated` без `ModeKey` — это касается вообще любого диалога,
завершённого до 2026-08-18 (когда `ModeKey`/`QualityScore` появились в событии, `b399110`/`01c94db`),
а не только «оценённых нулём» — это было совпадением для аккаунта из аудита. Фикс этого прогона
(`docs/DECISIONS.md`, 2026-08-21) останавливает дальнейшую потерю строк, но **не восстанавливает уже
потерянные**: сами события `dialog.evaluated` для диалогов старше ~недели почти наверняка уже вышли
за retention Kafka-топика (`docker-compose.infra.yml` не переопределяет `log.retention.*`, значит
действует дефолт брокера) — реплеить их нечем.
Единственный способ восстановить исторический счётчик — разовый бэкфилл: прочитать реальные сессии
и оценки прямо из Mongo ai-service (`GET /admin/dialog-sessions` уже показывает эти данные) и
записать недостающие строки `UserDialogScores` в learning-service напрямую, минуя Kafka. Это
межсервисная миграция данных, а не код-фикс, и трогает и БД, и продовые данные — не сделано в этом
прогоне (правило «не трогать прод-БД без явного разрешения»).
**Нужно решение:** стоит ли заказывать такой бэкфилл (разово, для тестовых аккаунтов до продовых
данных) — или считать историю до 40.22 навсегда неполной и оставить как есть, раз таких аккаунтов
немного и они предпродовые.

### Q-4 — настроек уведомлений («Продуктовые обновления», «Напоминания о практике») нет на бэкенде — заводить API или это осознанный локальный тумблер?

В проде и в локальном `main` настройки уведомлений на `/settings` живут **только в `localStorage`**
(`shared/stores/notification-preferences-store.ts`: ключи `notif.practiceReminders`,
`notif.productUpdates`, читаются/пишутся напрямую через `localStorage.getItem`/`setItem`, без единого
`fetch`/`apiClient` вызова) и подключены к UI в `app/(main)/settings/page.tsx`. Проверено, что это не
«фронт не дозвонился до готового API» — API просто не существует:

- В `notification-service` (`src/backend/notification-service`) нет ни одного файла/класса со словом
  «preference», нет модели, нет хранилища. `NotificationController.cs` объявляет ровно 4 маршрута:
  `GET` (список), `GET /unread-count`, `PUT` (отметить одно прочитанным), `PUT /read-all` — никакого
  `GET/PUT /notifications/preferences` или похожего.
- Общий grep `preference|productUpdate|practiceReminder` по всему `src/backend` (регистронезависимо)
  не даёт ни одного релевантного совпадения ни в одном сервисе (только случайные тёзки — `TenantJobScope`,
  `SkillGapSourceRefs` — которые про job-scheduling и рекомендации навыков, не про уведомления).
- Соответственно рассылка писем (`NotificationEmailDispatcher`, `DelayedChatEmailDispatcherService`)
  ничего не проверяет по этим двум флагам — она физически не может, раз хранилища этих настроек на
  бэкенде нет.

Итог: тумблеры на `/settings` управляют только тем, что видит сам пользователь **в этом браузере**.
Они (1) не переживают смену устройства/браузера/очистку localStorage, и (2) никак не влияют на то,
шлёт ли `notification-service` реальные письма/уведомления — «Продуктовые обновления» выключены по
умолчанию (`isProductUpdatesEnabled` дефолтится в `false`), но ничто на бэкенде эту настройку не
читает, так что если/когда появится код, который рассылает «продуктовые обновления», он по умолчанию
разошлёт их всем, включая тех, кто явно выключил тумблер в своём браузере.

Что нужно, если решат строить: (а) таблица/документ `NotificationPreferences` в `notification-service`
(либо в identity-service рядом с профилем — на выбор архитектора) с полями под текущие два тумблера,
per-user, с `GET`/`PUT` через gateway; (б) миграция текущего локального значения в бэкенд при первом
визите после деплоя (иначе все, кто явно всё настроил, читают как «default»); (в) реальная точка
проверки — сейчас непонятно, что вообще шлёт «продуктовые обновления» и «напоминания о практике»:
`DelayedChatEmailDispatcherService` шлёт только про непрочitанные чаты, welcome- и friend-request-
шаблоны существуют, но явного продуктового/ремайндер-рассыльщика в `notification-service` не нашлось
— то есть настройка сегодня не отключает вообще ничего, даже гипотетически, потому что нет и самой
рассылки, которую она должна была бы гасить.

**Нужно решение:** заводить ли backend API и таблицу под эти два тумблера сейчас (полноценная фича,
не багфикс — сознательно не делалось в этом прогоне), или оставить как есть до того, как появится
реальная рассылка, которую нужно будет уважать.

---

## Ответы владельца (заполнить утром)

<!-- Формат: ### Q-N — ответ: ... -->

---

<!-- Дописано прогоном AUDIT_SILENT_WRITES (класс W). Блоки добавлены в конец файла, чтобы не
     трогать раздел «Ответы владельца» выше — нумерация продолжает существующую. -->

### Q-5 — админка лиг/геймификации: починить сообщения об ошибках или убрать раздел?

Находка W-15 в `docs/AUDIT_SILENT_WRITES.md`: на `/admin/leagues` и `/admin/leagues/[id]`
пять мутаций (`adjustXp`, `removeMembership`, `resync`, `moveTier`, `closeWeek`) падают полностью молча — ошибка уходит только в
`clientLogger`. `closeWeek` при этом необратим, а его подтверждение закрывается через
`onSettled`, то есть выглядит выполненным при любом исходе.

При этом по продуктовому решению геймификация (XP / стрики / лиги) из продукта убрана и в UI
никогда не показывается. Обе страницы, однако, по-прежнему в навигации админки
(`src/frontend/app/(admin)/layout.tsx:130-131`).

**Нужно решение:** доводить обработку ошибок на этих экранах (работа ради раздела, которого в
продукте нет) — или убрать «Leagues» и «Gamification» из навигации админки целиком и закрыть
находку удалением? Второй вариант дешевле и убирает заодно риск, что кто-то нажмёт «закрыть
неделю» на снятой с продукта механике.

### Q-6 — онбординг: выбор навыков записывается «по возможности» и его провал не показывается

Находка W-13 в `docs/AUDIT_SILENT_WRITES.md`:
`src/frontend/features/auth/hooks/use-onboarding.ts:18-30` внутри одной мутации делает
`POST /onboarding`, а затем `PUT /skills/enrolled` в `try { … } catch { /* ignore */ }`.
Комментарий объясняет это как осознанный best-effort («user can adjust enrollment later from their
profile»), но пользователю ничего не говорят: онбординг завершается успешно, а на `/tree` человек
видит только базовый `sales-basics` вместо выбранных им навыков.

**Нужно решение** (это выбор продуктового поведения, а не однозначный багфикс):
(а) считать запись навыков частью успеха онбординга — то есть не переходить на `/tree`, а показать
ошибку и дать повторить; (б) оставить best-effort, но честно сказать на первом экране `/tree`
«не удалось сохранить выбор навыков, выбери их в профиле» со ссылкой; (в) оставить как есть.
Пока не менял ничего — это read-only прогон.

### Q-7 — RLS так и не включён, а прод идёт как `Development`: две операционные развилки из аудита tenancy

Находки T-1 и T-2 в `docs/AUDIT_TENANCY.md`. Обе — не код, а конфигурация, и обе требуют решения
человека, потому что переключение ломает работающее поведение.

**T-1.** `docker-compose.yml` подключает рантайм под `${APP_POSTGRES_USER:-$POSTGRES_USER}`, а
`APP_POSTGRES_*` не задан ни в `.env`, ни в `.env.example` (там строки 21-22 закомментированы).
Значит все сервисы ходят под владельцем схемы и суперпользователем, к которому `FORCE ROW LEVEL
SECURITY` не применяется — RLS не фильтрует ничего. Это шаг 12 из
`docs/TENANCY/RUNBOOK.md`, и он честно описан как ещё не выполненный. Изоляцию сейчас держит
только слой EF query filters. **Нужно решение:** выполнять шаг 12 (и тогда сразу решать судьбу
семи фоновых задач с `IgnoreQueryFilters()`, которым нужен либо отдельный `BYPASSRLS`-роль, либо
явная отсрочка) — или сознательно зафиксировать, что RLS остаётся выключенным, и тогда убрать из
комментариев формулировки вида «RLS уже доказал, что вызывающий внутри организации»
(`ContentAuthoringGuard`), потому что сейчас они описывают не то, что происходит.

**T-2.** `docker-compose.yml` ставит `ASPNETCORE_ENVIRONMENT=Development` всем сервисам, а
`docker-compose.prod.yml` (которым и деплоит `scripts/deploy-prod.sh`) это не переопределяет.
Последствия: `InternalServiceAuthFilter` при пустом `INTERNAL_SERVICE_SECRET` становится no-op
(fail-closed включается только вне Development), `POST /demo/token` перестаёт отдавать 404, Swagger
открыт. Порты сервисов проброшены на `127.0.0.1`, наружу торчат только frontend/gateway/grafana,
поэтому это не дыра из интернета — но любой контейнер в compose-сети может дёрнуть
`/internal/memberships/active` с произвольным `X-Organization-Id`. **Нужно решение:** добавить
`ASPNETCORE_ENVIRONMENT=Production` в прод-overlay (и заранее проверить, что при этом не отвалится
что-то, что сейчас молча живёт на Development-ветках) — и отдельно подтвердить, что
`INTERNAL_SERVICE_SECRET` в прод-`.env` реально задан, а не оставлен заглушкой из `.env.example`.

Ничего не менял — прогон read-only.

### Q-8 — редактор упражнений: заводить ли бэкенд-эндпойнт для сохранения порядка (W-9)?

Находка W-9 в `docs/AUDIT_SILENT_WRITES.md`: кнопки ▲▼ в редакторе упражнений
(`src/frontend/app/(admin)/admin/lessons/[lessonId]/exercises/page.tsx` и её копия под
`admin/skills/[id]/topics/[topicId]/lessons/[lessonId]/exercises`) переставляли строки только в
локальном состоянии и никогда не отправляли новый порядок на сервер. Проверил бэкенд:
`AdminExercisesController` (`src/backend/learning-service/Learning/Features/Admin/AdminExercisesController.cs`)
даёт ровно 5 маршрутов — `GET`/`POST` списком, `POST .../import`, `PUT /admin/exercises/{id}` (обновляет
одну запись, включая её `orderInLesson`) и `DELETE /admin/exercises/{id}` — никакого bulk/reorder
маршрута нет. `LessonOrderingTests.cs` (упомянутый в аудите как «у бэкенда есть упорядочивание
упражнений») на самом деле проверяет **сортировку списка уроков внутри навыка** (`GetLessonsForSkillAsync`,
`(TopicOrder, LessonOrder)`), про упражнения там ничего нет — это ссылка аудита оказалась неверной.

Ночью эндпойнт не заводил (правило «не изобретать бэкенд ночью»): кнопки ▲▼ и мёртвая функция
`moveExercise` убраны из обоих экранов, вместо них — пояснение, что порядок фиксируется при создании
упражнения и переставить существующие пока нельзя (коммит `316da24`).

**Нужно решение:** заводить ли `PUT /admin/lessons/{lessonId}/exercises/reorder` (тело —
`[{id, orderInLesson}]`, одна транзакция на все переставляемые строки) и вернуть кнопки — или считать
порядок упражнений внутри урока не важным для продукта и оставить фиксированным при создании. Если
решат заводить: нужна миграция/эндпойнт в learning-service + фронтовая мутация, которая шлёт весь
новый порядок одним запросом (не по одной строке — иначе частичный отказ снова разъедет `sortOrder`).

### Q-9 — AD-7: production showed no error on a failed refetch even though the installed
### `@tanstack/react-query` (and `main`'s `page.tsx`) should have surfaced one — needs a live-prod recheck once prod catches up to `main`

`docs/AUDIT_PROD.md` AD-7 claimed "isError does not fire in TanStack Query v5 on a background
refetch failure when a cache already exists." Checked this against the actual installed package
(`@tanstack/react-query@5.96.0` / `query-core@5.96.0`) by reading `query.js`'s reducer and
`queryObserver.js`'s `createResult`, and by running a live `QueryObserver` script against the real
package: `isError` (and the more specific `isRefetchError`) reliably become `true` on a failed
refetch, with the stale `data` preserved alongside. Full derivation in `docs/DECISIONS.md` under
"AD-7". So the generalized claim does not hold for this repo's version, and E-1..E-18
(`docs/AUDIT_ERROR_MASKING.md`) are not undermined by it — no need to redo them.

What's unresolved: the auditor's actual manual browser test against **production** genuinely showed
no banner and no toast after a forced 500 on "Refresh," twice, over 8 seconds — a real observation,
not a misread. Production is confirmed to run a build older than `origin/main` (this audit's own
preamble note), so its bundled `@tanstack/react-query` version and its copy of `voice/usage/page.tsx`
may differ from what's in `main` today; I could not reconcile the two without either a prod
bundle/version diff or re-running the same forced-500 test against a `main` deploy, and this run's
scope didn't cover redeploying or touching prod.

**Нужно решение / follow-up:** once prod is redeployed from current `main` (which now has the
`isLoadingError`/`isRefetchError` fix, commit hash in `docs/AUDIT_PROD.md`'s AD-7 entry), re-run the
auditor's exact repro (stub `window.fetch` to 500, click "Refresh" twice, wait ~8s) against prod to
confirm the banner now appears. If it still doesn't, that would mean something prod-specific (a
proxy/CDN swallowing the 500, a service worker, a different bundler output) is masking the failure
independently of TanStack Query, and is worth its own investigation rather than more `isError`
plumbing. Given this now checks out at the library level, a full re-verification sweep of the other
17 `isError`-gated fixes against actual refetch failures (not just initial-load failures) is
probably not necessary — but a couple of spot checks on `main` (not prod) would close out any
remaining doubt cheaply if someone wants extra confidence.
