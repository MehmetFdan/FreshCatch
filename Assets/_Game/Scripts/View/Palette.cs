using System.Collections.Generic;
using FreshCatch.Model;
using UnityEngine;

namespace FreshCatch.View
{
    /// <summary>Greybox colors and a material cache so every color shares one material.</summary>
    public static class Palette
    {
        public static readonly Color Water = new Color(0.18f, 0.45f, 0.62f);
        public static readonly Color Pier = new Color(0.55f, 0.42f, 0.30f);
        public static readonly Color Ground = new Color(0.80f, 0.76f, 0.68f);
        public static readonly Color Table = new Color(0.45f, 0.33f, 0.24f);
        public static readonly Color Crate = new Color(0.82f, 0.93f, 0.98f);
        public static readonly Color Hull = new Color(0.85f, 0.85f, 0.80f);
        public static readonly Color Customer = new Color(0.95f, 0.78f, 0.62f);
        public static readonly Color Faded = new Color(0.55f, 0.55f, 0.52f);
        public static readonly Color Rotten = new Color(0.30f, 0.33f, 0.18f);

        public static Color Of(FishType t)
        {
            switch (t)
            {
                case FishType.Hamsi: return new Color(0.30f, 0.65f, 0.95f);
                case FishType.Levrek: return new Color(0.95f, 0.85f, 0.25f);
                case FishType.Cipura: return new Color(0.95f, 0.45f, 0.55f);
                case FishType.Lufer: return new Color(0.40f, 0.85f, 0.45f);
                case FishType.Palamut: return new Color(0.65f, 0.45f, 0.90f);
                default: return Color.white;
            }
        }

        private static readonly Dictionary<Color, Material> Cache = new Dictionary<Color, Material>();
        private static Material _base;

        public static void SetBase(Material baseMaterial)
        {
            if (_base == baseMaterial) return;
            _base = baseMaterial;
            Cache.Clear();
        }

        public static Material Mat(Color c)
        {
            if (Cache.TryGetValue(c, out var m) && m != null) return m;
            m = _base != null ? new Material(_base) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.color = c;
            Cache[c] = m;
            return m;
        }

        /// <summary>Creates a primitive with a shared colored material. Removes the collider unless asked.</summary>
        public static GameObject Prim(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 scale, Color color, bool keepCollider = false, string name = null)
        {
            var go = GameObject.CreatePrimitive(type);
            if (name != null) go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = Mat(color);
            if (!keepCollider) Object.Destroy(go.GetComponent<Collider>());
            return go;
        }
    }
}
