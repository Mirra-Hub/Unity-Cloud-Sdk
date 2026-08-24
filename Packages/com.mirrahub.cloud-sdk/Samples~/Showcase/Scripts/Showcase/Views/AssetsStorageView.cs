using System;
using System.Collections.Generic;
using MirraCloud.Core.AssetsStorage;
using UnityEngine;
using UnityEngine.UIElements;

namespace MirraCloud.Example.Showcase
{
    /// <summary>
    /// Asset Storage screen, shaped like a file manager rather than a data dump: one browsable
    /// catalog where folders and files live together, breadcrumbs to walk back up, and image assets
    /// showing their actual picture.
    /// <para>
    /// The service returns the whole branch in one call (<c>LoadConfigAsync</c>) as two flat lists,
    /// so the hierarchy is rebuilt here from <c>FolderDto.parentFolderId</c>. Previews are fetched
    /// one asset at a time, which is why they are lazy, capped, cached and abandoned when the screen
    /// closes — a branch with a hundred images would otherwise open a hundred requests at once.
    /// </para>
    /// Read-only: the SDK downloads assets but never uploads them, so there is no Actions tab.
    /// </summary>
    public sealed class AssetsStorageView : ServiceView
    {
        private const string StructureSnippet =
@"// The whole branch in one call: every folder and every asset, as two flat lists.
var op = sdk.AssetsStorage.LoadConfigAsync();
await op.Task();

var result = op.Result;
if (result.IsSuccess)
{
    AssetStorageStructureDto s = result.Data;
    // s.folders: itemId, name, path, parentFolderId, createdAt, updatedAt
    // s.assets:  itemId, name, path, folderId, type, mimeType, extension, size, version
    // The service keeps them too: sdk.AssetsStorage.Assets / .Folders
}";

        private const string TextureSnippet =
@"// One image by id. DownloadHandlerTexture is used under the hood, so the texture
// arrives ready to assign — no manual byte decoding.
var op = sdk.AssetsStorage.LoadTextureFromId(assetId);
await op.Task();

if (op.Result.IsSuccess)
{
    Texture2D texture = op.Result.Data;
    image.image = texture;
}";

        private const string TextSnippet =
@"// Text or JSON. ExtractTextFileType.All also fills TextFile.Data with the raw bytes.
var op = sdk.AssetsStorage.LoadTextFromId(assetId, ExtractTextFileType.Text);
await op.Task();

if (op.Result.IsSuccess)
{
    string body = op.Result.Data.Text;
}";

        private const string AudioSnippet =
@"// Audio. The AudioType has to match the file — the handler cannot guess it.
var op = sdk.AssetsStorage.LoadAudioFromId(assetId, AudioType.MPEG);
await op.Task();

if (op.Result.IsSuccess)
{
    audioSource.clip = op.Result.Data;
    audioSource.Play();
}";

        private const string PublicSnippet =
@"// Anonymous fetch: no signed-in player, no token. The asset must be marked public in
// the console — a private one answers 403 instead.
var op = sdk.AssetsStorage.LoadPublicTextureFromId(assetId);
await op.Task();

bool servedAnonymously = op.Result.IsSuccess;";

        // At most this many preview downloads run at once; the rest wait in _queue. A branch with a
        // hundred images must not open a hundred sockets on the first frame.
        private const int MaxParallelPreviews = 4;

        // Previews start when a card comes within this many pixels of the viewport, so scrolling
        // finds them already loaded instead of kicking off the request on arrival.
        private const float PrefetchMargin = 240f;

        private const int GridPageSize = 60;

        private readonly Dictionary<string, Texture2D> _textures = new Dictionary<string, Texture2D>();
        private readonly HashSet<string> _failedTextures = new HashSet<string>();
        private readonly List<PreviewRequest> _queue = new List<PreviewRequest>();

        // Parent id -> direct children. Rebuilt on every load; the empty-string key is the root.
        private readonly Dictionary<string, List<FolderDto>> _childFolders =
            new Dictionary<string, List<FolderDto>>();
        private readonly Dictionary<string, List<AssetDto>> _folderAssets =
            new Dictionary<string, List<AssetDto>>();
        private readonly Dictionary<string, FolderDto> _foldersById = new Dictionary<string, FolderDto>();

        private List<AssetDto> _allAssets = new List<AssetDto>();
        private List<FolderDto> _allFolders = new List<FolderDto>();

        private VisualElement _browserSlot;
        private VisualElement _crumbSlot;
        private string _currentFolderId = string.Empty;
        private string _search = string.Empty;
        private bool _listMode;
        private int _inFlight;
        private bool _closed;
        private int _shownInGrid;

        public AssetsStorageView(ServiceMeta meta, Action onBack, ShowcaseContext ctx)
            : base(meta, onBack, ctx)
        {
            // Abandon everything still queued once the screen goes away: those results would be
            // applied to elements that are no longer in any panel.
            RegisterCallback<DetachFromPanelEvent>(_ => Close());
        }

