# Plan de Mejoras: Sistema Automatizado de Recargas con Arte Visual Profesional

**Fecha:** 15 de noviembre de 2026  
**Proyecto:** D:\Unity\CODEHELPER AnimationLab  
**Objetivo:** Transformar el sistema actual (3 sistemas paralelos confusos) en **UN sistema unificado, automatizado y con arte visual profesional** para recargas de cualquier arma.

---

## 🎯 VISIÓN GENERAL

> **Usuario define puntos de anclaje → Sistema genera recarga completa con movimiento, rotación, transiciones y dedos de calidad profesional automáticamente**

### Flujo objetivo (5 pasos guiados):

```
┌─────────────────────────────────────────────────────────────────────────────┐
│  1. SELECCIONAR ARMA          │  Tipo: Pistola / Rifle / Escopeta / SMG    │
│  2. DEFINIR PUNTOS ANCLAJE    │  Guardia → Cargador → Suministro → Arma    │
│  3. CONFIGURAR DEDOS          │  Slider mano + presets por fase            │
│  4. ELEGIR ESTILO VISUAL      │  Táctico / Fluido / Rápido / Cinemático    │
│  5. GENERAR + REFINAR         │  Preview automático → Ajustes finos → Guardar│
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 📋 TAREAS ORGANIZADAS POR PRIORIDAD

### FASE 1: LIMPIEZA Y UNIFICACIÓN (Semana 1-2)

| # | Tarea | Archivos afectados | Estado |
|---|-------|-------------------|--------|
| 1.1 | **Eliminar `LabReloadGenerator.cs`** (receta densa hardcoded 5 templates) | `LabReloadGenerator.cs`, `AnimationLabStudio.cs` | ⬜ Pendiente |
| 1.2 | **Eliminar `LabAK12Reload.cs`** (solo AK12, 5 etapas fijas, requiere piloto) | `LabAK12Reload.cs`, `AnimationLabAK12Reload.cs`, `LabAK12ReloadRecipe.cs` | ⬜ Pendiente |
| 1.3 | **Refactorizar `LabHandTemplateInsertion.cs`** → nuevo `AutoReloadGenerator.cs` | `LabHandTemplateInsertion.cs`, `LabGestureAuthoring.cs`, `LabHandTemplateLibrary.cs` | ⬜ Pendiente |
| 1.4 | **Unificar `LabTransitionAssistant` + `AnimationLabGripTransitionPreview`** → `TransitionEngine.cs` | `AnimationLabTransitions.cs`, `AnimationLabGripTransitionPreview.cs`, `LabTransitionAssistant.cs` | ⬜ Pendiente |
| 1.5 | **Reducir `LabPathMode` de 8 a 4** (Linear, Cinematic, Bezier, Flow) | `LabPathCurves.cs`, `AnimationLabTake.cs` | ⬜ Pendiente |
| 1.6 | **Unificar conceptos de agarre** (`LabGripPreset` + `fingerLabel` + `LabHandTemplate` → `GripProfile`) | `LabGripTools.cs`, `LabHandTemplateLibrary.cs`, `AnimationLabHandTemplates.cs` | ⬜ Pendiente |

### FASE 2: NUEVO SISTEMA AUTOMATIZADO (Semana 2-4)

| # | Tarea | Descripción | Estado |
|---|-------|-------------|--------|
| 2.1 | **Crear `WeaponTypeProfile`** (ScriptableObject) | Define patrón de recarga por tipo: Pistola, Rifle, Escopeta, SMG, Revólver, Sniper, Arco, Personalizado | ⬜ Pendiente |
| 2.2 | **Crear `AnchorPointWizard`** (EditorWindow paso a paso) | UI guiada: 1) Arma 2) Puntos anclaje 3) Dedos 4) Estilo 5) Generar | ⬜ Pendiente |
| 2.3 | **Implementar `AutoReloadGenerator`** | Algoritmo que toma puntos anclaje → genera curvas weapon/manos/piezas + dedos + transiciones | ⬜ Pendiente |
| 2.4 | **Crear `VisualStyleProfile`** (ScriptableObject) | Estilos predefinidos: Tactical, Fluid, Speed, Cinematic, Stealth - con curvas, timing, overshoot, settle | ⬜ Pendiente |
| 2.5 | **Sistema de dedos por fase automatizado** | Slider mano (izq/der) + preset por fase (Contacto, Extraer, Insertar, Soltar) → blend automático | ⬜ Pendiente |
| 2.6 | **Integración con `LabCoordinatedPaths`** | Contactos mano-pieza automáticos desde puntos anclaje, compile a 60Hz | ⬜ Pendiente |

### FASE 3: ARTE VISUAL PROFESIONAL (Semana 3-5)

| # | Tarea | Referencia visual | Estado |
|---|-------|-------------------|--------|
| 3.1 | **Analizar referencias de recarga** | YouTube Shorts, GameAnim, referencias AAA (CoD, Battlefield, Tarkov, Insurgency) | ⬜ Pendiente |
| 3.2 | **Definir "Micro-comportamientos" por estilo** | Anticipación, overshoot, settle, micro-jitter, weight shift, breath | ⬜ Pendiente |
| 3.3 | **Curvas de animación signature por arma/estilo** | Rifle-Tactical: peso, control, 2-manos | ⬜ Pendiente |
| 3.4 | **Sistema de "Motion Archetypes"** | Plantillas de movimiento reutilizables (grab, pull, insert, rack, release) | ⬜ Pendiente |
| 3.5 | **Polish automático: secondary motion** | Weapon sway, recoil anticipation, finger tension/relax, wrist roll | ⬜ Pendiente |

### FASE 4: UX Y PULIMIENTO (Semana 4-6)

| # | Tarea | Estado |
|---|-------|--------|
| 4.1 | **Modo "Sencillo" real** (no solo ocultar paneles) | ⬜ Pendiente |
| 4.2 | **Validación visual en tiempo real** (alertas: contacto antes de blend-out, alcance IK, penetración) | ⬜ Pendiente |
| 4.3 | **Presets de un clic** ("Recarga AK12 táctica", "Recarga 1911 speed", "Recarga escopeta tube") | ⬜ Pendiente |
| 4.4 | **Exportación directa a juego** (sin pasos manuales de asignar Action) | ⬜ Pendiente |
| 4.5 | **Documentación interactiva integrada** (tooltips contextuales, mini-tutoriales) | ⬜ Pendiente |

---

## 🗑️ QUÉ ELIMINAR (Código muerto / redundante)

### Archivos completos a BORRAR:
```
❌ LabReloadGenerator.cs              ← Generador denso hardcoded (206 samples, fases fijas)
❌ LabAK12Reload.cs                   ← Solo AK12, requiere piloto, 5 etapas rígidas
❌ AnimationLabAK12Reload.cs          ← UI específica AK12
❌ LabAK12ReloadRecipe.cs             ← Data structure solo AK12
❌ LabHandTemplateLibrary.cs          ← 8 guiones orientativos (reemplazar por WeaponTypeProfile)
❌ LabGestureAuthoring.cs             ← Lógica fases/timing (integrar en AutoReloadGenerator)
❌ LabTransitionAssistant.cs          ← Unificar en TransitionEngine
❌ AnimationLabGripTransitionPreview.cs ← Unificar en TransitionEngine
```

### Código a ELIMINAR dentro de archivos:
```
❌ LabPathMode.Legacy, .Smooth, .Arc, .Ease  (mantener: Linear, Cinematic, Bezier, Flow)
❌ LabFingerBlend.Legacy  (mantener: Linear, Smooth, Step)
❌ LabHandTemplate enum (8 valores) → WeaponTypeProfile
❌ LabReloadTemplate enum (5 valores) → WeaponTypeProfile.reloadPattern
❌ LabHandSource enum (Procedural/Reload/ReloadEmpty) → unificar en WeaponTypeProfile
```

---

## ➕ QUÉ AÑADIR (Nuevos sistemas)

### 1. `WeaponTypeProfile` (ScriptableObject) — **CORE**

```csharp
// Assets/AnimationLab/Runtime/WeaponTypeProfile.cs
public enum ReloadPattern {
    MagazineDropInsert,      // Pistola, Rifle, SMG: soltar cargador → insertar nuevo
    MagazineDropInsertRack,  // Rifle/AR: + accionar cerrojo si vacío
    TubeFeed,                // Escopeta: cartucho a cartucho
    RevolverSwingOut,        // Revólver: abrir tambor → expeler → cargar → cerrar
    BoltActionSingle,        // Sniper bolt: cerrojo → cartucho → cerrojo
    BeltFeed,                // LMG: cinta
    BreakAction,             // Escopeta break: abrir → expeler → cargar → cerrar
    Custom                   // Usuario define pasos
}

