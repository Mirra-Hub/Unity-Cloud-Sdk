using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using MirraCloud.Core;
using MirraCloud.Core.Economy.Dto;
using MirraCloud.Json;
using Plugins.MirraCloud.Core.General.AsyncOperations;
using UnityEngine.UIElements;

namespace MirraCloud.Example.Showcase
{
    /// <summary>
    /// Economy screen: what the player owns (wallet, items, energy) and what the project defines
    /// (catalog) — with every write attached to the thing it changes rather than to a form that
    /// asks for its id.
    /// <para>
    /// The wallet lists every currency the project defines, not only the ones the player already
    /// holds: a balance of zero is still a row you can top up, which is the whole point of
    /// <c>AddCurrencyAsync</c>. Items act from their own card, energy from its own meter.
    /// </para>
    /// <para>
    /// One <c>LoadInventoryAsync</c> feeds the first three tabs and one <c>LoadConfigsAsync</c>
    /// feeds the catalog and every id picker, so both are cached and the search box re-renders from
    /// memory instead of asking the server again.
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

        private const string CurrencySnippet =
@"// Currency is not an item and has its own three calls. Amounts are decimal, so a soft
// currency can carry fractions without the rounding an int would force.
var op = sdk.Economy.AddCurrencyAsync(""gold"", 100m);
await op.Task();

WalletEntryDto after = op.Result.Data;   // the balance *after* the change
// after.CurrencyId, after.Balance

// Subtract fails with economy.not_enough_currency and changes nothing when the player is
// short, unless the currency is allowed to go negative.
await sdk.Economy.SubtractCurrencyAsync(""gold"", 25m).Task();

// Set writes an exact balance — for a debug menu or a migration, not for gameplay.
await sdk.Economy.SetCurrencyAsync(""gold"", 0m).Task();";

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
// of an item rather than on the item type. Pass the same inventoryKey the slot lives in,
// or the call addresses a slot in the default bag.
var props = new Dictionary<string, object> { { ""durability"", 42 } };
await sdk.Economy.UpdateItemPropertiesAsync(""sword"", slotId, props, inventoryKey).Task();";

        private const string EnergySnippet =
@"// Energy regenerates on the server; the client only spends, tops up, or lifts the cap.
await sdk.Economy.SpendEnergyAsync(""stamina"", 1).Task();
await sdk.Economy.AddEnergyAsync(""stamina"", 5).Task();
await sdk.Economy.SetUnlimitedEnergyAsync(""stamina"", 3600).Task();

// Balances can also be read on their own, without the whole inventory — one meter, or all
// of them.
var one = sdk.Economy.GetEnergyAsync(""stamina"");
var allOfThem = sdk.Economy.GetEnergiesAsync();";

        private static readonly string[] Operations = { "add", "subtract", "set" };

        private PlayerInventoryDto _inventory;
        private EconomyConfigsDto _configs;
        private bool _configsRequested;
        private bool _walletWantsConfigs;
        private bool _closed;

        private Tabs _tabs;
        private string _query = string.Empty;

        // Each list renders into its own slot so the search box can re-filter from the cached
        // response instead of re-issuing the read on every keystroke.
        private VisualElement _walletSlot;
        private VisualElement _itemsSlot;
        private VisualElement _energySlot;
        private VisualElement _energyKpiSlot;
        private VisualElement _catalogSlot;

        public EconomyView(ServiceMeta meta, Action onBack, ShowcaseContext ctx)
            : base(meta, onBack, ctx)
        {
            RegisterCallback<DetachFromPanelEvent>(_ => _closed = true);
        }

        protected override void Populate()
        {
            _closed = false;
            _query = string.Empty;
            _inventory = null;
            _configs = null;
            _configsRequested = false;
            _walletWantsConfigs = false;
            _walletSlot = null;
            _itemsSlot = null;
            _energySlot = null;
            _energyKpiSlot = null;
            _catalogSlot = null;

            DeclareCall(new SdkCall("Read the player's economy", InventorySnippet,
                "One call behind the Wallet, Inventory and Energy tabs."));
            DeclareCall(new SdkCall("Read the project's catalog", ConfigsSnippet,
                "Also what fills the currency and item pickers, so nothing has to be typed by hand."));
            DeclareCall(new SdkCall("Add, spend and set currency", CurrencySnippet,
                "Currency is not an item — these three calls are the only way to move a balance."));
            DeclareCall(new SdkCall("Grant and take items", GrantSnippet));
            DeclareCall(new SdkCall("Consume an item", ConsumeSnippet));
            DeclareCall(new SdkCall("Update slot properties", PropertiesSnippet));
            DeclareCall(new SdkCall("Spend, refill and read energy", EnergySnippet));

            UseToolbar()
                .WithSearch("Filter by id", OnSearch)
                .WithSpacer()
                .WithRefresh(Refresh);

            _tabs = UseTabs();
            _tabs.Add("Wallet", LucideIcon.Wallet, BuildWallet)
                .Add("Inventory", LucideIcon.Package, BuildInventory)
                .Add("Energy", LucideIcon.Zap, BuildEnergy)
                .Add("Catalog", LucideIcon.Boxes, BuildCatalog);

            LoadConfigs();
        }

