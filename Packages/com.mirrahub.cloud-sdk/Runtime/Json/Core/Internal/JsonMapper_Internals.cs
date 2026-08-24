using System;
using System.Collections.Generic;

namespace MirraCloud.Json {
    public partial class JsonMapper {
        static readonly JsonMapper defaultInstance = new();

        /////////////////////////////////////////////////

        delegate void ExporterFunc(object obj, JsonMapper mapper, JsonTokenWriter os);
        delegate object ImporterFunc(JsonMapper mapper, JsonTokenReader tokenReader);

        readonly Dictionary<Type, ImporterFunc> importers = new();
        readonly Dictionary<Type, ExporterFunc> exporters = new();
    }
}
