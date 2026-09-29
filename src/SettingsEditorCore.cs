using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Rage2Toolkit
{
    public class RTPCProp
    {
        public uint Hash;
        public string Name;
        public byte Type;
        public int Offset;
        public int ValueOffset;
        public object Value;

        public string DisplayValue()
        {
            if (Value == null) return "";
            if (Value is float f) return f.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);
            if (Value is int i) return i.ToString();
            if (Value is uint u) return u.ToString();
            if (Value is string s) return s;
            if (Value is float[] arr)
            {
                var parts = new List<string>();
                foreach (var x in arr) parts.Add(x.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture));
                return string.Join(", ", parts);
            }
            if (Value is ulong ul) return ul.ToString();
            return Value.ToString();
        }
    }

    public class RTPCNode
    {
        public uint Hash;
        public string Name;
        public List<RTPCProp> Props = new List<RTPCProp>();
        public List<RTPCNode> Children = new List<RTPCNode>();
    }

    public class RTPCFile
    {
        public string FilePath;
        public uint Version;
        public RTPCNode Root;
    }

    public class RTPCPatch
    {
        public int Offset;
        public byte Type;
        public double Value;
        public string Name;
    }

    public class PropInfo
    {
        public string Name;
        public string Conf;
        public string Desc;
        public string Reason;
    }

    public static class RTPCParser
    {
        public static readonly Dictionary<uint, string> WellKnown = new Dictionary<uint, string>
        {
            { 0xAA7D522A, "root" }, { 0x83647B76, "spawn_system" }, { 0x8EB4F892, "budget" },
            { 0x4C803F66, "props" }, { 0xEFE3D972, "civilians" }, { 0x29D87713, "animals" },
            { 0xEAC7EFFD, "encounters" }, { 0xBC7DCD86, "vehicles" }, { 0xC04B7961, "weapons" },
            { 0x433DFAB8, "aircraft" }, { 0x35B5E1F5, "watercraft" }, { 0x24118452, "mainchar" },
            { 0xAF850D9F, "drivers" }, { 0x98209D8E, "polymorphs" }, { 0xF17425C8, "economy" },
            { 0xD36722F2, "Dynaplaced" }, { 0x0F0B6DE5, "narrative" }, { 0x6706A862, "museum" },
            { 0x4B4BD045, "combatants" },
            { 0xF71C2A21, "desc" }, { 0x9A972F56, "definitions" }, { 0xBCB349EA, "rank_unload_scale" },
            { 0x7295DAEE, "rank_despawn" }, { 0x79B1AC0D, "high_priority" }, { 0x1F9589B0, "primary" },
            { 0x45FA699F, "preload" }, { 0x3E1B4BCD, "def_limit" }, { 0x89D007B0, "inst_limit" },
            { 0x9A2DB97E, "proximity_limit" }, { 0x7432C7A9, "default_aabb" }
        };

        public static string NameOf(uint h)
        {
            string n;
            if (WellKnown.TryGetValue(h, out n)) return n;
            if (h == 0) return "zero";
            return "unk_" + h.ToString("X8");
        }

        static uint U32(byte[] d, int o) { return BitConverter.ToUInt32(d, o); }
        static ushort U16(byte[] d, int o) { return BitConverter.ToUInt16(d, o); }

        static string CString(byte[] data, uint off, int ml = 2000)
        {
            int io = (int)off;
            if (io < 0 || io >= data.Length) return "";
            int end = Array.IndexOf(data, (byte)0, io);
            if (end < 0 || end - io > ml) end = Math.Min(io + ml, data.Length);
            return Encoding.UTF8.GetString(data, io, end - io);
        }

        static RTPCProp ReadProp(byte[] data, int off)
        {
            if (off + 9 > data.Length) return null;
            uint hash = U32(data, off);
            uint raw = U32(data, off + 4);
            byte type = data[off + 8];
            var p = new RTPCProp { Hash = hash, Name = NameOf(hash), Type = type, Offset = off, ValueOffset = off + 4 };
            try
            {
                switch (type)
                {
                    case 1: p.Value = (int)raw; break;
                    case 2: p.Value = (float)Math.Round((double)BitConverter.ToSingle(data, off + 4), 6); break;
                    case 3: p.Value = CString(data, raw); break;
                    case 4:
                        if (raw + 8 <= data.Length)
                            p.Value = new float[] { BitConverter.ToSingle(data, (int)raw), BitConverter.ToSingle(data, (int)raw + 4) };
                        break;
                    case 5:
                        if (raw + 12 <= data.Length)
                            p.Value = new float[] { BitConverter.ToSingle(data, (int)raw), BitConverter.ToSingle(data, (int)raw + 4), BitConverter.ToSingle(data, (int)raw + 8) };
                        break;
                    case 6:
                        if (raw + 16 <= data.Length)
                            p.Value = new float[] { BitConverter.ToSingle(data, (int)raw), BitConverter.ToSingle(data, (int)raw + 4), BitConverter.ToSingle(data, (int)raw + 8), BitConverter.ToSingle(data, (int)raw + 12) };
                        break;
                    case 13:
                        if (raw + 8 <= data.Length)
                            p.Value = BitConverter.ToUInt64(data, (int)raw);
                        break;
                }
            }
            catch { }
            return p;
        }

        static int Align4(int x) { return x + ((4 - (x % 4)) % 4); }

        static RTPCNode ReadNode(byte[] data, ref int pos, int depth)
        {
            if (depth > 30 || pos + 12 > data.Length) return null;
            uint nh = U32(data, pos);
            uint doff = U32(data, pos + 4);
            ushort pc = U16(data, pos + 8);
            ushort cc = U16(data, pos + 10);
            var node = new RTPCNode { Hash = nh, Name = NameOf(nh) };

            int p = (int)doff;
            for (int i = 0; i < pc; i++)
            {
                var pr = ReadProp(data, p);
                if (pr != null) node.Props.Add(pr);
                p += 9;
            }
            int cp = Align4(p);
            pos += 12;
            for (int i = 0; i < cc; i++)
            {
                var child = ReadNode(data, ref cp, depth + 1);
                if (child != null) node.Children.Add(child);
            }
            return node;
        }

        public static RTPCFile Parse(string path)
        {
            byte[] data = File.ReadAllBytes(path);
            uint ver = U32(data, 4);
            int pos = 8;
            var root = ReadNode(data, ref pos, 0);
            return new RTPCFile { FilePath = path, Version = ver, Root = root };
        }
    }

    public static class RTPCApplier
    {
        public static int Apply(string rtpcPath, List<RTPCPatch> patches)
        {
            byte[] data = File.ReadAllBytes(rtpcPath);
            int n = 0;
            foreach (var p in patches)
            {
                if (p.Offset < 0 || p.Offset + 4 > data.Length) continue;
                if (p.Type == 1)
                {
                    BitConverter.GetBytes((uint)p.Value).CopyTo(data, p.Offset);
                    n++;
                }
                else if (p.Type == 2)
                {
                    BitConverter.GetBytes((float)p.Value).CopyTo(data, p.Offset);
                    n++;
                }
            }
            File.WriteAllBytes(rtpcPath, data);
            return n;
        }
    }

    public static class SemanticMap
    {
        public static Dictionary<string, PropInfo> Map = new Dictionary<string, PropInfo>(StringComparer.Ordinal);
        public static Dictionary<string, string> Resolved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public static Dictionary<string, string> ResolvedProbable = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public static Dictionary<string, string> ResolvedExperimental = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        static void A(string k, string n, string c, string d, string r = null)
        {
            Map[k] = new PropInfo { Name = n, Conf = c, Desc = d, Reason = r };
        }

        static SemanticMap()
        {
            A("root", "Root", "I", "Top of the file. Never edit.");
            A("spawn_system", "Spawn System", "I", "Root container of the entire spawn pipeline. Child nodes are the different subsystems (Budget, Definitions, Streamers...).");
            A("budget", "Spawn Budget", "I", "GLOBAL BUDGET. The engine has a fixed pool of resources to place entities around the player. Each category below (Civilians, Vehicles, Combatants, Animals...) competes for a slice of this budget. Raising a category's \"Amount in World\" gives it more of the pool; lowering it gives less.");
            A("props", "Properties", "I", "Internal property block for the parent node.");
            A("type_infos", "Type Infos", "I", "Definitions of each vehicle / creature type (size, physics, AI template).");
            A("civilians", "Civilians", "C", "Regular NPC pedestrians that walk around cities and roads. Passive. Their presence is what makes the open world feel alive.");
            A("animals", "Animals", "C", "Wildlife and creatures (mutants, dogs, wild boars). Includes hostile variants.");
            A("encounters", "Encounters", "C", "Dynamic events placed in the world: convoys, raids, ambushes, races. These are the \"random stuff happens\" moments.");
            A("vehicles", "Ground Vehicles", "C", "Cars, trucks, buggies, bikes parked or driven. Without drivers they are static; with drivers they move.");
            A("weapons", "Weapons", "C", "Pickup spawners for weapons and ammo on the ground.");
            A("aircraft", "Air Vehicles", "C", "Flying vehicles (gyrocopters, drones). Rare and mostly scripted.");
            A("watercraft", "Water Vehicles", "C", "Boats and other water transport. Only spawn near lakes and rivers.");
            A("mainchar", "Main Characters", "C", "Story NPCs. Essential to the plot. Do not lower this below 1 or plot missions can break.");
            A("drivers", "Drivers", "C", "NPCs that sit inside vehicles. Each driver makes one vehicle move around the world.");
            A("polymorphs", "Polymorphs", "C", "Shape-shifting creatures (a specific RAGE 2 enemy type).");
            A("economy", "Economy", "C", "Money, vendors, trading posts, currency spawns.");
            A("Dynaplaced", "Dynaplaced Objects", "C", "Objects placed dynamically at runtime (props, cover, loot containers) based on where the player is.");
            A("narrative", "Narrative Events", "C", "Scripted story-triggered events that place specific entities when certain conditions occur.");
            A("museum", "Museum Displays", "C", "Decoration objects inside the museum location. Almost never gameplay-relevant.");
            A("combatants", "Combatants", "C", "Hostile and neutral fighters that populate combat zones: bandits, Authority soldiers, mutants. These are the enemies you actually fight. Competes for the same global budget as Civilians, so raising Civilians can reduce Combatants.");
            A("misc", "Miscellaneous Props", "C", "Small decorative props (crates, barrels, street signs). Purely cosmetic.");
            A("desc", "Label", "C", "Display name of this category. Read-only.");
            A("definitions", "Rules File", "C", "Path to the definitions file that tells the engine WHICH entities to spawn. Read-only.");
            A("rank_unload_scale", "Amount in World", "C", "MAIN MULTIPLIER for this category. It scales how much of the global budget is spent on this category. Raise it to see MORE (e.g. more civilians), lower for fewer.");
            A("rank_despawn", "Despawn Distance (m)", "C", "Distance in meters at which the game removes instances of this category when you walk away. Higher = entities stay around longer, lower = they free up resources sooner.");
            A("high_priority", "High Priority", "C", "0 or 1. If 1, this category is served BEFORE lower-priority categories when the budget is tight.");
            A("primary", "Essential Category", "C", "0 or 1. If 1, the engine always keeps at least a minimal presence of this category even when budget is exhausted.");
            A("inst_limit", "Max Active at Once", "C", "Hard cap. The engine will never have more than this many active at the same time, no matter how much budget is available.");
            A("def_limit", "Max Variants", "C", "Maximum number of different variants (models/skins) of this category allowed active at once.");
            A("proximity_limit", "Min Separation (m)", "C", "Minimum distance in meters between two instances. Prevents clumping.");
            A("preload", "Preload Count", "C", "How many instances are prepared in memory before they appear on screen. Higher = smoother appearance (less pop-in).");
            A("default_aabb", "Collision Box", "C", "Axis-aligned bounding box used for coarse collision. Read-only.");
            A("Fire", "Fire Damage", "C", "Damage type for fire / burning sources.");
            A("Electricity", "Electricity Damage", "C", "Damage type for electric / shock sources.");
            A("Bullet", "Bullet Damage", "C", "Damage type for bullets / firearms.");
            A("Impact", "Impact Damage", "C", "Damage type for collisions, melee hits and physical strikes.");
            A("EMP", "EMP Damage", "C", "Damage type for EMP / disruptor sources.");
            A("Corruption", "Corruption Damage", "C", "Damage type for corruption / nanotrite sources.");
            A("Unknown", "Unknown Damage", "C", "Fallback damage type used when nothing else applies.");
            A("difficulty", "Difficulty", "C", "Overall difficulty profile for the game.");
            A("easy", "Easy", "C", "Easy difficulty preset.");
            A("normal", "Normal", "C", "Normal difficulty preset.");
            A("hard", "Hard", "C", "Hard difficulty preset.");
            A("icon", "Icon", "C", "Icon asset used in the UI.");
            A("influence", "Influence", "C", "Faction / region influence value.");
            A("category", "Category", "C", "Category tag for filtering.");
            A("folder", "Folder", "C", "Folder / asset path.");
            A("text", "Text", "C", "Localisation / display text.");
            A("team", "Team", "C", "Team / faction assignment.");
            A("preset_name", "Preset Name", "C", "Name of the weather preset.");
            A("weather_presets", "Weather Presets", "C", "List of weather preset entries.");
            A("settlements", "Settlements", "C", "Settlement entries.");

            // Deduced
            A("unk_C43CBF04", "Attempt Frequency", "D", "How often the engine TRIES to spawn this category. Lower value = more frequent attempts. Not 100% confirmed.",
                "Hipotesis: multiplicador de frecuencia. Type=float (byte [rax+8]==2), slot [rbp+0x78]. Valores tipicos 0.7-16.0. NO confirmado in-game.");
            A("unk_73EDD9B4", "Spawn Radius (m)", "D", "Distance from the player within which this category is allowed to spawn. Not 100% confirmed.",
                "Hipotesis: radio de spawn en metros. Type=float, valores tipicos 1.5-2.5. Slot [rbp+0x74] adyacente a otras props de spawn. NO confirmado.");
            A("unk_445E4AA3", "Spawn Cooldown (s)", "D", "Minimum seconds between two consecutive spawns of this category. Not 100% confirmed.",
                "Hipotesis: cooldown entre spawns. Type=int, valores 30-50. Slot r12d. Compatible con segundos. NO confirmado.");
            A("unk_569055C7", "Lifetime (s)", "D", "How long an instance lives before despawning on its own. Not 100% confirmed.",
                "Hipotesis: tiempo de vida. Type=float, valores tipicos 75-300. Rango de lifetime de instancias. NO confirmado.");
            A("unk_0D19D678", "Max Spawn Distance (m)", "D", "Maximum distance at which this category can spawn. Not 100% confirmed.",
                "Hipotesis: distancia maxima de spawn. Type=int convertido a float (cvtdq2ps). Valores 30-200. Compatible con metros. NO confirmado.");
            A("unk_45EC7582", "Spawn Rate Multiplier", "D", "Multiplier applied to the spawn rate. Not 100% confirmed.",
                "Hipotesis: multiplicador de rate. Type=float, valores tipicos 1.0-2.75. Slot [rbp+0x70] tras preload. NO confirmado.");
            A("unk_E92A5496", "Cleanup Radius (m)", "D", "Radius around the player where instances are force-despawned if the budget is tight. Not 100% confirmed.",
                "NO LOCALIZADO en el exe ni en RED_EYE DB. Nombre puramente inferido por contexto del budget pool. ALTAMENTE ESPECULATIVO.");
            A("unk_B079723B", "Despawn Delay (s)", "D", "Delay before a marked instance is removed. Not 100% confirmed.",
                "Hipotesis: retardo antes de despawn. Deducido por posicion en el RTPC (cercano a rank_despawn). NO confirmado.");
            A("unk_780113EE", "Spawn Probability", "D", "Relative probability that this category is picked when the engine decides to spawn something. Not 100% confirmed.",
                "NO LOCALIZADO en el exe ni en RED_EYE DB. Nombre inferido por contexto. ALTAMENTE ESPECULATIVO.");

            // RED_EYE added
            A("defence_tower_health", "Tower Health", "C", "Health pool of defence towers. Higher = tankier.");
            A("defence_tower_stage1_global_cooldown_min", "T1 Global Cooldown (min)", "C", "Stage 1 minimum global cooldown between tower actions.");
            A("defence_tower_stage1_single_cannon_cooldown_min", "T1 Cannon Cooldown (min)", "C", "Stage 1 minimum cooldown per single cannon.");
            A("defence_tower_stage2_large_cooldown_max", "T2 Large Cooldown (max)", "C", "Stage 2 maximum cooldown for large turret.");
            A("defence_tower_stage2_large_cooldown_min", "T2 Large Cooldown (min)", "C", "Stage 2 minimum cooldown for large turret.");
            A("defence_tower_stage2_popcorn_cooldown_min", "T2 Popcorn Cooldown (min)", "C", "Stage 2 minimum cooldown for rapid-fire turret.");
            A("defence_tower_stage2_pulse_cooldown_min", "T2 Pulse Cooldown (min)", "C", "Stage 2 minimum cooldown for pulse turret.");
            A("defence_tower_stage2_single_cannon_cooldown_max", "T2 Cannon Cooldown (max)", "C", "Stage 2 maximum cooldown per single cannon.");
            A("defence_tower_stage3_large_cooldown_max", "T3 Large Cooldown (max)", "C", "Stage 3 maximum cooldown for large turret.");
            A("defence_tower_stage3_large_cooldown_min", "T3 Large Cooldown (min)", "C", "Stage 3 minimum cooldown for large turret.");
            A("defence_tower_stage3_single_cannon_cooldown_max", "T3 Cannon Cooldown (max)", "C", "Stage 3 maximum cooldown per single cannon.");
            A("defence_tower_stage3_single_cannon_cooldown_min", "T3 Cannon Cooldown (min)", "C", "Stage 3 minimum cooldown per single cannon.");
            A("AmmunitionRocket", "Rocket Ammo", "C", "Ammunition identifier for rocket launchers.");
            A("AmmunitionTumbleGun", "TumbleGun Ammo", "C", "Ammunition identifier for the TumbleGun.");
            A("AmmunitionLongRange", "Long Range Ammo", "C", "Ammunition identifier for sniper / long range weapons.");
            A("AmmunitionSmallArms", "Small Arms Ammo", "C", "Ammunition identifier for pistols, SMGs, rifles.");
            A("IngredientJunk", "Junk Material", "C", "Crafting ingredient: junk.");
            A("IngredientElectronicComponent", "Electronic Component", "C", "Crafting ingredient: electronics.");
            A("IngredientMetalComponent", "Metal Component", "C", "Crafting ingredient: metal.");
            A("IngredientExplosiveComponent", "Explosive Component", "C", "Crafting ingredient: explosives.");
            A("v001_prototype.ee", "Prototype Vehicle", "C", "Vehicle prefab: prototype.");
            A("v002_warrig.ee", "War Rig Vehicle", "C", "Vehicle prefab: war rig.");
            A("v004_gastownwarrig_trailer.ee", "Gas Town Trailer", "C", "Vehicle prefab: Gas Town trailer.");
            A("v005_scythe_car.ee", "Scythe Car", "C", "Vehicle prefab: scythe car.");
            A("v006_gutgash_bus.ee", "Gutgash Bus", "C", "Vehicle prefab: gutgash bus.");
            A("v010_armored_car.ee", "Armored Car", "C", "Vehicle prefab: armored car.");
            A("v011_beetle_car.ee", "Beetle Car", "C", "Vehicle prefab: beetle car.");
            A("v012_pickup.ee", "Pickup Vehicle", "C", "Vehicle prefab: pickup truck.");
            A("v012_pickup_right.ee", "Pickup (Right-Hand)", "C", "Vehicle prefab: right-hand drive pickup.");
            A("v015_heavyramcar.ee", "Heavy Ram Car", "C", "Vehicle prefab: heavy ram car.");
            A("SupportItemGrenade", "Grenade Item", "C", "Support item: grenade.");
            A("SupportItemHealthPack", "Health Pack Item", "C", "Support item: health pack.");
            A("CurrencyBase", "Currency Base", "C", "Base currency amount for economy.");
            A("BaseHealth", "Base Health", "C", "Base health value used as default.");
            A("melee_damage", "Melee Damage", "C", "Damage dealt by melee attacks.");
            A("Melee", "Melee (Damage Type)", "C", "Damage type identifier: melee.");
            A("accuracy_increase_rate", "Accuracy Increase Rate", "C", "Rate at which accuracy grows.");
            A("power_move_cooldown_global", "Power Move CD (Global)", "C", "Global cooldown for power moves.");
            A("power_move_cooldown_local", "Power Move CD (Local)", "C", "Per-weapon cooldown for power moves.");
            A("shrouded_bolter_armor", "Shrouded Bolter Armor", "C", "Armor value for the shrouded bolter enemy.");
            A("mutant_shared_health", "Mutant Shared Health", "C", "Shared health pool for mutant enemies.");
            A("cyber_crusher_weakspot_health", "Cyber Crusher Weakspot HP", "C", "Health pool for cyber crusher weakspot.");
            A("WingStick", "Wing Stick", "C", "Wing stick component.");
            A("PreferenceMultiplier", "Preference Multiplier", "C", "Multiplier applied to preference weights.");
            A("AmountMultiplier", "Amount Multiplier", "C", "Generic multiplier applied to a quantity.");
            A("SmallOffset", "Small Offset", "C", "Small spatial offset (low magnitude).");
            A("MinimumOffset", "Minimum Offset", "C", "Minimum offset floor.");
            A("LargeOffset", "Large Offset", "C", "Large spatial offset (high magnitude).");
            A("overcast", "Overcast", "C", "Weather preset: overcast.");
            A("semi_indoor", "Semi-Indoor", "C", "Marker for semi-indoor zones.");
            A("target_outline_color", "Target Outline Color", "C", "Color used for target outline highlight.");
            A("exploration", "Exploration Marker", "C", "Marker for exploration / discovery systems.");
            A("cameras", "Cameras", "C", "Camera definitions list.");
            A("id", "ID", "I", "Numeric identifier. Read-only.");
            A("type", "Type", "I", "Type tag. Read-only.");
            A("value", "Value", "I", "Generic value field. Read-only.");
            A("min", "Minimum", "I", "Generic minimum value. Read-only.");
            A("max", "Maximum", "I", "Generic maximum value. Read-only.");
            A("entity", "Entity", "I", "Entity reference. Read-only.");
            A("enum_def", "Enum Definition", "I", "Enumeration definition block. Read-only.");
            A("threshold", "Threshold", "C", "Generic threshold value.");
            A("priority", "Priority", "C", "Generic priority ordering.");
            A("increment", "Increment", "C", "Increment step for progression.");
            A("enable_event", "Enable Event", "C", "Event to trigger when enabled.");
        }

        public static PropInfo GetInfo(string rawName)
        {
            if (string.IsNullOrEmpty(rawName)) return new PropInfo { Name = "?", Conf = "E", Desc = "" };
            PropInfo info;
            if (Map.TryGetValue(rawName, out info)) return info;
            if (rawName.StartsWith("unk_", StringComparison.OrdinalIgnoreCase))
            {
                string key = rawName.Substring(4).ToUpperInvariant();
                string unkKey = "unk_" + key;
                if (Map.TryGetValue(unkKey, out info)) return info;
                string pretty;
                if (ResolvedProbable.TryGetValue(key, out pretty))
                {
                    if (Map.TryGetValue(pretty, out info)) return info;
                    return new PropInfo { Name = pretty, Conf = "P", Desc = "Probable name. High-confidence structural pattern match. Meaning likely correct but not 100% verified." };
                }
                if (Resolved.TryGetValue(key, out pretty))
                {
                    if (Map.TryGetValue(pretty, out info)) return info;
                    return new PropInfo { Name = pretty, Conf = "D", Desc = "Resolved name. Meaning not yet documented - use with caution." };
                }
                if (ResolvedExperimental.TryGetValue(key, out pretty))
                {
                    if (Map.TryGetValue(pretty, out info)) return info;
                    return new PropInfo { Name = pretty, Conf = "E", Desc = "Experimental name. Very low confidence, may be a hash collision. Use with extreme caution." };
                }
                return new PropInfo { Name = "Unknown (" + rawName.Substring(4) + ")", Conf = "E", Desc = "Hash not resolved yet. Meaning unknown." };
            }
            return new PropInfo { Name = rawName, Conf = "E", Desc = "" };
        }

        public static void LoadNames()
        {
            var candidates = new List<string>();
            try { candidates.Add(@"D:\RAGE2MODDING\_tools\settings_editor\crack_names_clean.json"); } catch { }
            try { candidates.Add(@"D:\RAGE2MODDING\_tools\settings_editor\crack_names.json"); } catch { }
            try { candidates.Add(@"D:\DEV-TOOLS\apexconv\crack_names_clean.json"); } catch { }
            try { candidates.Add(@"D:\DEV-TOOLS\apexconv\crack_names.json"); } catch { }
            try { candidates.Add(Path.Combine(Rage2Toolkit.Paths.ExeDir, "data", "settings_editor_names.json")); } catch { }

            var probablePaths = new List<string>();
            try { probablePaths.Add(Path.Combine(Rage2Toolkit.Paths.ExeDir, "data", "settings_editor_probable.json")); } catch { }
            try { probablePaths.Add(@"D:\DEV-TOOLS\apexconv\settings_editor_probable.json"); } catch { }

            var experimentalPaths = new List<string>();
            try { experimentalPaths.Add(Path.Combine(Rage2Toolkit.Paths.ExeDir, "data", "settings_editor_experimental.json")); } catch { }
            try { experimentalPaths.Add(@"D:\DEV-TOOLS\apexconv\settings_editor_experimental.json"); } catch { }

            Action<string, Dictionary<string, string>> loadInto = (cp, dict) =>
            {
                try
                {
                    if (!File.Exists(cp)) return;
                    string raw = File.ReadAllText(cp);
                    using (var doc = JsonDocument.Parse(raw))
                    {
                        foreach (var prop in doc.RootElement.EnumerateObject())
                        {
                            string k = prop.Name.ToUpperInvariant().Replace("0X", "");
                            string v = prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString() : prop.Value.ToString();
                            if (!string.IsNullOrEmpty(v) && !dict.ContainsKey(k)) dict[k] = v;
                        }
                    }
                }
                catch { }
            };

            foreach (var cp in candidates) loadInto(cp, Resolved);
            foreach (var cp in probablePaths) loadInto(cp, ResolvedProbable);
            foreach (var cp in experimentalPaths) loadInto(cp, ResolvedExperimental);
        }
    }
}