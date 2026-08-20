# Аудит изоляции организаций (tenancy) — ночной прогон 2026-08-21

Только чтение кода. Ничего не запускалось против живых БД. Ветка `main`, HEAD `3c53769`.

## Охват

Перечислил все **330** HTTP-действий в **76** контроллерах восьми сервисов вместе с их
`[Authorize]`-политикой, `[TenantScoped]`, `[TenantTransaction]` и `ServiceFilter`; из них **137**
берут идентификатор ресурса из маршрута/query/тела. Для всех 137 прочитал гейт, а путь до данных
(сервис → DbContext/репозиторий → query filter/RLS/явный предикат) — примерно для 80, выбранных по
классам риска: организация, пользователь, задание, компания, сессия диалога, тред, оверрайд
контента, приглашение, membership.

## Что уже гарантировано (не отчитываюсь по этому)

- **Организация никогда не берётся из запроса.** `scripts/tenancy-boundary-lint.py` запрещает
  `OrganizationId` в request-DTO, `[FromQuery]/[FromRoute] organizationId` и сегмент
  `{organizationId}` в шаблоне маршрута; исключения — только `PlatformAdminController` (impersonation /
  bootstrap, `RequireSuperAdmin`) и `InternalOrganizationBootstrapController` (m2m). Проверил: обе
  записи в allow-list живые и соответствуют описанию.
- **`X-Organization-Id` не подделать.** `src/backend/gateway/Gateway/IdentityForwarding.cs:25-28`
  сначала `Remove` всех трёх identity-заголовков, потом ставит их только из валидированного
  принципала (`org_id`). `TenantContextMiddleware` читает организацию исключительно из этого
  заголовка, а платформенный режим — только из клейма `role` валидированного токена
  (`building-blocks/BuildingBlocks/Tenancy/TenantContextMiddleware.cs:36-60`).
- **`AddDbContextPool` запрещён** (`scripts/tenancy-pool-lint.py`) — утечки query-filter между
  тенантами через пул контекстов нет.
- **Запись под чужую организацию** блокируется `TenantSaveChangesInterceptor` (сравнение с
  `OriginalValues`, immutable `OrganizationId`) + `WITH CHECK` в RLS.
- **Route parity** (`src/backend/route-parity/RouteParity.Tests/`) гарантирует, что каждый публичный
  маршрут контроллера есть в таблице шлюза.

---

## Находки

### [ ] T-1 RLS выключен во всех развёрнутых окружениях — рантайм ходит под суперпользователем
- **Эндпоинт:** не эндпоинт, а конфигурация всего бэкенда.
  `docker-compose.yml:38,205,333,367,409,452,…` — `ConnectionStrings__Postgres=…;Username=${APP_POSTGRES_USER:-$POSTGRES_USER}`;
  `.env.example:21-22` — `APP_POSTGRES_USER`/`APP_POSTGRES_PASSWORD` закомментированы;
  `.env` (текущий) их не содержит вовсе;
  `scripts/deploy-prod.sh:13` — прод = `docker-compose.yml` + `docker-compose.prod.yml`, а overlay эти переменные не переопределяет.
- **Что не проверяется:** предполагается, что «настоящая граница» — это RLS
  (`docs/TENANCY/TENANCY.md` §1.5, `TenantRlsMigrationBuilderExtensions` ставит `ENABLE`+`FORCE`+`USING`+`WITH CHECK`).
  Фактически рантайм подключается под `${POSTGRES_USER}` — владельцем схемы и суперпользователем
  кластера, а `FORCE ROW LEVEL SECURITY` к суперпользователю **не применяется**. Это прямо написано
  в `docs/TENANCY/RUNBOOK.md:117-119` и вынесено в «Шаг 12», который ещё не выполнен.
