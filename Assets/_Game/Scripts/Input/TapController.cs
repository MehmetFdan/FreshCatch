using System;
using FreshCatch.View;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FreshCatch.Input
{
    /// <summary>Turns a mouse click or touch into a raycast and reports which fish was tapped.</summary>
    public class TapController : MonoBehaviour
    {
        [SerializeField] private Camera cam;

        public event Action<FishView> FishTapped;

        private void Awake()
        {
            if (cam == null) cam = Camera.main;
        }

        private void Update()
        {
            var pointer = Pointer.current;
            if (pointer == null || !pointer.press.wasPressedThisFrame) return;

            var ray = cam.ScreenPointToRay(pointer.position.ReadValue());
            // Thick ray: fish are small on a phone, so be forgiving.
            if (!Physics.SphereCast(ray, 0.25f, out var hit, 200f)) return;
            var fish = hit.collider.GetComponentInParent<FishView>();
            if (fish != null) FishTapped?.Invoke(fish);
        }
    }
}
