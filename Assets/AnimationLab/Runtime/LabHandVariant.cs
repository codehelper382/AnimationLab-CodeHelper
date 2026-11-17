using UnityEngine;

namespace CodeHelper.AnimationLab
{
    // Authoring recipe only: no scene objects, clips, editor types or automatic gameplay assignment.
    public sealed class LabHandVariant : ScriptableObject
    {
        public string description;
        public string equipmentId;
        public LabHandInsert settings = new LabHandInsert();
    }
}
