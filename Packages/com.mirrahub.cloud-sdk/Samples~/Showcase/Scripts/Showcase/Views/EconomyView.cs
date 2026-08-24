using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MirraCloud.Core.Economy.Dto;
using MirraCloud.Json;
using UnityEngine.UIElements;

namespace MirraCloud.Example.Showcase
{
    /// <summary>
    /// Economy screen: what the player owns (wallet, items, energy), what the project defines
    /// (catalog), and everything the SDK can change about it (actions).
    /// <para>
    /// One <c>LoadInventoryAsync</c> feeds the first three tabs, so a write only has to reload that
    /// single call to show its effect — which is exactly what each action does when it succeeds.
    /// </para>
    /// </summary>
    public sealed class EconomyView : ServiceView
    {
        private const string InventorySnippet =
@"// Wallet, items and energy in one call — the whole player-facing economy state.
var op = sdk.Economy.LoadInventoryAsync();
await op.Task();

var result = op.Result;
if (result.IsSuccess)
{
    PlayerInventoryDto inv = result.Data;
    // inv.Wallet:   CurrencyId, Balance
    // inv.Items:    SlotId, ItemId, Quantity, InventoryKey, Properties
    // inv.Energies: EnergyId, CurrentValue, MaxValue, SecondsUntilNextRecharge, IsUnlimited
}";

        private const string ConfigsSnippet =
@"// What the project defines, as opposed to what this player holds. Each entry carries the
// designer's fields and components as raw JSON.
var op = sdk.Economy.LoadConfigsAsync();
await op.Task();

if (op.Result.IsSuccess)
{
    EconomyConfigsDto cfg = op.Result.Data;
    // cfg.Currencies / cfg.Items / cfg.Energies : Dictionary<string, EconomySdkResourceDto>
}";

        private const string GrantSnippet =
@"// Grant and take away. SubtractItemAsync fails when the player is short; the Safe variant
// clamps to zero instead — pick per feature, not per taste.
await sdk.Economy.AddItemAsync(""sword"", 1).Task();
await sdk.Economy.SubtractItemAsync(""potion"", 2).Task();
await sdk.Economy.SubtractItemSafeAsync(""potion"", 999).Task();

// inventoryKey is optional and addresses a named bag (""stash"", ""mail"", …).
await sdk.Economy.AddItemAsync(""sword"", 1, ""stash"").Task();";

        private const string ConsumeSnippet =
@"// Consuming spends the item and returns what it granted — the reward payload is the
// point, so read it rather than reloading blindly.
var op = sdk.Economy.ConsumeItemAsync(""loot_box"", slotId);
await op.Task();

if (op.Result.IsSuccess)
{
    ConsumeItemResponseDto got = op.Result.Data;
    // got.GrantedCurrencies / got.GrantedItems / got.GrantedEnergies
}";

        private const string PropertiesSnippet =
@"// Per-slot properties: durability, enchantments, anything the design needs on one copy
// of an item rather than on the item type.
var props = new Dictionary<string, object> { { ""durability"", 42 } };
await sdk.Economy.UpdateItemPropertiesAsync(""sword"", slotId, props).Task();";

        private const string EnergySnippet =
@"// Energy regenerates on the server; the client only spends, tops up, or lifts the cap.
await sdk.Economy.SpendEnergyAsync(""stamina"", 1).Task();
await sdk.Economy.AddEnergyAsync(""stamina"", 5).Task();
await sdk.Economy.SetUnlimitedEnergyAsync(""stamina"", 3600).Task();

// Balances can also be read on their own, without the whole inventory.
var one = sdk.Economy.GetEnergyAsync(""stamina"");
var allOfThem = sdk.Economy.GetEnergiesAsync();";

        private PlayerInventoryDto _inventory;
        private Tabs _tabs;

        public EconomyView(ServiceMeta meta, Action onBack, ShowcaseContext ctx)
            : base(meta, onBack, ctx)
        {
        }