        protected override void Populate()
        {
            _closed = false;
            _search = string.Empty;
            _currentFolderId = string.Empty;
            _queue.Clear();
            _browserSlot = null;
            _crumbSlot = null;

            DeclareCall(new SdkCall("Read the branch structure", StructureSnippet,
                "One request feeds the whole screen — the folder tree is rebuilt on the client."));
            DeclareCall(new SdkCall("Download an image", TextureSnippet,
                "Issued per visible card, at most " + MaxParallelPreviews
                + " at a time, and cached for the session."));
            DeclareCall(new SdkCall("Download text or JSON", TextSnippet));
            DeclareCall(new SdkCall("Download audio", AudioSnippet));
            DeclareCall(new SdkCall("Download a public asset anonymously", PublicSnippet,
                "Works without a signed-in player; a private asset answers 403."));

            // A dropdown rather than a toggle button: the picker shows which mode is active without
            // this screen having to keep a button label in sync with the flag.
            UseToolbar()
                .WithSearch("Search this branch by name", OnSearch)
                .WithFilter("View", new[] { "Grid", "List" }, SetMode, _listMode ? "List" : "Grid")
                .WithSpacer()
                .WithRefresh(Refresh);

            var slot = AddSlot();
            ViewBind.Load(
                () => Sdk.AssetsStorage.LoadConfigAsync(),
                slot,
                BuildCatalog,
                d => d == null
                    || ((d.assets == null || d.assets.Count == 0) && (d.folders == null || d.folders.Count == 0)),
                new BindOptions
                {
                    Log = Ctx.Log,
                    Label = "Asset structure",
                    Snippet = StructureSnippet,
                    ServiceName = "Asset Storage",
                    ConfigurationRequest = true,
                    AllowRetry = true,
                    EmptyView = NothingUploaded,
                });
        }

        private static VisualElement NothingUploaded()
        {
            return ZeroState.Panel(LucideIcon.FolderOpen,
                "This branch has no files yet",
                "Assets are uploaded in the Mirra Hub console, per project and branch. Once a file is "
                + "there it appears in this catalog and the game can download it by id.",
                hint: "Images, audio, text, documents and asset bundles all come through the same call.");
        }

        // ----- catalog ---------------------------------------------------------------------------

        private VisualElement BuildCatalog(AssetStorageStructureDto data)
        {
            _allAssets = data.assets ?? new List<AssetDto>();
            _allFolders = data.folders ?? new List<FolderDto>();
            Index();

            long totalSize = 0L;
            var perType = new Dictionary<AssetType, int>();
            foreach (var a in _allAssets)
            {
                totalSize += a.size;
                int had;
                perType.TryGetValue(a.type, out had);
                perType[a.type] = had + 1;
            }

            SetStatus(_allAssets.Count + " files · " + _allFolders.Count + " folders", ChipTone.Ok);

            var root = new VisualElement();

            var kpis = new KpiRow();
            if (_allAssets.Count == 0)
            {
                kpis.AddZero("Files", LucideIcon.File)
                    .AddZero("Folders", LucideIcon.Folder)
                    .AddZero("Total size", LucideIcon.HardDrive, "0 B")
                    .AddZero("Types", LucideIcon.Hash);
            }
            else
            {
                kpis.Add("Files", LucideIcon.File, Fmt.Number(_allAssets.Count))
                    .Add("Folders", LucideIcon.Folder, Fmt.Number(_allFolders.Count))
                    .Add("Total size", LucideIcon.HardDrive, Fmt.Bytes(totalSize))
                    .Add("Types", LucideIcon.Hash, perType.Count.ToString());
            }
            root.Add(kpis);

            root.Add(BuildTypeBreakdown(perType, totalSize));

            root.Add(new SectionHeader("Catalog"));
            _crumbSlot = new VisualElement();
            _crumbSlot.AddToClassList("sc-fs-crumbs");
            root.Add(_crumbSlot);

            _browserSlot = new VisualElement();
            root.Add(_browserSlot);

            root.Add(BuildPublicSection());

            RenderBrowser();
            return root;
        }

        /// <summary>
        /// Rebuilds the parent/child maps. Two shapes of awkward data are expected and neither may
        /// drop a file: a parent id that is null or empty (a root item), and a parent id pointing at
        /// a folder that did not come with the response (an orphan — surfaced at the root).
        /// </summary>
        private void Index()
        {
            _childFolders.Clear();
            _folderAssets.Clear();
            _foldersById.Clear();

            foreach (var f in _allFolders)
            {
                if (!string.IsNullOrEmpty(f.id))
                {
                    _foldersById[f.id] = f;
                }
            }

            foreach (var f in _allFolders)
            {
                string parent = Norm(f.parentFolderId);
                if (parent.Length > 0 && !_foldersById.ContainsKey(parent))
                {
                    parent = string.Empty;
                }
                Bucket(_childFolders, parent).Add(f);
            }

            foreach (var a in _allAssets)
            {
                string parent = Norm(a.folderId);
                if (parent.Length > 0 && !_foldersById.ContainsKey(parent))
                {
                    parent = string.Empty;
                }
                Bucket(_folderAssets, parent).Add(a);
            }
        }

