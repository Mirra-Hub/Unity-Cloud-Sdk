using System.Threading.Tasks;

namespace Plugins.MirraCloud.Core.General.AsyncOperations
{
    public interface IBaseAsyncOperation
    {
        bool IsDone { get; }
        Task Task();
    }
}