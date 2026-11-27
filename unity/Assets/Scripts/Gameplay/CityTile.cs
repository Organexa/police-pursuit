using UnityEngine;

namespace PolicePursuit.Gameplay
{
    // One stretch of street from the city models; RoadTiles recycles it like the placeholder tile.
    public static class CityTile
    {
        public static void Build(Transform tile, int index, float length)
        {
            float half = PlayerCar.RoadHalfWidth + 1;
            // The road picture is two lanes wide with markings; only its plain strip is used (textures clamp).
            var road = Tiled("City/asphalt_2-5_tracks", new Vector2(0.27f, 1), new Vector2(0.2f, 0));
            var walk = Tiled("City/sideway_tile", Vector2.one, Vector2.zero);

            Slab(tile, new Vector3(0, -0.1f, -length / 4), new Vector3(half * 2, 0.2f, length / 2), road);
            Slab(tile, new Vector3(0, -0.1f, length / 4), new Vector3(half * 2, 0.2f, length / 2), road);
            for (float z = -15; z <= 15; z += 10)
                foreach (float x in new[] { -2f, 2f })
                    Shapes.Box(tile, new Vector3(x, 0.01f, z), new Vector3(0.15f, 0.02f, 4), new Color(0.8f, 0.8f, 0.8f));

            foreach (int side in new[] { -1, 1 })
            {
                Slab(tile, new Vector3(side * (half + 2.5f), 0, 0), new Vector3(5, 0.3f, length), walk);
                Place(tile, "City/lamp_pole_single_" + (side < 0 ? "orange" : "white"),
                    new Vector3(side * (half + 0.6f), 0, side * 10), -side * 90);
                Place(tile, "City/tree_3", new Vector3(side * (half + 3.5f), 0.15f, -side * 10), 0);

                var building = Place(tile, "City/office_building_" + (1 + (index * 3 + (side + 1) / 2) % 4),
                    Vector3.zero, -side * 90);
                // Fit one building to the tile and set its front just behind the pavement.
                var bounds = Bounds(building);
                float fit = Mathf.Min(1, (length - 1) / bounds.size.z);
                building.transform.localScale *= fit;
                bounds = Bounds(building);
                float front = side < 0 ? bounds.max.x : bounds.min.x;
                building.transform.position += new Vector3(side * (half + 5.5f) - front,
                    0, tile.position.z - bounds.center.z);
            }
            if (index == 0) Place(tile, "City/highway_support", new Vector3(0, 9, 0), 0);
        }

        static GameObject Place(Transform tile, string name, Vector3 pos, float yaw)
        {
            var prefab = Resources.Load<GameObject>(name);
            var go = Object.Instantiate(prefab, tile);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            foreach (var c in go.GetComponentsInChildren<Collider>()) Object.Destroy(c); // hits are distance checks
            return go;
        }

        static Bounds Bounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        static Material Tiled(string name, Vector2 scale, Vector2 offset)
        {
            var m = new Material(Resources.Load<Material>(name));
            foreach (var map in new[] { "_BaseMap", "_BumpMap" })
            {
                m.SetTextureScale(map, scale);
                m.SetTextureOffset(map, offset);
            }
            return m;
        }

        static void Slab(Transform tile, Vector3 pos, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(tile, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material;
        }
    }
}