        private void OnSearch(string text)
        {
            string next = text == null ? string.Empty : text.Trim();
            if (string.Equals(next, _query, StringComparison.Ordinal))
            {
                return;
            }
            _query = next;

            // Everything on this screen is already in memory, so filtering is a re-render.
            RenderWallet();
            RenderItems();
            RenderEnergies();
            RenderCatalog();
        }

        private bool Matches(params string[] fields)
        {
            if (_query.Length == 0)
            {
                return true;
            }
            foreach (var field in fields)
            {
                if (field != null && field.IndexOf(_query, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        private Label FilterNote(int shown, int total)
        {
            return Hint(_query.Length == 0
                ? string.Empty
                : shown + " of " + total + " match \"" + Fmt.Truncate(_query, 24) + "\"");
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

        /// <summary>
        /// Reads the catalog once per screen. It is not just the Catalog tab's data: the wallet
        /// lists currencies from it, and every "which item?" picker is filled from it.
        /// </summary>
        private async void LoadConfigs()
        {
            if (_configsRequested)
            {
                return;
            }
            _configsRequested = true;

            var op = Sdk.Economy.LoadConfigsAsync();
            if (op == null)
            {
                return;
            }
            await op.Task();
            var result = op.Result;
            if (_closed || result == null || !result.IsSuccess || result.Data == null)
            {
                return;
            }
            _configs = result.Data;

            // The wallet renders from the cached inventory, so re-filling its slot costs nothing.
            // Only a wallet that fell all the way through to its zero state has to be rebuilt.
            if (_walletSlot != null)
            {
                RenderWallet();
            }
            else if (_walletWantsConfigs)
            {
                _tabs.Invalidate(0);
            }
        }

        private static int Count<T>(Dictionary<string, T> map)
        {
            return map == null ? 0 : map.Count;
        }

        // ----- wallet ---------------------------------------------------------------------------

        private VisualElement BuildWallet()
        {
            return InventoryPane(
                data =>
                {
                    var rows = MergedWallet(data);
                    var col = new VisualElement();

                    var kpis = new KpiRow();
                    decimal total = 0m;
                    int held = 0;
                    foreach (var row in rows)
                    {
                        total += row.Balance;
                        if (row.Held)
                        {
                            held++;
                        }
                    }
                    if (rows.Count == 0)
                    {
                        kpis.AddZero("Currencies", LucideIcon.Coins).AddZero("Total held", LucideIcon.Sigma);
                    }
                    else
                    {
                        kpis.Add("Currencies", LucideIcon.Coins, held + " / " + rows.Count)
                            .Add("Total held", LucideIcon.Sigma, Fmt.Number((double)total));
                    }
                    col.Add(kpis);

                    var chart = new BarChart(170f);
                    var points = new List<ChartPoint>();
                    foreach (var row in rows)
                    {
                        points.Add(new ChartPoint(row.CurrencyId, (float)row.Balance));
                    }
                    chart.SetData(points).SetEmptyText("No currencies in this project yet");
                    col.Add(chart);

                    var bar = new VisualElement();
                    bar.AddToClassList("sc-row-actions");
                    bar.style.justifyContent = Justify.SpaceBetween;
                    bar.Add(new SectionHeader("Balances", rows.Count.ToString()));
                    bar.Add(GlyphButton("Add currency", LucideIcon.CirclePlus,
                        () => OpenGrantCurrencyDialog(), "sc-btn--primary"));
                    col.Add(bar);

                    col.Add(Hint("Every currency the project defines is listed, held or not — a zero "
                        + "balance is still a row you can top up."));

                    _walletSlot = new VisualElement();
                    col.Add(_walletSlot);
                    RenderWallet();
                    return col;
                },
                data => MergedWallet(data).Count == 0,
                () =>
                {
                    // No wallet and no catalog either — possibly just because the catalog has not
                    // answered yet, in which case LoadConfigs rebuilds this pane.
                    _walletWantsConfigs = _configs == null;
                    return ZeroState.Table(WalletColumns(),
                        "Currencies are defined in the Mirra Hub console. Once one exists it shows up "
                        + "here with a zero balance, ready to be topped up.",
                        3, "Add currency", () => OpenGrantCurrencyDialog());
                });
        }

        /// <summary>
        /// The player's balances plus every currency the project defines that they do not hold yet.
        /// Without the second half there is no row to press "add" on, which is exactly the state a
        /// fresh account starts in.
        /// </summary>
        private List<WalletRow> MergedWallet(PlayerInventoryDto data)
        {
            var rows = new List<WalletRow>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            if (data != null && data.Wallet != null)
            {
                foreach (var entry in data.Wallet)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.CurrencyId))
                    {
                        continue;
                    }
                    seen.Add(entry.CurrencyId);
                    rows.Add(new WalletRow { CurrencyId = entry.CurrencyId, Balance = entry.Balance, Held = true });
                }
            }

            if (_configs != null && _configs.Currencies != null)
            {
                foreach (var pair in _configs.Currencies)
                {
                    if (!string.IsNullOrEmpty(pair.Key) && seen.Add(pair.Key))
                    {
                        rows.Add(new WalletRow { CurrencyId = pair.Key, Balance = 0m, Held = false });
                    }
                }
            }
            return rows;
        }

        private void RenderWallet()
        {
            if (_walletSlot == null)
            {
                return;
            }
            _walletSlot.Clear();

            var all = MergedWallet(_inventory);
            var shown = new List<WalletRow>();
            foreach (var row in all)
            {
                if (Matches(row.CurrencyId))
                {
                    shown.Add(row);
                }
            }

            if (shown.Count == 0)
            {
                _walletSlot.Add(Hint("No currency id matches \"" + Fmt.Truncate(_query, 24) + "\"."));
                return;
            }

            _walletSlot.Add(FilterNote(shown.Count, all.Count));
            var table = new DataTable(WalletColumns()).WithZebra().WithSort(1, false);
            table.Bind(shown);
            _walletSlot.Add(table);
        }

        private DataColumn[] WalletColumns()
        {
            return new[]
            {
                new DataColumn
                {
                    Header = "CURRENCY", Grow = 2f,
                    SortKey = o => ((WalletRow)o).CurrencyId,
                    Cell = o =>
                    {
                        var row = (WalletRow)o;
                        var line = new VisualElement();
                        line.AddToClassList("sc-row-actions");
                        line.style.justifyContent = Justify.FlexStart;

                        var label = new Label(Fmt.OrDash(row.CurrencyId));
                        label.enableRichText = false;
                        line.Add(label);

                        if (!row.Held)
                        {
                            var badge = new Badge("never held", ChipTone.Neutral);
                            badge.style.marginLeft = 8f;
                            line.Add(badge);
                        }
                        return line;
                    },
                },
                new DataColumn
                {
                    Header = "BALANCE", Grow = 1f, Align = "right",
                    SortKey = o => (double)((WalletRow)o).Balance,
                    Cell = o => new Label(Fmt.Number((double)((WalletRow)o).Balance)),
                },
                new DataColumn
                {
                    Header = string.Empty, FixedWidth = true, Px = 168, Align = "right",
                    Cell = o =>
                    {
                        var row = (WalletRow)o;
                        var line = new VisualElement();
                        line.AddToClassList("sc-row-actions");
                        line.Add(GlyphButton("Add", LucideIcon.Plus,
                            () => OpenCurrencyDialog(row.CurrencyId, true)));
                        line.Add(GlyphButton("Change", LucideIcon.SlidersHorizontal,
                            () => OpenCurrencyDialog(row.CurrencyId, false)));
                        return line;
                    },
                },
            };
        }

        /// <summary>Picks the currency first, for the case where no row exists to press "add" on.</summary>
        private void OpenGrantCurrencyDialog()
        {
            var options = Keys(_configs != null ? _configs.Currencies : null);
            FormField picker = options.Count > 0
                ? FormField.Choice("currency", "Currency", options.ToArray(), options[0])
                : FormField.Text("currency", "Currency id", null, true)
                    .WithPlaceholder("The catalog has not answered, so the id has to be typed");

            FormDialog.Open(Popup, "Add currency",
                new[] { picker, AmountField("Amount", 100m) },
                "Add",
                values => ChangeCurrency(values.Text("currency"), "add", Amount(values)));
        }

        private void OpenCurrencyDialog(string currencyId, bool addOnly)
        {
            var fields = new List<FormField>();
            if (!addOnly)
            {
                fields.Add(FormField.Choice("op", "Operation", Operations, "add")
                    .WithPlaceholder("subtract fails when the balance is too low; set writes it exactly"));
            }
            fields.Add(AmountField("Amount", 100m));

            FormDialog.Open(Popup,
                (addOnly ? "Add to " : "Change ") + Fmt.Truncate(Fmt.OrDash(currencyId), 24),
                fields,
                addOnly ? "Add" : "Apply",
                values => ChangeCurrency(currencyId, addOnly ? "add" : values.Choice("op"), Amount(values)));
        }

        private async void ChangeCurrency(string currencyId, string operation, decimal amount)
        {
            string id = currencyId == null ? null : currencyId.Trim();
            if (string.IsNullOrEmpty(id))
            {
                Warn("Pick a currency first.");
                return;
            }

            AsyncOperation<RestApiResult<WalletEntryDto>> op;
            if (operation == "subtract")
            {
                op = Sdk.Economy.SubtractCurrencyAsync(id, amount);
            }
            else if (operation == "set")
            {
                op = Sdk.Economy.SetCurrencyAsync(id, amount);
            }
            else
            {
                op = Sdk.Economy.AddCurrencyAsync(id, amount);
            }

            var outcome = await AwaitData(op, "Economy · currency " + operation);
            if (!outcome.Ok)
            {
                Fail(outcome, "Currency " + operation);
                return;
            }

            // The call answers with the balance after the change, so the toast can state the new
            // number instead of just "done".
            var after = op.Result.Data;
            Ok(after != null
                ? id + " · " + Fmt.Number((double)after.Balance)
                : id + " updated");
            if (!_closed)
            {
                _tabs.Invalidate(0);
            }
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

                    var bar = new VisualElement();
                    bar.AddToClassList("sc-row-actions");
                    bar.style.justifyContent = Justify.SpaceBetween;
                    bar.Add(new SectionHeader("Slots", items.Count.ToString()));
                    bar.Add(GlyphButton("Grant an item", LucideIcon.Plus,
                        () => OpenGrantItemDialog(null, null), "sc-btn--primary"));
                    col.Add(bar);

                    col.Add(Hint("A slot opens with everything you can do to that copy: grant more, "
                        + "take some away, consume it, or write its properties."));

                    _itemsSlot = new VisualElement();
                    col.Add(_itemsSlot);
                    RenderItems();
                    return col;
                },
                data => data == null || data.Items == null || data.Items.Count == 0,
                () => ZeroState.Cards(LucideIcon.Package,
                    "This player holds no items yet. Grant one and it shows up here as a slot.",
                    4, "Grant an item", () => OpenGrantItemDialog(null, null)));
        }

