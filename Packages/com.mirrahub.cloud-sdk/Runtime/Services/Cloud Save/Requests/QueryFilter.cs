namespace MirraCloud.Core.CloudSave.Requests
{
    public class QueryFilter
    {
        public string key;
        public CloudSaveIndexOp op = CloudSaveIndexOp.Equal;
        public object value;
        public bool? asc;
    }
}