- **Атака:** прямой эксплуатации через HTTP я не нашёл — реально изоляцию сейчас держит слой 2
  (EF global query filters), и он покрывает практически те же таблицы, что и RLS-политики (сверил
  списки: `LearningDbContext.cs:142-201` vs `20260816113551_RefreshTenantPoliciesForPlatformStaff.cs:34-50`,
  и так же в ai/company/gamification/social/identity/organization). Опасность в том, что все
  места, которые в комментариях ссылаются на RLS как на доказательство, сейчас опираются на
  один-единственный слой, который снимается одним `IgnoreQueryFilters()` или одним сырым SQL:
  `ContentAuthoringGuard.MayAuthor` (`learning-service/.../Features/Content/ContentAuthoringGuard.cs:44-46`)
  буквально написан как «строка с владельцем принадлежит этой организации, RLS уже доказал, что
  вызывающий внутри неё»; `DialogSessionRepository`/`ChatConversationRepository` — единственная
  защита Mongo; семь фоновых задач с `IgnoreQueryFilters()` вообще без бэкстопа.
- **Уверенность:** точно (по коду compose + RUNBOOK; на живой БД проверяется одним
  `SELECT current_user, usesuper` — см. RUNBOOK шаг 11)
- **Severity:** major

### [x] T-2 Прод запускается с `ASPNETCORE_ENVIRONMENT=Development`, что отключает `InternalServiceAuthFilter` при пустом секрете

**Исправлено (2026-08-21, ночной прогон).** `docker-compose.prod.yml` теперь переопределяет
`ASPNETCORE_ENVIRONMENT=Production` для всех десяти .NET-сервисов (identity, ai, analytics,
notification, gamification, social, learning, company, organization, gateway). Проверено
`docker compose -f docker-compose.yml -f docker-compose.prod.yml config` — во всех десяти
резолвится `Production`; база одна (`scripts/dev-*.sh`) по-прежнему резолвится в `Development` —
локальная разработка не тронута. `InternalServiceAuthFilter` уже fail-closed вне Development при
пустом секрете (40.34) — теперь эта ветка реально доступна в проде. Остаток — человеку, записан в
`docs/DONT_FORGET.md`: подтвердить, что `INTERNAL_SERVICE_SECRET` реально задан в прод-`.env`
(иначе после деплоя internal-вызовы начнут шумно отвечать 403 вместо тихого allow), и что
`/demo/token`/Swagger действительно закрылись на живом стенде.
- **Эндпоинт:** все `/internal/*` и `/ai/*` маршруты. Фильтр:
  `learning-service/Learning/Common/Security/InternalServiceAuthFilter.cs:40-46`
  (а также идентичные копии `identity-service/Identity/Common/Security/InternalServiceAuthFilter.cs`,
  `ai-service/Ai/Features/Evaluation/InternalServiceAuthFilter.cs`).
  Конфигурация: `docker-compose.yml:34,201,273,297,329,363,405,448,484,533` — `ASPNETCORE_ENVIRONMENT=Development`
  для **всех** сервисов, и `docker-compose.prod.yml` это не переопределяет.
- **Что не проверяется:** фильтр «fail closed» только вне Development:
  `if (string.IsNullOrWhiteSpace(_expectedSecret)) { if (_isDevelopment) return; … 403 }`.
  Секрет приходит из `InternalAuth__ServiceSecret=${INTERNAL_SERVICE_SECRET}`; docker подставит
  пустую строку, если переменной в `.env` нет, и тогда в Development фильтр становится no-op —
  ровно та регрессия, которую закрывал 40.34, но закрытая только для non-Development.
- **Атака:** любой процесс на хосте или любой контейнер внутри compose-сети (grafana, prometheus,
  kafka-ui, frontend) делает
  `GET http://identity:8080/internal/memberships/active` с заголовком `X-Organization-Id: <любой guid>`
  и получает полный ростер произвольной организации (`userIds` + `administratorUserIds`,
  `InternalMembershipsController.cs:86-100`). Точно так же
  `GET http://learning:8080/internal/assignments/practice-context?userId=…&modeKey=…` отдаёт название
  и персону задания чужой организации. На этих маршрутах организация **штатно** берётся из
  клиентского заголовка (это задокументированное исключение §1.3), поэтому единственный гейт —
  общий секрет; при пустом секрете гейта нет.
  Из интернета это не достаётся: все порты сервисов проброшены как `127.0.0.1:…` и traefik
  маршрутизирует только frontend/gateway/grafana — нужен плацдарм на хосте или в docker-сети.
