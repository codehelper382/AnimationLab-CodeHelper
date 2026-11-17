using System.Collections.Generic;

namespace CodeHelper.ProceduralShooterFramework.Equipment
{
    /// <summary>
    /// Explicitly ticked selection/action coordinator. Does not instantiate prefabs, play clips, write bones or read input.
    /// Keep the same EquipmentInstance when reselecting an item to preserve its ammo/cycle state.
    /// </summary>
    public sealed class EquipmentRuntime
    {
        private readonly HashSet<EquipmentInstance> _knownItems = new HashSet<EquipmentInstance>();
        public EquipmentInstance Current { get; private set; }
        public EquipmentInstance Pending { get; private set; }
        public EquipmentSwitchPhase Phase { get; private set; }
        public uint SelectionVersion { get; private set; }
        public bool IsReady => Phase == EquipmentSwitchPhase.Ready && Current != null && !Current.IsBusy;
        public bool CanAim => Phase == EquipmentSwitchPhase.Ready && Current != null && Current.CanAim;

        /// <summary>Null explicitly holsters the current item. Busy actions must finish or be cancelled first.</summary>
        public bool TrySelect(EquipmentInstance next)
        {
            if (Phase != EquipmentSwitchPhase.Ready || next == Current ||
                (Current != null && Current.IsBusy) || (next != null && next.IsBusy)) return false;
            Pending = next;
            if (next != null) _knownItems.Add(next);
            if (Current != null)
            {
                Phase = EquipmentSwitchPhase.Unequipping;
                Current.TryTransition(EquipmentActionKind.Unequip);
            }
            else SwapAndEquip();
            FinishImmediateTransitions();
            return true;
        }

        public bool TryFire() => IsReady && Current.TryFire();
        public bool TryCycle() => IsReady && Current.TryCycle();
        public bool TryReload() => IsReady && Current.TryReload();
        public bool TryMeleeAttack() => IsReady && Current.TryMeleeAttack();
        public bool CancelAction() => Phase == EquipmentSwitchPhase.Ready && Current != null && Current.CancelAction();

        public void Tick(float deltaTime)
        {
            float dt = EquipmentNumbers.NonNegative(deltaTime);
            // Inactive items retain their action state, but their firing/attack intervals still elapse.
            foreach (EquipmentInstance item in _knownItems) item.AdvanceCooldown(dt);
            Current?.AdvanceAction(dt);
            FinishImmediateTransitions();
        }

        /// <summary>For teardown. Leaves item ammo intact, cancels pending selection, and clears equipped references.</summary>
        public void Clear()
        {
            Current?.Interrupt();
            Current = Pending = null;
            _knownItems.Clear();
            Phase = EquipmentSwitchPhase.Ready;
            unchecked { SelectionVersion++; }
        }

        private void SwapAndEquip()
        {
            Current = Pending;
            Pending = null;
            unchecked { SelectionVersion++; }
            Phase = Current == null ? EquipmentSwitchPhase.Ready : EquipmentSwitchPhase.Equipping;
            Current?.TryTransition(EquipmentActionKind.Equip);
        }

        private void FinishImmediateTransitions()
        {
            if (Phase == EquipmentSwitchPhase.Unequipping && !Current.IsBusy)
            {
                float remaining = Current.Action.TimePastEnd;
                SwapAndEquip();
                Current?.AdvanceAction(remaining);
            }
            if (Phase == EquipmentSwitchPhase.Equipping && !Current.IsBusy) Phase = EquipmentSwitchPhase.Ready;
        }
    }
}
