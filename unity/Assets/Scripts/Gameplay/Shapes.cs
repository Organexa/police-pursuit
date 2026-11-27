using UnityEngine;

namespace PolicePursuit.Gameplay
{
    // Placeholder art: coloured primitives, no external assets.
    public static class Shapes
    {
        public static readonly Color Dark = Hex(0x0A0E1A);
        public static readonly Color Navy = Hex(0x101A3A);
        public static readonly Color Cyan = Hex(0x00E5FF);
        public static readonly Color Magenta = Hex(0xFF2BD6);
        public static readonly Color Red = Hex(0xFF3B3B);

        public static Color Hex(int rgb) =>
            new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);

        static Material flat;

        public static GameObject Box(Transform parent, Vector3 pos, Vector3 size, Color color,
            PrimitiveType type = PrimitiveType.Cube, float glow = 0)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.Destroy(go.GetComponent<Collider>()); // hits are distance checks, not physics
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            if (flat == null) flat = new Material(Resources.Load<Shader>("Flat"));
            go.GetComponent<Renderer>().sharedMaterial = flat;
            Paint(go.GetComponent<Renderer>(), color, glow);
            return go;
        }

        public static void Paint(Renderer r, Color color, float glow = 0)
        {
            var block = new MaterialPropertyBlock();
            block.SetColor("_Color", color * (1 + glow));
            r.SetPropertyBlock(block);
        }

        public static void LightWindows(Renderer r)
        {
            var block = new MaterialPropertyBlock();
            r.GetPropertyBlock(block); // keep the colour
            block.SetFloat("_Windows", 1);
            r.SetPropertyBlock(block);
        }

        public static GameObject Car(Transform parent, string name, Color body)
        {
            var car = new GameObject(name);
            car.transform.SetParent(parent, false);
            Box(car.transform, new Vector3(0, 0.5f, 0), new Vector3(1.8f, 0.6f, 4f), body);
            Box(car.transform, new Vector3(0, 1.05f, -0.3f), new Vector3(1.5f, 0.5f, 1.8f), Dark);
            return car;
        }
    }
}
