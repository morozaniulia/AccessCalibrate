// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;

namespace AccessCalibration.Demo
{
    /// <summary>
    /// Loads the Quaternius models (CC0) from Resources/AccessCalibrateDemo/Models and sizes them.
    /// If a model is missing, a coloured primitive is used instead so the demo still runs.
    /// </summary>
    public static class DemoAssets
    {
        const string Folder = "AccessCalibrateDemo/Models/";

        /// <summary>
        /// Instantiates a model scaled so its largest horizontal side (or its height when
        /// <paramref name="byHeight"/>) equals <paramref name="size"/>, standing on <paramref name="position"/>.
        /// </summary>
        public static GameObject Spawn(string model, Vector3 position, float size, Transform parent,
                                       bool byHeight = false, float yaw = 0f, PrimitiveType fallback = PrimitiveType.Cube,
                                       Color? fallbackColor = null)
        {
            var prefab = Resources.Load<GameObject>(Folder + model);
            GameObject go;
            if (prefab != null)
            {
                go = Object.Instantiate(prefab, parent);
                go.name = model;
                foreach (var c in go.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            }
            else
            {
                go = GameObject.CreatePrimitive(fallback);
                go.name = model + " (fallback)";
                go.transform.SetParent(parent, false);
                Object.Destroy(go.GetComponent<Collider>());
                var r = go.GetComponent<Renderer>();
                if (fallbackColor.HasValue && r != null) r.material.color = fallbackColor.Value;
            }

            // Keep the model's own import rotation (Blender exports are rotated −90° on X) and add the yaw.
            Quaternion own = prefab != null ? prefab.transform.localRotation : Quaternion.identity;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0) * own;
            go.transform.localScale = Vector3.one;
            var b = Bounds(go);
            float current = byHeight ? b.size.y : Mathf.Max(b.size.x, b.size.z);
            if (current > 0.0001f) go.transform.localScale = Vector3.one * (size / current);

            // Put the bottom of the model on the requested point, centred horizontally.
            b = Bounds(go);
            go.transform.position += position - new Vector3(b.center.x, b.min.y, b.center.z);
            return go;
        }

        public static Bounds Bounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = renderers[0].bounds;
            foreach (var r in renderers.Skip(1)) b.Encapsulate(r.bounds);
            return b;
        }

        /// <summary>Animation clips stored inside a model file, by short name ("Run", "Idle"…).</summary>
        public static Dictionary<string, AnimationClip> Clips(string model)
        {
            var result = new Dictionary<string, AnimationClip>();
            foreach (var clip in Resources.LoadAll<AnimationClip>(Folder + model))
            {
                if (clip.name.StartsWith("__preview__")) continue;
                string shortName = clip.name.Contains("|") ? clip.name.Substring(clip.name.LastIndexOf('|') + 1) : clip.name;
                result[shortName] = clip;
            }
            return result;
        }
    }

    /// <summary>
    /// Plays one animation clip at a time on a model, without an Animator Controller asset
    /// (Playables API), looping by default.
    /// </summary>
    public sealed class SimpleAnimator : MonoBehaviour
    {
        PlayableGraph _graph;
        AnimationPlayableOutput _output;
        AnimationClipPlayable _current;
        Dictionary<string, AnimationClip> _clips;
        string _playing;
        bool _loop;

        public void Init(Dictionary<string, AnimationClip> clips)
        {
            _clips = clips;
            var animator = GetComponentInChildren<Animator>();
            if (animator == null) animator = gameObject.AddComponent<Animator>();
            animator.applyRootMotion = false;
            _graph = PlayableGraph.Create(name + " Animation");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            _output = AnimationPlayableOutput.Create(_graph, "Output", animator);
            _graph.Play();
        }

        /// <summary>Plays a clip. Asking for the clip already playing does nothing, unless <paramref name="restart"/>.</summary>
        public void Play(string clipName, bool loop = true, bool restart = false)
        {
            if (_clips == null || !_clips.TryGetValue(clipName, out var clip)) return;
            if (clipName == _playing && !restart) return;
            if (_current.IsValid()) _current.Destroy();
            _current = AnimationClipPlayable.Create(_graph, clip);
            _output.SetSourcePlayable(_current);
            _playing = clipName;
            _loop = loop;
        }

        void Update()
        {
            if (!_current.IsValid()) return;
            double length = _current.GetAnimationClip().length;
            if (length <= 0) return;
            double t = _current.GetTime();
            if (_loop && t > length) _current.SetTime(t % length);
            else if (!_loop && t > length) _current.SetTime(length);
        }

        void OnDestroy()
        {
            if (_graph.IsValid()) _graph.Destroy();
        }
    }
}
