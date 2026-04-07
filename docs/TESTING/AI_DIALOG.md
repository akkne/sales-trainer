# AI Dialog — Testing Checklist

## Manual Testing

### Prerequisites
- [ ] Backend running with valid `OpenAI:ApiKey` in `appsettings.json`
- [ ] MongoDB running and accessible
- [ ] Test user account logged in

### Bundle & Mode Display

- [ ] Navigate to `/dialog` tab
- [ ] Bundles grid displays (at least "Холодные звонки")
- [ ] Bundle card shows icon, title, description
- [ ] Click bundle → navigates to `/dialog/[bundleId]`
- [ ] Modes grid displays (at least "Обход секретаря")
- [ ] Mode card shows title, description
- [ ] "Назад к навыкам" link works

### Chat Session

- [ ] Click mode → navigates to `/dialog/[bundleId]/[modeId]`
- [ ] Session starts automatically
- [ ] AI sends first message (secretary greeting)
- [ ] User can type and send message
- [ ] AI responds to user message
- [ ] Messages display correctly (user = green right, AI = gray left)
- [ ] Typing indicator shows while waiting for AI
- [ ] Chat auto-scrolls to bottom on new message

### Session Completion

- [ ] "Завершить диалог" button appears after AI stop signal
- [ ] Click "Завершить" → shows "Формируем обратную связь..."
- [ ] Feedback modal appears with analysis
- [ ] "Понятно" button → returns to `/dialog`
- [ ] Session marked as completed in MongoDB

### Error Handling

- [ ] Close button (✕) returns to `/dialog`
- [ ] Error message shows if session fails to start
- [ ] Error message shows if message fails to send
- [ ] App doesn't crash on network errors

### Admin Panel

- [ ] Navigate to `/admin/dialog`
- [ ] Bundles table displays
- [ ] Create new bundle → appears in table
- [ ] Edit bundle → changes saved
- [ ] Delete bundle → removed (with confirmation)
- [ ] Click bundle → navigates to modes page
- [ ] Create new mode with system/feedback prompts
- [ ] Edit mode → changes saved
- [ ] Delete mode → removed

### Graceful Degradation (no API key)

- [ ] Remove/invalidate `OpenAI:ApiKey`
- [ ] `/dialog/bundles` returns empty array
- [ ] Dialog page shows "Практика диалогов пока недоступна"
- [ ] Admin CRUD still works

## Integration Tests (outline)

```csharp
// DialogControllerTests.cs

[Fact]
public async Task GetBundles_ReturnsActiveBundles_WhenOpenAiConfigured()
{
    // Arrange: seed bundle, configure API key
    // Act: GET /dialog/bundles
    // Assert: returns bundle list
}

[Fact]
public async Task GetBundles_ReturnsEmptyArray_WhenOpenAiNotConfigured()
{
    // Arrange: no API key
    // Act: GET /dialog/bundles
    // Assert: returns []
}

[Fact]
public async Task StartSession_CreatesSessionWithFirstMessage()
{
    // Arrange: seed bundle + mode
    // Act: POST /dialog/sessions
    // Assert: session created, has 1 assistant message
}

[Fact]
public async Task SendMessage_AddsUserAndAssistantMessages()
{
    // Arrange: active session
    // Act: POST /dialog/sessions/{id}/messages
    // Assert: returns assistant message, session has 3 messages total
}

[Fact]
public async Task CompleteSession_GeneratesFeedback()
{
    // Arrange: active session with messages
    // Act: POST /dialog/sessions/{id}/complete
    // Assert: returns feedback, session status = completed
}

[Fact]
public async Task AdminCreateBundle_RequiresAdminRole()
{
    // Arrange: user token (not admin)
    // Act: POST /admin/dialog/bundles
    // Assert: 403 Forbidden
}
```

## Unit Tests (outline)

```csharp
// OpenAiChatServiceTests.cs

[Fact]
public void IsConfigured_ReturnsFalse_WhenApiKeyIsPlaceholder()
{
    // Arrange: config with "REPLACE_WITH_OPENAI_API_KEY"
    // Act: check IsConfigured
    // Assert: false
}

[Fact]
public void IsConfigured_ReturnsTrue_WhenApiKeyIsValid()
{
    // Arrange: config with "sk-..."
    // Act: check IsConfigured
    // Assert: true
}

[Fact]
public async Task SendChatMessageAsync_ThrowsIfNotConfigured()
{
    // Arrange: no API key
    // Act/Assert: throws InvalidOperationException
}

// DialogServiceTests.cs

[Fact]
public async Task StartSessionAsync_ThrowsIfModeNotFound()
{
    // Arrange: invalid modeId
    // Act/Assert: throws InvalidOperationException
}
```
