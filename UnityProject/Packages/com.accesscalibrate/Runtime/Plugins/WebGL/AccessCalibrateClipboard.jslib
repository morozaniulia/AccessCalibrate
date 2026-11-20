// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.
// Copy text to the system clipboard from a WebGL build (AccessCalibration.Clipboard). Browsers allow
// this only on a secure page (https or localhost); some also require a recent key press or click
// (a gamepad button doesn't count). The result ("ok" / "fail") goes back to Unity with SendMessage.
// No network access.
mergeInto(LibraryManager.library, {
    AC_CopyToClipboard: function (textPtr, objectPtr, methodPtr) {
        var text = UTF8ToString(textPtr);
        var objectName = UTF8ToString(objectPtr);
        var methodName = UTF8ToString(methodPtr);

        function refocus() {
            var canvas = Module.canvas || document.querySelector("canvas");
            if (canvas && canvas.focus) canvas.focus();
        }
        function done(ok) {
            window.accessCalibrateCopy = ok ? "ok" : "fail";   // for browser tests
            refocus();
            SendMessage(objectName, methodName, ok ? "ok" : "fail");
        }
        function fallback() {
            var ok = false;
            try {
                var area = document.createElement("textarea");
                area.value = text;
                area.setAttribute("readonly", "");
                area.style.position = "fixed";
                area.style.left = "-9999px";
                document.body.appendChild(area);
                area.select();
                ok = document.execCommand("copy");
                document.body.removeChild(area);
            } catch (e) { ok = false; }
            done(ok);
        }

        if (navigator.clipboard && window.isSecureContext) {
            navigator.clipboard.writeText(text).then(function () { done(true); }, fallback);
        } else {
            fallback();
        }
    }
});