[CreateAssetMenu(menuName = "AnimationLab/Weapon Type Profile")]
public class WeaponTypeProfile : ScriptableObject {
    public string displayName = "AK12";
    public ReloadPattern pattern = ReloadPattern.MagazineDropInsert;
    
    // Puntos de anclaje REQUERIDOS por patrón
    public AnchorDefinition[] requiredAnchors;
    
    // Puntos OPCIONALES (para variaciones)
    public AnchorDefinition[] optionalAnchors;
    
    // Timing base (normalizado 0-1) por fase
    public PhaseTiming[] phaseTimings;
    
    // Estilos visuales compatibles
    public VisualStyleProfile[] compatibleStyles;
    
    // Configuración de manos
    public HandRole leftHand = HandRole.Support;   // Support / Manipulate / None
    public HandRole rightHand = HandRole.Firing;   // Firing / Manipulate / Charge / None
    
    // Referencias de piezas por nombre (path en prefab)
    public string magazinePath = "AK12Rig/Root/Mag";
    public string chargeHandlePath = "AK12Rig/Root/Charge";
    public string boltPath = "";
    public string shellPortPath = "";
}

[Serializable]
public class AnchorDefinition {
    public string id;              // "guard", "magazine_well", "magazine_grip", "charge_handle"
    public string label;           // "Guardia", "Pozo cargador", "Agarre cargador", "Palanca"
    public bool isRequired;
    public HandSide hand;          // Left / Right / Either
    public AnchorType type;        // Static / Dynamic / Contact
    public Vector3 defaultOffset;  // Offset por defecto desde guardia (cm)
}

