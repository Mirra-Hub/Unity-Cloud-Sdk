namespace MirraCloud.Core.Storage
{
    public interface IStorage
    {
        bool HasKey(string guestIDKey);
        string GetString(string key);
        void SaveString(string key, string value);
        void DeleteKey(string key);
        void DeleteKeys(params string[] key);
    }
}