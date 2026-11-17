# Plan de Limpieza y Consolidación - Animation Lab

**Objetivo:** Eliminar código redundante, unificar sistemas paralelos y dejar base sólida para el nuevo sistema automatizado.

**Fecha:** 15 noviembre 2026  
**Branch sugerida:** `cleanup/consolidation-phase1`

---

## 📋 CHECKLIST DE EJECUCIÓN

### FASE 1: Análisis de dependencias (ANTES de borrar)

| # | Archivo | Referencias externas | Acción |
|---|---------|---------------------|--------|
| 1.1 | `LabReloadGenerator.cs` | `AnimationLabStudio.cs`, `LabReloadReference.cs` | Verificar → Migrar lógica útil → Borrar |
| 1.2 | `LabAK12Reload.cs` | `AnimationLabAK12Reload.cs`, `LabAK12ReloadRecipe.cs` | Verificar → Borrar (solo AK12) |
| 1.3 | `AnimationLabAK12Reload.cs` | `AnimationLabWindow.cs` (DrawAK12Reload) | Verificar → Borrar UI |
| 1.4 | `LabAK12ReloadRecipe.cs` | `AnimationLabTake.cs`, `LabAK12Reload.cs` | Verificar → Borrar |
| 1.5 | `LabHandTemplateLibrary.cs` | `LabHandTemplateInsertion.cs`, `AnimationLabHandTemplates.cs`, `LabGestureAuthoring.cs` | Verificar → Migrar guiones → Borrar |
| 1.6 | `LabGestureAuthoring.cs` | `LabHandTemplateInsertion.cs`, `LabHandTemplateLibrary.cs`, `LabReloadReference.cs` | Verificar → Migrar lógica → Borrar |
| 1.7 | `LabTransitionAssistant.cs` | `AnimationLabTransitions.cs`, `AnimationLabWindow.cs` | Verificar → Unificar en TransitionEngine → Borrar |
| 1.8 | `AnimationLabGripTransitionPreview.cs` | `AnimationLabWindow.cs` | Verificar → Unificar en TransitionEngine → Borrar |
| 1.9 | `LabPathMode` (enum) | `AnimationLabTake.cs`, `LabPathCurves.cs`, `LabTimeline.cs`, `LabWeaponDynamics.cs`, `LabCoordinatedPaths.cs`, `LabTransitionAssistant.cs`, `LabObjectAuthoring.cs` | Reducir 8→4, actualizar switch/case |
| 1.10 | `LabFingerBlend` (enum) | `AnimationLabTake.cs`, `LabFingerKeyTools.cs`, `LabTimeline.cs`, `AnimationLabTransitions.cs`, `AnimationLabGripTransitionPreview.cs` | Reducir 4→3 |
| 1.11 | `LabHandTemplate` (enum) | `AnimationLabTake.cs`, `LabHandTemplateInsertion.cs`, `LabHandTemplateLibrary.cs`, `AnimationLabHandTemplates.cs`, `LabGestureAuthoring.cs`, `LabReloadReference.cs` | Reemplazar por WeaponTypeProfile |
| 1.12 | `LabReloadTemplate` (enum) | `AnimationLabTake.cs`, `LabReloadGenerator.cs`, `LabReloadReference.cs` | Reemplazar por WeaponTypeProfile.reloadPattern |

---

### FASE 2: Eliminación ordenada (con tests tras cada paso)

#### Paso 2.1: `LabAK12Reload.cs` + `AnimationLabAK12Reload.cs` + `LabAK12ReloadRecipe.cs` + `AnimationLabAK12Checks.cs` + `AnimationLabAK12Capture.cs`
- **Riesgo:** BAJO (solo AK12, código aislado)
- **Tests:** ~~`AnimationLabAK12Checks.Run`~~ → **ELIMINADO** (tests específicos AK12 removidos)
- **Estado:** ✅ **COMPLETADO** (15 nov 2026)
  - Archivos borrados: 5
  - Referencias limpiadas en: AnimationLabTake.cs, LabAssetIO.cs, LabTakeValidation.cs, AnimationLabWindow.cs, AnimationLabHandTrial.cs, AnimationLabRecovery.cs, AnimationLabTimeline.cs, AnimationLabRecoveryRepair.cs, AnimationLabStudio.cs, AnimationLabVisualPoints.cs, AnimationLabWeaponDynamics.cs
  - Tests AK12 específicos eliminados (ya no aplican)