[Serializable]
public class PhaseTiming {
    public string phaseName;       // "Approach", "Contact", "Extract", "Transit", "Insert", "Seat", "Rack", "Recover"
    public float startNorm;        // 0-1
    public float endNorm;          // 0-1
    public bool allowOverlap;
}
```

### 2. `VisualStyleProfile` (ScriptableObject) — **ARTE VISUAL**

```csharp
// Assets/AnimationLab/Runtime/VisualStyleProfile.cs
[CreateAssetMenu(menuName = "AnimationLab/Visual Style Profile")]
public class VisualStyleProfile : ScriptableObject {
    public string styleName = "Tactical";
    public StyleArchetype archetype = StyleArchetype.Tactical;
    
    // Curvas signature por fase
    public AnimationCurve approachCurve = AnimationCurve.EaseInOut(0,0,1,1);
    public AnimationCurve extractCurve = new AnimationCurve(new Keyframe(0,0,0,2), new Keyframe(1,1,2,0));
    public AnimationCurve insertCurve = AnimationCurve.EaseInOut(0,0,1,1);
    public AnimationCurve settleCurve = new AnimationCurve(new Keyframe(0,0,0,0), new Keyframe(0.3f,1,0,0), new Keyframe(1,1,0,0));
    
    // Parámetros de "juice" / feel
    [Range(0,1)] public float anticipation = 0.15f;      // Anticipación antes de movimiento
    [Range(0,1)] public float overshoot = 0.05f;         // Sobrepaso en llegada
    [Range(0,1)] public float settleTime = 0.2f;         // Tiempo de asentamiento (normalizado)
    [Range(0,1)] public float microJitter = 0.02f;       // Micro-vibración en contacto
    [Range(0,1)] public float weightShift = 0.1f;        // Desplazamiento de peso corporal
    [Range(0,1)] public float breathSync = 0.3f;         // Sincronía con respiración
    
    // Dedos por fase (presets)
    public GripProfile[] gripProfiles;
    
    // Arma secondary motion
    public WeaponDynamicsSettings weaponDynamics;
}

