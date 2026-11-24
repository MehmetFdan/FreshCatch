using System.Collections;
using System.Collections.Generic;
using FreshCatch.Model;
using UnityEngine;

namespace FreshCatch.View
{
    /// <summary>
    /// Builds the greybox board for a BoardState and animates it from the model's events.
    /// Layout on the XZ plane (camera looks down at an angle from -Z):
    ///   customers z = +6, stall z = +1.5, boat z = -3.
    /// </summary>
    public class BoardView : MonoBehaviour
    {
        [SerializeField] private Material baseMaterial;

        private const float CustomerZ = 6f;
        private const float StallZ = 1.5f;
        private const float BoatZ = -3.2f;
        private const float CustomerSpacing = 2.8f;
        private const float MaxBoardWidth = 8.4f;

        private BoardState _board;
        private Transform _root;
        private readonly Dictionary<Fish, FishView> _fish = new Dictionary<Fish, FishView>();
        private readonly Dictionary<Customer, CustomerView> _customers = new Dictionary<Customer, CustomerView>();
        private CustomerView[] _customerSlots;
        private Boat _shownBoat;
        private GameObject _boatGo;

        public void Bind(BoardState board)
        {
            Unbind();
            Palette.SetBase(baseMaterial);
            _board = board;

            _root = new GameObject("Board").transform;
            _root.SetParent(transform, false);
            _customerSlots = new CustomerView[board.Level.VisibleCustomers];
            BuildStatic();

            board.FishMovedToStall += OnFishMovedToStall;
            board.FishServed += OnFishServed;
            board.CustomerArrived += OnCustomerArrived;
            board.CustomerLeft += OnCustomerLeft;
        }

        private void Unbind()
        {
            if (_board != null)
            {
                _board.FishMovedToStall -= OnFishMovedToStall;
                _board.FishServed -= OnFishServed;
                _board.CustomerArrived -= OnCustomerArrived;
                _board.CustomerLeft -= OnCustomerLeft;
            }
            StopAllCoroutines();
            if (_root != null) Destroy(_root.gameObject);
            _fish.Clear();
            _customers.Clear();
            _shownBoat = null;
            _boatGo = null;
            _board = null;
        }

        private void OnDestroy() => Unbind();

        // ------------------------------------------------------------------ layout

        private float SlotSpacing => Mathf.Min(1.15f, MaxBoardWidth / Mathf.Max(1, _board.Stall.Capacity));

        public Vector3 SlotPosition(int slot)
        {
            var n = _board.Stall.Capacity;
            var x = (slot - (n - 1) * 0.5f) * SlotSpacing;
            return new Vector3(x, 1.25f, StallZ);
        }

        private Vector3 CustomerPosition(int slot)
        {
            var n = _customerSlots.Length;
            return new Vector3((slot - (n - 1) * 0.5f) * CustomerSpacing, 0f, CustomerZ);
        }

        private static Vector3 BoatFishLocal(int index, int count)
        {
            const int perRow = 4;
            var rows = (count + perRow - 1) / perRow;
            var row = index / perRow;
            var inRow = Mathf.Min(perRow, count - row * perRow);
            var col = index % perRow;
            var x = (col - (inRow - 1) * 0.5f) * 1.05f;
            var z = (row - (rows - 1) * 0.5f) * -0.9f;
            return new Vector3(x, 0.75f, z);
        }

        private void BuildStatic()
        {
            Palette.Prim(PrimitiveType.Cube, _root, new Vector3(0f, -0.1f, -6f), new Vector3(30f, 0.2f, 12f), Palette.Water, name: "Water");
            Palette.Prim(PrimitiveType.Cube, _root, new Vector3(0f, -0.05f, 6f), new Vector3(30f, 0.2f, 12f), Palette.Ground, name: "Market");
            Palette.Prim(PrimitiveType.Cube, _root, new Vector3(0f, 0.1f, -0.6f), new Vector3(30f, 0.2f, 1.6f), Palette.Pier, name: "Pier");

            var width = _board.Stall.Capacity * SlotSpacing + 0.4f;
            Palette.Prim(PrimitiveType.Cube, _root, new Vector3(0f, 0.45f, StallZ), new Vector3(width, 0.9f, 1.4f), Palette.Table, name: "Stall");
            for (var i = 0; i < _board.Stall.Capacity; i++)
            {
                var p = SlotPosition(i);
                Palette.Prim(PrimitiveType.Cube, _root, new Vector3(p.x, 0.97f, StallZ), new Vector3(SlotSpacing * 0.9f, 0.15f, 1.1f), Palette.Crate, name: $"Crate_{i}");
            }
        }

