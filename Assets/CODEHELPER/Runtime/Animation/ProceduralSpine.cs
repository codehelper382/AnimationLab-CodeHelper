using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Serialization;

namespace CodeHelper.ProceduralShooterFramework.Animation
{
    public enum SpineDriveMode
    {
        /// <summary>Simple y seguro: rota los huesos en LateUpdate. No toca el Animator.</summary>
        LateUpdate,
        /// <summary>Procesa la pose dentro del grafo. Medir en Profiler antes de elegir por rendimiento.</summary>
        AnimationJob
    }

    // ═══════════════════════════════════════════════════════════════════
    //  CÓMO FUNCIONA (resumen)
    //
    //    SpineLookProfile (asset)   →  QUÉ huesos giran y CUÁNTO
    //    ProceduralSpine (este)     →  CUÁNDO y CON QUÉ INPUTS (alpha, suavizado, recoil)
    //    SpineLookMath / Job        →  matemática compartida; distinta pose de entrada según modo
    //
    //  Cada frame:
    //    1. Suavizar inputs y recoil
    //    2. Estabilizar spine contra la bind pose
    //    3. Inclinar pelvis (opcional, PelvisTilt)
    //    4. Aplicar Roll → Yaw → Pitch
    //
    //  Orden de ejecución:  Animator → ProceduralSpine (200) → HandIK (300) → Weapon (400)
    // ═══════════════════════════════════════════════════════════════════
    [AddComponentMenu("CodeHelper/Shooter Framework/Animation/Procedural Spine (LookLayer)")]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(ShooterExecutionOrder.Spine)]
    public class ProceduralSpine : MonoBehaviour
    {
        /// <summary>Primer ProceduralSpine activo. Con varios personajes usa referencias directas.</summary>
        public static ProceduralSpine Instance { get; private set; }

        // ─────────────────────────────────────────────────────────────────
        //  INSPECTOR
        // ─────────────────────────────────────────────────────────────────
        [Header("1. Setup")]
        [Tooltip("Qué huesos giran y cuánto. Si lo dejas vacío y el rig es Humanoid, " +
                 "se usa un perfil por defecto automáticamente.")]
        public SpineLookProfile profile;

        [Tooltip("LateUpdate: simple, no toca el Animator.\n" +
                 "AnimationJob: usa un grafo propio (no combinar con otro grafo sobre el mismo Animator). " +
                 "cambia los parámetros con spine.SetFloat / SetBool / SetInteger / SetTrigger " +
                 "en vez de animator.SetFloat.")]
        public SpineDriveMode mode = SpineDriveMode.LateUpdate;
        [Tooltip("Enable character pose/action clip channels in this component's graph, before procedural look. Works in both drive modes.")]
        public bool enableEquipmentAnimation;

        [Tooltip("Opcional. Si es null se busca en este GameObject o en sus hijos.")]
        public Animator animator;
        [Tooltip("Raíz del personaje. Si es null se usa transform.root.")]
        public Transform animationRoot;
        [Tooltip("Opcional. Si es null se busca en este GameObject.")]
        public PelvisTilt pelvisTilt;

        [Header("2. Blend")]
        [Range(0f, 1f)] public float alpha = 1f;
        [Range(0f, 1f)] public float stabilizationWeight = 1f;
        [Min(0f)] [Tooltip("Segundos de suavizado de los inputs. 0 = sin suavizado.")]
        public float inputSmoothTime = 0f;
        [Tooltip("Usa tiempo sin escala (útil si pausas con timeScale = 0).")]
        public bool useUnscaledTime = false;

        [Header("3. Bind Pose")]
        [Tooltip("Clip del que se toma la pose de referencia (ej. rifle_aim).\n" +
                 "Si es null se usa la pose del modelo al iniciar.")]
        public AnimationClip bindPoseClip;
        [Range(0f, 1f)] [Tooltip("Tiempo NORMALIZADO del clip. 0 = primer frame, 1 = último.")]
        public float bindPoseTime = 0f;

        [Header("4. Recoil")]
        public float recoilSnappiness = 20f;
        public float recoilReturnSpeed = 10f;

        [Header("5. Player Inputs (grados, -90 a 90)")]
        [Range(-90f, 90f)] public float inputVertical = 0f;
        [Range(-90f, 90f)] public float inputHorizontal = 0f;
        [Range(-90f, 90f)] public float inputLean = 0f;
        [Range(-90f, 90f)] public float inputTurnOffset = 0f;
        [Tooltip("El yaw usa inputTurnOffset en vez de inputHorizontal.")]
        public bool useTurnOffset = false;

        [Header("Debug")]
        [Tooltip("Sustituye el pitch del jugador para probar el rig en Play.")]
        [FormerlySerializedAs("probarPitch")] public bool testPitchEnabled = false;
        [Range(-90f, 90f)]
        [FormerlySerializedAs("pitchDePrueba")] public float testPitch = 0f;

        // ─────────────────────────────────────────────────────────────────
        #region Runtime data
        private struct RuntimeBone
        {
            public Transform bone;
            public Vector2 limit;
            public float recoilFactor;
            public Quaternion offset;
            public bool hasOffset;
            public bool ignoreClip;
            public Quaternion reference; // bind pose
        }

        private struct LockedBone
        {
            public Transform bone;
            public Quaternion reference;
            public int depth;
            public Quaternion beforeLocal, afterLocal;
            public Quaternion cameraBase, cameraRoll, cameraPrePitch;
        }

        private const float ActiveThreshold = 0.001f;
        private const float RecoilEpsilon = 1e-6f;
        private const float MinAngle = 1e-4f;

        private static readonly Quaternion WorldForward = Quaternion.LookRotation(Vector3.forward, Vector3.up);
        private static readonly RuntimeBone[] NoBones = new RuntimeBone[0];

        private RuntimeBone[] _pitch = NoBones;
        private RuntimeBone[] _yaw = NoBones;
        private RuntimeBone[] _roll = NoBones;
        private LockedBone[] _locked = new LockedBone[0];
        private Quaternion _rootBindInv = Quaternion.identity;
        private bool _hasFramePose;
        private bool _hasCameraPose;
        private float _cameraPitchHeading;
        private int _referenceGeneration;

        private bool _ready;
        private SpineDriveMode _activeMode;
        private Coroutine _initRoutine;
        private SpineLookProfile _fallbackProfile;

        private Vector3 _targetRecoil, _currentRecoil;
        private float _sPitch, _sYaw, _sLean, _sTurn;
        private float _vPitch, _vYaw, _vLean, _vTurn;

        // Animation Job
        private PlayableGraph _graph;
        private AnimationScriptPlayable _jobPlayable;
        private AnimatorControllerPlayable _controller;
        private EquipmentAnimationLayer _equipmentLayer;
        private SpineLookJob _job;
        private NativeArray<SpineLookAtom> _rollAtoms, _yawAtoms, _pitchAtoms;
        private NativeArray<SpineLockedAtom> _lockedAtoms;
        private PelvisTilt _boundPelvis;
        private Transform _boundPelvisBone, _boundLeftThigh, _boundRightThigh;
        private readonly Dictionary<string, Transform> _boneMap = new Dictionary<string, Transform>();

        private bool IsActive => alpha > ActiveThreshold;
        private float PitchTarget => testPitchEnabled ? Mathf.Clamp(testPitch, -90f, 90f) : inputVertical;
        #endregion

        /// <summary>Pitch suavizado realmente aplicado (grados).</summary>
        public float AppliedPitch => _sPitch;
        /// <summary>True cuando ya capturó la bind pose y está aplicando la mirada.</summary>
        public bool IsReady => _ready;
        /// <summary>Modo realmente en uso (puede diferir de 'mode' si el job no pudo iniciar).</summary>
        public SpineDriveMode ActiveMode => _activeMode;
        public int ReferenceGeneration => _referenceGeneration;

        /// <summary>World phases of this frame, for separating body-carried pose from procedural
        /// roll/pitch in the camera anchor. Reads completed stream data in LateUpdate for job mode.</summary>
        public bool TryGetCameraBonePose(Transform bone, out Quaternion basePose, out Quaternion rollPose,
            out Quaternion prePitchPose, out float pitchHeading)
        {
            basePose = rollPose = prePitchPose = Quaternion.identity;
            pitchHeading = 0f;
            if (!_ready || !enabled || !IsActive) return false;
            for (int i = 0; i < _locked.Length; i++)
            {
                if (_locked[i].bone != bone) continue;
                if (_activeMode == SpineDriveMode.AnimationJob)
                {
                    if (!_lockedAtoms.IsCreated) return false;
                    SpineLockedAtom atom = _lockedAtoms[i];
                    if (!atom.cameraPoseValid) return false;
                    // A paused/culled Animator may retain the previous evaluation while its
                    // parent root turns. Transport snapshots with that root, but reject them
                    // if another writer replaced the bone pose in the meantime.
                    Quaternion rootDelta = animationRoot.rotation * Quaternion.Inverse(atom.cameraRoot);
                    if (Quaternion.Angle(bone.rotation, rootDelta * atom.cameraFinal) > 0.05f) return false;
                    basePose = rootDelta * atom.cameraBase;
                    rollPose = rootDelta * atom.cameraRoll;
                    prePitchPose = rootDelta * atom.cameraPrePitch;
                    pitchHeading = animationRoot.eulerAngles.y + atom.cameraPitchYaw;
                }
                else
                {
                    if (!_hasCameraPose) return false;
                    basePose = _locked[i].cameraBase;
                    rollPose = _locked[i].cameraRoll;
                    prePitchPose = _locked[i].cameraPrePitch;
                    pitchHeading = _cameraPitchHeading;
                }
                return true;
            }
            return false;
        }

        /// <summary>Neutral stabilization reference before procedural look. Used only when calibrating
        /// an independent head anchor; returns false for bones not controlled by this spine.</summary>
        public bool TryGetReferenceWorldRotation(Transform bone, out Quaternion rotation)
        {
            rotation = Quaternion.identity;
            if (!_ready || animationRoot == null || bone == null) return false;
            for (int i = 0; i < _locked.Length; i++)
            {
                if (_locked[i].bone != bone) continue;
                rotation = animationRoot.rotation * _rootBindInv * _locked[i].reference;
                return true;
            }
            return false;
        }

        // ─────────────────────────────────────────────────────────────────
        #region Unity Lifecycle
        void Awake()
        {
            if (animationRoot == null) animationRoot = transform.root;
            if (animator == null) animator = GetComponent<Animator>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (pelvisTilt == null) pelvisTilt = GetComponent<PelvisTilt>();
        }

        void OnEnable()
        {
            if (Instance == null) Instance = this;
            Reinitialize();
        }

        void OnDisable()
        {
            TearDown();
            if (Instance == this) Instance = null;
        }

        void OnDestroy()
        {
            TearDown();
            if (_fallbackProfile != null) Destroy(_fallbackProfile);
            if (Instance == this) Instance = null;
        }

        void OnValidate()
        {
            alpha = Mathf.Clamp01(alpha);
            stabilizationWeight = Mathf.Clamp01(stabilizationWeight);
            inputSmoothTime = Mathf.Max(0f, inputSmoothTime);
            recoilSnappiness = Mathf.Max(0f, recoilSnappiness);
            recoilReturnSpeed = Mathf.Max(0f, recoilReturnSpeed);
        }

        // AnimationJob: los datos deben estar listos ANTES de que el Animator evalúe el grafo,
        // por eso se actualizan en Update y no en LateUpdate.
        void Update()
        {
            if (!_ready) return;
            if (_activeMode == SpineDriveMode.LateUpdate)
            {
                // Remove our last additive result BEFORE the Animator evaluates again.
                // Also works for unanimated bones/paused Animators without accumulating look.
                if (pelvisTilt != null) pelvisTilt.Restore();
                RestoreFramePose();
                return;
            }
            Tick(DeltaTime());
            PushJobData();
        }

        void LateUpdate()
        {
            if (!_ready || animationRoot == null || _activeMode != SpineDriveMode.LateUpdate) return;
            _hasCameraPose = false;

            if (pelvisTilt != null) pelvisTilt.Restore();

            Tick(DeltaTime());
            if (!IsActive) return;
            CaptureFramePose(false);
            StabilizeSpine();

            if (pelvisTilt != null && pelvisTilt.isActiveAndEnabled)
                pelvisTilt.Apply(animationRoot, _sPitch / 90f, alpha);

            CaptureCameraPose(0);
            ApplyLook();
            CaptureFramePose(true);
        }

        private float DeltaTime() => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        #endregion

        // ─────────────────────────────────────────────────────────────────
        #region Initialization
        /// <summary>Cambia el perfil en runtime (por ejemplo al equipar otra arma).</summary>
        public void SetProfile(SpineLookProfile newProfile)
        {
            profile = newProfile;
            Reinitialize();
        }

        /// <summary>
        /// Vuelve a resolver huesos y capturar la bind pose (espera 1 frame para que
        /// el Animator escriba su pose). Llámalo si cambias el perfil o el bind pose clip.
        /// </summary>
        public void Reinitialize()
        {
            TearDown();
            if (!isActiveAndEnabled) return;
            _initRoutine = StartCoroutine(InitRoutine());
        }

        private IEnumerator InitRoutine()
        {
            yield return null;
            _initRoutine = null;

            if (animationRoot == null || !ResolveRuntimeBones()) yield break;

            CaptureBindPose();

            _activeMode = mode;
            if (_activeMode == SpineDriveMode.AnimationJob && !TryBuildJobGraph())
                _activeMode = SpineDriveMode.LateUpdate; // fallback seguro
            if (_activeMode == SpineDriveMode.LateUpdate && enableEquipmentAnimation) TryBuildEquipmentGraph();

            SnapInputs();
            if (_activeMode == SpineDriveMode.AnimationJob) PushJobData();
            _ready = true;
        }

        private void TearDown()
        {
            if (_initRoutine != null) { StopCoroutine(_initRoutine); _initRoutine = null; }
            _ready = false;
            _hasCameraPose = false;

            if (pelvisTilt != null) pelvisTilt.Restore();
            RestoreFramePose();

            if (_graph.IsValid()) _graph.Destroy();
            _equipmentLayer?.Dispose();
            _equipmentLayer = null;
            DisposeArray(ref _rollAtoms);
            DisposeArray(ref _yawAtoms);
            DisposeArray(ref _pitchAtoms);
            DisposeArray(ref _lockedAtoms);
            _job = default;
            _boundPelvis = null;
            _boundPelvisBone = _boundLeftThigh = _boundRightThigh = null;
        }

        private static void DisposeArray<T>(ref NativeArray<T> array) where T : unmanaged
        {
            if (array.IsCreated) array.Dispose();
            array = default;
        }

        // Convierte el perfil (datos) en huesos reales de la escena.
        private bool ResolveRuntimeBones()
        {
            SpineLookProfile source = profile != null ? profile : GetFallbackProfile();
            if (source == null) return false;

            // A single hierarchy walk per rebuild, never one allocation/search per custom element.
            _boneMap.Clear();
            Transform[] hierarchy = animationRoot.GetComponentsInChildren<Transform>(true);
            foreach (Transform bone in hierarchy)
                if (!_boneMap.ContainsKey(bone.name)) _boneMap.Add(bone.name, bone);

            _pitch = ResolveList(source.pitchElements, "Pitch");
            _yaw = ResolveList(source.yawElements, "Yaw");
            _roll = ResolveList(source.rollElements, "Roll");
            if (profile == null)
            {
                // Redistribute only the generated default. Preserve authored profile limits.
                NormalizeDefaults(_pitch, source.pitchTotal);
                NormalizeDefaults(_yaw, source.yawTotal);
                NormalizeDefaults(_roll, source.rollTotal);
            }
            _boneMap.Clear();
            if (_pitch.Length + _yaw.Length + _roll.Length > 0) return true;
            Debug.LogWarning("[ProceduralSpine] No se resolvió ningún hueso del perfil.", this);
            return false;
        }

        private static void NormalizeDefaults(RuntimeBone[] bones, float total)
        {
            float sum = 0f;
            for (int i = 0; i < bones.Length; i++) sum += bones[i].limit.x;
            if (sum <= 0f) return;
            float scale = total / sum;
            for (int i = 0; i < bones.Length; i++) bones[i].limit *= scale;
        }

        private SpineLookProfile GetFallbackProfile()
        {
            if (animator == null || !animator.isHuman)
            {
                Debug.LogWarning("[ProceduralSpine] Asigna un SpineLookProfile (el rig no es Humanoid).", this);
                return null;
            }
            if (_fallbackProfile == null) _fallbackProfile = SpineLookProfile.CreateHumanoidDefault();
            return _fallbackProfile;
        }

        private RuntimeBone[] ResolveList(List<SpineLookElement> elements, string label)
        {
            if (elements == null) return NoBones;
            var result = new List<RuntimeBone>(elements.Count);
            var seen = new HashSet<Transform>();
            for (int i = 0; i < elements.Count; i++)
            {
                SpineLookElement e = elements[i];
                Transform bone = null;
                if (e != null && !string.IsNullOrEmpty(e.customBone)) _boneMap.TryGetValue(e.customBone, out bone);
                else bone = SpineLookProfile.ResolveBone(animator, animationRoot, e);
                if (bone == null)
                {
                    Debug.LogWarning($"[ProceduralSpine] {label}: no se encontró el hueso '{e?.DisplayName}'.", this);
                    continue;
                }
                if (bone == animationRoot || !bone.IsChildOf(animationRoot) || !seen.Add(bone))
                {
                    Debug.LogWarning($"[ProceduralSpine] {label}: hueso duplicado o fuera del rig: '{bone.name}'.", this);
                    continue;
                }
                result.Add(new RuntimeBone
                {
                    bone = bone,
                    limit = new Vector2(Mathf.Max(0f, e.clampedAngle.x), Mathf.Max(0f, e.clampedAngle.y)),
                    recoilFactor = Mathf.Clamp(e.recoilFactor, 0f, 2f),
                    offset = Quaternion.Euler(e.offsetEuler),
                    hasOffset = e.offsetEuler.sqrMagnitude > 1e-8f,
                    ignoreClip = e.ignoreClipSample,
                    reference = Quaternion.identity
                });
            }
            return result.ToArray();
        }
        #endregion

        // ─────────────────────────────────────────────────────────────────
        #region Bind Pose
        // Samplea el clip, guarda las referencias y RESTAURA la pose del rig.
        private void CaptureBindPose()
        {
            if (bindPoseClip == null) { StoreReferences(); return; }

            Transform[] all = animationRoot.GetComponentsInChildren<Transform>(true);
            int n = all.Length;
            var pos = new Vector3[n];
            var rot = new Quaternion[n];
            var scl = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                pos[i] = all[i].localPosition;
                rot[i] = all[i].localRotation;
                scl[i] = all[i].localScale;
            }

            try
            {
                GameObject sampleRoot = animator != null && animator.transform.IsChildOf(animationRoot)
                    ? animator.gameObject : animationRoot.gameObject;
                bindPoseClip.SampleAnimation(sampleRoot, Mathf.Clamp01(bindPoseTime) * bindPoseClip.length);
                StoreReferences();
            }
            finally
            {
                for (int i = 0; i < n; i++)
                {
                    if (all[i] == null) continue;
                    all[i].localPosition = pos[i];
                    all[i].localRotation = rot[i];
                    all[i].localScale = scl[i];
                }
            }
        }

        // ignoreClipSample = true → siempre "mirando al frente" (world forward)
        // ignoreClipSample = false → rotación actual del hueso (pose del clip)
        private void StoreReferences()
        {
            _rootBindInv = Quaternion.Inverse(animationRoot.rotation);
            SetReferences(_pitch);
            SetReferences(_yaw);
            SetReferences(_roll);
            BuildLockedList();
            unchecked { _referenceGeneration++; }
        }

        private static void SetReferences(RuntimeBone[] bones)
        {
            for (int i = 0; i < bones.Length; i++)
                bones[i].reference = bones[i].ignoreClip ? WorldForward : bones[i].bone.rotation;
        }

        // Huesos únicos (el primero gana: pitch → yaw → roll), padres primero,
        // para que un padre nunca pise a un hijo ya estabilizado.
        private void BuildLockedList()
        {
            var list = new List<LockedBone>();
            var seen = new HashSet<Transform>();
            AddLocked(_pitch, list, seen);
            AddLocked(_yaw, list, seen);
            AddLocked(_roll, list, seen);
            list.Sort((a, b) => a.depth.CompareTo(b.depth));
            _locked = list.ToArray();
        }

        private static void AddLocked(RuntimeBone[] bones, List<LockedBone> list, HashSet<Transform> seen)
        {
            for (int i = 0; i < bones.Length; i++)
            {
                if (!seen.Add(bones[i].bone)) continue;
                int depth = 0;
                for (Transform t = bones[i].bone.parent; t != null; t = t.parent) depth++;
                list.Add(new LockedBone { bone = bones[i].bone, reference = bones[i].reference, depth = depth });
            }
        }
        #endregion

        // ─────────────────────────────────────────────────────────────────
        #region Inputs & Recoil
        private void SnapInputs()
        {
            _sPitch = PitchTarget;
            _sYaw = inputHorizontal;
            _sLean = inputLean;
            _sTurn = inputTurnOffset;
            _vPitch = _vYaw = _vLean = _vTurn = 0f;
        }

        private void Tick(float dt)
        {
            SmoothInputs(dt);
            UpdateRecoil(dt);
        }

        private void SmoothInputs(float dt)
        {
            if (inputSmoothTime <= 0.0001f)
            {
                _sPitch = PitchTarget; _sYaw = inputHorizontal; _sLean = inputLean; _sTurn = inputTurnOffset;
                return;
            }
            if (dt <= 0f) return;

            _sPitch = Mathf.SmoothDamp(_sPitch, PitchTarget, ref _vPitch, inputSmoothTime, Mathf.Infinity, dt);
            _sYaw = Mathf.SmoothDamp(_sYaw, inputHorizontal, ref _vYaw, inputSmoothTime, Mathf.Infinity, dt);
            _sLean = Mathf.SmoothDamp(_sLean, inputLean, ref _vLean, inputSmoothTime, Mathf.Infinity, dt);
            _sTurn = Mathf.SmoothDamp(_sTurn, inputTurnOffset, ref _vTurn, inputSmoothTime, Mathf.Infinity, dt);
        }

        // Damping independiente del framerate. Se "duerme" cuando no hay recoil.
        private void UpdateRecoil(float dt)
        {
            if (_targetRecoil.sqrMagnitude < RecoilEpsilon && _currentRecoil.sqrMagnitude < RecoilEpsilon)
            {
                _targetRecoil = Vector3.zero;
                _currentRecoil = Vector3.zero;
                return;
            }
            float returnDamp = 1f - Mathf.Exp(-Mathf.Max(0f, recoilReturnSpeed) * dt);
            float snapDamp = 1f - Mathf.Exp(-Mathf.Max(0f, recoilSnappiness) * dt);
            _targetRecoil = Vector3.Lerp(_targetRecoil, Vector3.zero, returnDamp);
            _currentRecoil = Vector3.Lerp(_currentRecoil, _targetRecoil, snapDamp);
        }
        #endregion

        // ─────────────────────────────────────────────────────────────────
        #region Modo LateUpdate
        private void CaptureFramePose(bool after)
        {
            for (int i = 0; i < _locked.Length; i++)
            {
                if (_locked[i].bone == null) continue;
                Quaternion rotation = _locked[i].bone.localRotation;
                if (after) _locked[i].afterLocal = rotation;
                else _locked[i].beforeLocal = rotation;
            }
            _hasFramePose = after;
        }

        private void RestoreFramePose()
        {
            if (!_hasFramePose) return;
            _hasFramePose = false;
            for (int i = 0; i < _locked.Length; i++)
            {
                Transform bone = _locked[i].bone;
                // Do not overwrite a new pose written by another system after us.
                if (bone != null && Quaternion.Angle(bone.localRotation, _locked[i].afterLocal) <= 0.05f)
                    bone.localRotation = _locked[i].beforeLocal;
            }
        }

        private void StabilizeSpine()
        {
            float weight = Mathf.Clamp01(stabilizationWeight) * Mathf.Clamp01(alpha);
            if (weight <= ActiveThreshold || _locked.Length == 0) return;

            Quaternion rootDelta = animationRoot.rotation * _rootBindInv;
            bool full = weight >= 1f;

            for (int i = 0; i < _locked.Length; i++)
            {
                Transform bone = _locked[i].bone;
                if (bone == null) continue;
                Quaternion target = rootDelta * _locked[i].reference;
                bone.rotation = full ? target : Quaternion.Slerp(bone.rotation, target, weight);
            }
        }

        private void ApplyLook()
        {
            Quaternion rootRot = animationRoot.rotation;
            float yawInput = useTurnOffset ? _sTurn : _sYaw;

            ApplyAxis(_roll, rootRot * Vector3.forward, _sLean / 90f, _currentRecoil.z, 1f);
            CaptureCameraPose(1);
            float yawApplied = ApplyAxis(_yaw, rootRot * Vector3.up, yawInput / 90f, _currentRecoil.y, 1f);
            CaptureCameraPose(2);
            _cameraPitchHeading = animationRoot.eulerAngles.y + yawApplied;
            _hasCameraPose = true;

            Vector3 pitchAxis = rootRot * (Quaternion.AngleAxis(yawApplied, Vector3.up) * Vector3.right);
            ApplyAxis(_pitch, pitchAxis, _sPitch / 90f, _currentRecoil.x, -1f);
        }

        private void CaptureCameraPose(int phase)
        {
            for (int i = 0; i < _locked.Length; i++)
            {
                if (_locked[i].bone == null) continue;
                Quaternion rotation = _locked[i].bone.rotation;
                if (phase == 0) _locked[i].cameraBase = rotation;
                else if (phase == 1) _locked[i].cameraRoll = rotation;
                else _locked[i].cameraPrePitch = rotation;
            }
        }

        private float ApplyAxis(RuntimeBone[] bones, Vector3 axis, float fraction, float recoil, float recoilSign)
        {
            float applied = 0f;
            for (int i = 0; i < bones.Length; i++)
            {
                Transform bone = bones[i].bone;
                if (bone == null) continue;

                float angle = SpineLookMath.ComputeAngle(bones[i].limit, fraction, recoil,
                                                         bones[i].recoilFactor, recoilSign);
                applied += angle;

                if (!bones[i].hasOffset && Mathf.Abs(angle) < MinAngle) continue;

                bone.rotation = SpineLookMath.Rotate(bone.rotation, axis, angle, bones[i].offset, Mathf.Clamp01(alpha));
            }
            return applied * Mathf.Clamp01(alpha);
        }
        #endregion

        // ─────────────────────────────────────────────────────────────────
        #region Modo Animation Job
        // Crea un grafo propio: AnimatorController → SpineLookJob → Animator.
        private bool TryBuildJobGraph()
        {
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                Debug.LogWarning("[ProceduralSpine] AnimationJob necesita un Animator con Controller. " +
                                 "Se usa el modo LateUpdate.", this);
                return false;
            }

            // Stream handles may only refer to transforms under this Animator.
            if (!CanBind(_pitch) || !CanBind(_yaw) || !CanBind(_roll))
            {
                Debug.LogWarning("[ProceduralSpine] Hay huesos fuera de la jerarquía del Animator. Se usa LateUpdate.", this);
                return false;
            }

            try
            {
                _rollAtoms = BuildAtoms(_roll);
                _yawAtoms = BuildAtoms(_yaw);
                _pitchAtoms = BuildAtoms(_pitch);
                _lockedAtoms = new NativeArray<SpineLockedAtom>(_locked.Length, Allocator.Persistent);
                for (int i = 0; i < _locked.Length; i++)
                {
                    _lockedAtoms[i] = new SpineLockedAtom
                    {
                        handle = animator.BindStreamTransform(_locked[i].bone),
                        reference = _locked[i].reference
                    };
                }

                _job = new SpineLookJob
                {
                    // The character root may be ABOVE the Animator; it is a scene reference, not a stream bone.
                    root = animator.BindSceneTransform(animationRoot),
                    roll = _rollAtoms,
                    yaw = _yawAtoms,
                    pitch = _pitchAtoms,
                    locked = _lockedAtoms,
                    rootBindInv = _rootBindInv,
                    ready = true
                };

                UpdateJobPelvis();

                _graph = PlayableGraph.Create("ProceduralSpine");
                _graph.SetTimeUpdateMode(useUnscaledTime ? DirectorUpdateMode.UnscaledGameTime
                                                       : DirectorUpdateMode.GameTime);

                var output = AnimationPlayableOutput.Create(_graph, "ProceduralSpineOutput", animator);
                _controller = AnimatorControllerPlayable.Create(_graph, animator.runtimeAnimatorController);
                CopyAnimatorStateToController();
                Playable source = _controller;
                if (enableEquipmentAnimation)
                {
                    _equipmentLayer = new EquipmentAnimationLayer(_graph, source);
                    source = _equipmentLayer.Output;
                }
                _jobPlayable = AnimationScriptPlayable.Create(_graph, _job);
                _jobPlayable.AddInput(source, 0, 1f);
                output.SetSourcePlayable(_jobPlayable);
                _graph.Play();
                return true;
            }
            catch (System.Exception exception)
            {
                if (_graph.IsValid()) _graph.Destroy();
                _equipmentLayer?.Dispose();
                _equipmentLayer = null;
                DisposeArray(ref _rollAtoms);
                DisposeArray(ref _yawAtoms);
                DisposeArray(ref _pitchAtoms);
                DisposeArray(ref _lockedAtoms);
                _job = default;
                _boundPelvis = null;
                Debug.LogWarning($"[ProceduralSpine] No se pudo crear el job; se usa LateUpdate. {exception.Message}", this);
                return false;
            }
        }

        private bool CanBind(RuntimeBone[] bones)
        {
            for (int i = 0; i < bones.Length; i++)
                if (bones[i].bone == null || !bones[i].bone.IsChildOf(animator.transform)) return false;
            return true;
        }

        private bool TryBuildEquipmentGraph()
        {
            if (animator == null || animator.runtimeAnimatorController == null) return false;
            try
            {
                _graph = PlayableGraph.Create("ProceduralSpine.Equipment");
                _graph.SetTimeUpdateMode(useUnscaledTime ? DirectorUpdateMode.UnscaledGameTime : DirectorUpdateMode.GameTime);
                _controller = AnimatorControllerPlayable.Create(_graph, animator.runtimeAnimatorController);
                CopyAnimatorStateToController();
                _equipmentLayer = new EquipmentAnimationLayer(_graph, _controller);
                var output = AnimationPlayableOutput.Create(_graph, "EquipmentBeforeSpine", animator);
                output.SetSourcePlayable(_equipmentLayer.Output);
                _graph.Play();
                return true;
            }
            catch (System.Exception exception)
            {
                if (_graph.IsValid()) _graph.Destroy();
                _equipmentLayer?.Dispose();
                _equipmentLayer = null;
                Debug.LogWarning($"[ProceduralSpine] Equipment graph unavailable: {exception.Message}", this);
                return false;
            }
        }

        public bool SetEquipmentAnimation(AnimationClip pose, AvatarMask poseMask, double poseTime,
            AnimationClip action, AvatarMask actionMask, double actionTime, float actionWeight,
            bool leftArmOwnedByIK = false, bool rightArmOwnedByIK = false)
        {
            if (!_ready || _equipmentLayer == null || !_graph.IsValid()) return false;
            _equipmentLayer.Set(pose, poseMask, poseTime, action, actionMask, actionTime, actionWeight,
                leftArmOwnedByIK, rightArmOwnedByIK);
            return true;
        }

        // Preserve basic parameters/current states at graph creation. Active transitions and pending
        // triggers cannot be cloned exactly; do not switch modes/profiles during critical transitions.
        private void CopyAnimatorStateToController()
        {
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                int id = parameter.nameHash;
                switch (parameter.type)
                {
                    case AnimatorControllerParameterType.Float: _controller.SetFloat(id, animator.GetFloat(id)); break;
                    case AnimatorControllerParameterType.Int: _controller.SetInteger(id, animator.GetInteger(id)); break;
                    case AnimatorControllerParameterType.Bool: _controller.SetBool(id, animator.GetBool(id)); break;
                }
            }
            for (int i = 0; i < animator.layerCount; i++)
            {
                _controller.SetLayerWeight(i, animator.GetLayerWeight(i));
                AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(i);
                if (state.fullPathHash != 0) _controller.Play(state.fullPathHash, i, state.normalizedTime);
            }
        }

        private NativeArray<SpineLookAtom> BuildAtoms(RuntimeBone[] bones)
        {
            var atoms = new NativeArray<SpineLookAtom>(bones.Length, Allocator.Persistent);
            for (int i = 0; i < bones.Length; i++)
            {
                atoms[i] = new SpineLookAtom
                {
                    handle = animator.BindStreamTransform(bones[i].bone),
                    limit = bones[i].limit,
                    recoilFactor = bones[i].recoilFactor,
                    offset = bones[i].offset,
                    hasOffset = bones[i].hasOffset
                };
            }
            return atoms;
        }

        private bool PelvisConfigured()
        {
            return pelvisTilt != null && pelvisTilt.isActiveAndEnabled && pelvisTilt.IsConfigured &&
                   pelvisTilt.pelvisBone != animationRoot;
        }

        private void UpdateJobPelvis()
        {
            _job.hasPelvis = false;
            if (!PelvisConfigured()) return;
            // Cache handles until the references change; enable/angle are live settings.
            if (_boundPelvis != pelvisTilt || _boundPelvisBone != pelvisTilt.pelvisBone ||
                _boundLeftThigh != pelvisTilt.leftThigh || _boundRightThigh != pelvisTilt.rightThigh)
            {
                if (!pelvisTilt.pelvisBone.IsChildOf(animator.transform) ||
                    !pelvisTilt.leftThigh.IsChildOf(animator.transform) ||
                    !pelvisTilt.rightThigh.IsChildOf(animator.transform)) return;
                _job.pelvis = animator.BindStreamTransform(pelvisTilt.pelvisBone);
                _job.leftThigh = animator.BindStreamTransform(pelvisTilt.leftThigh);
                _job.rightThigh = animator.BindStreamTransform(pelvisTilt.rightThigh);
                _boundPelvis = pelvisTilt;
                _boundPelvisBone = pelvisTilt.pelvisBone;
                _boundLeftThigh = pelvisTilt.leftThigh;
                _boundRightThigh = pelvisTilt.rightThigh;
            }
            _job.pelvisMaxAngle = Mathf.Clamp(pelvisTilt.maxAngle, 0f, 30f);
            _job.hasPelvis = true;
        }

        // Copia los datos del frame al job (barato: solo floats).
        private void PushJobData()
        {
            _job.alpha = Mathf.Clamp01(alpha);
            _job.stabilization = Mathf.Clamp01(stabilizationWeight);
            _job.pitchInput = _sPitch;
            _job.yawInput = useTurnOffset ? _sTurn : _sYaw;
            _job.leanInput = _sLean;
            _job.recoil = _currentRecoil;
            UpdateJobPelvis();
            _jobPlayable.SetJobData(_job);
        }
        #endregion

        // ─────────────────────────────────────────────────────────────────
        #region Public API
        /// <summary>Inputs en GRADOS (-90 a 90).</summary>
        public void SetLookInputs(float vert, float horz, float lean, float turnOffset = 0f)
        {
            inputVertical = Mathf.Clamp(vert, -90f, 90f);
            inputHorizontal = Mathf.Clamp(horz, -90f, 90f);
            inputLean = Mathf.Clamp(lean, -90f, 90f);
            inputTurnOffset = Mathf.Clamp(turnOffset, -90f, 90f);
        }

        /// <param name="recoilForce">X = pitch, Y = yaw, Z = roll (grados).</param>
        public void AddSpineRecoil(Vector3 recoilForce) { _targetRecoil += recoilForce; }

        public void ResetRecoil()
        {
            _targetRecoil = Vector3.zero;
            _currentRecoil = Vector3.zero;
        }

        // ── Parámetros del Animator ──
        // Úsalos SIEMPRE en vez de animator.SetXxx: funcionan en ambos modos.
        // (En modo AnimationJob el Controller corre en un grafo propio.)
        private bool UsesOwnGraph => _ready && _graph.IsValid() && _controller.IsValid();

        public void SetFloat(int id, float value)
        { if (UsesOwnGraph) _controller.SetFloat(id, value); else if (animator != null) animator.SetFloat(id, value); }

        public void SetBool(int id, bool value)
        { if (UsesOwnGraph) _controller.SetBool(id, value); else if (animator != null) animator.SetBool(id, value); }

        public void SetInteger(int id, int value)
        { if (UsesOwnGraph) _controller.SetInteger(id, value); else if (animator != null) animator.SetInteger(id, value); }

        public void SetTrigger(int id)
        { if (UsesOwnGraph) _controller.SetTrigger(id); else if (animator != null) animator.SetTrigger(id); }
        #endregion

        // ─────────────────────────────────────────────────────────────────
        #region Gizmos (solo Editor)
