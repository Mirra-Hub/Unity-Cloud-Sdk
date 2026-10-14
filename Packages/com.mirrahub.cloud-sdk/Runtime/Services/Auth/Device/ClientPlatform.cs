namespace MirraCloud.Core.Auth
{
    /// <summary>
    /// The kind of client a session runs in, taken from <see cref="UnityEngine.Application.platform"/>. Not the
    /// project's platform (<see cref="Configuration.PlatformKey"/>): a session has both.
    /// </summary>
    public enum ClientPlatform
    {
        Unknown = 0,
        Android = 1,
        Ios = 2,
        WebGl = 3,
        Windows = 4,
        MacOs = 5,
        Linux = 6,
        Editor = 7,
        Console = 8
    }
}
