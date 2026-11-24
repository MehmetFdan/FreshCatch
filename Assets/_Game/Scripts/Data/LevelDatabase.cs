using System.Collections.Generic;
using UnityEngine;

namespace FreshCatch.Data
{
    [CreateAssetMenu(menuName = "FreshCatch/Level Database", fileName = "LevelDatabase")]
    public class LevelDatabase : ScriptableObject
    {
        public List<LevelData> levels = new List<LevelData>();
    }
}
