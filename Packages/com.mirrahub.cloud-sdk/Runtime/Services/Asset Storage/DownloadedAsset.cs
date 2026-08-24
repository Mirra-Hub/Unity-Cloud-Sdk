namespace MirraCloud.Core.AssetsStorage
{
    public readonly struct DownloadedAsset<T>
    {
        public readonly T Value;
        public readonly byte[] Bytes;

        public DownloadedAsset(T value, byte[] bytes)
        {
            Value = value;
            Bytes = bytes;
        }
    }
}
