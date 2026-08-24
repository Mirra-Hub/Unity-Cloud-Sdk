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
- `CreateProfileAsync(dto, autoSelect)` → `ProfileInfo`
- `DeleteProfileAsync(profileId)`
- `SelectProfileAsync(profileId)` — выбрать активный профиль
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

- `PlayerAccountInfo` — текущий аккаунт
- `Profiles` — список профилей

## События

- `OnProfilesChanged` — изменился список профилей
- `OnProfileUpdated` — обновлён профиль
- `OnProfileSelected` — выбран профиль

## Code
- `Core/Services/PlayerAccount/*`
