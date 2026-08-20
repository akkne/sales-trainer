# Аудит: провал записи, выданный за успех (класс W)

Ночной прогон 2026-08-21. Продолжение `docs/AUDIT_ERROR_MASKING.md` (класс E — «ошибка чтения
выглядит как «данных нет»»). Здесь другой класс: **UI ведёт себя так, будто сервер согласился,
хотя он отказал**. Эталоны класса: E-18 (фабрикация реплики ИИ, исправлено в `d280d72`) и
побочный эффект E-14 (сохранение поверх реальных данных нулями, исправлено в `be7e5e8`).

Только `src/frontend`. Только чтение, ничего не правилось.

Обозначения: `[ ]` — не исправлено, `[x]` — исправлено.

---

## Сводка (по убыванию ущерба)

| id | что | ущерб | severity |
|----|-----|-------|----------|
| W-5 | диалоговый тренажёр: неотправленная реплика остаётся в стенограмме | потеря данных + расхождение с серверной стенограммой, по которой считается оценка | blocker |
| W-1 | чат с другом: поле очищается до ответа сервера | потеря данных (сообщение) | major |
| W-8 | редактор упражнений: строка исчезает до ответа сервера, отката нет | ложный вывод «удалено» | major |
| W-9 | редактор упражнений: перестановка ▲▼ не отправляется вообще | потеря данных (порядок) | major |
| W-13 | онбординг: выбор навыков пишется best-effort, отказ съеден `catch {}` | потеря данных (выбор) | major |
| W-6 | вся контентная админка: `onError` только в `clientLogger` (49 мутаций) | нет сообщения | major |
| W-2 | сессия урока: провал submit молчит в 9 из 10 типов упражнений | нет сообщения | major |
| W-3 | теоретический урок: провал отправки карточек молчит, урок не закрывается | нет сообщения | major |
| W-7 | «Выйти»: провал logout оставляет сессию живой | нет сообщения | major |
| W-12 | вход через Google: провал не показывается нигде | нет сообщения | major |
| W-4 | обсуждения: провал публикации ответа молчит | нет сообщения | minor |
| W-10 | оверрайд режима диалога: «Взять базу»/«Оставить своё» молчат | нет сообщения | minor |
| W-11 | редактор урока организации: подтверждение удаления закрывается всегда | нет сообщения | minor |
| W-14 | группа «клик без последствий» (друзья, голоса, «прочитано», роли) | нет сообщения | minor |
| W-15 | админка лиг: 5 мутаций молча, включая необратимое «закрыть неделю» | нет сообщения | minor |

---

### [x] W-1 Чат с другом: текст стирается из поля до ответа сервера
- **Где:** `src/frontend/features/friends/components/chat-input.tsx:16-20` (`handleSend` →
  `onSend(trimmed); setValue("")`), оба потребителя — `features/friends/components/chat-window.tsx:38`
  (правый рельс) и `:151` (полноэкранный чат). Мутация — `useSendChatMessage`
  (`features/friends/hooks/use-chat.ts:56`).
- **Мутация:** `POST /chat/conversations/{id}/messages`. `onError` есть и показывает toast
  «Не удалось отправить сообщение», но композер уже очищен: черновик нигде не сохранён и вернуть
  его нечем.
- **Что видит пользователь:** поле опустело — это и есть универсальный сигнал «отправлено».
  Сообщение в ленте не появляется (лента живёт на `invalidateQueries` в `onSuccess`), toast
  уезжает через несколько секунд, и остаётся пустой чат без набранного текста.
- **Ущерб:** потеря данных (набранное сообщение) + ложный вывод «отправлено»
- **Severity:** major

### [x] W-2 Сессия урока: провал `POST /exercises/{id}/submit` молчит в 9 из 10 типов упражнений
- **Где:** `src/frontend/app/session/[lessonId]/page.tsx:99-124` (`handleExerciseSubmit`,
  `submitExerciseMutation.mutate` на строке 101) — экран прохождения урока, кнопка «Проверить».
