using BepInEx;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;

namespace MC_SVBC304Nuke
{
    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    public class Main : BaseUnityPlugin
    {
        public const string pluginGuid = "mc.starvalor.bc304nuke";
        public const string pluginName = "SV BC304 Nuke";
        public const string pluginVersion = "1.0.0";

        private const int bc304ID = 308;
        private const int nukeID = 45352;
        private const string equipmentName = "Nuclear Warhead Transporter";
        private const string description = "Enlightens the targeted ship.\n\n" +
            "Beams a nuclear warhead directly onto the target ship.  Deals full damage to target, ignoring shields and armor.  Area of effect damage is affected by all defences.\n" +
            "<color=#808080>Target Damage:</color> <color=#FFFFFF><TDMG></color>\n" +
            "<color=#808080>Crit Chance:</color> <color=#FFFFFF><CRIT></color>\n" +
            "<color=#808080>Crit Damage Bonus:</color> <color=#FFFFFF><CDMG>x</color>\n" +
            "<color=#808080>AOE Damage:</color> <color=#FFFFFF><ADMG></color>\n" +
            "<color=#808080>AOE Range:</color> <color=#FFFFFF><RNG></color>";
        private const float baseDmg = 2500;
        private const float aoeMod = 0.2f;
        private const float baseCritChance = 10;
        private const float baseCritDamage = 1.5f;
        private const float baseAOE = 200;
        private const float cooldown = 60;
        private const float energyCost = 1000;

        private static Sprite equipmentIcon;
        private static GameObject explodeFX;
        private static GameObject beamAudioGO;
        private static GameObject buffGO = null;
        
        public void Awake()
        {
            Harmony.CreateAndPatchAll(typeof(Main));

            string pluginfolder = System.IO.Path.GetDirectoryName(GetType().Assembly.Location);
            string bundleName = "mc_svbc304nuke";
            AssetBundle assets = AssetBundle.LoadFromFile($"{pluginfolder}\\{bundleName}");
            equipmentIcon = assets.LoadAsset<Sprite>("Assets/Nuke/icon.png");
            explodeFX = assets.LoadAsset<GameObject>("Assets/Nuke/ppfxExplosionHeavyShockwave.prefab");
            beamAudioGO = assets.LoadAsset<GameObject>("Assets/Nuke/BeamAudio.prefab");
        }

        //private void Update()
        //{
        //    if (Input.GetKeyDown(KeyCode.Backspace))
        //    {
        //        List<ShipBonus> sb = new List<ShipBonus>(GameManager.instance.Player.GetComponent<SpaceShip>().shipData.GetShipModelData().modelBonus);
        //        sb.Add(new SB_BuiltInEquipment() { equipmentID = id });
        //        GameManager.instance.Player.GetComponent<SpaceShip>().shipData.GetShipModelData().modelBonus = sb.ToArray();
        //    }
        //}

        [HarmonyPatch(typeof(DemoControl), "SpawnMainMenuBackground")]
        [HarmonyPostfix]
        private static void DemoControlSpawnMainMenuBackground_Post()
        {
            List<ShipBonus> sb = new List<ShipBonus>(ShipDB.GetModel(bc304ID).modelBonus);
            sb.Add(new SB_BuiltInEquipment() { equipmentID = nukeID });
            ShipDB.GetModel(bc304ID).modelBonus = sb.ToArray();
        }

        [HarmonyPatch(typeof(EquipmentDB), "LoadDatabaseForce")]
        [HarmonyPostfix]
        private static void EquipmentDBLoadDBForce_Post()
        {
            AccessTools.StaticFieldRefAccess<List<Equipment>>(typeof(EquipmentDB), "equipments").Add(CreateEquipment());
        }

        private static Equipment CreateEquipment()
        {
            Equipment equipment = ScriptableObject.CreateInstance<Equipment>();
            equipment.name = nukeID + "." + equipmentName;
            equipment.id = nukeID;
            equipment.refName = equipmentName;
            equipment.minShipClass = ShipClassLevel.Cruiser;
            equipment.activated = true;
            equipment.enableChangeKey = true;
            equipment.space = 10;
            equipment.energyCost = energyCost;
            equipment.energyCostPerShipClass = false;
            equipment.rarityCostMod = 0.8f;
            equipment.techLevel = 40;
            equipment.sortPower = 2;
            equipment.massChange = 0;
            equipment.type = EquipmentType.Device;
            equipment.effects = new List<Effect>() { new Effect() { type = 53, description = "", mod = 1f, value = 20f, uniqueLevel = 0 } };
            equipment.uniqueReplacement = true;
            equipment.rarityMod = 2f;
            equipment.sellChance = 0;
            equipment.repReq = new ReputationRequisite() { factionIndex = 0, repNeeded = 0 };
            equipment.dropLevel = DropLevel.DontDrop;
            equipment.lootChance = 0;
            equipment.spawnInArena = false;
            equipment.sprite = equipmentIcon;
            equipment.activeEquipmentIndex = nukeID;
            equipment.defaultKey = KeyCode.Alpha3;
            equipment.requiredItemID = -1;
            equipment.requiredQnt = 0;
            equipment.equipName = equipmentName;
            equipment.description = description;
            equipment.craftingMaterials = null;
            if (buffGO == null)
                MakeBuffGO(equipment);
            equipment.buff = buffGO;

            return equipment;
        }

        private static void MakeBuffGO(Equipment equip)
        {
            buffGO = new GameObject { name = "NukeTransporter" };
            buffGO.AddComponent<BuffControl>();
            buffGO.GetComponent<BuffControl>().owner = null;
            buffGO.GetComponent<BuffControl>().activeEquipment = MakeActiveEquip(
                equip, null, equip.defaultKey, 1, 0);
        }

        private static AE_MCNuke MakeActiveEquip(Equipment equipment, SpaceShip ss, KeyCode key, int rarity, int qnt)
        {
            AE_MCNuke nukeAE = new AE_MCNuke
            {
                id = equipment.id,
                rarity = rarity,
                key = key,
                ss = ss,
                isPlayer = (ss != null && ss.CompareTag("Player")),
                equipment = equipment,
                qnt = qnt
            };
            nukeAE.active = false;
            return nukeAE;
        }

        [HarmonyPatch(typeof(ActiveEquipment), "ActivateDeactivate")]
        [HarmonyPrefix]
        internal static void ActivateDeactivate_Pre(ActiveEquipment __instance)
        {
            if (__instance != null || __instance.equipment != null ||
                __instance.equipment.id != nukeID)
                return;

            if (buffGO == null)
                MakeBuffGO(__instance.equipment);
            buffGO.GetComponent<BuffControl>().activeEquipment = __instance;
            __instance.equipment.buff = buffGO;
        }

        [HarmonyPatch(typeof(ActiveEquipment), "AddActivatedEquipment")]
        [HarmonyPrefix]
        private static bool ActiveEquipmentAdd_Pre(Equipment equipment, SpaceShip ss, KeyCode key, int rarity, int qnt, ref ActiveEquipment __result)
        {
            if (GameManager.instance != null && GameManager.instance.inGame &&
                equipment.id == nukeID)
            {
                __result = MakeActiveEquip(equipment, ss, key, rarity, qnt);
                ss.activeEquips.Add(__result);
                __result.AfterConstructor();
                return false;
            }

            return true;
        }

        [HarmonyPatch(typeof(EquipmentDB), nameof(EquipmentDB.GetEquipmentString))]
        [HarmonyPostfix]
        private static void EquipmentDBGetEquipmentString_Post(int id, int rarity, ref string __result)
        {
            if (id != Main.nukeID)
                return;

            SpaceShip ss = GameManager.instance.Player.GetComponent<SpaceShip>();
            float aoe = baseAOE + ss.stats.aoeBonus;
            float critChance = Mathf.Clamp(baseCritChance + ss.stats.weaponCritBonus, 0f, 100f);
            float critDamage = 2 + ss.stats.weaponCritDamageBonus;
            float damage = (((baseDmg + PChar.SKMod(4)) * 
                (1f + (float)PChar.Char.PassiveLimited(1) * 0.01f)) *
                ItemDB.GetRarityMod(rarity, 1f)) *
                (1f + ss.stats.heavyWeaponBonus) *
                ss.energyMmt.valueMod(0);
            float aoedamage = ((((baseDmg * aoeMod) + PChar.SKMod(4)) *
                (1f + (float)PChar.Char.PassiveLimited(1) * 0.01f)) *
                ItemDB.GetRarityMod(rarity, 1f)) *
                (1f + ss.stats.heavyWeaponBonus) *
                ss.energyMmt.valueMod(0); ;

            __result = __result.Replace("<TDMG>", damage.ToString());
            __result = __result.Replace("<ADMG>", aoedamage.ToString());
            __result = __result.Replace("<CRIT>", critChance.ToString());
            __result = __result.Replace("<CDMG>", critDamage.ToString());
            __result = __result.Replace("<RNG>", aoe.ToString());
        }

        public class AE_MCNuke : AE_BuffBased
        {
            protected override bool showBuffIcon
            {
                get
                {
                    return this.isPlayer;
                }
            }

            private Transform target;

            public AE_MCNuke()
            {
                this.targetIsSelf = true;
                this.saveState = true;
                this.saveCooldownID = id;
                this.cooldownTime = cooldown;                
            }

            public override void ActivateDeactivate(bool shiftPressed, Transform target)
            {
                this.startEnergyCost = this.equipment.energyCost;

                if (this.active)
                    base.ActivateDeactivate(shiftPressed, target);

                if (target == null || target.CompareTag("Player"))
                {
                    InfoPanelControl.inst.ShowWarning("Invalid transporter target.", 1, false);
                    return;
                }

                this.target = target;

                base.ActivateDeactivate(shiftPressed, target);
            }

            public override void AfterActivate()
            {
                base.AfterActivate();

                ExplosionBehaviour eb = target.gameObject.AddComponent<ExplosionBehaviour>();
                eb.ss = this.ss;
                eb.aoe = baseAOE + ss.stats.aoeBonus;
                eb.critChance = Mathf.Clamp(baseCritChance + ss.stats.weaponCritBonus, 0f, 100f);
                eb.critDamage = 2 + ss.stats.weaponCritDamageBonus;
                eb.damage = (((baseDmg + PChar.SKMod(4)) *
                (1f + (float)PChar.Char.PassiveLimited(1) * 0.01f)) *
                ItemDB.GetRarityMod(rarity, 1f)) *
                (1f + ss.stats.heavyWeaponBonus) *
                ss.energyMmt.valueMod(0);
                eb.rarity = this.rarity;

                GameObject beamAudio = GameObject.Instantiate(beamAudioGO, ss.transform.position, ss.transform.rotation);
                beamAudio.GetComponent<AudioSource>().volume = SoundSys.SFXvolume;

                base.ActivateDeactivate(false, null);
            }
        }

        internal class ExplosionBehaviour : MonoBehaviour
        {         
            internal float damage;
            internal float aoe;
            internal float critChance;
            internal float critDamage;
            internal SpaceShip ss;
            internal int rarity;

            private void Start()
            {
                Invoke("Explode", 2f);
            }

            private void Explode()
            {
                Entity targetEntity = base.GetComponent<Entity>();
                if (targetEntity != null)
                    targetEntity.Apply_Damage(damage, new TDamage(critChance, critDamage, 100, false, false), DamageType.IgnoreShields, targetEntity.transform.position, ss.transform, WeaponImpact.normal);

                GameObject newFX = GameObject.Instantiate(explodeFX, base.transform.position + new Vector3(0, 2, 0), base.transform.rotation);
                newFX.GetComponent<AudioSource>().volume = SoundSys.SFXvolume;
                newFX.transform.Find("shockwavefast").GetComponent<ParticleSystem>().startSize = aoe * 2;
                Explosion explosion = Object.Instantiate<GameObject>(ObjManager.GetProj("Projectiles/Explosion"), base.transform.position, base.transform.rotation).GetComponent<Explosion>();
                explosion.aoe = aoe;
                explosion.damage = ((((baseDmg * aoeMod) + PChar.SKMod(4)) *
                    (1f + (float)PChar.Char.PassiveLimited(1) * 0.01f)) *
                    ItemDB.GetRarityMod(rarity, 1f)) *
                    (1f + ss.stats.heavyWeaponBonus) *
                    ss.energyMmt.valueMod(0);
                explosion.damageType = DamageType.Normal;
                explosion.tDmg = new TDamage(critChance, critDamage, 0, false, false);
                explosion.canHitProjectiles = true;
                explosion.owner = ss.transform;
                explosion.Setup(0.6f);
            }
        }
    }
}
