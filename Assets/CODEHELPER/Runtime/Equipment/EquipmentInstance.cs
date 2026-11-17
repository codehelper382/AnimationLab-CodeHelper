using System;
using UnityEngine;

namespace CodeHelper.ProceduralShooterFramework.Equipment
{
    /// <summary>Owned runtime state of ONE item. Two items may share the same immutable definition.</summary>
    public sealed class EquipmentInstance
    {
        public EquipmentDefinition Definition { get; }
        public EquipmentActionState Action { get; } = new EquipmentActionState();
        public int MagazineRounds { get; private set; }
        public int ReserveRounds { get; private set; }
        public bool ChamberLoaded { get; private set; }
        public bool NeedsCycle { get; private set; }
        public int ShotCount { get; private set; }
        public int MeleeContactCount { get; private set; }
        public int TotalLoadedRounds => MagazineRounds + (ChamberLoaded ? 1 : 0);
        public bool IsBusy => Action.IsActive;
        public bool CanAim => (!IsBusy || Action.Kind == EquipmentActionKind.Fire) && Definition.canAim;
        public int MagazineCapacity => _capacity;

        private readonly FirearmDefinition _firearm;
        private readonly MeleeDefinition _melee;
        private readonly int _capacity;
        private readonly FirearmMechanism _mechanism;
        private readonly ReloadMethod _reloadMethod;
        private readonly bool _reloadChambers;
        private readonly float _shotInterval, _attackInterval;
        private double _cooldown;
        private int _attackIndex;

        public EquipmentInstance(EquipmentDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            Definition = definition;
            _firearm = definition as FirearmDefinition;
            _melee = definition as MeleeDefinition;
            if (_firearm != null)
            {
                _capacity = Mathf.Max(1, _firearm.magazineCapacity);
                MagazineRounds = Mathf.Clamp(_firearm.initialMagazineRounds, 0, _capacity);
                ReserveRounds = Mathf.Max(0, _firearm.initialReserveRounds);
                ChamberLoaded = _firearm.initiallyChambered;
                _mechanism = _firearm.mechanism;
                _reloadMethod = _firearm.reloadMethod;
                _reloadChambers = _firearm.reloadChambersRound;
                float rpm = _firearm.roundsPerMinute;
                _shotInterval = 60f / (EquipmentNumbers.Finite(rpm) ? Mathf.Max(1f, rpm) : 600f);
            }
            if (_melee != null) _attackInterval = EquipmentNumbers.NonNegative(_melee.attackInterval);
        }

        public bool TryFire()
        {
            if (_firearm == null || IsBusy || _cooldown > 0 || NeedsCycle || !ChamberLoaded) return false;
            Begin(EquipmentActionKind.Fire, _firearm.fire);
            return true;
        }

        public bool TryCycle()
        {
            if (_firearm == null || IsBusy || (!NeedsCycle && ChamberLoaded)) return false;
            if (!NeedsCycle && MagazineRounds == 0) return false;
            Begin(EquipmentActionKind.Cycle, _firearm.cycle);
            return true;
        }

        public bool TryReload()
        {
            if (_firearm == null || IsBusy || ReserveRounds <= 0 || MagazineRounds >= _capacity) return false;
            EquipmentActionDefinition action = _reloadMethod == ReloadMethod.IndividualRounds && _firearm.insertRound != null
                ? _firearm.insertRound : !ChamberLoaded && _firearm.reloadEmpty != null
                    ? _firearm.reloadEmpty : _firearm.reload;
            Begin(EquipmentActionKind.Reload, action);
            return true;
        }

        public bool TryMeleeAttack()
        {
            if (_melee == null || IsBusy || _cooldown > 0 || _melee.attacks == null || _melee.attacks.Length == 0)
                return false;
            EquipmentActionDefinition action = _melee.attacks[_attackIndex % _melee.attacks.Length];
            if (action == null) return false;
            // Advance the combination only on actual contact, not on a cancelled wind-up.
            Begin(EquipmentActionKind.MeleeAttack, action);
            return true;
        }

        public bool CancelAction() => Action.Cancel(false);

        /// <summary>Aborts presentation on disable/removal, including non-cancellable actions. Never undoes a commit.</summary>
        public void Interrupt() => Action.Cancel(true);

        public void Tick(float deltaTime)
        {
            float dt = EquipmentNumbers.NonNegative(deltaTime);
            AdvanceCooldown(dt);
            AdvanceAction(dt);
        }

        internal void AdvanceCooldown(float deltaTime) => _cooldown = Math.Max(0, _cooldown - deltaTime);

        internal bool TryTransition(EquipmentActionKind kind)
        {
            if (IsBusy || (kind != EquipmentActionKind.Equip && kind != EquipmentActionKind.Unequip)) return false;
            Begin(kind, kind == EquipmentActionKind.Equip ? Definition.equip : Definition.unequip);
            return true;
        }

        private void Begin(EquipmentActionKind kind, EquipmentActionDefinition definition)
        {
            Action.Start(kind, definition);
            AdvanceAction(0f); // Handles milestone 0 and missing/zero-duration actions immediately.
        }

        internal void AdvanceAction(float dt)
        {
            if (!Action.Advance(dt)) return;
            switch (Action.Kind)
            {
                case EquipmentActionKind.Fire:
                    ChamberLoaded = false;
                    ShotCount++;
                    NeedsCycle = _mechanism != FirearmMechanism.SelfLoading;
                    if (!NeedsCycle) FeedChamber();
                    _cooldown = Math.Max(0, _shotInterval - Action.TimePastCommit);
                    break;
                case EquipmentActionKind.Cycle:
                    NeedsCycle = false;
                    FeedChamber();
                    break;
                case EquipmentActionKind.Reload:
                    int amount = Math.Min(ReserveRounds, _capacity - MagazineRounds);
                    if (_reloadMethod == ReloadMethod.IndividualRounds) amount = Math.Min(1, amount);
                    MagazineRounds += amount;
                    ReserveRounds -= amount;
                    if (_reloadChambers)
                    {
                        NeedsCycle = false;
                        FeedChamber();
                    }
                    break;
                case EquipmentActionKind.MeleeAttack:
                    MeleeContactCount++;
                    _attackIndex = _melee.attacks == null || _melee.attacks.Length == 0
                        ? 0 : (_attackIndex + 1) % _melee.attacks.Length;
                    _cooldown = Math.Max(0, _attackInterval - Action.TimePastCommit);
                    break;
            }
        }

        private void FeedChamber()
        {
            if (ChamberLoaded || MagazineRounds <= 0) return;
            MagazineRounds--;
            ChamberLoaded = true;
        }
    }
}
