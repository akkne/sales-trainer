# Аудит контрактов фронт ↔ бэк (статический)

Статический разбор кода без запуска приложения. Дополняет живой браузерный аудит.

**Охват:** 316 точек вызова API во фронте (314 `apiClient.*` вне тестов + 2 прямых
`fetch()` на стриминг голоса). Сверено с 329 маршрутами контроллеров в
`src/backend/*-service` и с YARP-таблицей `src/backend/gateway/Gateway/appsettings.json`.

**Исключено по договорённости:** `GET /skills/progress-summary`, `GET /readiness`.

**Проверено и расхождений нет:**
- Шлюз покрывает все префиксы, которые зовёт фронт; ни один префикс не уводит в чужой кластер.
  Ни одного «есть контроллер, но нет маршрута в шлюзе».
- Ни одного вызова с несуществующим путём или неверным глаголом (PUT вместо PATCH и т.п.).
- Имена query-параметров совпадают с `[FromQuery]` во всех 20 фильтрующих вызовах.
- Дублирующихся шаблонов маршрутов (риск `AmbiguousMatchException` → 500) нет.

---

### [x] C-1 GET /admin/skills/{guid}/topics — GUID уходит в сегмент {skillIconicName}
- **Фронт вызывает:** GET `/admin/skills/${skillIconicName}/topics` — src/frontend/features/admin/hooks/use-admin.ts:158, вызывается из src/frontend/app/(admin)/admin/skills/[id]/topics/[topicId]/page.tsx:56 с `id` = **GUID навыка**, не iconicName.
- **Бэкенд:** `[HttpGet("admin/skills/{skillIconicName}/topics")]` — src/backend/learning-service/Learning/Features/Admin/AdminTopicsController.cs:28; поиск идёт по `IconicName == skillIconicName`, иначе `NotFound` (там же, :31-32). Маршрута с `{skillId:guid}` нет ни в одном сервисе. Шлюз: `/admin/skills/{**catch-all}` → learning, корректно.
- Ссылки на страницу дают именно GUID: src/frontend/app/(admin)/admin/skills/page.tsx:203 (`skill.id`) и src/frontend/app/(admin)/admin/topics/page.tsx:339 (`topic.skillId`). Соседняя страница делает правильно: src/frontend/app/(admin)/admin/skills/[id]/page.tsx:32 передаёт `skill?.iconicName`.
- **Следствие:** 404, `topics` остаётся `[]`, `topic` = undefined → страница вечно висит на `Loading topic...` (page.tsx:117). Форма темы и весь список уроков не рендерятся никогда.
- **Severity:** blocker

### [x] C-2 GET /skills/{id}/reference получает id справочного материала вместо id навыка
- **Фронт вызывает:** GET `/skills/${skillSlug}/reference` — src/frontend/features/skills/hooks/use-reference.ts:18, аргумент из src/frontend/app/(main)/reference/[id]/page.tsx:15.
- **Бэкенд:** `[HttpGet("skills/{skillId:guid}/reference")]` — src/backend/learning-service/Learning/Features/Reference/ReferenceController.cs:12; фильтр `material.SkillId == skillId` — .../Reference/Services/Implementation/ReferenceService.cs:18. Шлюз: `/skills/{**catch-all}` → learning, корректно.
- Единственный вход на маршрут — src/frontend/features/assignments/components/active-assignment-card.tsx:128 (`/reference/${item.reference}`), а `item.reference` для `reference_material` — это id материала: src/backend/learning-service/Learning/Features/Assignments/Models/ActiveAssignmentDto.cs:49 («a lesson-version id, a reference-material id, or a dialog mode key»).
- Маршрута `GET /reference/{materialId}` не существует. Поле `skillSlug` бэкенд не отдаёт нигде: `ReferenceMaterialDto` несёт `SkillId` — .../Reference/Models/ReferenceMaterialDto.cs:10.
- **Следствие:** GUID материала проходит ограничение `:guid`, ответ 200 с пустым массивом. Пункт «Теория» в карточке активного задания всегда открывает страницу «Материалы пока не добавлены». Ошибки нет — восстановиться из UI нельзя.
- **Severity:** major

### [x] C-3 DialogBundleDto не содержит skillSlug, а skillTitle всегда пустая строка