        protected override void Populate()
        {
            DeclareCall(new SdkCall("Read the player's economy", InventorySnippet,
                "One call behind the Wallet, Inventory and Energy tabs."));
            DeclareCall(new SdkCall("Read the project's catalog", ConfigsSnippet));
            DeclareCall(new SdkCall("Grant and take items", GrantSnippet));
            DeclareCall(new SdkCall("Consume an item", ConsumeSnippet));
            DeclareCall(new SdkCall("Update slot properties", PropertiesSnippet));
            DeclareCall(new SdkCall("Spend and refill energy", EnergySnippet));

            UseToolbar().WithSpacer().WithRefresh(Refresh);

            _tabs = UseTabs();
            _tabs.Add("Wallet", LucideIcon.Wallet, BuildWallet)
                .Add("Inventory", LucideIcon.Package, BuildInventory)
                .Add("Energy", LucideIcon.Zap, BuildEnergy)
                .Add("Catalog", LucideIcon.Boxes, BuildCatalog)
                .Add("Actions", LucideIcon.Sparkles, BuildActions);
        }

        // Every inventory-backed tab goes through here, so the three of them share one request
        // shape and one set of states.
        private VisualElement InventoryPane(Func<PlayerInventoryDto, VisualElement> render,
            Func<PlayerInventoryDto, bool> isEmpty, Func<VisualElement> empty)
        {
            var slot = new VisualElement();
            ViewBind.Load(
                () => Sdk.Economy.LoadInventoryAsync(),
                slot,
                data =>
                {
                    _inventory = data;
                    SyncStatus();
                    return render(data);
                },
                isEmpty,
                new BindOptions
                {
                    Log = Ctx.Log,
                    Label = "Inventory",
                    Snippet = InventorySnippet,
                    ServiceName = "Economy",
                    AllowRetry = true,
                    EmptyView = empty,
                });
            return slot;
        }

        private void SyncStatus()
        {
            if (_inventory == null)
            {
                SetStatus("No inventory", ChipTone.Warn);
                return;
            }
            int currencies = _inventory.Wallet != null ? _inventory.Wallet.Count : 0;
            int items = _inventory.Items != null ? _inventory.Items.Count : 0;
            SetStatus(currencies + " currencies · " + items + " item slots",
                currencies + items > 0 ? ChipTone.Ok : ChipTone.Neutral);
        }

        // ----- wallet ---------------------------------------------------------------------------

        private VisualElement BuildWallet()
        {
            return InventoryPane(
                data =>
                {
                    var wallet = data.Wallet ?? new List<WalletEntryDto>();
                    var col = new VisualElement();

                    var kpis = new KpiRow();
                    if (wallet.Count == 0)
                    {
                        kpis.AddZero("Currencies", LucideIcon.Coins).AddZero("Total held", LucideIcon.Sigma);
                    }
                    else
                    {
                        decimal total = 0m;
                        foreach (var w in wallet)
                        {
                            total += w.Balance;
                        }
                        kpis.Add("Currencies", LucideIcon.Coins, wallet.Count.ToString())
                            .Add("Total held", LucideIcon.Sigma, Fmt.Number((double)total));
                    }
                    col.Add(kpis);

                    var chart = new BarChart(170f);
                    var points = new List<ChartPoint>();
                    foreach (var w in wallet)
                    {
                        points.Add(new ChartPoint(w.CurrencyId, (float)w.Balance));
                    }
                    chart.SetData(points).SetEmptyText("No currencies in this project yet");
                    col.Add(chart);

                    col.Add(new SectionHeader("Balances", wallet.Count.ToString()));
                    var table = new DataTable(WalletColumns()).WithZebra().WithSort(1, false);
                    table.Bind(wallet);
                    col.Add(table);
                    return col;
                },
                data => data == null || data.Wallet == null || data.Wallet.Count == 0,
                () => ZeroState.Table(WalletColumns(),
                    "Currencies are defined in the Mirra Hub console; a player's balance appears here "
                    + "once something grants them one.",
                    3, "Grant a demo currency", () => _tabs.Select(4)));
        }

