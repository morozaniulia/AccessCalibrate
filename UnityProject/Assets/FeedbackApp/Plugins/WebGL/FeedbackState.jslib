// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.
// The feedback build publishes its current screen as window.accessCalibrateState, for browser tests.
// (Copying to the clipboard is in the package: Runtime/Plugins/WebGL/AccessCalibrateClipboard.jslib.)
mergeInto(LibraryManager.library, {
    // Current screen, for browser tests (see FeedbackApp.State).
    AC_SetState: function (statePtr) {
        window.accessCalibrateState = UTF8ToString(statePtr);
    }
});
