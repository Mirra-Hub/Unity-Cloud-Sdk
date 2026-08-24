using System.Collections.Generic;
using System.Threading.Tasks;

namespace MirraCloud.Core.Storage.Blob
{
    public interface IBlobStorage
    {
        Task<IBlobContainer> OpenContainerAsync(string containerId);

        Task DeleteContainerAsync(string containerId);

        Task<IReadOnlyList<string>> ListContainersAsync(string prefix);
    }
}