#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Transform root = animationRoot != null ? animationRoot : transform;
            Animator anim = animator != null ? animator : GetComponentInChildren<Animator>();
            SpineLookProfile source = profile;
            if (source == null) return;

            DrawElements(source.pitchElements, anim, root, Color.red, Vector3.right, Vector3.forward);
            DrawElements(source.yawElements, anim, root, new Color(1f, 0.92f, 0.016f), Vector3.up, Vector3.forward);
            DrawElements(source.rollElements, anim, root, Color.cyan, Vector3.forward, Vector3.up);
        }

        // Los límites se dibujan sobre el eje REAL de rotación (no sobre los ejes del hueso).
        private void DrawElements(List<SpineLookElement> list, Animator anim, Transform root,
                                  Color color, Vector3 localAxis, Vector3 localReference)
        {
            Vector3 axis = root.TransformDirection(localAxis);
            Vector3 reference = root.TransformDirection(localReference);

            for (int i = 0; i < list.Count; i++)
            {
                SpineLookElement e = list[i];
                Transform bone = SpineLookProfile.ResolveBone(anim, root, e);
                if (bone == null) continue;

                // Blanco = ignoreClipSample | color = normal | gris = alpha 0
                Gizmos.color = e.ignoreClipSample ? Color.white
                             : (alpha > 0f ? color : new Color(0.5f, 0.5f, 0.5f, 0.4f));
                Gizmos.DrawWireSphere(bone.position, 0.05f);
                if (alpha <= 0f) continue;

                const float size = 0.2f;
                Gizmos.DrawLine(bone.position, bone.position + Quaternion.AngleAxis(e.clampedAngle.x, axis) * reference * size);
                Gizmos.DrawLine(bone.position, bone.position + Quaternion.AngleAxis(-e.clampedAngle.y, axis) * reference * size);
            }
        }
#endif
        #endregion
    }
}
