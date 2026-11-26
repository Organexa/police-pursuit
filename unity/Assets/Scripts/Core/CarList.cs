namespace PolicePursuit.Core
{
    public sealed class Car
    {
        public readonly string Id, Name;
        public readonly float TopSpeed, Handling, Armor; // m/s, steer m/s, share of damage blocked
        public readonly int Price, Color; // coins, 0xRRGGBB
        public readonly bool Premium; // IAP only

        public Car(string id, string name, float topSpeed, float handling, float armor, int price, bool premium, int color)
        {
            Id = id; Name = name; TopSpeed = topSpeed; Handling = handling; Armor = armor;
            Price = price; Premium = premium; Color = color;
        }
    }

    // Must match shared/cars.json (submit-score reads that file); an EditMode test checks it.
    public static class CarList
    {
        public static readonly Car[] All =
        {
            new Car("starter", "Neon Runner", 30f, 7.0f, 0.0f, 0, false, 0x00E5FF),
            new Car("hatch", "Alley Cat", 31f, 8.5f, 0.0f, 500, false, 0x7CFF4F),
            new Car("muscle", "Night Brute", 34f, 6.5f, 0.2f, 1500, false, 0xFF8A1F),
            new Car("sport", "Volt GT", 37f, 8.0f, 0.1f, 3500, false, 0xFFE600),
            new Car("tank", "Iron Ox", 32f, 6.0f, 0.4f, 5000, false, 0x9AA4B2),
            new Car("phantom", "Phantom X", 40f, 9.0f, 0.25f, 0, true, 0xB14CFF)
        };

        // Unknown or missing id gives the starter car.
        public static Car Get(string id)
        {
            foreach (var car in All)
                if (car.Id == id) return car;
            return All[0];
        }
    }
}