- **Мутация:** `POST /exercises/{id}/submit` через `useSubmitExercise`
  (`features/exercise/hooks/use-lesson.ts:54-74`) — у хука нет `onError`, у вызова передан только
  `onSuccess`. `submitExerciseMutation.error` прокинут ровно в один компонент —
  `RewriteExercise` (`page.tsx:346`, отрисовка в `features/exercise/components/rewrite-exercise.tsx:142`).
  Остальные девять типов (`ChooseOption`, `FillBlank`, `Reorder`, `MatchPairs`, `Categorize`,
  `SpotMistake`, `FreeText`, `EvaluateCall`, `AiDialogue`) получают только `isSubmitting` и
  `submittedResult`.
- **Что видит пользователь:** нажал «Проверить» → спиннер на кнопке → кнопка снова активна, ответ
  не разобран, ни слова об ошибке. Читается как «кнопка не работает», и учащийся жмёт её повторно,
  каждый раз отправляя новую попытку на сервер.
- **Ущерб:** нет сообщения (+ дубли попыток при повторных нажатиях)
- **Severity:** major

### [x] W-3 Теоретический урок: провал отправки карточек не показывается и оставляет урок наполовину зачтённым
- **Где:** `src/frontend/app/session/[lessonId]/page.tsx:556-569` (`TheoryLessonFlow.handleComplete`,
  `await submitExerciseMutation.mutateAsync(...)` в цикле по всем карточкам, строка 563), кнопка
  «Завершить» на последней карточке.
- **Мутация:** `POST /exercises/{id}/submit` по одному разу на карточку. `try/catch` нет: первый
  отказ роняет `handleComplete` необработанным rejection. `setCompleted(true)` не выполняется —
  это защищает от ложного экрана «Теория пройдена», но карточки до места отказа уже зачтены.
- **Что видит пользователь:** ничего. Экран остаётся на последней карточке, `isCompleting`
  сбрасывается, кнопка снова активна. Повторное нажатие заново отправляет уже зачтённые карточки.
  Бэкенд закрывает урок только когда зачтены все карточки, так что урок остаётся незакрытым без
  единого объяснения.
- **Ущерб:** нет сообщения (частичная запись прогресса, урок не закрывается)
- **Severity:** major

### [ ] W-4 Обсуждения: провал публикации ответа в теме молчит
- **Где:** `src/frontend/app/(main)/discuss/[threadId]/page.tsx:54-68` (`submitReply`,
  `await addReply.mutateAsync(...)` на строке 57), кнопка «Ответить» в теме.
- **Мутация:** `POST /discuss/threads/{id}/replies` через `useAddReply`
  (`features/discuss/hooks/use-discuss.ts:178`) — без `onError`. `try/catch` в `submitReply`
  обёрнут только вокруг загрузки фото; сам `addReply.mutateAsync` без обработки. Состояние
  `replyError` существует, но заполняется только сообщением про фото.
- **Что видит пользователь:** ответ не появился в ленте, текст в поле сохранился (`setReplyBody("")`
  до него не доходит), сообщения об ошибке нет. Для сравнения: то же место в
  `features/discuss/components/new-thread-modal.tsx:56-62` обработано правильно.
- **Ущерб:** нет сообщения
- **Severity:** minor

### [x] W-5 Диалоговый тренажёр: при отказе отправки реплика пользователя остаётся в стенограмме
- **Где:** два экрана с одинаковым кодом:
  `src/frontend/app/dialog/[bundleId]/[modeId]/page.tsx:163-186` (добавление в `messages` на 171,
  `catch` на 183) — тренажёр «Диалог»; и
  `src/frontend/app/companies/[id]/call/chat/page.tsx:148-173` (добавление на 156, `catch` на 168) —
  тренировочный звонок по компании. Композер —
  `features/dialog/components/chat-input.tsx:15-22`, очищает поле сразу после `onSend`.
- **Мутация:** `sendDialogMessage` → `POST /dialog/sessions/{id}/messages`. При отказе показывается
  `setError(...)`, но реплика пользователя **не откатывается** из `messages`, и текст из поля уже
  стёрт (`setInputValue("")` в композере).
- **Что видит пользователь:** свой пузырь в стенограмме — то есть «реплика ушла», — плюс строку
  ошибки. На сервере этой реплики нет: последующее `completeDialogSession` разбирает и оценивает
  другую стенограмму, чем та, что на экране. Ровно тот же ущерб, что в E-18, только со стороны
  реплики учащегося: там фабриковался ответ ИИ, здесь остаётся неотправленная реплика человека.
  Восстановить текст нечем — надо набирать заново.