**Исправлено:** `GET internal/skills/lookup` в learning-service (новый `InternalSkillsController`,
защищён `InternalServiceAuthFilter`, без `[TenantScoped]` — навыки сейчас всегда глобальны) отдаёт
`{id, iconicName, title}` по всем навыкам. В ai-service новый `SkillLookupClient` читает этот список
один раз на запрос и `DialogBundleDto.FromEntity` заполняет `SkillSlug`/`SkillTitle` по словарю
(деградирует к `""` при недоступности learning-service или отсутствии id — список бандлов не падает).
Использовано во всех точках, отдающих `DialogBundleDto`: `GET /dialog/bundles`,
`GET|POST|PUT /admin/dialog/bundles[/:id]`. Документация (`docs/API_CONTRACTS.md`) обновлена и
больше не утверждает, что `skillTitle` намеренно пустой.
- **Фронт вызывает:** GET `/dialog/bundles` — src/frontend/features/dialog/hooks/use-dialog.ts:70; GET `/admin/dialog/bundles` — src/frontend/features/dialog/hooks/use-admin-dialog.ts:81. Типы объявляют `skillSlug` и `skillTitle`: use-dialog.ts:7-8, use-admin-dialog.ts:7-8.
- **Бэкенд:** `DialogBundleDto` — src/backend/ai-service/Ai/Features/Dialog/Models/DialogBundleDto.cs:3-13: поля `SkillSlug` нет вовсе, а `FromEntity` жёстко пишет `SkillTitle = ""` (там же, :19). Других присваиваний `SkillTitle` в ai-service нет. Тот же маппер используется и админским контроллером — .../Dialog/AdminDialogController.cs:70,85,106,138. Шлюз: `/dialog/{**catch-all}` и `/admin/dialog/{**catch-all}` → ai, корректно.
- Оба поля рендерятся: src/frontend/app/(admin)/admin/dialog/page.tsx:350-351 и src/frontend/app/(admin)/admin/dialog/[bundleId]/page.tsx:144.
- **Следствие:** в админском списке бандлов колонка навыка показывает пустоту и голые скобки `()`; на странице режимов — `Skill:  ()`. Администратор не видит, к какому навыку привязан бандл. На пользовательском экране (app/(main)/dialog/page.tsx:231) пилюля навыка просто не рисуется — пустая строка ложна.
- Побочно: docs/API_CONTRACTS.md:1637 документирует `skillSlug`/`skillTitle` как существующие — фронт написан по документации, а не по коду; документацию тоже надо править.
- **Severity:** major

### [x] C-4 TeamSkillMapMemberDto.DisplayName nullable, фронт объявляет его обязательным и вызывает localeCompare
- **Фронт вызывает:** GET `/admin/team/skill-map` — src/frontend/features/org-shell/hooks/use-team-directory.ts:80. Тип: `displayName: string` и `isActiveMember: boolean` — там же, :36-37. Сортировка: `left.displayName.localeCompare(right.displayName, "ru")` — :107.
- **Бэкенд:** `TeamSkillMapMemberDto(Guid UserId, string? DisplayName, bool? IsActiveMember, ...)` — src/backend/learning-service/Learning/Features/TeamInsights/Models/TeamSkillMapDto.cs:85-87. Значение берётся как `displayNames.GetValueOrDefault(userId)` — .../TeamInsights/Services/Implementation/TeamSkillMapService.cs:176, то есть `null` для любого участника без строки в `UserReplicas` (отставание репликации). Сам бэкенд от null защищается: `.ThenBy(member => member.DisplayName ?? string.Empty, ...)` — там же, :182. Шлюз: `/admin/team/{**catch-all}` → learning, корректно.
- **Следствие:** `TypeError: Cannot read properties of null` в `useMemo` при рендере → падает фильтр по сотрудникам на /org/dialogs (src/frontend/app/(org)/org/dialogs/page.tsx:70,119) и любой другой потребитель общего каталога команды. Не «имя без подписи», а пустой/сломанный экран.
- Соседний потребитель того же ответа сделан правильно: src/frontend/features/org-team/utils/team-roster.ts:16,87 (`boolean | null`, `member.displayName || UNNAMED_MEMBER_LABEL`).
- **Severity:** major

