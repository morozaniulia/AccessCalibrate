// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace AccessCalibration
{
    /// <summary>
    /// Puts text on the system clipboard. In a WebGL build the browser does it (navigator.clipboard,
    /// with a fallback), which needs a secure page (https or localhost) and, in some browsers, a
    /// recent click or key press; the result arrives a moment later. Nothing is sent anywhere.
    /// </summary>
    public static class Clipboard
    {
        static Action<bool> s_Pending;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void AC_CopyToClipboard(string text, string objectName, string methodName);
#endif

        /// <summary>Copies <paramref name="text"/>; <paramref name="done"/> gets true when it worked.</summary>
        public static void Copy(string text, Action<bool> done = null)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            s_Pending = done;
            AC_CopyToClipboard(text, Receiver.Instance.gameObject.name, nameof(Receiver.OnCopied));
#else
            GUIUtility.systemCopyBuffer = text;
            done?.Invoke(true);
#endif
        }

        /// <summary>Receives the browser's answer (SendMessage needs a GameObject).</summary>
        sealed class Receiver : MonoBehaviour
        {
            static Receiver s_Instance;
            internal static Receiver Instance
            {
                get
                {
                    if (s_Instance == null)
                    {
                        var go = new GameObject("AccessCalibrate Clipboard") { hideFlags = HideFlags.HideInHierarchy };
                        DontDestroyOnLoad(go);
                        s_Instance = go.AddComponent<Receiver>();
                    }
                    return s_Instance;
                }
            }

            public void OnCopied(string result)
            {
                var done = s_Pending;
                s_Pending = null;
                done?.Invoke(result == "ok");
            }
        }
    }
}
