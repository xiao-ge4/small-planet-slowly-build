mergeInto(LibraryManager.library, {
  SmallWorld_FlushPersistentData: function () {
    var state = Module["smallWorldPersistentStorage"];
    if (!state) {
      state = Module["smallWorldPersistentStorage"] = {
        pending: false,
        running: false,
        timer: null
      };
      state.flush = function () {
        if (state.timer !== null) {
          clearTimeout(state.timer);
          state.timer = null;
        }
        if (state.running || !state.pending) return;
        state.pending = false;
        state.running = true;
        FS.syncfs(false, function (error) {
          state.running = false;
          if (error) console.warn("Small World: browser save synchronization failed.", error);
          if (state.pending) state.flush();
        });
      };
      document.addEventListener("visibilitychange", function () {
        if (document.visibilityState === "hidden") state.flush();
      });
      window.addEventListener("pagehide", function () { state.flush(); });
    }
    state.pending = true;
    if (!state.running && state.timer === null) state.timer = setTimeout(state.flush, 100);
  }
});
