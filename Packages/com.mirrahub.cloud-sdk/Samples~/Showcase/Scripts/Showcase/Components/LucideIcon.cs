namespace MirraCloud.Example.Showcase
{
    /// <summary>
    /// Codepoints of the Lucide icon font (Example/UI/Fonts/lucide.ttf) used by the showcase.
    /// Values come from the vendored release's info.json (icon name → encodedCode) — when
    /// updating the ttf, re-check them against the new info.json.
    /// A label shows an icon by using one of these strings as its text plus the "sc-icon"
    /// USS class (which switches the label to the Lucide font asset).
    /// </summary>
    public static class LucideIcon
    {
        // navigation / actions
        public const string ArrowLeft = "\uE048";   // arrow-left
        public const string ArrowRight = "\uE049";   // arrow-right
        public const string ArrowUp = "\uE04A";   // arrow-up
        public const string ArrowDown = "\uE042";   // arrow-down
        public const string ArrowUpDown = "\uE37D";   // arrow-up-down
        public const string ChevronUp = "\uE070";   // chevron-up
        public const string ChevronDown = "\uE06D";   // chevron-down
        public const string ChevronLeft = "\uE06E";   // chevron-left
        public const string ChevronRight = "\uE06F";   // chevron-right
        public const string ChevronsLeft = "\uE072";   // chevrons-left
        public const string ChevronsRight = "\uE073";   // chevrons-right
        public const string ChevronsUpDown = "\uE211";   // chevrons-up-down
        public const string X = "\uE1B2";   // x
        public const string Search = "\uE151";   // search
        public const string Pencil = "\uE1F9";   // pencil
        public const string Plus = "\uE13D";   // plus
        public const string Minus = "\uE11C";   // minus
        public const string Check = "\uE06C";   // check
        public const string CheckCheck = "\uE38E";   // check-check
        public const string Copy = "\uE09E";   // copy
        public const string Trash = "\uE18E";   // trash-2
        public const string Save = "\uE14D";   // save
        public const string Send = "\uE152";   // send
        public const string Filter = "\uE0DC";   // filter
        public const string Settings = "\uE154";   // settings
        public const string RefreshCw = "\uE145";   // refresh-cw
        public const string RotateCw = "\uE149";   // rotate-cw
        public const string EllipsisVertical = "\uE0B7";   // ellipsis-vertical
        public const string ExternalLink = "\uE0B9";   // external-link
        public const string Eye = "\uE0BA";   // eye
        public const string Download = "\uE0B2";   // download
        public const string Upload = "\uE19E";   // upload
        public const string LogOut = "\uE10E";   // log-out
        public const string Link = "\uE102";   // link
        public const string Paperclip = "\uE12D";   // paperclip
        public const string Reply = "\uE22A";   // reply
        public const string Smile = "\uE164";   // smile

        // states
        public const string Inbox = "\uE0F7";   // inbox
        public const string CircleAlert = "\uE077";   // circle-alert (alert-circle in this ttf)
        public const string CircleCheck = "\uE226";   // circle-check
        public const string CircleX = "\uE084";   // circle-x
        public const string CircleHelp = "\uE082";   // circle-help
        public const string CirclePlus = "\uE081";   // circle-plus
        public const string CircleMinus = "\uE07E";   // circle-minus
        public const string CircleDot = "\uE345";   // circle-dot
        public const string TriangleAlert = "\uE193";   // alert-triangle
        public const string Info = "\uE0F9";   // info
        public const string Lock = "\uE10B";   // lock
        public const string Loader = "\uE109";   // loader
        public const string Ban = "\uE051";   // ban
        public const string Wifi = "\uE1AE";   // wifi
        public const string WifiOff = "\uE1AF";   // wifi-off
        public const string Bell = "\uE059";   // bell
        public const string Construction = "\uE3B4";   // construction

        // people / social
        public const string User = "\uE19F";   // user
        public const string UserPlus = "\uE1A2";   // user-plus
        public const string Users = "\uE1A4";   // users
        public const string AtSign = "\uE04E";   // at-sign
        public const string MessageCircle = "\uE116";   // message-circle
        public const string MessageSquare = "\uE117";   // message-square

        // competition / live-ops
        public const string Trophy = "\uE373";   // trophy
        public const string Medal = "\uE36F";   // medal
        public const string Star = "\uE176";   // star
        public const string Flame = "\uE0D2";   // flame
        public const string TrendingUp = "\uE191";   // trending-up
        public const string Swords = "\uE2B4";   // swords
        public const string Target = "\uE180";   // target
        public const string Gift = "\uE0E1";   // gift

        // time / calendar
        public const string CalendarCheck = "\uE2B7";   // calendar-check
        public const string CalendarDays = "\uE2B9";   // calendar-days
        public const string CalendarPlus = "\uE2BC";   // calendar-plus
        public const string Clock = "\uE087";   // clock
        public const string History = "\uE1F5";   // history

        // economy / commerce
        public const string Coins = "\uE097";   // coins
        public const string Wallet = "\uE204";   // wallet
        public const string ShoppingCart = "\uE15C";   // shopping-cart
        public const string TicketPercent = "\uE5B0";   // ticket-percent
        public const string Percent = "\uE132";   // percent
        public const string Gem = "\uE242";   // gem
        public const string Zap = "\uE1B4";   // zap
        public const string Package = "\uE129";   // package

        // data / config
        public const string Database = "\uE0AD";   // database
        public const string Boxes = "\uE2D0";   // boxes
        public const string SlidersHorizontal = "\uE29A";   // sliders-horizontal
        public const string Braces = "\uE36A";   // braces
        public const string Languages = "\uE0FE";   // languages
        public const string ChartPie = "\uE06B";   // chart-pie
        public const string ChartLine = "\uE2A5";   // chart-line
        public const string Activity = "\uE038";   // activity
        public const string Sigma = "\uE201";   // sigma
        public const string Hash = "\uE0EF";   // hash
        public const string List = "\uE106";   // list
        public const string Layers = "\uE529";   // layers
        public const string Tag = "\uE17F";   // tag

        // files / storage
        public const string FolderOpen = "\uE247";   // folder-open
        public const string Folder = "\uE0D7";   // folder
        public const string File = "\uE0C0";   // file
        public const string FileText = "\uE0CC";   // file-text
        public const string Archive = "\uE041";   // archive
        public const string Image = "\uE0F6";   // image
        public const string Music = "\uE122";   // music
        public const string Play = "\uE13C";   // play

        // tools / platform
        public const string Shield = "\uE158";   // shield
        public const string ShieldCheck = "\uE1FF";   // shield-check
        public const string Code = "\uE093";   // code
        public const string Rocket = "\uE286";   // rocket
        public const string Globe = "\uE0E8";   // globe
        public const string GitBranch = "\uE0E2";   // git-branch
        public const string GitMerge = "\uE0E4";   // git-merge
        public const string Sparkles = "\uE412";   // sparkles
        public const string Terminal = "\uE181";   // terminal
        public const string Workflow = "\uE425";   // workflow
        public const string Waypoints = "\uE542";   // waypoints

        // people (extra)
        public const string UserCheck = "\uE1A0";   // user-check
        public const string UserMinus = "\uE1A1";   // user-minus
        public const string UserX = "\uE1A3";   // user-x
        public const string Crown = "\uE1D6";   // crown
        public const string BadgeCheck = "\uE241";   // badge-check
        public const string KeyRound = "\uE4A3";   // key-round
        public const string DoorOpen = "\uE3D6";   // door-open

        // charts
        public const string BarChart = "\uE06A";   // bar-chart
        public const string BarChart3 = "\uE2A3";   // bar-chart-3
        public const string AreaChart = "\uE4D3";   // area-chart
        public const string TrendingDown = "\uE190";   // trending-down

        // layout / views
        public const string LayoutGrid = "\uE0FF";   // layout-grid
        public const string LayoutList = "\uE1D9";   // layout-list
        public const string Table = "\uE17D";   // table
        public const string ListTree = "\uE408";   // list-tree
        public const string FolderTree = "\uE33C";   // folder-tree
        public const string FolderPlus = "\uE0D9";   // folder-plus
        public const string HardDrive = "\uE0ED";   // hard-drive
        public const string Cloud = "\uE088";   // cloud
        public const string Box = "\uE061";   // box
        public const string Binary = "\uE1F2";   // binary
        public const string ScrollText = "\uE45F";   // scroll-text
        public const string FileCode = "\uE0C3";   // file-code
        public const string FileImage = "\uE31C";   // file-image
        public const string FileSearch = "\uE0CB";   // file-search
        public const string Sheet = "\uE157";   // sheet

        // time (extra)
        public const string Timer = "\uE1E0";   // timer
        public const string Hourglass = "\uE296";   // hourglass
        public const string Gauge = "\uE1BF";   // gauge
        public const string CalendarClock = "\uE304";   // calendar-clock

        // misc
        public const string Flag = "\uE0D1";   // flag
        public const string Pin = "\uE259";   // pin
        public const string Bookmark = "\uE060";   // bookmark
        public const string Component = "\uE2AD";   // component
        public const string Blocks = "\uE4FA";   // blocks
        public const string CirclePlay = "\uE080";   // circle-play
        public const string CirclePause = "\uE07F";   // circle-pause
        public const string Volume = "\uE1AB";   // volume-2
    }
}
