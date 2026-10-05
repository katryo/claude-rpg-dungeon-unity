using UnityEngine;

namespace HD2DRPG
{
    /// <summary>Editable data asset for a skill (MP cost, kind, target, element, power, hits, buff, visual effect).</summary>
    [CreateAssetMenu(menuName = "HD-2D RPG/Skill", fileName = "NewSkill")]
    public class SkillAsset : ScriptableObject
    {
        public SkillDef Def = new SkillDef();
    }
}