#### Paso 2.2: `LabReloadGenerator.cs` 
- **Riesgo:** MEDIO (referenciado en Studio)
- **Migración:** Extraer lógica de contactos/phase timing útil → `LabReloadReference.cs` o nuevo `AutoReloadGenerator.cs`
- **Tests:** `AnimationLabProceduralChecks.Run`

#### Paso 2.3: `LabHandTemplateLibrary.cs` + `LabGestureAuthoring.cs`
- **Riesgo:** MEDIO (usado por HandTemplateInsertion)
- **Migración:** Guiones → documentación/asset data; Lógica fases → `AutoReloadGenerator`
- **Tests:** `AnimationLabHandTemplateChecks.Run`

#### Paso 2.4: `LabTransitionAssistant.cs` + `AnimationLabGripTransitionPreview.cs`
- **Riesgo:** ALTO (usado en Transitions UI)
- **Migración:** Crear `TransitionEngine.cs` unificado ANTES de borrar
- **Tests:** `AnimationLabTransitionChecks.Run`, `AnimationLabGripFrameChecks.Run`

#### Paso 2.5: Reducir enums `LabPathMode` / `LabFingerBlend` / `LabHandTemplate` / `LabReloadTemplate`
- **Riesgo:** ALTO (muchos archivos)
- **Estrategia:** Mantener valores Legacy como obsoletos `[Obsolete]`, no borrar aún
- **Tests:** TODOS

---

### FASE 3: Consolidación de sistemas

| Nuevo sistema | Reemplaza a | Estado |
|---------------|-------------|--------|
| `TransitionEngine.cs` | `LabTransitionAssistant` + `AnimationLabGripTransitionPreview` | ⬜ Pendiente |
| `WeaponTypeProfile.cs` (Runtime) | `LabHandTemplate` + `LabReloadTemplate` + `LabHandSource` | ⬜ Pendiente |
| `VisualStyleProfile.cs` (Runtime) | Parámetros hardcoded en generadores | ⬜ Pendiente |
| `GripProfile.cs` (Runtime) | `LabGripPreset` + `fingerLabel` + `LabHandTemplate` | ⬜ Pendiente |
| `AutoReloadGenerator.cs` (Editor) | `LabReloadGenerator` + `LabHandTemplateInsertion` + `LabAK12Reload` | ⬜ Pendiente |

---

### FASE 4: Limpieza UI y documentación

| Archivo | Acción |
|---------|--------|
| `AnimationLabStudio.cs` | Simplificar pestaña Recargas: 1 selector modo en lugar de 3 secciones |
| `AnimationLabWindow.cs` | Eliminar `DrawAK12Reload()`, `DrawHandTemplates()` legacy, `DrawWeaponDynamics()` legacy |
| `AnimationLabBeginner.cs` | Actualizar guía interactiva al nuevo flujo |
| `GUIA_RECARGAS_POR_FOTOGRAMAS.md` | Actualizar a nuevo sistema |
| `GUIA_STUDIO_VISUAL.md` | Actualizar pestaña Recargas |

---

## 🧪 PROTOCOLO DE VALIDACIÓN TRAS CADA PASO

```powershell
# 1. Compilar en Unity (sin errores)
# 2. Ejecutar suite específica afectada
# 3. Ejecutar suite CORE
& 'C:\Program Files\Unity\Hub\Editor\6000.3.14f1\Editor\Unity.exe' -batchmode -nographics -quit -projectPath (Resolve-Path ..).Path -executeMethod CodeHelper.AnimationLab.Editor.AnimationLabChecks.Run -logFile "$env:LOCALAPPDATA\Temp\opencode\AnimationLab.Checks.log"

# 4. Verificar: [AnimationLabChecks] PASS
# 5. Si falla → Revertir / Fix / Re-test
```

**Suites por área:**
| Área | Suite |
|------|-------|
| Core/Preview/Bake/Export | `AnimationLabChecks.Run` |
| AK12 Coreografía | **Retirada del árbol activo**; los 14 PASS de `VALIDACION.md` corresponden a la copia histórica validada, no a una suite disponible ahora. |
| Authoring básico | `AnimationLabAuthoringChecks.Run` |
| Procedural/Paths/Templates | `AnimationLabProceduralChecks.Run` |
| Hand Templates | `AnimationLabHandTemplateChecks.Run` |
| Studio Visual | `AnimationLabStudioChecks.Run` |
| References/Curvas libres | `AnimationLabReferenceChecks.Run` |
| Grips/Frames/Clipboard | `AnimationLabGripFrameChecks.Run` |
| Combined/Transitions | `AnimationLabCombinedChecks.Run` |
| Dynamics/Weapon Motion | `AnimationLabDynamicsChecks.Run` |
| Usability/Home/Nav | `AnimationLabUsabilityChecks.Run` |
| Transitions/Move frames | `AnimationLabTransitionChecks.Run` |
| Safety/Simple flow | `AnimationLabImprovementChecks.Run` |
| Workshop/Pieces | `AnimationLabWorkshopChecks.Run` |

