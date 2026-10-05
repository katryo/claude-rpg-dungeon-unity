using UnityEngine;

namespace HD2DRPG
{
    /// <summary>Editable data asset for an enemy (stats, shield, weaknesses, rewards, skills).</summary>
    [CreateAssetMenu(menuName = "HD-2D RPG/Enemy", fileName = "NewEnemy")]
    public class EnemyAsset : ScriptableObject
    {
        public EnemyDef Def = new EnemyDef();
    }
}
