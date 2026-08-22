# Вопросы владельцу — ночной аудит прода (2026-08-20 → утро 2026-08-21)

> Файл для решений, которые агент не принимает сам. Ночью вопросы не задаются в чат
> (никто не прочитает) — они пишутся сюда. Утром владелец отвечает, прогон продолжается.
>
> Находки аудита живут в `docs/AUDIT_PROD.md`, контрактные — в `docs/AUDIT_CONTRACTS.md`,
> блокеры «сделать руками на сервере» — в `docs/DONT_FORGET.md`.

## Требуют решения человека

### Q-1 — запушитtcь и redeploy: без этого прод не увидит ни один ночной фикс
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

**ВАЖНО — сделать ДО пуша и деплоя (иначе прод сломается):** ночной фикс T-2 (`1a7606c`)
пинит `ASPNETCORE_ENVIRONMENT=Production` во всех сервисах прод-оверлея, а внутренний
service-to-service фильтр теперь **fail-closed**. Это значит: если в реальном прод-`.env`
переменная `INTERNAL_SERVICE_SECRET` не задана, межсервисные вызовы после деплоя начнут
честно отдавать 403 вместо того, чтобы молча пропускать всё (как сейчас в Development-режиме).
Поэтому порядок такой: **сначала** убедиться, что `INTERNAL_SERVICE_SECRET` выставлен на сервере,
**потом** пушить и деплоить. Подробности — `docs/DONT_FORGET.md` и Q-7.

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

**Correction (R-10, `docs/AUDIT_NIGHT_REVIEW.md`; verified independently, not on the review's
say-so):** the claim above — "порядка нет" / "there is no way to persist a new order" — is wrong,
and I verified it myself by reading three files, not by trusting either W-9's commit message or
the reviewer's finding second-hand:

- `src/frontend/app/(org)/org/content/lessons/[lessonId]/page.tsx:181-199` (`moveExercise`) already
  persists a reorder today, in production, on the org content-override screen: it computes the
  shifted rows via `moveExerciseInList` and, for every exercise whose `orderInLesson` actually
  changed, awaits `updateExercise.mutateAsync({ exerciseId, body: { ..., orderInLesson } })` — one
  `PUT` per changed row, no bulk endpoint.
- `src/backend/learning-service/Learning/Features/Admin/AdminExercisesController.cs:174-199`
  (`PUT /admin/exercises/{id}`) reads `exercise.OrderInLesson = requestDto.OrderInLesson;` before
  saving — the single-exercise update endpoint already accepts and persists `orderInLesson`. This
  is the same endpoint W-9's own `updateExerciseMut` (`page.tsx:148-150` in both admin screens) was
  already calling for every "Save changes" click.
- So "building a bulk-reorder endpoint isn't a night-run call" was true but beside the point — no
  bulk endpoint is needed to move one row past its neighbor, exactly as the org editor demonstrates.

Restored the ▲▼ buttons in both admin screens
(`app/(admin)/admin/lessons/[lessonId]/exercises/page.tsx` and
`.../admin/skills/[id]/topics/[topicId]/lessons/[lessonId]/exercises/page.tsx`), wired to a new
`moveExercise(fromIndex, toIndex)` that renumbers the local rows, then loops the same
`updateExerciseMut.mutateAsync` (already in each file) over every row whose `sortOrder` changed,
mirroring the org editor's persistence exactly. On failure it rolls the local reorder back (the
mutation's own `onError` already toasts). Fixed in commit (see `docs/AUDIT_NIGHT_REVIEW.md` R-10).
The open decision above — whether a real bulk-reorder endpoint is worth building for atomicity —
still stands and is unaffected by this correction; per-row `PUT`s is what shipped, not a batch
transaction.

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

### Q-10 — AD-5: platform staff can now *read* another organization's real AI quota, but there is still no way to *write* one — needs an owner decision on the write path

`docs/AUDIT_PROD.md` AD-5: `/admin/organizations/<id>/quota` showed the session's own organization's
quota and spend under whichever organization's name was in the URL — the auditor saw identical
numbers for "Acme Sales" and "Sellevate · default". Root cause confirmed by reading the stack
end to end:

