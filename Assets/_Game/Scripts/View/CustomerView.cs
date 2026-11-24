using System.Collections;
using System.Collections.Generic;
using FreshCatch.Model;
using UnityEngine;

namespace FreshCatch.View
{
    /// <summary>A greybox customer: a capsule body with a row of colored order bubbles above the head.</summary>
    public class CustomerView : MonoBehaviour
    {
        public Customer Customer { get; private set; }

        private readonly List<GameObject> _icons = new List<GameObject>();
        private Transform _iconRoot;

        public static CustomerView Create(Customer customer, Transform parent, Vector3 worldPos)
        {
            var go = new GameObject($"Customer_{customer.Id}");
            go.transform.SetParent(parent, false);
            go.transform.position = worldPos;

            Palette.Prim(PrimitiveType.Capsule, go.transform, new Vector3(0f, 0.9f, 0f), new Vector3(0.9f, 0.9f, 0.9f), Palette.Customer, name: "Body");
            Palette.Prim(PrimitiveType.Cylinder, go.transform, new Vector3(0f, 0.25f, -0.55f), new Vector3(0.9f, 0.25f, 0.5f), Palette.Table, name: "Basket");

            var v = go.AddComponent<CustomerView>();
            v.Customer = customer;
            v._iconRoot = new GameObject("Order").transform;
            v._iconRoot.SetParent(go.transform, false);
            v._iconRoot.localPosition = new Vector3(0f, 2.3f, 0f);
            v.Refresh();
            return v;
        }

        public Vector3 BasketPosition => transform.position + new Vector3(0f, 0.5f, -0.55f);

        /// <summary>Rebuilds the order bubbles from what the customer still wants.</summary>
        public void Refresh()
        {
            foreach (var i in _icons) Destroy(i);
            _icons.Clear();

            var remaining = Customer.Remaining;
            const float spacing = 0.42f;
            var startX = -(remaining.Count - 1) * spacing * 0.5f;
            for (var i = 0; i < remaining.Count; i++)
            {
                var icon = Palette.Prim(PrimitiveType.Sphere, _iconRoot, new Vector3(startX + i * spacing, 0f, 0f),
                    Vector3.one * 0.36f, Palette.Of(remaining[i]), name: remaining[i].ToString());
                _icons.Add(icon);
            }
        }

        public void ArriveFrom(Vector3 offset)
        {
            StartCoroutine(Slide(transform.position + offset, transform.position, 0.35f, false));
        }

        public void LeaveTo(Vector3 offset, float delay)
        {
            StartCoroutine(LeaveRoutine(offset, delay));
        }

        private IEnumerator LeaveRoutine(Vector3 offset, float delay)
        {
            yield return new WaitForSeconds(delay);
            // Happy hop, then walk off.
            var p = transform.position;
            for (var t = 0f; t < 1f; t += Time.deltaTime / 0.2f)
            {
                transform.position = p + Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.5f;
                yield return null;
            }
            yield return Slide(p, p + offset, 0.4f, true);
        }

        private IEnumerator Slide(Vector3 from, Vector3 to, float duration, bool destroyAtEnd)
        {
            for (var t = 0f; t < 1f; t += Time.deltaTime / duration)
            {
                transform.position = Vector3.Lerp(from, to, t * t * (3f - 2f * t));
                yield return null;
            }
            transform.position = to;
            if (destroyAtEnd) Destroy(gameObject);
        }
    }
}
