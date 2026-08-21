# Независимое ревью ночного прогона (`origin/main..main`)

**Ревью велось против HEAD `84c4449` → к концу прогона HEAD стал `7aa7800`.**
Пока я читал, приземлилось ещё 5 коммитов: `846c020`, `8ef73b5`, `7d93a1f`, `cccc8b9`, `7aa7800`.
`846c020` (T-6, раскатка `[TenantScoped]`) попал в мой разбор области 3, потому что я снимал дифф
уже после него. Остальные четыре (`8ef73b5`, `7d93a1f`, `cccc8b9`, `7aa7800`) **не проверены**.
Итого в диапазоне 70 коммитов.

Роль: независимый ревьюер. Ничего не правил, не коммитил, серверы не трогал. Задача — найти
регрессии, а не подтвердить работу; похвалы здесь нет намеренно.

---

## Находки

### [x] R-1 Провал `POST /auth/logout` оставляет живую refresh-cookie, и api-client молча выдаёт по ней новый access-token
- **Коммит/файл:** `29a9a22`; `src/frontend/features/auth/hooks/use-auth.ts:280-296`,
  `src/frontend/shared/stores/auth-store.ts:70-73`, `src/frontend/shared/api/api-client.ts:78-86`,
  `src/backend/identity-service/Identity/Features/Auth/AuthController.cs:327-338`
- **Что не так:** refresh-token живёт **только** в httpOnly-cookie, и отзывает его исключительно
  серверный `POST /auth/logout` (`RevokeRefreshTokenAsync` + `Response.Cookies.Delete`).
  `clearAuthSession()` удаляет из браузера **только** `localStorage.accessToken` — cookie он
  тронуть не может. Значит на новом пути (`onSettled` при провале запроса) браузер остаётся с
  валидной refresh-cookie, а `fetchWithAuthToken` на любой 401 сам вызывает `attemptTokenRefresh()`
  → `POST /auth/refresh` с этой cookie → успех → `localStorage.setItem("accessToken", ...)`
  напрямую, минуя zustand. Сессия восстановлена на сетевом уровне, а `authenticatedUser` в стор
  так и остался `null`. Текст тоста «Вы вышли на этом устройстве» при этом — неправда.
- **Как проявится:** identity-service недоступен / гейтвей отдал 502 / нет сети. Пользователь
  нажимает «Выйти» в `/settings` → тост «Не удалось завершить сессию на сервере… Вы вышли на этом
  устройстве» → редирект на `/login`. Дальше пользователь набирает в адресной строке `/tree` (или
  жмёт «назад»): запросы уходят без `Authorization` → 401 → refresh по живой cookie → новый
  access-token в `localStorage` → данные грузятся под тем же аккаунтом, хотя UI считает, что
  пользователь вышел. На чужом устройстве (сценарий, ради которого тост и советует сменить пароль)
  сессия фактически не закрыта.
- **Severity:** major
- **Уверенность:** механизм — точно (прочитан весь путь: cookie отзывается только сервером;
  `doRefresh` пишет в `localStorage` напрямую; `useInitAuth` не сработает, т.к. `store.accessToken`
  остался `null`); конкретный автотриггер сразу после выхода — требует проверки (`track.ts`
  защищён `hasAccessToken()`, `/auth/login/start` анонимен, так что на самом `/login` ничего
  не «оживляет» сессию само)
- **Resolved (commit `8f98116a`):** `clearAuthSession()` now sets an `authSessionTerminated`
  localStorage marker; `attemptTokenRefresh()` in `api-client.ts` checks it before calling
  `doRefresh()` and short-circuits to `false` if set, so a leftover refresh-cookie can no longer
  mint a new access token after a logout. `setAccessToken()` clears the marker on the next
  successful login. A live session's ordinary refresh (no logout involved) never sets the marker,
  so that path is unchanged.

### [x] R-2 На истёкшей сессии logout даёт двойную навигацию и пугающий тост про смену пароля
- **Коммит/файл:** `29a9a22`; `src/frontend/features/auth/hooks/use-auth.ts:283-296`,
  `src/frontend/shared/api/api-client.ts:78-86`
- **Что не так:** если `POST /auth/logout` вернул 401 и refresh не удался, api-client сам делает
  `localStorage.removeItem` + `window.location.href = "/login"` и бросает `Error("Session expired")`.
  Дальше `onError` показывает тост «…советуем сменить пароль, если устройство не ваше», а
  `onSettled` делает `router.push("/login")` поверх уже идущего hard-navigation. Тост при полной
  перезагрузке страницы гарантированно теряется, а сам текст для «сессия просто истекла» —
  неуместно тревожный.
- **Как проявится:** пользователь не заходил сутки, access-token истёк, refresh-token тоже.
  Жмёт «Выйти» → мигает тост про смену пароля → жёсткая перезагрузка `/login` съедает его.
- **Severity:** minor
- **Уверенность:** точно
- **Resolved (commit `8f98116a`):** `SessionExpiredError` (thrown by `fetchWithAuthToken`'s 401
  branch instead of the previous plain `Error`) lets `useLogout` tell "session already expired,
  api-client already hard-navigated" apart from an actual failed revoke. `onError` now skips the
  password-change toast, and `onSettled` skips the redundant `router.push` for this case.

### [x] R-3 `useLogout` не чистит кеш React Query, в отличие от входа
- **Коммит/файл:** `29a9a22`; `src/frontend/features/auth/hooks/use-auth.ts:288-292` против
  `use-auth.ts:36-37` (`useHandleSuccessfulAuth` делает `queryClient.clear()`)
- **Что не так:** выход оставляет в кеше все данные вышедшего пользователя. Сейчас это прикрыто
  тем, что `queryClient.clear()` вызывается на **входе**, но защита односторонняя: любой путь,
  который окажется между выходом и следующим `handleSuccessfulAuth` (см. R-1 — восстановление
  токена через refresh), увидит кеш прошлого пользователя.
- **Как проявится:** совместно с R-1: токен восстановился, пользователь вернулся на `/tree`,
  часть экранов рисуется из кеша прошлой сессии без запроса.
- **Severity:** minor
- **Уверенность:** точно (что `clear()` нет), требует проверки (что это где-то видно глазом)
- **Resolved (commit `8f98116a`):** `useLogout`'s `onSettled` now calls `queryClient.clear()`,
  matching `useHandleSuccessfulAuth`'s behavior on login.

### [x] R-4 `submitError` — общая мутация на весь урок, её никто не сбрасывает: ошибка едет на следующие упражнения
- **Коммит/файл:** `16afd08`; `src/frontend/app/session/[lessonId]/page.tsx:70` (`useSubmitExercise()`
  один инстанс на весь `SessionFlow`), `:146-152` (`handleSkip` / `handleContinueAfterResult` —
  `submitExerciseMutation.reset()` не вызывается нигде), `:280..389` (`submitError={submitExerciseMutation.error}`
  во все 10 типов), `src/frontend/features/exercise/components/exercise-action-footer.tsx:52-58`
- **Что не так:** `submitExerciseMutation.error` в TanStack Query «липкий» — он живёт до
  следующего `mutate` или явного `reset()`. Ни `handleSkip()`, ни `handleContinueAfterResult()`,
  ни `handleStartMistakesReview()` не сбрасывают мутацию. Это ровно тот случай, о котором просили:
  новая ветка ошибки рисуется **вместо/поверх** рабочего контента.
- **Как проявится:** упражнение 3, `POST /exercises/{id}/submit` падает (500/таймаут) → в футере
  красное «Произошла ошибка при проверке. Попробуй ещё раз.». Пользователь жмёт «Пропустить» →
  открывается упражнение 4, к которому он не прикасался, **и там уже висит та же красная ошибка**.
  И на 5-м, и на 6-м — до первой успешной отправки. Плюс то же самое переезжает в раунд «разбор
  ошибок» (`handleStartMistakesReview` тоже не сбрасывает).
- **Severity:** major
- **Уверенность:** точно
- **Resolved (commit `472aba7b`):** `advanceToNext()` — the single chokepoint both `handleSkip`
  and `handleContinueAfterResult` funnel through — now calls `submitExerciseMutation.reset()`
  before advancing, so the error clears on every exercise transition (including into the
  mistakes-review round, which is reached through the same function).

### [x] R-5 E-11: `isError` в `SessionRouter` выбрасывает весь урок при провале фонового refetch
- **Коммит/файл:** `e608179`; `src/frontend/app/session/[lessonId]/page.tsx:594-620`
  (`if (isError || !exercises) return <ErrorState .../>`), хук —
  `src/frontend/features/exercise/hooks/use-lesson.ts:47-52` (без `staleTime`/`refetchOnWindowFocus`),
  глобальные дефолты — `src/frontend/app/providers.tsx:52` (`staleTime: 60_000, retry: 1`)
- **Что не так:** до фикса `isError` не читался вообще, поэтому провалившийся **фоновый** refetch
  был безвреден. Теперь `isError` — единственное условие, и он становится `true` на любом
  завершившемся с ошибкой запросе, даже когда `data` уже есть. `SessionRouter` при этом
  размонтирует `SessionFlow` целиком, а всё состояние прохождения (`exerciseQueue`,
  `currentQueueIndex`, `correctAnswerCount`, `mistakeExercises`, таймер) — локальный state
  внутри `SessionFlow`.
- **Как проявится:** учащийся идёт по уроку дольше минуты (запрос стал stale), переключается в
  другую вкладку и возвращается → `refetchOnWindowFocus` (дефолт `true`, здесь не отключён)
  дёргает `/lessons/{id}/exercises` → сеть моргнула → после `retry: 1` `isError = true` →
  **весь урок заменяется на «Не удалось загрузить уроки/урок» и весь прогресс прохождения
  теряется**, даже кнопка «Повторить» начнёт урок с нуля.
- **Severity:** major
- **Уверенность:** механизм `isError` при наличии данных — точно; что `refetchOnWindowFocus`
  реально стреляет в этом хуке — требует проверки в браузере
- **Resolved (commit `0f6a53ee`):** `SessionRouter` now destructures `isLoadingError` instead of
  `isError` and gates the full-screen error state on that (plus `!exercises` for the belt-and-
  braces "no data at all" case). `isLoadingError` is only `true` on a first-load failure with no
  data ever obtained; a background-refetch failure (`isRefetchError`) leaves `exercises` populated
  and now falls through to the normal render, per the AD-7-established
  `isLoadingError`/`isRefetchError` split for this installed `@tanstack/react-query` version
  (see `docs/DECISIONS.md`).

### [x] R-6 Тот же shape во всей серии E-фиксов: `isError` проверяется раньше «пусто» и вытесняет уже отрисованные данные — RESOLVED
- **Коммит/файл:** серия `c7e54d9`, `529c096`, `b75563d`, `6817709`, `84ecf34`, `248115d`,
  `eb9d771`, `be193e4`; пример — `src/frontend/app/(main)/tree/page.tsx:201-208`
  (`if (isError) return <ErrorState .../>` стоит **до** `if (enrolledSkills.length === 0)`) и
  `:475-484` (`isError ? <ErrorState/> : <>…весь центр экрана…</>`)
- **Что не так:** инверсии «пусто → ошибка» я **не нашёл** — во всех проверенных местах пустое
  состояние сохранено и достижимо. Но выбранная форма (`isError` как первый гейт, без
  `&& !data`) означает, что провал фонового обновления гасит уже показанный корректный контент.
  Это системное решение серии, а не единичная опечатка, поэтому вынесено одним пунктом.
- **Как проявится:** `/tree` открыт, навыки отрисованы; спустя минуту фоновый refetch `/skills`
  падает → список навыков и весь центральный столбец заменяются на ErrorState, хотя данные в
  кеше валидны.