        private void RenderItems()
        {
            if (_itemsSlot == null)
            {
                return;
            }
            _itemsSlot.Clear();

            var all = _inventory != null && _inventory.Items != null
                ? _inventory.Items
                : new List<ItemSlotDto>();
            var shown = new List<ItemSlotDto>();
            foreach (var slot in all)
            {
                if (slot != null && Matches(slot.ItemId, slot.InventoryKey))
                {
                    shown.Add(slot);
                }
            }

            if (shown.Count == 0)
            {
                _itemsSlot.Add(Hint("No slot matches \"" + Fmt.Truncate(_query, 24) + "\"."));
                return;
            }

            _itemsSlot.Add(FilterNote(shown.Count, all.Count));
            var grid = new VisualElement();
            grid.AddToClassList("sc-item-grid");
            foreach (var slot in shown)
            {
                grid.Add(ItemCard(slot));
            }
            _itemsSlot.Add(grid);
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

        /// <summary>
        /// One slot in full: the ids the write calls need, its properties, and the five calls that
        /// act on it — each already knowing the item, the slot and the bag.
        /// </summary>
        private void ShowItem(ItemSlotDto slot)
        {
            if (Popup == null)
            {
                return;
            }

            var body = new ScrollView(ScrollViewMode.Vertical);
            body.style.maxHeight = 460f;

            var actions = new VisualElement();
            actions.AddToClassList("sc-eco-actions");
            actions.Add(GlyphButton("Grant more", LucideIcon.Plus,
                () => OpenGrantItemDialog(slot.ItemId, slot.InventoryKey), "sc-btn--primary"));
            actions.Add(GlyphButton("Take", LucideIcon.Minus, () => OpenTakeItemDialog(slot, false)));
            actions.Add(GlyphButton("Take (safe)", LucideIcon.ShieldCheck,
                () => OpenTakeItemDialog(slot, true)));
            actions.Add(GlyphButton("Consume", LucideIcon.Gift, () => ConfirmConsume(slot)));
            actions.Add(GlyphButton("Properties", LucideIcon.Braces, () => OpenPropertiesDialog(slot)));
            body.Add(actions);

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
                    + "differs between two otherwise identical items.",
                    "Write properties", () => OpenPropertiesDialog(slot)));
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

