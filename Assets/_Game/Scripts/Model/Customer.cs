using System.Collections.Generic;

namespace FreshCatch.Model
{
    /// <summary>A customer with a basket order, e.g. 3 Hamsi + 1 Levrek. Order of items doesn't matter.</summary>
    public sealed class Customer
    {
        private static int _nextId;
        private readonly List<FishType> _remaining;

        public int Id { get; }
        public IReadOnlyList<FishType> Order { get; }
        public IReadOnlyList<FishType> Remaining => _remaining;

        public Customer(IEnumerable<FishType> order)
        {
            Id = ++_nextId;
            Order = new List<FishType>(order);
            _remaining = new List<FishType>(Order);
        }

        public bool IsSatisfied => _remaining.Count == 0;

        public bool Wants(FishType type) => _remaining.Contains(type);

        /// <summary>Takes one fish of this type into the basket. Returns false if not wanted.</summary>
        public bool Give(FishType type) => _remaining.Remove(type);
    }
}