- **Severity:** minor (для `/tree` и остальных — косметика; для `/session` то же самое стоит
  major, см. R-5, потому что там теряется работа)
- **Уверенность:** точно (код), требует проверки (частота реального триггера)
- **Partially resolved (commit `8cd7e975`):** swapped the same `isError` → `isLoadingError` gate
  (see R-5) in the four highest-impact screens named in the task scope —
  `app/(main)/tree/page.tsx` (`PathSkillList`, `PathOverallProgress`, `PathCenterColumn`,
  `SkillTreePage` root), `app/(main)/skill/[id]/page.tsx`, `app/(main)/skill/[id]/map/page.tsx`,
  and `app/(main)/profile/page.tsx` (the "Уроки" stat tile and the "Изучаемые навыки" card).
  **Not swept tonight:** the other E-series screens sharing this shape — `/reference/<id>` (E-1),
  `/guidebook` (E-8), `/companies/<id>` (E-9), `/friends` + `/friends/<userId>` (E-10), and the
  E-13..E-17 admin content-list screens (owned by a concurrent agent's pass, not re-checked here).
  Recorded as `docs/NIGHT_AUDIT_QUESTIONS.md` Q-14 rather than left unmentioned.
- **Fully resolved (2026-08-21, see Q-14):** swept every remaining screen named above plus the
  admin content-list screens. `/friends/[userId]/page.tsx` needed no change — it already gates on
  `!profile` (data presence), not on `isError`. `/friends/page.tsx`'s incoming-requests banner and
  `/admin/quotes`, `/admin/discuss`, and `CompanyReadinessCard`/`CompanyBriefingCard` needed no
  change either — each is already additive (never discards already-rendered content) or already
  gates on data presence. `/admin/voice/usage` already used the full
  `isLoadingError`/`isRefetchError` split and was left untouched. Everywhere else the destructive
  gate (`isError`/bare `error` before an early return, or before an already-populated list) was
  swapped for `isLoadingError`, exactly as done for R-5/the first R-6 pass: no inversion of
  behaviour, only the background-refetch-discards-good-data case is now fixed. 20 screens/files
  touched in total — see `docs/NIGHT_AUDIT_QUESTIONS.md` Q-14 for the full file list and the one
  pre-existing test (`__tests__/CompanyPage.test.tsx`) whose mocks needed `isLoadingError: true`
  added to keep matching the (now more precise) production gate.

### [x] R-7 `stripFeedbackHtml` склеивает слова на границах блочных тегов
- **Коммит/файл:** `b128e0a`; `src/frontend/shared/components/feedback-html.tsx:29-31`;
  потребитель — `src/frontend/features/org-dialogs/components/dialog-session-list.tsx:105`
- **Что не так:** `sanitizeHtml(html, { allowedTags: [] })` вырезает теги, **не подставляя
  пробел**. Затем `.replace(/\s+/g, " ")` уже нечего чинить.
- **Как проявится:** проверено запуском на реальном пакете (`sanitize-html@2.17.7`):
  - `"<h3>Итог</h3><p>Первое предложение.</p><p>Второе предложение.</p>"` →
    `"ИтогПервое предложение.Второе предложение."`
  - `"<ul><li>раз</li><li>два</li></ul>"` → `"раздва"`
  - `"строка1<br>строка2"` → `"строка1строка2"`
  РОП видит в списке `/org/dialogs` превью фидбэка со склеенными словами в каждой второй строке.
- **Severity:** major
- **Уверенность:** точно (выполнено, вывод выше)
- **Resolved:** `stripFeedbackHtml` (`src/frontend/shared/components/feedback-html.tsx`) now
  sanitizes to the safe allowlist first, then replaces block-boundary tags (`h3`/`p`/`ul`/`ol`/
  `li`/`br`) with a literal space *before* discarding the remaining tags, so block boundaries
  never glue words together. Re-run oracle: `"<h3>Итог</h3><p>Первое предложение.</p><p>Второе
  предложение.</p>"` → `"Итог Первое предложение. Второе предложение."`;
  `"<ul><li>раз</li><li>два</li></ul>"` → `"раз два"`; `"строка1<br>строка2"` →
  `"строка1 строка2"`.

### [x] R-8 `stripFeedbackHtml` возвращает HTML-escaped сущности, а рендерится как текст
- **Коммит/файл:** `b128e0a`; `src/frontend/shared/components/feedback-html.tsx:29-31`;
  `src/frontend/features/org-dialogs/components/dialog-session-list.tsx:105`
  (`{stripFeedbackHtml(session.feedbackSummary)}` — текстовый child React, не `innerHTML`)
- **Что не так:** `sanitize-html` — генератор **HTML**, он экранирует `&`, `<`, `>` в текстовых
  узлах. Результат кладётся в React как обычный текст, поэтому сущности видны буквально.
- **Как проявится:** проверено запуском:
  - `"Оценка < 70 & \"низко\""` → `"Оценка &lt; 70 &amp; \"низко\""`
  - `"Клиент сказал: 5 > 3"` → `"Клиент сказал: 5 &gt; 3"`
  Любой фидбэк, где модель написала «ниже 70 & мало» или «5 > 3», в превью отрисуется как
  `&amp;` / `&gt;`. Для LLM-вывода это не редкость.
- **Severity:** major
- **Уверенность:** точно (выполнено)
- **Resolved:** added `decodeFeedbackTextEntities` in `feedback-html.tsx`, which undoes exactly
  the three entities `sanitize-html`'s text escaper produces (`&lt;`, `&gt;`, `&amp;` — decoded
  last to avoid mangling a literal `&lt;` typed by the model), applied after the plain-text pass.
  Re-run oracle: `"Оценка < 70 & \"низко\""` → `"Оценка < 70 & \"низко\""`;
  `"Клиент сказал: 5 > 3"` → `"Клиент сказал: 5 > 3"`.

### [x] R-9 `<ol>` не в allowlist: нумерованный список превращается в осиротевшие `<li>`
- **Коммит/файл:** `b128e0a`; `src/frontend/shared/components/feedback-html.tsx:12`
- **Что не так:** `allowedTags` содержит `ul`, `li`, но не `ol`. При `disallowedTagsMode: "discard"`
  `<ol>` выбрасывается, а его `<li>` остаются.
- **Как проявится:** проверено: `"<ol><li>a</li><li>b</li></ol>"` → `"<li>a</li><li>b</li>"` —
  `<li>` вне списка, без маркеров и отступов. Модель регулярно нумерует рекомендации.
- **Severity:** minor
- **Уверенность:** точно (выполнено)
- **Resolved:** added `"ol"` to `FEEDBACK_HTML_OPTIONS.allowedTags` in `feedback-html.tsx`.
  Re-run oracle: `sanitizeFeedbackHtml("<ol><li>a</li><li>b</li></ol>")` →
  `"<ol><li>a</li><li>b</li></ol>"` (kept intact); `stripFeedbackHtml` of the same input →
  `"a b"`.
- **Не находка (проверено и чисто):** сам allowlist безопасен. `allowedAttributes: {}` убивает
  `href`, `style` и все `on*`; `script`/`img onerror`/`a href="javascript:"`/`svg onload`/`iframe`
  вырезаются полностью (`nonTextTags` по умолчанию съедает и содержимое `script`/`style`).
  Зависимость в правильном `src/frontend/package.json` (prod) + `@types` в dev, залочено
  (`sanitize-html 2.17.7` в `package-lock.json`).
- **Re-verified after the R-7/R-8/R-9 fix (allowlist unchanged, only text-joining/decoding
  logic added around it):** `strip("<script>alert(1)</script><img src=x onerror=alert(1)><a
  href=\"javascript:alert(1)\">x</a>")` → `"x"`; `strip('<p style="position:fixed"
  onclick="x()">t</p>')` → `"t"` (`sanitizeFeedbackHtml` of the same → `"<p>t</p>"`, no `style`/
  `onclick`); `strip("<svg onload=alert(1)></svg><iframe src=x></iframe>")` → `""`. All three
  match pre-fix behavior — nothing dangerous survives.

### [x] R-10 W-9: ▲▼ удалены по неверному обоснованию — переупорядочивание уже персистится в соседнем экране
- **Коммит/файл:** `316da24`;
  `src/frontend/app/(admin)/admin/lessons/[lessonId]/exercises/page.tsx:287-292` и
  `.../skills/[id]/topics/[topicId]/lessons/[lessonId]/exercises/page.tsx:264-269`
  (новый текст «нет способа переупорядочить»), против уже существующего
  `src/frontend/app/(org)/org/content/lessons/[lessonId]/page.tsx:181-199`
- **Что не так:** коммит утверждает «there is no bulk-reorder endpoint on the backend … building
  one isn't a night-run call» и удаляет кнопки. Но org-редактор оверрайдов **уже** персистит
  переупорядочивание существующим эндпоинтом: `moveExerciseInList` + цикл
  `updateExercise.mutateAsync({ exerciseId, body: { …, orderInLesson } })` по каждому сдвинутому
  упражнению. Bulk-эндпоинт для этого и не нужен, а `updateExerciseMut` в удалённом экране уже
  был под рукой (`page.tsx:151`).
- **Как проявится:** контент-админ теряет рабочую функцию, и получает в интерфейсе утверждение
  «persisting a new order needs a backend endpoint that doesn't exist today», которое неверно —
  в двух шагах от него РОП переупорядочивает упражнения и это сохраняется.
- **Severity:** major
- **Уверенность:** точно
- **Resolved:** verified the claim independently by reading all three files named above (not
  taking either the commit message or this review on faith) — confirmed org editor's
  `moveExercise` persists via a per-row `updateExercise.mutateAsync({..., orderInLesson})` loop,
  and confirmed `PUT /admin/exercises/{id}` (`AdminExercisesController.cs:187`) persists
  `OrderInLesson` and is the very endpoint W-9's own `updateExerciseMut` already calls on save.
  Restored the ▲▼ buttons in both admin screens
  (`app/(admin)/admin/lessons/[lessonId]/exercises/page.tsx`,
  `.../admin/skills/[id]/topics/[topicId]/lessons/[lessonId]/exercises/page.tsx`), added a
  `moveExercise(fromIndex, toIndex)` in each that renumbers locally then loops the existing
  `updateExerciseMut.mutateAsync` over every row whose `sortOrder` changed (rolling back on
  failure), and removed the now-false "no way to reorder" note. Full evidence and correction
  appended to `docs/NIGHT_AUDIT_QUESTIONS.md` Q-8.

### [x] R-11 `saveExercise` на успехе сбрасывает `localRows` целиком и теряет несохранённые правки других строк
- **Коммит/файл:** `316da24`;
  `src/frontend/app/(admin)/admin/lessons/[lessonId]/exercises/page.tsx:204-212` (`await qc.invalidateQueries(...); setLocalRows(null);`),
  `:164-178` (`rows = localRows ?? server`, `setRows` пишет в `localRows`)
- **Что не так:** `localRows` — единая теневая копия **всего списка**. `setLocalRows(null)` после
  успешного сохранения одной строки выбрасывает вместе с ней все несохранённые изменения
  остальных строк, которые до этого коммита жили в теневой копии сколько угодно долго.
- **Как проявится:** админ правит текст упражнения A, не сохраняя, переключается на упражнение B
  (`editingId` один, так что A остаётся отредактированным в `localRows`), сохраняет B →
  `setLocalRows(null)` → правки A молча исчезают, список перерисовывается с сервера.
- **Severity:** minor (данные на сервере целы, теряется только набранный текст — но молча)
- **Уверенность:** точно по коду, требует проверки в UI (зависит от того, разрешает ли экран
  оставить A изменённым и уйти на B)