        private void OpenGrantItemDialog(string itemId, string bag)
        {
            var fields = new List<FormField>();
            if (string.IsNullOrEmpty(itemId))
            {
                var options = Keys(_configs != null ? _configs.Items : null);
                fields.Add(options.Count > 0
                    ? FormField.Choice("itemId", "Item", options.ToArray(), options[0])
                    : FormField.Text("itemId", "Item id", null, true)
                        .WithPlaceholder("The catalog has not answered, so the id has to be typed"));
            }
            fields.Add(FormField.Int("amount", "Amount", 1));
            fields.Add(FormField.Text("bag", "Bag", bag)
                .WithPlaceholder("Leave empty for the default bag"));

            FormDialog.Open(Popup,
                string.IsNullOrEmpty(itemId) ? "Grant an item" : "Grant more " + Fmt.Truncate(itemId, 22),
                fields, "Grant",
                values =>
                {
                    string id = string.IsNullOrEmpty(itemId) ? values.Text("itemId") : itemId;
                    Write(Sdk.Economy.AddItemAsync(id, values.Int("amount"), Blank(values.Text("bag"))),
                        "Granted " + values.Int("amount") + " × " + id, "Grant item");
                });
        }

        private void OpenTakeItemDialog(ItemSlotDto slot, bool safe)
        {
            FormDialog.Open(Popup,
                (safe ? "Take (safe) " : "Take ") + Fmt.Truncate(Fmt.OrDash(slot.ItemId), 22),
                new[]
                {
                    FormField.Int("amount", "Amount", 1)
                        .WithPlaceholder(safe
                            ? "Clamps to what the player has"
                            : "Fails outright when the player is short"),
                },
                "Take",
                values =>
                {
                    int amount = values.Int("amount");
                    var op = safe
                        ? Sdk.Economy.SubtractItemSafeAsync(slot.ItemId, amount, Blank(slot.InventoryKey))
                        : Sdk.Economy.SubtractItemAsync(slot.ItemId, amount, Blank(slot.InventoryKey));
                    Write(op, (safe ? "Took up to " : "Took ") + amount + " × " + slot.ItemId,
                        "Take item");
                });
        }