        private static DataColumn[] WalletColumns()
        {
            return new[]
            {
                new DataColumn
                {
                    Header = "CURRENCY", Grow = 2f,
                    SortKey = o => ((WalletEntryDto)o).CurrencyId,
                    Cell = o =>
                    {
                        var label = new Label(Fmt.OrDash(((WalletEntryDto)o).CurrencyId));
                        label.enableRichText = false;
                        return label;
                    },
                },
                new DataColumn
                {
                    Header = "BALANCE", Grow = 1f, Align = "right",
                    SortKey = o => (double)((WalletEntryDto)o).Balance,
                    Cell = o => new Label(Fmt.Number((double)((WalletEntryDto)o).Balance)),
                },
            };
        }

        // ----- inventory ------------------------------------------------------------------------

        private VisualElement BuildInventory()
        {
            return InventoryPane(
                data =>
                {
                    var items = data.Items ?? new List<ItemSlotDto>();
                    var col = new VisualElement();

                    var bags = new HashSet<string>();
                    int quantity = 0;
                    foreach (var i in items)
                    {
                        quantity += i.Quantity;
                        bags.Add(string.IsNullOrEmpty(i.InventoryKey) ? "default" : i.InventoryKey);
                    }

                    col.Add(new KpiRow()
                        .Add("Slots", LucideIcon.Package, items.Count.ToString())
                        .Add("Total quantity", LucideIcon.Sigma, Fmt.Number(quantity))
                        .Add("Bags", LucideIcon.Boxes, bags.Count.ToString()));

                    col.Add(new SectionHeader("Slots", items.Count.ToString()));
                    var grid = new VisualElement();
                    grid.AddToClassList("sc-item-grid");
                    foreach (var slot in items)
                    {
                        grid.Add(ItemCard(slot));
                    }
                    col.Add(grid);
                    return col;
                },
                data => data == null || data.Items == null || data.Items.Count == 0,
                () => ZeroState.Cards(LucideIcon.Package,
                    "This player holds no items yet. Grant one from the Actions tab and it will show up "
                    + "here as a slot.",
                    4, "Grant an item", () => _tabs.Select(4)));
        }

        private VisualElement ItemCard(ItemSlotDto slot)
        {
            var card = new VisualElement();
            card.AddToClassList("sc-item");

            var icon = new VisualElement();
            icon.AddToClassList("sc-item__icon");
            var glyph = new Label(LucideIcon.Package);
            glyph.AddToClassList("sc-item__glyph");
            glyph.AddToClassList("sc-icon");
            icon.Add(glyph);
            var qty = new Label("x" + slot.Quantity);
            qty.AddToClassList("sc-item__qty");
            icon.Add(qty);
            card.Add(icon);

            var name = new Label(Fmt.OrDash(slot.ItemId));
            name.enableRichText = false;
            name.AddToClassList("sc-item__name");
            card.Add(name);

            var sub = new Label(string.IsNullOrEmpty(slot.InventoryKey) ? "default bag" : slot.InventoryKey);
            sub.enableRichText = false;
            sub.AddToClassList("sc-item__sub");
            card.Add(sub);

            card.RegisterCallback<ClickEvent>(_ => ShowItem(slot));
            return card;
        }

        private void ShowItem(ItemSlotDto slot)
        {
            if (Popup == null)
            {
                return;
            }

            var body = new ScrollView(ScrollViewMode.Vertical);
            body.style.maxHeight = 460f;

            var kv = new VisualElement();
            kv.AddToClassList("sc-kv-list");
            kv.Add(Kv("Item id", Fmt.OrDash(slot.ItemId), slot.ItemId));
            kv.Add(Kv("Slot id", Fmt.OrDash(slot.SlotId), slot.SlotId));
            kv.Add(Kv("Quantity", slot.Quantity.ToString(), null));
            kv.Add(Kv("Bag", string.IsNullOrEmpty(slot.InventoryKey) ? "default" : slot.InventoryKey, null));
            body.Add(kv);

            body.Add(new SectionHeader("Properties"));
            if (slot.Properties == null || slot.Properties.Count == 0)
            {
                body.Add(ZeroState.Panel(LucideIcon.Braces, "No slot properties",
                    "Properties live on one copy of an item — durability, enchantments, anything that "
                    + "differs between two otherwise identical items. Set them from the Actions tab."));
            }
            else
            {
                var props = new VisualElement();
                props.AddToClassList("sc-kv-list");
                foreach (var pair in slot.Properties)
                {
                    props.Add(Kv(pair.Key, pair.Value == null ? Fmt.Dash : pair.Value.ToString(), null));
                }
                body.Add(props);
            }

            Popup.Open(body, Fmt.Truncate(Fmt.OrDash(slot.ItemId), 34));
        }