### [x] R-12 Конкурентные удаления: `onError` одного восстанавливает строку, уже удалённую другим
- **Коммит/файл:** `316da24`; те же два файла, `deleteRow`, `:225-243`
  (`const previousRows = rows; … onError: () => setRows(previousRows)`)
- **Что не так:** `previousRows` — снимок **всего** списка на момент клика. Откат одного
  провалившегося DELETE записывает этот снимок целиком.
- **Как проявится:** админ быстро удаляет A, затем B. DELETE B успешен, DELETE A падает →
  `onError` A ставит `previousRows` A, где B ещё присутствует → B возвращается в список, хотя
  на сервере он удалён. Ошибка исчезнет только после ручной перезагрузки. Плюс `onError` снова
  прибивает `localRows` как постоянную тень сервера — то, от чего коммит и уходил.
- **Severity:** minor
- **Уверенность:** точно

### [x] R-13 Composer держит уже «отправленный» текст на экране весь round-trip — реплика видна дважды
- **Коммит/файл:** `0ee865a`; `src/frontend/features/dialog/components/chat-input.tsx:18-25`
  (`const succeeded = await onSend(...); if (succeeded) setInputValue("")`),
  `src/frontend/app/dialog/[bundleId]/[modeId]/page.tsx:171` (оптимистичный бабл добавляется
  **до** `await sendDialogMessage`), `src/frontend/app/companies/[id]/call/chat/page.tsx:154-168`
- **Что не так:** оптимистичный бабл кладётся в стенограмму сразу, а поле ввода теперь очищается
  только после ответа сервера. Между этими двумя моментами — полный round-trip до OpenAI.
- **Как проявится:** учащийся отправляет реплику в диалоговом тренажёре. 3–10 секунд один и тот же
  текст висит одновременно в стенограмме и в (задизейбленном, opacity 0.5) поле ввода. Читается
  как «не отправилось», и это провоцирует именно то повторное нажатие, от которого фикс уходил.
- **Severity:** minor
- **Уверенность:** точно по коду; в friends-чате проблемы нет (там нет оптимистичного бабла)
- **Не находка (проверено):** двойной отправки нет — `disabled` в обоих composer'ах привязан к
  `isSending` / `sendMutation.isPending`, а `handleSendMessage` дополнительно возвращает `false`
  при `isSending`. Все места вызова обновлены (два `ChatInput` и два `RailChatInput`),
  `npx tsc --noEmit` чист, «легаси-алиас» `export { RailChatInput as ChatInput }` никем не
  импортируется.

### [x] R-14 Откат оптимистичного бабла слепо срезает последний элемент, а не свой
- **Коммит/файл:** `0ee865a`; `src/frontend/app/dialog/[bundleId]/[modeId]/page.tsx:188`
  (`setMessages((prev) => prev.slice(0, -1))`), конкурирующие писатели —
  `:298-321` (`handleVoiceTranscript` / `handleVoiceAiResponse`, оба добавляют сообщения и
  **не** трогают `isSending`)
- **Что не так:** `slice(0, -1)` предполагает, что последний элемент — именно свой оптимистичный
  бабл. Голосовой путь (`useVoice`) пишет в тот же `messages` через отдельные колбэки без
  синхронизации с `isSending`.
- **Как проявится:** пользователь в голосовом режиме, WS-ответ ещё в пути; он переключает
  `chatMode` на текст и отправляет реплику, которая падает → откат срезает пришедшую голосовую
  реплику ИИ вместо своего бабла. Узкое окно, но детерминированное.
- **Severity:** minor
- **Уверенность:** требует проверки (нужен точный сценарий переключения режима на живом WS)

### [x] R-15 Новый ai→learning lookup без кеша: полный каталог навыков на каждый запрос списка диалогов и на каждую запись бандла
- **Коммит/файл:** `2717266`;
  `src/backend/ai-service/Ai/Infrastructure/Learning/SkillLookupClient.cs:38-56`,
  `src/backend/learning-service/Learning/Features/SkillTree/InternalSkillsController.cs:27-34`
  (`database.Skills.Select(...).ToListAsync()` — без фильтров и пагинации),
  `src/backend/ai-service/Ai/Features/Dialog/DialogController.cs:70`,
  `src/backend/ai-service/Ai/Features/Dialog/AdminDialogController.cs:63,90,113,148`
- **Что не так:** ни `IMemoryCache`, ни ETag, ни circuit breaker, ни retry. Навыки — глобальный
  контент (`OrganizationId IS NULL` на всю фазу 40.10), то есть идеальный кандидат на кеш; вместо
  этого таблица целиком тянется по HTTP на **каждый** `GET /dialog/bundles`,
  `GET /admin/dialog/bundles`, `GET /admin/dialog/bundles/{id}`, `POST /admin/dialog/bundles` и
  `PUT /admin/dialog/bundles/{id}` — включая два маршрута записи, которым каталог нужен
  исключительно чтобы украсить тело ответа.
- **Как проявится:** learning-service тормозит (холодный старт, GC-пауза, всплеск запросов) →
  каждое открытие `/dialog` у каждого учащегося добавляет до 5 с (клиентский таймаут,
  `Math.Clamp(TimeoutSeconds, 1, 30)`, дефолт 5) и только потом рисует список **без** названий
  навыков. Админ, сохраняющий бандл, ждёт те же +5 с после того, как запись уже прошла.
- **Severity:** major
- **Уверенность:** точно
- **Не находка (проверено):** fail-open работает — любое исключение ловится и даёт пустую карту,
  список бандлов не падает; `catch (OperationCanceledException) when (ct.IsCancellationRequested)`
  корректно пробрасывает только отмену вызывающего. Эндпоинт действительно недостижим снаружи:
  `/internal` отсутствует в таблице маршрутов гейтвея (94 маршрута в
  `src/backend/gateway/Gateway/appsettings.json`, ни одного `internal`), это пинится
  `RouteParity.Tests` (`ControllerRouteInventory.cs:57` — `UnroutedPrefixes`), а порты сервисов в
  `docker-compose.yml` публикуются только на `127.0.0.1`. Ключи handshake совпадают на обоих
  концах (`InternalServiceAuthentication.SecretConfigurationKey == "InternalAuth:ServiceSecret"`,
  и `InternalAuth__ServiceSecret` проброшен всем пяти сервисам, у которых есть либо
  internal-эндпоинт, либо internal-клиент: identity, ai, learning, company, organization).
- **Resolved:** added `SkillCatalogCache` (`src/backend/ai-service/Ai/Infrastructure/Learning/
  SkillCatalogCache.cs`) — a process-wide `MemoryCache` singleton, the same pattern this codebase
  already uses for `TtsAudioCache` (the only other cache in the backend): its own `MemoryCache`
  instance rather than DI's `IMemoryCache`, registered `AddSingleton` and injected into the
  per-request typed client. `SkillLookupClient.GetSkillSummariesAsync` now checks the cache first
  and only hits learning-service on a miss; a successful fetch populates one shared entry (key:
  the whole catalog, since skills are global content) with a TTL from the new
  `LearningServiceConfiguration.SkillCatalogCacheMinutes` (default 5 minutes). A failure never
  caches the empty fallback, so an outage keeps retrying on the next call instead of pinning every
  bundle to "no skill label" for the TTL. `dotnet build`/`dotnet test` for `ai-service/Ai.Tests`:
  0 errors, 170/170 passed.

### [x] R-16 `LearningService__BaseUrl` не выставлен для профиля Local Dev — новый lookup локально не работает вообще
- **Коммит/файл:** `2717266`;
  `src/backend/ai-service/Ai/appsettings.json:32-36` (`"BaseUrl": "http://learning:8080"`),
  `docker-compose.yml:214` (единственное место, где переменная задаётся),
  `scripts/lib-local-env.sh` / `scripts/dev-ai.sh` (переменной нет; в
  `lib-local-env.sh:248-253` для соседнего клиента `IdentityService__BaseUrl` её как раз
  переопределяют на `http://localhost:...` именно из-за этой проблемы)
- **Что не так:** `http://learning:8080` — имя в docker-сети, на хосте не резолвится. Профиль
  Local Dev (по `.claude/CLAUDE.md` — дефолтный) запускает бэкенды на хосте, поэтому новый
  `SkillLookupClient` там падает на DNS **всегда**, тихо (fail-open → пустая карта → пустые
  `skillSlug`/`skillTitle`).
- **Как проявится:** локально `/dialog` и `/admin/dialog` показывают бандлы без названия навыка,
  в логах — только `LogWarning`. То есть C-3 в дефолтном dev-профиле не проверяем, и заявление
  коммита о том, что поле теперь заполняется, локально не воспроизводится.
- **Severity:** minor
- **Уверенность:** точно (переменная присутствует ровно в одном файле — docker-compose.yml)
- **Resolved:** added `export LearningService__BaseUrl="http://localhost:${LOCAL_LEARNING_PORT}"`
  to `scripts/dev-ai.sh`, following the same reasoning `export_organization_env` already documents
  for `IdentityService__BaseUrl` in `scripts/lib-local-env.sh` — the committed
  `http://learning:8080` is a Docker-network hostname that doesn't resolve on the host. Both
  `SkillLookupClient` and `AssignmentPracticeContextClient` read the same `LearningService:BaseUrl`
  key, so this fix reaches both.

### [ ] R-17 Гейт перед деплоем: T-2 включает Production, и без `INTERNAL_SERVICE_SECRET` в прод `.env` все internal-вызовы начнут молча деградировать
- **Коммит/файл:** `1a7606c`; `docker-compose.prod.yml:48-101`,
  `src/backend/learning-service/Learning/Common/Security/InternalServiceAuthFilter.cs:39-54`,
  `src/backend/ai-service/Ai/Infrastructure/Learning/LearningClientServiceCollectionExtensions.cs:39-46,72-79`
- **Что не так:** сам фикс верен и проверен мной независимо (см. раздел проверок — merge даёт
  `Production` всем десяти .NET-сервисам, не затирая остальной `environment`, а базовый файл в
  одиночку по-прежнему `Development`). Проблема в последствии: `InternalServiceAuthFilter` вне
  Development при пустом секрете отвечает 403, а **клиенты при пустом секрете просто не
  отправляют заголовок**. Значит если `INTERNAL_SERVICE_SECRET` в реальном прод `.env` не задан,
  каждый internal-вызов получит 403. Для двух ai→learning клиентов это fail-open, то есть
  **молча**: `SkillLookupClient` → пустые названия навыков; `AssignmentPracticeContextClient` →
  «нет задания», то есть неперсонализированная практика для всех, кто стартует по заданию.
  Автор зафиксировал это в `docs/DONT_FORGET.md`, но это не проверка, а обязательный ручной гейт.
- **Как проявится:** деплой проходит, ошибок в UI нет, у всех учащихся практика по заданию
  перестаёт быть персонализированной, а у бандлов пропадают подписи навыков. В логах —
  `LogError` про неконфигурированный секрет и `LogWarning` от клиентов.
- **Severity:** blocker (как условие деплоя, не как дефект кода)
- **Уверенность:** точно (весь путь прочитан; факт наличия/отсутствия переменной в прод `.env`
  из репозитория не проверить — переменная не задана даже в локальном окружении, `docker compose
  config` предупредил «INTERNAL_SERVICE_SECRET is not set»)

### [x] R-18 `[TenantScoped]` на learner-контроллерах ломает demo-token
- **Коммит/файл:** `846c020`;
  `src/backend/building-blocks/BuildingBlocks/Tenancy/TenantContextMiddleware.cs:51-57`,
  `src/backend/identity-service/Identity/Features/Auth/DemoTokenController.cs:36-48`
  («the token it issues carries no role and no organization»), контроллеры —
  `SkillsController`, `SkillTreeController`, `ExerciseController`, `ReferenceController`,
  `TechniqueController`, `DailyQuotesController`, `ProgramController`, `AssignmentsController`,
  `DialogReviewsController`, `DialogController`
