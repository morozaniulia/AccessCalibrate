// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.

using UnityEngine;
using UnityEngine.UIElements;

namespace AccessCalibration
{
    /// <summary>
    /// The full-screen background of the calibration screens, read from the theme tokens:
    /// a vertical gradient (--ac-bg-top → --ac-bg-bottom, drawn here), an optional image covering
    /// the screen (--ac-bg-image, set in USS; Dark uses the galaxy, the other themes set it to none)
    /// and a colour laid over the image (--ac-bg-overlay).
    /// </summary>
    public sealed class ThemeBackdrop : VisualElement
    {
        static readonly CustomStyleProperty<Color> s_Top = new CustomStyleProperty<Color>("--ac-backdrop-top");
        static readonly CustomStyleProperty<Color> s_Bottom = new CustomStyleProperty<Color>("--ac-backdrop-bottom");

        readonly VisualElement _image = new VisualElement { name = "ac-backdrop-image", pickingMode = PickingMode.Ignore };
        readonly VisualElement _overlay = new VisualElement { name = "ac-backdrop-overlay", pickingMode = PickingMode.Ignore };
        Color _top = Color.black, _bottom = Color.black;

        /// <summary>The image in use (null when the theme has none).</summary>
        public Texture2D Image => _image.resolvedStyle.backgroundImage.texture;
        public Color Top => _top;
        public Color Bottom => _bottom;

        public ThemeBackdrop()
        {
            name = "ac-backdrop";
            AddToClassList("ac-backdrop");
            _image.AddToClassList("ac-backdrop-image");
            _overlay.AddToClassList("ac-backdrop-overlay");
            pickingMode = PickingMode.Ignore;
            Add(_image);
            Add(_overlay);
            generateVisualContent += Draw;
            RegisterCallback<CustomStyleResolvedEvent>(OnStyle);
        }

        void OnStyle(CustomStyleResolvedEvent e)
        {
            if (!e.customStyle.TryGetValue(s_Top, out _top)) _top = Color.black;
            if (!e.customStyle.TryGetValue(s_Bottom, out _bottom)) _bottom = _top;
            MarkDirtyRepaint();
        }

        void Draw(MeshGenerationContext mgc)
        {
            var r = contentRect;
            if (r.width <= 0 || r.height <= 0) return;
            var mesh = mgc.Allocate(4, 6);
            float z = Vertex.nearZ;
            mesh.SetNextVertex(new Vertex { position = new Vector3(r.xMin, r.yMin, z), tint = _top });
            mesh.SetNextVertex(new Vertex { position = new Vector3(r.xMax, r.yMin, z), tint = _top });
            mesh.SetNextVertex(new Vertex { position = new Vector3(r.xMax, r.yMax, z), tint = _bottom });
            mesh.SetNextVertex(new Vertex { position = new Vector3(r.xMin, r.yMax, z), tint = _bottom });
            mesh.SetNextIndex(0); mesh.SetNextIndex(1); mesh.SetNextIndex(2);
            mesh.SetNextIndex(0); mesh.SetNextIndex(2); mesh.SetNextIndex(3);
        }
    }
}
