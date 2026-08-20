# Аудит: ошибка запроса, показанная как «нет данных»

Класс дефекта из `docs/AUDIT_PROD.md` (O-1, «Прогон 2»): запрос падает (500/404), а экран
рисует спокойный пустой стейт («Никто не зачислен», «Материалы пока не добавлены», `0`, `—`).
Пользователь делает ложный вывод: «данных нет», хотя данные просто не загрузились.

Область прогона: весь `src/frontend`, **кроме** `app/(org)/**` и `features/org-*/**`
(они проверены отдельно). Проверялись: `app/(main)/**`, `app/session/**`, `app/dialog/**`,
`app/companies/**`, `app/(admin)/**`, `app/(auth)/**` и их `features/**`.

Контекст, общий для всех находок: `app/providers.tsx` задаёт `retry: 1` — то есть запрос
доходит до `isError` быстро, и пустой стейт показывается почти сразу после отказа.
Общего хелпера для «загрузка / ошибка / пусто» в проекте нет (`shared/components/error-state.tsx`
есть, но каждый экран подключает его вручную), поэтому каждая находка правится точечно.

Режим: read-only, код не менялся.

---

### [ ] E-1 `/reference/<id>`: 404/500 справочника выглядит как «материалов нет»
- **Экран:** `/reference/<materialId>` — `app/(main)/reference/[id]/page.tsx:26-72`
- **Запрос:** `useHandbook()` → `GET /reference` и `useReferenceMaterials(skillId)` →
  `GET /skills/{skillId}/reference` (`features/skills/hooks/use-reference.ts:16-37`).
  Оба `isError` не читаются вообще: страница берёт только `data` + `isLoading`.
  Хуже: если падает первый запрос, `skillId` остаётся `undefined`, второй запрос
  выключен (`enabled: !!skillId`), `isLoading` становится `false` — и экран мгновенно
  уходит в пустой стейт, не сделав ни одного запроса за материалами.
- **Что видит пользователь:** блок «Материалы пока не добавлены». Вывод «по этому навыку
  теории нет» — ложный: это ровно тот случай, который в проде подтверждён как A-4
  (`GET /skills/first-contact/reference` → 404 из-за slug вместо GUID, а экран молчал).
- **Severity:** major

### [ ] E-2 `/profile`: падение `/skills/progress-summary` даёт «Точность —» и «Навыки 0»
- **Экран:** `/profile` — плитки статистики, `app/(main)/profile/page.tsx:38`, `175-231`
- **Запрос:** `useProgressSummary()` → `GET /skills/progress-summary`
  (`features/skills/hooks/use-progress-summary.ts:22`). `isError`/`error` не читаются:
  страница деструктурирует только `data`. При ошибке `progressSummary === undefined`, и код
  идёт по тем же ветвям, что и «прогресса ещё нет»: `progressSummary?.averageExerciseScore == null`
  → «—» (строка 187-188), `progressSummary?.completedSkillCount ?? 0` → «0» без знаменателя
  (строка 210-213).
- **Что видит пользователь:** «ТОЧНОСТЬ —», «НАВЫКИ 0». Это ровно то, что зафиксировано в проде
  как A-2 (там причиной был бэкенд, но фронт и при 404/500 этого эндпоинта нарисует то же самое,
  а `GET /skills/progress-summary` → 404 — известный прод-дефект). Ученик делает вывод
  «я ничего не освоил», хотя прогресс есть и виден на `/tree`.
- **Severity:** major

### [ ] E-3 `/profile`: падение `/skills` → «Пока нет навыков. Обратись к администратору», а `/profile` → пустой экран
- **Экран:** `/profile` — `app/(main)/profile/page.tsx:35`, `66`, `102`, `251-260`
- **Запрос:** `useSkills()` → `GET /skills` (`features/skills/hooks/use-skill-tree.ts:51`).
  `isError` не читается; везде подставляется `(allSkills ?? [])`. Отдельно: `useProfile()` →
  `GET /profile`; при ошибке `profileLoading` уже `false`, `profileStats` — `undefined`,
  и строка 66 `if (!profileStats) return null;` рисует **полностью пустую страницу** без
  сообщения и без кнопки «Повторить».
- **Что видит пользователь:** (а) карточка «Изучаемые навыки» → «Пока нет навыков. Обратись
  к администратору» — прямое указание идти к админу из-за сетевой ошибки; (б) плитка «УРОКИ 0»
  (`totalLessonsDone` считается из пустого массива); (в) при падении `/profile` — белый экран,
  который читается как «страница сломалась/пустая», без возможности повторить.