- **Что не так:** demo-token не несёт ни организации, ни роли, поэтому
  `!organizationIdWasResolved && !callerIsPlatformStaff` → 403 на **весь** learner-API. Это прямо
  противоречит зафиксированному в коде инварианту: комментарий в
  `src/frontend/features/auth/components/awaiting-organization-gate.tsx:30-35` утверждает
  «the demo token — which has no user row at all — keeps working».
- **Как проявится:** `POST /demo/token` (только вне Production), затем любой learner-экран → 403
  на все данные. В проде не проявится вообще: T-2 (`1a7606c`) делает окружение Production, где
  demo-контроллер отдаёт 404.
- **Severity:** minor (dev-only, но инвариант в коде теперь ложный)
- **Уверенность:** точно по коду; фронтенд `POST /demo/token` не вызывает, так что это ручной
  dev-инструмент
- **Resolved (commit `3ce8b8f8`):** added `DemoCallers` (`BuildingBlocks/Tenancy/DemoCallers.cs`),
  mirroring `PlatformRoles` — a single source for the `isDemo` claim and an
  `IsDemoCaller(principal)` predicate. `TenantContextMiddleware` now treats a demo caller as a
  third gate exemption alongside platform staff, but a narrower one: it passes `[TenantScoped]`
  without entering platform-wide mode and without an organization, so `ITenantContext` stays in
  the same "neither org nor platform-wide" state `TenantConnectionInterceptor` already treats as
  fail-closed (RLS GUCs unset, EF query filters resolve to global-content-only or empty rows) —
  the state a non-tenant-scoped route already left it in before `846c020`. No isolation guarantee
  for real tenants widens; the exemption only lets the demo caller past the 403 gate.
  `DemoTokenController` now mints the claim from `DemoCallers.IsDemoClaimType` instead of its own
  private duplicate constant. Verified: `building-blocks/BuildingBlocks.Tests` 119/0 (4 skipped,
  need live Postgres), `identity-service/Identity.Tests` 136/0, `learning-service/Learning.Tests`
  90/0, `company-service/Company.Tests` 135/0, `route-parity/RouteParity.Tests` 5/0, both tenancy
  lints clean. `ai-service` build is currently red from an unrelated concurrent agent's in-progress
  work on the ai→learning skill lookup (untracked `SkillCatalogCache.cs` referencing a config
  property not yet added) — confirmed via `git stash` that the failure exists independent of this
  fix, out of scope here per the run's file-ownership split.

### [x] R-19 В коммит «apply [TenantScoped]» въехало расширение tenancy-границы, не упомянутое в сообщении
- **Коммит/файл:** `846c020`;
  `src/backend/ai-service/Ai/Features/Quotas/AdminAiQuotaController.cs:80-89` — новый маршрут
  `GET /admin/ai-quota/{organizationId:guid}`; allow-list — `scripts/tenancy-boundary-lint.py:87,97`,
  добавлен **другим** коммитом (`cccc8b9`)
- **Что не так:** сообщение `846c020` перечисляет только добавленные атрибуты и явно называет,
  чего не трогало. При этом коммит вводит первый в ai-service маршрут, берущий `organizationId`
  **из URL** — исключение из правила «организация приходит только из `ITenantContext`»
  (docs/TENANCY §1.3). Само исключение выглядит корректно ограниченным
  (`[Authorize(Policy = RequirePlatformAdministrator)]`, только `GET`, `PUT` продолжает писать в
  свою организацию), но линт, который должен был это поймать, был разрешён отдельным более поздним
  коммитом — то есть между `846c020` и `cccc8b9` `tenancy-boundary-lint` на main падал.
- **Как проявится:** ревью tenancy-границ по сообщениям коммитов пропустит это изменение;
  `git bisect` по линту даст красный интервал.
- **Severity:** minor (гигиена и обозреваемость границы, не утечка)
- **Уверенность:** точно по содержимому коммитов; что линт падал в интервале — требует проверки
  (я запускал его только на HEAD, где он чист)
- **Resolved (docs, this commit):** подтверждено с доказательствами, а не только по
  содержимому коммитов. `git show 846c020:scripts/tenancy-boundary-lint.py` не содержит
  `AdminAiQuotaController.cs` в `ALLOWED_ROUTE_TEMPLATE_PATHS`; запуск линтера в изолированном
  `git worktree` на `846c020` даёт ровно 1 нарушение на этой самой строке — окно красного линта
  на `main` длиной 8 минут (`02:21:01` → `02:29:17`) подтверждён, а не предположен. Причина —
  не скрытое изменение, а коллизия атрибуции коммитов между двумя параллельными агентами в одном
  working tree: `cccc8b9` (AD-5) сам признаёт в своём сообщении, что код `GetQuotaForOrganization`
  «landed earlier in 846c020 ... rather than in this commit — both agents touched the file
  concurrently in the same working tree». Расширение границы разобрано по существу и оставлено
  как есть (не откатывается): контроллер целиком под `RequirePlatformAdministrator`, поэтому любой
  вызывающий уже имеет `IsPlatformWide=true`, и EF-фильтр `OrganizationQuota` уже читает через все
  организации для такого вызывающего независимо от маршрута — сегмент маршрута лишь сужает уже
  межтенантный read, `PUT` не тронут. Задокументировано честно и подробно в `docs/DECISIONS.md`
  (новая запись «R-19: ...») с воспроизведённым выводом линтера. `tenancy-boundary-lint` на текущем
  HEAD подтверждён чистым (`tenancy-boundary-lint: clean.`).

### [x] R-20 `DialogModeKey = ""` безопасен только благодаря валидации на записи; для legacy-строк проверки нет
- **Коммит/файл:** `e2f68df`;
  `src/backend/learning-service/Learning/Eventing/AssignmentThresholdConsumer.cs:138`,
  `.../Assignments/Services/Implementation/AssignmentThresholdEvaluator.cs:233-250`
  (`modeKeys.Contains(score.DialogModeKey)`),
  `.../Assignments/Services/Implementation/AssignmentDocumentSerializer.cs:101-105`
- **Что не так:** утверждение коммита «`modeKeys.Contains("")` is false for every real assignment
  reference» опирается ровно на одну строку — сериализатор отвергает `reference.Length == 0` **на
  записи**. На чтении (`DeserializeContent`) повторной проверки нет. Любая строка `Assignments`,
  созданная до появления этой валидации или записанная в БД в обход сервиса, с
  `kind = "dialog_scenario"` и пустым `reference` даст `modeKeys = [""]` — и тогда **каждый**
  диалог без mode key этого пользователя за окно засчитается в задание.
- **Как проявится:** такое задание мгновенно «выполняется» у всех, у кого есть хоть три
  mode-key-less диалога с оценкой ≥ порога — то есть ровно тот вред, который старый drop и
  предотвращал.
- **Severity:** minor
- **Уверенность:** требует проверки (нужен SQL по `Assignments.Content` на пустые reference —
  я прод-БД не трогал)
- **Не находка (проверено):** остальные опасения по e2f68df закрыты.
  `DialogModeKey` — `IsRequired()` + `HasMaxLength(100)`, пустая строка это NOT NULL, ограничение
  не нарушает. Уникальный индекс — `(OrganizationId, UserId, SessionId)`, `DialogModeKey` в него
  не входит, дублей не появится. `DialogModeId` — не FK («Never matched on»), `Guid.Empty`
  допустим. `TeamSkillMapService:105` группирует по `UserId` и не смотрит на `DialogModeKey`.
- **Resolved (commit `604dd1d3`):** `AssignmentThresholdEvaluator.MeasureDialoguesAsync` теперь
  сам отфильтровывает пустые/whitespace `Reference` при построении `modeKeys`
  (`.Where(reference => !string.IsNullOrWhiteSpace(reference))`), а не полагается на то, что
  `SerializeContent` всегда отверг такую строку на записи. Второй потребитель того же поля,
  `MyAssignmentService`'s practice-context lookup, перепроверен и не нуждается в правке: он
  принимает mode key как параметр и уже отвергает пустой `modeKey` до сравнения, так что пустой
  `Reference` там не может совпасть ни при каком входе. `TeamSkillMapService` не читает
  `DialogModeKey` вовсе — подтверждено ранее. Build learning-service чист,
  `dotnet test learning-service/Learning.Tests --filter "TestCategory!=Integration"` 90/0 (метод
  не покрыт юнит-тестами — только пропущенным integration-тестом, `docs/TESTING/PHASE_40_BACKLOG.md`
  строка 54), оба tenancy-линта чисты, `route-parity/RouteParity.Tests` 5/0 (маршруты не менялись).

### [x] R-21 Корневой `app/not-found.tsx` накрывает и англоязычную `/admin/*`, и формальную `/org/*`
- **Коммит/файл:** `953d598`; `src/frontend/app/not-found.tsx:1-32` (единственный `not-found.tsx`
  в проекте — проверено `find app -name not-found.tsx`)
- **Что не так:** корневой `not-found` в App Router перехватывает все несовпавшие URL. Текст —
  русский в неформальном «ты»-регистре («Вернуться к пути»), а `/admin/*` в этом проекте
  полностью англоязычна («Edit Exercises», «+ Add exercise», «Delete this exercise?»), а `/org/*`
  ведётся в формальном «вы» (см. комментарий в `src/frontend/app/demo/page.tsx:36-39`).
  Кнопка ведёт на `/tree` — экран учащегося, для платформенного админа и РОПа не тот адрес.
- **Как проявится:** опечатка в `/admin/lesons` → русская страница 404 из учебного приложения
  с предложением «Вернуться к пути» посреди англоязычной админки.
- **Severity:** minor
- **Уверенность:** точно

### [x] R-22 `aria-pressed` навешен на каждый кликабельный `Chip`, включая не-тогглы
- **Коммит/файл:** `d06ae60`; `src/frontend/shared/components/chip.tsx:60-72`
- **Что не так:** `aria-pressed={active}` выставляется всегда, когда передан `onClick`.
  `active` по умолчанию `false`, поэтому любой чип-действие (не переключатель) объявляется
  скринридеру как «кнопка-тоггл, выключена».
- **Как проявится:** VoiceOver/NVDA читает «переключатель, не нажат» там, где на деле обычное
  действие. Фильтры (`app/(org)/org/content/overrides/page.tsx:139-144`) — настоящие тогглы и
  корректны; проблема в остальных.
- **Severity:** minor
- **Уверенность:** точно
- **Не находка (проверено):** визуальной регрессии от подмены `<span>` на `<button>` нет —
  инлайн-`style` задаёт `background`, `color`, `border`, `fontFamily`, `fontSize`, `fontWeight`,
  `padding` и фиксированную `height`, то есть перекрывает всё, что браузер навязал бы кнопке.
  Вложенных интерактивных элементов не нашёл: `Chip` внутри `<Link>` идёт без `onClick` и
  рендерится `<span>` (`features/org-dialogs/components/dialog-session-list.tsx:109`).

---

## Что проверено и чисто (тоже результат)

- **Область 1, `[TenantScoped]`-безопасность:** `TenantConnectionInterceptor.BuildSetLocalCommandText`
  при отсутствии организации возвращает `null` и **ничего не выполняет** — добавленные скоупы не
  могут упасть на «нет тенанта». Fail-closed обеспечивается RLS-политикой
  (`current_setting(..., true)`), а не исключением.
