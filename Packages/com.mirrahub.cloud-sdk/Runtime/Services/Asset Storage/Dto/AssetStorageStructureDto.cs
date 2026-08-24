using System;
using System.Collections.Generic;

namespace MirraCloud.Core.AssetsStorage
{
    [Serializable]
    public class AssetStorageStructureDto
    {
        public List<FolderDto> folders;
        public List<AssetDto> assets;
    }
}