- **Severity:** major

### [ ] E-4 `/tree`: падение `/skills/<slug>/lessons` обнуляет всю статистику навыка и советует «попросить администратора»
- **Экран:** `/tree`, центральная колонка — `app/(main)/tree/page.tsx:385`, `387-399`,
  `477-507`, `511-520`
- **Запрос:** `useLessonsForSkill(skillSlug)` → `GET /skills/{slug}/lessons`
  (`features/exercise/hooks/use-lesson.ts:39-44`). Компонент `PathCenterColumn` берёт только
  `data` и `isLoading`; `isError` не проверяется. При ошибке `lessons === undefined` →
  `sorted = []` → `totalCount = 0`, `completedCount = 0`, `progressPct = 0`, `avgAccuracy = null`,
  `remainingCount = 0`. Корневая страница ловит только ошибку `useSkillTree()`
  (строки 717, 730-739 — там `ErrorState` есть), а это другой эндпоинт, поэтому страница
  считается «загруженной».
- **Что видит пользователь:** шапка навыка «Уроки 0 / Завершено 0 / Точность — / Осталось 0»,
  прогресс-бар 0 %, и вместо таймлайна — «Уроки пока не добавлены. Попроси администратора
  добавить уроки». Ученик, у которого 7 из 21 урока пройдено, видит обнулённый прогресс и
  ложное утверждение, что контента нет. Это самая заметная точка входа в приложение.
- **Severity:** major

### [ ] E-5 `/tree`: падение `/skills` → «Нет активных навыков» в списке слева
- **Экран:** `/tree`, левая колонка и мобильный пикер — `app/(main)/tree/page.tsx:151`,
  `182-210`, `238-240`
- **Запрос:** `useSkills()` → `GET /skills`. `isError` не читается; `(allSkills ?? [])`.
  Обратить внимание: корневой `ErrorState` на строке 730 срабатывает только на `/skill-tree`,
  так что при живом `/skill-tree` и мёртвом `/skills` страница рисуется целиком.
- **Что видит пользователь:** «Нет активных навыков. Добавить в профиле» — предложение пойти
  и записаться на навыки, хотя записи уже есть. Блок общего прогресса «Освоено навыков»
  (`PathOverallProgress`) при этом просто исчезает (`return null`), то есть исчезновение
  прогресса тоже не объясняется.
- **Severity:** major

### [ ] E-6 `/skill/<slug>`: та же обнулённая шапка + slug вместо названия навыка
- **Экран:** `/skill/<slug>` — `app/(main)/skill/[id]/page.tsx:74-101`, `170-198`
- **Запрос:** `useLessonsForSkill(slug)` → `GET /skills/{slug}/lessons` и `useSkills()` →
  `GET /skills`. Ни один `isError` не читается; экран знает только «спиннер» и «данные».
  Ветки ошибки нет вообще: `if (isLoading) …` и сразу рендер.
- **Что видит пользователь:** «Уроки 0 / Завершено 0 / Точность — / Осталось 0», прогресс 0 %,
  «Уроки пока не добавлены. Попроси администратора добавить уроки». Плюс при падении `/skills`
  заголовком становится сам slug (`skill?.title ?? skillSlug`, строка 103) и статус
  «Доступен» — то есть освоенный навык выглядит как нетронутый. Тот же экран уже фигурировал
  в проде (A-3), причина другая, но симптом идентичен.
- **Severity:** major

### [ ] E-7 `/skill/<slug>/map`: кольцо прогресса рисует 0 % при ошибке
- **Экран:** `/skill/<slug>/map` — `app/(main)/skill/[id]/map/page.tsx:74-97`, `132-159`
- **Запрос:** те же `useLessonsForSkill` + `useSkills`, `isError` не проверяется.
- **Что видит пользователь:** круговой индикатор «0 %» и подпись «0 из 0 уроков завершено»,
  заголовок — slug. Ложный вывод: «прогресс сброшен». Ниже — пустой «Путь обучения».
- **Severity:** major

