namespace MirraCloud.Core.Storage.Blob
{
    public enum BlobStatus
    {
        Success,
        NotFound,
        Error,
    }

    public readonly struct BlobResult
    {
        public readonly BlobStatus Status;
        public readonly byte[] Value;

        public BlobResult(BlobStatus status, byte[] value)
        {
            Status = status;
            Value = value;
        }

        public bool Success
        {
            get { return Status == BlobStatus.Success; }
        }

        public static BlobResult Ok(byte[] value)
        {
            return new BlobResult(BlobStatus.Success, value);
        }

        public static BlobResult NotFound()
        {
            return new BlobResult(BlobStatus.NotFound, null);
        }

        public static BlobResult Error()
        {
            return new BlobResult(BlobStatus.Error, null);
        }
    }
}
