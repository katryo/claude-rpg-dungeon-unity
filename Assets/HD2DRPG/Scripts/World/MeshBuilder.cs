using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HD2DRPG
{
    /// <summary>Accumulates quads and boxes with world-space-tiled UVs into a single mesh.</summary>
    public class MeshBuilder
    {
        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<Vector3> normals = new List<Vector3>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<int> tris = new List<int>();

        public int VertexCount => verts.Count;

        /// <summary>
        /// Adds a face. <paramref name="origin"/> is the bottom-left corner as seen from the front,
        /// <paramref name="u"/> points right and <paramref name="v"/> points up (both full-length edges).
        /// </summary>
        public void Face(Vector3 origin, Vector3 u, Vector3 v, float uvScale = 1f, Vector2 uvOffset = default, bool worldUV = true)
        {
            int i = verts.Count;
            Vector3 n = Vector3.Cross(v, u).normalized;
            Vector3 a = origin, b = origin + v, c = origin + u + v, d = origin + u;
            verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
            for (int k = 0; k < 4; k++) normals.Add(n);
            if (worldUV)
            {
                Vector3 ud = u.normalized, vd = v.normalized;
                uvs.Add(new Vector2(Vector3.Dot(a, ud), Vector3.Dot(a, vd)) * uvScale + uvOffset);
                uvs.Add(new Vector2(Vector3.Dot(b, ud), Vector3.Dot(b, vd)) * uvScale + uvOffset);
                uvs.Add(new Vector2(Vector3.Dot(c, ud), Vector3.Dot(c, vd)) * uvScale + uvOffset);
                uvs.Add(new Vector2(Vector3.Dot(d, ud), Vector3.Dot(d, vd)) * uvScale + uvOffset);
            }
            else
            {
                float ul = u.magnitude * uvScale, vl = v.magnitude * uvScale;
                uvs.Add(uvOffset);
                uvs.Add(uvOffset + new Vector2(0, vl));
                uvs.Add(uvOffset + new Vector2(ul, vl));
                uvs.Add(uvOffset + new Vector2(ul, 0));
            }
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
        }

        /// <summary>Face with UVs running from (0,0) to <paramref name="uvMax"/>.</summary>
        public void FaceUV(Vector3 origin, Vector3 u, Vector3 v, Vector2 uvMax)
        {
            int i = verts.Count;
            Vector3 n = Vector3.Cross(v, u).normalized;
            verts.Add(origin); verts.Add(origin + v); verts.Add(origin + u + v); verts.Add(origin + u);
            for (int k = 0; k < 4; k++) normals.Add(n);
            uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(0, uvMax.y)); uvs.Add(uvMax); uvs.Add(new Vector2(uvMax.x, 0));
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
        }

        /// <summary>Quad with explicit 0..1 UVs (decals, windows, banners).</summary>
        public void Decal(Vector3 origin, Vector3 u, Vector3 v)
        {
            int i = verts.Count;
            Vector3 n = Vector3.Cross(v, u).normalized;
            verts.Add(origin); verts.Add(origin + v); verts.Add(origin + u + v); verts.Add(origin + u);
            for (int k = 0; k < 4; k++) normals.Add(n);
            uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(0, 1)); uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(1, 0));
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
        }

        [System.Flags]
        public enum Faces { None = 0, Top = 1, Bottom = 2, North = 4, South = 8, East = 16, West = 32, All = 63, Sides = 60 }

        public void Box(Vector3 min, Vector3 max, Faces faces = Faces.All, float uvScale = 1f)
        {
            Vector3 s = max - min;
            if ((faces & Faces.Top) != 0)
                Face(new Vector3(min.x, max.y, min.z), new Vector3(s.x, 0, 0), new Vector3(0, 0, s.z), uvScale);
            if ((faces & Faces.Bottom) != 0)
                Face(new Vector3(min.x, min.y, max.z), new Vector3(s.x, 0, 0), new Vector3(0, 0, -s.z), uvScale);
            if ((faces & Faces.South) != 0) // -Z
                Face(new Vector3(min.x, min.y, min.z), new Vector3(s.x, 0, 0), new Vector3(0, s.y, 0), uvScale);
            if ((faces & Faces.North) != 0) // +Z
                Face(new Vector3(max.x, min.y, max.z), new Vector3(-s.x, 0, 0), new Vector3(0, s.y, 0), uvScale);
            if ((faces & Faces.East) != 0) // +X
                Face(new Vector3(max.x, min.y, min.z), new Vector3(0, 0, s.z), new Vector3(0, s.y, 0), uvScale);
            if ((faces & Faces.West) != 0) // -X
                Face(new Vector3(min.x, min.y, max.z), new Vector3(0, 0, -s.z), new Vector3(0, s.y, 0), uvScale);
        }

        /// <summary>Vertical n-sided prism (pillar shaft) centered at base.</summary>
        public void Prism(Vector3 baseCenter, float radius, float height, int sides = 8)
        {
            float circumference = 2 * Mathf.PI * radius;
            for (int k = 0; k < sides; k++)
            {
                float a0 = k * Mathf.PI * 2 / sides, a1 = (k + 1) * Mathf.PI * 2 / sides;
                Vector3 p0 = baseCenter + new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * radius;
                Vector3 p1 = baseCenter + new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * radius;
                int i = verts.Count;
                Vector3 n0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0));
                Vector3 n1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                verts.Add(p0); verts.Add(p0 + Vector3.up * height); verts.Add(p1 + Vector3.up * height); verts.Add(p1);
                normals.Add(n0); normals.Add(n0); normals.Add(n1); normals.Add(n1);
                float u0 = k / (float)sides * circumference, u1 = (k + 1) / (float)sides * circumference;
                uvs.Add(new Vector2(u0, baseCenter.y)); uvs.Add(new Vector2(u0, baseCenter.y + height));
                uvs.Add(new Vector2(u1, baseCenter.y + height)); uvs.Add(new Vector2(u1, baseCenter.y));
                tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
                tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
            }
        }

        public Mesh Build(string name)
        {
            var m = new Mesh { name = name };
            if (verts.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(verts);
            m.SetNormals(normals);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            m.RecalculateTangents();
            return m;
        }

        public GameObject Create(string name, Transform parent, Material mat, bool castShadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = Build(name);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            r.receiveShadows = true;
            return go;
        }

        /// <summary>Unit quad mesh with pivot at bottom-center, facing -Z (towards a camera looking +Z).</summary>
        public static Mesh SpriteQuad(float w, float h, float pivotY = 0f)
        {
            var mb = new MeshBuilder();
            mb.Decal(new Vector3(-w / 2, -pivotY * h, 0), new Vector3(w, 0, 0), new Vector3(0, h, 0));
            return mb.Build("SpriteQuad");
        }
    }
}