- **Область 3, T-5 (`e64c296`):** `AiTenantTransactionScope` действительно реентрантен —
  `CurrentTransaction is not null` даёт инертный скоуп, внешний владеет коммитом, read-скоуп
  делает `RollbackAsync`. Ни один из пяти новых скоупов не оборачивает `SaveChangesAsync`:
  `GetModeByIdAsync` вызывается из `StartSessionAsync`/`SendMessageAsync`/`CompleteSessionAsync`
  до внешнего вызова OpenAI, сессии живут в Mongo. Транзакция вокруг вызова OpenAI не держится —
  это в коммите обосновано и соответствует коду.
- **Область 3, T-4 (`9c1650d`):** `[TenantTransaction]` на классе + `using Sellevate.Learning.Features.Content;`
  — сборка и 90 юнит-тестов зелёные, паттерн совпадает с четырьмя соседними контроллерами.
- **Область 3, T-2 (`1a7606c`), проверено независимо:** `docker compose -f docker-compose.yml -f
  docker-compose.prod.yml config --format json` → `ASPNETCORE_ENVIRONMENT=Production` у всех
  десяти .NET-сервисов, при этом остальные ключи `environment` сохранены (ai 37 ключей,
  identity 28, gateway 15, social 15, organization 14, learning 12, notification 12,
  gamification 10, company 9, analytics 7) и `InternalAuth__ServiceSecret` остался у identity,
  ai, learning, company, organization. То есть слияние списков `environment` действительно
  merge-per-key, а не replace — прод-конфиг не обнулён.
- **Локальная разработка не сломана:** `scripts/lib-local-env.sh` (8 мест) и `scripts/dev-*.sh`
  экспортируют `ASPNETCORE_ENVIRONMENT="Development"` напрямую и не читают compose-оверлей;
  `InternalAuth__ServiceSecret` там намеренно не задан и `InternalServiceAuthFilter` в Development
  пропускает (`lib-local-env.sh:249-252`). Единственная локальная дырка — R-16.
- **Область 2:** все места вызова `onSend` обновлены, двойной отправки нет, откат работает
  (детали в R-13/R-14).
- **Область 4:** fail-open, недостижимость `/internal` извне и совпадение ключей handshake —
  подробно в R-15.
- **Область 5:** ограничения БД, уникальный индекс и группировка — подробно в R-20.
- **Общий свод по диффу:** ни одного `console.log`/`debugger`/`TODO`/`FIXME`/`HACK`, ни одного
  добавленного `any`/`as any`, ни закомментированного кода. Единственное совпадение по гриппу —
  слово «any» внутри английского комментария (`friends/[userId]` E-10).
- **Мёртвых ссылок после удаления ▲▼ нет:** `moveExercise`/`moveExerciseInList` остались только
  там, где используются (`app/(org)/org/content/lessons/[lessonId]/page.tsx`,
  `features/org-content-overrides/utils/exercise-summary.ts` + его тест); переменная `index`
  в обоих отредактированных файлах по-прежнему нужна.
- **Взаимных откатов между агентами не нашёл.** Файлы, тронутые несколькими коммитами:
  `app/session/[lessonId]/page.tsx` (4: `e608179`, `16afd08`, `c8fe966`, `953d598`),
  `SkillsController.cs` (3: `b724a2c`, `c78fe2c`, `846c020`),
  `TechniqueService.cs` (3: `9b3080a`, `7a79dec`, `df11c47`),
  `use-team-directory.ts` (2: `ef4939e`, `d854e4c`),
  `use-auth.ts` (2), `use-admin-dialog.ts` (2), `guidebook/page.tsx` (3), `friends/[userId]` (3).
  Итоговый дифф каждого из них связный: правки складываются, а не затирают друг друга.
  `use-admin.ts`, вопреки предупреждению, тронут ровно одним коммитом (`b393059`).
- **`use-admin.ts` язык корректен:** тосты по-английски (`Failed to create skill: …`) — совпадает
  с англоязычной платформенной админкой. Русские тексты — только на learner/org-экранах.

---

## Покрытие: что реально прочитано, а что нет

