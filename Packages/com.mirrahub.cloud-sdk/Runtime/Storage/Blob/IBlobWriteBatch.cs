using System.Threading.Tasks;

namespace MirraCloud.Core.Storage.Blob
{
    public interface IBlobWriteBatch
    {
        void Put(string key, byte[] data);

        void Delete(string key);

        Task CommitAsync();
    }
}
