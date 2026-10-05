using UnityEngine;

namespace HD2DRPG
{
    public static class Util
    {
        /// <summary>Destroy that also works when the world is being built in edit mode (scene baking).</summary>
        public static void Kill(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o);
            else Object.DestroyImmediate(o);
        }

        /// <summary>Creates a quad primitive without its collider.</summary>
        public static GameObject Quad(string name)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = name;
            return go;
        }

        /// <summary>Creates a cube primitive without its collider.</summary>
        public static GameObject Cube(string name)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = name;
            return go;
        }
    }

    /// <summary>
    /// Field collision comes from the colliders on walls, pillars and props in the scene, so moving
    /// or deleting them in the editor changes where the party can walk.
    /// </summary>
    public static class WorldCollision
    {
        public static bool CanStand(Vector3 p, float radius)
        {
            return !Physics.CheckSphere(new Vector3(p.x, 0.6f, p.z), radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }
    }
}
