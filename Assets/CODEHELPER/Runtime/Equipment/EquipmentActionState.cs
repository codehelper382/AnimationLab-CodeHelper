namespace CodeHelper.ProceduralShooterFramework.Equipment
{
    /// <summary>Per-instance clock. Timing/cancellation are captured at Start; no AnimationEvents required.</summary>
    public sealed class EquipmentActionState
    {
        public EquipmentActionKind Kind { get; private set; }
        public EquipmentActionDefinition Definition { get; private set; }
        public bool IsActive { get; private set; }
        public bool IsCommitted { get; private set; }
        public float Progress { get; private set; }
        public uint Version { get; private set; }
        public uint CommitVersion { get; private set; }
        public EquipmentActionKind LastCommittedKind { get; private set; }
        public bool CanCancel => IsActive && _canCancel;
        internal double TimePastCommit { get; private set; }
        internal float TimePastEnd { get; private set; }
        private double _elapsed;
        private float _duration, _commitTime;
        private bool _canCancel;

        internal void Start(EquipmentActionKind kind, EquipmentActionDefinition definition)
        {
            Kind = kind;
            Definition = definition;
            _duration = definition == null ? 0f : EquipmentNumbers.NonNegative(definition.duration);
            _commitTime = definition == null ? 0f : EquipmentNumbers.Progress(definition.commitTime);
            _canCancel = definition == null || definition.canCancel;
            _elapsed = 0;
            Progress = 0f;
            IsActive = true;
            IsCommitted = false;
            TimePastCommit = 0;
            TimePastEnd = 0f;
            unchecked { Version++; }
        }

        // Returns true exactly once per action, even if a single step crosses both milestone and end.
        internal bool Advance(float deltaTime)
        {
            if (!IsActive) return false;
            _elapsed += EquipmentNumbers.NonNegative(deltaTime);
            Progress = _duration <= 0f ? 1f : (float)System.Math.Min(1.0, _elapsed / _duration);
            bool commit = !IsCommitted && Progress >= _commitTime;
            if (commit)
            {
                IsCommitted = true;
                TimePastCommit = System.Math.Max(0, _elapsed - (double)_duration * _commitTime);
                LastCommittedKind = Kind;
                unchecked { CommitVersion++; }
            }
            if (Progress >= 1f)
            {
                IsActive = false;
                TimePastEnd = (float)System.Math.Max(0, _elapsed - _duration);
            }
            return commit;
        }

        internal bool Cancel(bool force)
        {
            if (!IsActive || (!force && !_canCancel)) return false;
            IsActive = false;
            TimePastEnd = 0f;
            return true;
        }
    }
}
