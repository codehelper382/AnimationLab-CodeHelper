using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    // Rollback preserves existing key and finger identities (Undo/UI selections may hold them).
    internal sealed class LabAuthoringTransaction : IDisposable
    {
        private readonly AnimationLabTake take, backup;
        private readonly List<LabHandKey> left, right;
        private readonly List<LabWeaponKey> weapon;
        private readonly List<(List<LabWeaponKey> list, LabWeaponKey[] keys)> objects = new List<(List<LabWeaponKey>,LabWeaponKey[])>();
        private readonly List<Action> restore = new List<Action>();
        private bool committed;
        internal LabAuthoringTransaction(AnimationLabTake source)
        {
            if(source==null||source.action==null||AssetDatabase.Contains(source))throw new InvalidOperationException("La transacción necesita un borrador independiente.");
            take=source;backup=LabAssetIO.Clone(source);
            left=source.left.ToList();right=source.right.ToList();weapon=source.weapon.ToList();
            foreach(var key in left.Concat(right))
            {
                var target=key;string json=JsonUtility.ToJson(key);var fingers=key.fingers;
                restore.Add(()=>{JsonUtility.FromJsonOverwrite(json,target);target.fingers=fingers;});
            }
            foreach(var list in source.parts.Select(p=>p.keys).Concat(source.props.Select(p=>p.keys)).Prepend(source.weapon))
            {
                objects.Add((list,list.ToArray()));
                foreach(var key in list)
                {var target=key;string json=JsonUtility.ToJson(key);restore.Add(()=>JsonUtility.FromJsonOverwrite(json,target));}
            }
        }
        internal void Commit() => committed=true;
        public void Dispose()
        {
            if(!committed&&take!=null)
            {
                foreach(var action in restore)action();
                take.left=left;take.right=right;take.weapon=weapon;
                foreach(var entry in objects){entry.list.Clear();entry.list.AddRange(entry.keys);}
                EditorUtility.CopySerialized(backup.action,take.action);
                take.weaponDynamics=JsonUtility.FromJson<LabWeaponDynamicsRecipe>(JsonUtility.ToJson(backup.weaponDynamics));
                take.leftContactMotion=backup.leftContactMotion;take.rightContactMotion=backup.rightContactMotion;
                take.guidedReload=LabGuidedReloadPlan.Clone(backup.guidedReload);
            }
            LabAssetIO.DestroyDraft(backup);
        }
    }
}