- **Ущерб:** потеря данных (реплика) + запись неверных данных в восприятии (стенограмма на экране
  расходится с серверной, по которой считается оценка)
- **Severity:** blocker (обучение против того, чего сервер не видел — тот же класс, что E-18)

### [x] W-6 Вся контентная админка: `onError` пишет только в `clientLogger`, на экран не выходит ничего
- **Где:** `src/frontend/features/admin/hooks/use-admin.ts` — 49 мутаций, у 48 есть `onError`, и
  каждый — только `clientLogger.error(...)` (примеры: `:111`, `:126`, `:140`, `:172`, `:187`,
  `:202`, `:234`, `:249`). Ни один экран этих ошибок не читает: страницы вызывают
  `await X.mutateAsync(...)` без `try/catch` (`app/(admin)/admin/reference/page.tsx:62,67`,
  `admin/topics/page.tsx:94,114,129`, `admin/skills/[id]/page.tsx:67,72`,
  `admin/skills/[id]/reference/page.tsx:41,58`, `admin/skills/page.tsx:51,57`,
  `admin/prompts/page.tsx:27,46`, `admin/skills/[id]/topics/[topicId]/page.tsx:108,113`,
  `admin/discuss/page.tsx:41,50`) или `X.mutate(id)` без колбэков.
  `features/dialog/hooks/use-admin-dialog.ts` (8 мутаций) не имеет даже `onError`, а его экраны
  (`admin/dialog/page.tsx:60,66,71`, `admin/dialog/[bundleId]/page.tsx:61,67,72`) тоже без
  `try/catch`.
- **Мутация:** весь набор `POST/PUT/DELETE /admin/*` (навыки, темы, уроки, упражнения, справочник,
  техники, промпты, теги обсуждений, бандлы/режимы диалогов). При отказе: необработанный rejection
  в обработчике `onClick`, ноль изменений на экране.
- **Что видит пользователь:** админ жмёт «Save» / «Create» / «Confirm» → кнопка мигает «Saving…» →
  возвращается в исходное состояние. Форма остаётся открытой с введёнными данными, список не
  меняется. Это читается либо как «кнопка не нажалась», либо как «сохранилось, просто список не
  обновился» — и оба вывода ложные. Отдельно опасен путь удаления: `deleteX.mutate(id)` и сразу
  `setConfirmDeleteId(null)` (`admin/reference/page.tsx:207-208`,
  `admin/skills/[id]/page.tsx:324-325`, `admin/skills/[id]/reference/page.tsx:258-259`,
  `admin/skills/[id]/topics/[topicId]/page.tsx:392-393`, `admin/techniques/page.tsx:310-311`) —
  подтверждение закрывается независимо от результата, то есть жест «удалить» выглядит принятым.
- **Ущерб:** нет сообщения (по всей админке; правки контента теряются молча)
- **Severity:** major

### [ ] W-7 «Выйти»: провал `POST /auth/logout` оставляет сессию живой и ничего не говорит
- **Где:** `src/frontend/features/auth/hooks/use-auth.ts:269-282` (`useLogout`). Вызовы:
  `app/(main)/settings/page.tsx:160` (кнопка «Выйти» в настройках) и
  `features/auth/components/awaiting-organization-gate.tsx:75` (единственный выход для
  пользователя без организации).
- **Мутация:** `POST /auth/logout`. У хука только `onSuccess`: `clearAuthSession()` +
  `router.push("/login")`. `onError` нет ни в хуке, ни в двух вызовах.
- **Что видит пользователь:** «Выходим…» → снова «Выйти», тот же экран настроек. Токен в
  `auth-store` не очищен, редиректа нет. Человек, который нажал «Выйти» и отошёл от чужого
  компьютера, считает, что вышел; сессия при этом активна. На экране-заглушке
  `awaiting-organization-gate` это вообще единственный способ выйти, и он молча не работает.
- **Ущерб:** нет сообщения (последствие — сессия остаётся активной при уверенности, что выход
  выполнен)
- **Severity:** major

