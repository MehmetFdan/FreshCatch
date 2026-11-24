using System.Collections.Generic;

namespace FreshCatch.Model
{
    /// <summary>The ice-crate slots on the stall. Slot indices are stable so views can bind to them.</summary>
    public sealed class Stall
    {
        private readonly Fish[] _slots;

        public Stall(int slotCount)
        {
            _slots = new Fish[slotCount < 1 ? 1 : slotCount];
        }

        public int Capacity => _slots.Length;

        public Fish this[int index] => _slots[index];

        public int Count
        {
            get
            {
                var n = 0;
                foreach (var f in _slots) if (f != null) n++;
                return n;
            }
        }

        public bool IsFull => Count == _slots.Length;

        public IEnumerable<Fish> Fish
        {
            get { foreach (var f in _slots) if (f != null) yield return f; }
        }

        /// <summary>Puts the fish in the first empty slot. Returns the slot index, or -1 if full.</summary>
        public int TryAdd(Fish fish)
        {
            for (var i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] != null) continue;
                _slots[i] = fish;
                return i;
            }
            return -1;
        }

        public Fish RemoveAt(int index)
        {
            var f = _slots[index];
            _slots[index] = null;
            return f;
        }

        public int IndexOf(Fish fish) => System.Array.IndexOf(_slots, fish);
    }
}
