#if UNITY_WEBGL && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;

namespace MirraCloud.Core.AssetsStorage
{
    internal static class AudioBlobUrl
    {
        public static string Create(byte[] bytes)
        {
            IntPtr pointer = BwCreateBlobUrl(bytes, bytes.Length);
            string url = Marshal.PtrToStringUTF8(pointer);
            BwFreeString(pointer);
            return url;
        }

        public static void Revoke(string url)
        {
            if (string.IsNullOrEmpty(url) == false)
            {
                BwRevokeBlobUrl(url);
            }
        }

        [DllImport("__Internal")]
        private static extern IntPtr BwCreateBlobUrl(byte[] data, int length);

        [DllImport("__Internal")]
        private static extern void BwFreeString(IntPtr pointer);

        [DllImport("__Internal")]
        private static extern void BwRevokeBlobUrl(string url);
    }
}
#endif