### [x] W-8 Редактор упражнений: строка исчезает из списка до ответа сервера и не возвращается
- **Где:** `src/frontend/app/(admin)/admin/lessons/[lessonId]/exercises/page.tsx:222-228`
  (`deleteRow`) и её копия
  `src/frontend/app/(admin)/admin/skills/[id]/topics/[topicId]/lessons/[lessonId]/exercises/page.tsx:222-228`
  (файлы почти идентичны). Кнопка удаления упражнения в уроке.
- **Мутация:** `DELETE /admin/exercises/{id}` через `useDeleteExercise`. Код:
  `setRows(rows.filter(r => r.id !== id)); deleteMut.mutate(id);` — сначала оптимистично убирается
  строка, потом уходит запрос без колбэков. Откатa нет. `useDeleteExercise` инвалидирует кэш только
  в `onSuccess`, а `onError` пишет лишь в `clientLogger`.
- **Что видит пользователь:** упражнение исчезло из редактора — жест удаления выглядит успешным.
  Хуже того: `rows` берётся из `localRows ?? exercises` (`:165-170`), и как только `localRows`
  проинициализирован, серверные данные вообще перестают управлять экраном — то есть исчезнувшая
  строка не вернётся ни при каком фоновом перечитывании, только после перезагрузки страницы.
  Упражнение при этом осталось в уроке, и команда продолжает его получать.
- **Ущерб:** ложный вывод «удалено» + расхождение экрана с базой (нет сообщения)
- **Severity:** major

### [x] W-9 Редактор упражнений: перестановка ▲▼ вообще не отправляется на сервер
- **Где:** `src/frontend/app/(admin)/admin/lessons/[lessonId]/exercises/page.tsx:326,332`
  (`setRows(moveExercise(rows, index, ±1))`, `moveExercise` на `:59-64` пересчитывает `sortOrder`) и
  та же пара строк в копии под `admin/skills/[id]/topics/[topicId]/lessons/[lessonId]/exercises`.
- **Мутация:** отсутствует. Перестановка меняет только `localRows`; единственная запись порядка —
  `saveExercise(row)` (`:182-208`), и она отправляет `orderInLesson` только той строки, которую
  админ отдельно открыл и сохранил. Кнопки «сохранить всё» на экране нет (единственный
  `saveExercise` вызывается из `:394`).
- **Что видит пользователь:** упражнения переставились на экране и остались в новом порядке —
  внешне это законченное действие. После перезагрузки порядок прежний; при частичном сохранении
  одной строки `sortOrder` расходится с остальными.
- **Ущерб:** потеря данных (перестановка) + ложный вывод «переставил и сохранилось»
- **Severity:** major

### [ ] W-10 Оверрайд режима диалога: «Взять базу» и «Оставить своё» падают молча
- **Где:** `src/frontend/app/(org)/org/content/overrides/[kind]/[overrideId]/page.tsx` —
  `DialogModeOverrideReviewScreen` (с `:285`), кнопки на `:403` (`keepOverride.mutate`) и `:435`
  (`acceptBase.mutate` из `ConfirmDialog`).
- **Мутация:** `POST /admin/dialog-mode-overrides/{id}/keep` и `.../accept-base`. У обеих передан
  только `onSuccess`. Соседний экран того же файла (`LearningOverrideReviewScreen`) выводит
  `{(acceptBase.isError || keepOverride.isError) && ...}` на `:247` — в экране режима диалога этого
  блока нет.
- **Что видит пользователь:** «Взять базу» — диалог подтверждения остаётся открытым, редиректа нет,
  ни слова об ошибке; действие деструктивное (копия организации уходит в архив), поэтому админ
  повторит нажатие. «Оставить своё» — бейдж состояния не меняется, тоже молча.
- **Ущерб:** нет сообщения (несогласованность с соседним экраном того же файла)
- **Severity:** minor

### [ ] W-11 Редактор урока организации: подтверждение удаления упражнения закрывается независимо от результата
- **Где:** `src/frontend/app/(org)/org/content/lessons/[lessonId]/page.tsx:438-443` —
  `void withDraft(() => deleteExercise.mutateAsync(exercise.id)).then(() => setExerciseToDelete(null))`.
