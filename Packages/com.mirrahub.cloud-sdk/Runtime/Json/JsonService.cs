using System;
using System.IO;
using System.Text;

namespace MirraCloud.Json
{
    public class JsonService : IJsonService
    {
        private readonly JsonMapper _mapper;

        public JsonService()
        {
            _mapper = new JsonMapper();
        }
        
        public string ToJson(object val, bool prettyPrint = false)
        {
            var stringBuilder = new StringBuilder();
            using (var stringWriter = new StringWriter(stringBuilder)) {
                _mapper.Write(val, new JsonTokenWriter(stringWriter, prettyPrint));
            }
            return stringBuilder.ToString();
        }

        public T FromJson<T>(string json)
        {
            return _mapper.Read<T>(new JsonTokenReader(new StringReader(json)));
        }

        public JsonService RegisterImporter<T>(IJsonImporter<T> importer)
        {
            _mapper.RegisterImporter(importer.ImportJson);

            return this;
        }
    }
}