- `GET`/`PUT /admin/ai-quota` (`AdminAiQuotaController`, ai-service) both resolved the organization
  from `ITenantContext.OrganizationId`, which comes only from the caller's own `X-Organization-Id`
  header (`org_id` claim on the caller's own token) — never from the URL. For a platform `Admin`/
  `SuperAdmin` with a membership in Sellevate's own default organization (as `admin@sellevate.site`
  has), that header is always their own organization, regardless of which organization's quota page
  they opened.
- The controller's own doc comment said the intended cross-org path was impersonation ("the one
  [organization] they [platform staff] impersonated into, 40.9"), but `PlatformAdminService`
  deliberately mints impersonation tokens with `role: User` (so an impersonation session can never
  start another, or reach any `RequireSuperAdmin` route) — and `AdminAiQuotaController` requires
  `RequirePlatformAdministrator` (`role: Admin`/`SuperAdmin`), which `role: User` never satisfies.
  So the documented mechanism was never actually reachable; this is why the auditor could find no
  working path at all, not even via impersonation.
- This is a real design contradiction, not just an implementation gap, and it is visible in the
  design doc itself: `docs/TENANCY/ADMIN_UI_DESIGN.md` §3.2 says the quota screen "is reachable
  only from within impersonation" and that the org registry's "Quota" link should first enter
  impersonation and then open the screen — but the same document's access table further down lists
  `/admin/ai-quota` under "platform only (RequirePlatformAdmin / RequireSuperAdmin)", which an
  impersonation token can never satisfy. The implementation followed the access table, not the
  impersonate-first note: the "Quota" link in `app/(admin)/admin/organizations/page.tsx` (~line
  273) is a plain `<Link href=".../quota">`, a few lines above the `impersonate()` handler the
  "Impersonate" button already calls — "Quota" never calls it, so opening "Quota" never enters the
  target organization at all, even though the design doc describes it as if it did.

**Fixed tonight (read only):** added `GET /admin/ai-quota/{organizationId}`
(`AdminAiQuotaController.GetQuotaForOrganization` → `AiQuotaService.GetSettingsForOrganizationAsync`),
which reads the named organization's row directly. This is safe as a platform-staff-only *read*: every
caller here is already in platform-wide mode (`RequirePlatformAdministrator` ⇒ `role: Admin`/
`SuperAdmin` ⇒ `TenantContextMiddleware.EnterPlatformMode()`), and `OrganizationQuota`'s own EF query
filter already widens to every organization for platform-wide callers — the new route segment only
narrows an already-cross-tenant-readable query to the organization the screen is showing, instead of
defaulting it to the caller's own. Allow-listed in `scripts/tenancy-boundary-lint.py` accordingly. The
frontend quota screen (`/admin/organizations/[organizationId]/quota`) now reads through this endpoint,
so the numbers shown always belong to the organization named in the URL. `PUT /admin/ai-quota` is
untouched — a save is still only enabled when the session's own organization matches the URL
(`resolveQuotaEditability`), exactly as before, so this fix closes the "silently shows the wrong
organization's numbers as if they were in effect" danger without opening any new write path.

**What is still not possible, and needs a product decision:** a platform admin still cannot *save* a
quota for an organization that is not their own session's. Two different real fixes exist, with
different security trade-offs, and picking one is not a debugging call:

1. **Make impersonation actually reach this endpoint.** Add an ai-service-local authorization policy
   (not the six-service-shared `RequirePlatformAdministrator`) that also accepts a validated
   impersonation token (the `imp: true` claim, which only identity-service's `RequireSuperAdmin`-gated
   impersonation endpoint can mint, and which is fully audited via `ImpersonationAuditEntry`). Every
   downstream layer already does the right thing for an impersonation token with zero further
   changes: its `org_id` claim is the *target* organization, `TenantContextMiddleware` resolves
   `TenantContext.OrganizationId` to it, `TenantSaveChangesInterceptor` and the RLS `WITH CHECK`
   clause both already enforce that a write's `OrganizationId` matches it, and `role: User` keeps the
   session from also becoming platform-wide. The only genuinely open question is a second, unrelated
   blocker: the `(admin)` route layout (`app/(admin)/layout.tsx`) redirects any `role: User` session
   away from every `/admin/**` route, including this one — impersonating currently makes the whole
   admin panel unreachable, so the layout gate would also need a narrow, explicit exception for this
   one screen while impersonating (and *only* this one — the rest of `/admin/**` is platform content
   administration, not per-organization). That is a real, if small, frontend security-relevant change
   and deserves its own sign-off, not a debugger's unilateral call at 2 AM.
