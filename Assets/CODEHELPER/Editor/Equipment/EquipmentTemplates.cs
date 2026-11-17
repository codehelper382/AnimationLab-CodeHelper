#if UNITY_EDITOR
using CodeHelper.ProceduralShooterFramework.Equipment;
using UnityEditor;
using UnityEngine;

namespace CodeHelper.ProceduralShooterFramework.EditorTools
{
    /// <summary>Own authoring templates. No scene changes, third-party assets or clip guesses.</summary>
    public static class EquipmentTemplates
    {
        [MenuItem("Assets/Create/CodeHelper/Equipment/Templates/Pistol")]
        private static void Pistol() => Save(CreateFirearm("Pistol", FirearmMechanism.SelfLoading, 15, 360f));

        [MenuItem("Assets/Create/CodeHelper/Equipment/Templates/Rifle")]
        private static void Rifle()
        {
            FirearmDefinition item = CreateFirearm("Rifle", FirearmMechanism.SelfLoading, 30, 600f);
            item.fireMode = FireMode.Automatic;
            Save(item);
        }

        [MenuItem("Assets/Create/CodeHelper/Equipment/Templates/Bolt Action Rifle")]
        private static void Bolt() => Save(CreateFirearm("BoltRifle", FirearmMechanism.ManualBolt, 5, 60f));

        [MenuItem("Assets/Create/CodeHelper/Equipment/Templates/Pump Shotgun")]
        private static void Pump()
        {
            FirearmDefinition item = CreateFirearm("PumpShotgun", FirearmMechanism.Pump, 6, 90f);
            item.reloadMethod = ReloadMethod.IndividualRounds;
            item.insertRound = MakeAction("Insert shell", 0.65f, 0.7f);
            Save(item);
        }

        [MenuItem("Assets/Create/CodeHelper/Equipment/Templates/Knife")]
        private static void Knife() => Save(CreateMelee(false));

        [MenuItem("Assets/Create/CodeHelper/Equipment/Templates/Fists")]
        private static void Fists() => Save(CreateMelee(true));

        public static FirearmDefinition CreateFirearm(string name, FirearmMechanism mechanism, int capacity, float rpm)
        {
            FirearmDefinition item = ScriptableObject.CreateInstance<FirearmDefinition>();
            item.name = item.displayName = item.equipmentId = name;
            item.mechanism = mechanism;
            item.magazineCapacity = capacity;
            item.initialMagazineRounds = Mathf.Max(0, capacity - 1);
            item.initialReserveRounds = capacity * 3;
            item.roundsPerMinute = rpm;
            item.reloadChambersRound = mechanism == FirearmMechanism.SelfLoading;
            item.equip = MakeAction("Equip", 0.35f, 1f);
            item.unequip = MakeAction("Unequip", 0.3f, 1f);
            item.fire = MakeAction("Fire", 0.05f, 0f, false);
            item.cycle = MakeAction("Cycle mechanism", 0.7f, 0.7f);
            item.reload = MakeAction("Reload", 2f, 0.7f);
            item.reloadEmpty = MakeAction("Reload empty", 2.5f, 0.8f);
            return item;
        }

        public static MeleeDefinition CreateMelee(bool fists)
        {
            MeleeDefinition item = ScriptableObject.CreateInstance<MeleeDefinition>();
            item.name = item.displayName = item.equipmentId = fists ? "Fists" : "Knife";
            item.isUnarmed = fists;
            item.canAim = false;
            item.leftGripWeight = item.rightGripWeight = 0f;
            item.equip = MakeAction("Equip", 0.35f, 1f);
            item.unequip = MakeAction("Unequip", 0.3f, 1f);
            item.attacks = fists
                ? new[] { MakeAction("Left punch", 0.45f, 0.4f), MakeAction("Right punch", 0.45f, 0.4f) }
                : new[] { MakeAction("Slash", 0.5f, 0.4f) };
            return item;
        }

        public static EquipmentActionDefinition MakeAction(string name, float duration, float commit, bool cancellable = true)
        {
            EquipmentActionDefinition action = ScriptableObject.CreateInstance<EquipmentActionDefinition>();
            action.name = name;
            action.duration = duration;
            action.commitTime = commit;
            action.canCancel = cancellable;
            return action;
        }

        private static void Save(EquipmentDefinition item)
        {
            string path = EditorUtility.SaveFilePanelInProject("Create equipment definition", item.name, "asset",
                "Choose where to save the equipment and its own action subassets.");
            if (string.IsNullOrEmpty(path))
            {
                DestroyTemporary(item);
                return;
            }
            AssetDatabase.CreateAsset(item, path);
            foreach (EquipmentActionDefinition action in Actions(item))
                if (action != null) AssetDatabase.AddObjectToAsset(action, item);
            EditorUtility.SetDirty(item);
            AssetDatabase.SaveAssets();
            Selection.activeObject = item;
            EditorGUIUtility.PingObject(item);
        }

        public static System.Collections.Generic.IEnumerable<EquipmentActionDefinition> Actions(EquipmentDefinition item)
        {
            yield return item.equip;
            yield return item.unequip;
            if (item is FirearmDefinition firearm)
            {
                yield return firearm.fire;
                yield return firearm.cycle;
                yield return firearm.reload;
                yield return firearm.reloadEmpty;
                yield return firearm.insertRound;
            }
            if (item is MeleeDefinition melee && melee.attacks != null)
                foreach (EquipmentActionDefinition action in melee.attacks) yield return action;
        }

        public static void DestroyTemporary(EquipmentDefinition item)
        {
            foreach (EquipmentActionDefinition action in Actions(item))
                if (action != null && !EditorUtility.IsPersistent(action)) Object.DestroyImmediate(action);
            if (!EditorUtility.IsPersistent(item)) Object.DestroyImmediate(item);
        }
    }
}
#endif