- **Уверенность:** точно в части «прод идёт как Development»; эксплуатируемость надо проверить на
  живом стенде (задан ли `INTERNAL_SERVICE_SECRET` в проде — в `.env.example:43` только заглушка)
- **Severity:** major

### [ ] T-3 Ordinary member видит справочник пользователей всей платформы (имя + проверка существования email)
- **Эндпоинт:** `GET /friends/search?query=…`
  (`social-service/Social/Features/Friends/FriendController.cs:158`, реализация
  `Features/Friends/Services/Implementation/FriendService.cs:308-315`) и
  `GET /friends/profile/{targetUserId}`
  (`FriendController.cs:190`, реализация `FriendService.cs:354-356`).
- **Что не проверяется:** принадлежность найденного пользователя организации вызывающего.
  `UserReplicas` намеренно не тенант-скоупная (нет `OrganizationId`, нет query filter в
  `SocialDbContext.cs:56,68-88`, нет RLS-политики), а `SearchUsersAsync` ищет `ILike` по
  `DisplayName` **и по `Email`** по всей таблице; `GetPublicProfileAsync` берёт реплику по
  произвольному `targetUserId` без единого условия по организации.
- **Атака:** обычный участник организации A с любым валидным токеном:
  `GET /friends/search?query=ivanov@konkurent.ru` — если 200 с непустым списком, значит такой
  сотрудник есть на платформе, и в ответе придёт его `DisplayName` и `userId`; далее
  `GET /friends/profile/{этот userId}` подтверждает имя. Плюс `POST /friends/requests` создаёт
  заявку в чужую организацию (принять её нельзя — `Friendships` тенант-скоупная — но сам факт
  существования человека уже раскрыт). Утечки прогресса/переписки нет: XP/лидерборд в этих DTO
  жёстко нули, чат и обсуждения тенант-скоупные и проверены отдельно.
- **Уверенность:** точно. Это **известное и сознательно отложенное** решение, а не пропуск:
  `docs/DECISIONS.md:2985-3000` («что это оставляет открытым, сказано прямо») + `docs/DONT_FORGET.md`.
- **Severity:** minor

### [ ] T-4 Гейт «кто может править версию урока» читает БД вне транзакции и трактует «0 строк» как «можно»
- **Эндпоинт:** `POST /admin/lessons/{lessonId}/versions/draft` и `POST /admin/lessons/{lessonId}/versions/publish`,
  `learning-service/Learning/Features/Admin/AdminLessonVersionsController.cs:109-125` (гейт),
  вызовы на строках 66 и 87.
- **Что не проверяется:** контроллер — единственный из «контентных» admin-контроллеров
  learning-service **без** `[TenantTransaction]` (сравни `AdminLessonsController.cs:19`,
  `AdminExercisesController.cs:22`, `AdminReferenceController.cs:19`, `AdminTechniquesController.cs:22`).
  Гейт делает `database.Lessons.Where(id == lessonId)` до того, как сервис откроет свою транзакцию,
  то есть без `SET LOCAL app.organization_id`, и на пустой выборке возвращает `null` = «разрешено»,
  оставляя решение сервису.
- **Атака:** воспроизводимой атаки нет. Проверил обе конфигурации: глобальный урок
  (`OrganizationId IS NULL`) виден и с выключенным GUC (контентная политика и EF-фильтр допускают
  `NULL`), поэтому ветка `isGlobalLesson → Forbid()` срабатывает; чужой org-owned урок отфильтрован
  и дальше сервис отдаёт 404. Отчитываюсь как о форме: fail-open гейт + чтение вне тенант-транзакции
  в одном методе — это ровно тот шаблон, который сломается от следующей правки контентной политики.