        private void ConfirmConsume(ItemSlotDto slot)
        {
            ConfirmDialog.Open(Popup, "Consume " + Fmt.Truncate(Fmt.OrDash(slot.ItemId), 22),
                "Spends one copy from this slot and returns whatever it granted — the loot-box call.",
                "Consume",
                () => Consume(slot),
                null,
                false);
        }

        private async void Consume(ItemSlotDto slot)
        {
            var op = Sdk.Economy.ConsumeItemAsync(slot.ItemId, slot.SlotId, Blank(slot.InventoryKey));
            var outcome = await AwaitData(op, "Economy · consume");
            if (!outcome.Ok)
            {
                Fail(outcome, "Consume");
                return;
            }

            Ok("Consumed " + slot.ItemId);
            if (_closed)
            {
                return;
            }
            _tabs.Invalidate(0);
            _tabs.Invalidate(1);
            _tabs.Invalidate(2);

            // The reward payload is the reason this call exists, so it is shown rather than dropped.
            if (Popup != null)
            {
                var body = new VisualElement();
                body.Add(Hint("What the item granted:"));
                body.Add(Rewards(op.Result.Data));
                Popup.Open(body, "Consumed " + Fmt.Truncate(Fmt.OrDash(slot.ItemId), 24));
            }
        }

        private void OpenPropertiesDialog(ItemSlotDto slot)
        {
            FormDialog.Open(Popup, "Slot properties",
                new[]
                {
                    FormField.Json("properties", "Properties", CurrentProperties(slot))
                        .WithPlaceholder("A JSON object; it replaces what the slot carries today"),
                },
                "Write",
                values => WriteProperties(slot, values.Text("properties")));
        }

        /// <summary>What the slot holds now, as an editable JSON object rather than a blank field.</summary>
        private static string CurrentProperties(ItemSlotDto slot)
        {
            if (slot.Properties == null || slot.Properties.Count == 0)
            {
                return "{\n  \"durability\": 42\n}";
            }
            var tree = new JsonValue(JsonValueType.Object);
            foreach (var pair in slot.Properties)
            {
                tree[pair.Key] = FromPlain(pair.Value);
            }
            return Pretty(tree);
        }

