using UnityEngine;

namespace PolicePursuit.Gameplay
{
    // Open-world mode: a small walled district with the player's car, a police chaser, an armed suspect and a station.
    // Free roam when Mission is null; otherwise the mission decides what each outcome means.
    // shortcut: placeholder box city in a 4x4 grid; replace with the city pack and a real road network.
    public class OpenWorld : MonoBehaviour
    {
        public const int Blocks = 4;
        public const float Pitch = 54, Road = 14; // block spacing and road width, metres
        public const float Edge = Blocks / 2f * Pitch + 10; // walls sit this far from the centre
        public static readonly Vector3 Spawn = new Vector3(0, 0.6f, 0); // centre crossroads

        public static readonly Vector3 PoliceSpawn = new Vector3(-2 * Pitch, 0.6f, -2 * Pitch); // south-west corner
        public const float BustRange = 7, BustSpeed = 3, BustTime = 2; // metres, m/s, seconds

        public VehicleController Car { get; private set; }
        public PoliceChase Police { get; private set; }
        public Walker Walker { get; private set; }
        public Health PlayerHealth { get; private set; }
        public WeaponController Gun { get; private set; }
        public MeleeController Melee { get; private set; }
        public Suspect Suspect { get; private set; }
        public static readonly Vector3 SuspectPost = new Vector3(Pitch, 0, Pitch / 2); // on the road east of the start
        public int Downs { get; private set; } // times the player was incapacitated

        // Arrest loop: cuff a surrendered or incapacitated suspect, walk them to the car, drive to the station.
        public static readonly Vector3 Station = new Vector3(2 * Pitch, 0, 2 * Pitch); // north-east crossroads
        public const float ArrestRange = 2, CuffTime = 2, LoadRange = 6, StationRange = 10;
        public bool ArrestHeld { get; set; } // set each frame by the ARREST button or a test
        public float ArrestProgress => cuffTime / CuffTime;
        public bool CanArrest => OnFoot && Suspect.CanBeCuffed && (Mission == null || Mission.Side == Mission.Role.Police)
            && Vector3.Distance(Walker.transform.position, Suspect.transform.position) < ArrestRange;
        public int Deliveries { get; private set; }
        float cuffTime;

        public Mission Mission { get; private set; }
        public event System.Action<Mission> MissionEnded;
        public FleeDriver Fleer { get; private set; }
        public CivilTraffic Traffic { get; private set; }
        public Pedestrians People { get; private set; }
        public TrafficLights Lights { get; private set; }
        public RoadBlock Block { get; private set; }

        // Wanted level while the police are after you: 1 star, one more every 30 s (the endless run's rule), up to 5.
        // Out of sight and over 80 m from every police car (and out of the searchlight) for 10 s: back to 1.
        public const float LoseRange = 80, LoseAfter = 10;
        public bool Chased => Police.gameObject.activeSelf && Police.Target != null;
        public float ChaseTime { get; set; } // settable so tests (and later, missions) can start a chase hot
        public int Wanted => Chased ? Core.WantedLevel.ForDuration(ChaseTime) : 0;
        public Helicopter Heli { get; private set; }
        public PoliceDog Dog { get; private set; }
        public PrintTrail Footprints { get; private set; } // yours, for a few seconds after wading
        bool walkerWasWet;
        public const float DogRest = 30; // after a dog loses your scent, none comes out again for this long
        float noDogUntil;

        // A park with a pond in place of one building, south-east of the centre. Wading is slow; dogs lose the scent.
        public static readonly Vector2Int ParkBlock = new Vector2Int(2, 1);
        public const float PondHalf = 14;
        public static Vector3 ParkCentre => new Vector3((ParkBlock.x - Blocks / 2f + 0.5f) * Pitch, 0, (ParkBlock.y - Blocks / 2f + 0.5f) * Pitch);
        // Low fences on the park's north and west sides: you can jump them, a police dog cannot.
        public const float FenceHeight = 0.7f, FenceInset = 0.5f;
        public static float FenceHalf => (Pitch - Road) / 2 - FenceInset; // from the park centre
        public static bool InWater(Vector3 p) => Mathf.Abs(p.x - ParkCentre.x) < PondHalf && Mathf.Abs(p.z - ParkCentre.z) < PondHalf;

