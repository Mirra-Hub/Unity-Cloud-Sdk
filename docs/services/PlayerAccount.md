# PlayerAccount

`PlayerAccountService` — управление аккаунтом и профилями игрока.

## Аккаунт

- `GetAccountAsync()` → `PlayerAccountInfo`
- `UpdateNicknameAsync(nickname)`
- `UpdateAgeAsync(age)`
- `UpdateCountryAsync(country)`
- `UpdateLanguageAsync(languageCode)`
- `SetAccountIconUrlAsync(iconUrl)`
- `UpdateIconWithUploadAsync(fileData | texture | sprite)` — загрузка иконки
- `UpdateSegmentsAsync(segmentIds)`

## Профили

- `GetProfilesAsync()` → `ProfileInfo[]`
- `GetProfileAsync(profileId)` → `ProfileInfo`
- `CreateProfileAsync(dto, autoSelect)` → `ProfileInfo`. С `autoSelect: true` профиль ещё и выбирается — как в `SelectProfileAsync`, операция завершается после refresh сессии.
- `DeleteProfileAsync(profileId)`
- `SelectProfileAsync(profileId)` — выбрать активный профиль. Сервер не выдаёт новый токен, а все сервисы уровня профиля (экономика, сейвы, покупки, награды, аналитика) работают по токену — поэтому SDK сразу обновляет сессию, и операция завершается только после этого: вызовы после `await` уже идут от нового профиля. Если refresh не прошёл из-за сети или 5xx, игрок не разлогинивается: профиль на сервере уже выбран, SDK пишет ошибку в лог, а вызовы идут от старого профиля до следующего refresh (например, после 401). Аналитика до переключения отправляет буфер старого профиля и после него начинает новую игровую сессию; чаты переподключают сокет от нового профиля.
- `ReplaceProfileAsync(profileId, dto)` — полная замена профиля
- `UpdateProfileNicknameAsync(profileId, username)`
- `SetProfileIconUrlAsync(profileId, iconUrl)`
- `UpdateProfileIconWithUploadAsync(profileId, fileData | texture | sprite)`
- `UpdateProfileSegmentsAsync(profileId, segmentIds)`
- `UpdateProfilePresenceStatusAsync(profileId, status)`

## Роли игрока

- `GetPlayerRolesAsync()` → `PlayerRoleInfo[]` — каталог ролей текущей ветки (ключ + название), только чтение.
- Назначенные игроку роли приходят в `ProfileInfo.RoleKeys` (список ключей); названия резолвятся через каталог.
- Роли назначаются только из админ-панели — SDK-мутаций нет.

## Свойства

- `PlayerAccountInfo` — текущий аккаунт (заполняется при входе и из ответа refresh — в том числе при восстановлении сессии в `InitializeAsync`)
- `PlayerAccountInfo.SelectedProfileId` — выбранный профиль, от которого идут вызовы уровня профиля
- `Profiles` — список профилей

## События

- `OnProfilesChanged` — изменился список профилей
- `OnProfileUpdated` — обновлён профиль
- `OnProfileSelected` — выбран профиль; приходит после refresh сессии, так что вызовы из обработчика уже идут от нового профиля

## Code
- `Packages/com.mirrahub.cloud-sdk/Runtime/Services/PlayerAccount/*`
