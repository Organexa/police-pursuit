using System.Collections.Generic;
using UnityEngine;

namespace PolicePursuit.Gameplay
{
    // The bought-store car and city (Assets/ThirdParty, a private repository checked out by the build).
    // Without them everything stays placeholder boxes.
    public static class Models
    {
        public const int PlayerLod = 1, TrafficLod = 2;

        public static bool Available => Resources.Load<GameObject>("Car/RMCar26") != null;

        static readonly Dictionary<int, Mesh> meshes = new Dictionary<int, Mesh>();
        static readonly Dictionary<int, Material[]> materials = new Dictionary<int, Material[]>();
        static readonly Dictionary<string, Material> painted = new Dictionary<string, Material>();

        // A car of the game's size (4 long, on the ground, facing +z), merged to one mesh with a few materials:
        // the model has about 200 parts, far too many draw calls for a street full of cars.
        public static GameObject Car(Transform parent, Color paint, int lod)
        {
            if (!meshes.ContainsKey(lod)) Merge(lod);
            var go = new GameObject("Model");
            go.transform.SetParent(parent, false);
            var mesh = meshes[lod];
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>();
            float scale = PlayerCar.HalfLength * 2 / mesh.bounds.size.z;
            go.transform.localScale = Vector3.one * scale;
            go.transform.localPosition = -scale * new Vector3(mesh.bounds.center.x, mesh.bounds.min.y, mesh.bounds.center.z);
            Paint(go.GetComponent<Renderer>(), lod, paint);
            return go;
        }

        // Swaps a placeholder car's two boxes for the model; anything added to the body later (police lights) stays.
        public static void Dress(Transform body, Color paint, int lod)
        {
            if (!Available) return;
            body.GetChild(0).GetComponent<Renderer>().enabled = false;
            body.GetChild(1).GetComponent<Renderer>().enabled = false;
            Car(body, paint, lod);
        }

        public static void Paint(Renderer r, int lod, Color colour)
        {
            var list = (Material[])materials[lod].Clone();
            for (int i = 0; i < list.Length; i++)
            {
                if (!list[i].name.EndsWith("_Paint")) continue;
                string key = ColorUtility.ToHtmlStringRGB(colour);
                if (!painted.TryGetValue(key, out var m) || m == null)
                {
                    painted[key] = m = new Material(list[i]) { name = "Car_" + key };
                    m.SetColor("_BaseColor", colour);
                }
                list[i] = m;
            }
            r.sharedMaterials = list;
        }

        static void Merge(int lod)
        {
            var source = Object.Instantiate(Resources.Load<GameObject>("Car/RMCar26"));
            source.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var parts = new Dictionary<Material, List<CombineInstance>>();
            foreach (var r in source.GetComponentsInChildren<MeshRenderer>())
            {
                int at = r.name.LastIndexOf("_LOD", System.StringComparison.Ordinal);
                if (at >= 0 && r.name.Substring(at + 4) != lod.ToString()) continue;
                var mesh = r.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh == null) continue;
                var shared = r.sharedMaterials;
                for (int sub = 0; sub < mesh.subMeshCount && sub < shared.Length; sub++)
                {
                    if (shared[sub] == null) continue;
                    if (!parts.TryGetValue(shared[sub], out var list)) parts[shared[sub]] = list = new List<CombineInstance>();
                    list.Add(new CombineInstance { mesh = mesh, subMeshIndex = sub, transform = r.transform.localToWorldMatrix });
                }
            }
            var pieces = new List<CombineInstance>();
            var order = new List<Material>();
            foreach (var pair in parts)
            {
                var merged = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                merged.CombineMeshes(pair.Value.ToArray(), true, true);
                pieces.Add(new CombineInstance { mesh = merged, transform = Matrix4x4.identity });
                order.Add(pair.Key);
            }
            var car = new Mesh { name = "Car LOD" + lod, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            car.CombineMeshes(pieces.ToArray(), false, false);
            meshes[lod] = car;
            materials[lod] = order.ToArray();
            source.SetActive(false); // Destroy waits for the end of the frame
            Object.Destroy(source);
        }
    }
}
