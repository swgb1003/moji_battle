mergeInto(LibraryManager.library, {
  MojiResumeAudio__deps: ['$WEBAudio'],
  MojiResumeAudio: function () {
    if (WEBAudio.audioContext) {
      WEBAudio.audioContext.resume().catch(function (error) {
        console.warn('Audio could not start:', error);
      });
    }
  }
});
