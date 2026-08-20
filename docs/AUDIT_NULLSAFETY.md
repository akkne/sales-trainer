# Аудит null-safety на границе DTO ↔ фронт

Класс баги — как C-4 (`TeamSkillMapMemberDto.DisplayName`, исправлен в `d854e4c`):
бэкенд отдаёт `null`, фронт типизирует поле non-null и разыменовывает без охраны.

Метод: (1) поля из реплицированных данных (`UserReplicas`, `OrganizationReplicas`,
`OrganizationProfileReplica`) → (2) поля, null только на одной ветке метода →
(3) общий проход по nullable-членам DTO против их фронтовых зеркал.

Проверено попарно (бэкенд + фронт), не по одной стороне.

---

## Срез 1 — реплицированные данные (eventually consistent)

### Проверено и ЧИСТО (не находки)

- `AssignmentDashboardRowDto.DisplayName` (`string?`,
  `src/backend/learning-service/Learning/Features/Assignments/Models/AssignmentDashboardDto.cs:59`)
  — фронт типизирует честно `displayName: string | null`
  (`src/frontend/features/org-assignments/types/assignment.ts:111`) и охраняет в
  `describeRecipientName` (`src/frontend/features/org-assignments/components/remind-dialog.tsx:39`).
- `DialogReviewNoteDto.SubjectDisplayName` / `AuthorDisplayName` (`string?`,
  `src/backend/learning-service/Learning/Features/DialogReviews/Models/DialogReviewDtos.cs:21,23`)
  — null и из `UserReplicas`, и на write-ветках (`ToDto(note, null, null)` в create/update,
  `DialogReviewService.cs:96,185,327,359,370`). Фронт: `string | null` в обоих зеркалах
  (`use-dialog-reviews.ts:23,25`, `use-dialog-review-notes.ts:27,30`), охрана в
  `review-note-thread.ts:80,114` (`labelFor`) и в `dialog-reviews/page.tsx:101`.
- `LeagueParticipantDto.DisplayName` / `AdminLeagueMemberDto.DisplayName` (`string`, non-null)
  — берутся через INNER `Join` с `UserReplicas`
  (`LeagueService.cs:116`, `AdminLeaguesController.cs:515`): участник без реплики просто
  выпадает из выборки, null не возникает.
- `AdminVoiceUsageEntryDto.DisplayName` (`string`, инициализирован `""`,
  `src/backend/ai-service/Ai/Features/Voice/Models/AdminVoiceUsageEntryDto.cs:7`)
  — при отсутствии реплики остаётся `""`; фронт рендерит `entry.displayName || "—"`
  (`src/frontend/app/(admin)/admin/voice/usage/page.tsx:120`).
- `orgName` в `GET /auth/me` (`string?`, `AuthController.cs:64`) — во фронте вообще не читается
  (см. Q-1 в docs/NIGHT_AUDIT_QUESTIONS.md), падать нечему.
- `authenticatedUser.displayName` (`string` во фронте, `use-auth.ts:13`) — клейм
  `ClaimTypeNames.DisplayName` выставляется на всех путях выдачи токена
  (`AuthenticationService.cs:584`, `PlatformAdminService.cs:308`, `DemoTokenController.cs:46`),
  плюс охрана в `nav-rail.tsx:40`. Не находка.
- `MembershipDto.DisplayName` / `Email` (`string`, non-null,
  `src/backend/identity-service/Identity/Features/Membership/Models/MembershipDto.cs:23`) — читаются
  из `databaseContext.Users`, которыми identity владеет сам, а не из реплики
  (`MembershipsController.cs:71-84`). Фронт `displayName: string`
  (`org-people/types/organization-people.ts:7`, `org-team/types/organization-membership.ts:14`)
  — честно. Не находка.
- `DiscussThreadSummaryDto/DetailDto/ReplyDto.AuthorName`, `TopAuthorDto.AuthorName`
  (`string`, non-null) — при отсутствии реплики подставляется `""`
  (`DiscussService.Tags.cs:83,290,314`, `DiscussService.cs:329`). Во фронте всюду
  `authorName || "Аноним"`, кроме `src/frontend/app/(admin)/admin/discuss/page.tsx:104`,
  где отрендерится пустая ячейка. Крайне мелко, см. N-6.