        // One covered road: from the west-centre crossroads north to the next one. The helicopter cannot see into it.
        public static readonly Vector2Int TunnelFrom = new Vector2Int(1, 2), TunnelTo = new Vector2Int(1, 3);
        public const float TunnelRoof = 5.5f;
        public static bool InTunnel(Vector3 p)
        {
            var a = PoliceChase.Centre(TunnelFrom);
            var b = PoliceChase.Centre(TunnelTo);
            float half = Road / 2;
            return p.x > a.x - half && p.x < a.x + half && p.z > a.z + half && p.z < b.z - half;
        }
        float loseTime;
        public bool CarLocked { get; private set; } // broken down: cannot be driven again this attempt
        GameObject garagePad, safehousePad;
        bool reported;

        public string Objective => Mission != null ? Mission.Objective(this) : ArrestObjective;
        public string ArrestObjective =>
            Suspect.Now == Suspect.State.Armed ? "Stop the armed suspect"
            : Suspect.CanBeCuffed ? "Arrest the suspect"
            : Suspect.Now == Suspect.State.Cuffed ? "Bring the suspect to your car"
            : Suspect.Now == Suspect.State.InCar ? "Drive to the police station"
            : "";
        public bool OnFoot => Walker.gameObject.activeSelf;
        public Transform Player => OnFoot ? Walker.transform : Car.transform;
        float PlayerSpeed => OnFoot ? Walker.Speed : Mathf.Abs(Car.Speed);
        public const float ExitSpeed = 3, EnterRange = 4; // m/s, metres
        public bool CanExit => !OnFoot && Mathf.Abs(Car.Speed) < ExitSpeed;
        public bool CanEnter => OnFoot && !CarLocked && Vector3.Distance(Walker.transform.position, Car.transform.position) < EnterRange;
        public float LookYaw { get; set; } // camera turn on foot, degrees
        public float PoliceDistance => Mathf.Min(Vector3.Distance(Player.position, Police.transform.position), Block.ChaserDistance(Player.position));
        public float BustProgress => bustTime / BustTime; // 0..1 while the police are pulling you over
        public int Busts { get; private set; }
        float bustTime;
        Camera cam;

