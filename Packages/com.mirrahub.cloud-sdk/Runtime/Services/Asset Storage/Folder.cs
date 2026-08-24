namespace MirraCloud.Core.AssetsStorage
{
    public class Folder : BaseItemStorage
    {
        public readonly string ParentFolderId;
        
        public Folder(FolderDto dto) : base(dto)
        {
            ParentFolderId = dto.parentFolderId;
        }
    }
}