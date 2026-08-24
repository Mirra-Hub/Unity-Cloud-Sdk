using System;
using MirraCloud.Json;

namespace MirraCloud.Core.Groups.Dto.Response
{
    [Serializable]
    public class PaginatedResult<T>
    {
        [JsonNameCamel] public T[] Items;
        [JsonNameCamel] public int TotalCount;
        [JsonNameCamel] public int Page;
        [JsonNameCamel] public int PageSize;
    }
}