        void Awake()
        {
            var ground = Solid(new Vector3(0, -0.5f, 0), new Vector3(Edge * 2, 1, Edge * 2), Shapes.Hex(0x1A1D24));
            ground.name = "Ground";
            for (int i = 0; i <= Blocks; i++)
            {
                float at = (i - Blocks / 2f) * Pitch;
                Shapes.Box(transform, new Vector3(at, 0.01f, 0), new Vector3(Road, 0.02f, Edge * 2), Shapes.Hex(0x2A2E38));
                Shapes.Box(transform, new Vector3(0, 0.01f, at), new Vector3(Edge * 2, 0.02f, Road), Shapes.Hex(0x2A2E38));
            }

            var random = new System.Random(7); // same city every time
            for (int x = 0; x < Blocks; x++)
            for (int z = 0; z < Blocks; z++)
            {
                float h = 10 + (float)random.NextDouble() * 30;
                var centre = new Vector3((x - Blocks / 2f + 0.5f) * Pitch, h / 2, (z - Blocks / 2f + 0.5f) * Pitch);
                float pavement = Pitch - Road + Pedestrian.Pavement * 2;
                Shapes.Box(transform, new Vector3(centre.x, 0.02f, centre.z), new Vector3(pavement, 0.04f, pavement), Shapes.Hex(0x3A3F4A));
                if (new Vector2Int(x, z) == ParkBlock)
                {
                    Shapes.Box(transform, new Vector3(centre.x, 0.03f, centre.z), new Vector3(Pitch - Road, 0.04f, Pitch - Road), Shapes.Hex(0x1F3A24));
                    Shapes.Box(transform, new Vector3(centre.x, 0.05f, centre.z), new Vector3(PondHalf * 2, 0.04f, PondHalf * 2), Shapes.Hex(0x1C4E80), glow: 0.3f).name = "Pond";
                    float f = FenceHalf, len = f * 2;
                    Solid(new Vector3(centre.x, FenceHeight / 2, centre.z + f), new Vector3(len, FenceHeight, 0.15f), Shapes.Hex(0x8A8F99)).name = "Fence north";
                    Solid(new Vector3(centre.x - f, FenceHeight / 2, centre.z), new Vector3(0.15f, FenceHeight, len), Shapes.Hex(0x8A8F99)).name = "Fence west";
                    continue;
                }
                var b = Solid(centre, new Vector3(Pitch - Road, h, Pitch - Road), Shapes.Navy);
                Shapes.LightWindows(b.GetComponent<Renderer>());
            }

            for (int side = 0; side < 4; side++)
            {
                var dir = Quaternion.Euler(0, side * 90, 0) * Vector3.forward;
                var size = side % 2 == 0 ? new Vector3(Edge * 2, 3, 1) : new Vector3(1, 3, Edge * 2);
                Solid(dir * Edge + Vector3.up * 1.5f, size, Shapes.Magenta).name = "Wall";
            }

            Car = VehicleController.Create(transform, Spawn, Shapes.Cyan);
            Police = VehicleController.Create(transform, PoliceSpawn, Color.white, police: true).gameObject.AddComponent<PoliceChase>();
            Walker = Walker.Create(transform);
            PlayerHealth = Walker.gameObject.AddComponent<Health>();
            Gun = Walker.gameObject.AddComponent<WeaponController>();
            Gun.Init(WeaponData.PlayerPistol());
            Melee = Walker.gameObject.AddComponent<MeleeController>();
            PlayerHealth.Downed += () =>
            {
                Downs++;
                if (Mission != null) Mission.End(Mission.Outcome.Incapacitated);
                else ResetCar(); // incapacitated: start over, no injury shown
            };
            Walker.gameObject.SetActive(false);
            Police.Target = Car.transform;
            Suspect = Suspect.Create(transform, SuspectPost);
            Suspect.TargetMelee = Melee;
            var pad = Shapes.Box(transform, Station + Vector3.up * 0.03f, new Vector3(12, 0.04f, 12), Color.blue, glow: 1.5f);
            pad.name = "Station";
            garagePad = Shapes.Box(transform, Mission.Garage + Vector3.up * 0.03f, new Vector3(12, 0.04f, 12), Color.green, glow: 1.5f);
            safehousePad = Shapes.Box(transform, Mission.Safehouse + Vector3.up * 0.03f, new Vector3(10, 0.04f, 10), Color.green, glow: 1.5f);
            Traffic = new GameObject("Traffic").AddComponent<CivilTraffic>();
            Traffic.transform.SetParent(transform, false);
            Traffic.Player = Car.transform;
            Traffic.PoliceCar = Police.GetComponent<VehicleController>();
            // Half the endless run's density: the district is small.
            Traffic.Init(Core.GraphicsPresets.MaxCars(Core.GraphicsPresets.Load()) / 2);
            Heli = Helicopter.Create(transform);
            Dog = PoliceDog.Create(transform);
            Footprints = new PrintTrail(transform, 0.8f, new Vector3(0.16f, 0.01f, 0.28f));
            Dog.Barked += () => Core.GameManager.Instance?.Sound?.Bark();
            var tunnelMid = (PoliceChase.Centre(TunnelFrom) + PoliceChase.Centre(TunnelTo)) / 2;
            Solid(tunnelMid + Vector3.up * TunnelRoof, new Vector3(Road + 1, 1, Pitch - Road), Shapes.Hex(0x2A2E38)).name = "Tunnel roof";
            Block = new GameObject("Roadblock").AddComponent<RoadBlock>();
            Block.transform.SetParent(transform, false);
            Block.Init();
            Traffic.MoreSirens = Block.Cars; // only count while they are driving: a parked roadblock is silent
            Lights = new GameObject("Traffic lights").AddComponent<TrafficLights>();
            Lights.transform.SetParent(transform, false);
            Lights.Build();
            People = new GameObject("Pedestrians").AddComponent<Pedestrians>();
            People.transform.SetParent(transform, false);
            People.Init(Core.GraphicsPresets.MaxCars(Core.GraphicsPresets.Load()));
            People.PoliceCar = Police.transform;
            People.PoliceVehicle = Police.GetComponent<VehicleController>();
            People.PlayerCar = Car;
            Fleer = VehicleController.Create(transform, Mission.FleeSpawn, Shapes.Hex(0xFF8A00)).gameObject.AddComponent<FleeDriver>();
            Begin(null);
        }

