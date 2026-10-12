mergeInto(LibraryManager.library, {
    MojiSaveWorldCupPhoto: function (data, length, filename) {
        var bytes = HEAPU8.slice(data, data + length);
        var url = URL.createObjectURL(new Blob([bytes], { type: 'image/png' }));
        var link = document.createElement('a');
        link.href = url;
        link.download = UTF8ToString(filename);
        document.body.appendChild(link);
        link.click();
        link.remove();
        setTimeout(function () { URL.revokeObjectURL(url); }, 60000);
    }
});
