var AudioBlobUrlLib = {
    BwCreateBlobUrl: function (dataPtr, length) {
        var bytes = HEAPU8.slice(dataPtr, dataPtr + length);
        var blob = new Blob([bytes]);
        var url = URL.createObjectURL(blob);
        var size = lengthBytesUTF8(url) + 1;
        var buffer = _malloc(size);
        stringToUTF8(url, buffer, size);
        return buffer;
    },

    BwFreeString: function (ptr) {
        _free(ptr);
    },

    BwRevokeBlobUrl: function (urlPtr) {
        URL.revokeObjectURL(UTF8ToString(urlPtr));
    }
};

mergeInto(LibraryManager.library, AudioBlobUrlLib);