        // Starts free roam (null) or a fresh attempt at the mission as `side`.
        public void Begin(Mission.Role? side)
        {
            Mission = side == null ? null : new Mission(side.Value);
            reported = false;
            ResetCar();
            bool police = side == Mission.Role.Police, prisoner = side == Mission.Role.Prisoner;
            SirenOn = false;
            if (police) Car.AddLightBar(); // stays fitted afterwards, dark unless the siren is on
            Police.gameObject.SetActive(!police);
            Fleer.gameObject.SetActive(police);
            Fleer.enabled = true;
            Fleer.GetComponent<VehicleController>().Handbrake = false;
            Fleer.GetComponent<VehicleController>().Place(transform.TransformPoint(Mission.FleeSpawn), 0);
            Fleer.StartAt(PoliceChase.Node(Spawn), PoliceChase.Node(Spawn) + Vector2Int.up);
            garagePad.SetActive(prisoner);
            safehousePad.SetActive(prisoner);
            Suspect.Colour = prisoner ? Color.blue : Shapes.Hex(0xFF8A00);
            PlaceSuspect(prisoner ? Mission.GuardPost : SuspectPost);
            Suspect.gameObject.SetActive(!police); // the escapee appears when they bail out
        }

        public void Restart() => Begin(Mission?.Side);

        // The player's own siren, only when playing the police side: lights flash and traffic makes way.
        public bool PlayerIsPolice => Mission != null && Mission.Side == Mission.Role.Police;
        public bool SirenOn { get; private set; }

        public bool ToggleSiren()
        {
            if (!PlayerIsPolice) return false;
            SirenOn = !SirenOn;
            return true;
        }

        public const float HornRange = 15, HornEvery = 0.6f;
        float nextHorn;

        // Only from the car. True when it sounded (not during the short gap between honks).
        public bool Honk()
        {
            if (OnFoot || Time.time < nextHorn) return false;
            nextHorn = Time.time + HornEvery;
            People.HurryNear(Car.transform.position, HornRange);
            return true;
        }

        public void PlaceSuspect(Vector3 feet)
        {
            Suspect.SetPost(feet);
            Suspect.ResetSuspect();
        }

        // Scripted: the car stops dead and the player has to go on on foot.
        public void BreakDown()
        {
            Car.Place(Car.transform.position, Car.transform.eulerAngles.y);
            ExitCar();
            CarLocked = true;
        }

        // Fires the player's weapon the way the player faces (or the camera looks, when standing still).
        public bool Shoot()
        {
            if (!OnFoot || Melee.Busy || Melee.Blocking) return false;
            var aim = Walker.Move.sqrMagnitude > 0.01f ? Walker.transform.forward : Quaternion.Euler(0, LookYaw, 0) * Vector3.forward;
            return Gun.Fire(aim);
        }

        // Out of the driver's side if there is room, else the other side, else behind. False when boxed in or moving.
        public bool ExitCar()
        {
            if (!CanExit) return false;
            var t = Car.transform;
            foreach (var offset in new[] { -t.right * 2f, t.right * 2f, -t.forward * 3.2f })
            {
                var feet = new Vector3(t.position.x, 0.05f, t.position.z) + Vector3.ProjectOnPlane(offset, Vector3.up);
                var low = feet + Vector3.up * (Walker.Radius + 0.05f);
                var high = feet + Vector3.up * (Walker.Height - Walker.Radius);
                if (Physics.CheckCapsule(low, high, Walker.Radius)) continue;
                Car.Pedal = Car.Steer = 0;
                Car.Handbrake = true; // parked
                Walker.gameObject.SetActive(true);
                Walker.Place(feet, t.eulerAngles.y);
                LookYaw = t.eulerAngles.y;
                Police.Target = Walker.transform;
                return true;
            }
            return false;
        }