public enum StyleArchetype {
    Tactical,    // Controlado, peso, 2-manos, militar (CoD MW, Tarkov)
    Fluid,       // Fluido, continuo, elegante (Battlefield, Ghost Recon)
    Speed,       // Rápido, agresivo, competition (IPSC, Insurgency comp)
    Cinematic,   // Exagerado, dramático, slow-mo ready (cinemáticas, trailers)
    Stealth,     // Silencioso, minimalista, sin overshoot (Splinter Cell, MGSV)
    Survival     // Torpe, nervioso, improvisado (DayZ, Project Zomboid)
}
```

### 3. `AnchorPointWizard` (EditorWindow) — **UX GUIDADA**

```csharp
// Assets/AnimationLab/Editor/AnchorPointWizard.cs
// Paso a paso visual:
// ┌────────────────────────────────────────────────────────────┐
// │  ████████░░░░  PASO 1/5: TIPO DE ARMA                     │
// │  [AK12 ▼]  [Rifle]  [Pistola]  [Escopeta]  [Personalizado] │
// ├────────────────────────────────────────────────────────────┤
// │  ██████████░░  PASO 2/5: PUNTOS DE ANCLAJE                │
// │  ● Guardia (F0)          [Auto] [Manual] [Vista]          │
// │  ● Pozo cargador         [Auto] [Manual] [Vista]          │
// │  ● Agarre cargador       [Auto] [Manual] [Vista]          │
// │  ● Palanca cerrojo (opt) [Auto] [Manual] [Vista]          │
// ├────────────────────────────────────────────────────────────┤
// │  ████████████░  PASO 3/5: DEDOS POR FASE                  │
// │  Mano: [Izquierda ▼]  Slider: [Contacto][Extraer][Insertar][Soltar]│
// │  Presets: [Cargador ▼] [Cerrojo ▼] [Abierto ▼] [Personalizado ▼]│
// ├────────────────────────────────────────────────────────────┤
// │  ██████████████  PASO 4/5: ESTILO VISUAL                 │
// │  [Tactical ▼]  Preview: ▶  Descripción: "Control militar..."│
// ├────────────────────────────────────────────────────────────┤
// │  ██████████████  PASO 5/5: GENERAR Y REFINAR             │
// │  [Generar]  [Preview ▶]  [Ajustes finos]  [Guardar copia] │
// └────────────────────────────────────────────────────────────┘
```

### 4. `AutoReloadGenerator` — **MOTOR AUTOMATIZADO**

```csharp
// Assets/AnimationLab/Editor/AutoReloadGenerator.cs
internal static class AutoReloadGenerator {
    public struct GenerationSettings {
        public WeaponTypeProfile weaponProfile;
        public VisualStyleProfile styleProfile;
        public AnchorData[] anchors;           // Puntos definidos por usuario
        public GripSettings[] gripSettings;    // Dedos por fase
        public float duration;                 // Segundos
        public bool coordinateHands;           // Mano izq + pieza coordinada
        public bool includeChargeHandle;       // Si arma lo requiere
    }
    
    public struct AnchorData {
        public string anchorId;
        public Vector3 position;    // Local a guardia (metros)
        public Quaternion rotation;
        public bool hasContact;     // Si este punto es contacto con pieza
        public string contactPiecePath; // "AK12Rig/Root/Mag"
    }
    
    /// <summary>
    /// Genera toma completa: weapon curves, hand curves (ambas), piece curves, 
    /// finger keys con blends, contactos compilados, IK weights.
    /// </summary>
    public static AnimationLabTake Generate(AnimationLabTake baseTake, LabPreview preview, GenerationSettings settings) {
        // 1. Validar anchors requeridos por weaponProfile.pattern
        // 2. Calcular timings por fase desde weaponProfile.phaseTimings + styleProfile
        // 3. Generar curvas weapon (offset + rotation) con styleProfile.curves
        // 4. Generar curvas manos (ambas) con LabPathCurves (Cinematic/Bezier/Flow)
        // 5. Generar curvas piezas (magazine, charge) sincronizadas
        // 6. Compilar contactos mano-pieza → LabCoordinatedPaths.Compile
        // 7. Generar finger keys por fase con GripSettings + styleProfile.gripProfiles
        // 8. Aplicar blends Smooth/Step entre fases
        // 9. Aplicar weapon dynamics (styleProfile.weaponDynamics)
        // 10. Validar y retornar take listo
    }
}
```

### 5. `TransitionEngine` — **TRANSICIONES UNIFICADAS**

```csharp
// Assets/AnimationLab/Editor/TransitionEngine.cs
internal static class TransitionEngine {
    public struct TransitionSpec {
        public float fromTime, toTime;
        public TransitionType type;      // Approach / Pass / Grip / Release / Return
        public TransitionShape shape;    // Direct / Arc / Bezier / Contact
        public Vector3 arcOffset;
        public bool continueAtArrival;   // Suavizar con siguiente tramo
        public int settleFrames;
        // Dedos
        public bool animateFingers;
        public GripProfile targetGrip;
        public FingerBlendMode blendMode; // Smooth / Linear / Step
        public float blendDurationNorm;   // 0-1
        public int fingerStartFrame;      // Relativo a fromTime
    }
    