### [ ] E-8 `/guidebook`: отказ `/techniques` показывается как «Ничего не найдено» — вина перекладывается на запрос пользователя
- **Экран:** `/guidebook` — `app/(main)/guidebook/page.tsx:133-134`, `173-176`, `254-259`
- **Запрос:** `useTechniques({...})` → `GET /techniques` и `useTechniquesMeta()` →
  `GET /techniques/meta` (`features/skills/hooks/use-techniques.ts:79-105`). Оба `isError`
  игнорируются; более того, `const { data: cards = [], isLoading } = useTechniques(...)`
  подставляет пустой массив прямо в деструктуризации, так что «упало» и «пусто» становятся
  одним и тем же значением.
- **Что видит пользователь:** подзаголовок «0 техник · освоено 0» (при падении `meta`),
  строка фильтров без навыков и на месте сетки — «Ничего не найдено. Попробуй другой запрос
  или навык». Текст прямо утверждает, что дело в поисковом запросе, хотя запрос вообще не
  дошёл до сервера. Ложный вывод: «в справочнике нет техник по моему навыку».
- **Severity:** major

### [ ] E-9 `/companies/<id>`: история звонков и контакты исчезают молча
- **Экран:** `/companies/<id>` — `app/(main)/companies/[id]/page.tsx:72`, `77`, `80`,
  `300-327`; пустые стейты в `features/companies/components/company-timeline.tsx:113-115`
  и `features/companies/components/company-contacts-card.tsx:106-107`
- **Запрос:** `useCompanyLogs` → `GET /companies/{id}/logs`, `useCompanyPracticeCalls` →
  `GET /companies/{id}/practice-calls`, `useCompanyContacts` → `GET /companies/{id}/contacts`.
  Все три деструктурируются как `{ data }`, без `isError`, и передаются вниз как
  `logs ?? []`, `practiceCalls ?? []`, `contacts ?? []`. Сама компания (`useCompany`) обработана
  корректно — `error` → отдельный экран (строки 119-146), и `readiness`/`briefing` тоже
  прокидывают `errorMessage`, поэтому падение именно этих трёх списков остаётся невидимым.
- **Что видит пользователь:** «Здесь появятся ваши тренировки и записи о реальных звонках» и
  «Пока нет контактов — добавьте, с кем вы общаетесь в этой компании». Это ручные данные CRM:
  вывод «мои записи о звонках пропали / я их не заводил» ложный и провоцирует завести их
  заново (дубли).
- **Severity:** major

### [ ] E-10 `/friends` и `/friends/<userId>`: ошибка выдаётся за «нет друзей» и «пользователь не найден»
- **Экран:** `/friends` — `app/(main)/friends/page.tsx:32-38`, `86`, `127-153`;
  `/friends/<userId>` — `app/(main)/friends/[userId]/page.tsx:21`, `50-61`
- **Запрос:** `useFriends()` → `GET /friends`, `useFriendRequests()` → `GET /friends/requests`,
  `useConversations()` → `GET /chat/conversations`, `usePublicProfile(id)` →
  `GET /friends/profile/{id}` (`features/friends/hooks/use-friends.ts:52-88`). Ни один `isError`
  не читается.
- **Что видит пользователь:** (а) «Найди своего первого напарника! Используй поиск выше…»,
  хотя друзья есть; (б) секция «Заявки» скрывается целиком (`incomingRequests.length > 0`),
  так что входящая заявка становится невидимой без объяснения; (в) на профиле — «Пользователь
  не найден», то есть утверждение о несуществовании человека при обычной 500.
- **Severity:** minor (социальная зона: неверный вывод не влияет на обучение и цифры прогресса,
  но текст «Пользователь не найден» — прямая ложь о состоянии данных)

### [ ] E-11 `/session/<lessonId>`: при ошибке спиннер крутится вечно
- **Экран:** `/session/<lessonId>` — `app/session/[lessonId]/page.tsx:573-578` и `164-170`
- **Запрос:** `useExercisesForLesson(lessonId)` → `GET /lessons/{id}/exercises`
  (`features/exercise/hooks/use-lesson.ts:47-51`). `isError` не читается ни в `SessionRouter`,
  ни в `SessionFlow`. Условия ровно такие: `if (isLoading || !exercises) return <SessionLoader />;`
  и `if (isLoading || exerciseQueue.length === 0) return <спиннер>;`. При ошибке
  `isLoading === false`, `exercises === undefined` — оба условия остаются истинными **навсегда**.
- **Что видит пользователь:** бесконечный крутящийся спиннер на весь экран, без текста, без
  «Повторить» и без выхода (крестик «Выйти» находится внутри непоказанного плеера). Урок
  выглядит «зависшим»; вывод «приложение сломалось / урок не открывается» — и пути дальше нет.
  То же самое произойдёт, если бэкенд легально вернёт пустой список упражнений.