        // ------------------------------------------------------------------ boat

        /// <summary>Call after Start() and after every successful move: docks/undocks boat visuals.</summary>
        public void SyncBoat()
        {
            if (_board.CurrentBoat == _shownBoat) return;

            if (_boatGo != null) StartCoroutine(SailAway(_boatGo));
            _boatGo = null;
            _shownBoat = _board.CurrentBoat;
            if (_shownBoat == null) return;

            _boatGo = new GameObject($"Boat_{_shownBoat.Id}");
            _boatGo.transform.SetParent(_root, false);
            Palette.Prim(PrimitiveType.Cube, _boatGo.transform, new Vector3(0f, 0.25f, 0f), new Vector3(4.8f, 0.5f, 2.2f), Palette.Hull, name: "Hull");
            Palette.Prim(PrimitiveType.Cube, _boatGo.transform, new Vector3(2.7f, 0.25f, 0f), new Vector3(0.8f, 0.45f, 1.4f), Palette.Hull, name: "Bow");

            var count = _shownBoat.Fish.Count;
            for (var i = 0; i < count; i++)
            {
                var fish = _shownBoat.Fish[i];
                var v = FishView.Create(fish, _boatGo.transform, Vector3.zero);
                v.transform.localPosition = BoatFishLocal(i, count);
                _fish[fish] = v;
            }
            StartCoroutine(Dock(_boatGo.transform));
        }

        private IEnumerator Dock(Transform boat)
        {
            var to = new Vector3(0f, 0f, BoatZ);
            var from = to + Vector3.right * 12f;
            for (var t = 0f; t < 1f; t += Time.deltaTime / 0.5f)
            {
                if (boat == null) yield break;
                var e = 1f - Mathf.Pow(1f - t, 3f);
                boat.localPosition = Vector3.Lerp(from, to, e);
                yield return null;
            }
            if (boat != null) boat.localPosition = to;
        }

        private IEnumerator SailAway(GameObject boat)
        {
            yield return new WaitForSeconds(0.3f);
            var from = boat.transform.localPosition;
            var to = from + Vector3.left * 14f;
            for (var t = 0f; t < 1f; t += Time.deltaTime / 0.6f)
            {
                if (boat == null) yield break;
                boat.transform.localPosition = Vector3.Lerp(from, to, t * t);
                yield return null;
            }
            if (boat != null) Destroy(boat);
        }

        // ------------------------------------------------------------------ events

        private void OnFishMovedToStall(Fish fish, int slot)
        {
            if (!_fish.TryGetValue(fish, out var v)) return;
            v.transform.SetParent(_root, true);
            v.FlyTo(SlotPosition(slot));
        }

        private void OnFishServed(Fish fish, Customer customer)
        {
            if (!_fish.TryGetValue(fish, out var v)) return;
            _fish.Remove(fish);
            v.transform.SetParent(_root, true);

            if (_customers.TryGetValue(customer, out var cv))
            {
                cv.Refresh();
                v.FlyTo(cv.BasketPosition, 0.3f, 1.6f, () => Destroy(v.gameObject), shrink: true);
            }
            else
            {
                Destroy(v.gameObject);
            }
        }

        private void OnCustomerArrived(Customer customer)
        {
            var slot = System.Array.IndexOf(_customerSlots, null);
            if (slot < 0) slot = 0;
            var cv = CustomerView.Create(customer, _root, CustomerPosition(slot));
            cv.ArriveFrom(Vector3.left * 8f);
            _customerSlots[slot] = cv;
            _customers[customer] = cv;
        }

        private void OnCustomerLeft(Customer customer)
        {
            if (!_customers.TryGetValue(customer, out var cv)) return;
            _customers.Remove(customer);
            var slot = System.Array.IndexOf(_customerSlots, cv);
            if (slot >= 0) _customerSlots[slot] = null;
            cv.LeaveTo(Vector3.right * 9f, 0.3f);
        }

        /// <summary>Removes a fish's view without animation (e.g. a cleared rotten fish).</summary>
        public void RemoveFish(Fish fish)
        {
            if (!_fish.TryGetValue(fish, out var v)) return;
            _fish.Remove(fish);
            Destroy(v.gameObject);
        }
    }
}