- **Мутация:** `DELETE` упражнения внутри `withDraft`. `withDraft` возвращает `false` при отказе и
  выставляет `writeFailure`, но `.then` закрывает диалог в любом случае — в отличие от соседнего
  «Сохранить название» (`:350-356`), который проверяет `saved`.
- **Что видит пользователь:** диалог закрылся — жест принят; строка упражнения остаётся в списке, и
  где-то на странице появляется общая фраза «Не удалось сохранить». Связь между закрывшимся диалогом
  и этой фразой неочевидна.
- **Ущерб:** нет сообщения (сообщение есть, но действие выглядит выполненным)
- **Severity:** minor

### [ ] W-12 Вход через Google: провал `POST /auth/google` не показывается нигде (и комментарий утверждает обратное)
- **Где:** `src/frontend/shared/components/google-login-button.tsx:12-19` — в `onError` от
  `<GoogleLogin>` стоит комментарий «error state handled inside useGoogleLogin mutation». В
  `features/auth/hooks/use-auth.ts:250-267` (`useGoogleLogin`) `onError` делает только
  `clientLogger.warn`. Компонент не рендерит ни `isError`, ни `error`, пропсов для этого у него нет;
  вызовы `<GoogleLoginButton />` на `app/(auth)/login/page.tsx` и `app/(auth)/register/page.tsx`
  ничего не передают.
- **Мутация:** `POST /auth/google` с id-токеном Google.
- **Что видит пользователь:** попап Google проходит успешно (пользователь считает, что он вошёл),
  затем — тот же экран входа без единого слова. Экраны логина/регистрации показывают ошибки
  собственных мутаций (`loginMutation.isError`), поэтому отсутствие сообщения именно у Google-входа
  читается как «кнопка сломана». Комментарий в коде дезинформирует: обработки нет.
- **Ущерб:** нет сообщения (вход в тупик)
- **Severity:** major

### [x] W-13 Онбординг: выбранные навыки записываются «по возможности», отказ проглатывается пустым `catch`
- **Где:** `src/frontend/features/auth/hooks/use-onboarding.ts:18-30` — внутри одного `mutationFn`
  сначала `POST /onboarding`, затем `PUT /skills/enrolled` в `try { … } catch { /* ignore */ }`.
  Экран — `app/(auth)/onboarding/page.tsx:79-86` (шаг «Навыки», последняя кнопка).
- **Мутация:** `PUT /skills/enrolled` с выбранными на 4-м шаге навыками. Отказ гасится пустым
  `catch`; `onSuccess` всё равно ставит `isOnboardingCompleted: true` и уводит на `/tree`.
- **Что видит пользователь:** онбординг завершился, «навыки выбраны» — и на `/tree` только базовый
  `sales-basics`, потому что записать выбор не удалось. Ничто на экране об этом не говорит; в коде
  оправдание «user can adjust enrollment later from their profile», но пользователю про это не
  сообщают, а сам он считает, что уже выбрал.
- **Ущерб:** потеря данных (выбор навыков) + ложный вывод «выбор сохранён»
- **Severity:** major

### [ ] W-14 Группа «клик без последствий»: мутации без `onError` в социальных и второстепенных экранах
- **Где:** мутации, у которых нет ни `onError`, ни отображения `isError`, а состояние кнопки целиком
  берётся с сервера, — так что отказ выглядит как «кнопка не нажалась»:
  - `features/friends/hooks/use-friends.ts` — 6 мутаций, ни одного `onError`; вызовы:
    `features/friends/components/friendship-button.tsx:46,70,85,99`,
    `features/friends/components/friend-request-card.tsx:64,72,84`,
    `app/(main)/friends/page.tsx:66`, `app/(main)/friends/[userId]/page.tsx:27`.
  - `features/discuss/hooks/use-discuss.ts` — `useThreadVote` (`:239`), `useReplyVote` (`:253`),
    `useSetAcceptedReply` (`:266`), `useDeleteDiscussPhoto` (`:227`); вызовы:
    `features/discuss/components/thread-card.tsx:21`,
    `app/(main)/discuss/[threadId]/page.tsx:109,139,179,198,216`.
  - `features/dialog-reviews/hooks/use-dialog-reviews.ts:80` (`useAcknowledgeCoachingNote`), вызов —
    `app/(main)/dialog-reviews/page.tsx:156` («Прочитано»).
  - `features/skills/hooks/use-techniques.ts:120` (`useMarkTechniqueSeen`), вызов —
    `app/(main)/guidebook/page.tsx:153`.
  - `features/admin/hooks/use-demo-requests.ts:92` (`useUpdateDemoRequestStatus`) — `onError` только
    `clientLogger`; вызовы `app/(admin)/admin/demo-requests/page.tsx:101,105`, при этом `:105`
    закрывает подтверждение через `onSettled`.
  - `features/notifications/hooks/use-notifications.ts:63,82` — `onError` только `clientLogger.warn`;
    `features/notifications/components/notification-panel.tsx:29,38`, причём `:29` закрывает панель и
    делает `router.push` независимо от результата.
  - `features/admin/components/user-detail-modal.tsx:114,146,177` (фото / имя / роль пользователя) —
    `onError` мутаций в `use-admin.ts` только логирует.
