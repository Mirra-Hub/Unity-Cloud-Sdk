# MirraCloud Showcase — official Unity example

A polished, runnable example that demonstrates **every MirraCloud SDK service** with a real,
visual UI (not raw JSON). You sign in from a dedicated auth screen, land on a grid of services,
and open any service to see its data rendered properly — tables, cards, avatars, reward chips,
progress bars, live countdowns, and interactive tools.

Built entirely with **UI Toolkit** (runtime UXML/USS), wired through the same **VContainer**
bootstrap the rest of the SDK uses.

---

## Run it

This sample needs [VContainer](https://github.com/hadashiA/VContainer), which is not a package
dependency (UPM cannot resolve git dependencies for you) — add it first:

```
https://github.com/hadashiA/VContainer.git?path=VContainer/Assets/VContainer#1.17.0
```

1. Import the sample: `Package Manager → Mirra Cloud SDK → Samples → Showcase → Import`.
2. Connect the project: `Tools → Mirra Cloud → Manager`. That writes
   `Assets/MirraCloud/Resources/Configuration.asset`, which the sample reads like any other game.
3. Open `Assets/Samples/Mirra Cloud SDK/<version>/Showcase/Scenes/MC_Showcase.unity` and press
   **Play**.

You start on the **auth screen**: pick a provider (Guest / Device / Email, or an external
provider via in-app WebView/OpenID). On success you move to the **services screen** — a grid of
all SDK modules. Tap any card to open its detail view.

> Dev tip: the `ShowcaseInstaller` component on `ShowcaseRoot` has a `_devForceServices` toggle
> that skips the auth gate and drops you straight on the services grid (handy for visual QA).

---

## How it's put together

```
Showcase/
  Scenes/MC_Showcase.unity        # ShowcaseUI (UIDocument) + ShowcaseRoot (installer) + EventSystem
  UI/
    Showcase.uxml / Showcase.uss  # design-system tokens + every sc- component style
    BridgePanelSettings.asset      # ConstantPixelSize panel (crisp text)
    Fonts/                         # LiberationSans (OFL) for text, lucide.ttf (ISC) for icons
  Scripts/Showcase/
    App/      ShowcaseApp, ShowcaseInstaller, ShowcaseModules, Nav, Popup, Toasts
    Views/    AuthView, ServicesView, ServiceView (base) + one view per service
    Components/  Avatar, Card, Chip/RewardChip/CountdownChip, StatTile, ListRow,
                 DataTable, ProgressBar, SectionHeader, Skeleton/EmptyState/ErrorState
    Infra/    ViewBind, RemoteImageLoader, Fmt
```

**Bootstrap & DI.** `ShowcaseInstaller : LifetimeScope` registers the `IMirraCloudSdk` singleton,
grabs the scene's `UIDocument`, and runs `ShowcaseApp` (an `IStartable`). The scope is
self-contained, so the scene runs as-is in any project this folder is dropped into — in a real
game you would register the SDK once in a project-wide root scope instead. `ShowcaseApp` builds
the nav/overlay/toast hosts, gates on auth, and routes provider buttons to the SDK.

**Auth.** `AuthView` offers Guest / Device / Email and external providers. External providers use
**OpenID over an in-app WebView** (`LoginOpenIdAsync(providerId, new OpenIdLoginOptions { UseInAppWebView = true })`)
— no native plugins required. (WebView is unavailable on WebGL/in-Editor.)

**Per-service views.** `ShowcaseApp.OpenModule` resolves a view by module id. Every service has a
hand-built `ServiceView` subclass (back button + accent title + scrollable content column). They
bind data through one small helper:

```csharp
ViewBind.Load(sdk.SomeService.SomeReadAsync(), slot, data => RenderIt(data),
    isEmpty: d => d == null || d.Length == 0,
    emptyView: () => EmptyState.Build("glyph", "Nothing here"));
```

`ViewBind` drives the uniform `AsyncOperation<RestApiResult<T>>` contract through
**Loading → Data / Empty / Error** automatically (the SDK never throws on HTTP — failures are
values), so each view only writes the happy-path render.

---

## What each service shows

| Service | View |
| --- | --- |
| Player Account | Hero card (avatar, nickname/@handle, trait chips, lifetime stat tiles, segments) + sub-profiles |
| Leaderboard | Tab per board, config chips + ranked table (medal ranks, avatars, scores) |
| Economy | Wallet tiles + energy meters (fill bar + recharge/cooldown) + item grid |
| Friends | Counts strip + friends (presence) / incoming / outgoing requests |
| Assets Storage | Summary stats + by-type breakdown + folders list + assets table |
| Chats | Lookup by channel/group id → channel header, members, recent messages |
| Tournaments | Tab per tournament, leagues with rewards-for-places, standings, your rewards |
| Challenges | Card per challenge with live progress bar, status, reward tiers, countdown |
| Daily Rewards | Streak/progress header + day-by-day reward track + streak bonuses + milestones |
| Groups | My-groups list → group card + members table (owner highlighted) |
| Remote Config | Per-group typed key/value table |
| Localization | Lookup by collection → language selector + key→translation table |
| Segments | Player membership chips + all-segments status table |
| Entities | Config snapshot → per-config dynamic field table + components |
| Cloud Save | Player key/value records (type, value, access masks, version) |
| Purchases | Store catalog (price/currency/rewards) + order history |
| Promo Codes | Redeem tool (with status gate) + redemption history |
| Profanity Filter | Check tool → verdict, masked output, matched fragments |
| Cloud Code | Invoke a function by key → dynamic JSON result |
| Analytics | Fire tools (custom event / session / playtime) |
| WebView | Open-a-URL tool (gated on `IsReady`) + live event log |
| Deployment | Local config card + resolve-branch-for-version tool |

---

## Notes

- **Read-only by intent.** Detail screens demonstrate reads (and a few non-destructive tools like
  promo redeem, profanity check, cloud-code invoke). Money/buy flows and bulk mutations are left
  out on purpose.
- **Fonts.** UI Toolkit's default runtime font renders as solid boxes here, so `Showcase.uss` pins
  an explicit `-unity-font`: `UI/Fonts/LiberationSans.ttf` for text and `UI/Fonts/lucide.ttf` for
  the `.sc-icon` glyphs. Both are referenced by a path relative to the `.uss`, so they survive the
  sample being imported anywhere. Keep them if you fork the styles. Liberation Sans is SIL OFL 1.1
  and Lucide is ISC — licenses ship next to the files.
- **Editing the sample.** The source of truth is `Samples~/Showcase` inside the package, and Unity
  cannot see a `~` folder. To change it, edit the imported copy under `Assets/Samples/…` and copy
  the files back over `Samples~/Showcase`. The `.meta` files must come back too — they carry the
  guids the scene, UXML and USS resolve through.
- **Avatars.** `RemoteImageLoader` fetches avatar URLs with an initials+color fallback, so a
  missing/broken image never shows a broken box.