        private async void WriteProperties(ItemSlotDto slot, string raw)
        {
            var properties = new Dictionary<string, object>();
            if (!string.IsNullOrWhiteSpace(raw))
            {
                try
                {
                    var tree = new JsonService().FromJson<JsonValue>(raw);
                    if (tree == null || tree.Type != JsonValueType.Object)
                    {
                        Warn("Properties must be a JSON object.");
                        return;
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
                    Warn("That is not valid JSON: " + e.Message);
                    return;
                }
            }

            // The bag matters: without it the call addresses a slot in the default inventory, which
            // is a different slot than the one that was opened.
            var op = Sdk.Economy.UpdateItemPropertiesAsync(slot.ItemId, slot.SlotId, properties,
                Blank(slot.InventoryKey));
            var outcome = await AwaitData(op, "Economy · properties");
            if (!outcome.Ok)
            {
                Fail(outcome, "Properties");
                return;
            }
            Ok(properties.Count + " propert" + (properties.Count == 1 ? "y" : "ies") + " written");
            if (!_closed)
            {
                _tabs.Invalidate(1);
            }
        }

        // The reward payload of a consume, as chips.
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

        // ----- energy ---------------------------------------------------------------------------

        private VisualElement BuildEnergy()
        {
            return InventoryPane(
                data =>
                {
                    var col = new VisualElement();

                    // The counts move with a reload just like the cards do, so they share a slot.
                    _energyKpiSlot = new VisualElement();
                    col.Add(_energyKpiSlot);

                    var bar = new VisualElement();
                    bar.AddToClassList("sc-row-actions");
                    bar.style.justifyContent = Justify.SpaceBetween;
                    bar.Add(new SectionHeader("Meters",
                        (data.Energies != null ? data.Energies.Count : 0).ToString()));
                    bar.Add(GlyphButton("Reload meters", LucideIcon.RefreshCw, ReloadAllEnergies));
                    col.Add(bar);

                    col.Add(Hint("Energy refills on the server. \"Reload meters\" re-reads them with "
                        + "GetEnergiesAsync, without pulling the whole inventory again."));

                    _energySlot = new VisualElement();
                    col.Add(_energySlot);
                    RenderEnergies();
                    return col;
                },
                data => data == null || data.Energies == null || data.Energies.Count == 0,
                () => ZeroState.Panel(LucideIcon.Zap, "No energy meters",
                    "Energy is a stamina-style resource that refills over time on the server. Define one "
                    + "in the Mirra Hub console and the player's meter, recharge timer and cooldown "
                    + "appear here."));
        }

        private static KpiRow EnergyKpis(List<EnergyBalanceDto> energies)
        {
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
            return new KpiRow()
                .Add("Meters", LucideIcon.Zap, energies.Count.ToString())
                .Add("At full", LucideIcon.CircleCheck, full.ToString())
                .Add("Unlimited", LucideIcon.Sparkles, unlimited.ToString(), null, unlimited > 0);
        }

        private void RenderEnergies()
        {
            if (_energySlot == null)
            {
                return;
            }
            _energySlot.Clear();

            var all = _inventory != null && _inventory.Energies != null
                ? _inventory.Energies
                : new List<EnergyBalanceDto>();

            if (_energyKpiSlot != null)
            {
                _energyKpiSlot.Clear();
                _energyKpiSlot.Add(EnergyKpis(all));
            }

            var shown = new List<EnergyBalanceDto>();
            foreach (var energy in all)
            {
                if (energy != null && Matches(energy.EnergyId))
                {
                    shown.Add(energy);
                }
            }

            if (shown.Count == 0)
            {
                _energySlot.Add(Hint("No meter matches \"" + Fmt.Truncate(_query, 24) + "\"."));
                return;
            }

            _energySlot.Add(FilterNote(shown.Count, all.Count));
            foreach (var energy in shown)
            {
                // Each meter owns a slot so its own reload can replace just that card.
                var host = new VisualElement();
                host.Add(EnergyCard(energy, host));
                _energySlot.Add(host);
            }
        }

        private VisualElement EnergyCard(EnergyBalanceDto energy, VisualElement host)
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

            string id = energy.EnergyId;
            var actions = new VisualElement();
            actions.AddToClassList("sc-eco-actions");
            actions.Add(GlyphButton("Spend", LucideIcon.Minus, () => OpenEnergyDialog(id, false)));
            actions.Add(GlyphButton("Refill", LucideIcon.Plus, () => OpenEnergyDialog(id, true),
                "sc-btn--primary"));
            actions.Add(GlyphButton("Unlimited", LucideIcon.Sparkles, () => OpenUnlimitedDialog(id)));
            actions.Add(GlyphButton("Reload", LucideIcon.RefreshCw, () => ReloadEnergy(id, host)));
            card.Body.Add(actions);
            return card;
        }

        private void OpenEnergyDialog(string energyId, bool refill)
        {
            FormDialog.Open(Popup,
                (refill ? "Refill " : "Spend ") + Fmt.Truncate(Fmt.OrDash(energyId), 22),
                new[] { FormField.Int("amount", "Amount", refill ? 5 : 1) },
                refill ? "Refill" : "Spend",
                values =>
                {
                    int amount = values.Int("amount");
                    var op = refill
                        ? Sdk.Economy.AddEnergyAsync(energyId, amount)
                        : Sdk.Economy.SpendEnergyAsync(energyId, amount);
                    Write(op, (refill ? "Added " : "Spent ") + amount + " " + energyId, "Energy", 2);
                });
        }

        private void OpenUnlimitedDialog(string energyId)
        {
            FormDialog.Open(Popup, "Unlimited " + Fmt.Truncate(Fmt.OrDash(energyId), 22),
                new[]
                {
                    FormField.Int("seconds", "Duration (seconds)", 3600)
                        .WithPlaceholder("Lifts the cap for a while — the timed booster pattern"),
                },
                "Grant",
                values => Write(Sdk.Economy.SetUnlimitedEnergyAsync(energyId, values.Int("seconds")),
                    "Unlimited " + energyId + " for "
                        + Fmt.Duration(TimeSpan.FromSeconds(values.Int("seconds"))),
                    "Unlimited energy", 2));
        }