- **Уверенность:** defence-in-depth
- **Severity:** minor

### [ ] T-5 В ai-service пять из шести читающих методов `DialogService` не открывают тенант-транзакцию
- **Эндпоинт:** `GET /dialog/bundles`, `GET /dialog/company-call-mode`, `GET /dialog/custom-scenario-mode`,
  `POST /dialog/sessions` (через `GetModeByIdAsync`) —
  `ai-service/Ai/Features/Dialog/Services/Implementation/DialogService.cs:84,92,119,128,138`;
  сосед `GetActiveModesForBundleAsync` (там же, строка 110) транзакцию открывает.
  `DialogController` (`Features/Dialog/DialogController.cs:36-39`) — единственный dialog-контроллер
  без `[TenantTransaction]` (есть у `AdminDialogController.cs:42`,
  `AdminDialogSessionsController.cs:40`, `AdminDialogOverridesController.cs:28`).
- **Что не проверяется:** предполагается, что границу держит RLS на `DialogBundles`/`DialogModes`
  (`20260815154837_AddOrganizationId.cs:68`, `EnableTenantRlsForContent`), но без транзакции
  `SET LOCAL` не выставляется. Утечки нет: EF query filter
  (`AiDbContext.cs:72,74`, `IsPlatformWide || OrganizationId == null || == current`) в этих
  запросах остаётся и он корректен.
- **Атака:** чужие режимы диалога получить нельзя. Практический эффект обратный и проявится
  после включения RLS (T-1, шаг 12): свои org-authored режимы станут невидимы участникам
  организации — тихий fail-closed без ошибки в логе, то есть тот самый сценарий, ради которого
  в learning-service и завели `[TenantTransaction]`.
- **Уверенность:** defence-in-depth (после шага 12 — функциональный регресс, не утечка)
- **Severity:** minor

### [ ] T-6 `[TenantScoped]` расставлен непоследовательно
- **Эндпоинт:** атрибута нет ни на одном контроллере learning-service, кроме
  `InternalAssignmentsController.cs:33`; нет на `company-service/.../CompanyController.cs:26-28`;
  в ai-service есть на `AdminAiQuotaController.cs:56`, но нет на соседях
  `AdminAiUsageController.cs:30-33`, `AdminDialogSessionsController.cs`, `DialogController.cs`.
- **Что не проверяется:** ничего не «не проверяется» — атрибут лишь отвечает 403 на запрос без
  организации и без платформенной роли (`TenantContextMiddleware.cs:51-57`). Там, где его нет,
  запрос без организации доходит до данных, и дальше всё зависит от того, fail-closed ли слой ниже.
- **Атака:** утечки не нашёл. Проверил три исхода для запроса без организации: strict-таблицы →
  `OrganizationId == null` → 0 строк; контентные таблицы → только глобальная библиотека (это и есть
  задуманное поведение); Mongo и Redis → `RequireOrganizationId()` / `OrganizationPrefix()` бросают
  (`DialogSessionRepository.cs:384-386`, `ChatConversationRepository.cs:207`,
  `notification-service/.../RedisKeys.cs:53-61`). То есть слой ниже везде fail-closed, и
  отсутствие атрибута — потеря явного 403, а не граница.
- **Уверенность:** defence-in-depth
- **Severity:** minor

---

## Проверено и чисто

Ниже — срезы, где я прочитал и эндпоинт, и его гейт, и путь до данных, и не нашёл ни IDOR, ни
разрыва в авторизации. Это тоже результат.

