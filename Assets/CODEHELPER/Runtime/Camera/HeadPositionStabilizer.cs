using CodeHelper.ProceduralShooterFramework.Animation;
using UnityEngine;

/// <summary>
/// Evaluates an independent eye position from the current skeletal segments without writing bones.
/// Each segment keeps its current length/local displacement. Body posture and procedural roll/pitch
/// are separated using spine phase snapshots, then transported into the view heading.
/// This follows pitch/posture, not a root-locked eye height.
/// </summary>
public sealed class HeadPositionStabilizer
{
    private struct Link
    {
        public Transform parent, child;
        public Quaternion reference, inverseReference;
    }

    private Link[] _links;
    private Transform _root, _head;
    private ProceduralSpine _spine;
    private int _referenceGeneration;

    public void Reset()
    {
        _root = null;
        _head = null;
        _spine = null;
        // Keep storage until a new calibration is needed; no allocations during normal evaluation.
    }

    public bool TryGetPosition(Transform root, Transform head, ProceduralSpine spine, float viewYaw,
        out Vector3 position)
    {
        position = head != null ? head.position : Vector3.zero;
        if (root == null || head == null || head == root || !head.IsChildOf(root)) return false;
        if (spine != null && !spine.IsReady)
        {
            Reset(); // Also rebuild neutral references after a profile/bind-pose reinitialization.
            return true;
        }
        int generation = spine != null ? spine.ReferenceGeneration : 0;
        if (_root != root || _head != head || _spine != spine || _referenceGeneration != generation || !HasSameHierarchy())
            Capture(root, head, spine);

        Quaternion bodyHeading = Quaternion.AngleAxis(root.eulerAngles.y, Vector3.up);
        Quaternion inverseBodyHeading = Quaternion.Inverse(bodyHeading);
        Quaternion viewHeading = Quaternion.AngleAxis(viewYaw, Vector3.up);
        Quaternion headingCorrection = viewHeading * inverseBodyHeading;
        Quaternion inverseHeadingCorrection = Quaternion.Inverse(headingCorrection);
        position = root.position;
        Quaternion previousBase = root.rotation, previousRoll = root.rotation, previousPrePitch = root.rotation;
        float previousPitchHeading = viewYaw;
        for (int i = 0; i < _links.Length; i++)
        {
            Link link = _links[i];
            Quaternion current = link.parent.rotation;
            Quaternion basePose = i == 0 ? current : previousBase * link.parent.localRotation;
            Quaternion rollPose = i == 0 ? current : previousRoll * link.parent.localRotation;
            Quaternion prePitchPose = i == 0 ? current : previousPrePitch * link.parent.localRotation;
            float pitchHeading = previousPitchHeading;
            if (spine != null && spine.TryGetCameraBonePose(link.parent, out Quaternion sourceBase,
                    out Quaternion sourceRoll, out Quaternion sourcePrePitch, out float sourcePitchHeading))
            {
                basePose = sourceBase;
                rollPose = sourceRoll;
                prePitchPose = sourcePrePitch;
                pitchHeading = sourcePitchHeading;
            }
            previousBase = basePose;
            previousRoll = rollPose;
            previousPrePitch = prePitchPose;
            previousPitchHeading = pitchHeading;

            // Body posture (including tilted hips/pelvis) is carried by the BODY heading.
            // Procedural pitch is already world/view oriented. Treating both as one world swing
            // would still orbit the camera when a tilted body turns.
            Quaternion bodyDelta = inverseBodyHeading * basePose * link.inverseReference;
            Quaternion virtualBase = viewHeading * RemoveYawTwist(bodyDelta) * link.reference;
            Quaternion rollDelta = rollPose * Quaternion.Inverse(basePose);
            Quaternion virtualRoll = headingCorrection * rollDelta * inverseHeadingCorrection;
            Quaternion pitchDelta = current * Quaternion.Inverse(prePitchPose);
            Quaternion pitchCorrection = Quaternion.AngleAxis(Mathf.DeltaAngle(pitchHeading, viewYaw), Vector3.up);
            Quaternion virtualPitch = pitchCorrection * pitchDelta * Quaternion.Inverse(pitchCorrection);
            Quaternion virtualRotation = virtualPitch * virtualRoll * virtualBase;
            Vector3 localSegment = Quaternion.Inverse(current) * (link.child.position - link.parent.position);
            position += virtualRotation * localSegment;
        }
        return true;
    }

    private static Quaternion RemoveYawTwist(Quaternion delta)
    {
        float norm = delta.y * delta.y + delta.w * delta.w;
        if (norm <= 1e-10f) return delta;
        float invLength = 1f / Mathf.Sqrt(norm);
        return delta * new Quaternion(0f, -delta.y * invLength, 0f, delta.w * invLength);
    }

    private bool HasSameHierarchy()
    {
        if (_links == null) return false;
        for (int i = 0; i < _links.Length; i++)
            if (_links[i].child == null || _links[i].parent == null || _links[i].child.parent != _links[i].parent)
                return false;
        return true;
    }

    private void Capture(Transform root, Transform head, ProceduralSpine spine)
    {
        int count = 0;
        for (Transform node = head; node != root; node = node.parent) count++;
        if (_links == null || _links.Length != count) _links = new Link[count];
        Transform child = head;
        for (int i = count - 1; i >= 0; i--)
        {
            _links[i].child = child;
            _links[i].parent = child.parent;
            child = child.parent;
        }
        Quaternion previousReference = root.rotation;
        Quaternion inverseRootHeading = Quaternion.Inverse(Quaternion.AngleAxis(root.eulerAngles.y, Vector3.up));
        for (int i = 0; i < count; i++)
        {
            Transform parent = _links[i].parent;
            Quaternion reference = i == 0 ? root.rotation : previousReference * parent.localRotation;
            // Capture a neutral procedural reference, not a frame that may already contain look pitch.
            // Intermediary unmapped bones retain their local bind/animated orientation.
            if (spine != null && spine.TryGetReferenceWorldRotation(parent, out Quaternion neutral)) reference = neutral;
            _links[i].reference = inverseRootHeading * reference;
            _links[i].inverseReference = Quaternion.Inverse(_links[i].reference);
            previousReference = reference;
        }
        _referenceGeneration = spine != null ? spine.ReferenceGeneration : 0;
        _root = root;
        _head = head;
        _spine = spine;
    }
}
