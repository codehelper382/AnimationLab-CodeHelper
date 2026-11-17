using System;
using System.Collections.Generic;
using System.Linq;
using HandGripToolkit;
using UnityEditor;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    public sealed partial class AnimationLabWindow
    {
        private bool editObject, objectPending, worldSpace, objectReplacementApproved, includeGroupFingers = true;
        private string objectId, gripName = "Mi agarre", gripCategory = "Personalizado";
        private int followHands, gripIndex, shapeIndex;
        private readonly Dictionary<HandSide, (Vector3 p, Quaternion r)> followContacts = new Dictionary<HandSide, (Vector3, Quaternion)>();
        private bool navigating;
        private readonly HashSet<KeyCode> navigationKeys = new HashSet<KeyCode>();
        private LabProp SelectedProp => objectId != null && objectId.StartsWith("prop:") ? draft.props.FirstOrDefault(p => "prop:" + p.id == objectId) : null;
        private Transform ObjectTarget => objectId == null ? null : SelectedProp != null ? preview.Objects.ResolveProp(SelectedProp.id) :
            objectId.StartsWith("part:") ? preview.Objects.ResolvePart(objectId.Substring(5)) : null;

        private List<LabWeaponKey> ObjectKeys(bool create = false)
        {
            if (SelectedProp != null) return SelectedProp.keys;
            if (objectId == null || !objectId.StartsWith("part:")) return null;
            string path = objectId.Substring(5);
            var track = draft.parts.FirstOrDefault(t => t.path == path);
            if (track == null && create) { track = new LabObjectTrack { path = path }; draft.parts.Add(track); }
            return track?.keys;
        }

        private string[] ObjectIds()
        {
            var root = preview.Slot.visualRoot;
            var parts = root != null ? root.GetComponentsInChildren<Transform>(true).Where(t => t != root)
                .Select(t => AnimationUtility.CalculateTransformPath(t, root)).Where(p => !p.Contains("(runtime)"))
                .GroupBy(p => p).Where(g => g.Count() == 1).Select(g => "part:" + g.Key).OrderBy(p => p) : Enumerable.Empty<string>();
            return parts.Concat(draft.props.Select(p => "prop:" + p.id)).ToArray();
        }

        private string ObjectLabel(string id)
        {
            if (!id.StartsWith("part:")) return "Diseño · " + (draft.props.FirstOrDefault(p => "prop:" + p.id == id)?.name ?? "no encontrado");
            string path = id.Substring(5), leaf = path.Substring(path.LastIndexOf('/') + 1);
            string hint = leaf == "Mag" ? "Cargador" : leaf == "Charge" ? "Palanca de carga / cerrojo" : "Pieza";
            return hint + " · " + path;
        }

        private void DrawObjectEditor()
        {
            var ids = ObjectIds();
            string[] choices = new[] { "Selecciona cargador, cerrojo u objeto…" }.Concat(ids.Select(ObjectLabel)).ToArray();
            int current = Array.IndexOf(ids, objectId) + 1;
            int next = EditorGUILayout.Popup("Pieza / objeto", current, choices);
            if (next != current && next > 0) Safe(() => SelectObject(ids[next - 1]));
            if (ObjectTarget != null)
            {
                EditorGUI.BeginChangeCheck();
                int following = EditorGUILayout.Popup(new GUIContent("Mover junto con", "Guarda los contactos de esta pieza y mano. Dos puntos de conjunto crean un recorrido común, compilado a curvas normales."), followHands,
                    new[] { "Solo la pieza", "Mano izquierda", "Mano derecha", "Ambas manos" });
                bool fingersTogether = EditorGUILayout.Toggle("Incluir dedos en el conjunto", includeGroupFingers);
                if (EditorGUI.EndChangeCheck() && CanLeaveFrame()) { followHands = following; includeGroupFingers = fingersTogether; Safe(SyncSelection); }
                if(includeGroupFingers&&followHands!=0)
                {
                    groupFingerBlend=(LabFingerBlend)EditorGUILayout.Popup("Transición de dedos",(int)groupFingerBlend,new[]{"Original (compatible)","Lineal","Suave","Instantánea"});
                    using(new EditorGUI.DisabledScope(groupFingerBlend==LabFingerBlend.Step||groupFingerBlend==LabFingerBlend.Legacy))
                    {
                        int start=Mathf.Max(0,CurrentFrame-groupFingerFrames);
                        int nextStart=EditorGUILayout.DelayedIntField("Mantener dedos hasta F",start);
                        if(nextStart!=start)groupFingerFrames=CurrentFrame-Mathf.Clamp(nextStart,0,CurrentFrame);
                    }
                    EditorGUILayout.LabelField("Llegada del conjunto", $"F {CurrentFrame}",EditorStyles.boldLabel);
                    if(GUILayout.Button("Configurar agarre y ver su transición…"))Safe(()=>SelectTrack((int)FollowingSides().First()*2+1,progress));
                }
                DrawPositionEditor();
                EditorGUILayout.HelpBox("Las piezas necesitan un Transform/hueso independiente. Un cargador soldado a una única malla no se puede separar con este editor. El selector muestra rutas completas para no confundir piezas homónimas.", MessageType.None);
            }
            else EditorGUILayout.HelpBox("Selecciona una pieza del modelo. Si no existe como objeto/hueso separado, crea un diseño de referencia debajo. No se separan vértices de una malla automáticamente.", MessageType.Info);
            DrawPropBuilder();
        }

        private void SelectObject(string id)
        {
            if (!CanLeaveFrame()) return;
            objectId = id; objectReplacementApproved = false;
            editObject = true; editWeapon = false; playing = false;
            SyncSelection();
        }

        private void DrawPropBuilder()
        {
            EditorGUILayout.Space(); EditorGUILayout.LabelField("Diseñar un objeto con formas", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Nuevo objeto")) Safe(CreateProp);
                using (new EditorGUI.DisabledScope(SelectedProp == null))
                    if (GUILayout.Button("Guardar diseño como prefab…")) Safe(() =>
                    {
                        if (!CanLeaveFrame()) return;
                        string path = EditorUtility.SaveFilePanelInProject("Guardar diseño (sin lógica de juego)", SelectedProp.name, "prefab", "Guarda geometría estática. Las claves de la toma se conservan por separado.", LabAssetIO.Output);
                        if (!string.IsNullOrEmpty(path)) LabObjectAuthoring.ExportProp(SelectedProp, path);
                    });
            }
            var prop = SelectedProp;
            if (prop == null) return;
            if (GUILayout.Button(new GUIContent("Quitar este diseño de la toma", "Solo quita este objeto de referencia, no los puntos ya grabados de las manos ni prefabs guardados."))) Safe(() =>
            {
                if (!CanLeaveFrame()) return;
                if (!EditorUtility.DisplayDialog("Quitar diseño", "Se quitará este diseño de la toma. No borra los puntos de manos ni el prefab guardado. Puedes deshacer.", "Quitar", "Cancelar")) return;
                Undo.RecordObject(draft, "Quitar diseño"); draft.props.Remove(prop); objectId = null; dirty = true; SyncSelection();
            });
            if (SelectedProp == null) return;
            EditorGUI.BeginChangeCheck();
            string name = EditorGUILayout.TextField("Nombre del diseño", prop.name);
            if (EditorGUI.EndChangeCheck()) { Undo.RecordObject(draft, "Renombrar diseño"); prop.name = name; dirty = true; }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("+ Cubo")) AddShape(PrimitiveType.Cube);
                if (GUILayout.Button("+ Esfera")) AddShape(PrimitiveType.Sphere);
                if (GUILayout.Button("+ Cilindro")) AddShape(PrimitiveType.Cylinder);
            }
            if (prop.shapes.Count == 0) return;
            shapeIndex = EditorGUILayout.Popup("Forma a diseñar", Mathf.Clamp(shapeIndex, 0, prop.shapes.Count - 1), prop.shapes.Select((s, i) => (i + 1) + " · " + s.type).ToArray());
            var shape = prop.shapes[shapeIndex];
            EditorGUI.BeginChangeCheck();
            Vector3 p = EditorGUILayout.Vector3Field("Centro de la forma (cm)", shape.position * 100f) * 0.01f;
            Vector3 r = EditorGUILayout.Vector3Field("Giro de la forma", shape.euler);
            Vector3 size = EditorGUILayout.Vector3Field("Dimensiones completas (cm)", shape.size * 100f) * 0.01f;
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(draft, "Diseñar forma"); shape.position = p; shape.euler = r;
                shape.size = new Vector3(Mathf.Clamp(size.x, 0.001f, 2), Mathf.Clamp(size.y, 0.001f, 2), Mathf.Clamp(size.z, 0.001f, 2)); dirty = true;
            }
            if (GUILayout.Button("Quitar esta forma") && prop.shapes.Count > 1)
            { Undo.RecordObject(draft, "Quitar forma"); prop.shapes.RemoveAt(shapeIndex); dirty = true; }
            EditorGUILayout.HelpBox("Combina varias formas mirando el resultado en la vista. Dimensiones: ancho / alto / fondo; en cilindros: diámetro / altura / diámetro. El diseño se conserva en la toma. El prefab es geometría, no una nueva arma funcional.", MessageType.None);
        }

        private void CreateProp()
        {
            if (!CanLeaveFrame()) return;
            Undo.RecordObject(draft, "Crear objeto de referencia");
            var hand = preview.Animator.GetBoneTransform(side == HandSide.Left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            var holder = preview.Equipment.weaponPose.PoseRoot;
            var prop = new LabProp { id = Guid.NewGuid().ToString("N"), name = "Mi objeto " + (draft.props.Count + 1), position = holder.InverseTransformPoint(hand.position + hand.forward * 0.08f) };
            prop.shapes.Add(new LabPrimitive()); draft.props.Add(prop);
            objectId = "prop:" + prop.id; editObject = true; editWeapon = false; shapeIndex = 0; dirty = true;
            objectReplacementApproved = false; SyncSelection();
        }

        private void AddShape(PrimitiveType type)
        {
            if (SelectedProp == null || SelectedProp.shapes.Count >= 64) return;
            Undo.RecordObject(draft, "Añadir forma"); SelectedProp.shapes.Add(new LabPrimitive { type = type }); shapeIndex = SelectedProp.shapes.Count - 1; dirty = true;
        }

        private IEnumerable<HandSide> FollowingSides()
        {
            if (followHands == 1 || followHands == 3) yield return HandSide.Left;
            if (followHands == 2 || followHands == 3) yield return HandSide.Right;
        }

        private bool BeginObjectEdit(bool allowPrompt = true)
        {
            if (ObjectTarget == null) throw new InvalidOperationException("Selecciona primero una pieza u objeto.");
            if (objectPending) return true;
            bool hasSource = SelectedProp == null && (ObjectKeys()?.Count ?? 0) == 0 && draft.action.weaponClip != null &&
                AnimationUtility.GetCurveBindings(draft.action.weaponClip).Any(b => b.path == objectId.Substring(5) && b.type == typeof(Transform));
            if (hasSource && !objectReplacementApproved)
            {
                if (!allowPrompt) throw new InvalidOperationException("Falta confirmar la sustitución del movimiento de la pieza.");
                if (!EditorUtility.DisplayDialog("Esta pieza ya está animada", "Sus nuevos puntos reemplazarán posición y giro de esta pieza en el clip exportado, conservando las otras piezas. Crea puntos inicial/final si quieres mantener esos extremos.", "Crear puntos de la pieza", "Cancelar")) return false;
                objectReplacementApproved = true;
            }
            var oldSide = side;
            try
            {
                editObject = false;
                foreach (var hand in FollowingSides())
                {
                    side = hand;
                    if (!ApprovePositionReplacement(allowPrompt)) return false;
                }
            }
            finally { side = oldSide; editObject = true; }
            // A compiled wrist already compensates fades. Reuse its stored contact, or sample its
            // effective relation before removing fades; otherwise recapture applies compensation twice.
            var compiledContacts=new Dictionary<HandSide,LabPathContact>();
            var linked=LabCoordinatedPaths.Segments(draft).ToArray();
            if(linked.Length>0||draft.leftContactMotion||draft.rightContactMotion)
            {
                preview.Evaluate(draft,progress);var item=ObjectTarget;
                foreach(var hand in FollowingSides())
                {
                    var segment=LabCoordinatedPaths.At(draft,hand,progress);
                    if(segment!=null&&segment.objectId==objectId)compiledContacts[hand]=SegmentContact(segment,progress);
                    else if(linked.Any(s=>s.side==hand)||(hand==HandSide.Left?draft.leftContactMotion:draft.rightContactMotion))
                    {
                        var chain=hand==HandSide.Left?preview.Equipment.handIK.leftHand:preview.Equipment.handIK.rightHand;
                        var wrist=chain.target!=null?chain.target:chain.hand;
                        compiledContacts[hand]=new LabPathContact{active=true,position=item.InverseTransformPoint(wrist.position),euler=(Quaternion.Inverse(item.rotation)*wrist.rotation).eulerAngles};
                    }
                }
            }
            float incoming = draft.action.blendIn, outgoing = draft.action.blendOut;
            try { draft.action.blendIn = draft.action.blendOut = 0; preview.Evaluate(draft, progress, pitch, yaw, aim, rawFingers: true); }
            finally { draft.action.blendIn = incoming; draft.action.blendOut = outgoing; }
            followContacts.Clear();
            var target = ObjectTarget;
            foreach (var hand in FollowingSides())
            {
                var chain = hand == HandSide.Left ? preview.Equipment.handIK.leftHand : preview.Equipment.handIK.rightHand;
                var wrist = chain.target != null ? chain.target : chain.hand;
                followContacts[hand] = compiledContacts.TryGetValue(hand,out var contact)?(contact.position,Quaternion.Euler(contact.euler)):
                    (target.InverseTransformPoint(wrist.position), Quaternion.Inverse(target.rotation) * wrist.rotation);
                preview.EnableHand(hand);
            }
            objectPending = true; playing = false; return true;
        }

        private void ApplyObjectEdit()
        {
            var target = ObjectTarget;
            if (target == null) return;
            if (SelectedProp == null) preview.Objects.Touch(target);
            target.SetLocalPositionAndRotation(keyPosition, Quaternion.Euler(keyRotation));
            foreach (var contact in followContacts)
            {
                var chain = contact.Key == HandSide.Left ? preview.Equipment.handIK.leftHand : preview.Equipment.handIK.rightHand;
                if (chain.target != null) chain.target.SetPositionAndRotation(target.TransformPoint(contact.Value.p), target.rotation * contact.Value.r);
            }
        }

        private bool CommitObject(bool recordUnchanged, bool allowPrompt)
        {
            if (!objectPending && !recordUnchanged) return true;
            if(FollowingSides().Contains(HandSide.Right)&&draft.action.followAnimatedRightHand)
                throw new InvalidOperationException("Desactiva explícitamente Follow Animated Right Hand antes de combinar la derecha con una pieza.");
            if (!BeginObjectEdit(allowPrompt)) return false;
            EvaluateNow(true);
            var poses = new Dictionary<HandSide, HandPoseData>();
            var initialGrips=new Dictionary<HandSide,HandPoseData>();
            AnimationLabTake candidate=null;
            try
            {
                foreach (var hand in followContacts.Keys)
                    if (includeGroupFingers)
                    {
                        var pose = CreateInstance<HandPoseData>(); pose.Side = hand; pose.hideFlags = HideFlags.HideAndDontSave;
                        poses[hand] = pose; pose.Capture(preview.Animator); LabAssetIO.NormalizeFingerPose(pose, preview);
                        var oldSide=side;
                        try{side=hand;if(NeedsInitialGrip(groupFingerBlend))initialGrips[hand]=CaptureInitialGrip();}
                        finally{side=oldSide;}
                    }
                candidate=LabAssetIO.Clone(draft);
                var candidateObjectKeys=TakeObjectKeys(candidate,objectId,true);
                var existing=ObjectKeys()?.FirstOrDefault(k=>Mathf.Abs(k.time-progress)<LabTimeline.TimeEpsilon);
                var inherited=new LabWeaponKey{time=progress};if(existing==null)InheritObjectContacts(inherited);
                LabObjectAuthoring.Record(candidateObjectKeys, progress, keyPosition, keyRotation);
                var objectKey=candidateObjectKeys.First(k=>Mathf.Abs(k.time-progress)<LabTimeline.TimeEpsilon);
                if(existing==null){objectKey.leftContact=inherited.leftContact;objectKey.rightContact=inherited.rightContact;}
                var changedFingers=new HashSet<(HandSide,float)>();
                foreach (var hand in followContacts.Keys)
                {
                    var contact=followContacts[hand];
                    LabCoordinatedPaths.SetContact(objectKey,hand,new LabPathContact{active=true,position=contact.p,euler=contact.r.eulerAngles});
                    if(hand==HandSide.Left)objectKey.breakLeftContact=false;else objectKey.breakRightContact=false;
                    var keys = hand == HandSide.Left ? candidate.left : candidate.right;
                    var motion = hand == HandSide.Left ? candidate.action.leftHandMotion : candidate.action.rightHandMotion;
                    var chain = hand == HandSide.Left ? preview.Equipment.handIK.leftHand : preview.Equipment.handIK.rightHand;
                    var guard = preview.Guard(hand);
                    Vector3 rotation = LabTimeline.UnwrapEuler((Quaternion.Inverse(guard.rotation) * chain.target.rotation).eulerAngles, motion.rotation.Evaluate(progress));
                    LabTimeline.RecordWrist(keys, motion, progress, guard.InverseTransformPoint(chain.target.position), rotation);
                    if(hand==HandSide.Left)candidate.action.leftHandTargetMotion=null;
                    if (poses.TryGetValue(hand, out var pose))
                    {
                        if(initialGrips.TryGetValue(hand,out var initial))
                        {
                            LabTimeline.RecordFingers(keys,0,Instantiate(initial));var baseKey=LabTimeline.FindFingers(keys,0);
                            baseKey.fingerBlend=LabFingerBlend.Step;baseKey.fingerBlendDuration=0;changedFingers.Add((hand,0));
                        }
                        var previous = LabTimeline.FindFingers(keys, progress)?.fingers;
                        if (previous != null) DestroyImmediate(previous);
                        LabTimeline.RecordFingers(keys, progress, Instantiate(pose));changedFingers.Add((hand,progress));
                        var key=LabTimeline.FindFingers(keys,progress);key.fingerBlend=groupFingerBlend;key.fingerBlendDuration=groupFingerFrames/(60f*draft.action.duration);
                        key.fingerLabel=hand==side&&fingersEdited?fingerPoseLabel:"Captura del conjunto";
                    }
                }
                LabCoordinatedPaths.Compile(candidate,preview);
                Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
                Undo.RecordObject(draft, "Guardar conjunto mano y objeto"); Undo.RecordObject(draft.action, "Guardar conjunto mano y objeto");
                var changedHands=LabCoordinatedPaths.Segments(candidate).Select(s=>s.side).Concat(followContacts.Keys).Distinct().ToArray();
                ApplyCoordinatedCandidate(candidate,objectId,changedHands,changedFingers);
                Undo.CollapseUndoOperations(group);
                dirty = true;
                notice = $"Conjunto guardado en F {CurrentFrame}. Dos puntos de conjunto comparten recorrido; al curvar la pieza también se actualiza la mano.";
                SyncSelection(); return true;
            }
            finally
            {
                foreach (var pose in poses.Values.Concat(initialGrips.Values)) if (pose != null) DestroyImmediate(pose);
                LabAssetIO.DestroyDraft(candidate);
            }
        }
        private LabFingerBlend groupFingerBlend=LabFingerBlend.Smooth;
        private int groupFingerFrames=10;

        private Transform LocalReference => editObject ? ObjectTarget?.parent : editWeapon ? preview.Equipment.weaponPose.PoseRoot.parent : preview.Guard(side);
        private Vector3 LocalUnitScale
        {
            get
            {
                var t = LocalReference;
                return t == null ? Vector3.one : new Vector3(t.TransformVector(Vector3.right).magnitude, t.TransformVector(Vector3.up).magnitude, t.TransformVector(Vector3.forward).magnitude);
            }
        }

        private void EditedWorldPose(out Vector3 position, out Quaternion rotation)
        {
            var reference = LocalReference;
            Vector3 p = keyPosition; Quaternion r = Quaternion.Euler(keyRotation);
            if (editWeapon)
            {
                var holder = preview.Equipment.weaponPose.PoseRoot;
                p += holder.localPosition - preview.lastWeaponOffset;
                r = holder.localRotation * Quaternion.Inverse(Quaternion.Euler(preview.lastWeaponEuler)) * r;
            }
            position = reference != null ? reference.TransformPoint(p) : p;
            rotation = reference != null ? reference.rotation * r : r;
        }

        private void SetEditedWorldPose(Vector3 position, Quaternion rotation)
        {
            var reference = LocalReference;
            Vector3 p = reference != null ? reference.InverseTransformPoint(position) : position;
            Quaternion r = reference != null ? Quaternion.Inverse(reference.rotation) * rotation : rotation;
            if (editWeapon)
            {
                var holder = preview.Equipment.weaponPose.PoseRoot;
                p -= holder.localPosition - preview.lastWeaponOffset;
                r = Quaternion.Inverse(holder.localRotation * Quaternion.Inverse(Quaternion.Euler(preview.lastWeaponEuler))) * r;
            }
            keyPosition = p; keyRotation = LabTimeline.UnwrapEuler(r.eulerAngles, keyRotation);
        }

        private void DrawGripLibrary()
        {
            EditorGUILayout.Space(); EditorGUILayout.LabelField("Agarres preparados y personalizados", EditorStyles.boldLabel);
            gripIndex = EditorGUILayout.Popup("Tipo preparado", gripIndex, new[]{"Sostener arma · animación cargada"}.Concat(LabGripTools.Names).ToArray());
            if (GUILayout.Button("Aplicar agarre como copia editable")) Safe(() =>
            {if(gripIndex==0)UseInitialGrip();else{SetFingerPose(LabGripTools.Preset(preview.Animator, side, gripIndex-1));fingerPoseLabel=LabGripTools.Names[gripIndex-1];}});
            selectedGripType = (LabGripPreset)EditorGUILayout.ObjectField("Mi tipo / variante", selectedGripType, typeof(LabGripPreset), false);
            using(new EditorGUI.DisabledScope(selectedGripType==null))
            if (GUILayout.Button("Aplicar mi tipo como copia editable")) Safe(() =>
            {
                var asset=selectedGripType;
                if (asset.side != side || asset.fingers == null) throw new InvalidOperationException("El agarre necesita una pose de la mano seleccionada.");
                SetFingerPose(Instantiate(asset.fingers));
                fingerPoseLabel=asset.displayName;
            });
            gripName = EditorGUILayout.TextField("Nombre de mi agarre", gripName);
            gripCategory = EditorGUILayout.TextField("Categoría", gripCategory);
            if (GUILayout.Button("Guardar como nuevo tipo / variante…")) Safe(SaveGripType);
            string[] ids = ObjectIds();
            int selected = Array.IndexOf(ids, objectId) + 1;
            int next = EditorGUILayout.Popup("Objeto para adaptar dedos", selected, new[] { "Selecciona una referencia…" }.Concat(ids.Select(ObjectLabel)).ToArray());
            if (next > 0 && next != selected) objectId = ids[next - 1];
            using (new EditorGUI.DisabledScope(ObjectTarget == null))
                if (GUILayout.Button("Aproximar dedos a la superficie")) Safe(() =>
                {
                    EvaluateNow(true);
                    var pose = LabGripTools.Fit(preview.Animator, side, ObjectTarget, SelectedProp, 0.002f, out int contacts);
                    SetFingerPose(pose);
                    notice = $"Aproximación: {contacts}/5 dedos alcanzaron la referencia. Revisa penetraciones y pulgar antes de Guardar dedos.";
                });
            EditorGUILayout.HelpBox("Coloca primero la muñeca alrededor del objeto. La adaptación es una aproximación geométrica, no un agarre perfecto: no mueve la muñeca ni valida fuerza/contacto real. En piezas con malla usa su caja visible; para precisión usa primitivas.", MessageType.None);
        }
        private LabGripPreset selectedGripType;

        private void SetFingerPose(HandPoseData pose)
        {
            bool exact=HandKeys.Any(k=>k.fingers!=null&&Mathf.Abs(k.time-progress)<LabTimeline.TimeEpsilon);
            if(fingerBlendMode==LabFingerBlend.Legacy||(!exact&&!fingersEdited)){fingerBlendMode=LabFingerBlend.Smooth;fingerTransitionFrames=10;}
            fingerPoseLabel="Pose editada";
            ClearFingerOverride(); pose.hideFlags = HideFlags.HideAndDontSave;
            fingerBase = pose; fingers = Instantiate(pose); fingers.hideFlags = HideFlags.HideAndDontSave;
            curls = new Vector3[5]; fingersEdited = true; playing = false;
        }

        private void SaveGripType()
        {
            if (string.IsNullOrWhiteSpace(gripName)) throw new InvalidOperationException("Escribe un nombre para el tipo de agarre.");
            if (fingers == null) CaptureFingerBase();
            string path = EditorUtility.SaveFilePanelInProject("Guardar tipo de agarre", gripName, "asset", "Crea una copia reutilizable para esta mano.", LabAssetIO.Output);
            if (string.IsNullOrEmpty(path)) return;
            path = AssetDatabase.GenerateUniqueAssetPath(path);
            var asset = LabObjectAuthoring.SaveGripType(gripName, gripCategory, side, fingers, path);
            EditorGUIUtility.PingObject(asset);
        }
    }
}
