using System.Collections.Generic;
using static FreshCatch.Model.FishType;

namespace FreshCatch.Model
{
    /// <summary>
    /// Hand-made starter levels so the prototype is playable without any level assets.
    /// Each call returns new instances, so callers may modify them freely.
    /// </summary>
    public static class BuiltInLevels
    {
        public static int Count => All().Count;

        public static LevelDefinition Get(int index)
        {
            var all = All();
            return all[((index % all.Count) + all.Count) % all.Count];
        }

        private static List<FishType> L(params FishType[] t) => new List<FishType>(t);

        public static List<LevelDefinition> All() => new List<LevelDefinition>
        {
            // 1: two types, learn tap -> stall -> customer
            new LevelDefinition
            {
                StallSlots = 5, VisibleCustomers = 2, MaxRottenFish = 2, FreshnessMoves = 12,
                Boats = { L(Hamsi, Hamsi, Levrek, Levrek) },
                Customers = { L(Hamsi, Hamsi), L(Levrek, Levrek) }
            },
            // 2: second boat, mixed orders
            new LevelDefinition
            {
                StallSlots = 5, VisibleCustomers = 2, MaxRottenFish = 2, FreshnessMoves = 12,
                Boats = { L(Hamsi, Levrek, Hamsi, Cipura), L(Cipura, Levrek, Hamsi) },
                Customers = { L(Hamsi, Hamsi), L(Levrek, Cipura), L(Cipura, Levrek, Hamsi) }
            },
            // 3: four types, three customers visible
            new LevelDefinition
            {
                StallSlots = 6, VisibleCustomers = 3, MaxRottenFish = 2, FreshnessMoves = 10,
                Boats = { L(Hamsi, Cipura, Levrek, Hamsi), L(Palamut, Hamsi, Cipura, Levrek), L(Palamut, Cipura, Levrek) },
                Customers = { L(Hamsi, Hamsi, Hamsi), L(Cipura, Levrek), L(Palamut, Palamut), L(Cipura, Levrek), L(Cipura, Levrek) }
            },
            // 4: tighter freshness, only one rotten fish allowed
            new LevelDefinition
            {
                StallSlots = 6, VisibleCustomers = 3, MaxRottenFish = 1, FreshnessMoves = 9,
                Boats = { L(Lufer, Hamsi, Hamsi, Lufer, Cipura), L(Levrek, Lufer, Cipura, Hamsi), L(Levrek, Cipura, Hamsi, Levrek) },
                Customers = { L(Hamsi, Hamsi), L(Lufer, Lufer, Lufer), L(Cipura, Levrek), L(Hamsi, Hamsi, Cipura), L(Levrek, Levrek, Cipura) }
            },
            // 5: five types, bigger boats
            new LevelDefinition
            {
                StallSlots = 7, VisibleCustomers = 3, MaxRottenFish = 1, FreshnessMoves = 8,
                Boats = { L(Palamut, Palamut, Hamsi, Levrek, Cipura, Lufer), L(Hamsi, Palamut, Cipura, Lufer, Levrek), L(Levrek, Hamsi, Cipura, Lufer, Palamut) },
                Customers = { L(Palamut, Palamut, Palamut), L(Hamsi, Levrek), L(Cipura, Cipura), L(Lufer, Lufer, Lufer), L(Hamsi, Hamsi, Levrek), L(Cipura, Levrek, Palamut) }
            },
            // 6: only two customers visible -> stall management matters
            new LevelDefinition
            {
                StallSlots = 6, VisibleCustomers = 2, MaxRottenFish = 1, FreshnessMoves = 14,
                Boats = { L(Hamsi, Hamsi, Levrek, Levrek, Cipura, Cipura), L(Lufer, Lufer, Palamut, Palamut, Hamsi, Levrek), L(Cipura, Lufer, Palamut, Hamsi) },
                Customers = { L(Hamsi, Levrek, Cipura), L(Lufer, Palamut), L(Hamsi, Hamsi, Hamsi), L(Levrek, Levrek), L(Cipura, Cipura), L(Lufer, Lufer, Palamut, Palamut) }
            },
        };
    }
}