- social-service `ChatService` / `FriendService` — все чтения `UserReplicas` уже сводят
  отсутствие реплики к `ChatConstants.UnknownDisplayName` / `UnknownDisplayName`
  (`ChatService.cs:170,245`, `FriendService.cs:102,395,486`). Не находки.

### [x] N-1 `TeamSkillMapMemberDto.IsActiveMember` — вся команда помечается как уволенная

- **Бэкенд:** `TeamSkillMapMemberDto.IsActiveMember`, `bool?`,
  `src/backend/learning-service/Learning/Features/TeamInsights/Models/TeamSkillMapDto.cs:86`.
  Null на ветке отказа identity-service: `TryReadRosterAsync` ловит исключение и возвращает
  `null` (`TeamSkillMapService.cs:275-289`), после чего
  `roster is null ? null : roster.Contains(userId)` (`TeamSkillMapService.cs:253`) даёт `null`
  **сразу для всех** участников. То есть отказ ростера — это не «у одного человека null»,
  а «null у всей команды».
- **Фронт:** `TeamSkillMapMember.isActiveMember: boolean` —
  `src/frontend/features/org-shell/hooks/use-team-directory.ts:51`; ложь протаскивается дальше
  без нормализации в `TeamMemberName.isActiveMember: boolean` (`:77`, присваивание `:116`).
  Разыменования:
  - `use-team-directory.ts:119-120` — сортировка `left.isActiveMember ? -1 : 1`: `null` уходит
    в ветку «уволен», все уезжают в конец списка (не падает, но порядок неверный);
  - `src/frontend/features/org-assignments/components/audience-picker.tsx:96` —
    `{!member.isActiveMember && <span>уже не работает в компании</span>}`: `!null === true`,
    поэтому подпись «уже не работает в компании» появляется у **каждого** человека в списке
    выбора аудитории задания.
  Хук возвращает `isRosterKnown` (`use-team-directory.ts:135`), но `audience-picker` его не
  читает — охрана есть, ей просто не пользуются. Ср. `org-team/utils/team-roster.ts:17,78-80`,
  где то же поле типизировано `boolean | null` и нормализуется через `rosterKnown` —
  правильный образец.
- **Следствие:** неверный рендер (РОП видит «уволены» напротив всей действующей команды и,
  скорее всего, не отправит задание никому)
- **Severity:** major

---

## Срез 2 — non-nullable по объявлению, null по факту (десериализация авторского JSON)

Второй источник того же класса баги: поле объявлено non-nullable в C#-записи, но приходит
не из БД-колонки, а из `JsonSerializer.Deserialize` над авторским JSON. `System.Text.Json`
по умолчанию **не проверяет** nullable-аннотации (`RespectNullableAnnotations` не включён,
`[JsonRequired]` нигде не стоит), поэтому отсутствующий ключ даёт `null` в non-nullable
свойстве, и оно уезжает на фронт как `null`.

### [x] N-2 `TechniqueDialogTurnDto.Annotations` — `.map` по null валит справочник техник

- **Бэкенд:** `TechniqueDialogTurnDto.Annotations`, объявлен non-nullable
  `TechniqueDialogAnnotationDto[]`,
  `src/backend/learning-service/Learning/Features/Techniques/Models/TechniqueDialogTurnDto.cs:7`.
  Значение приходит из `DeserializeDialogTurns(technique.DialogJson)`
  (`TechniqueService.cs:165,338-352`). Функция страхует только *весь массив* (`?? Array.Empty`)
  и `JsonException` — но не отдельный turn: реплика без ключа `annotations` (или с
  `"annotations": null`) даёт `Annotations == null`. Валидации на записи нет вообще:
  `AdminTechniqueWriteRequestDto.Dialog` — это `JsonNode?`
  (`Features/Admin/Models/AdminTechniqueWriteRequestDto.cs:15`), и контроллер кладёт его в
  колонку дословно: `technique.DialogJson = SerializeNullable(payload.Dialog)`
  (`AdminTechniquesController.cs:362`). На фронте админки это свободная textarea, которая
  проверяет только `JSON.parse` (`app/(admin)/admin/techniques/page.tsx:425-439` — тот же
  паттерн, что и для `challenges`).
