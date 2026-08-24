using MirraCloud.Core.Storage.Blob;

namespace MirraCloud.Core.Storage.Blob.Tests
{
    public sealed class TempFileBlobStorage : FileBlobStorage
    {
        private readonly string _rootPath;

        public TempFileBlobStorage(string rootPath)
        {
            _rootPath = rootPath;
        }

        protected override string RootPath
        {
            get { return _rootPath; }
        }
    }
}