        private VisualElement BuildTypeBreakdown(Dictionary<AssetType, int> perType, long totalSize)
        {
            var box = new VisualElement();
            box.AddToClassList("sc-fs-breakdown");

            var donut = new DonutChart(150f);
            var points = new List<ChartPoint>();
            foreach (var kv in perType)
            {
                points.Add(new ChartPoint(kv.Key.ToString(), kv.Value));
            }
            donut.SetData(points)
                .SetCenter(Fmt.Number(_allAssets.Count), _allAssets.Count == 1 ? "file" : "files")
                .SetEmptyText("No files to break down");
            box.Add(donut);

            var side = new VisualElement();
            side.AddToClassList("sc-fs-breakdown__side");

            var caption = new Label(_allAssets.Count == 0
                ? "Upload a file to see what this branch is made of."
                : "Types in this branch, and how much space each one takes.");
            caption.AddToClassList("sc-fs-hint");
            side.Add(caption);

            var sizes = new VisualElement();
            sizes.AddToClassList("sc-chip-row");
            foreach (var kv in perType)
            {
                long bytes = 0L;
                foreach (var a in _allAssets)
                {
                    if (a.type == kv.Key)
                    {
                        bytes += a.size;
                    }
                }
                sizes.Add(new Chip(kv.Key + " · " + Fmt.Bytes(bytes), ToneFor(kv.Key)));
            }
            if (perType.Count > 1)
            {
                sizes.Add(new Chip("Total · " + Fmt.Bytes(totalSize), ChipTone.Neutral));
            }
            side.Add(sizes);
            box.Add(side);
            return box;
        }

        // ----- browser --------------------------------------------------------------------------

        private void OnSearch(string text)
        {
            _search = text == null ? string.Empty : text.Trim();
            RenderBrowser();
        }

        private void SetMode(string mode)
        {
            bool list = mode == "List";
            if (list == _listMode)
            {
                return;
            }
            _listMode = list;
            RenderBrowser();
        }

        private void Open(string folderId)
        {
            _currentFolderId = Norm(folderId);
            RenderBrowser();
        }

        private void RenderBrowser()
        {
            if (_browserSlot == null)
            {
                return;
            }

            // Every re-render throws away the cards the queue was pointing at.
            _queue.Clear();
            _shownInGrid = 0;

            RenderCrumbs();
            _browserSlot.Clear();

            if (_search.Length > 0)
            {
                _browserSlot.Add(BuildSearchResults());
                SchedulePreviewSweep();
                return;
            }

            var folders = Children(_childFolders, _currentFolderId);
            var assets = Children(_folderAssets, _currentFolderId);

            if (folders.Count == 0 && assets.Count == 0)
            {
                _browserSlot.Add(_currentFolderId.Length == 0 ? NothingUploaded() : EmptyFolder());
                return;
            }

            _browserSlot.Add(_listMode ? BuildList(folders, assets) : BuildGrid(folders, assets));
            SchedulePreviewSweep();
        }

        private static VisualElement EmptyFolder()
        {
            return ZeroState.Panel(LucideIcon.FolderOpen, "This folder is empty",
                "Nothing has been uploaded into it yet. Use the breadcrumbs to browse the rest of the branch.");
        }

        private void RenderCrumbs()
        {
            if (_crumbSlot == null)
            {
                return;
            }
            _crumbSlot.Clear();

            if (_search.Length > 0)
            {
                var searching = new Label("Search results for \"" + Fmt.Truncate(_search, 24) + "\"");
                searching.enableRichText = false;
                searching.AddToClassList("sc-fs-crumb--current");
                _crumbSlot.Add(searching);
                return;
            }

            var chain = ParentChain(_currentFolderId);
            _crumbSlot.Add(Crumb("Branch root", string.Empty, _currentFolderId.Length > 0));
            for (int i = 0; i < chain.Count; i++)
            {
                var sep = new Label(LucideIcon.ChevronRight);
                sep.AddToClassList("sc-fs-crumb__sep");
                sep.AddToClassList("sc-icon");
                _crumbSlot.Add(sep);
                _crumbSlot.Add(Crumb(Fmt.OrDash(chain[i].name), chain[i].id, i < chain.Count - 1));
            }
        }

        private VisualElement Crumb(string text, string folderId, bool clickable)
        {
            var label = new Label(text);
            label.enableRichText = false;
            label.AddToClassList(clickable ? "sc-fs-crumb" : "sc-fs-crumb--current");
            if (clickable)
            {
                label.RegisterCallback<ClickEvent>(_ => Open(folderId));
            }
            return label;
        }

        /// <summary>
        /// Walks a folder up to the root. Depth-capped and visit-tracked: a parentFolderId cycle in
        /// the data would otherwise spin here forever.
        /// </summary>
        private List<FolderDto> ParentChain(string folderId)
        {
            var chain = new List<FolderDto>();
            var seen = new HashSet<string>();
            string id = Norm(folderId);
            while (id.Length > 0 && seen.Add(id) && chain.Count < 64)
            {
                FolderDto f;
                if (!_foldersById.TryGetValue(id, out f))
                {
                    break;
                }
                chain.Insert(0, f);
                id = Norm(f.parentFolderId);
            }
            return chain;
        }

        private VisualElement BuildGrid(List<FolderDto> folders, List<AssetDto> assets)
        {
            var wrap = new VisualElement();

            var grid = new VisualElement();
            grid.AddToClassList("sc-asset-grid");
            foreach (var f in folders)
            {
                grid.Add(FolderCard(f));
            }

            int shown = 0;
            foreach (var a in assets)
            {
                if (shown >= GridPageSize)
                {
                    break;
                }
                grid.Add(AssetCard(a));
                shown++;
            }
            _shownInGrid = shown;
            wrap.Add(grid);

            // Folders always render in full; only the file list is capped, and the reader is told so
            // rather than being left with a silently truncated folder.
            if (assets.Count > shown)
            {
                int rest = assets.Count - shown;
                var more = new Button { text = "Show " + Math.Min(GridPageSize, rest) + " more of " + Fmt.Number(rest) };
                more.AddToClassList("sc-btn");
                more.AddToClassList("sc-fs-more");
                more.clicked += () => ShowMore(grid, more, assets);
                wrap.Add(more);
            }
            return wrap;
        }