- **Мутация:** голосование, заявки в друзья, «принятый ответ», удаление фото, отметки «прочитано»,
  смена статуса заявки на демо, смена роли пользователя.
- **Что видит пользователь:** нажатие ничего не меняет и ничего не сообщает. Ложный вывод здесь
  мягче, чем в W-1…W-13 (счётчик/бейдж остаётся прежним, то есть экран не врёт о результате), но
  ощущается как поломка интерфейса, и в случае «Прочитано» / «принятый ответ» человек считает
  действие выполненным и не повторяет его.
- **Ущерб:** нет сообщения
- **Severity:** minor

### [ ] W-15 Админка лиг: четыре мутации без единого сообщения об ошибке (устаревший раздел, но раздел живой)
- **Где:** `src/frontend/app/(admin)/admin/leagues/[id]/page.tsx:46` (`adjustXp`), `:60`
  (`removeMembership`, закрывается через `onSettled`), `:86` (`resync`), `:159` (`moveTier`) — в файле
  нет ни одного `isError`. Плюс `app/(admin)/admin/leagues/page.tsx:69` — `closeWeek.mutate(undefined,
  { onSettled: () => setConfirmClose(false) })`: закрытие недели необратимо, а подтверждение
  закрывается независимо от результата и ошибку не показывает (в отличие от `updateSettings` на
  `:181` того же файла, у которого сообщение есть).
- **Мутация:** `POST/PUT/DELETE /admin/leagues/*`. Ошибки уходят только в `clientLogger` (хуки в
  `features/admin/hooks/use-admin.ts`).
- **Что видит пользователь:** «Confirm» на закрытии недели гаснет — жест принят, результат неизвестен;
  начисленные XP не появились; участник остался в списке. Раздел относится к снятой с продукта
  геймификации (лиги/XP/стрики), но обе страницы по-прежнему есть в навигации админки
  (`app/(admin)/layout.tsx:130-131`), то есть достижимы.
- **Ущерб:** нет сообщения
- **Severity:** minor

---

## Проверено и находкой не является

Чтобы было видно, где код уже держит этот класс правильно (это же список образцов для правок выше):

- **`app/(org)/**` целиком** — самый аккуратный участок. `org/profile`, `org/program`, `org/people`,
  `org/assignments/*`, `org/content/generation/*`, `org/content/lessons/[lessonId]`,
  `features/org-*` — везде `onError`/`try-catch` с текстом на экране через
  `describe*WriteFailure`, отдельная `ErrorState` на провал чтения и явная проверка «сохранилось ли»
  перед закрытием формы. Исключения — W-10 и W-11.
- **`features/companies/hooks/*`** — у всех мутаций `onError` с `toast.error`;
  `useUpdateCompanyStatus` (`use-companies.ts:92-118`) — единственный корректный оптимистичный
  апдейт в проекте: `onMutate` снимает снапшот, `onError` откатывает, `onSettled` пересинхронизирует.
  `useCreatePracticeCall` (`use-practice-calls.ts:32`) — `retry: 2` + toast.
- **`features/skills/hooks/use-skill-tree.ts:70-101`** (`useUpdateEnrolledSkills`) — оптимистичный
  апдейт с откатом в `onError`, плюс `isError` доезжает до `manage-skills-modal.tsx:141`.