- **Severity:** blocker

### [ ] E-12 `/tree`: активное задание с дедлайном при ошибке просто не показывается
- **Экран:** `/tree`, полоса задания — `features/assignments/components/active-assignment-card.tsx:23-27`
- **Запрос:** `useActiveAssignments()` → `GET /assignments/active`
  (`features/assignments/hooks/use-assignments.ts:49-59`). Компонент берёт только `data` и
  делает `if (!assignments || assignments.length === 0) return null;` — то есть «ошибка» и
  «заданий нет» дают один и тот же результат. В комментарии (строки 13-21) это названо
  осознанным решением, но обосновано оно тем, что задание не должно **заменять** домашний
  экран, а не тем, что об ошибке можно молчать.
- **Что видит пользователь:** обычное дерево навыков без полосы задания. Ложный вывод: «задания
  от руководителя нет» — а оно есть, и у него дедлайн. Здесь достаточно узкой строки-уведомления
  вместо полосы, домашний экран это не ломает.
- **Severity:** major

### [x] E-13 `/admin/users`: при ошибке — пустая таблица без единого слова
- **Экран:** `/admin/users` — `app/(admin)/admin/users/page.tsx:17`, `41-44`, `68`
- **Запрос:** `useAdminUsers()` → `GET /admin/users` (`features/admin/hooks/use-admin.ts:856-861`).
  `const { data: users = [], isLoading } = useAdminUsers();` — дефолт `[]` прямо в
  деструктуризации, `isError` не существует в компоненте. Ветка после `isLoading` — сразу
  таблица.
- **Что видит пользователь:** шапка таблицы (Email / Display name / Provider / Role /
  Registered) и **ноль строк**, без сообщения и без «Повторить». Вывод «в системе нет
  пользователей» для платформенного админа абсурден, но экран не даёт ничего другого.
- **Severity:** major

### [x] E-14 Вся контентная админка: `data = []` в деструктуризации превращает 500 в «No … found»
- **Экран:** системный шаблон, `isError` не проверяется ни на одном из этих экранов:
  - `/admin/skills` — `app/(admin)/admin/skills/page.tsx:28`, `168-171` → «No skills yet.»
  - `/admin/topics` — `app/(admin)/admin/topics/page.tsx:20-21`, `255-258` → «No topics found.»
  - `/admin/lessons` — `app/(admin)/admin/lessons/page.tsx:23-25`, `221-224` → «No lessons found.»
  - `/admin/reference` — `app/(admin)/admin/reference/page.tsx:38-44`, `144-147` → «No reference materials found.»
  - `/admin/techniques` — `app/(admin)/admin/techniques/page.tsx:58-62`, `235-238` → «No techniques found.»
  - `/admin/prompts` — `app/(admin)/admin/prompts/page.tsx:13-15` (после `isLoading` просто пустой список)
  - `/admin/skill-stages` — `app/(admin)/admin/skill-stages/page.tsx:23`, `122`
  - `/admin/quotes` — `app/(admin)/admin/quotes/page.tsx:65`, `188`
- **Запрос:** `useAdminSkills`, `useAdminAllTopics`, `useAdminAllLessons`,
  `useAdminReferenceAll`/`useAdminReferenceCategories`, `useAdminTechniques`,
  `useExerciseTypePrompts`, `useAdminSkillStages`, `useAdminDailyQuotes` — все обычные
  `useQuery` в `features/admin/hooks/use-admin.ts`. Везде один и тот же приём:
  `const { data: X = [], isLoading } = useAdminY()`, после чего `X.length === 0` служит
  и признаком «пусто», и признаком «упало». Ошибки **мутаций** на этих же экранах
  показываются (`createSkill.isError` и т.п.) — то есть отсутствие обработки чтения
  выглядит как недосмотр, а не как решение.
- **Что видит пользователь:** «No skills yet» / «No lessons found» / «No reference materials
  found» на экранах, откуда контент импортируют и создают. Опасное действие рядом:
  админ может решить, что контент пропал, и запустить импорт заново → дубли. Единственный
  косвенный намёк — кнопки экспорта, которые самоблокируются при `length === 0`.
- **Severity:** major

