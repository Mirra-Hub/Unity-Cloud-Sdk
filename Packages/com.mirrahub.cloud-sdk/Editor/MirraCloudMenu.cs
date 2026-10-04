namespace MirraCloud.Editor
{
    /// <summary>
    /// Every item of the editor's top-level MirraCloud menu, in one place. Unity draws a separator between two items
    /// whose priorities are more than 10 apart, so each group starts a hundred further on.
    /// </summary>
    internal static class MirraCloudMenu
    {
        private const string Root = "MirraCloud/";

        public const string Manager = Root + "Manager";
        public const int ManagerPriority = 0;

        public const string RequestInspector = Root + "Debug/Request Inspector";
        public const int RequestInspectorPriority = 100;

        public const string ClearSignIn = Root + "Data/Clear Sign-In";
        public const int ClearSignInPriority = 200;

        public const string ClearCache = Root + "Data/Clear Cache";
        public const int ClearCachePriority = 201;

        public const string ShowCache = Root + "Data/Show Cache";
        public const int ShowCachePriority = 202;
    }
}