    /// <summary>
    /// Aplica transición a take (posición y/o dedos), genera keys intermedias,
    /// actualiza LabPathCurves, preserva canales no afectados.
    /// </summary>
    public static AnimationLabTake Apply(AnimationLabTake take, LabPreview preview, TransitionSpec spec) { ... }
}
```

---

## 🎨 REFERENCIAS VISUALES PARA ESTILOS PREDEFINIDOS

### Análisis del video referenciado (YouTube Short: BtWowPXS2og)
> **Nota:** Necesito que me proporciones más referencias o permita analizar videos. Por ahora, defino arquetipos basados en estándares AAA:

| Estilo | Referencias juegos | Características clave | Curvas signature |
|--------|-------------------|----------------------|------------------|
| **Tactical** | CoD MW2019/22, Tarkov, Ready or Not | Peso realista, 2-manos coordinadas, control de boca de fuego, micro-settle | EaseInOut + overshoot 3-5% + settle 15-20% |
| **Fluid** | Battlefield 2042, Ghost Recon Breakpoint | Continuidad C1, sin paradas, arcos amplios, peso corporal | Cinematic/Flow, anticipación 10-15% |
| **Speed** | Insurgency Sandstorm (comp), IPSC videos | Mínimo tiempo, agresivo, overshoot 8-12%, snap transitions | Bezier agresivo, Step en dedos, blendOut mínimo |
| **Cinematic** | Trailers, cinemáticas, The Last of Us 2 | Exagerado, dramático, slow-mo ready, follow-through | Overshoot 15-20%, anticipation 20%, follow-through |
| **Stealth** | Splinter Cell, MGSV, Hitman | Silencioso, minimalista, sin overshoot, control total | Linear/Smooth, micro-jitter 0, weight shift sutil |
| **Survival** | DayZ, Project Zomboid, Sons of the Forest | Torpe, nervioso, improvisado, manos temblorosas | Jitter alto, overshoot variable, timing irregular |

### Micro-comportamientos por fase (para inyectar en generador):

```csharp
// Ejemplo: Fase "Contact Magazine" - Tactical
contactPhase = {
    anticipation: 0.12f,           // 12% antes: muñeca empieza a orientar
    wristPreRotation: 8f,          // Grados de pre-rotación hacia cargador
    fingerPreCurl: 0.3f,           // 30% cierre dedos antes de contacto
    contactStiffness: 0.9f,        // Qué tan "pegado" queda al contacto
    microSettleFrames: 3,          // Frames de micro-asentamiento
    weightShift: new Vector3(-2, 1, 0), // cm desplazamiento cuerpo
    breathHold: true               // Suspender respiración durante contacto
}