### [ ] E-15 `/admin/organizations`: пустая таблица организаций при отказе `/organizations`
- **Экран:** `/admin/organizations` — `app/(admin)/admin/organizations/page.tsx:41-42`,
  `180-196`, `293`
- **Запрос:** `usePlatformOrganizations()` → `GET /organizations` и `useImpersonationAudit()` →
  `GET /admin/platform/impersonation` (`features/admin/hooks/use-organizations.ts:60-65`,
  `139-144`). Оба — `{ data: X = [] }`, без `isError`. Ошибки создания организации при этом
  показываются через `errorMessage`, то есть шаблон «ошибка записи видна, ошибка чтения нет»
  повторяется и здесь.
- **Что видит пользователь:** пустую таблицу организаций и пустой журнал импersonation.
  Прямо над таблицей — форма «Create organization». Ложный вывод «этой организации ещё нет»
  ведёт к созданию дубликата (и к 409 slug-taken в лучшем случае).
- **Severity:** major

### [ ] E-16 Редакторы упражнений: «No exercises yet. Click "+ Add exercise" to create one» при отказе чтения
- **Экран:** `/admin/lessons/<lessonId>/exercises` —
  `app/(admin)/admin/lessons/[lessonId]/exercises/page.tsx:146`, `305-308`;
  и его вложенный близнец
  `app/(admin)/admin/skills/[id]/topics/[topicId]/lessons/[lessonId]/exercises/page.tsx:146`, `282-285`.
  Тот же шаблон в `/admin/skills/<id>` (`:259-260` «No topics yet.»),
  `/admin/skills/<id>/reference` (`:146-149`), `/admin/skills/<id>/topics/<topicId>`
  (`:321-322` «No lessons yet.»), `/admin/leagues/tiers` (`:22`, `:99`).
- **Запрос:** `useAdminExercises(lessonId)` → `GET /admin/lessons/{id}/exercises`
  (`features/admin/hooks/use-admin.ts:272-279`), `{ data: exercises = [], isLoading }`,
  `isError` не читается.
- **Что видит пользователь:** прямое приглашение создать упражнения в уроке, у которого они
  уже есть. Здесь ложный вывод дороже, чем в списках: админ жмёт «+ Add exercise» и **пишет
  второй набор упражнений** в тот же урок. Выделено из E-14 отдельно именно из-за призыва
  к действию в тексте пустого стейта.
- **Severity:** major

### [ ] E-17 `/admin/skills/<id>`: вечное «Loading skill...» вместо ошибки
- **Экран:** `/admin/skills/<id>` — `app/(admin)/admin/skills/[id]/page.tsx:24-26`, `68-69`
- **Запрос:** `useAdminSkills()` → `GET /admin/skills`. Навык ищется в списке
  (`skills.find(...)`), а ветка отказа выглядит так: `if (!skill) return <p>Loading skill...</p>;`.
  При ошибке запроса (и при несуществующем id) `skills` навсегда `[]`, значит `skill`
  навсегда `undefined` — надпись «Loading skill...» остаётся на экране бесконечно.
- **Что видит пользователь:** страница, которая «грузится» вечно. Ложный вывод «медленно/
  зависло», хотя запрос уже завершился отказом; ни ретрая, ни объяснения нет.
- **Severity:** major

### [x] E-18 Упражнение «диалог с ИИ»: при ошибке фабрикуется реплика ИИ-клиента
- **Экран:** любой урок с упражнением `ai_dialogue` (`/session/<lessonId>`) —
  `features/exercise/components/ai-dialogue-exercise.tsx:104-121`
- **Запрос:** `POST /exercises/{exerciseId}/chat`. В `catch` реплика **придумывается на
  клиенте**:
  ```
  } catch (error) {
      console.error("Failed to send message:", error);
      setMessages(prev => [...prev, { role: "assistant",
          content: "Понял. Что ещё хотел обсудить?" }]);
  }
  ```
  Ошибка уходит только в консоль. Голосовая ветка того же компонента ошибку показывает
  (`handleVoiceError` → `voiceError`), а текстовая — нет.
- **Что видит пользователь:** «клиент» ответил нейтральной фразой, и диалог продолжается.
  Это уже не «пусто вместо ошибки», а выдуманные данные: ученик считает, что его реплика
  дошла до ИИ и учтена, тогда как ход не сохранён и не оценён. Дальше он ещё и добирает
  `minTurns` из фальшивых реплик и уходит с упражнения «выполненным».
- **Severity:** blocker

