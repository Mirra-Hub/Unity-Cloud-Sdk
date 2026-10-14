using UnityEngine;

namespace MirraCloud.Core.Auth
{
    /// <summary>Reads the device a session runs on. Call on the main thread: <see cref="SystemInfo"/> requires it.</summary>
    internal static class DeviceInfoProvider
    {
        private const int MaxFieldLength = 128;

        public static SessionDeviceDto Collect() => new SessionDeviceDto
        {
            DeviceName = Limit(SystemInfo.deviceName),
            DeviceModel = Limit(SystemInfo.deviceModel),
            DeviceType = ToDeviceType(SystemInfo.deviceType),
            ClientPlatform = ToClientPlatform(Application.platform),
            OperatingSystem = Limit(SystemInfo.operatingSystem),
            AppVersion = Limit(Application.version),
            SdkVersion = MirraCloudSdkInfo.Version,
            EngineVersion = Limit(Application.unityVersion)
        };

        internal static ClientDeviceType ToDeviceType(DeviceType type) => type switch
        {
            DeviceType.Handheld => ClientDeviceType.Handheld,
            DeviceType.Console => ClientDeviceType.Console,
            DeviceType.Desktop => ClientDeviceType.Desktop,
            _ => ClientDeviceType.Unknown
        };

        internal static ClientPlatform ToClientPlatform(RuntimePlatform platform)
        {
            switch (platform)
            {
                case RuntimePlatform.Android:
                    return ClientPlatform.Android;
                case RuntimePlatform.IPhonePlayer:
                    return ClientPlatform.Ios;
                case RuntimePlatform.WebGLPlayer:
                    return ClientPlatform.WebGl;
                case RuntimePlatform.WindowsPlayer:
                    return ClientPlatform.Windows;
                case RuntimePlatform.OSXPlayer:
                    return ClientPlatform.MacOs;
                case RuntimePlatform.LinuxPlayer:
                    return ClientPlatform.Linux;
                case RuntimePlatform.WindowsEditor:
                case RuntimePlatform.OSXEditor:
                case RuntimePlatform.LinuxEditor:
                    return ClientPlatform.Editor;
                case RuntimePlatform.PS4:
                case RuntimePlatform.PS5:
                case RuntimePlatform.XboxOne:
                case RuntimePlatform.GameCoreXboxSeries:
                case RuntimePlatform.GameCoreXboxOne:
                case RuntimePlatform.Switch:
                    return ClientPlatform.Console;
                default:
                    return ClientPlatform.Unknown;
            }
        }

        private static string Limit(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value == SystemInfo.unsupportedIdentifier)
            {
                return null;
            }

            value = value.Trim();
            return value.Length <= MaxFieldLength ? value : value.Substring(0, MaxFieldLength);
        }
    }
}
