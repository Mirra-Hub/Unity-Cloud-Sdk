using MirraCloud.Core.Storage.Blob;

namespace MirraCloud.Core.Storage.Blob.Tests
{
    public sealed class TempSqliteBlobStorage : SqliteBlobStorage
    {
        private readonly string _rootPath;

        public TempSqliteBlobStorage(string rootPath)
        {
            _rootPath = rootPath;
        }

        protected override string RootPath
        {
            get { return _rootPath; }
        }
    }
}