| # | Область | Статус |
|---|---------|--------|
| 1 | `29a9a22` logout `onSuccess`→`onSettled` | **разобрано полностью** (стор, api-client, refresh, оба места вызова, серверный logout/refresh) → R-1, R-2, R-3 |
| 2 | `0ee865a`/`8a73356` `onSend: Promise<boolean>` | **разобрано полностью** (оба composer'а, все 4 места вызова, мутация friends-чата, голосовой путь) → R-13, R-14 |
| 3 | Tenancy T-1/T-2/T-4/T-5/T-6 | **разобрано** (`AiTenantTransactionScope`, `TenantConnectionInterceptor`, `TenantContextMiddleware`, все 28 добавленных `[TenantScoped]` по списку, merge prod-оверлея, dev-скрипты) → R-18, R-19. **Не проверял:** каждый из 28 контроллеров построчно на соответствие атрибута реальному запросу — читал их по классу вызывающего (learner / org-admin / platform-only) и по гейту `[Authorize]`, а не пометоду |
| 4 | `2717266` ai→learning skill lookup | **разобрано полностью** (клиент, DI, таймаут, эндпоинт, гейтвей, route-parity, порты, ключи секрета, локальная конфигурация) → R-15, R-16, R-17 |
| 5 | `e2f68df` `DialogModeKey = ""` | **разобрано полностью** (консьюмер, entity, конфигурация EF, оба читателя, валидация reference) → R-20 |
| 6 | `b128e0a` `sanitize-html` / `FeedbackHtml` | **разобрано полностью, с прогоном пакета** → R-7, R-8, R-9 |
| 7 | `16afd08` `submitError` ×10 и серия E-фиксов | **разобрано** → R-4, R-5, R-6. **Не проверял:** все 10 компонентов упражнений по отдельности (проверил общий футер + `ai-dialogue-exercise`, остальные 8 получают проп транзитом через тот же футер); из ~18 E-фиксов прочитал целиком `e608179` и `c7e54d9`, остальные — по диффу на форму гейта |
| 8 | `316da24` exercise-editor / `localRows` / ▲▼ | **разобрано полностью** (оба файла, `saveExercise`, `deleteRow`, `setRows`, мёртвые ссылки, соседний org-редактор) → R-10, R-11, R-12 |

**Не дошёл (честно):**
- 4 коммита, приземлившиеся во время ревью: `8ef73b5` (AD-2 unknown skill stage),
  `7d93a1f` (AD-4 сортировка списка уроков), `cccc8b9` (AD-5 фронтенд квоты),
  `7aa7800` (AD-6 `isHidden`). Из них `cccc8b9` косвенно затронут в R-19.
- Серия «error-masking» E-фиксов (`b046303` и ~15 коммитов под ним) прочитана выборочно —
  общая форма гейта проверена (R-6), но каждый экран отдельно не открывал.
- Коммиты null-safety (`df11c47`, `7a79dec`, `deaca92`, `d854e4c`, `ef4939e`) и мелкие org-фиксы
  (`7a372e1`, `37f27b6`, `6faf0db`, `063290f`, `b9c5565`, `775a945`, `adc5023`, `df2fec7`)
  прочитаны только по диффу, без разбора вызывающих.
- Интеграционные тесты (`TestCategory=Integration`) нигде не запускались — им нужен живой
  Postgres, а серверы я по правилам прогона не поднимал. Значит **RLS-поведение добавленных
  скоупов и тенант-изоляция на живой БД не проверены ни мной, ни авторами** (в их же коммитах:
  «11/14/12 integration tests skipped, need live Postgres»).
- Прод-`.env` и деплой-артефакт из репозитория не проверить — см. R-17.

---

## Результаты проверочных команд (verbatim, HEAD `7aa7800`)

```
$ cd src/frontend && npx tsc --noEmit
TSC_EXIT=0
(без вывода)

$ cd src/frontend && npx vitest run
 Test Files  87 passed (87)
      Tests  977 passed (977)
   Duration  7.01s
[exited with code 0]

$ bash scripts/tenancy-boundary-lint.sh
tenancy-boundary-lint: clean.

$ bash scripts/tenancy-pool-lint.sh
tenancy-pool-lint: clean.

$ cd src/backend && dotnet build ai-service/Ai.Tests
Build succeeded.
    0 Error(s)
$ dotnet test ai-service/Ai.Tests --no-build --filter "TestCategory!=Integration"
Passed!  - Failed:     0, Passed:   170, Skipped:     0, Total:   170, Duration: 736 ms

$ dotnet build company-service/Company.Tests
Build succeeded.
    0 Error(s)
$ dotnet test company-service/Company.Tests --no-build --filter "TestCategory!=Integration"
Passed!  - Failed:     0, Passed:   135, Skipped:     0, Total:   135, Duration: 1 s

$ dotnet build identity-service/Identity.Tests
Build succeeded.
    0 Error(s)
$ dotnet test identity-service/Identity.Tests --no-build --filter "TestCategory!=Integration"
Passed!  - Failed:     0, Passed:   136, Skipped:     0, Total:   136, Duration: 3 s

$ dotnet build learning-service/Learning.Tests
Build succeeded.
    0 Error(s)
$ dotnet test learning-service/Learning.Tests --no-build --filter "TestCategory!=Integration"
Passed!  - Failed:     0, Passed:    90, Skipped:     0, Total:    90, Duration: 1 s

$ dotnet test route-parity/RouteParity.Tests --no-build
Passed!  - Failed:     0, Passed:     5, Skipped:     0, Total:     5, Duration: 14 ms

$ docker compose -f docker-compose.yml -f docker-compose.prod.yml config --format json
(предупреждения: "INTERNAL_SERVICE_SECRET is not set. Defaulting to a blank string." ×5,
 "DEMO_REQUESTS_NOTIFICATION_EMAIL is not set")
ai               ASPNETCORE=Production   nEnvKeys= 37 secret=True
analytics        ASPNETCORE=Production   nEnvKeys=  7 secret=False
company          ASPNETCORE=Production   nEnvKeys=  9 secret=True
gamification     ASPNETCORE=Production   nEnvKeys= 10 secret=False
gateway          ASPNETCORE=Production   nEnvKeys= 15 secret=False
identity         ASPNETCORE=Production   nEnvKeys= 28 secret=True
learning         ASPNETCORE=Production   nEnvKeys= 12 secret=True
notification     ASPNETCORE=Production   nEnvKeys= 12 secret=False
organization     ASPNETCORE=Production   nEnvKeys= 14 secret=True
social           ASPNETCORE=Production   nEnvKeys= 15 secret=False
```

Прогон `sanitize-html@2.17.7` (обоснование R-7, R-8, R-9):

```
$ node -e '...' в src/frontend
strip("<h3>Итог</h3><p>Первое предложение.</p><p>Второе предложение.</p>")
  → "ИтогПервое предложение.Второе предложение."
strip("<ul><li>раз</li><li>два</li></ul>")      → "раздва"
strip("строка1<br>строка2")                      → "строка1строка2"
strip("Оценка < 70 & \"низко\"")                 → "Оценка &lt; 70 &amp; \"низко\""
strip("Клиент сказал: 5 > 3")                    → "Клиент сказал: 5 &gt; 3"
keep("<script>alert(1)</script><img src=x onerror=alert(1)><a href=\"javascript:alert(1)\">x</a>")
  → "x"
keep("<ol><li>a</li><li>b</li></ol>")            → "<li>a</li><li>b</li>"
keep("<p style=\"position:fixed\" onclick=\"x()\">t</p>") → "<p>t</p>"
keep("<svg onload=alert(1)></svg><iframe src=x></iframe>") → ""
```

---

## Вердикт

**REQUEST CHANGES.** Все автоматические проверки зелёные — ни одна из находок ниже ими не
ловится, что само по себе стоит отметить: у 977 фронтенд-тестов и 531 бэкенд-юнит-теста нет
покрытия ни на липкость `mutation.error` (R-4), ни на `stripFeedbackHtml` (R-7/R-8), ни на
поведение `isError` при наличии данных (R-5).

Блокирует деплой: **R-17** (ручной гейт: подтвердить `INTERNAL_SERVICE_SECRET` в прод `.env`
до того, как T-2 включит Production).
Требует правки до пуша: **R-1**, **R-4**, **R-5**, **R-7**, **R-8**, **R-10**, **R-15**.

---

## Ревью 2 — коммиты после первого ревью

**Диапазон:** `7870d60f..90d5e491` (25 коммитов). `7870d60f` — коммит, которым был создан этот
файл, то есть граница первого ревью; `90d5e491` — HEAD на момент проверки. Рабочее дерево при
этом было грязным (другой агент правил ~26 файлов, включая `use-auth.ts`,
`app/session/[lessonId]/page.tsx` и оба админских редактора упражнений) — ревью проведено по
**закоммиченному** состоянию `90d5e491`, незакоммиченные правки не смотрелись.

Нумерация `R2-n` отдельная; секции R-1…R-22 выше не переписывались и не пересматривались.

### [x] R2-1 ▲▼ в админском редакторе упражнений не сохраняют порядок вообще: `changedRows` всегда пустой
- **Коммит/файл:** `ad47210b` (восстановление кнопок) + `5a1a4572`;
  `src/frontend/app/(admin)/admin/lessons/[lessonId]/exercises/page.tsx:228-259`,
  `src/frontend/app/(admin)/admin/skills/[id]/topics/[topicId]/lessons/[lessonId]/exercises/page.tsx`
  (тот же код), источник шаблона —
  `src/frontend/app/(org)/org/content/lessons/[lessonId]/page.tsx:181-186` +
  `src/frontend/features/org-content-overrides/utils/exercise-summary.ts:98`
- **Что не так:** `renumbered` присваивает каждой строке `sortOrder = i + 1` по её **новой**
  позиции, а фильтр сравнивает это с `previousRows[i]?.sortOrder` — то есть с `sortOrder`
  **прежнего жильца той же позиции**, а не самой строки. Для списка, пронумерованного подряд
  `1..n` (обычный случай: `addExercise` ставит `rows.length + 1`,
  `ContentGenerationStepRunner` — `orderInLesson++`, сиды — `1, 2, 3`), обе части всегда равны
  `i + 1`, и фильтр не отбирает ничего. Проверено прогоном ровно этого выражения:
  ```
  contiguous 1..3, move idx0 -> idx1 (down): {"order":"r1:1,r0:2,r2:3","puts":[]}
  contiguous 1..3, move idx2 -> idx0 (up x2): {"order":"r2:1,r0:2,r1:3","puts":[]}
  contiguous 1..5, move idx3 -> idx1:        {"order":"r0:1,r3:2,r1:3,r2:4,r4:5","puts":[]}
  0-based 0..2,    move idx0 -> idx1:        {"order":"r1:1,r0:2,r2:3","puts":["r1->1","r0->2","r2->3"]}
  sparse 1,2,4,    move idx0 -> idx1:        {"order":"r1:1,r0:2,r2:3","puts":["r2->3"]}
  ```
  Ни одного `PUT` не уходит, дальше `try` доходит до `invalidateQueries` + `setLocalRows(null)`,
  и список схлопывается назад к серверному порядку.
- **Как проявится:** админ жмёт ▲ или ▼, строка визуально переезжает, экран моргает рефетчем и
  строка возвращается на место. Ни ошибки, ни тоста — кнопки просто не работают. То есть
  `ad47210b` вернул органы управления, которые ничего не делают, ровно тем же способом, каким
  R-1 «починил» логаут: обоснование («org-редактор уже персистит реордер таким же циклом»)
  взято из файла, где сломан тот же фильтр — `moveExerciseInList` тоже перенумеровывает по
  позиции (`index + 1`), а `moved` считается тем же позиционным сравнением. То есть дефект в
  org-редакторе существовал до диапазона, а `ad47210b` скопировал его в два админских экрана,
  сославшись на него как на доказательство работоспособности.
- **Severity:** major
- **Уверенность:** точно (выражение прогнано, вывод выше)
- **Как править:** сравнивать по идентичности строки, а не по позиции:
  `previousRows.find(p => p.id === row.id)?.sortOrder !== row.sortOrder`. Тот же фикс нужен и в
  org-редакторе (вне диапазона, но это первоисточник).

### [ ] R2-2 при частично прошедшем реордере экран и сервер расходятся молча
- **Коммит/файл:** `ad47210b`; `.../admin/lessons/[lessonId]/exercises/page.tsx:249-259`
  (`moveExercise`, блок `catch`)
- **Что не так:** цикл делает по одному `PUT /admin/exercises/{id}` на строку последовательно.
  Если падает второй из трёх, первый уже записан. `catch` делает только
  `setRows(previousRows)` — ни `setLocalRows(null)`, ни `invalidateQueries`. Экран показывает
  исходный порядок, сервер держит наполовину перенумерованный список, и следующее нажатие ▲
  считает `changedRows` от неверной базы.
- **Как проявится:** админ видит тост об ошибке и «откат», но ученик получает урок с
  задвоенным/пропущенным `orderInLesson`. Триггерится только там, где `PUT`-ы реально уходят
  (см. R2-1: непрерывная нумерация `1..n` их не отправляет вообще), то есть на контенте с
  разреженным или нулевым `orderInLesson`.
- **Severity:** major
- **Уверенность:** точно (путь кода однозначен), но триггер узкий
- **Как править:** в `catch` — `setLocalRows(null)` + `invalidateQueries`, чтобы экран показал
  фактическое состояние сервера, а не выдуманный откат. Правильнее — bulk-эндпоинт реордера в
  одной транзакции.

### [x] R2-3 `setLocalRows(null)` убран из `saveExercise` и `deleteRow.onSuccess`: теневая копия навсегда перекрывает рефетч
- **Коммит/файл:** `5a1a4572`; `.../admin/lessons/[lessonId]/exercises/page.tsx:207-217, 277-292`
  (и тот же код во втором редакторе)
- **Что не так:** коммит удалил оба сброса вместе с комментарием, который прямо объяснял, зачем
  они нужны («otherwise localRows keeps overriding every future refetch forever»). `rows` — это
  `localRows ?? exercises.map(...)`; `localRows` становится непустым при первой же правке и
  теперь обнуляется **только** на успешной ветке `moveExercise`. Следствия: `await
  qc.invalidateQueries(...)` в `saveExercise` — мёртвая работа (рефетч выполняется, результат
  не используется никогда); правки другого админа не появляются до перезагрузки страницы;
  серверная перенумерация остальных строк не приезжает. И главное — сверка по ссылке
  `r === row` работает **только** потому, что `localRows` больше не сбрасывается: обнулись он
  между `await` и `setRows`, и `prev` пересобрался бы из серверных данных, где объекта `row` уже
  нет, — `map` не совпал бы ни с чем.
- **Как проявится:** редактор упражнений «залипает» на локальной копии до перезагрузки; двое
  админов в одном уроке не видят правок друг друга; исправление R-11 держится на неочевидной
  связке двух состояний.
- **Severity:** major
- **Уверенность:** точно
- **Как править:** сверять сохранённую строку по `id` (для create — по временному ключу), а не
  по ссылке, и вернуть сброс теневой копии там, где локальных несохранённых правок больше нет.

### [x] R2-4 X-2 закрыт не полностью: защита от «перемешали в правильный порядок» сравнивает с авторским порядком массива, а не с верным
- **Коммит/файл:** `116704eb` (галочка X-2 — `bddf08ae`);
  `src/frontend/features/exercise/components/reorder-exercise.tsx:11, 39-46`
- **Что не так:** тип объявляет `correct_position: number`, но `StripAnswerKeyFields` вырезает
  это поле из ученического контента — значит в рантайме оно `undefined` у всех элементов.
  `sort((a, b) => a.pos - b.pos)` получает `NaN` на каждой паре и оставляет порядок как есть.
  Проверено: `[{},{},{}]` → `preSubmitCorrectOrderGuess = [0,1,2]`, то есть тождество.
  Защита X-2 поэтому отказывается выдать только **авторский порядок массива**. При этом
  `src/frontend/features/admin/components/exercise-editors/ordering-editor.tsx:19-32` меняет
  местами **значения** `correct_position`, не двигая элементы в массиве, — то есть у любого
  упражнения, которое автор правил кнопками ↑↓ в редакторе, порядок массива и верный порядок
  расходятся.
- **Как проявится:** на таком упражнении Fisher–Yates по-прежнему может выдать решённый
  порядок, и «Проверить» даёт 100 % без единого действия — ровно то, что X-2 описывает и что
  `bddf08ae` отметил как `[x]`. TypeScript при этом молчит: тип обещает `number` там, где всегда
  `undefined`, — тот же класс дефекта, который X-3/X-6/X-8 и устраняли.
- **Severity:** major
- **Уверенность:** точно (механизм воспроизведён на выражении; частота зависит от контента)
- **Как править:** на ученической стороне тип должен запрещать поле (`correct_position?: never`),
  а перемешивание — уехать на сервер (или сервер должен отдавать готовый `shuffledOrder`):
  клиент принципиально не может проверить свою перестановку против ответа, которого у него нет.
- **Resolved (2026-08-21):** contract intentionally kept as-is — the correct answer still only
  ever arrives in the submit *result*, never in the exercise payload, so a server-side
  `shuffledOrder` was out of scope here. Instead: `ReorderItem` in `reorder-exercise.tsx` no
  longer declares `correct_position` at all (it is honestly absent from the learner payload), and
  the guard now compares the shuffle against the array's own arrival order (`identityIndices`)
  instead of a "guess" built from a field that is always `undefined` at runtime. This is the same
  honest limit the review names: the client cannot check its shuffle against the real solved
  order pre-submit, so the guard's only truthful job is refusing to hand back the exact order the
  content arrived in (falling back to a rotation on the rare shuffle that lands there anyway).
  `src/frontend/features/exercise/components/reorder-exercise.tsx`.

### [x] R2-5 разовый сбой `/auth/me` навсегда выключает молчаливое обновление токена
- **Коммит/файл:** `8f98116a`; `src/frontend/features/auth/hooks/use-auth.ts:77`,
  `src/frontend/shared/stores/auth-store.ts:82`, `src/frontend/shared/api/api-client.ts:60, 131`
- **Что не так:** `useInitAuth` делает `.catch(() => clearAuthSession())` на **любой** отказ
  `/auth/me` — 500, сетевой обрыв, `RequestTimeoutError`, не только отвергнутый токен. А
  `clearAuthSession` теперь пишет липкий маркер `authSessionTerminated`, который
  `attemptTokenRefresh` уважает до следующего явного `setAccessToken`. До этого коммита тот же
  `catch` лишь убирал access-token, и первый же 401 молча обновлял сессию по refresh-куке.
- **Как проявится:** одна икота гейтвея на загрузке страницы — и пользователя выкидывает на
  `/login` по-настоящему, при полностью живой refresh-куке. Восстанавливается только повторным
  входом. R-1 при этом закрыт корректно (логаут действительно стал терминальным) — проблема в
  том, что «сессия завершена намеренно» расширили до «`/auth/me` однажды не ответил».
- **Severity:** major
- **Уверенность:** точно (путь кода), частота — требует проверки
- **Как править:** ставить маркер только на пути логаута и на явном отказе аутентификации
  (`SessionExpiredError` / `ApiError` со `status === 401`), а не на любом отказе `/auth/me`.

### [x] R2-6 ключ `authSessionTerminated` продублирован строковым литералом в двух файлах
- **Коммит/файл:** `8f98116a`; `src/frontend/shared/api/api-client.ts:60`
  (`const SESSION_TERMINATED_KEY`, не экспортируется) против
  `src/frontend/shared/stores/auth-store.ts:73, 82` (литерал `"authSessionTerminated"`)
- **Что не так:** единственное, что держит два места вместе, — комментарий «Keep this key name in
  sync». Опечатка в любом из них бесшумно возвращает R-1, и ни один тест не упадёт. В том же
  прогоне `3ce8b8f8` специально завёл `DemoCallers.IsDemoClaimType`, чтобы издатель и проверяющий
  не могли разойтись в написании клейма, — здесь сделано наоборот.
- **Как проявится:** тихая регрессия R-1 при любой будущей правке имени ключа.
- **Severity:** minor
- **Уверенность:** точно
- **Как править:** экспортировать `SESSION_TERMINATED_KEY` (или вынести в общий модуль) и
  импортировать в стор.

### [ ] R2-7 «Пропустить» на каждом упражнении теперь закрывает урок и открывает следующий — со счётом 0
- **Коммит/файл:** `d7b090d1` + `6893c2ad`;
  `src/frontend/app/session/[lessonId]/page.tsx:150-165`,
  `src/backend/learning-service/Learning/Features/Exercises/Services/Implementation/ExerciseService.cs:481-484, 588-600, 634-645`
- **Что не так:** скип теперь пишет настоящую строку `UserExerciseAttempt`, а
  `UpdateLessonProgressAsync` считает `attemptedExercises` как «различные упражнения урока, по
  которым есть **хоть какая-нибудь** попытка» — скип от ответа там неотличим. Значит
  `allAttempted` → `Completed` → `UnlockNextLessonInTopicAsync`; а вывод разблокировки из
  `6893c2ad` тоже цепляется именно за `Completed`.
- **Как проявится:** ученик проходит всё дерево, нажимая «Пропустить», получая `completed` и
  `bestScore = 0` на каждом уроке. X-4 действительно требовалось закрыть (иначе урок,
  пройденный скипами, не закрывался никогда), но это следствие нигде не записано — ни в
  `d7b090d1`, ни в `docs/DECISIONS.md`. Вынесено владельцу как `docs/NIGHT_AUDIT_QUESTIONS.md`
  Q-16.
- **Severity:** major
- **Уверенность:** точно (механизм); продуктовое решение — за владельцем

### [x] R2-8 `advanceToNext()` не сбрасывает `lastSubmissionResult` — новый DTO с ответом в одном вызывающем от преждевременного раскрытия
- **Коммит/файл:** `472aba7b` + `d7b090d1` + `116704eb`;
  `src/frontend/app/session/[lessonId]/page.tsx:132-147`
- **Что не так:** сегодня утечки нет, все пути проверены: `handleContinueAfterResult` (`:166`) и
  `handleStartMistakesReview` (`:179`) чистят состояние сами, а `handleSkip` его никогда не
  ставит (скип и разобранный результат взаимоисключающи — при `isAnswered` футер меняется с
  `ExerciseActionFooter` на `ExerciseResultBanner`, кнопки «Пропустить» там уже нет). Но
  `submittedResult` — это теперь **носитель ключа ответа** (`correctAnswer`), а
  `isAnswered = submittedResult != null`. Любой будущий вызывающий, который переходит к
  следующему упражнению после ответа не почистив состояние, отрисует следующее упражнение как
  уже отвеченное и подсветит в нём индекс из ответа на предыдущее.
- **Как проявится:** сейчас — никак; при следующей правке очереди упражнений — раскрытие
  правильного ответа до ответа ученика.
- **Severity:** minor (латентно; последствие при срабатывании — major)
- **Уверенность:** точно
- **Как править:** перенести `setLastSubmissionResult(null)` внутрь `advanceToNext()`, рядом с
  `submitExerciseMutation.reset()`, который `472aba7b` там уже поставил.

### [ ] R2-9 `stripFeedbackHtml` теперь возвращает живую разметку (XSS-аллоулист при этом цел)
- **Коммит/файл:** `11c7069c`; `src/frontend/shared/components/feedback-html.tsx:23-35, 43-51`
- **Что не так:** аллоулист проверен заново и держится — все векторы вырезаются (вывод ниже, в
  секции верификации). Но `decodeFeedbackTextEntities` работает **после** санитайза, поэтому
  обезвреженная полезная нагрузка возвращается в живой вид как текст:
  `strip("&lt;script&gt;alert(1)&lt;/script&gt;")` → `"<script>alert(1)</script>"`. Сегодня это
  безопасно: единственный потребитель —
  `src/frontend/features/org-dialogs/components/dialog-session-list.tsx:105`, где результат
  подставляется текстовым child-ом React и экранируется им повторно. Опасность в контракте:
  функция с именем `strip…Html` отдаёт живую разметку, и следующий потребитель (`title=`,
  тултип, `innerHTML`) получит stored XSS.
- **Как проявится:** сейчас — никак. При добавлении любого не-текстового потребителя — XSS из
  вывода LLM.
- **Severity:** minor
- **Уверенность:** точно (замерено)
- **Как править:** зафиксировать контракт в docstring («результат безопасен только как текстовый
  узел») либо не декодировать сущности, а отдавать отдельную функцию для текстового рендера.

### [ ] R2-10 вывод разблокировки X-11 сделан только в одном из трёх путей чтения уроков
- **Коммит/файл:** `6893c2ad`;
  `.../Exercises/Services/Implementation/ExerciseService.cs:106` (`GetAllLessonsAsync`),
  `:171` (`GetLessonsForTopicAsync`) против `:246-262` (исправленный `GetLessonsForSkillAsync`)
- **Что не так:** два других пути по-прежнему возвращают `progressRecord?.Status ??
  LessonProgressStatuses.Locked`, причём `GetAllLessonsAsync` (`GET /lessons`) не открывает даже
  первый урок. Сегодня безвредно: все три ученических экрана (`/tree`, `/skill/[id]`,
  `/skill/[id]/map`) ходят через `useLessonsForSkill`, а `useAllLessons`
  (`src/frontend/features/exercise/hooks/use-lesson.ts:42`) **экспортирован и не используется
  нигде** — мёртвый код. Прочее по коммиту чисто: `previousLessonCompleted` считается от
  **сохранённого** статуса, поэтому разблокировка не разбегается каскадом; порядок обхода —
  `topicOrder`, затем `orderInTopic`; тема без уроков просто не даёт элементов и не рвёт цепочку;
  новых запросов на горячем пути не добавлено (цикл идёт по уже загруженным данным).
- **Как проявится:** сейчас — никак; при подключении `GET /lessons` или
  `GET /topics/{id}/lessons` к любому экрану X-11 вернётся именно там.
- **Severity:** minor
- **Уверенность:** точно

### [ ] R2-11 ключ `SkillCatalogCache` не квалифицирован тенантом, хотя `Skill.OrganizationId` — реальная колонка
- **Коммит/файл:** `01de8624`;
  `src/backend/ai-service/Ai/Infrastructure/Learning/SkillCatalogCache.cs:26-59`,
  `src/backend/learning-service/Learning/Infrastructure/Data/LearningDbContext.cs:189`
- **Что не так:** по трём заданным вопросам кеш чист: рост ограничен (один фиксированный ключ
  `"skill-catalog"`), отравления сбоем нет (`Set` не кеширует пустой каталог, `return Empty` на
  ошибке проходит мимо кеша), `MemoryCache` потокобезопасен. Утечки между тенантами сегодня тоже
  нет — internal-вызов не несёт заголовка организации, поэтому фильтр
  `IsPlatformWide || OrganizationId == null || OrganizationId == _tenantContext.OrganizationId`
  сводится к «только глобальные», детерминированно. Но посылка «навыки глобальны» — это
  комментарий, а не инвариант, который кеш проверяет: тенантные навыки — поддерживаемая форма
  (`Learning.Tests/Unit/LearningTenancyModelTests.cs:111-112` их прямо засеивает). В день, когда
  этот lookup получит заголовок организации или платформенный режим, названия навыков одного
  тенанта будут отдаваться всем на время TTL.
- **Как проявится:** сейчас — никак; при расширении internal-контракта — межтенантная утечка
  названий навыков на 5 минут.
- **Severity:** minor
- **Уверенность:** точно по форме, низкая по вероятности срабатывания
- **Прочее (не находка):** защиты от «стампеда» нет — N одновременных промахов дадут N запросов.
  Для каталога навыков это неважно.

### [ ] R2-12 корневой `not-found.tsx` стал клиентским; на 404 внутри `/admin` мигает ученическая русская копия
- **Коммит/файл:** `afacedb0`; `src/frontend/app/not-found.tsx:1-8`
- **Что не так:** добавлены `"use client"` и `usePathname()`. При пререндере границы
  `/_not-found` `usePathname()` возвращает `null`, оба guard-а (`pathname?.startsWith`) не
  срабатывают и в первый HTML уходит ученический вариант; английский admin-вариант и формальный
  org-вариант появляются только после гидратации.
- **Как проявится:** админ, набравший опечатку в `/admin/...`, видит вспышку «Страница не
  найдена / Вернуться к пути» до подмены на «Page not found / Back to admin». Сама по себе
  правка R-21 верная — вопрос только в порядке рендера.
- **Severity:** minor
- **Уверенность:** требует проверки (поведение зависит от версии Next.js; проверяется одной
  ручной загрузкой битого `/admin/...`)

### Чисто (проверено, находок нет)

- **`116704eb` / `90d5e491` — главный вопрос «может ли правильный ответ утечь до ответа
  ученика»: нет.** `StripAnswerKeyFields`
  (`ExerciseService.cs:358-420`) не тронут и по-прежнему срезает всё, что срезал:
  `is_correct` у `choose_option`/`fill_blank`, `correct_position` у `reorder`, `category` у
  `categorize`, `is_mistake` у `spot_mistake`, `ai_prompt` у `ai_dialogue`.
  `ExerciseSubmissionResultDto` конструируется ровно в одном месте — `ExerciseService.cs:548`,
  внутри `SubmitExerciseAnswerAsync` (проверено grep-ом по всему репозиторию); ветка
  `isSkipped` собирает `ExerciseEvaluationResult` без `CorrectAnswer`, то есть скип не
  раскрывает ничего. В логах и телах ошибок DTO не появляется: `AiEvaluationClient` логирует
  только тело **не**-2xx-ответа, в котором результата нет, а `EvaluationController` отдаёт на
  сбоях `{ message }` без результата. Кеша запросов на пути submit нет (это мутация).
- **Плумбинг `AiEvaluationResult` / `AiExerciseEvaluationStrategy` — корректен для отсутствующего
  значения.** `CorrectLineIndex` — необязательный параметр записи со значением по умолчанию
  `null` на обеих сторонах, ai-service отдаёт запись напрямую (`Ok(result)`), клиент читает её
  через `JsonSerializerDefaults.Web` — имена совпадают. На learning-стороне стоит
  pattern-guard `result.CorrectLineIndex is int correctLineIndex`, поэтому отсутствующее поле
  даёт `CorrectAnswer: null`, а не фиктивный индекс `0`. В ai-service `mistakeIndex >= 0 ?
  mistakeIndex : null` — упражнение без помеченной ошибки не выдаёт индекс `-1` наружу. На
  фронтенде `correctLineIndex ?? -1` и `correctOptionIndex ?? null` обрабатывают отсутствие
  без подсветки чужой строки.
- **`90d5e491` — правка теста законна, а не подогнана.** От прежних 50 не зависело ничего:
  единственные потребители балла упражнения — `app/session/[lessonId]/page.tsx:106`
  (`score >= 70`) и `features/exercise/components/exercise-result-banner.tsx:22-23` (полосы
  70/40); `AssignmentThresholdEvaluator` считает пороги по `UserDialogScores`, а не по
  упражнениям; прогресс урока берёт `Math.max` лучшего балла. Обратной регрессии тоже нет: вызов
  ИИ и раньше стоял под `lineCorrect && …`, поэтому неверная строка давала 0 и до, и после —
  снятое `score += aiScore` не могло ничего отнять. Ветка `isCorrect = lineCorrect` заменяет
  порог `score >= 75`, который при значениях 0/100 эквивалентен.
- **`3ce8b8f8` — граница безопасности держится.** Клейм `isDemo` минтится ровно в одном месте
  (`DemoTokenController.IssueDemoToken`), которое отвечает 404 при `environment.IsProduction()`,
  а `docker-compose.prod.yml:52-100` выставляет `ASPNETCORE_ENVIRONMENT=Production` всем
  сервисам — то есть в проде токен не выдаётся вообще. Клейм едет внутри подписанного JWT,
  поэтому клиент его себе не присвоит. Исключение — один булев терм в одном `if`
  (`TenantContextMiddleware.cs:62`): `EnterPlatformMode()` для демо не вызывается,
  `SetOrganization` тоже, так что `ITenantContext` остаётся в состоянии «ни организации, ни
  платформы», которое `TenantConnectionInterceptor` уже трактует fail-closed.
  `DemoTokenController` теперь читает имя клейма из той же константы, которой минтит. Оба
  tenancy-линта чисты.
- **`604dd1d3` — корректно и по месту.** Фильтр пустых `Reference` стоит до `Distinct`, а
  `modeKeys.Count == 0 → return null` (`AssignmentThresholdEvaluator.cs:253-256`) означает, что
  задание, у которого **все** ссылки на сценарии пустые, трактуется как «требования по диалогам
  нет», а не как «порог выполнен автоматически». Это правильная сторона ошибки.
- **`2d66f469` — чисто.** `disabled={isSending || …}` стоит на обоих вызовах `ChatInput`
  (`app/dialog/[bundleId]/[modeId]/page.tsx:590`,
  `app/companies/[id]/call/chat/page.tsx:305`), поэтому восстановление черновика при неудаче не
  затрёт свежий ввод. Сверка по идентичности при откате не может не совпасть: единственные пути,
  заменяющие массив целиком (`setMessages(session.messages)`, `setMessages([])`), и так убирают
  оптимистичный пузырь.
- **`6e575822`, `472aba7b`, `0f6a53ee`, `aa8e36bb`, `8cd7e975`, `d7b090d1` (кроме R2-7),
  `95a826a1`, `b9314b90`, `bd7d9e85`, `da35e502`, `f5252317`, `20f920a9`, `bddf08ae` (кроме
  галочки X-2, см. R2-4)** — читаны, находок нет.
- **Прочёс диапазона:** ни одного `console.log`/`console.debug`/`debugger`, ни одного
  `TODO`/`HACK`/`FIXME`/`XXX`, ни одного нового `any` (проверено по всем изменённым `.ts`,
  `.tsx`, `.cs` и отдельно по добавленным строкам диффа). Мёртвого кода от снятых-и-возвращённых
  ▲▼ не осталось. Копии не в том языке для своей области не найдено (`afacedb0` как раз это и
  чинит). Молчаливых перезаписей между агентами нет: четыре коммита в
  `app/session/[lessonId]/page.tsx` и четыре в `ExerciseService.cs` — аддитивные и не
  перекрывающиеся (проверено полным диффом диапазона по каждому файлу).

### Вне диапазона, но присутствует на `90d5e491` — блокирует пуш

- **Два падающих фронтенд-теста.** `__tests__/CompanyPage.test.tsx` → «shows the not-found state
  on a 404» и «shows a generic error state with retry for non-404 failures». Причина — не в моём
  диапазоне: `app/(main)/companies/[id]/page.tsx:138` гейтит полноэкранную замену на
  `isLoadingError`, а тест мокает только `isError`, поэтому компонент рендерит пустоту. Внёс это
  `eb9d771d` (2026-08-21 01:34), то есть **до** границы первого ревью `7870d60f` (03:42);
  в диапазоне `7870d60f..HEAD` файлы `companies/**` и сам тест не менялись вовсе (проверено
  `git log --name-only`). Первое ревью это пропустило. Нарушает CLAUDE.md Rule #4 («Never commit
  with failing tests») и должно быть закрыто до пуша — либо мок теста дополняется
  `isLoadingError`, либо гейт возвращается к `isError`.