        public bool EnterCar()
        {
            if (!CanEnter) return false;
            Walker.gameObject.SetActive(false);
            Car.Handbrake = false;
            Police.Target = Car.transform;
            cuffTime = 0;
            if (Suspect.Now == Suspect.State.Cuffed
                && Vector3.Distance(Suspect.transform.position, Car.transform.position) < LoadRange)
                Suspect.PutInCar();
            return true;
        }

        GameObject Solid(Vector3 pos, Vector3 size, Color colour)
        {
            var go = Shapes.Box(transform, pos, size, colour);
            go.AddComponent<BoxCollider>(); // Shapes strips colliders; the open world needs them
            return go;
        }

        public void ResetCar()
        {
            Walker.gameObject.SetActive(false);
            CarLocked = false;
            PlayerHealth.Restore();
            Melee.ResetMelee();
            Gun.Restock();
            Suspect.ResetSuspect();
            Police.Target = Car.transform;
            Car.Handbrake = false;
            Car.Place(transform.TransformPoint(Spawn), 0);
            Police.GetComponent<VehicleController>().Place(transform.TransformPoint(PoliceSpawn), 0);
            Police.ResetChase();
            Block.ResetBlock();
            ChaseTime = loseTime = 0;
            Heli.Leave();
            Dog.Recall();
            Footprints.Clear();
            walkerWasWet = false;
            noDogUntil = 0;
            Traffic.ResetTraffic();
            bustTime = cuffTime = 0;
        }

        // A dog comes out of the chasing police car when you are on foot close by; it holding you is a bust.
        bool TickDog()
        {
            bool chasingOnFoot = OnFoot && Chased;
            if (!chasingOnFoot) { if (Dog.gameObject.activeSelf) Dog.Recall(); return false; }
            var car = Police.transform;
            if (Dog.GaveUp) { Dog.Recall(); noDogUntil = Time.time + DogRest; return false; }
            if (!Dog.gameObject.activeSelf && Time.time >= noDogUntil
                && Vector3.Distance(car.position, Walker.transform.position) < PoliceDog.ReleaseRange)
                Dog.Release(new Vector3(car.position.x, 0.1f, car.position.z) - car.right * 2, Walker.transform, Melee, Footprints);
            if (!Dog.Holding) return false;
            Busts++;
            if (Mission != null) Mission.End(Mission.Outcome.Busted);
            else ResetCar();
            return true;
        }

        // Out of every way the police have of knowing where you are.
        public static bool Hidden(float policeDistance, bool policeSee, bool inSearchlight, bool dogBarking) =>
            policeDistance > LoseRange && !policeSee && !inSearchlight && !dogBarking;

        void TickWanted(float dt)
        {
            if (!Chased) { ChaseTime = loseTime = 0; Heli.Leave(); return; }
            ChaseTime += dt;
            bool hidden = Hidden(PoliceDistance, Police.InSight, Heli.InBeam(Player.position), Dog.Barking);
            loseTime = hidden ? loseTime + dt : 0;
            if (loseTime > LoseAfter) { ChaseTime = loseTime = 0; } // lost them, for now
            if (Wanted >= Helicopter.FromWanted)
            {
                if (!Heli.gameObject.activeSelf) Heli.Arrive(Player.position);
                var velocity = OnFoot ? Walker.transform.forward * Walker.Speed : Car.Body.linearVelocity;
                Heli.Tick(Player.position, velocity, dt, !InTunnel(Player.position));
            }
            else if (Heli.gameObject.activeSelf) Heli.Leave();
        }

        // Runs after the UI and tests have set ArrestHeld for this frame.
        void TickArrest(float dt)
        {
            cuffTime = ArrestHeld && CanArrest ? cuffTime + dt : 0;
            if (cuffTime >= CuffTime)
            {
                cuffTime = 0;
                Suspect.Cuff(Walker.transform);
            }
            if (!OnFoot && Suspect.Now == Suspect.State.InCar && Mathf.Abs(Car.Speed) < ExitSpeed
                && Vector3.Distance(Car.transform.position, transform.TransformPoint(Station)) < StationRange)
            {
                Suspect.Deliver();
                Deliveries++;
                if (Mission != null) Mission.End(Mission.Outcome.Captured);
                else ResetCar();
            }
        }

