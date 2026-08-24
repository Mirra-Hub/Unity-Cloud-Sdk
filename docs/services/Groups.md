# Groups

`GroupsService` — группы/кланы с ролями, правами, приглашениями и банами.

## Группы

- `CreateAsync(dto)` → `GroupDto`
- `GetAsync(groupId)` → `GroupDto`
- `UpdateAsync(groupId, dto)` → `GroupDto`
- `DeleteAsync(groupId)`
- `SearchAsync(query, visibility, page, pageSize)` → `PaginatedResult<GroupListItemDto>`
- `JoinAsync(groupId)` — вступить в открытую группу
- `CreateChatAsync(groupId)` → `ChatConfigDto` — создать чат группы

## Участники

- `GetMembersAsync(groupId, page, pageSize)` → `PaginatedResult<MemberDto>`
- `UpdateMemberRoleAsync(groupId, profileId, dto)` → `MemberDto`
- `KickMemberAsync(groupId, profileId)`
- `LeaveAsync(groupId)` — выйти из группы

## Группы игрока

- `GetMyGroupsAsync(page, pageSize)` → `PaginatedResult<GroupListItemDto>`
- `GetPlayerGroupsAsync(playerId, page, pageSize)` → `PaginatedResult<GroupListItemDto>`

## Роли

- `GetRolesAsync(groupId)` → `RoleDto[]`
- `CreateRoleAsync(groupId, dto)` → `RoleDto`
- `UpdateRoleAsync(groupId, roleId, dto)` → `RoleDto`
- `DeleteRoleAsync(groupId, roleId)`

## Баны

- `GetBansAsync(groupId, page, pageSize)` → `PaginatedResult<BanDto>`
- `BanPlayerAsync(groupId, dto)`
- `UnbanPlayerAsync(groupId, profileId)`

## Приглашения

- `CreateInviteAsync(groupId, dto)` → `InviteDto` — пригласить игрока
- `RevokeInviteAsync(groupId, inviteId)` — отозвать
- `AcceptInviteAsync(groupId, inviteId)` — принять
- `DeclineInviteAsync(groupId, inviteId)` — отклонить
- `CreateInviteKeyAsync(groupId, dto)` → `InviteKeyDto` — создать ключ-приглашение
- `DeleteInviteKeyAsync(groupId, inviteKeyId)` — удалить ключ
- `JoinByKeyAsync(groupId, dto)` — вступить по ключу

## Заявки на вступление

- `CreateJoinRequestAsync(groupId)` → `JoinRequestDto`
- `GetJoinRequestsAsync(groupId, statusFilter, page, pageSize)` → `PaginatedResult<JoinRequestDto>`
- `ApproveJoinRequestAsync(groupId, requestId)`
- `RejectJoinRequestAsync(groupId, requestId)`

## Code
- `Core/Services/Groups/*`
