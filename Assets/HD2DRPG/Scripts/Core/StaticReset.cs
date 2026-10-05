using UnityEngine;

namespace HD2DRPG
{
    /// <summary>
    /// With "Enter Play Mode Options" (no domain reload) static caches survive from edit mode,
    /// including materials the scene baker turned into assets. Start every play session clean.
    /// </summary>
    static class StaticReset
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Mats.ClearCache();
            SpriteActor.ClearCache();
            Database.Reset();
        }
    }
}