        // Caught when the police stay close while you are (nearly) stopped; both cars then start over.
        void Update()
        {
            Suspect.Target = OnFoot ? Walker.transform : null;
            TickWanted(Time.deltaTime);
            // Roadblocks only while the police are actually after you in your car.
            Block.Tick(Car, Police.gameObject.activeSelf && Police.Target != null && !OnFoot, Time.deltaTime);
            Fleer.From = Player;
            Traffic.Player = Player;
            Car.Flash(SirenOn);
            Traffic.PlayerSiren = SirenOn ? Car : null;
            People.PlayerSiren = SirenOn;
            Fleer.Scared = SirenOn && Vector3.Distance(Car.transform.position, Fleer.transform.position) < FleeDriver.SpookRange;
            if (Mission != null && Mission.Result != Mission.Outcome.None) return;
            if (TickDog()) return;
            bool held = (Police.gameObject.activeSelf || Block.Chasing) && PoliceDistance < BustRange && PlayerSpeed < BustSpeed;
            bustTime = held ? bustTime + Time.deltaTime : 0;
            if (bustTime < BustTime) return;
            Busts++;
            if (Mission != null) Mission.End(Mission.Outcome.Busted);
            else ResetCar();
        }

        void OnEnable()
        {
            cam = Camera.main;
            if (cam == null) return;
            var chase = cam.GetComponent<CarCamera>();
            if (chase != null) chase.enabled = false;
            Snap();
        }

        void OnDisable()
        {
            if (cam == null) return;
            cam.fieldOfView = 60;
            var chase = cam.GetComponent<CarCamera>();
            if (chase != null) { chase.enabled = true; chase.Snap(); }
        }

        Vector3 CamGoal(out Vector3 look)
        {
            Vector3 goal;
            if (OnFoot)
            {
                var p = Walker.transform.position;
                look = p + Vector3.up * 1.5f;
                goal = p - Quaternion.Euler(0, LookYaw, 0) * Vector3.forward * 4.5f + Vector3.up * 2.4f;
            }
            else
            {
                var t = Car.transform;
                var back = Vector3.ProjectOnPlane(t.forward, Vector3.up).normalized;
                float distance = 7 + Car.SpeedKmh * 0.02f; // pulls back a little with speed
                look = t.position + Vector3.up * 1.2f;
                goal = t.position - back * distance + Vector3.up * 3.2f;
            }
            // Keep the camera out of buildings: stop just in front of whatever is between it and the player.
            if (Physics.Linecast(look, goal, out var hit)) goal = hit.point + hit.normal * 0.3f;
            return goal;
        }

        void Snap()
        {
            if (Car == null) return;
            cam.transform.position = CamGoal(out var look);
            cam.transform.LookAt(look);
        }

        void LateUpdate()
        {
            if (Car == null) return;
            TickArrest(Time.deltaTime);
            if (Mission != null)
            {
                Mission.Tick(this, Time.deltaTime);
                if (Mission.Result != Mission.Outcome.None && !reported)
                {
                    reported = true;
                    MissionEnded?.Invoke(Mission);
                }
            }
            if (cam == null) return;
            Footprints.Tick(Walker.transform, Time.deltaTime); // also in the car, so old prints still fade
            if (OnFoot)
            {
                if (Melee.Busy || Melee.Blocking) Walker.Move = Vector2.zero; // planted while striking or guarding
                bool wet = InWater(Walker.transform.position);
                Walker.SpeedScale = wet ? 0.5f : 1;
                if (walkerWasWet && !wet) Footprints.Soak(Walker.transform.position);
                walkerWasWet = wet;
                Walker.Tick(LookYaw, Time.deltaTime);
            }
            var goal = CamGoal(out var look);
            float k = 1 - Mathf.Exp(-6 * Time.deltaTime); // frame-rate independent smoothing
            cam.transform.position = Vector3.Lerp(cam.transform.position, goal, k);
            cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation,
                Quaternion.LookRotation(look - cam.transform.position), k);
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, 60 + Car.SpeedKmh * 0.08f, k); // wider at speed, capped by top speed
        }
    }
}