        private VisualElement Kv(string key, string value, string copyable)
        {
            var row = new VisualElement();
            row.AddToClassList("sc-kv");

            var k = new Label(key);
            k.AddToClassList("sc-kv__k");
            row.Add(k);

            var v = new Label(value);
            v.enableRichText = false;
            v.AddToClassList("sc-kv__v");
            row.Add(v);

            if (!string.IsNullOrEmpty(copyable))
            {
                row.Add(new CopyButton(copyable, Toasts));
            }
            return row;
        }

        // ----- energy ---------------------------------------------------------------------------

        private VisualElement BuildEnergy()
        {
            return InventoryPane(
                data =>
                {
                    var energies = data.Energies ?? new List<EnergyBalanceDto>();
                    var col = new VisualElement();

                    int full = 0;
                    int unlimited = 0;
                    foreach (var e in energies)
                    {
                        if (e.IsUnlimited)
                        {
                            unlimited++;
                        }
                        if (e.MaxValue > 0 && e.CurrentValue >= e.MaxValue)
                        {
                            full++;
                        }
                    }

                    col.Add(new KpiRow()
                        .Add("Meters", LucideIcon.Zap, energies.Count.ToString())
                        .Add("At full", LucideIcon.CircleCheck, full.ToString())
                        .Add("Unlimited", LucideIcon.Sparkles, unlimited.ToString(), null, unlimited > 0));

                    foreach (var energy in energies)
                    {
                        col.Add(EnergyCard(energy));
                    }
                    return col;
                },
                data => data == null || data.Energies == null || data.Energies.Count == 0,
                () => ZeroState.Panel(LucideIcon.Zap, "No energy meters",
                    "Energy is a stamina-style resource that refills over time on the server. Define one "
                    + "in the console and the player's meter, recharge timer and cooldown appear here.",
                    "Try the energy actions", () => _tabs.Select(4)));
        }

        private VisualElement EnergyCard(EnergyBalanceDto energy)
        {
            var card = new Card(Meta.Accent);
            card.AddToClassList("sc-eco-energy");

            var head = new VisualElement();
            head.AddToClassList("sc-energy__head");

            var name = new Label(Fmt.OrDash(energy.EnergyId));
            name.enableRichText = false;
            name.AddToClassList("sc-energy__name");
            head.Add(name);

            var value = new Label(energy.CurrentValue + " / " + energy.MaxValue);
            value.AddToClassList("sc-energy__val");
            head.Add(value);
            card.Body.Add(head);

            card.Body.Add(new ProgressBar()
                .Set(energy.CurrentValue, energy.MaxValue)
                .SetAccent(energy.IsUnlimited ? ShowcaseTheme.Warn : Meta.Accent));

            var chips = new VisualElement();
            chips.AddToClassList("sc-chip-row");

            if (energy.IsUnlimited)
            {
                chips.Add(energy.UnlimitedRemainingSeconds.HasValue
                    ? new CountdownChip(DateTime.UtcNow.AddSeconds(energy.UnlimitedRemainingSeconds.Value))
                    : new Chip("unlimited", ChipTone.Warn));
            }
            if (energy.IsOnCooldown)
            {
                chips.Add(energy.CooldownRemainingSeconds.HasValue
                    ? new Chip("cooldown " + Fmt.Duration(TimeSpan.FromSeconds(energy.CooldownRemainingSeconds.Value)),
                        ChipTone.Bad)
                    : new Chip("on cooldown", ChipTone.Bad));
            }
            if (energy.SecondsUntilNextRecharge.HasValue && !energy.IsUnlimited)
            {
                chips.Add(new Chip("+1 in "
                    + Fmt.Duration(TimeSpan.FromSeconds(energy.SecondsUntilNextRecharge.Value)), ChipTone.Info));
            }
            if (energy.SecondsUntilFullRecharge.HasValue && !energy.IsUnlimited)
            {
                chips.Add(new Chip("full in "
                    + Fmt.Duration(TimeSpan.FromSeconds(energy.SecondsUntilFullRecharge.Value)), ChipTone.Neutral));
            }
            if (energy.OverflowValue > 0)
            {
                chips.Add(new Chip("overflow " + energy.OverflowValue, ChipTone.Accent));
            }
            if (chips.childCount > 0)
            {
                card.Body.Add(chips);
            }
            return card;
        }