### [x] C-5 GET /skills/{skillSlug}/lessons отвечает 404 на навык без уроков
- **Фронт вызывает:** GET `/skills/${skillSlug}/lessons` — src/frontend/features/exercise/hooks/use-lesson.ts:42. Ветки на 404 нет, ошибка уходит в общий error-state.
- **Бэкенд:** src/backend/learning-service/Learning/Features/SkillTree/Endpoints/SkillsController.cs:74-77 — `if (lessons.Count == 0) return NotFound(...)`. Путь и глагол совпадают, шлюз корректен; расходится семантика ответа.
- **Следствие:** навык, у которого пока нет уроков, показывает «не удалось загрузить» вместо пустого списка — на src/frontend/app/(main)/tree/page.tsx и app/(main)/skill/[id]/page.tsx.
- **Severity:** minor

### [x] C-6 ProvisionDemoRequestResult.inviteExpiresAt объявлен обязательным, бэкенд возвращает null на повторном провижининге

**Исправлено:** бэкенд и docs/API_CONTRACTS.md были правы уже до фикса (`InviteExpiresAt`
документирован как `null` на ветке `alreadyProvisioned` — это намеренное поведение, не баг). Баг был
только в типах фронта: `ProvisionDemoRequestResult.inviteExpiresAt` и
`ProvisionedDetails.inviteExpiresAt` (features/admin/hooks/use-demo-requests.ts,
app/(admin)/admin/demo-requests/page.tsx) стали `string | null`, а условие рендера в page.tsx
проверяет теперь `cachedDetails?.inviteExpiresAt` (а не просто `cachedDetails`), так что `null`
показывает предусмотренный текст «unknown (not provisioned this session)» вместо
`new Date(null)` → 01.01.1970.
- **Фронт вызывает:** POST `/admin/demo-requests/${id}/provision` — src/frontend/features/admin/hooks/use-demo-requests.ts:123; тип `inviteExpiresAt: string` — там же, :65. Рендер: `new Date(cachedDetails.inviteExpiresAt).toLocaleString()` — src/frontend/app/(admin)/admin/demo-requests/page.tsx:357.
- **Бэкенд:** `DateTime? InviteExpiresAt` — src/backend/organization-service/Organization/Features/DemoRequests/Models/DemoRequestProvisioningResultDto.cs:25; на ветке «уже провижинили» отдаётся `InviteExpiresAt: null, AlreadyProvisioned: true` — .../Services/Implementation/DemoRequestProvisioningService.cs:276. Шлюз: `/admin/demo-requests/{**catch-all}` → organization, корректно.
- **Следствие:** повторное нажатие «Provision» (двойной клик, ретрай) печатает дату из эпохи (01.01.1970) вместо предусмотренного кодом текста «unknown (not provisioned this session)».
- **Severity:** minor

---

## Не вошло в список (проверено, сейчас безвредно)

Поля объявлены в TS-типах, но бэкенд их не отдаёт **и** UI их не читает — визуального
эффекта нет, но значение всегда `undefined`:

- `ExerciseData.sortOrder` — src/frontend/features/exercise/hooks/use-lesson.ts:19; бэкенд отдаёт `OrderInLesson` (.../Exercises/Models/ExerciseDto.cs).
- `ReferenceMaterial.skillSlug` / `ReferenceMaterialChoice.skillSlug` — use-reference.ts:11, features/org-assignments/hooks/use-assignment-content-sources.ts:52; бэкенд отдаёт `SkillId`.
- `DialogFeedback.xpEarned` — features/dialog/hooks/use-dialog.ts:39; `DialogFeedbackDto` его не несёт, и модалка сознательно его не показывает (features/dialog/components/feedback-modal.tsx:89).

Смежный дефект, не контрактный: src/frontend/app/(admin)/admin/skills/[id]/topics/[topicId]/page.tsx:341
ссылался на `/admin/skills/{skillId}/topics/{topicId}/lessons/{lessonId}`, а страницы по
этому пути нет — только `.../lessons/[lessonId]/exercises/page.tsx`; клик уходил в `notFound()`.
Был замаскирован C-1. **[x] Исправлено в том же коммите, что и C-1** — ссылка на заголовок урока
теперь ведёт на `.../lessons/{lessonId}/exercises`, тот же путь, что уже был у соседней ссылки
"Exercises →".
