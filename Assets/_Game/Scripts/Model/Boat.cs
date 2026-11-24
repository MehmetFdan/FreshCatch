using System.Collections.Generic;

namespace FreshCatch.Model
{
    /// <summary>A boat docked at the pier. Any fish on it can be picked, in any order.</summary>
    public sealed class Boat
    {
        private static int _nextId;
        private readonly List<Fish> _fish;

        public int Id { get; }
        public IReadOnlyList<Fish> Fish => _fish;
        public bool IsEmpty => _fish.Count == 0;

        public Boat(IEnumerable<Fish> fish)
        {
            Id = ++_nextId;
            _fish = new List<Fish>(fish);
        }

        public Fish Take(int index)
        {
            var f = _fish[index];
            _fish.RemoveAt(index);
            return f;
        }
    }
}