**Внутренний служебный контур (пункт 4 задания) — заявление подтверждено независимо.**
В таблице маршрутов шлюза (`src/backend/gateway/Gateway/appsettings.json`, 94 маршрута, ни одного
`{**catch-all}`-корня и никакого fallback) нет ни одного префикса `/internal` или `/ai`. Прогнал
сверку префиксов по всем 17 внутренним путям — ни один не покрывается ни одним маршрутом шлюза:
`internal/skills/lookup`, `internal/assignments/practice-context`, `internal/memberships/active`,
`internal/organizations/{organizationId}/bootstrap-admin`, `ai/chat`, `ai/chat/stream`, `ai/tts`,
`ai/evaluate`, `ai/content/{structure,generate,rewrite,review}`, `ai/quota/preflight`,
`ai/companies/{briefing,persona,readiness,parse-log}`. Все 17 несут
`[ServiceFilter(typeof(InternalServiceAuthFilter))]`. Плюс это удерживается тестом
`route-parity/RouteParity.Tests/ControllerGatewayRouteParityTests.cs`. Оговорка — T-2.

**Заголовок организации.** `IdentityForwarding.Apply` (`gateway/Gateway/IdentityForwarding.cs:25-52`)
безусловно снимает `X-User-Id`/`X-User-Role`/`X-Organization-Id` и ставит их только из
валидированного принципала. `TenantContextMiddleware` читает организацию только из этого заголовка,
а платформенный режим — только из клейма `role`. Фронтенд обращается исключительно к шлюзу
(`src/frontend/config/environment.ts:2`) и нигде не формирует эти заголовки. Токен impersonation
выпускается на организацию из тела запроса, но сам эндпоинт — `RequireSuperAdmin`
(`PlatformAdminController.cs:28`), а сам токен не даёт платформенных прав.

**identity-service — memberships.** `Memberships` не имеет ни query filter, ни RLS (в
`IdentityDbContext.cs` это единственная тенант-таблица без фильтра — `Invites`), поэтому проверил
**все** 11 обращений к `.Memberships`: в каждом есть явный предикат по организации или по
`UserId == user.Id` (`MembershipsController.cs:59,102`, `InternalMembershipsController.cs:88`,
`InviteService.cs:121-123,324-327`, `AuthenticationService.cs:510-512`,
`OrganizationBootstrapService.cs:174`, `PlatformAdminService.cs:127`,
`OrganizationAuthConfigurationResolver.cs:67`). `DELETE /memberships/{userId}` дополнительно
требует `RequireOrgSuperAdmin` при `RequireOrgAdmin` на классе — это осознанная разница, а не
рассинхрон.

**company-service (CRM «компании» — двойной скоуп org + user).** Проверил все 26 методов
`CompanyService`: каждый открывает `TenantTransactionScope` первым оператором и каждый
sub-resource-запрос несёт собственный `UserId == userId`, а не наследует проверку родителя.
Все 5 таблиц под strict query filter + RLS. Ни один из 26 эндпоинтов `CompanyController`,
берущих `companyId`/`contactId`/`personaId`/`entryId` из маршрута, не отдаёт чужую строку.

**Mongo (транскрипты диалогов и чат).** `DialogSessionRepository` и `ChatConversationRepository` —
единственные владельцы своих коллекций, каждый фильтр начинается с `TenantReadFilter()`/
`TenantWriteFilter()`, чтение расширяется для платформенного персонала, запись — нигде,
неустановленный тенант бросает. `GET /admin/dialog-sessions/{sessionId}` идёт через
`FindForOrganizationAsync` (org-фильтр), `GET /dialog/sessions/{sessionId}` — через
`SessionOfUserForReadFilter` (org + userId). `GET /chat/conversations/{conversationId}/messages`
— org + участник.

**Redis-only сервисы.** notification (`Common/Constants/RedisKeys.cs`) и analytics
(`Features/Presence/Services/Implementation/PresenceTracker.cs:36`) префиксуют каждый
организационный ключ `org:{orgId}:` и бросают на пустой организации. Два намеренно
непрефиксованных ключа (`notifications:chat-email:pending`, `notifications:user:{userId}`)
задокументированы и не содержат тенант-данных.