        // ----- catalog --------------------------------------------------------------------------

        private VisualElement BuildCatalog()
        {
            var slot = new VisualElement();
            ViewBind.Load(
                () => Sdk.Economy.LoadConfigsAsync(),
                slot,
                BuildCatalogBody,
                data => data == null
                    || (Count(data.Currencies) + Count(data.Items) + Count(data.Energies) == 0),
                new BindOptions
                {
                    Log = Ctx.Log,
                    Label = "Economy catalog",
                    Snippet = ConfigsSnippet,
                    ServiceName = "Economy",
                    ConfigurationRequest = true,
                    AllowRetry = true,
                    EmptyView = () => ZeroState.Panel(LucideIcon.Boxes, "Nothing defined yet",
                        "Currencies, items and energy meters are authored in the Mirra Hub console. Once "
                        + "they exist here, a game can grant and spend them through the calls in the "
                        + "code drawer."),
                });
            return slot;
        }

        private static int Count<T>(Dictionary<string, T> map)
        {
            return map == null ? 0 : map.Count;
        }

        private VisualElement BuildCatalogBody(EconomyConfigsDto configs)
        {
            var col = new VisualElement();

            col.Add(new KpiRow()
                .Add("Currencies", LucideIcon.Coins, Count(configs.Currencies).ToString())
                .Add("Items", LucideIcon.Package, Count(configs.Items).ToString())
                .Add("Energies", LucideIcon.Zap, Count(configs.Energies).ToString()));

            var donut = new DonutChart(150f);
            donut.SetData(new[]
                {
                    new ChartPoint("Currencies", Count(configs.Currencies)),
                    new ChartPoint("Items", Count(configs.Items)),
                    new ChartPoint("Energies", Count(configs.Energies)),
                })
                .SetCenter(Fmt.Number(Count(configs.Currencies) + Count(configs.Items) + Count(configs.Energies)),
                    "definitions")
                .SetEmptyText("Nothing defined");
            col.Add(donut);

            col.Add(CatalogSection("Currencies", configs.Currencies));
            col.Add(CatalogSection("Items", configs.Items));
            col.Add(CatalogSection("Energies", configs.Energies));
            return col;
        }

        private VisualElement CatalogSection(string title, Dictionary<string, EconomySdkResourceDto> map)
        {
            var box = new VisualElement();
            box.Add(new SectionHeader(title, Count(map).ToString()));

            if (map == null || map.Count == 0)
            {
                box.Add(ZeroState.Panel(LucideIcon.Braces, "No " + title.ToLowerInvariant(),
                    "None are defined in this project."));
                return box;
            }

            var list = new VisualElement();
            foreach (var pair in map)
            {
                var row = new ListRow();
                row.SetTitle(pair.Key);
                row.SetSubtitle(Describe(pair.Value));

                var trailing = new VisualElement();
                trailing.AddToClassList("sc-chip-row");
                trailing.Add(new CopyButton(pair.Key, Toasts, "id"));
                row.SetTrailing(trailing);

                var definition = pair.Value;
                string key = pair.Key;
                row.RegisterCallback<ClickEvent>(_ => ShowDefinition(key, definition));
                list.Add(row);
            }
            box.Add(list);
            return box;
        }

        private static string Describe(EconomySdkResourceDto resource)
        {
            if (resource == null)
            {
                return "no definition";
            }
            int components = resource.Components != null ? resource.Components.Count : 0;
            return components == 0 ? "no components" : components + (components == 1 ? " component" : " components");
        }

