using System;
using System.Collections.Generic;

namespace FreshCatch.Model
{
    /// <summary>
    /// The whole game board and its rules. Pure C#: views subscribe to the events and animate.
    ///
    /// Moves:
    ///  - TapBoatFish(i): moves fish i from the docked boat to the first free stall slot
    ///    (or straight to a customer when AutoServe is on and someone wants it).
    ///  - TapStallFish(slot): gives that stall fish to the first active customer who wants it.
    /// Rotten fish stay in their slot and block it.
    /// </summary>
    public sealed class BoardState
    {
        private readonly LevelDefinition _level;
        private readonly Queue<List<FishType>> _boatQueue;
        private readonly Queue<List<FishType>> _customerQueue;
        private readonly List<Customer> _activeCustomers = new List<Customer>();

        public Stall Stall { get; }
        public Boat CurrentBoat { get; private set; }
        public IReadOnlyList<Customer> ActiveCustomers => _activeCustomers;
        public int RottenCount { get; private set; }
        public int MoveCount { get; private set; }
        public int CustomersServed { get; private set; }
        public int TotalCustomers { get; }
        public GameResult Result { get; private set; } = GameResult.Playing;
        public LoseReason LoseReason { get; private set; } = LoseReason.None;
        public LevelDefinition Level => _level;

        public int BoatsRemaining => _boatQueue.Count;
        public IEnumerable<FishType> NextBoatPreview => _boatQueue.Count > 0 ? _boatQueue.Peek() : null;

        // ---- Events (views listen to these) ----
        public event Action<Fish, int> FishMovedToStall;          // fish, slot
        public event Action<Fish, Customer> FishServed;            // fish, customer (from stall or boat)
        public event Action<Fish> FishRotted;
        public event Action<Customer> CustomerArrived;
        public event Action<Customer> CustomerLeft;
        public event Action<Boat> BoatDocked;
        public event Action<GameResult, LoseReason> GameEnded;

        public BoardState(LevelDefinition level)
        {
            _level = level ?? throw new ArgumentNullException(nameof(level));
            Stall = new Stall(level.StallSlots);
            _boatQueue = new Queue<List<FishType>>(level.Boats);
            _customerQueue = new Queue<List<FishType>>(level.Customers);
            TotalCustomers = level.Customers.Count;
        }

        /// <summary>Docks the first boat and seats the first customers. Call once, after subscribing.</summary>
        public void Start()
        {
            FillCustomers();
            DockNextBoat();
            Evaluate();
        }

        // ------------------------------------------------------------------ moves

        public bool CanTapBoatFish(int index) =>
            Result == GameResult.Playing && CurrentBoat != null &&
            index >= 0 && index < CurrentBoat.Fish.Count &&
            (!Stall.IsFull || (_level.AutoServe && FindCustomerFor(CurrentBoat.Fish[index].Type) != null));

        public bool TapBoatFish(int index)
        {
            if (!CanTapBoatFish(index)) return false;

            var fish = CurrentBoat.Take(index);
            Customer target = _level.AutoServe ? FindCustomerFor(fish.Type) : null;
            if (target != null)
            {
                Serve(fish, target);
            }
            else
            {
                var slot = Stall.TryAdd(fish);
                FishMovedToStall?.Invoke(fish, slot);
            }

            if (CurrentBoat.IsEmpty) DockNextBoat();
            EndMove();
            return true;
        }

        public bool CanTapStallFish(int slot)
        {
            if (Result != GameResult.Playing || slot < 0 || slot >= Stall.Capacity) return false;
            var fish = Stall[slot];
            return fish != null && !fish.IsRotten && FindCustomerFor(fish.Type) != null;
        }

        public bool TapStallFish(int slot)
        {
            if (!CanTapStallFish(slot)) return false;
            var fish = Stall.RemoveAt(slot);
            Serve(fish, FindCustomerFor(fish.Type));
            EndMove();
            return true;
        }

        /// <summary>Advances real time. Only does anything in TimeBased mode.</summary>
        public void Tick(float deltaSeconds)
        {
            if (Result != GameResult.Playing || _level.FreshnessMode != FreshnessMode.TimeBased) return;
            AgeAll(deltaSeconds);
            Evaluate();
        }

        // ------------------------------------------------------------------ boosters

        /// <summary>Ice bucket: restores freshness on every non-rotten fish on the stall and the boat.</summary>
        public void RefreshAll(float amount)
        {
            foreach (var f in Stall.Fish) f.Refresh(amount);
            if (CurrentBoat != null) foreach (var f in CurrentBoat.Fish) f.Refresh(amount);
        }

        /// <summary>Removes a rotten fish from its slot (cleanup booster). Doesn't reduce RottenCount.</summary>
        public bool ClearRotten(int slot)
        {
            var f = Stall[slot];
            if (f == null || !f.IsRotten) return false;
            Stall.RemoveAt(slot);
            Evaluate();
            return true;
        }

        // ------------------------------------------------------------------ internals

        private Customer FindCustomerFor(FishType type)
        {
            foreach (var c in _activeCustomers) if (c.Wants(type)) return c;
            return null;
        }

        private void Serve(Fish fish, Customer customer)
        {
            customer.Give(fish.Type);
            FishServed?.Invoke(fish, customer);
            if (!customer.IsSatisfied) return;

            _activeCustomers.Remove(customer);
            CustomersServed++;
            CustomerLeft?.Invoke(customer);
            FillCustomers();
        }

        private void FillCustomers()
        {
            while (_activeCustomers.Count < _level.VisibleCustomers && _customerQueue.Count > 0)
            {
                var c = new Customer(_customerQueue.Dequeue());
                _activeCustomers.Add(c);
                CustomerArrived?.Invoke(c);
            }
        }

        private void DockNextBoat()
        {
            if (_boatQueue.Count == 0) { CurrentBoat = null; return; }
            var max = _level.MaxFreshness;
            var fish = new List<Fish>();
            foreach (var t in _boatQueue.Dequeue()) fish.Add(new Fish(t, max));
            CurrentBoat = new Boat(fish);
            BoatDocked?.Invoke(CurrentBoat);
        }

        private void EndMove()
        {
            MoveCount++;
            if (_level.FreshnessMode == FreshnessMode.MoveBased) AgeAll(1f);
            Evaluate();
        }

        /// <summary>Only fish on the stall age; fish on the boat are still on ice.</summary>
        private void AgeAll(float amount)
        {
            foreach (var f in Stall.Fish)
            {
                if (!f.Age(amount)) continue;
                RottenCount++;
                FishRotted?.Invoke(f);
            }
        }

        private void Evaluate()
        {
            if (Result != GameResult.Playing) return;

            if (_activeCustomers.Count == 0 && _customerQueue.Count == 0)
            {
                End(GameResult.Won, LoseReason.None);
                return;
            }
            if (RottenCount > _level.MaxRottenFish)
            {
                End(GameResult.Lost, LoseReason.TooManyRotten);
                return;
            }
            if (!HasAnyMove())
            {
                End(GameResult.Lost, LoseReason.NoMovesLeft);
            }
        }

        public bool HasAnyMove()
        {
            for (var i = 0; i < Stall.Capacity; i++) if (CanTapStallFish(i)) return true;
            if (CurrentBoat != null)
                for (var i = 0; i < CurrentBoat.Fish.Count; i++) if (CanTapBoatFish(i)) return true;
            return false;
        }

        private void End(GameResult result, LoseReason reason)
        {
            Result = result;
            LoseReason = reason;
            GameEnded?.Invoke(result, reason);
        }
    }
}