// Ejemplo: Fase "Insert Magazine" - Speed
insertPhase = {
    anticipation: 0.05f,
    snapInsert: true,              // Inserción "click" instantánea
    overshoot: 0.1f,               // 10% sobrepaso hacia arriba
    fingerSnapOpen: 0.9f,          // Dedos se abren 90% al soltar
    wristFlick: 15f,               // Flick de muñeca al asentar
    minimalSettle: true            // Sin settle, directo a siguiente fase
}
```

---

## 🔧 INTEGRACIÓN CON SISTEMAS EXISTENTES (NO ROMPER)

| Sistema existente | Cómo integrar | Qué preservar |
|-------------------|---------------|---------------|
| `LabPreview` | Usar tal cual para evaluar puntos anclaje | Orden evaluación, reflexión, cleanup |
| `LabAssetIO` | Guardar takes generados normalmente | Deep copy, bake fingers, export paths |
| `LabCoordinatedPaths` | Llamar `Compile()` tras generar piece/hand curves | Contact compilation 60Hz, fade handling |
| `LabPathCurves` | Usar `Build()` con modos Cinematic/Bezier/Flow | Hermite exacto, preview=bake |
| `LabWeaponDynamics` | Aplicar `Generate()` con styleProfile.weaponDynamics | Snapshots, restore, no acumulación |
| `LabTakeValidation` | Validar take generado antes de retornar | Solo datos, sin mutar |
| `LabAuthoringTransaction` | Envolver generación en transacción | Rollback en fallo, Undo |
| `LabEmergencyRecovery` | Funciona automático con nuevos takes | Multi-nivel, session-only |
| Tests nativos | **Ejecutar TODOS tras cada cambio** | Baseline de calidad |

---

## 📦 ESTRUCTURA DE ARCHIVOS NUEVA PROPUESTA

```
Assets/
├── AnimationLab/
│   ├── Runtime/
│   │   ├── AnimationLabTake.cs              (existente, intacto)
│   │   ├── LabPathMode.cs                   (reducido: Linear, Cinematic, Bezier, Flow)
│   │   ├── LabFingerBlend.cs                (reducido: Linear, Smooth, Step)
│   │   ├── WeaponTypeProfile.cs             ← NUEVO
│   │   ├── VisualStyleProfile.cs            ← NUEVO
│   │   ├── GripProfile.cs                   ← NUEVO (reemplaza LabGripPreset + LabHandTemplate)
│   │   ├── AnchorDefinition.cs              ← NUEVO
│   │   └── ReloadPattern.cs                 ← NUEVO (enum)
│   ├── Editor/
│   │   ├── AnimationLabWindow.cs            (simplificado, delega a nuevos sistemas)
│   │   ├── AnchorPointWizard.cs             ← NUEVO (ventana paso a paso)
│   │   ├── AutoReloadGenerator.cs           ← NUEVO (motor principal)
│   │   ├── TransitionEngine.cs              ← NUEVO (unifica transiciones)
│   │   ├── VisualStyleLibrary.cs            ← NUEVO (presets Tactical/Fluid/Speed/...)
│   │   ├── WeaponProfileLibrary.cs          ← NUEVO (presets AK12/M4/1911/870/...)
│   │   ├── LabPreview.cs                    (existente, intacto)
│   │   ├── LabAssetIO.cs                    (existente, intacto)
│   │   ├── LabCoordinatedPaths.cs           (existente, intacto)
│   │   ├── LabPathCurves.cs                 (existente, intacto)
│   │   ├── LabWeaponDynamics.cs             (existente, intacto)
│   │   ├── LabTimeline.cs                   (existente, intacto)
│   │   ├── LabTakeValidation.cs             (existente, intacto)
│   │   ├── LabAuthoringTransaction.cs       (existente, intacto)
│   │   ├── LabEmergencyRecovery.cs          (existente, intacto)
│   │   └── [Tests] AnimationLab*Checks.cs   (existentes, intactos)
│   ├── PortableEditor/
│   │   └── AnimationLabTakeEditor.cs        (existente)
│   └── Authored/
│       ├── BASE CORRECTA.asset              (existente)
│       ├── Profiles/                        ← NUEVA CARPETA
│       │   ├── WeaponTypes/
│       │   │   ├── AK12.asset
│       │   │   ├── M4A1.asset
│       │   │   ├── Glock19.asset
│       │   │   ├── Remington870.asset
│       │   │   └── Custom.asset
│       │   └── VisualStyles/
│       │       ├── Tactical.asset
│       │       ├── Fluid.asset
│       │       ├── Speed.asset
│       │       ├── Cinematic.asset
│       │       ├── Stealth.asset
│       │       └── Survival.asset
│       └── [Tomas usuario]
```

---

## ✅ CRITERIOS DE ACEPTACIÓN (Definition of Done)

### Funcionales:
- [ ] Usuario crea recarga completa en **< 5 minutos** (vs 30+ min actual)
- [ ] Soporte nativo: Pistola, Rifle, Escopeta, SMG, Revólver, Sniper, Arco
- [ ] Estilos visuales producen resultados **visualmente distinguibles** y profesionales
- [ ] Puntos anclaje: **auto-detectados** (desde prefab) + **editables manualmente**
- [ ] Dedos: **auto-generados por fase** + **slider mano** + **presets editables**
- [ ] Contactos mano-pieza: **compilados automáticamente** a 60Hz sin configuración
- [ ] Transiciones: **suaves por defecto** (Cinematic/Flow), editables con Bezier
- [ ] Guardar → Exportar → Importar en juego: **sin pasos manuales extra**

### Técnicos:
- [ ] **Cero regresiones**: Todas las suites de test pasan (`Verificar laboratorio (aislado)` + 13 suites)
- [ ] `BASE CORRECTA.asset` **byte-for-byte identical** tras tests
- [ ] Preview scene **sin leaks** (cleanup correcto)
- [ ] Compilación **sin UNITY_EDITOR** en runtime
- [ ] Export package **solo paths permitidos** (LabAssetIO.ExportPaths)
- [ ] Undo/Redo **funciona en toda la cadena** (wizard → generación → ajustes)

### UX:
- [ ] Modo **Sencillo** por defecto (oculta complejidad, no funcionalidad)
- [ ] Tooltips contextuales en **cada campo** del wizard
- [ ] Validación visual **en tiempo real** (alertas rojas/amarillas en viewport)
- [ ] Preview **Antes/Después** instantáneo al cambiar estilo/parámetros
- [ ] Documentación integrada (no PDFs externos)

---

## 🚀 PRÓXIMOS PASOS INMEDIATOS

### Para empezar YA (esta semana):

1. **Ejecutar baseline completo:**
   ```powershell
   & 'C:\Program Files\Unity\Hub\Editor\6000.3.14f1\Editor\Unity.exe' -batchmode -nographics -quit -projectPath (Resolve-Path ..).Path -executeMethod CodeHelper.AnimationLab.Editor.AnimationLabChecks.Run -logFile "$env:LOCALAPPDATA\Temp\opencode\AnimationLab.Baseline.log"
   ```
   Guardar log como referencia.

2. **Crear branch de trabajo:** `feature/auto-reload-system`

3. **Implementar `WeaponTypeProfile` + `VisualStyleProfile`** (Runtime, sin dependencias Editor)

4. **Crear 2-3 perfiles de arma base:** AK12 (Rifle), Glock19 (Pistola), Remington870 (Escopeta)

5. **Crear 3 estilos visuales base:** Tactical, Fluid, Speed

6. **Probar generación manual** (script temporal) → validar con `LabPreview` → iterar curvas

7. **Luego:** `AnchorPointWizard` + `AutoReloadGenerator` integración completa

---

## 📝 NOTAS Y DECISIONES PENDIENTES

| Tema | Decisión necesaria | Impacto |
|------|-------------------|---------|
| **Referencias visuales** | ¿Tienes más videos/referencias? ¿Puedo analizar frame-by-frame? | Define curvas signature por estilo |
| **IK de dedos avanzado** | ¿Usar `LabGripTools.Fit` (geométrico) o mantener presets musculares? | Calidad contacto vs simplicidad |
| **Piezas genéricas** | ¿Soportar armas sin Mag/Charge paths fijos? (dynamic resolve) | Flexibilidad vs convención |
| **Blend entre estilos** | ¿Permitir mezclar Tactical + Speed? (weighted blend) | Complejidad UX vs potencia |
| **Audio sync** | ¿Marcadores de eventos de audio (click, rack, slap)? | Integración juego futura |
| **Multi-personaje** | ¿Retargeting a diferentes avatares/rigs? | Alcance del lab vs juego |

---

## 🎯 MÉTRICAS DE ÉXITO

| Métrica | Actual | Objetivo |
|---------|--------|----------|
| Tiempo crear recarga AK12 | ~45 min | **< 5 min** |
| Tiempo crear recarga pistola | ~30 min | **< 3 min** |
| Curva de aprendizaje (novato) | Muy alta | **Guiado 5 pasos** |
| Calidad visual (subjetiva) | Manual/experto | **Profesional auto** |
| Bugs de contacto/IK | Frecuentes | **Cero (auto-compilado)** |
| Archivos redundantes | ~15 | **0** |
| Suites de test pasando | 14/14 | **14/14 (sin regresiones)** |

---

**Documento vivo** — Actualizar conforme avanza implementación.  
**Próxima revisión:** Tras baseline + primeros 2 perfiles de arma + 3 estilos.