        private void ShowMore(VisualElement grid, Button more, List<AssetDto> assets)
        {
            int added = 0;
            while (_shownInGrid < assets.Count && added < GridPageSize)
            {
                grid.Add(AssetCard(assets[_shownInGrid]));
                _shownInGrid++;
                added++;
            }

            int rest = assets.Count - _shownInGrid;
            if (rest <= 0)
            {
                more.style.display = DisplayStyle.None;
            }
            else
            {
                more.text = "Show " + Math.Min(GridPageSize, rest) + " more of " + Fmt.Number(rest);
            }
            SchedulePreviewSweep();
        }

        private VisualElement BuildList(List<FolderDto> folders, List<AssetDto> assets)
        {
            // Folders and files share one table so the catalog reads as a single place. Rows carry
            // the DTO itself and the cells branch on which of the two it is.
            var rows = new List<object>();
            foreach (var f in folders)
            {
                rows.Add(f);
            }
            foreach (var a in assets)
            {
                rows.Add(a);
            }

            var table = new DataTable(BrowserColumns())
                .WithZebra()
                .WithMaxHeight(560f)
                .WithRowClick(OnRowClick);
            table.Bind(rows);
            return table;
        }

        private DataColumn[] BrowserColumns()
        {
            return new[]
            {
                new DataColumn
                {
                    Header = string.Empty, FixedWidth = true, Px = 34,
                    Cell = o =>
                    {
                        var folder = o as FolderDto;
                        var glyph = new Label(folder != null ? LucideIcon.Folder : GlyphFor(((AssetDto)o).type));
                        glyph.AddToClassList("sc-icon");
                        glyph.style.color = folder != null ? ShowcaseTheme.AccentSoft : AccentFor(((AssetDto)o).type);
                        return glyph;
                    },
                },
                new DataColumn
                {
                    Header = "NAME", Grow = 3f,
                    SortKey = NameOf,
                    Cell = o =>
                    {
                        var label = new Label(Fmt.OrDash(NameOf(o) as string));
                        label.enableRichText = false;
                        return label;
                    },
                },
                new DataColumn
                {
                    Header = "TYPE", Grow = 1f,
                    SortKey = o => o is FolderDto ? "Folder" : ((AssetDto)o).type.ToString(),
                    Cell = o =>
                    {
                        var folder = o as FolderDto;
                        if (folder != null)
                        {
                            return new Chip("Folder", ChipTone.Accent);
                        }
                        var asset = (AssetDto)o;
                        return new Chip(asset.type.ToString(), ToneFor(asset.type));
                    },
                },
                new DataColumn
                {
                    Header = "SIZE", Grow = 1f, Align = "right",
                    // Folders sort below every file rather than pretending to have a byte size.
                    SortKey = o => o is FolderDto ? -1L : ((AssetDto)o).size,
                    Cell = o =>
                    {
                        var folder = o as FolderDto;
                        if (folder == null)
                        {
                            return new Label(Fmt.Bytes(((AssetDto)o).size));
                        }
                        int count = Children(_folderAssets, folder.id).Count;
                        return new Label(count + (count == 1 ? " file" : " files"));
                    },
                },
                new DataColumn
                {
                    Header = "UPDATED", Grow = 1f, Align = "right",
                    SortKey = o => UpdatedOf(o),
                    Cell = o => new Label(Fmt.Date(UpdatedOf(o))),
                },
            };
        }

        private void OnRowClick(object row)
        {
            var folder = row as FolderDto;
            if (folder != null)
            {
                Open(folder.id);
                return;
            }
            ShowDetails((AssetDto)row);
        }

        private VisualElement BuildSearchResults()
        {
            var hits = new List<object>();
            foreach (var f in _allFolders)
            {
                if (Matches(f.name))
                {
                    hits.Add(f);
                }
            }
            foreach (var a in _allAssets)
            {
                if (Matches(a.name))
                {
                    hits.Add(a);
                }
            }

            if (hits.Count == 0)
            {
                return ZeroState.Panel(LucideIcon.FileSearch, "No match in this branch",
                    "Nothing here is named like \"" + Fmt.Truncate(_search, 24)
                    + "\". The search covers every folder and file in the branch, not just this folder.");
            }

            var wrap = new VisualElement();
            var header = new Label(hits.Count + (hits.Count == 1 ? " match" : " matches") + " across the branch");
            header.AddToClassList("sc-fs-hint");
            wrap.Add(header);

            var list = new VisualElement();
            foreach (var hit in hits)
            {
                var folder = hit as FolderDto;
                var row = new ListRow();

                var glyph = new Label(folder != null ? LucideIcon.Folder : GlyphFor(((AssetDto)hit).type));
                glyph.AddToClassList("sc-icon");
                glyph.AddToClassList("sc-fs-hit__glyph");
                glyph.style.color = folder != null ? ShowcaseTheme.AccentSoft : AccentFor(((AssetDto)hit).type);
                row.SetLead(glyph);

                row.SetTitle(Fmt.OrDash(NameOf(hit) as string));
                row.SetSubtitle(Fmt.OrDash(PathOf(hit)));
                if (folder != null)
                {
                    row.SetTrailing(new Chip("Folder", ChipTone.Accent));
                }
                else
                {
                    row.SetTrailing(new Chip(Fmt.Bytes(((AssetDto)hit).size), ChipTone.Neutral));
                }

                object captured = hit;
                row.RegisterCallback<ClickEvent>(_ => OnRowClick(captured));
                row.AddToClassList("sc-fs-hit");
                list.Add(row);
            }
            wrap.Add(list);
            return wrap;
        }

