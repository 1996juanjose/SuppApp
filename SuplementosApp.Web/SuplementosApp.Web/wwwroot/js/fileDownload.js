window.downloadFileFromBase64 = function (fileName, contentType, base64Content) {
    const link = document.createElement('a');
    link.download = fileName;
    link.href = 'data:' + contentType + ';base64,' + base64Content;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
};
