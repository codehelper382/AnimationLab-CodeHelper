#if UNITY_EDITOR
using System;
using CodeHelper.ProceduralShooterFramework.Equipment;
using CodeHelper.ProceduralShooterFramework.EditorTools;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class EquipmentChecks
{
    [MenuItem("Tools/CodeHelper/Run Equipment Checks")]
    public static void Run()
    {
        CheckSelfLoading();
        CheckManual(FirearmMechanism.ManualBolt);
        CheckManual(FirearmMechanism.Pump);
        CheckIndividualReload();
        CheckMeleeAndSwitching();
        CheckInvalidDataAndTiming();
        CheckHolsteredCooldownAndSwitchTiming();
        CheckTemplatesPersist();
        Debug.Log("[EquipmentChecks] Passed: shared definitions/independent state, chamber and magazine accounting, " +
            "self-loading/bolt/pump, reload/cycle/contact milestones, cancellation before/after commit, one commit per action, " +
            "single-shell insertion, empty reload selection, melee/fists, ordered switching/holster, zero/large/invalid steps, " +
            "hand curves and template persistence. Clip playback, visual swaps, input and hit detection are not implemented by this data layer.");
    }

    private static void CheckSelfLoading()
    {
        FirearmDefinition definition = EquipmentTemplates.CreateFirearm("Pistol check", FirearmMechanism.SelfLoading, 3, 600f);
        try
        {
            definition.initialMagazineRounds = 2;
            definition.initialReserveRounds = 7;
            definition.fire.duration = 0.2f;
            definition.fire.commitTime = 0.5f;
            definition.fire.canCancel = true;
            string original = JsonUtility.ToJson(definition);
            var a = new EquipmentInstance(definition);
            var b = new EquipmentInstance(definition);
            Require(a.TryFire() && !a.TryFire(), "fire action blocks duplicate start");
            Require(a.CanAim, "fire action does not force ADS off");
            a.Tick(0.05f);
            Require(a.TotalLoadedRounds == 3 && a.ShotCount == 0, "no shot before milestone");
            Require(a.CancelAction() && a.TotalLoadedRounds == 3, "cancel wind-up consumes no round");
            Require(a.TryFire(), "retry cancelled shot");
            a.Tick(0.11f);
            Require(a.ShotCount == 1 && a.ChamberLoaded && a.MagazineRounds == 1 && !a.NeedsCycle,
                "self-loading consumes chamber then feeds next round");
            uint commit = a.Action.CommitVersion;
            Require(a.CancelAction() && !a.TryFire(), "cancel after shot keeps ammo debit and cadence");
            a.Tick(0.2f);
            Require(a.Action.CommitVersion == commit && a.TryFire(), "cadence recovers without repeat commit");
            a.Tick(1f);
            Require(a.ShotCount == 2 && a.ChamberLoaded && a.MagazineRounds == 0, "last magazine round is chambered");
            Require(a.TryFire(), "can shoot chamber with empty magazine");
            a.Tick(1f);
            Require(a.TotalLoadedRounds == 0 && !a.TryFire(), "empty gun cannot fire");
            Require(b.TotalLoadedRounds == 3 && b.ReserveRounds == 7 && b.ShotCount == 0, "shared asset does not share ammo");
            Require(a.TryReload() && a.Action.Definition == definition.reloadEmpty, "empty reload chosen from chamber state");
            a.Tick(3f);
            Require(a.ChamberLoaded && a.MagazineRounds == 2 && a.ReserveRounds == 4, "reload with bolt release feeds chamber");
            Require(a.TryReload(), "tactical top-up supports magazine plus one");
            a.Tick(3f);
            Require(a.MagazineRounds == 3 && a.ChamberLoaded && a.ReserveRounds == 3 && !a.TryReload(),
                "full magazine plus chamber without double transfer");
            int reserve = a.ReserveRounds;
            a.Tick(100f);
            Require(a.ReserveRounds == reserve, "completed reload cannot repeat");
            Require(JsonUtility.ToJson(definition) == original, "runtime never modifies definition");
        }
        finally { EquipmentTemplates.DestroyTemporary(definition); }
    }

    private static void CheckManual(FirearmMechanism mechanism)
    {
        FirearmDefinition definition = EquipmentTemplates.CreateFirearm("Manual check", mechanism, 3, 600f);
        try
        {
            var item = new EquipmentInstance(definition);
            Require(item.TryFire() && item.NeedsCycle && !item.ChamberLoaded && item.MagazineRounds == 2,
                "manual shot leaves mechanism pending, not a new live round");
            item.Tick(1f);
            Require(!item.TryFire(), "manual weapon cannot shoot twice without cycling");
            Require(item.TryCycle(), "cycle starts independently from reload");
            item.Tick(0.2f);
            Require(item.NeedsCycle && item.CancelAction(), "cancel before cycle commit keeps pending mechanism");
            Require(item.TryCycle(), "retry cycle");
            item.Tick(0.6f);
            Require(!item.NeedsCycle && item.ChamberLoaded && item.MagazineRounds == 1, "cycle feeds exactly one round");
            Require(item.CancelAction() && !item.TryCycle(), "cancel after cycle cannot feed another round");
            item.Tick(1f);
            Require(item.TryFire(), "manual weapon ready after completed mechanism");
            item.Tick(1f);
            int total = item.TotalLoadedRounds + item.ReserveRounds;
            Require(item.TryReload(), "reload is allowed while manual mechanism is pending");
            item.Tick(3f);
            Require(item.NeedsCycle && !item.ChamberLoaded && item.MagazineRounds == 3,
                "loading ammo alone does not magically operate bolt/pump");
            Require(item.TotalLoadedRounds + item.ReserveRounds == total, "reload conserves ammo");
            Require(item.TryCycle(), "cycle after reload");
            item.Tick(1f);
            Require(item.ChamberLoaded && item.MagazineRounds == 2, "post-reload cycle works");
        }
        finally { EquipmentTemplates.DestroyTemporary(definition); }
    }

    private static void CheckIndividualReload()
    {
        FirearmDefinition definition = EquipmentTemplates.CreateFirearm("Shell check", FirearmMechanism.Pump, 3, 90f);
        definition.reloadMethod = ReloadMethod.IndividualRounds;
        definition.insertRound = EquipmentTemplates.MakeAction("Insert check", 1f, 0.5f);
        definition.initialMagazineRounds = 0;
        definition.initiallyChambered = false;
        definition.initialReserveRounds = 2;
        try
        {
            var item = new EquipmentInstance(definition);
            Require(item.TryReload() && item.Action.Definition == definition.insertRound, "insertion action selected");
            item.Tick(0.49f);
            Require(item.CancelAction() && item.MagazineRounds == 0 && item.ReserveRounds == 2, "cancel shell before insert");
            Require(item.TryReload(), "restart insert");
            item.Tick(0.6f);
            Require(item.CancelAction() && item.MagazineRounds == 1 && item.ReserveRounds == 1,
                "inserted shell stays on cancellation");
            Require(item.TryReload(), "next shell");
            item.Tick(5f);
            Require(item.MagazineRounds == 2 && item.ReserveRounds == 0 && !item.ChamberLoaded && !item.TryReload(),
                "each action inserts one shell only, no extra reserve or automatic pump");
            Require(item.TryCycle(), "initial chambering shares cycle action");
            item.Tick(1f);
            Require(item.MagazineRounds == 1 && item.ChamberLoaded, "chambering does not create ammo");
        }
        finally { EquipmentTemplates.DestroyTemporary(definition); }
    }

    private static void CheckMeleeAndSwitching()
    {
        MeleeDefinition fists = EquipmentTemplates.CreateMelee(true);
        FirearmDefinition gun = EquipmentTemplates.CreateFirearm("Switch check", FirearmMechanism.ManualBolt, 3, 600f);
        try
        {
            var hands = new EquipmentInstance(fists);
            var rifle = new EquipmentInstance(gun);
            var runtime = new EquipmentRuntime();
            Require(fists.visualPrefab == null && fists.leftGripWeight == 0f && fists.rightGripWeight == 0f,
                "fists template needs no weapon or firearm grips");
            Require(runtime.TrySelect(hands) && runtime.Phase == EquipmentSwitchPhase.Equipping && !runtime.CanAim,
                "initial equip blocks interaction");
            Require(!runtime.TryMeleeAttack() && !runtime.TrySelect(rifle), "no actions or selection spam during equip");
            runtime.Tick(1f);
            Require(runtime.IsReady && !runtime.CanAim && !runtime.TryFire() && !runtime.TryReload(), "fists are melee only");
            Require(runtime.TryMeleeAttack() && hands.Action.Definition == fists.attacks[0], "left punch first");
            runtime.Tick(0.05f);
            Require(runtime.CancelAction() && hands.MeleeContactCount == 0, "cancel before contact");
            Require(runtime.TryMeleeAttack() && hands.Action.Definition == fists.attacks[0], "cancelled punch doesn't advance combination");
            runtime.Tick(1f);
            Require(hands.MeleeContactCount == 1 && runtime.TryMeleeAttack() && hands.Action.Definition == fists.attacks[1],
                "next attack uses right punch, no firearm fields needed");
            Require(!runtime.TrySelect(rifle), "attack must end or cancel before switching");
            runtime.Tick(1f);
            Require(runtime.TrySelect(rifle) && runtime.Current == hands && runtime.Pending == rifle,
                "outgoing item remains selected during unequip");
            uint version = runtime.SelectionVersion;
            Require(!runtime.CancelAction(), "switch cannot be cancelled by attack cancellation");
            runtime.Tick(0.31f);
            Require(runtime.Current == rifle && runtime.Phase == EquipmentSwitchPhase.Equipping &&
                runtime.SelectionVersion == version + 1 && !runtime.TryFire(), "swap only after unequip, then new equip");
            runtime.Tick(1f);
            Require(runtime.CanAim && runtime.TryFire(), "firearm becomes usable after equip");
            runtime.Tick(1f);
            Require(rifle.NeedsCycle && runtime.TrySelect(hands), "manual state can be holstered");
            runtime.Tick(1f);
            runtime.Tick(1f);
            Require(runtime.TrySelect(rifle), "reselect same gun instance");
            runtime.Tick(1f);
            runtime.Tick(1f);
            Require(rifle.NeedsCycle && rifle.ShotCount == 1 && !runtime.TryFire(), "switching preserves ammo and pending mechanism");
            Require(runtime.TrySelect(null), "explicit holster");
            runtime.Tick(1f);
            Require(runtime.Current == null && !runtime.IsReady && runtime.Phase == EquipmentSwitchPhase.Ready,
                "holster has no imaginary fists or weapon");
            Require(runtime.TrySelect(hands), "can select again after holster");
            runtime.Clear();
            Require(runtime.Current == null && runtime.Pending == null && !hands.IsBusy, "clear safely interrupts equipment presentation");
        }
        finally
        {
            EquipmentTemplates.DestroyTemporary(fists);
            EquipmentTemplates.DestroyTemporary(gun);
        }
    }

    private static void CheckInvalidDataAndTiming()
    {
        FirearmDefinition gun = EquipmentTemplates.CreateFirearm("Timing check", FirearmMechanism.SelfLoading, 3, 600f);
        try
        {
            gun.equip.duration = gun.unequip.duration = 0f;
            gun.initialMagazineRounds = 999;
            gun.initialReserveRounds = -10;
            gun.fire.duration = 0f;
            gun.fire.commitTime = 1f;
            var item = new EquipmentInstance(gun);
            var runtime = new EquipmentRuntime();
            Require(runtime.TrySelect(item) && runtime.IsReady, "zero-duration transitions end immediately");
            Require(item.MagazineRounds == 3 && item.ReserveRounds == 0, "invalid ammunition clamped without editing asset");
            Require(runtime.TryFire() && item.ShotCount == 1 && !runtime.TryFire(), "instant shot commits once and obeys cadence");
            runtime.Tick(float.NaN);
            runtime.Tick(float.PositiveInfinity);
            runtime.Tick(-1f);
            Require(!runtime.TryFire(), "invalid steps cannot bypass cooldown");
            runtime.Tick(1f);
            Require(runtime.TryFire(), "valid step advances cooldown");
            Require(runtime.TrySelect(null) && runtime.Current == null, "zero-duration holster immediate");
            gun.fire.duration = 1f;
            gun.fire.commitTime = 1f;
            gun.fire.canCancel = false;
            item = new EquipmentInstance(gun);
            Require(item.TryFire(), "delayed action");
            item.Tick(0.1f);
            Require(!item.CancelAction() && item.IsBusy, "non-cancellable action enforced");
            gun.fire.duration = 0f;
            gun.fire.commitTime = 0f;
            item.Tick(0.1f);
            Require(item.ShotCount == 0 && item.IsBusy, "active timing captured, not changed by editing shared asset");
            item.Interrupt();
            Require(!item.IsBusy && item.ShotCount == 0, "teardown can interrupt without phantom shot");
            EquipmentActionDefinition curve = gun.cycle;
            curve.leftGrip = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            curve.leftIK = AnimationCurve.Constant(0f, 1f, 0.8f);
            EquipmentHandSample sample = curve.SampleHands(0.5f);
            Require(Mathf.Abs(sample.leftGrip - 0.5f) < 0.0001f && Mathf.Abs(sample.leftIK - 0.8f) < 0.0001f,
                "grip attachment and IK influence are distinct channels");
            curve.rightGrip = null;
            curve.weaponPosition.x = AnimationCurve.Linear(0f, 0f, 1f, 0.1f);
            Require(curve.SampleHands(float.NaN).rightGrip == 1f && curve.weaponPosition.Evaluate(2f).x == 0.1f,
                "curve sampling clamps progress and handles missing data");
            foreach (float step in new[] { 1f / 30f, 1f / 60f, 1f / 144f })
            {
                gun.fire.duration = 0.18f;
                gun.fire.commitTime = 0.4f;
                item = new EquipmentInstance(gun);
                Require(item.TryFire(), "frame rate shot begins");
                for (int i = 0; i < 200; i++) item.Tick(step);
                Require(item.ShotCount == 1 && item.Action.CommitVersion == 1 && !item.IsBusy,
                    "exactly one milestone at each tested frame rate");
            }
        }
        finally { EquipmentTemplates.DestroyTemporary(gun); }
    }

    private static void CheckHolsteredCooldownAndSwitchTiming()
    {
        FirearmDefinition gun = EquipmentTemplates.CreateFirearm("Slow check", FirearmMechanism.SelfLoading, 3, 6f);
        MeleeDefinition fists = EquipmentTemplates.CreateMelee(true);
        try
        {
            var rifle = new EquipmentInstance(gun);
            var hands = new EquipmentInstance(fists);
            var runtime = new EquipmentRuntime();
            Require(runtime.TrySelect(rifle), "equip slow gun");
            runtime.Tick(1f);
            Require(runtime.TryFire(), "slow gun shot");
            runtime.Tick(1f);
            Require(!runtime.TryFire(), "slow gun is cooling down");
            Require(runtime.TrySelect(hands), "holster cooling gun");
            runtime.Tick(1f);
            Require(runtime.Current == hands && runtime.IsReady, "large frame carries unequip time into equip");
            runtime.Tick(10f);
            Require(runtime.TrySelect(rifle), "return to cooled gun");
            runtime.Tick(1f);
            Require(runtime.IsReady && runtime.TryFire(), "holstered cooldown elapses without advancing inactive actions");
            runtime.Clear();

            foreach (float step in new[] { 1f, 1f / 30f, 1f / 60f, 1f / 144f })
            {
                runtime = new EquipmentRuntime();
                hands = new EquipmentInstance(fists);
                rifle = new EquipmentInstance(gun);
                Require(runtime.TrySelect(hands), "switch timing initial selection");
                runtime.Tick(1f);
                Require(runtime.TrySelect(rifle), "switch timing start");
                float elapsed = 0f;
                // 0.3 unequip + 0.35 equip must finish within 0.66 seconds regardless of partitioning.
                while (elapsed < 0.66f)
                {
                    float dt = Mathf.Min(step, 0.66f - elapsed);
                    runtime.Tick(dt);
                    elapsed += dt;
                }
                Require(runtime.Current == rifle && runtime.IsReady && runtime.SelectionVersion == 2,
                    "same switch deadline with single or partitioned steps");
                runtime.Clear();
            }
        }
        finally
        {
            EquipmentTemplates.DestroyTemporary(gun);
            EquipmentTemplates.DestroyTemporary(fists);
        }
    }

    private static void CheckTemplatesPersist()
    {
        string path = AssetDatabase.GenerateUniqueAssetPath("Assets/CodeHelperEquipmentCheck.asset");
        MeleeDefinition fists = EquipmentTemplates.CreateMelee(true);
        try
        {
            AssetDatabase.CreateAsset(fists, path);
            foreach (EquipmentActionDefinition action in EquipmentTemplates.Actions(fists))
                AssetDatabase.AddObjectToAsset(action, fists);
            EditorUtility.SetDirty(fists);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            MeleeDefinition saved = AssetDatabase.LoadAssetAtPath<MeleeDefinition>(path);
            Require(saved != null && saved.isUnarmed && saved.attacks.Length == 2 &&
                AssetDatabase.GetAssetPath(saved.attacks[0]) == path && saved.equip.duration > 0f,
                "template and its action subassets persist together");
        }
        finally
        {
            AssetDatabase.DeleteAsset(path);
            if (fists != null && !EditorUtility.IsPersistent(fists)) EquipmentTemplates.DestroyTemporary(fists);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception("[EquipmentChecks] " + message);
    }
}
#endif