        private void ShowDefinition(string key, EconomySdkResourceDto resource)
        {
            if (Popup == null || resource == null)
            {
                return;
            }

            var body = new ScrollView(ScrollViewMode.Vertical);
            body.style.maxHeight = 460f;

            body.Add(new SectionHeader("Fields"));
            body.Add(new JsonViewer().SetRaw(Pretty(resource.Fields)).SetMaxLines(18));

            body.Add(new SectionHeader("Components",
                resource.Components != null ? resource.Components.Count.ToString() : "0"));
            if (resource.Components == null || resource.Components.Count == 0)
            {
                body.Add(ZeroState.Panel(LucideIcon.Component, "No components",
                    "Components attach behaviour to a definition — stack limits, recharge rules, and so on."));
            }
            else
            {
                foreach (var pair in resource.Components)
                {
                    body.Add(new SectionHeader(pair.Key));
                    body.Add(new JsonViewer().SetRaw(Pretty(pair.Value)).SetMaxLines(12));
                }
            }

            Popup.Open(body, Fmt.Truncate(key, 34));
        }

        // ----- actions --------------------------------------------------------------------------

        private VisualElement BuildActions()
        {
            var col = new VisualElement();

            var hint = new Label("Everything the SDK can change about the player's economy. Each card "
                + "runs one call and reloads the tab it affects, so the result is visible immediately.");
            hint.AddToClassList("sc-fs-hint");
            col.Add(hint);

            col.Add(new ActionCard("Grant items", "Adds a quantity of an item to a bag.", LucideIcon.Plus)
                .WithFields(
                    FormField.Text("itemId", "Item id", null, true),
                    FormField.Int("amount", "Amount", 1),
                    FormField.Text("bag", "Bag (optional)"))
                .WithSnippet(GrantSnippet)
                .OnRun("Grant", v => Run(
                    Sdk.Economy.AddItemAsync(v.Text("itemId"), v.Int("amount"), Nullable(v.Text("bag"))),
                    "Granted " + v.Int("amount") + " × " + v.Text("itemId"), 1)));

            col.Add(new ActionCard("Take items",
                    "Removes a quantity. This is the strict variant: it fails when the player is short.",
                    LucideIcon.Minus)
                .WithFields(
                    FormField.Text("itemId", "Item id", null, true),
                    FormField.Int("amount", "Amount", 1),
                    FormField.Text("bag", "Bag (optional)"))
                .WithSnippet(GrantSnippet)
                .OnRun("Subtract", v => Run(
                    Sdk.Economy.SubtractItemAsync(v.Text("itemId"), v.Int("amount"), Nullable(v.Text("bag"))),
                    "Took " + v.Int("amount") + " × " + v.Text("itemId"), 1)));

            col.Add(new ActionCard("Take items (safe)",
                    "Same, but clamps to what the player actually has instead of failing.", LucideIcon.ShieldCheck)
                .WithFields(
                    FormField.Text("itemId", "Item id", null, true),
                    FormField.Int("amount", "Amount", 1),
                    FormField.Text("bag", "Bag (optional)"))
                .WithSnippet(GrantSnippet)
                .OnRun("Subtract safely", v => Run(
                    Sdk.Economy.SubtractItemSafeAsync(v.Text("itemId"), v.Int("amount"), Nullable(v.Text("bag"))),
                    "Took up to " + v.Int("amount") + " × " + v.Text("itemId"), 1)));

            col.Add(new ActionCard("Consume an item",
                    "Spends one copy and returns whatever it granted — the loot-box call.", LucideIcon.Gift)
                .WithFields(
                    FormField.Text("itemId", "Item id", null, true),
                    FormField.Text("slotId", "Slot id (optional)"),
                    FormField.Text("bag", "Bag (optional)"))
                .WithSnippet(ConsumeSnippet)
                .OnRun("Consume", ConsumeAction));

            col.Add(new ActionCard("Set slot properties",
                    "Writes per-slot JSON: durability, enchantments, anything that differs between two "
                    + "copies of the same item.", LucideIcon.Braces)
                .WithFields(
                    FormField.Text("itemId", "Item id", null, true),
                    FormField.Text("slotId", "Slot id", null, true),
                    FormField.Json("properties", "Properties", "{\n  \"durability\": 42\n}"))
                .WithSnippet(PropertiesSnippet)
                .OnRun("Update", PropertiesAction));

            col.Add(new ActionCard("Spend energy", "Takes energy from a meter.", LucideIcon.Zap)
                .WithFields(
                    FormField.Text("energyId", "Energy id", null, true),
                    FormField.Int("amount", "Amount", 1))
                .WithSnippet(EnergySnippet)
                .OnRun("Spend", v => Run(
                    Sdk.Economy.SpendEnergyAsync(v.Text("energyId"), v.Int("amount")),
                    "Spent " + v.Int("amount") + " " + v.Text("energyId"), 2)));

            col.Add(new ActionCard("Refill energy", "Adds energy back, up to the meter's rules.", LucideIcon.CirclePlus)
                .WithFields(
                    FormField.Text("energyId", "Energy id", null, true),
                    FormField.Int("amount", "Amount", 1))
                .WithSnippet(EnergySnippet)
                .OnRun("Add", v => Run(
                    Sdk.Economy.AddEnergyAsync(v.Text("energyId"), v.Int("amount")),
                    "Added " + v.Int("amount") + " " + v.Text("energyId"), 2)));

            col.Add(new ActionCard("Grant unlimited energy",
                    "Lifts the cap for a while — the timed booster pattern.", LucideIcon.Sparkles)
                .WithFields(
                    FormField.Text("energyId", "Energy id", null, true),
                    FormField.Int("seconds", "Duration (seconds)", 3600))
                .WithSnippet(EnergySnippet)
                .OnRun("Grant", v => Run(
                    Sdk.Economy.SetUnlimitedEnergyAsync(v.Text("energyId"), v.Int("seconds")),
                    "Unlimited " + v.Text("energyId") + " for "
                        + Fmt.Duration(TimeSpan.FromSeconds(v.Int("seconds"))), 2)));

            return col;
        }