- **Фронт:** `TechniqueDialogTurn.annotations: TechniqueDialogAnnotation[]` —
  `src/frontend/features/skills/hooks/use-techniques.ts:32`. Разыменование без охраны:
  `src/frontend/app/(main)/guidebook/page.tsx:484` —
  `const anno = turn.annotations.map((a) => a.label).join(" · ");`
  → `TypeError: Cannot read properties of null (reading 'map')` и падение всего экрана
  техники (не одной реплики — `.map` внутри рендера разбора диалога).
- **Следствие:** crash (учащийся; триггерится опечаткой автора контента, не действием
  пользователя — шаблон импорта `features/admin/lib/import-templates.ts:126-139` всегда
  пишет `annotations: []`, поэтому на seed-данных не воспроизводится)
- **Severity:** major

### [ ] N-3 `TechniqueCoachChallengeDto.Label` / `TechniqueDialogAnnotationDto.Label` — тот же путь, но без падения

- **Бэкенд:** `TechniqueCoachChallengeDto.Label` (`string`, non-nullable,
  `Features/Techniques/Models/TechniqueCoachChallengeDto.cs:4`) и
  `TechniqueDialogAnnotationDto.Label` (`string`,
  `Features/Techniques/Models/TechniqueDialogAnnotationDto.cs:4`) — оба из того же
  невалидируемого авторского JSON (`DeserializeChallenges`, `TechniqueService.cs:369-383`;
  запись — `AdminTechniqueCoachDto.Challenges` как `JsonNode?`,
  `Features/Admin/Models/AdminTechniqueCoachDto.cs:10`). Элемент без ключа `label` даёт `null`.
  Отдельно: элемент, равный литеральному `null` в массиве (`[null]`), даёт `null` **весь
  элемент** — и тогда `challenge.label` / `a.label` падает так же, как N-2.
- **Фронт:** `TechniqueCoachChallenge.label: string` (`use-techniques.ts:41`),
  `TechniqueDialogAnnotation.label: string` (`:24`). Рендер:
  `src/frontend/app/(main)/guidebook/page.tsx:600` (`{challenge.label}`) и `:484`
  (`a.label` внутри `.map`).
- **Следствие:** неверный рендер (React печатает `null` как пустоту — пустой пункт списка);
  crash только в подслучае «элемент массива = null»
- **Severity:** minor

### [ ] N-4 `authorName` в админской таблице обсуждений — пустая ячейка вместо «Аноним»

- **Бэкенд:** `DiscussThreadSummaryDto.AuthorName` (`string`, non-nullable,
  `src/backend/social-service/Social/Features/Discuss/Models/DiscussThreadSummaryDto.cs:8`),
  при отсутствии реплики в `UserReplicas` подставляется `""`
  (`DiscussService.Tags.cs:290,314`, `DiscussService.cs:329`) — то есть тут не null, а пустая
  строка. Формально типы честные.
- **Фронт:** `DiscussThread.authorName: string`
  (`src/frontend/features/discuss/hooks/use-discuss.ts:20`); все пользовательские экраны
  охраняют (`authorName || "Аноним"` в `discuss/page.tsx:182`,
  `discuss/[threadId]/page.tsx:149,206`, `thread-card.tsx:80`), а админский —
  `src/frontend/app/(admin)/admin/discuss/page.tsx:104` — рендерит `{thread.authorName}` как есть.
- **Следствие:** неверный рендер (пустая ячейка «Автор» у нового пользователя, пока Kafka
  `user.updated` не догнал)
- **Severity:** minor

---

## Срез 3 — общий проход по nullable-членам DTO против фронтовых зеркал

Метод: механическая выборка всех nullable-членов C#-записей/классов вне `Migrations`/`*.Tests`
(позиционные параметры `T? Name` и свойства `public T? Name { get; }`) и сопоставление по
camelCase-имени с полями фронтовых `interface`, объявленными **без** `| null`, `| undefined`
или `?`. 107 совпадающих имён, из них после отбрасывания входящих (`*RequestDto`,
`*WriteRequestDto`, `*Seed`) и коллизий имён между разными DTO — 36 реальных пар
«ответный DTO ↔ его фронтовое зеркало». Проверены попарно; кроме N-1 расхождений нет.

