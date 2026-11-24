using System;
using System.Collections;
using FreshCatch.Model;
using UnityEngine;

namespace FreshCatch.View
{
    /// <summary>A greybox fish: a sideways capsule whose color drains to grey as it loses freshness.</summary>
    public class FishView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public Fish Fish { get; private set; }

        private Renderer _renderer;
        private MaterialPropertyBlock _mpb;
        private Color _color;
        private Coroutine _motion;
        private Vector3 _baseScale;

        public static FishView Create(Fish fish, Transform parent, Vector3 worldPos)
        {
            var go = Palette.Prim(PrimitiveType.Capsule, parent, Vector3.zero, new Vector3(0.38f, 0.42f, 0.38f),
                Palette.Of(fish.Type), keepCollider: true, name: $"Fish_{fish.Type}_{fish.Id}");
            go.transform.position = worldPos;
            go.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            var v = go.AddComponent<FishView>();
            v.Fish = fish;
            v._color = Palette.Of(fish.Type);
            v._renderer = go.GetComponent<Renderer>();
            v._mpb = new MaterialPropertyBlock();
            v._baseScale = go.transform.localScale;
            return v;
        }

        private void Update()
        {
            if (Fish == null) return;

            Color c;
            if (Fish.IsRotten) c = Palette.Rotten;
            else c = Color.Lerp(Palette.Faded, _color, Mathf.Clamp01(Fish.Normalized * 1.4f - 0.2f));
            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorId, c);
            _renderer.SetPropertyBlock(_mpb);

            // Nervous wiggle once the fish starts fading, as a warning.
            if (_motion == null && Fish.Stage == FreshnessStage.Faded)
            {
                var w = Mathf.Sin(Time.time * 18f) * 6f;
                transform.rotation = Quaternion.Euler(0f, w, 90f);
            }
        }

        /// <summary>Flies along an arc to a world position.</summary>
        public void FlyTo(Vector3 target, float duration = 0.28f, float height = 1.2f, Action onDone = null, bool shrink = false)
        {
            if (_motion != null) StopCoroutine(_motion);
            _motion = StartCoroutine(FlyRoutine(target, duration, height, onDone, shrink));
        }

        private IEnumerator FlyRoutine(Vector3 target, float duration, float height, Action onDone, bool shrink)
        {
            var start = transform.position;
            transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            for (var t = 0f; t < 1f; t += Time.deltaTime / duration)
            {
                var e = 1f - (1f - t) * (1f - t); // ease out
                var p = Vector3.Lerp(start, target, e);
                p.y += Mathf.Sin(t * Mathf.PI) * height;
                transform.position = p;
                if (shrink) transform.localScale = _baseScale * Mathf.Lerp(1f, 0.4f, t);
                yield return null;
            }
            transform.position = target;
            _motion = null;
            if (!shrink) yield return Pop();
            onDone?.Invoke();
        }

        private IEnumerator Pop()
        {
            for (var t = 0f; t < 1f; t += Time.deltaTime / 0.15f)
            {
                transform.localScale = _baseScale * (1f + Mathf.Sin(t * Mathf.PI) * 0.25f);
                yield return null;
            }
            transform.localScale = _baseScale;
        }

        /// <summary>"No" feedback for an invalid tap.</summary>
        public void Shake()
        {
            if (_motion != null) return;
            _motion = StartCoroutine(ShakeRoutine());
        }

        private IEnumerator ShakeRoutine()
        {
            var origin = transform.position;
            for (var t = 0f; t < 1f; t += Time.deltaTime / 0.25f)
            {
                transform.position = origin + Vector3.right * (Mathf.Sin(t * Mathf.PI * 6f) * 0.12f * (1f - t));
                yield return null;
            }
            transform.position = origin;
            _motion = null;
        }
    }
}