**learning-service — контентные admin-эндпоинты (самый подозрительный срез).** `RequireOrgAdmin`
на `AdminLessonsController`/`AdminExercisesController`/`AdminReferenceController`/
`AdminTechniquesController` — против `RequirePlatformAdmin` на соседних
`AdminSkillsController`/`AdminTopicsController`/`AdminSkillStagesController`/
`AdminDailyQuotesController`/`AdminExerciseTypePromptsController`/`AdminSeederController` — это
не рассинхрон, а фаза 40.18: разницу закрывает `ContentAuthoringGuard` (`MayAuthor` требует
платформенных прав для строки с `OrganizationId IS NULL`, т.е. РОП не может править общую
библиотеку) плюс `[TenantTransaction]` на каждом из четырёх. Проверил все 11 вызовов гейта.

**learning-service — задания, программы, оверрайды, ревью.** `Assignment`, `AssignmentProgress`,
`ProgramVersion/Item/Enrollment`, `UserDialogScore`, `DialogReviewNote`, `ContentGenerationJob`,
`ContentAdaptationJob/Item`, `TeamSkillGapDismissal` — все под strict query filter (`== current`,
без ветки «или глобальный») и все под `EnableTenantRls`. Все публичные методы сервисов открывают
`TenantTransactionScope` (проверил скриптом по всем `Features/**`; единственные исключения —
приватные хелперы внутри уже открытого скоупа). `POST /dialog-reviews/disputes` сверяет
`score.UserId != authorUserId`, `POST /dialog-reviews/{noteId}/acknowledge` — `SubjectUserId ==
actorUserId`: обычный участник не дотянется ни до оценки, ни до коуч-заметки коллеги.

**Ростер для admin-экранов.** `IdentityOrganizationMemberDirectory` кладёт на провод
`X-Organization-Id` строго из `ITenantContext` и бросает при отсутствии организации — id
организации ниоткуда из запроса не берётся.

**Обходы слоёв.** Все 7 `IgnoreQueryFilters()` — в фоновых sweep-задачах (системный режим,
зарегистрированы в `docs/TENANCY/BACKGROUND_JOBS.md`); все 6 `ExecuteUpdateAsync` — либо внутри
открытой тенант-транзакции и с сохранённым query filter (4 в learning), либо по нетенантным
таблицам `RefreshTokens` (2 в identity); 2 `ExecuteSqlInterpolated` подставляют
организацию из `ITenantContext` параметром. `ExecuteDeleteAsync` есть только в двух identity-
клинапах нетенантных таблиц. `AddDbContextPool` не встречается нигде (линт).

**Порядок middleware.** Во всех восьми сервисах `UseSellevateTenantContext()` идёт после
`UseAuthentication()`/`UseAuthorization()` и до `MapControllers()` (в gamification — через
`GamificationApplicationBuilderExtensions.cs`), т.е. `[TenantScoped]` действительно виден
middleware, а клейм `role` уже разобран.

**`POST /demo/token`** отдаёт 404 в Production и минтит токен без `role` и без `org_id`
(`DemoTokenController.cs:31-34,42-48`) — в тенант-плоскости безвреден. Но см. T-2: прод идёт как
Development, так что 404 там не срабатывает; токен без организации всё равно упирается в
fail-closed слои.

## Не дошёл

- Не проверял фронтенд на клиентские проверки роли (аудит про бэкенд-границу).
- Не проверял Kafka-консьюмеры и фоновые задачи построчно — опирался на
  `docs/TENANCY/BACKGROUND_JOBS.md` как на реестр, выборочно сверив только режимы, к которым
  ведут HTTP-пути.
- Не проверял gamification-эндпоинты содержательно (по `MEMORY.md` XP/лига/стрики выведены из
  продукта); ограничился тем, что все они `RequirePlatformAdmin` + `[TenantScoped]` либо
  собственные данные пользователя.
- Ничего не проверял на живой БД: `usesuper` для рантайм-роли и фактическое наличие
  `INTERNAL_SERVICE_SECRET` в прод-`.env` (T-1, T-2) остаются к подтверждению на стенде.