Nullable-коллекции проверены отдельным проходом (`IReadOnlyList<...>?` и т.п. в ответных DTO):
всего 10 объявлений, все либо внутренние (`OrganizationRoster`, `IdentityOrganizationMemberDirectory`,
события notification-service), либо уже с честным зеркалом (`CompanyReadinessDto`,
`ExtractedProfileDraftDto`). Ни одного `.map`/`.length` по потенциальному null из этого класса.

Также проверено и чисто:

- `AssignmentFunnelDto.LeftOrganizationCount` / `AssignedActiveCount` (`int?`) → фронт
  `number | null` (`org-assignments/types/assignment.ts:104-105`) и `?? 0` /
  `?? funnel.assignedCount - leftCount` (`components/assignment-funnel.tsx:73,76`).
- `TeamSkillMapStageDto` / `SkillDto` / `CellDto.AccuracyPercent` (`int?`) → всюду
  `accuracyPercent: number | null` (`use-team-directory.ts:19,29,39`).
- `TeamSkillMapMemberDto.WeakestStageKey`, `WeakestSkillId`, `AccuracyPercent`,
  `DialogAverageScore` — все с честными зеркалами; `team-roster.ts:17,78-90` —
  образцовая нормализация через `rosterKnown`.
- `ProgramItemDto.LessonTitle` / `LessonVersionNumber`, `ProgramDiffLessonDto.*` (`string?`,
  `int?`) → `program.ts:28,29,47,48` честно `| null`.
- `ProgramEnrollmentDto` — все поля non-nullable кроме `PreviousProgramVersionId`/`SwitchedAt`,
  зеркало совпадает (`program.ts:90-97`). `MyProgramDto` (много nullable) фронтом вообще
  не читается.
- `AdminDialogTranscriptDto` / `AdminDialogSessionSummaryDto` (`ModeKey`, `ModeTitle`, `Score`,
  `Feedback`, `AssignmentId`, `CompletedAt` — все `?`) → `use-dialog-transcript.ts:26-39`
  честно `| null`; `.localeCompare` над `modeTitle` в `org/dialogs/page.tsx:90` стоит **за**
  охраной `if (session.modeTitle)` на `:85`.
- `CompanyReadinessDto` (все поля nullable) → `use-company-readiness.ts:4-10` честно, плюс
  нормализация 204 → `EMPTY_READINESS`.
- `AiQuotaSettingsDto.UpdatedAt` (`DateTime?`) → `use-organization-quota.ts:32` `string | null`.
- `OrganizationProfileDto` — коллекции non-nullable на бэкенде, `organization-profile.ts:16-25`
  совпадает; `ExtractedProfileDraftDto` (всё nullable) → `ExtractedProfileDraft` с `?` на
  каждом поле.
- `ValidateScenarioResponseDto.RejectionReason` (`string?`) → `use-custom-scenario.ts:15`
  `| null` + `?? "…"` в `custom-scenario-modal.tsx:62`.
- `DemoRequestDto` — 8 nullable полей, зеркало `use-demo-requests.ts:22-42` совпадает
  поле в поле. `DemoRequestProvisioningResultDto.InviteExpiresAt` — C-6 уже исправлен,
  `ProvisionedDetails.inviteExpiresAt: string | null`
  (`app/(admin)/admin/demo-requests/page.tsx:162-170`).
- `ContentOverrideDto.Title` — non-nullable и заполняется из non-nullable колонок сущностей
  (`ContentOverrideService.cs:349,577,758`), поэтому `.localeCompare` в
  `override-rows.ts:96` безопасен. То же для `lessonTitle` в
  `org-content-adaptation/utils/proposal-queue.ts:54,249` (`ContentAdaptationItemSummaryDto.LessonTitle`
  — `string`, дефолт `""`).
- `TeamSkillGapsDto` — `AccuracyPercent` там `int` (не `int?`), зеркало
  `use-team-skill-gaps.ts:12,22,34` верное.
