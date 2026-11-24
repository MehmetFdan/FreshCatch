using System;
using System.Collections.Generic;
using FreshCatch.Model;
using UnityEngine;

namespace FreshCatch.Data
{
    [CreateAssetMenu(menuName = "FreshCatch/Level", fileName = "Level_")]
    public class LevelData : ScriptableObject
    {
        [Serializable]
        public class FishGroup
        {
            public List<FishType> fish = new List<FishType>();
        }

        [Min(1)] public int stallSlots = 7;
        [Min(1)] public int visibleCustomers = 3;
        [Min(0)] public int maxRottenFish = 2;

        public FreshnessMode freshnessMode = FreshnessMode.MoveBased;
        public float freshnessSeconds = 20f;
        public int freshnessMoves = 8;
        public bool autoServe;

        public List<FishGroup> boats = new List<FishGroup>();
        public List<FishGroup> customers = new List<FishGroup>();

        /// <summary>Returns a fresh, independent definition each call.</summary>
        public LevelDefinition ToDefinition()
        {
            var def = new LevelDefinition
            {
                StallSlots = stallSlots,
                VisibleCustomers = visibleCustomers,
                MaxRottenFish = maxRottenFish,
                FreshnessMode = freshnessMode,
                FreshnessSeconds = freshnessSeconds,
                FreshnessMoves = freshnessMoves,
                AutoServe = autoServe
            };
            foreach (var b in boats) def.Boats.Add(new List<FishType>(b.fish));
            foreach (var c in customers) def.Customers.Add(new List<FishType>(c.fish));
            return def;
        }

        private void OnValidate()
        {
            foreach (var e in ToDefinition().Validate())
                Debug.LogWarning($"[{name}] {e}", this);
        }
    }
}