        private static string Nullable(string text)
        {
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }

        /// <summary>
        /// Runs one write, reports it, and invalidates the tab whose data it changed so the reader
        /// sees the effect rather than being told about it.
        /// </summary>
        private async Task<ActionOutcome> Run<T>(
            Plugins.MirraCloud.Core.General.AsyncOperations.AsyncOperation<MirraCloud.Core.RestApiResult<T>> op,
            string success, int tabToRefresh)
        {
            if (op == null)
            {
                return ActionOutcome.Failure("The call could not be started.");
            }
            await op.Task();
            var result = op.Result;
            if (Ctx.Log != null && result != null)
            {
                Ctx.Log.Record("Economy write", result);
            }

            if (result == null || !result.IsSuccess)
            {
                return ActionOutcome.Failure(result != null && result.Error != null
                    ? Fmt.OrDash(result.Error.Message)
                    : "no response");
            }

            if (Toasts != null)
            {
                Toasts.Ok(success);
            }
            _tabs.Invalidate(tabToRefresh);
            _tabs.Invalidate(0);
            return ActionOutcome.Success(success);
        }

        private async Task<ActionOutcome> ConsumeAction(FormValues values)
        {
            var op = Sdk.Economy.ConsumeItemAsync(values.Text("itemId"),
                Nullable(values.Text("slotId")), Nullable(values.Text("bag")));
            if (op == null)
            {
                return ActionOutcome.Failure("The call could not be started.");
            }
            await op.Task();
            var result = op.Result;
            if (Ctx.Log != null && result != null)
            {
                Ctx.Log.Record("Consume item", result, ConsumeSnippet);
            }

            if (result == null || !result.IsSuccess)
            {
                return ActionOutcome.Failure(result != null && result.Error != null
                    ? Fmt.OrDash(result.Error.Message)
                    : "no response");
            }

            _tabs.Invalidate(0);
            _tabs.Invalidate(1);
            _tabs.Invalidate(2);
            if (Toasts != null)
            {
                Toasts.Ok("Consumed " + values.Text("itemId"));
            }
            return ActionOutcome.Success("Consumed " + values.Text("itemId"), Rewards(result.Data));
        }