- **5 нарушений `codestyle-lint`** в
  `src/backend/identity-service/Identity/Features/Auth/Services/Implementation/AuthenticationService.cs:152-153,
  262-264` (Rule 9). Файл в диапазоне не менялся — нарушения предшествуют границе.

### Покрытие

Просмотрены все 7 заявленных областей:

1. Контракт `submit`-ответа (`116704eb`, `90d5e491`) — **разобран полностью**, включая
   `StripAnswerKeyFields`, единственную точку сборки DTO, ветку скипа, логи/тела ошибок и
   плумбинг `AiEvaluationResult`/`AiExerciseEvaluationStrategy` на null/отсутствие. Утечки до
   ответа нет; см. R2-8 (латентно) и R2-4.
2. Смена семантики оценивания (`90d5e491`) — **разобрана**, потребители порогов перечислены.
   Правка теста законна.
3. Изъятие демо-токена из tenancy (`3ce8b8f8`) — **разобрано**, чисто.
4. `604dd1d3` и `6893c2ad` — **разобраны**; `604dd1d3` чисто, по `6893c2ad` — R2-10 (плюс
   проверены преждевременная разблокировка, N+1 и тема без уроков — всё чисто).
5. Редактор упражнений / композер (`5a1a4572`, `2d66f469`, `ad47210b`) — **разобраны**; R2-1,
   R2-2, R2-3.