2. **Give platform staff a direct write with an explicit organization id**, mirroring the
   `BootstrapOrganizationAdminRequestDto`/`CreateImpersonationRequestDto` carve-out already
   allow-listed in `tenancy-boundary-lint.py` for `RequireSuperAdmin` routes. This would need a new,
   narrowly-scoped write path that does not reuse the ambient per-request `ITenantContext` (which is
   write-once and already resolved to the caller's own organization by the time the controller runs —
   reassigning it mid-request throws by design, `TenantContext.SetOrganization`), so it would have to
   either mint a fresh scoped `DbContext`+`TenantContext` pair for the one write or bypass
   `TenantSaveChangesInterceptor`/RLS deliberately for this one call. Bigger surface than option 1,
   and it stops being "impersonate, then act as that organization" and becomes "platform staff writes
   directly into a customer's tenant without ever assuming its identity" — a different, and arguably
   weaker, security posture than the one every other write in this codebase holds to ("writes widen
   nowhere").
3. **Do nothing further**, and treat quota changes for a customer organization as an operation done
   outside the admin UI (support ticket → a one-off script/migration, logged in `docs/DONT_FORGET.md`
   the way other night-run data fixes are) until the owner decides this screen is worth the extra
   plumbing. The screen already refuses to save silently into the wrong organization, so nothing is
   unsafe about leaving it at read-only for other organizations.

Nothing was changed tonight on the write side beyond documenting this in the controller's own XML
doc comment (`AdminAiQuotaController`) so the next person reading the code sees the gap immediately
rather than rediscovering it.

### Q-11 — AD-2: should the `general` stage exist at all, or should the skill be reassigned?

`docs/AUDIT_PROD.md` AD-2: the skill `pipeline-management` ("Управление воронкой",
`131a011b-efc5-4f7d-b8c5-ecede2dab7cf`) has `stage: "general"` in the database, which is not one of
the 5 stages `GET /skills/stages` returns (`preparation, discovery, engagement, closing, retention`).
Fixed the *display/edit* bug tonight (unknown stages now show as "Другое" instead of leaking the raw
key, and the Edit form's Stage select now has an explicit "— не назначена (general) —" option instead
of silently pre-selecting "Подготовка" while the real value stays `general`) — full derivation in
`docs/AUDIT_PROD.md`'s AD-2 entry.

What was **not** decided, because it's a content/data call, not a code bug: whether `general` should
(a) become a real 6th row in `/admin/skill-stages` (if "general funnel skills that don't map to one
sales stage" is a real category worth keeping), or (b) be corrected to one of the 5 existing stages
for this one skill (which stage "Управление воронкой" / pipeline management actually belongs to is a
product/content judgment call, not something derivable from the code), or (c) some other skill later
gets the same treatment and it's worth deciding the general policy once rather than per-skill.

**Нужно решение:** pick (a), (b), or (c) above for `pipeline-management`, and whether any other future
skill is allowed to ship without a stage assignment at all (in which case an explicit "unassigned"
stage might belong in the stage registry itself, not just in the frontend's fallback rendering).

### Q-12 — AD-3: all 45 production techniques carry no skill links at all — content gap, not a code bug; nobody has written the data

`docs/AUDIT_PROD.md` AD-3: `/admin/techniques`'s skill filter returns 0 techniques for every one of
the 13 skills, because in production every one of the 45 techniques has `primarySkillId: null` and
`additionalSkillIds: []`. Explicitly not a duplicate of A-5 (already fixed, `9b3080a`) — A-5 was a
code bug in `GET /techniques/meta`'s counting (it only matched `PrimarySkillId`, missing techniques
that only carry `AdditionalSkills`); AD-3 is that **both** fields are empty for every row, so no
counting fix can find a link that was never written.

**Traced the whole path end to end looking for a code bug that drops the link, and found none:**

- The join table (`TechniqueSkill`/`AdditionalSkills`) and `Technique.PrimarySkillId` are populated
  correctly by every write path that was checked: `AdminTechniquesController.ApplyPayload` /
  `SyncAdditionalSkills` reconcile the child rows in place from whatever `PrimarySkillId` /
  `AdditionalSkillIds` the payload carries, and round-trip correctly through `Export` →
  `MapToWriteRequest` (which includes both fields) → re-`Import`.
- The admin edit form (`src/frontend/app/(admin)/admin/techniques/page.tsx`, `startEdit`/`handleSave`)
  seeds `primarySkillId`/`additionalSkillIds` from the loaded row and sends the whole form back
  unchanged unless the operator edits the "Primary skill" field — an ordinary wording edit does not
  silently wipe the links.
- A local, **untracked** (`.gitignore`'d, `.claude/` is entirely ignored) dev fixture,
  `.claude/local-seed/techniques.json` + `.claude/local-seed/seed.py`, does the skill-linking
  correctly: the JSON carries `primarySkillIconicName`/`additionalSkillIconicNames` as human-readable
  strings, and `seed.py`'s `seed_techniques()` resolves them to real skill GUIDs before
  `POST /admin/techniques/import`. This is local-only tooling (never committed, cannot have produced
  anything in a deployed environment) with only 10 techniques in it anyway — nowhere close to
  production's 45 — so it is not, and could not be, the origin of production's rows.
- No other technique content of any size exists anywhere in this repository or its leftover
  `.claude/worktrees/*` — nothing that could account for 45 rows.

**Conclusion:** production's 45 techniques were authored or imported through some process this
repository has no record of (a one-off admin-UI JSON import, or content written before skill-tagging
existed), and none of it was ever skill-tagged. This is a content-authoring gap, not a bug — per the
run's instructions, not inventing the missing links. Fixed instead what was fixable without touching
data: `/admin/techniques`'s empty state, which used to say the generic "No techniques found." even
when a skill filter is what produced the zero, now says `No techniques are linked to "<skill
name>" yet.` when a skill filter is active — an honest, specific message instead of a blank-looking
generic line for exactly the situation this audit finding is about.

**Нужно решение:** who owns going through the 45 real techniques and assigning each a primary (and
where relevant, additional) skill through the existing, working `/admin/techniques` edit form or a
prepared `/admin/techniques/import` JSON — this is content work, not a technical fix, and the run did
not invent placeholder skill assignments for real content.

### Q-13 — X-11: аккаунт `admin@sellevate.site` навсегда застрял на 7 из 21 урока; нужен бэкфилл прогресса на проде

**Что видно на проде (прогон 4, аудит упражнений).** В навыке «Первый контакт» все 7 уроков темы 1
(`topicOrder: 1`) имеют статус `completed`, а первый урок темы 2 («Четыре секунды и 86% голоса») и все
остальные 13 уроков — `locked`. Ученику в интерфейсе делать нечего: `/tree` показывает «7 / 21» и
«ОСТАЛОСЬ 14», и ни один урок не открывается.

**Почему само не починится.** Разблокировка первого урока следующей темы (`2fbac93`, «fix: unlock next
topic's first lesson on topic completion», 2026-07-24, уже в `origin/main`) висит на переходе
`!= Completed → Completed`: `ExerciseService.UpdateLessonProgressAsync` выставляет
`transitionedToCompleted` только при этом переходе (`:583-587`), и лишь под этим флагом вызывается
`UnlockNextLessonInTopicAsync` (`:596-603`). Для аккаунта, который закрыл тему раньше, перехода больше
никогда не будет. Проверено эмпирически: все детерминированные упражнения последнего урока темы 1
(`85cbff07-…`, 3 × `choose_option` + 1 × `reorder`) отправлены заново и приняты с `score: 100` —
статус урока 8 не изменился. Ни в админке, ни в org-зоне нет экрана, который правит прогресс
пользователя (там правится контент), так что руками через интерфейс это тоже не открыть.

**Нужно решение (две развилки, обе требуют человека).**
1. Чинить ли это кодом — пересчитывать доступность при чтении дерева (`GetLessonsForSkillAsync`
   уже знает и порядок тем, и все progress-строки: «первый незавершённый после последнего
   завершённого» вычисляется там же без событий) — или писать разовую миграцию/бэкфилл, которая
   пройдёт по всем пользователям и откроет первый урок следующей темы там, где предыдущая тема
   закрыта целиком. Первое лечит и будущие расхождения, второе не меняет поведение сервиса.
2. Разблокировать ли этот конкретный аккаунт на проде **сейчас**, чтобы аудит смог дойти до
   остальных 14 уроков (в них, среди прочего, единственный шанс проверить типы упражнений, которых
   нет в теме 1). Это запись в прод-данные, поэтому агент её не делал: правка `UserLessonProgress`
   на живой базе — решение владельца, а не аудита.

### Q-14 — R-6: the isError-before-empty sweep only covered session/tree/skill/profile; five more screens share the same shape and were not touched tonight

`docs/AUDIT_NIGHT_REVIEW.md` R-6 named the general defect across the E-fix series — `isError` is
checked before the empty case, with no `isLoadingError`/`isRefetchError` split, so a failed
*background* refetch of an already-loaded screen replaces good, still-cached content with the
error state instead of leaving it alone. Fixed tonight (commit `8cd7e975`, plus `0f6a53ee` for
R-5/`/session`) in the four screens the run scope called out as highest-impact: `/session/<id>`,
`/tree`, `/skill/<slug>`, `/skill/<slug>/map`, and `/profile`.

**Not swept** — same `isError`-gates-a-whole-region shape, same fix would apply (swap the
destructive gate to `isLoadingError`, per the AD-7-established split, `docs/DECISIONS.md`):
- `/reference/<materialId>` (E-1) — `app/(main)/reference/[id]/page.tsx`
- `/guidebook` (E-8) — `app/(main)/guidebook/page.tsx`
- `/companies/<id>` (E-9) — `app/(main)/companies/[id]/page.tsx` and its
  `CompanyTimeline`/`CompanyContactsCard` children
- `/friends` and `/friends/<userId>` (E-10) — `app/(main)/friends/page.tsx`,
  `app/(main)/friends/[userId]/page.tsx`
- `/admin/*` content-list screens (E-13..E-17) — out of tonight's scope already per the audit's
  own note that these are owned by a concurrent agent's pass; not re-checked here either.

**Нужно решение / follow-up:** no owner decision needed to fix these — it's the same mechanical
`isError` → `isLoadingError` swap already applied four times tonight. Flagging here only so the
remaining sweep isn't lost: whoever picks this up next should grep for `isError` in
`app/(main)/reference`, `app/(main)/guidebook`, `app/(main)/companies`, `app/(main)/friends`, and
the `app/(admin)/admin/**` content-list pages, and apply the same `isLoadingError` gate wherever
`isError` currently discards an already-rendered region rather than only gating an empty state.

**Update (2026-08-21) — swept, no owner decision was needed.** Applied the mechanical
`isError`/bare-`error` → `isLoadingError` swap to every screen listed above, plus every admin
content-list screen found by grepping the whole `app/(admin)/admin/**` tree (broader than the
E-13..E-17 subset originally named — the same shape also turned up in a few screens Q-14 didn't
enumerate). `docs/AUDIT_NIGHT_REVIEW.md` R-6 is now `[x]`.

Files touched:
- `app/(main)/reference/[id]/page.tsx` (E-1)
- `app/(main)/guidebook/page.tsx` (E-8)
- `app/(main)/companies/[id]/page.tsx` (E-9) — the top-level `error`/`isLoadingError` gate plus the
  `errorMessage` props fed to `CompanyContactsCard` and `CompanyTimeline` (those two components
  needed no change themselves — the destructive gate lived in the page, not the component).
  `CompanyReadinessCard`/`CompanyBriefingCard` needed no change: they already gate on data
  presence (`hasScore`/`hasContent`), not on `isError`.
- `app/(main)/friends/page.tsx` (E-10, the friends-grid gate only — the incoming-requests banner
  was already additive and non-destructive). `app/(main)/friends/[userId]/page.tsx` needed no
  change: it already gates on `!profile` (data presence).
- Admin content-list/detail screens: `organizations/page.tsx` (both the organizations table and
  the impersonation-audit list), `skill-stages/page.tsx`, `users/page.tsx`, `prompts/page.tsx`
  (both the prompts list and the rewards list), `lessons/page.tsx`, `skills/page.tsx`,
  `reference/page.tsx`, `topics/page.tsx`, `techniques/page.tsx`, `leagues/tiers/page.tsx`,
  `skills/[id]/page.tsx`, `skills/[id]/topics/[topicId]/page.tsx` (three gates: skills, topics,
  lessons), `skills/[id]/reference/page.tsx`, `lessons/[lessonId]/exercises/page.tsx`,
  `skills/[id]/topics/[topicId]/lessons/[lessonId]/exercises/page.tsx`, `demo-requests/page.tsx`,
  `dialog/page.tsx`, `dialog/[bundleId]/page.tsx`, and
  `organizations/[organizationId]/quota/page.tsx` (the `SpendPanel`'s `isError` prop).
- **Already correct, left untouched:** `admin/voice/usage/page.tsx` (already used the full
  `isLoadingError`/`isRefetchError` split — a good reference implementation), `admin/quotes/page.tsx`
  and `admin/discuss/page.tsx` (error banners already additive, never gating the list/calendar),
  `admin/gamification/page.tsx`/`admin/leagues/page.tsx`/`admin/dialog/page.tsx`'s mutation
  `.isError` banners (mutations don't have `isLoadingError`/`isRefetchError` and don't discard
  fetched data — not the R-6 shape at all).
- One pre-existing test needed its mocks updated to match the more precise gate:
  `__tests__/CompanyPage.test.tsx`'s two error-scenario mocks (`data: undefined, error: ...`) now
  also set `isLoadingError: true`, since that is exactly what a first-load failure with no data is
  — the mock was just missing a field the real hook always computes.
- Verified with `npx tsc --noEmit` (clean) and `npx vitest run` (977/977 passing) in
  `src/frontend`.

### Q-15 — X-7: `spot_mistake`'s explanation field was scored as half the grade while labelled optional; resolved as "the line alone passes", not "make the field mandatory"

**The conflict.** `spot_mistake`'s textarea is labelled «Объясни, почему это ошибка (необязательно)»
and `docs/NEW_EXERCISE_TYPES.md`'s own description says the learner "optionally explains why." But
`SpotMistakeEvaluationStrategy.EvaluateAnswerAsync` (ai-service) scored the correct line at only 50 of
100 points and only added the rest by grading a written explanation through the AI — so a learner who
selected the right line and left the (labelled-optional) field blank scored 50 and saw «Почти», and
`isCorrect: false` (the strategy's own `score >= 75` gate). Reproduced live in the audit run
(`docs/AUDIT_PROD.md` X-7): same exercise, right line, no explanation → `score: 50`; right line with a
154-character explanation → `score: 90`, `isCorrect: true`.

**The two ways to make the label and the grade agree, and why the line-alone option was chosen.**
1. Grade the line alone as full credit — the explanation still runs through the AI grader when
   written (for its own feedback), but never lowers the score the line already earned.
2. Mark the field mandatory and block "Проверить" until it is filled — the opposite fix, which would
   need a real frontend behaviour change (a new validation gate) and would contradict the exercise's
   own authored label and the type's documented design ("optionally explains why").

Option 1 was picked and shipped (this repo, `SpotMistakeEvaluationStrategy.cs`): it makes the code
match the label and the documented design instead of changing product behaviour a learner already
sees advertised as optional, and it is the smaller change. This is a product-intent call an audit
agent should not make unilaterally by convention, so it is recorded here per that convention; the
author of the label ("необязательно") already made the actual call — this fix only makes the scoring
consistent with it. If the owner instead wants explanation graded as required content (option 2),
that is a reversal of this decision, not a bug fix, and should update the label, add a submit-gate on
the frontend, and record why in `docs/DECISIONS.md` next to this entry.

**What shipped:** the mistake line alone now scores 100 and `isCorrect: true`; a written explanation
is still sent to the AI grader and its feedback is still shown, but it can no longer reduce the score.
`docs/DECISIONS.md` records the same call; `Ai.Tests/Unit/SpotMistakeEvaluationStrategyTests.cs`'s
existing "no explanation" test was updated to assert the new (100, correct) outcome instead of the old
(50, incorrect) one it had been asserting — that assertion previously encoded the bug being fixed.

**Owner's answer (2026-08-21): the middle option — 70, not 100.** Neither "the line alone scores
100" (what shipped in `90d5e491`) nor the original "the line alone scores 50" is right. A correct
line alone now scores **70** (`isCorrect: true`) — the same number the session gate already treats as
a pass (`>= 70`, `app/session/[lessonId]/page.tsx:28`'s `PASSING_SCORE_THRESHOLD * 10`), so a learner
who does exactly what the exercise asks passes, no more and no less. A written explanation stays
optional and is still graded by the AI, but now as a genuine bonus: it can raise the score up to 100
and can never pull it back below 70, even when the AI grades it poorly or its response fails to
parse. Implemented in `SpotMistakeEvaluationStrategy.cs` (see the `fix:` commit that follows this
answer in `git log` for the exact hash); `Ai.Tests/Unit/SpotMistakeEvaluationStrategyTests.cs`'s
"no explanation" test was updated again, from `(100, true)` to `(70, true)`. Recorded in
`docs/DECISIONS.md` under the same Q-15 entry.

### Q-16 — X-4 side effect: pressing «Пропустить» on every exercise now completes the lesson and unlocks the next one, at score 0

**What shipped.** `d7b090d1` (X-4) made "Skip" record a real, ungraded `UserExerciseAttempt`
instead of advancing the client queue with the server never seeing anything. That was the right
call for the bug it fixed — before it, a lesson finished entirely by skipping showed «Урок
завершён» on the client while the backend's completion gate never closed, so the lesson stayed
open forever and nothing after it ever unlocked.

**The consequence nobody wrote down.** `ExerciseService.UpdateLessonProgressAsync` computes
`attemptedExercises` as "distinct exercises of this lesson with **any** attempt row" — a skip is
indistinguishable from an answer there. So `allAttempted` becomes true, the lesson flips to
`Completed` with `BestScore = 0`, `UnlockNextLessonInTopicAsync` fires, and `6893c2ad`'s new
read-time unlock derivation (which also chains off `Completed`) reports the next lesson
`available`. Net effect: **a learner can walk the entire skill tree by pressing «Пропустить»,
finishing every lesson at 0 %.** Nothing gates on the score: `PASSING_SCORE_THRESHOLD`
(`app/session/[lessonId]/page.tsx:106`) only decides whether an exercise goes into the
end-of-lesson mistakes-review round, not whether the lesson counts.

**Why this is a question and not a finding to fix unilaterally.** The three ways out are all
product calls, not bug fixes:

1. **Leave it.** Skipping is a legitimate "I don't want to do this one" affordance, progress is
   the learner's own business, and `BestScore = 0` already records the truth for anyone looking
   at the admin panel or team analytics. Simplest, and consistent with the current "attempted,
   not passed" completion gate.
2. **Count only non-skipped attempts towards completion, but keep the attempt row.** Restores the
   integrity of "completed", and X-4's original bug does not come back for a learner who actually
   answers — but a lesson finished *entirely* by skipping goes back to never closing, which is the
   dead end X-4 was filed about. Would need a separate way out of that dead end (e.g. a skip
   budget per lesson).
3. **Require a minimum score to complete a lesson** (i.e. move the completion gate from
   "attempted" to "passed"). The largest change: it reverses the current documented gate, affects
   every existing learner's progress, and would need a backfill decision for accounts already
   marked complete at low scores.

**Recommendation if no answer comes:** option 1 (leave it) plus one line in
`docs/LEARNING_SERVICE.md` making it explicit that lesson completion means *attempted*, not
*passed*, and that skipping counts — so the next person reading the unlock chain does not
rediscover this as a bug. Option 2 or 3 should not be taken by an audit agent without the owner:
both change what "completed" means for data already in the database.

Raised from `docs/AUDIT_NIGHT_REVIEW.md` R2-7 (second review, commits `7870d60f..90d5e491`).

**Owner's answer (2026-08-21): option 1 — leave it, no code change.** Skipping every exercise in a
lesson still completes it at `BestScore = 0` and unlocks the next lesson. Maximum learner freedom —
nobody gets permanently stuck on an exercise they cannot or will not do — at the accepted cost that
the whole skill tree can be flipped through by pressing «Пропустить» without learning anything;
`BestScore = 0` stays the honest record of that for anyone looking (admin panel, team analytics).
Recorded as a deliberate decision in `docs/DECISIONS.md`, with the one-line clarification the
original finding recommended added to `docs/LEARNING_SERVICE.md`'s "Lesson progression / unlocking"
section: completion means *attempted*, not *passed*, and skipping counts as attempted.