        // The reward payload is the reason this call exists, so it is shown rather than summarised.
        private static VisualElement Rewards(ConsumeItemResponseDto response)
        {
            var row = new VisualElement();
            row.AddToClassList("sc-chip-row");

            if (response == null)
            {
                row.Add(new Chip("nothing granted", ChipTone.Neutral));
                return row;
            }

            if (response.GrantedCurrencies != null)
            {
                foreach (var c in response.GrantedCurrencies)
                {
                    row.Add(new RewardChip(LucideIcon.Coins, c.Key + " ×" + c.Amount));
                }
            }
            if (response.GrantedItems != null)
            {
                foreach (var i in response.GrantedItems)
                {
                    row.Add(new RewardChip(LucideIcon.Package, i.Key + " ×" + i.Quantity));
                }
            }
            if (response.GrantedEnergies != null)
            {
                foreach (var e in response.GrantedEnergies)
                {
                    row.Add(new RewardChip(LucideIcon.Zap, e.Key + " ×" + e.Amount));
                }
            }
            if (row.childCount == 0)
            {
                row.Add(new Chip("nothing granted", ChipTone.Neutral));
            }
            return row;
        }

        private async Task<ActionOutcome> PropertiesAction(FormValues values)
        {
            var properties = new Dictionary<string, object>();
            string raw = values.Text("properties");
            if (!string.IsNullOrWhiteSpace(raw))
            {
                try
                {
                    var tree = new JsonService().FromJson<JsonValue>(raw);
                    if (tree == null || tree.Type != JsonValueType.Object)
                    {
                        return ActionOutcome.Failure("Properties must be a JSON object.");
                    }
                    // JsonValue is both a list and a dictionary, and its own enumerator is the
                    // non-generic one — the cast is what picks the key/value pairs.
                    foreach (var pair in (IDictionary<string, JsonValue>)tree)
                    {
                        properties[pair.Key] = ToPlain(pair.Value);
                    }
                }
                catch (Exception e)
                {
                    return ActionOutcome.Failure("That is not valid JSON: " + e.Message);
                }
            }

            var op = Sdk.Economy.UpdateItemPropertiesAsync(values.Text("itemId"), values.Text("slotId"),
                properties, null);
            if (op == null)
            {
                return ActionOutcome.Failure("The call could not be started.");
            }
            await op.Task();
            var result = op.Result;
            if (Ctx.Log != null && result != null)
            {
                Ctx.Log.Record("Update properties", result, PropertiesSnippet);
            }

            if (result == null || !result.IsSuccess)
            {
                return ActionOutcome.Failure(result != null && result.Error != null
                    ? Fmt.OrDash(result.Error.Message)
                    : "no response");
            }

            _tabs.Invalidate(1);
            if (Toasts != null)
            {
                Toasts.Ok("Properties updated");
            }
            return ActionOutcome.Success(properties.Count + " propert"
                + (properties.Count == 1 ? "y" : "ies") + " written");
        }

        // Fmt.Json deliberately summarises a tree ("{ 4 keys }"); the definition dialog wants the
        // actual document, so it serializes instead.
        private static string Pretty(JsonValue value)
        {
            if (value == null)
            {
                return "null";
            }
            try
            {
                return new JsonService().ToJson(value, true);
            }
            catch (Exception e)
            {
                return "// could not render: " + e.Message;
            }
        }

        // The service takes plain CLR values, so the parsed JSON tree is flattened back down.
        // Objects and arrays are passed through as their JsonValue, which the serializer can write
        // back out unchanged.
        private static object ToPlain(JsonValue value)
        {
            if (value == null)
            {
                return null;
            }
            switch (value.Type)
            {
                case JsonValueType.Null: return null;
                case JsonValueType.Boolean: return (bool)value;
                case JsonValueType.Int: return (int)value;
                case JsonValueType.Double: return (double)value;
                case JsonValueType.String: return (string)value;
                default: return value;
            }
        }
    }
}