- **`app/(admin)/admin/organizations/*`** (включая `quota`) — `try/catch` с `errorMessage`/`feedback`
  на каждый путь записи.
- **`admin/quotes`, `admin/lessons` (`handleUpdate`), `admin/skill-stages`, `admin/leagues/tiers`,
  `admin/gamification` (XP goals), `admin/dialog` (XP scoring)** — сообщения об ошибке записи есть.
- **`features/admin/components/import-panel.tsx:104-108`** — `onImport` обёрнут в `try/catch`, ошибка
  импорта выводится; это закрывает все пять экранов импорта.
- **Авторизация:** `login`, `register`, `verify-email`, `invite/[token]`, `onboarding` (сам
  `POST /onboarding`), `demo` — все рендерят `isError` мутации. Исключения — W-12 (Google) и W-13
  (навыки в онбординге) и W-7 (logout).
- **`features/org-content-generation/components/structure-checkpoint-card.tsx:66-104`** — автосейв
  структуры: дебаунс, `onSuccess`-обновление реф-снапшота и `isError` рядом с «Сохранено HH:MM».
- **`features/discuss/components/new-thread-modal.tsx`** — единственное место в обсуждениях, где
  и создание, и последующая загрузка фото обработаны, включая частичный успех.
- **`shared/api/api-client.ts:87-91`** — транспорт не маскирует: любой не-2xx превращается в
  `ApiError`, 204 отдаётся как `undefined`, таймаут — в `RequestTimeoutError`. То есть все находки
  выше — про обработку на стороне UI, а не про проглатывание в клиенте.
- **`features/dialog/hooks/use-dialog.ts:111-165`** — `startDialogSession` / `sendDialogMessage` /
  `completeDialogSession` пробрасывают ошибки наружу (спецобработка 400 «уже завершена» корректна).
- **`features/voice/hooks/use-voice.ts:179-192`** — провал создания голосовой сессии уходит в
  `onError` вызывающего экрана.
- **`features/notifications/hooks/use-notifications.ts`** — `onError` есть, но только
  `clientLogger.warn`; вынесено в W-14, потому что это сознательное «залогировать и забыть».

## Покрытие

- Вызовов `.mutate(` / `.mutateAsync(` в `src/frontend` — 224, из них 38 в `__tests__/`;
  **проверено все 186 не-тестовых, во всех 72 файлах, где они есть**. Для каждого файла прочитан и
  сам вызов, и хук мутации (наличие `onError` и что он делает), и то, читает ли экран
  `isError`/`error`. Результат по каждому файлу — либо в находках выше, либо в разделе «проверено и
  находкой не является».
- Дополнительно проверены **165 объявлений `useMutation`** на наличие `onError` и на то, что
  `onError` делает (ключевое открытие — `use-admin.ts`: 48 `onError`, все только в лог).
- Проверены все прямые (не через `useMutation`) пути записи: `apiClient.post/put/patch/delete` в
  компонентах и сервисных модулях, `features/dialog/hooks/use-dialog.ts`,
  `features/voice/hooks/use-voice.ts`, `shared/analytics/track.ts`.
- Проверены все места оптимистичного обновления: `onMutate` (2 шт.) и `setQueryData` (14 шт.).

**Не дошёл (сознательно вне охвата):**

- `src/frontend/__tests__/**` — тестовые вызовы `.mutate` не аудировались (но существующие тесты
  `useCompanies`/`useCompanyLogs`/`useCompanyPersonas` покрывают откат оптимистичного статуса).
- `shared/analytics/track.ts:60` — «best-effort» отправка события аналитики; проглатывание ошибки
  здесь корректно по смыслу, отдельной находки не заводил.
- Голосовые режимы (`app/dialog/[bundleId]/[modeId]/voice/page.tsx`,
  `app/companies/[id]/call/voice/page.tsx`) проверены только по путям записи в БД
  (`createPracticeCall`, `completeDialogSession`, создание сессии). Логика WebRTC/стриминга и
  локальные субтитры (`handleTranscript`/`handleAiText`) не аудировались: там реплики приходят из
  голосового конвейера, а не из HTTP-мутации, так что «фабрикация» имеет другую природу и требует
  отдельного прогона.
- Бэкенд не смотрел вообще: все выводы — про то, что видит пользователь во фронтенде.
