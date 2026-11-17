using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using HandGripToolkit;
using UnityEditor;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    // Editor checkpoint, not a portable take. Never uses a bake or samples a preview candidate.
    internal static class LabEmergencyRecovery
    {
        [Serializable] internal sealed class Pose { public bool right; public int index; public string json; }
        [Serializable] internal sealed class Reference { public string role,guid; public long localId; }
        [Serializable] internal sealed class Contact { public HandSide side; public Vector3 position; public Quaternion rotation; }
        [Serializable] internal sealed class PendingState
        {
            public int track,followHands,fingerFrames;
            public float progress;
            public string objectId, fingerJson, fingerBaseJson, fingerLabel;
            public Vector3 position,euler;
            public bool wrist,weapon,item,fingers,includeGroupFingers,wristApproved,weaponApproved,objectApproved;
            public LabFingerBlend fingerBlend,groupBlend;
            public int groupFrames;
            public List<Contact> contacts=new List<Contact>();
        }
        [Serializable] internal sealed class Envelope
        {
            public int version=1;
            public string project,name,createdUtc,sourceGuid,lineage,takeJson,actionJson;
            public List<Pose> poses=new List<Pose>();
            public List<Reference> references=new List<Reference>();
            public List<LabAnchor> anchors=new List<LabAnchor>();
            public PendingState pending;
            public string reason;
        }
        internal static string Project => Path.GetFullPath(Application.dataPath);
        internal static string Folder => Path.GetFullPath(Path.Combine(Application.dataPath,"..","Library","AnimationLabRecovery"));
        private static string SessionKey => "AnimationLab.Emergency."+Project;
        private static readonly Dictionary<string,(DateTime modified,long length,Envelope data)> headers=new Dictionary<string,(DateTime,long,Envelope)>();
        private static string FallbackFolder
        {
            get
            {
                using(var hash=SHA256.Create())
                {
                    string id=BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(Project))).Replace("-","").Substring(0,16);
                    return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Temp","opencode","AnimationLabRecovery",id);
                }
            }
        }
        internal static Envelope Capture(AnimationLabTake take,string name,string sourceGuid,string lineage,PendingState pending)
        {
            var e=new Envelope {project=Project,name=name,sourceGuid=sourceGuid,lineage=lineage,
                createdUtc=DateTime.UtcNow.ToString("o"),takeJson=JsonUtility.ToJson(take),actionJson=take.action!=null?JsonUtility.ToJson(take.action):"{}",pending=pending};
            void Poses(List<LabHandKey> keys,bool right)
            {
                if(keys==null)return;
                for(int i=0;i<keys.Count;i++)if(keys[i]?.fingers!=null)e.poses.Add(new Pose{right=right,index=i,json=JsonUtility.ToJson(keys[i].fingers)});
            }
            Poses(take.left,false);Poses(take.right,true);
            void Ref(string role,UnityEngine.Object value)
            {
                if(value==null)return;
                if(!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value,out string guid,out long id))
                    throw new InvalidOperationException("La referencia "+role+" no es un asset persistente; guárdala antes de crear el respaldo.");
                e.references.Add(new Reference{role=role,guid=guid,localId=id});
            }
            Ref("sourceCharacter",take.sourceCharacterClip);Ref("sourceMask",take.sourceCharacterMask);Ref("sourceWeapon",take.sourceWeaponClip);
            if(take.action!=null)
            {
                Ref("character",take.action.characterClip);Ref("mask",take.action.characterMask);Ref("weapon",take.action.weaponClip);
                Ref("target",take.action.leftHandTargetMotion);Ref("leftMotion",take.action.leftHandMotion?.motionClip);Ref("rightMotion",take.action.rightHandMotion?.motionClip);
            }
            e.anchors=take.anchors?.Select(a=>a==null?null:new LabAnchor{path=a.path,role=a.role,position=a.position,rotation=a.rotation}).ToList()??new List<LabAnchor>();
            return e;
        }
        internal static string Fingerprint(Envelope e) => e.name+"|"+e.takeJson+e.actionJson+JsonUtility.ToJson(new FingerprintData{poses=e.poses,anchors=e.anchors,pending=e.pending});
        [Serializable] private sealed class FingerprintData { public List<Pose> poses; public List<LabAnchor> anchors;public PendingState pending; }
        internal static string Write(Envelope e,string reason)
        {
            e.reason=reason;
            string WriteAt(string folder)
            {
                Directory.CreateDirectory(folder);
                string path=Path.Combine(folder,DateTime.UtcNow.ToString("yyyyMMdd_HHmmss")+"_"+Guid.NewGuid().ToString("N")+".json");
                string temporary=path+".writing";
                File.WriteAllText(temporary,JsonUtility.ToJson(e,true),Encoding.UTF8);File.Move(temporary,path);
                return path;
            }
            try{return WriteAt(Folder);}
            catch(Exception primary) when(primary is IOException || primary is UnauthorizedAccessException)
            {
                try{return WriteAt(FallbackFolder);}
                catch(Exception fallback) when(fallback is IOException || fallback is UnauthorizedAccessException)
                {
                    SessionState.SetString(SessionKey,JsonUtility.ToJson(e));
                    Debug.LogWarning("[AnimationLab] No se pudo escribir en disco. Respaldo retenido SOLO en esta sesión de Unity; recupéralo y guarda cuanto antes.");
                    return "session:"+SessionKey;
                }
            }
        }
        internal static Envelope ReadHeader(string path)
        {
            var e=JsonUtility.FromJson<Envelope>(path.StartsWith("session:",StringComparison.Ordinal)
                ?SessionState.GetString(path.Substring(8),""):File.ReadAllText(path,Encoding.UTF8));
            if(e==null||e.version!=1||!string.Equals(e.project,Project,StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("El respaldo no corresponde a este proyecto o a una versión compatible.");
            return e;
        }
        internal static IEnumerable<(string path,Envelope data)> List()
        {
            string session=SessionState.GetString(SessionKey,"");
            if(!string.IsNullOrEmpty(session))
            {
                Envelope retained=null;try{retained=ReadHeader("session:"+SessionKey);}catch(Exception){ }
                if(retained!=null)yield return("session:"+SessionKey,retained);
            }
            foreach(string folder in new[]{Folder,FallbackFolder})
            {
                if(!Directory.Exists(folder))continue;
                foreach(string path in Directory.GetFiles(folder,"*.json").OrderByDescending(File.GetLastWriteTimeUtc))
                {
                    Envelope e=null;
                    try
                    {
                        var file=new FileInfo(path);
                        if(headers.TryGetValue(path,out var cached)&&cached.modified==file.LastWriteTimeUtc&&cached.length==file.Length)e=cached.data;
                        else
                        {
                            var full=ReadHeader(path);
                            // The browser retains only metadata, not every dense take's payload.
                            e=new Envelope{project=full.project,name=full.name,createdUtc=full.createdUtc,lineage=full.lineage,reason=full.reason};
                            headers[path]=(file.LastWriteTimeUtc,file.Length,e);
                        }
                    }
                    catch(Exception){ }
                    if(e!=null)yield return(path,e);
                }
            }
        }
        internal static AnimationLabTake Restore(Envelope e)
        {
            var take=ScriptableObject.CreateInstance<AnimationLabTake>();
            bool ownsAction=false;
            try
            {
                JsonUtility.FromJsonOverwrite(e.takeJson,take);
                // Discard session instance IDs before allocating any owned replacement.
                take.action=null;
                foreach(var key in (take.left??new List<LabHandKey>()).Concat(take.right??new List<LabHandKey>()))if(key!=null)key.fingers=null;
                take.action=ScriptableObject.CreateInstance<global::CodeHelper.ProceduralShooterFramework.Equipment.EquipmentActionDefinition>();ownsAction=true;
                JsonUtility.FromJsonOverwrite(e.actionJson,take.action);
                take.sourceCharacterClip=null;take.sourceCharacterMask=null;take.sourceWeaponClip=null;
                take.action.characterClip=null;take.action.characterMask=null;take.action.weaponClip=null;take.action.leftHandTargetMotion=null;
                if(take.action.leftHandMotion!=null)take.action.leftHandMotion.motionClip=null;
                if(take.action.rightHandMotion!=null)take.action.rightHandMotion.motionClip=null;
                foreach(var reference in e.references)
                {
                    var value=AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(reference.guid)).FirstOrDefault(a=>
                        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(a,out string guid,out long id)&&id==reference.localId);
                    if(value==null)throw new InvalidOperationException("Falta una dependencia del respaldo: "+reference.role+" ("+reference.guid+"). El JSON se conserva.");
                    switch(reference.role)
                    {
                        case "sourceCharacter":take.sourceCharacterClip=(AnimationClip)value;break;
                        case "sourceMask":take.sourceCharacterMask=(AvatarMask)value;break;
                        case "sourceWeapon":take.sourceWeaponClip=(AnimationClip)value;break;
                        case "character":take.action.characterClip=(AnimationClip)value;break;
                        case "mask":take.action.characterMask=(AvatarMask)value;break;
                        case "weapon":take.action.weaponClip=(AnimationClip)value;break;
                        case "target":take.action.leftHandTargetMotion=(AnimationClip)value;break;
                        case "leftMotion":take.action.leftHandMotion.motionClip=(AnimationClip)value;break;
                        case "rightMotion":take.action.rightHandMotion.motionClip=(AnimationClip)value;break;
                    }
                }
                foreach(var pose in e.poses)
                {
                    var keys=pose.right?take.right:take.left;
                    if(keys==null||pose.index<0||pose.index>=keys.Count||keys[pose.index]==null)continue;
                    var data=ScriptableObject.CreateInstance<HandPoseData>();JsonUtility.FromJsonOverwrite(pose.json,data);data.hideFlags=HideFlags.HideAndDontSave;
                    keys[pose.index].fingers=data;
                }
                take.anchors=e.anchors??new List<LabAnchor>();take.name=e.name;take.hideFlags=take.action.hideFlags=HideFlags.HideAndDontSave;
                return take;
            }
            catch
            {
                if(!ownsAction)
                {
                    take.action=null;
                    foreach(var key in (take.left??new List<LabHandKey>()).Concat(take.right??new List<LabHandKey>()))if(key!=null)key.fingers=null;
                }
                LabAssetIO.DestroyDraft(take);throw;
            }
        }
    }
}
