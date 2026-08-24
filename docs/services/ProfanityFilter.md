# ProfanityFilter

`ProfanityFilterService` — клиентская проверка пользовательского ввода через серверный фильтр мата. Подходит для UGC-форм, чатов поверх собственного транспорта, имён персонажей и т.п.

## Методы

- `CheckAsync(string text, string groupName = null)` → `ProfanityCheckResponse` — отправляет текст на проверку. Возвращает:
  - `isClean` — `true`, если совпадений нет.
  - `maskedText` — текст с заменой найденного на `*` (длина оригинала сохраняется).
  - `matches[]` — массив `{ start, length, word }` для каждого совпадения.

Локальные проверки до запроса:
- `text == null/""` → результат `isClean=true`, без сетевого вызова.
- `text.Length > 2000` → `ValidationFail`, без сетевого вызова.

`groupName` — имя группы фильтра, настроенной в админке. `null`/пустая строка → используется группа `default`. Неизвестное имя на сервере молча падает на `default` (не возвращает ошибку).

## Code
- `Core/Services/ProfanityFilter/ProfanityFilterService.cs`
- `Core/Services/ProfanityFilter/Requests/ProfanityCheckRequest.cs`
- `Core/Services/ProfanityFilter/Responses/ProfanityCheckResponse.cs`

## Пример

```csharp
var op = MirraCloudSDK.Instance.ProfanityFilter.CheckAsync(userMessage, groupName: "chat-strict");
op.UseCompleted(completed =>
{
    if (!completed.Result.IsSuccess)
    {
        Debug.LogError(completed.Result.Error?.Message);
        return;
    }

    if (completed.Result.Data.isClean)
        SendToServer(userMessage);
    else
        ShowToUser(completed.Result.Data.maskedText);
});
```

## Server endpoint
`POST api/cloud/sdk/profanity-filter/v1/projects/{projectId}/check`

Требует авторизации игрока (тот же flow, что у Chats / Friends SDK). Анонимные вызовы получают `401`.