- `DialogSessionDto` / `DialogBundleDto` / `DialogModeDto` (`= null!` на строковых полях):
  на записи защищены неявным `[Required]` ASP.NET Core для non-nullable ссылочных свойств
  (`CreateBundleRequestDto`), на чтении — из Mongo-документов, которые пишет только этот же
  код. `DialogFeedback` фронт получает как `feedback: DialogFeedbackDto | null` и охраняет;
  `xpEarned` DTO не несёт вовсе, фронт подставляет его из `session.xpEarned`
  (`app/dialog/[bundleId]/[modeId]/page.tsx:250`) — задокументировано, не находка.

---

## Итог

| id | заголовок | следствие | severity |
|----|-----------|-----------|----------|
| N-1 | `TeamSkillMapMemberDto.IsActiveMember` — вся команда помечается как уволенная | неверный рендер | major |
| N-2 | `TechniqueDialogTurnDto.Annotations` — `.map` по null валит справочник техник | crash | major |
| N-3 | `TechniqueCoachChallengeDto.Label` / `TechniqueDialogAnnotationDto.Label` | неверный рендер | minor |
| N-4 | `authorName` в админской таблице обсуждений | неверный рендер | minor |

**Главный вывод.** Класс C-4 в проекте почти вычищен: nullable-поля ответных DTO
практически везде имеют честные `| null` в зеркалах, и в нескольких местах (`team-roster.ts`,
`use-company-readiness.ts`, `assignment-funnel.tsx`, `remind-dialog.tsx`) нормализация
сделана образцово — на границе, один раз. Единственное оставшееся расхождение
«nullable на бэке ↔ non-null на фронте» — N-1, и оно в том же файле, который правил `d854e4c`:
`isActiveMember` — сосед `displayName`, которого фикс не тронул.

Второй, более опасный резерв — не nullable-объявления, а **non-nullable объявления, которые
`System.Text.Json` не обеспечивает** (N-2, N-3). Там, где DTO собирается не из БД-колонок, а
десериализацией авторского JSON (`Technique.DialogJson`, `Coach.ChallengesJson`; на записи —
`JsonNode?` без валидации), тип на бэкенде не гарантирует ничего, и фронт наследует ложь.
Системное лечение — `JsonSerializerOptions.RespectNullableAnnotations = true` (или
`[JsonRequired]`) на этих путях плюс валидация на записи, а не охрана на фронте.

### Охват

- Проверено попарно (бэкенд + фронт, оба конца): **~60 полей DTO** — из них 36 пар
  «nullable на бэке ↔ non-null на фронте» из механической выборки, плюс полные разборы
  `TeamSkillMapDto` (11 полей), `AssignmentDashboardDto`/`AssignmentFunnelDto`/`AssignmentSummaryDto`,
  `DialogReviewNoteDto`, `DemoRequestDto` (19 полей), `AdminDialogTranscriptDto` (13),
  `MembershipDto`, `ProgramItemDto`/`ProgramEnrollmentDto`, `OrganizationProfileDto`,
  `TechniqueDetailDto` и его вложенные.
- Механическая выборка покрыла все C#-DTO вне `Migrations`/`*.Tests` (позиционные параметры
  и свойства) против всех фронтовых `interface` вне `__tests__`.
- **НЕ дошло:**
  - `type X = {...}` и inline-типы во фронте (выборка ловила только `interface`) — они могли
    остаться незамеченными, если поле не встречается ни в одном `interface`;
  - «поле есть во фронте, но его нет в DTO вообще» (то есть всегда `undefined`) — обратный
    проход дал слишком много шума от пропсов компонентов и CSS-объектов и был прекращён;
    вероятность находки там низкая, но не нулевая;
  - Mongo-документы ai-service (`DialogSession`, `DialogMessage`, `DialogBundle` с `= null!`):
    проверены пути записи (защищены неявным `[Required]`), но не проверялось, нет ли в
    продовой базе старых документов без этих ключей — это вопрос к данным, не к коду;
  - события Kafka (`NotificationIntegrationEvents`, `OutgoingIntegrationEvents`) как
    отдельный контракт «сервис → сервис» — смотрелись только там, где они пересекались
    с проверяемым DTO.
