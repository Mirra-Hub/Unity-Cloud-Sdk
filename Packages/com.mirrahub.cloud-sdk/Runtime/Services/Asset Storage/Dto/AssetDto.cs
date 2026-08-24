using System;

namespace MirraCloud.Core.AssetsStorage
{
    [Serializable]
    public class AssetDto : BaseItemStorageDto
    {
        /// <summary>
        /// Survives a re-import and is the same in every environment, so this is the id a game
        /// hard-codes and the one every <c>Load*FromId</c> call takes. Assets have it; folders
        /// do not.
        /// </summary>
        public string stableId;

        /// <summary>Id of the folder this asset sits in, or null at the branch root.</summary>
        public string folderId;
        public string mimeType;
        public long size;
        public int version;
        public AssetType type;
        public string extension;

        /// <summary>Whether the anonymous <c>LoadPublic*</c> routes will serve this asset; a private
        /// one answers 403 there.</summary>
        public bool isPublic;
    }
}