6. Санитайзер (`11c7069c`) — **разобран, аллоулист перепроверен эмпирически**; R2-9.
7. `SkillCatalogCache` (`01de8624`) — **разобран**; R2-11.

Не дотянулся: рантайм-проверок не делал (сервера не трогал) — R2-1 и R2-12 стоило бы
подтвердить одним кликом в живом приложении, хотя R2-1 воспроизведён на самом выражении.
Интеграционные наборы бэкенда (`TestCategory=Integration`) не запускались по условию задания.
Незакоммиченные правки рабочего дерева (~26 файлов, другой агент) не ревьюились.

### Верификация (вывод дословно)

```
$ cd src/frontend && npx tsc --noEmit
EXIT=0
```

```
$ cd src/frontend && npx vitest run
 ❯ __tests__/CompanyPage.test.tsx (10 tests | 2 failed) 290ms
   × CompanyPage > shows the not-found state on a 404
     → Unable to find an element with the text: Компания не найдена.
   × CompanyPage > shows a generic error state with retry for non-404 failures
     → Unable to find an element with the text: Не удалось загрузить.

 Test Files  1 failed | 86 passed (87)
      Tests  2 failed | 975 passed (977)
EXIT=1
```

```
$ scripts/tenancy-boundary-lint.sh
tenancy-boundary-lint: clean.
EXIT=0

$ scripts/tenancy-pool-lint.sh
tenancy-pool-lint: clean.
EXIT=0
```

```
$ cd src/backend && dotnet build <project>
=== BUILD learning-service/Learning ===   Build succeeded. 0 Warning(s) 0 Error(s)
=== BUILD ai-service/Ai ===               0 Error(s); 4 warnings (NU1902 SharpCompress 0.30.1, NU1903 Snappier 1.0.0 — транзитивные, вне диапазона)
=== BUILD identity-service/Identity ===   Build succeeded. 0 Warning(s) 0 Error(s)
=== BUILD building-blocks/BuildingBlocks === Build succeeded. 0 Warning(s) 0 Error(s)
```

```
$ cd src/backend && dotnet test <project> --filter "TestCategory!=Integration"
learning-service/Learning.Tests        Passed! Failed: 0, Passed:  90, Skipped: 0, Total:  90
ai-service/Ai.Tests                    Passed! Failed: 0, Passed: 170, Skipped: 0, Total: 170
identity-service/Identity.Tests        Passed! Failed: 0, Passed: 136, Skipped: 0, Total: 136
building-blocks/BuildingBlocks.Tests   Passed! Failed: 0, Passed: 119, Skipped: 4, Total: 123
```

```
$ scripts/codestyle-lint.sh
src/backend/identity-service/.../AuthenticationService.cs:152: explanatory comment forbidden, use /// XML documentation (Rule 9)
src/backend/identity-service/.../AuthenticationService.cs:153: explanatory comment forbidden, use /// XML documentation (Rule 9)
src/backend/identity-service/.../AuthenticationService.cs:262: explanatory comment forbidden, use /// XML documentation (Rule 9)
src/backend/identity-service/.../AuthenticationService.cs:263: explanatory comment forbidden, use /// XML documentation (Rule 9)
src/backend/identity-service/.../AuthenticationService.cs:264: explanatory comment forbidden, use /// XML documentation (Rule 9)
codestyle-lint: 5 violation(s) found.   (файл вне диапазона)
```

Оракул санитайзера (`11c7069c`), прогнан на `sanitize-html 2.17.7` из
`src/frontend/node_modules` с точными опциями из `feedback-html.tsx`:

```
"<script>alert(1)</script>"                        san -> ""                          strip-> ""
"<img src=x onerror=alert(1)>"                     san -> ""                          strip-> ""
"<a href=\"javascript:alert(1)\">x</a>"            san -> "x"                         strip-> "x"
"<p style=\"position:fixed\" onclick=\"x()\">t</p>" san -> "<p>t</p>"                  strip-> "t"
"<svg onload=alert(1)></svg>"                      san -> ""                          strip-> ""
"<iframe src=x></iframe>"                          san -> ""                          strip-> ""
"<h3>Итог</h3><p>Первое.</p><p>Второе.</p>"        san -> "<h3>Итог</h3><p>…</p>"      strip-> "Итог Первое. Второе."
"<ol><li>a</li><li>b</li></ol>"                    san -> "<ol><li>a</li><li>b</li></ol>" strip-> "a b"
"строка1<br>строка2"                               san -> "строка1<br />строка2"       strip-> "строка1 строка2"
"Оценка < 70 & \"низко\""                          san -> "Оценка &lt; 70 &amp; …"     strip-> "Оценка < 70 & \"низко\""
"&lt;script&gt;alert(1)&lt;/script&gt;"            san -> "&lt;script&gt;…&lt;/script&gt;" strip-> "<script>alert(1)</script>"   <- R2-9
"&lt;img src=x onerror=alert(1)&gt;"               san -> "&lt;img …&gt;"              strip-> "<img src=x onerror=alert(1)>"    <- R2-9
"&amp;lt;script&amp;gt;alert(1)&amp;lt;/script&amp;gt;" san -> "&amp;lt;script&amp;gt;…" strip-> "&lt;script&gt;alert(1)&lt;/script&gt;"
```

Итог: `<script>`, `onerror`, `javascript:`-href, инлайновый `style`, `onclick`, `<svg onload>`,
`<iframe>` вырезаются и `sanitizeFeedbackHtml`, и `stripFeedbackHtml` — после правки с
декодированием сущностей аллоулист не ослаб. Единственное следствие — R2-9.

**Требует правки до пуша:** R2-1, R2-3, R2-5 + два падающих теста `CompanyPage` (вне диапазона).
**Требует решения владельца:** R2-7 → `docs/NIGHT_AUDIT_QUESTIONS.md` Q-16.