        private bool Matches(string name)
        {
            return !string.IsNullOrEmpty(name)
                && name.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // ----- cards ----------------------------------------------------------------------------

        private VisualElement FolderCard(FolderDto folder)
        {
            var card = new VisualElement();
            card.AddToClassList("sc-asset-card");
            card.AddToClassList("sc-asset-card--folder");

            var preview = new VisualElement();
            preview.AddToClassList("sc-asset-card__preview");
            var glyph = new Label(LucideIcon.Folder);
            glyph.AddToClassList("sc-asset-card__glyph");
            glyph.AddToClassList("sc-icon");
            glyph.style.color = ShowcaseTheme.AccentSoft;
            preview.Add(glyph);
            card.Add(preview);

            var body = new VisualElement();
            body.AddToClassList("sc-asset-card__body");
            var name = new Label(Fmt.OrDash(folder.name));
            name.enableRichText = false;
            name.AddToClassList("sc-asset-card__name");
            body.Add(name);

            int files = Children(_folderAssets, folder.id).Count;
            int subs = Children(_childFolders, folder.id).Count;
            var chips = new VisualElement();
            chips.AddToClassList("sc-chip-row");
            chips.Add(new Chip(files + (files == 1 ? " file" : " files"), ChipTone.Neutral));
            if (subs > 0)
            {
                chips.Add(new Chip(subs + (subs == 1 ? " folder" : " folders"), ChipTone.Accent));
            }
            body.Add(chips);
            card.Add(body);

            card.RegisterCallback<ClickEvent>(_ => Open(folder.id));
            return card;
        }

        private VisualElement AssetCard(AssetDto asset)
        {
            var card = new VisualElement();
            card.AddToClassList("sc-asset-card");

            var preview = new VisualElement();
            preview.AddToClassList("sc-asset-card__preview");

            var glyph = new Label(GlyphFor(asset.type));
            glyph.AddToClassList("sc-asset-card__glyph");
            glyph.AddToClassList("sc-icon");
            glyph.style.color = AccentFor(asset.type);
            preview.Add(glyph);

            var image = new Image { scaleMode = ScaleMode.ScaleToFit };
            image.AddToClassList("sc-asset-card__img");
            image.style.display = DisplayStyle.None;
            preview.Add(image);
            card.Add(preview);

            var body = new VisualElement();
            body.AddToClassList("sc-asset-card__body");
            var name = new Label(Fmt.OrDash(asset.name));
            name.enableRichText = false;
            name.AddToClassList("sc-asset-card__name");
            body.Add(name);

            var chips = new VisualElement();
            chips.AddToClassList("sc-chip-row");
            chips.Add(new Chip(asset.type.ToString(), ToneFor(asset.type)));
            chips.Add(new Chip(Fmt.Bytes(asset.size), ChipTone.Neutral));
            body.Add(chips);
            card.Add(body);

            if (asset.type == AssetType.Image && !string.IsNullOrEmpty(asset.stableId))
            {
                Texture2D cached;
                if (_textures.TryGetValue(asset.stableId, out cached) && cached != null)
                {
                    ApplyTexture(image, glyph, cached);
                }
                else if (!_failedTextures.Contains(asset.stableId))
                {
                    _queue.Add(new PreviewRequest
                    {
                        Id = asset.stableId,
                        Card = card,
                        Image = image,
                        Glyph = glyph,
                    });
                }
            }

            card.RegisterCallback<ClickEvent>(_ => ShowDetails(asset));
            return card;
        }

        // ----- lazy previews --------------------------------------------------------------------

        private sealed class PreviewRequest
        {
            public string Id;
            public VisualElement Card;
            public Image Image;
            public Label Glyph;
            public bool Started;
        }

        /// <summary>
        /// Layout is not resolved in the frame a card is created, so the first visibility sweep has
        /// to wait for it; after that, scrolling drives the sweeps.
        /// </summary>
        private void SchedulePreviewSweep()
        {
            if (_closed || _queue.Count == 0)
            {
                return;
            }
            Content.UnregisterCallback<GeometryChangedEvent>(OnContentGeometry);
            Content.RegisterCallback<GeometryChangedEvent>(OnContentGeometry);
            Content.verticalScroller.valueChanged -= OnScrolled;
            Content.verticalScroller.valueChanged += OnScrolled;
            schedule.Execute(SweepPreviews).StartingIn(0);
        }

        private void OnContentGeometry(GeometryChangedEvent evt)
        {
            SweepPreviews();
        }

        private void OnScrolled(float value)
        {
            SweepPreviews();
        }

        private void SweepPreviews()
        {
            if (_closed || _queue.Count == 0)
            {
                return;
            }

            Rect viewport = Content.contentViewport.worldBound;
            if (viewport.height <= 0f)
            {
                return; // not laid out yet — the geometry callback will come back here
            }
            viewport.yMin -= PrefetchMargin;
            viewport.yMax += PrefetchMargin;

            for (int i = _queue.Count - 1; i >= 0; i--)
            {
                var request = _queue[i];
                if (request.Started)
                {
                    continue;
                }
                if (request.Card.panel == null)
                {
                    _queue.RemoveAt(i); // the card was dropped by a re-render
                    continue;
                }
                if (_inFlight >= MaxParallelPreviews)
                {
                    return;
                }
                if (!viewport.Overlaps(request.Card.worldBound))
                {
                    continue;
                }
                request.Started = true;
                LoadPreview(request);
            }
        }

        private async void LoadPreview(PreviewRequest request)
        {
            _inFlight++;
            try
            {
                var op = Sdk.AssetsStorage.LoadTextureFromId(request.Id);
                if (op == null)
                {
                    _failedTextures.Add(request.Id);
                    return;
                }
                await op.Task();
                var result = op.Result;

                if (Ctx.Log != null && result != null)
                {
                    Ctx.Log.Record("Preview " + Fmt.Id(request.Id, 8), result, TextureSnippet);
                }

                if (result == null || !result.IsSuccess || result.Data == null)
                {
                    // Leave the type glyph in place: a file whose preview failed must still read
                    // as a file rather than as an empty tile.
                    _failedTextures.Add(request.Id);
                    return;
                }

                _textures[request.Id] = result.Data;
                if (!_closed && request.Image.panel != null)
                {
                    ApplyTexture(request.Image, request.Glyph, result.Data);
                }
            }
            catch (Exception e)
            {
                _failedTextures.Add(request.Id);
                Debug.LogWarning("[Showcase] asset preview " + request.Id + " failed: " + e.Message);
            }
            finally
            {
                _inFlight--;
                _queue.Remove(request);
                if (!_closed)
                {
                    SweepPreviews();
                }
            }
        }

        private static void ApplyTexture(Image image, Label glyph, Texture2D texture)
        {
            image.image = texture;
            image.style.display = DisplayStyle.Flex;
            glyph.style.display = DisplayStyle.None;
        }

        private void Close()
        {
            _closed = true;
            _queue.Clear();
            Content.UnregisterCallback<GeometryChangedEvent>(OnContentGeometry);
            Content.verticalScroller.valueChanged -= OnScrolled;
        }

        // ----- details --------------------------------------------------------------------------

        private void ShowDetails(AssetDto asset)
        {
            if (Popup == null)
            {
                return;
            }

            var body = new ScrollView(ScrollViewMode.Vertical);
            body.style.maxHeight = 520f;

            var head = new VisualElement();
            head.AddToClassList("sc-fs-detail__preview");
            body.Add(head);

            switch (asset.type)
            {
                case AssetType.Image:
                    head.Add(BuildImagePreview(asset));
                    break;
                case AssetType.Audio:
                    head.Add(BuildAudioPreview(asset));
                    break;
                case AssetType.Document:
                    head.Add(BuildTextPreview(asset));
                    break;
                default:
                    head.Add(BuildGenericPreview(asset));
                    break;
            }

            body.Add(new SectionHeader("Details"));
            var kv = new VisualElement();
            kv.AddToClassList("sc-kv-list");
            kv.Add(Kv("Stable id", Fmt.OrDash(asset.stableId), asset.stableId));
            kv.Add(Kv("Internal id", Fmt.OrDash(asset.id), asset.id));
            kv.Add(Kv("Path", Fmt.OrDash(asset.path), asset.path));
            kv.Add(Kv("Type", asset.type.ToString(), null));
            kv.Add(Kv("MIME", Fmt.OrDash(asset.mimeType), null));
            kv.Add(Kv("Extension", Fmt.OrDash(asset.extension), null));
            kv.Add(Kv("Size", Fmt.Bytes(asset.size), null));
            kv.Add(Kv("Version", asset.version.ToString(), null));
            kv.Add(Kv("Created", Fmt.DateTime2(asset.createdAt), null));
            kv.Add(Kv("Updated", Fmt.DateTime2(asset.updatedAt), null));
            body.Add(kv);

            Popup.Open(body, Fmt.Truncate(Fmt.OrDash(asset.name), 40));
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

        private VisualElement BuildImagePreview(AssetDto asset)
        {
            var host = new VisualElement();
            host.AddToClassList("sc-fs-detail__image-host");

            Texture2D cached;
            if (_textures.TryGetValue(asset.stableId, out cached) && cached != null)
            {
                host.Add(FullImage(cached));
                return host;
            }

            Skeleton.Into(host, 4);
            LoadFullImage(asset, host);
            return host;
        }

        private static VisualElement FullImage(Texture2D texture)
        {
            var col = new VisualElement();

            var image = new Image { scaleMode = ScaleMode.ScaleToFit, image = texture };
            image.AddToClassList("sc-fs-detail__image");
            col.Add(image);

            var caption = new Label(texture.width + " × " + texture.height + " px");
            caption.AddToClassList("sc-fs-detail__caption");
            col.Add(caption);
            return col;
        }

        private async void LoadFullImage(AssetDto asset, VisualElement host)
        {
            var op = Sdk.AssetsStorage.LoadTextureFromId(asset.stableId);
            if (op == null)
            {
                Replace(host, ErrorState.Message("This image could not be requested."));
                return;
            }
            await op.Task();
            var result = op.Result;
            if (Ctx.Log != null && result != null)
            {
                Ctx.Log.Record("Image " + Fmt.Id(asset.stableId, 8), result, TextureSnippet);
            }

            if (host.panel == null)
            {
                return; // the dialog is already closed
            }
            if (result == null || !result.IsSuccess || result.Data == null)
            {
                Replace(host, ErrorState.Build(result != null ? result.Error : null));
                return;
            }
            _textures[asset.stableId] = result.Data;
            Replace(host, FullImage(result.Data));
        }

        private VisualElement BuildAudioPreview(AssetDto asset)
        {
            var host = new VisualElement();
            host.AddToClassList("sc-fs-detail__audio");

            var hint = new Label("The AudioType has to match the file. This screen guesses it from the "
                + "extension; a game would pass the right one per asset.");
            hint.AddToClassList("sc-fs-hint");
            host.Add(hint);

            var status = new Label();
            status.AddToClassList("sc-fs-detail__caption");

            var play = new Button { text = "Load and play" };
            play.AddToClassList("sc-btn");
            play.AddToClassList("sc-btn--primary");
            play.clicked += () =>
            {
                play.SetEnabled(false);
                status.text = "Downloading…";
                PlayAudio(asset, play, status);
            };

            host.Add(play);
            host.Add(status);
            return host;
        }

        private async void PlayAudio(AssetDto asset, Button play, Label status)
        {
            var op = Sdk.AssetsStorage.LoadAudioFromId(asset.stableId, AudioTypeFor(asset.extension));
            if (op == null)
            {
                status.text = "Could not start the download.";
                play.SetEnabled(true);
                return;
            }
            await op.Task();
            var result = op.Result;
            if (Ctx.Log != null && result != null)
            {
                Ctx.Log.Record("Audio " + Fmt.Id(asset.stableId, 8), result, AudioSnippet);
            }

            if (play.panel == null)
            {
                return;
            }
            play.SetEnabled(true);

            if (result == null || !result.IsSuccess || result.Data == null)
            {
                status.text = result != null && result.Error != null && !string.IsNullOrEmpty(result.Error.Message)
                    ? result.Error.Message
                    : "The clip could not be decoded — check that the AudioType matches the file.";
                return;
            }

            var clip = result.Data;
            // PlayClipAtPoint owns the AudioSource it spawns, so the clip survives the dialog being
            // closed and this screen has nothing to tear down.
            AudioSource.PlayClipAtPoint(clip, Vector3.zero);
            status.text = "Playing · " + clip.length.ToString("0.0") + "s · " + clip.frequency + " Hz · "
                + clip.channels + (clip.channels == 1 ? " channel" : " channels");
        }

        private VisualElement BuildTextPreview(AssetDto asset)
        {
            var host = new VisualElement();
            Skeleton.Into(host, 4);
            LoadText(asset, host);
            return host;
        }

        private async void LoadText(AssetDto asset, VisualElement host)
        {
            var op = Sdk.AssetsStorage.LoadTextFromId(asset.stableId);
            if (op == null)
            {
                Replace(host, ErrorState.Message("This file could not be requested."));
                return;
            }
            await op.Task();
            var result = op.Result;
            if (Ctx.Log != null && result != null)
            {
                Ctx.Log.Record("Text " + Fmt.Id(asset.stableId, 8), result, TextSnippet);
            }

            if (host.panel == null)
            {
                return;
            }
            if (result == null || !result.IsSuccess || result.Data == null)
            {
                Replace(host, ErrorState.Build(result != null ? result.Error : null));
                return;
            }

            string text = result.Data.Text ?? string.Empty;
            if (text.Length == 0)
            {
                Replace(host, ZeroState.Panel(LucideIcon.FileText, "The file is empty",
                    "It downloaded fine — there is just nothing in it."));
                return;
            }
            Replace(host, new JsonViewer().SetRaw(text).SetMaxLines(24));
        }

        private VisualElement BuildGenericPreview(AssetDto asset)
        {
            return ZeroState.Panel(GlyphFor(asset.type), asset.type + " file",
                "There is no in-editor preview for this type. A game downloads it with "
                + (asset.type == AssetType.Archive
                    ? "LoadAssetBundleFromId and loads objects out of the bundle."
                    : "LoadTextFromId and reads the raw bytes from TextFile.Data."));
        }

        // ----- anonymous access -----------------------------------------------------------------

        private VisualElement BuildPublicSection()
        {
            var card = new Card(Meta.Accent);
            card.AddToClassList("sc-fs-public");
            card.WithTitle("Anonymous access", Meta.Accent);

            var text = new Label("An asset marked public in the console downloads without a signed-in "
                + "player — handy for a splash image or a config the game needs before login. "
                + "Everything else answers 403 on this route.");
            text.AddToClassList("sc-fs-hint");
            card.Body.Add(text);

            // Prefer an asset the server actually marks public, so the demo proves the route rather
            // than demonstrating a 403; fall back to any image if the branch has none.
            AssetDto candidate = null;
            AssetDto fallback = null;
            foreach (var a in _allAssets)
            {
                if (a.type != AssetType.Image || string.IsNullOrEmpty(a.stableId))
                {
                    continue;
                }
                if (a.isPublic)
                {
                    candidate = a;
                    break;
                }
                if (fallback == null)
                {
                    fallback = a;
                }
            }
            candidate = candidate ?? fallback;

            if (candidate == null)
            {
                card.Body.Add(ZeroState.Panel(LucideIcon.Globe, "Nothing to try it on",
                    "Upload an image to this branch and mark it public — this section will then fetch it "
                    + "without a token to prove the route works."));
                return card;
            }

            var row = new VisualElement();
            row.AddToClassList("sc-chip-row");

            var name = new Label(Fmt.Truncate(Fmt.OrDash(candidate.name), 30));
            name.enableRichText = false;
            name.AddToClassList("sc-fs-hint");
            row.Add(name);

            var verdict = new Label();
            verdict.AddToClassList("sc-fs-detail__caption");

            var button = new Button { text = "Fetch without a token" };
            button.AddToClassList("sc-btn");
            button.clicked += () =>
            {
                button.SetEnabled(false);
                verdict.text = "Requesting…";
                TryPublic(candidate, button, verdict);
            };
            row.Add(button);

            card.Body.Add(row);
            card.Body.Add(verdict);
            return card;
        }

        private async void TryPublic(AssetDto asset, Button button, Label verdict)
        {
            var op = Sdk.AssetsStorage.LoadPublicTextureFromId(asset.stableId);
            if (op == null)
            {
                verdict.text = "Could not start the request.";
                button.SetEnabled(true);
                return;
            }
            await op.Task();
            var result = op.Result;
            if (Ctx.Log != null && result != null)
            {
                Ctx.Log.Record("Public " + Fmt.Id(asset.stableId, 8), result, PublicSnippet);
            }

            if (button.panel == null)
            {
                return;
            }
            button.SetEnabled(true);

            if (result != null && result.IsSuccess && result.Data != null)
            {
                verdict.text = "Served anonymously — this asset is public ("
                    + result.Data.width + " × " + result.Data.height + " px).";
                return;
            }

            long? code = result != null ? result.HttpStatusCode : null;
            verdict.text = code == 403
                ? "403 — this asset is private, so the anonymous route refused it. That is the expected answer."
                : "The anonymous request failed: " + (result != null && result.Error != null
                    ? Fmt.OrDash(result.Error.Message)
                    : "no response");
        }

        // ----- helpers --------------------------------------------------------------------------

        private static string Norm(string id)
        {
            return string.IsNullOrEmpty(id) ? string.Empty : id;
        }

        private static List<T> Bucket<T>(Dictionary<string, List<T>> map, string key)
        {
            List<T> list;
            if (!map.TryGetValue(key, out list))
            {
                list = new List<T>();
                map[key] = list;
            }
            return list;
        }

        private static List<T> Children<T>(Dictionary<string, List<T>> map, string key)
        {
            List<T> list;
            return map.TryGetValue(Norm(key), out list) ? list : new List<T>();
        }

        private static IComparable NameOf(object item)
        {
            var folder = item as FolderDto;
            return folder != null ? folder.name : ((AssetDto)item).name;
        }

        private static string PathOf(object item)
        {
            var folder = item as FolderDto;
            return folder != null ? folder.path : ((AssetDto)item).path;
        }

        private static DateTime UpdatedOf(object item)
        {
            var folder = item as FolderDto;
            return folder != null ? folder.updatedAt : ((AssetDto)item).updatedAt;
        }

        private static AudioType AudioTypeFor(string extension)
        {
            string e = (extension ?? string.Empty).TrimStart('.').ToLowerInvariant();
            switch (e)
            {
                case "mp3": return AudioType.MPEG;
                case "ogg": return AudioType.OGGVORBIS;
                case "wav": return AudioType.WAV;
                case "aiff":
                case "aif": return AudioType.AIFF;
                case "xm": return AudioType.XM;
                case "it": return AudioType.IT;
                case "mod": return AudioType.MOD;
                default: return AudioType.UNKNOWN;
            }
        }

        private static string GlyphFor(AssetType t)
        {
            switch (t)
            {
                case AssetType.Image: return LucideIcon.FileImage;
                case AssetType.Audio: return LucideIcon.Music;
                case AssetType.Video: return LucideIcon.Play;
                case AssetType.Document: return LucideIcon.FileText;
                case AssetType.Archive: return LucideIcon.Archive;
                default: return LucideIcon.File;
            }
        }

        private static Color AccentFor(AssetType t)
        {
            switch (t)
            {
                case AssetType.Image: return ShowcaseTheme.Info;
                case AssetType.Audio: return ShowcaseTheme.Violet;
                case AssetType.Video: return ShowcaseTheme.Ok;
                case AssetType.Archive: return ShowcaseTheme.Warn;
                default: return ShowcaseTheme.TextMuted;
            }
        }

        private static ChipTone ToneFor(AssetType t)
        {
            switch (t)
            {
                case AssetType.Image: return ChipTone.Info;
                case AssetType.Audio: return ChipTone.Accent;
                case AssetType.Video: return ChipTone.Ok;
                case AssetType.Archive: return ChipTone.Warn;
                default: return ChipTone.Neutral;
            }
        }
    }
}