        /// <summary>One meter, re-read on its own — the reason <c>GetEnergyAsync</c> exists.</summary>
        private async void ReloadEnergy(string energyId, VisualElement host)
        {
            var op = Sdk.Economy.GetEnergyAsync(energyId);
            var outcome = await AwaitData(op, "Economy · energy");
            if (!outcome.Ok)
            {
                Fail(outcome, "Read energy");
                return;
            }
            if (_closed || host.panel == null)
            {
                return;
            }

            var fresh = op.Result.Data;
            if (fresh == null)
            {
                return;
            }
            Replace(_inventory != null ? _inventory.Energies : null, fresh);
            host.Clear();
            host.Add(EnergyCard(fresh, host));
        }

        /// <summary>Every meter, re-read without pulling the wallet and the items along with them.</summary>
        private async void ReloadAllEnergies()
        {
            var op = Sdk.Economy.GetEnergiesAsync();
            var outcome = await AwaitData(op, "Economy · energies");
            if (!outcome.Ok)
            {
                Fail(outcome, "Read energies");
                return;
            }
            if (_closed || _inventory == null)
            {
                return;
            }
            _inventory.Energies = op.Result.Data ?? new List<EnergyBalanceDto>();
            RenderEnergies();
        }

        private static void Replace(List<EnergyBalanceDto> list, EnergyBalanceDto fresh)
        {
            if (list == null || fresh == null)
            {
                return;
            }
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && string.Equals(list[i].EnergyId, fresh.EnergyId, StringComparison.Ordinal))
                {
                    list[i] = fresh;
                    return;
                }
            }
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

        private VisualElement BuildCatalogBody(EconomyConfigsDto configs)
        {
            _configs = configs;

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

            _catalogSlot = new VisualElement();
            col.Add(_catalogSlot);
            RenderCatalog();
            return col;
        }

        private void RenderCatalog()
        {
            if (_catalogSlot == null)
            {
                return;
            }
            _catalogSlot.Clear();
            if (_configs == null)
            {
                return;
            }
            _catalogSlot.Add(CatalogSection("Currencies", _configs.Currencies, CatalogKind.Currency));
            _catalogSlot.Add(CatalogSection("Items", _configs.Items, CatalogKind.Item));
            _catalogSlot.Add(CatalogSection("Energies", _configs.Energies, CatalogKind.Energy));
        }

        private VisualElement CatalogSection(string title, Dictionary<string, EconomySdkResourceDto> map,
                                             CatalogKind kind)
        {
            var box = new VisualElement();

            var shown = new List<KeyValuePair<string, EconomySdkResourceDto>>();
            if (map != null)
            {
                foreach (var pair in map)
                {
                    if (Matches(pair.Key))
                    {
                        shown.Add(pair);
                    }
                }
            }

            box.Add(new SectionHeader(title,
                _query.Length == 0
                    ? Count(map).ToString()
                    : shown.Count + " / " + Count(map)));

            if (map == null || map.Count == 0)
            {
                box.Add(ZeroState.Panel(LucideIcon.Braces, "No " + title.ToLowerInvariant(),
                    "None are defined in this project."));
                return box;
            }
            if (shown.Count == 0)
            {
                box.Add(Hint("Nothing here matches \"" + Fmt.Truncate(_query, 24) + "\"."));
                return box;
            }

            var list = new VisualElement();
            foreach (var pair in shown)
            {
                var row = new ListRow();
                row.AddToClassList("sc-eco-row");
                row.SetTitle(pair.Key);
                row.SetSubtitle(Describe(pair.Value));

                var trailing = new VisualElement();
                trailing.AddToClassList("sc-row-actions");
                trailing.Add(new CopyButton(pair.Key, Toasts, "id"));

                // A definition is only interesting because a player can be given one, so the row
                // offers that directly instead of sending the reader to a form.
                string key = pair.Key;
                switch (kind)
                {
                    case CatalogKind.Currency:
                        trailing.Add(GlyphButton("Add", LucideIcon.Coins,
                            () => OpenCurrencyDialog(key, true)));
                        break;
                    case CatalogKind.Item:
                        trailing.Add(GlyphButton("Grant", LucideIcon.Package,
                            () => OpenGrantItemDialog(key, null)));
                        break;
                    case CatalogKind.Energy:
                        trailing.Add(GlyphButton("Refill", LucideIcon.Zap,
                            () => OpenEnergyDialog(key, true)));
                        break;
                }
                row.SetTrailing(trailing);

                var definition = pair.Value;
                row.RegisterCallback<ClickEvent>(e =>
                {
                    for (var t = e.target as VisualElement; t != null && t != row; t = t.parent)
                    {
                        if (t is Button)
                        {
                            return;
                        }
                    }
                    ShowDefinition(key, definition);
                });
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

        // ----- small shared pieces ---------------------------------------------------------------

        private static Label Hint(string text)
        {
            var label = new Label(text ?? string.Empty);
            label.enableRichText = false;
            label.AddToClassList("sc-fs-hint");
            label.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
            return label;
        }

        /// <summary>Button with a leading Lucide glyph — the icon has to be its own label.</summary>
        private static Button GlyphButton(string text, string glyph, Action onClick, string tone = null)
        {
            var btn = new Button(() => onClick?.Invoke());
            btn.AddToClassList("sc-btn");
            btn.AddToClassList("sc-grp-btn");
            if (!string.IsNullOrEmpty(tone))
            {
                btn.AddToClassList(tone);
            }

            var g = new Label(glyph);
            g.AddToClassList("sc-grp-btn__glyph");
            g.AddToClassList("sc-icon");
            btn.Add(g);

            var t = new Label(text);
            t.enableRichText = false;
            btn.Add(t);
            return btn;
        }

        /// <summary>
        /// Validated as a number so the field catches a typo, but read back as text: money is
        /// decimal all the way to the wire, and a float round-trip would quietly lose cents.
        /// </summary>
        private static FormField AmountField(string label, decimal def)
        {
            return FormField.Float("amount", label)
                .WithDefault(def.ToString(CultureInfo.InvariantCulture));
        }

        private static decimal Amount(FormValues values)
        {
            return decimal.TryParse(values.Text("amount").Trim(), NumberStyles.Float,
                CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : 0m;
        }

        private static List<string> Keys<T>(Dictionary<string, T> map)
        {
            var keys = new List<string>();
            if (map != null)
            {
                foreach (var pair in map)
                {
                    if (!string.IsNullOrEmpty(pair.Key))
                    {
                        keys.Add(pair.Key);
                    }
                }
                keys.Sort(StringComparer.Ordinal);
            }
            return keys;
        }

        private static string Blank(string text)
        {
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
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

        /// <summary>The inverse, so a slot's stored properties can be shown as editable JSON.</summary>
        private static JsonValue FromPlain(object value)
        {
            if (value == null)
            {
                return new JsonValue(JsonValueType.Null);
            }
            var already = value as JsonValue;
            if (already != null)
            {
                return already;
            }
            if (value is bool)
            {
                return new JsonValue((bool)value);
            }
            if (value is int)
            {
                return new JsonValue((int)value);
            }
            if (value is long)
            {
                return new JsonValue((int)(long)value);
            }
            if (value is float)
            {
                return new JsonValue((double)(float)value);
            }
            if (value is double)
            {
                return new JsonValue((double)value);
            }
            if (value is decimal)
            {
                return new JsonValue((double)(decimal)value);
            }
            return new JsonValue(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);
        }

        // ----- shared plumbing ------------------------------------------------------------------

        private async void Write<T>(AsyncOperation<RestApiResult<T>> op, string success, string label,
                                    int extraTab = 1)
        {
            var outcome = await AwaitData(op, "Economy · " + label);
            if (!outcome.Ok)
            {
                Fail(outcome, label);
                return;
            }
            Ok(success);
            if (_closed)
            {
                return;
            }
            // The wallet always moves with a write that grants something, so it is refreshed
            // alongside whichever tab the call belongs to.
            _tabs.Invalidate(0);
            _tabs.Invalidate(extraTab);
        }

        private void Ok(string message)
        {
            if (Toasts != null && !string.IsNullOrEmpty(message))
            {
                Toasts.Ok(message);
            }
        }

        private void Fail(Outcome outcome, string label)
        {
            Warn(label + " failed · " + outcome.Message);
        }

        private void Warn(string message)
        {
            if (Toasts != null)
            {
                Toasts.Fail(message);
            }
        }

        private async Task<Outcome> AwaitData<T>(AsyncOperation<RestApiResult<T>> op, string label)
        {
            if (op == null)
            {
                return new Outcome { Ok = false, Message = "the call could not be started" };
            }
            await op.Task();

            var result = op.Result;
            if (Ctx.Log != null && result != null)
            {
                Ctx.Log.Record(label, result);
            }
            if (result != null && result.IsSuccess)
            {
                return new Outcome { Ok = true };
            }
            string message = result != null && result.Error != null && !string.IsNullOrEmpty(result.Error.Message)
                ? result.Error.Message
                : "no response";
            return new Outcome { Ok = false, Message = message };
        }

        private enum CatalogKind { Currency, Item, Energy }

        private sealed class WalletRow
        {
            public string CurrencyId;
            public decimal Balance;

            /// <summary>False for a currency that only exists in the catalog — balance is a real 0.</summary>
            public bool Held;
        }

        private struct Outcome
        {
            public bool Ok;
            public string Message;
        }
    }
}