---

## 🚀 EMPEZAR: Paso 2.1 - Eliminar código solo-AK12

### Archivos a BORRAR:
```
❌ Assets/AnimationLab/Runtime/LabAK12ReloadRecipe.cs
❌ Assets/AnimationLab/Editor/LabAK12Reload.cs
❌ Assets/AnimationLab/Editor/AnimationLabAK12Reload.cs
❌ Assets/AnimationLab/Editor/AnimationLabAK12Checks.cs  (tests específicos AK12)
❌ Assets/AnimationLab/Editor/AnimationLabAK12Capture.cs (capture específico AK12)
```

### Archivos a MODIFICAR:
- `AnimationLabTake.cs` → Quitar `LabAK12ReloadRecipe ak12Reload` field
- `AnimationLabWindow.cs` → Quitar `DrawAK12Reload()`, `HasAK12Trial`, `StartAK12Trial()`, `CancelAK12Trial()`, `AcceptAK12Trial()`, `DrawAK12TrialActions()`, `AK12Reach()`
- `LabAssetIO.cs` → Quitar clonación de `ak12Reload` en `Clone()` y `Save()`
- `AnimationLabStudio.cs` → Quitar `showAK12` logic en `DrawStudioInspector()`

### Tests a actualizar:
- `AnimationLabAK12Checks.RunAll()` → Eliminar o integrar en `AnimationLabCombinedChecks.RunAll()`

---

## 📝 COMANDOS ÚTILES

```powershell
# Ver referencias de un archivo (PowerShell)
Get-ChildItem -Recurse -Filter "*.cs" | Select-String "LabAK12Reload" | Select-Object Path, LineNumber, Line

# Ver referencias de enum
Get-ChildItem -Recurse -Filter "*.cs" | Select-String "LabPathMode" | Select-Object Path, LineNumber, Line

# Ejecutar test específico
# No ejecutar: AnimationLabAK12Checks fue retirado. Consultar VALIDACION.md para el registro histórico.
```

---

## ⚠️ REGLAS DE ORO

1. **Un archivo por commit** (o grupo lógico pequeño)
2. **Tests pasan ANTES de commit** 
3. **Si algo rompe → Revert inmediato**, analizar, fixear, re-test
4. **Documentar qué se quitó y por qué** en este archivo
5. **No tocar:** `LabPreview`, `LabAssetIO`, `LabTimeline`, `LabPathCurves`, `LabCoordinatedPaths`, `LabWeaponDynamics`, `LabTakeValidation`, `LabAuthoringTransaction`, `LabEmergencyRecovery`, `AnimationLabTake.cs` (excepto fields obsoletos)

---

## 📊 PROGRESO

| Fase | Tarea | Estado | Tests | Notas |
|------|-------|--------|-------|-------|
| 1 | Análisis dependencias | ⬜ Pendiente | - | |
| 2.1 | Borrar AK12-only code | ⬜ Pendiente | `AnimationLabChecks` + `CombinedChecks` | |
| 2.2 | Borrar LabReloadGenerator | ⬜ Pendiente | `ProceduralChecks` | Migrar contact logic primero |
| 2.3 | Borrar HandTemplateLibrary + GestureAuthoring | ⬜ Pendiente | `HandTemplateChecks` | Migrar phase logic |
| 2.4 | Unificar Transitions → TransitionEngine | ⬜ Pendiente | `TransitionChecks` + `GripFrameChecks` | Crear ANTES de borrar |
| 2.5 | Reducir enums (Legacy→Obsolete) | ⬜ Pendiente | TODOS | Cuidado breaking changes |
| 3 | Crear nuevos sistemas Runtime | ⬜ Pendiente | - | WeaponTypeProfile, VisualStyleProfile, GripProfile |
| 4 | Crear AutoReloadGenerator + AnchorWizard | ⬜ Pendiente | - | |
| 5 | UI simplification | ⬜ Pendiente | `StudioChecks` + `UsabilityChecks` | |

---

**Próximo paso:** Ejecutar análisis de dependencias (Fase 1) para confirmar impacto real antes de borrar nada.
