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
        public TyreMarks Tyres { get; private set; }
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
        public SpikeStrip Spikes { get; private set; }
        public CrossroadSpikes JunctionSpikes { get; private set; }
        // The helicopter drops its own strip ahead of you while it can see you driving fast.
        public SpikeStrip HeliSpikes { get; private set; }
        // Drive into the tunnel with the police after you and they lay a strip at the far end.
        public SpikeStrip TunnelSpikes { get; private set; }
        // Heading for a canal bridge with the police after you: a strip on its right-hand lane.
        public SpikeStrip BridgeSpikes { get; private set; }
        public const float BridgeSpikesNear = 20, BridgeSpikesFar = 45, BridgeSpikesRest = 30;
        float noBridgeSpikesUntil;
        public const float TunnelSpikesRest = 30, TunnelSpikesAt = 48; // seconds between; metres from the entry crossroads
        float noTunnelSpikesUntil;
        bool wasInTunnel;
        public const float HeliDropFirst = 10, HeliDropEvery = 40, HeliDropSpeed = 8;
        float heliOverheadFor, nextHeliDrop = HeliDropFirst;
        public bool Punctured { get; private set; } // tyres blown on a spike strip: half speed until you start over

        // Wanted level while the police are after you: 1 star, one more every 30 s (the endless run's rule), up to 5.
        // Out of sight and over 80 m from every police car (and out of the searchlight) for 10 s: back to 1.
        public const float LoseRange = 80, LoseAfter = 10;
        public bool Chased => Police.gameObject.activeSelf && Police.Target != null;
        public float ChaseTime { get; set; } // settable so tests (and later, missions) can start a chase hot
        public int Wanted => Chased ? Core.WantedLevel.ForDuration(ChaseTime) : 0;
        public Helicopter Heli { get; private set; }
        public PoliceDog Dog { get; private set; }
        public PrintTrail Footprints { get; private set; } // yours, for a few seconds after wading
        public Weather Sky { get; private set; }
        public Puddles Puddles { get; private set; }
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
        // A canal east to west through the southern row of blocks; the five north-south roads cross it on bridges.
        // Low railings (the park fences' height) line both banks: cars stay out, you can jump in, dogs cannot.
        public const int CanalRow = 0;
        public const float CanalHalf = 5;
        public static float CanalZ => (CanalRow - Blocks / 2f + 0.5f) * Pitch;
        public static bool OnBridge(Vector3 p) => Mathf.Abs(p.x - Mathf.Round(p.x / Pitch) * Pitch) < Road / 2;
        public static bool InCanal(Vector3 p) => Mathf.Abs(p.z - CanalZ) < CanalHalf && !OnBridge(p) && Mathf.Abs(p.x) < Blocks / 2f * Pitch;
        public static bool InWater(Vector3 p) =>
            (Mathf.Abs(p.x - ParkCentre.x) < PondHalf && Mathf.Abs(p.z - ParkCentre.z) < PondHalf) || InCanal(p);

        // One covered road: from the west-centre crossroads north to the next one. The helicopter cannot see into it.
        public static readonly Vector2Int TunnelFrom = new Vector2Int(1, 2), TunnelTo = new Vector2Int(1, 3);
        public const float TunnelRoof = 5.5f;
        // Hiding in traffic: crawling along with a civilian car close in front and another close behind, in your
        // lane, for a few seconds, and the helicopter loses you among them.
        public const float TrafficCrawl = 8, TrafficGap = 12, TrafficLane = 2.5f, TrafficHideAfter = 3;
        public bool HiddenInTraffic => trafficTime >= TrafficHideAfter;
        float trafficTime;

        public static bool Sandwiched(Vector3 pos, Vector3 forward, System.Collections.Generic.IEnumerable<Vector3> cars)
        {
            forward.y = 0;
            forward.Normalize();
            bool ahead = false, behind = false;
            foreach (var c in cars)
            {
                var d = c - pos;
                d.y = 0;
                float along = Vector3.Dot(d, forward);
                float side = (d - forward * along).magnitude;
                if (side > TrafficLane) continue;
                if (along > 2 && along < TrafficGap) ahead = true;
                if (along < -2 && along > -TrafficGap) behind = true;
            }
            return ahead && behind;
        }

        readonly System.Collections.Generic.List<Vector3> nearbyCars = new System.Collections.Generic.List<Vector3>();
        bool InTraffic()
        {
            if (OnFoot || Mathf.Abs(Car.Speed) >= TrafficCrawl) return false;
            nearbyCars.Clear();
            foreach (var c in Traffic.Cars)
                if (c.gameObject.activeSelf && !c.Crashed) nearbyCars.Add(c.transform.position);
            return Sandwiched(Car.transform.position, Car.transform.forward, nearbyCars);
        }

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
        // The one record of what the player is doing (milestone 3); OnFoot and the car checks below read it.
        public PlayerStateMachine States { get; } = new PlayerStateMachine();
        public const float ShootingFor = 1; // seconds after a shot that count as RangedCombat
        float lastShotAt = -10;
        public bool OnFoot => States.OnFoot;
        public Transform Player => OnFoot ? Walker.transform : Car.transform;
        float PlayerSpeed => OnFoot ? Walker.Speed : Mathf.Abs(Car.Speed);
        public const float ExitSpeed = 3, EnterRange = 4; // m/s, metres
        public bool CanExit => States.InCar && Mathf.Abs(Car.Speed) < ExitSpeed;
        // Not mid-strike: a punch or kick has to finish before you can get in.
        public bool CanEnter => OnFoot && States.State != PlayerState.MeleeCombat && !CarLocked
            && Vector3.Distance(Walker.transform.position, Car.transform.position) < EnterRange;
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
                if (z == CanalRow)
                {
                    // Two buildings with the canal between them, railings along both banks.
                    float side = Pitch - Road, half = side / 2, top = centre.z + half, bottom = centre.z - half;
                    float north = top - (CanalZ + CanalHalf), south = (CanalZ - CanalHalf) - bottom;
                    var nb = Solid(new Vector3(centre.x, h / 2, top - north / 2), new Vector3(side, h, north), Shapes.Navy);
                    var sb = Solid(new Vector3(centre.x, h * 0.4f, bottom + south / 2), new Vector3(side, h * 0.8f, south), Shapes.Navy);
                    Shapes.LightWindows(nb.GetComponent<Renderer>());
                    Shapes.LightWindows(sb.GetComponent<Renderer>());
                    Shapes.Box(transform, new Vector3(centre.x, 0.05f, CanalZ), new Vector3(side, 0.04f, CanalHalf * 2), Shapes.Hex(0x1C4E80), glow: 0.3f).name = "Canal";
                    foreach (float bank in new[] { CanalZ + CanalHalf, CanalZ - CanalHalf })
                        Solid(new Vector3(centre.x, FenceHeight / 2, bank), new Vector3(side, FenceHeight, 0.15f), Shapes.Hex(0x8A8F99)).name = "Canal railing";
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
            ParkPoliceCars();

            Car = VehicleController.Create(transform, Spawn, Shapes.Cyan);
            Car.TakesDamage = true;
            Tyres = Car.gameObject.AddComponent<TyreMarks>();
            Police =VehicleController.Create(transform, PoliceSpawn, Color.white, police: true).gameObject.AddComponent<PoliceChase>();
            Walker = Walker.Create(transform);
            PlayerHealth = Walker.gameObject.AddComponent<Health>();
            Gun = Walker.gameObject.AddComponent<WeaponController>();
            Gun.Init(WeaponData.PlayerPistol());
            Melee = Walker.gameObject.AddComponent<MeleeController>();
            PlayerHealth.Downed += () =>
            {
                Downs++;
                States.Go(PlayerState.Incapacitated);
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
            Sky = new GameObject("Weather").AddComponent<Weather>();
            Sky.transform.SetParent(transform, false);
            Sky.Init(Core.GraphicsPresets.Load() == Core.GfxPreset.Low ? 200 : 600);
            Puddles = new GameObject("Puddles").AddComponent<Puddles>();
            Puddles.transform.SetParent(transform, false);
            Puddles.Build();
            Dog.Barked += () => Core.GameManager.Instance?.Sound?.Bark();
            var tunnelMid = (PoliceChase.Centre(TunnelFrom) + PoliceChase.Centre(TunnelTo)) / 2;
            Solid(tunnelMid + Vector3.up * TunnelRoof, new Vector3(Road + 1, 1, Pitch - Road), Shapes.Hex(0x2A2E38)).name = "Tunnel roof";
            Block = new GameObject("Roadblock").AddComponent<RoadBlock>();
            Block.transform.SetParent(transform, false);
            Block.Init();
            Spikes = new GameObject("Spikes").AddComponent<SpikeStrip>();
            Spikes.transform.SetParent(transform, false);
            Spikes.Init();
            JunctionSpikes = new GameObject("Crossroads spikes").AddComponent<CrossroadSpikes>();
            JunctionSpikes.transform.SetParent(transform, false);
            JunctionSpikes.Init();
            HeliSpikes = new GameObject("Helicopter spikes").AddComponent<SpikeStrip>();
            HeliSpikes.transform.SetParent(transform, false);
            HeliSpikes.Init();
            TunnelSpikes = new GameObject("Tunnel spikes").AddComponent<SpikeStrip>();
            TunnelSpikes.transform.SetParent(transform, false);
            TunnelSpikes.Init();
            BridgeSpikes = new GameObject("Bridge spikes").AddComponent<SpikeStrip>();
            BridgeSpikes.transform.SetParent(transform, false);
            BridgeSpikes.Init();
            Block.Spawned += () => Spikes.Spawn(Block.From, Block.To, 0.5f - RoadBlock.SpikesBefore / Pitch);
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
            // Every light bar the puddles can pick up: the chaser, the roadblock cars, your own on the police side.
            Puddles.Lights.Add(Police.GetComponent<VehicleController>());
            Puddles.Lights.AddRange(Block.Cars);
            Puddles.Lights.Add(Car);
            Puddles.Drivers.AddRange(Puddles.Lights); // the same cars, plus civilian traffic
            Puddles.Drivers.Add(Fleer.GetComponent<VehicleController>());
            Puddles.Traffic = Traffic;
            Puddles.PlayerCar = Car;
            Puddles.PlayerSplashed += () => Core.GameManager.Instance?.Sound?.Splash();
            Begin(null);
        }

        // Starts free roam (null) or a fresh attempt at the mission as `side`.
        public void Begin(Mission.Role? side)
        {
            Mission = side == null ? null : new Mission(side.Value);
            reported = false;
            ResetCar();
            Sky.ResetWeather(); // a fresh visit starts dry; a bust does not change the weather
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
            if (!Gun.Fire(aim)) return false;
            lastShotAt = Time.time;
            return true;
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
                States.Go(PlayerState.ExitingVehicle);
                States.Go(PlayerState.OnFoot);
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
            States.Go(PlayerState.OnFoot); // out of a shot or an arrest first
            States.Go(PlayerState.EnteringVehicle);
            States.Go(PlayerState.Driving);
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

        // Parked police cars (a ComfyUI/TripoSR model) in the strip between the outer roads and the wall, 115 to
        // 117.5 m out: off every road, so traffic, roadblock gaps and spike strips never meet them. Mid-block only,
        // clear of the roads that run into the wall; every other block, alternating round the district.
        public const string ParkedCarName = "Parked police car";
        public static float ParkedStrip => (Blocks / 2f * Pitch + Road / 2 + Edge - 0.5f) / 2;

        void ParkPoliceCars()
        {
            for (int side = 0; side < 4; side++)
            for (int i = 0; i < Blocks; i++)
            {
                var outward = Quaternion.Euler(0, side * 90, 0) * Vector3.forward;
                var along = Quaternion.Euler(0, 90, 0) * outward;
                var pos = outward * ParkedStrip + along * ((i - Blocks / 2f + 0.5f) * Pitch);
                var facing = i < Blocks / 2 ? along : -along;
                // Parked police cars on every other block; a dumpster or a road barrier in each spot left between them.
                bool parked = i % 2 == side % 2;
                Place(parked ? "ParkedPoliceCar/police-car" : i % 4 < 2 ? "Props/dumpster/dumpster" : "Props/barrier/barrier",
                    parked ? ParkedCarName : PropName, pos, facing);
            }
            PlaceStreetProps();
        }

        // ComfyUI/TripoSR props (tools/comfy_3d.py, game_mesh.py), each kept off every road, crossing and walkway:
        // benches in the park between the pond and its fence, and fire hydrants against building walls on the
        // northern east-west road, 10 m either side of mid-block (roadblocks stand at mid-block; spike strips reach
        // 6 m from the road centre, the hydrants sit 6.7 m out; walkers pass 1 m from the wall, the hydrants 0.3 m).
        public const string PropName = "Street prop";
        public const float BenchFromPond = 16.75f, HydrantFromWall = 0.3f, HydrantAlong = 10;

        void PlaceStreetProps()
        {
            var park = ParkCentre;
            foreach (float x in new[] { -6f, 6f })
                Place("Props/bench/bench", PropName, park + new Vector3(x, 0, -BenchFromPond), Vector3.right); // south side
            foreach (float z in new[] { -6f, 6f })
                Place("Props/bench/bench", PropName, park + new Vector3(BenchFromPond, 0, z), Vector3.forward); // east side
            float half = (Pitch - Road) / 2; // building edge from its block centre
            for (int x = 0; x < Blocks; x++)
            {
                float blockX = (x - Blocks / 2f + 0.5f) * Pitch, blockZ = (Blocks - 1 - Blocks / 2f + 0.5f) * Pitch;
                var at = new Vector3(blockX + (x % 2 == 0 ? -HydrantAlong : HydrantAlong), 0, blockZ - half - HydrantFromWall);
                Place("Props/hydrant/hydrant", PropName, at, Vector3.forward);
            }
        }

        // Each model is a LOD group (milestone 4): the full model up close, the "-lod1" copy (about a quarter of the
        // triangles, tools/game_mesh.py) further off, and nothing once it is tiny on screen. Shares of screen height.
        public const float LodNear = 0.06f, LodCull = 0.012f;

        void Place(string path, string name, Vector3 pos, Vector3 facing)
        {
            var model = Resources.Load<GameObject>(path);
            if (model == null) return;
            var go = Instantiate(model, transform);
            go.name = name;
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.LookRotation(facing);
            go.GetComponentInChildren<MeshFilter>().gameObject.AddComponent<BoxCollider>(); // fits the full mesh
            var near = go.GetComponentsInChildren<Renderer>();
            var lowModel = Resources.Load<GameObject>(path + "-lod1");
            LOD[] lods;
            if (lowModel != null)
            {
                var low = Instantiate(lowModel, go.transform, false);
                low.name = "LOD1";
                lods = new[] { new LOD(LodNear, near), new LOD(LodCull, low.GetComponentsInChildren<Renderer>()) };
            }
            else lods = new[] { new LOD(LodCull, near) };
            var group = go.AddComponent<LODGroup>();
            group.SetLODs(lods);
            group.RecalculateBounds();
        }

        public void ResetCar()
        {
            States.Reset(PlayerState.Driving);
            Walker.gameObject.SetActive(false);
            CarLocked = false;
            PlayerHealth.Restore();
            Melee.ResetMelee();
            Gun.Restock();
            Suspect.ResetSuspect();
            Police.Target = Car.transform;
            Car.Handbrake = false;
            Car.Repair();
            Tyres.Clear();
            Car.Place(transform.TransformPoint(Spawn), 0);
            Police.GetComponent<VehicleController>().Place(transform.TransformPoint(PoliceSpawn), 0);
            Police.ResetChase();
            Block.ResetBlock();
            Spikes.ResetStrip();
            JunctionSpikes.ResetSpikes();
            HeliSpikes.Clear();
            TunnelSpikes.Clear();
            noTunnelSpikesUntil = 0;
            BridgeSpikes.Clear();
            noBridgeSpikesUntil = 0;
            wasInTunnel = false;
            heliOverheadFor = 0;
            nextHeliDrop = HeliDropFirst;
            Punctured = false;
            Car.SpeedLimit = VehicleController.TopSpeed;
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
            if (Mission != null) { States.Go(PlayerState.Captured); Mission.End(Mission.Outcome.Busted); }
            else ResetCar();
            return true;
        }

        // Out of every way the police have of knowing where you are.
        public static bool Hidden(float policeDistance, bool policeSee, bool inSearchlight, bool dogBarking) =>
            policeDistance > LoseRange && !policeSee && !inSearchlight && !dogBarking;

        void TickBridgeSpikes(bool chased)
        {
            var p = Car.transform.position;
            if (BridgeSpikes.Active && (!chased || Vector3.Dot(p - BridgeSpikes.Centre, BridgeSpikes.Along) > SpikeStrip.ClearBehind))
                BridgeSpikes.Clear();
            if (BridgeSpikes.Active || !chased || Time.time < noBridgeSpikesUntil || !OnBridge(p)) return;
            float toCanal = CanalZ - p.z;
            if (Mathf.Abs(toCanal) < BridgeSpikesNear || Mathf.Abs(toCanal) > BridgeSpikesFar) return;
            if (Car.transform.forward.z * toCanal <= 0) return; // driving away from it
            SpikeBridge(Mathf.RoundToInt(p.x / Pitch + Blocks / 2f), toCanal < 0);
        }

        // The bridge on north-south road `line`, approached from the north (true) or the south.
        public void SpikeBridge(int line, bool fromNorth)
        {
            var north = new Vector2Int(line, CanalRow + 1);
            var south = new Vector2Int(line, CanalRow);
            BridgeSpikes.Spawn(fromNorth ? north : south, fromNorth ? south : north, 0.5f);
            noBridgeSpikesUntil = Time.time + BridgeSpikesRest;
        }

        void TickTunnelSpikes(bool chased)
        {
            if (TunnelSpikes.Active && (!chased || Vector3.Dot(Car.transform.position - TunnelSpikes.Centre, TunnelSpikes.Along) > SpikeStrip.ClearBehind))
                TunnelSpikes.Clear();
            bool inside = !OnFoot && InTunnel(Car.transform.position);
            if (inside && !wasInTunnel && chased && Time.time >= noTunnelSpikesUntil) SpikeTunnelExit(Car.transform.forward);
            wasInTunnel = inside;
        }

        // The far end for a car heading `heading` through the tunnel; right-hand lane, as every strip.
        public void SpikeTunnelExit(Vector3 heading)
        {
            var dir = PoliceChase.Centre(TunnelTo) - PoliceChase.Centre(TunnelFrom);
            bool north = Vector3.Dot(heading, dir) >= 0;
            var from = north ? TunnelFrom : TunnelTo;
            var to = north ? TunnelTo : TunnelFrom;
            TunnelSpikes.Spawn(from, to, TunnelSpikesAt / Pitch);
            noTunnelSpikesUntil = Time.time + TunnelSpikesRest;
        }

        void TickHeliDrop(float dt)
        {
            if (!Heli.gameObject.activeSelf || !Heli.Overhead)
            {
                heliOverheadFor = 0;
                nextHeliDrop = HeliDropFirst;
                HeliSpikes.Clear(); // its strip goes when it does: chase over, or you shook it off
                return;
            }
            heliOverheadFor += dt;
            if (HeliSpikes.Active && !HeliSpikes.Falling
                && Vector3.Dot(Car.transform.position - HeliSpikes.Centre, HeliSpikes.Along) > SpikeStrip.ClearBehind) HeliSpikes.Clear();
            if (heliOverheadFor < nextHeliDrop || !Heli.CanSee || OnFoot || Mathf.Abs(Car.Speed) < HeliDropSpeed) return;
            nextHeliDrop = heliOverheadFor + HeliDropEvery;
            DropFromHelicopter();
        }

        public bool DropFromHelicopter()
        {
            if (!RoadBlock.Plan(Car.transform.position, Car.Body.linearVelocity, out var a, out var b)) return false;
            HeliSpikes.Spawn(a, b);
            HeliSpikes.Drop(Helicopter.Height);
            return true;
        }

        void TickWanted(float dt)
        {
            trafficTime = InTraffic() ? trafficTime + dt : 0;
            if (!Chased) { ChaseTime = loseTime = 0; Heli.Leave(); return; }
            ChaseTime += dt;
            bool hidden = Hidden(PoliceDistance, Police.InSight, Heli.InBeam(Player.position), Dog.Barking);
            loseTime = hidden ? loseTime + dt : 0;
            if (loseTime > LoseAfter) { ChaseTime = loseTime = 0; } // lost them, for now
            if (Wanted >= Helicopter.FromWanted)
            {
                if (!Heli.gameObject.activeSelf) Heli.Arrive(Player.position);
                var velocity = OnFoot ? Walker.transform.forward * Walker.Speed : Car.Body.linearVelocity;
                Heli.Tick(Player.position, velocity, dt, !InTunnel(Player.position) && !HiddenInTraffic);
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
            Spikes.Tick(Car, Police.gameObject.activeSelf && Police.Target != null && !OnFoot, Time.deltaTime);
            JunctionSpikes.Tick(Car, Police.gameObject.activeSelf && Police.Target != null && !OnFoot, Time.deltaTime);
            TickHeliDrop(Time.deltaTime);
            TickTunnelSpikes(Police.gameObject.activeSelf && Police.Target != null && !OnFoot);
            TickBridgeSpikes(Police.gameObject.activeSelf && Police.Target != null && !OnFoot);
            if (!Punctured && !OnFoot && (Spikes.Over(Car.transform.position) || JunctionSpikes.Over(Car.transform.position)
                                          || HeliSpikes.Over(Car.transform.position) || TunnelSpikes.Over(Car.transform.position)
                                          || BridgeSpikes.Over(Car.transform.position)))
            {
                Punctured = true;
                Car.SpeedLimit = VehicleController.TopSpeed * SpikeStrip.PuncturedScale;
            }
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
            if (Mission != null) { States.Go(PlayerState.Captured); Mission.End(Mission.Outcome.Busted); }
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

        // Puts the camera straight behind the player, e.g. after a teleport (the autotest's screenshots).
        public void SnapCamera() => Snap();

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
                    States.Go(Mission.Result == Mission.Outcome.Busted ? PlayerState.Captured
                        : Mission.Result == Mission.Outcome.Incapacitated ? PlayerState.Incapacitated : PlayerState.MissionComplete);
                    reported = true;
                    MissionEnded?.Invoke(Mission);
                }
            }
            if (cam == null) return;
            if (Sky.Raining) { Footprints.Clear(); Dog.WashPrints(); } // rain washes every wet print away at once
            Puddles.Show(Sky.Raining);
            Footprints.Tick(Walker.transform, Time.deltaTime); // also in the car, so old prints still fade
            States.SettleOnFoot(Melee.Busy || Melee.Blocking, Gun.Reloading || Time.time - lastShotAt < ShootingFor, cuffTime > 0);
            if (OnFoot)
            {
                if (Melee.Busy || Melee.Blocking) Walker.Move = Vector2.zero; // planted while striking or guarding
                bool wet = InWater(Walker.transform.position);
                Walker.SpeedScale = wet ? 0.5f : 1;
                if (walkerWasWet && !wet && !Sky.Raining) Footprints.Soak(Walker.transform.position);
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
