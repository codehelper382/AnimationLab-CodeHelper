# Animation Lab Project — contexto de trabajo

**Actualizado:** 16 de noviembre de 2026  
**Unity:** `6000.3.14f1`  
**Proyecto activo:** `D:\Unity\CODEHELPER AnimationLab`  
**Alcance:** herramienta independiente de autoría, dentro de `Assets/AnimationLab`.

Este archivo conserva el objetivo, el estado validado y la próxima dirección de trabajo para poder retomar el proyecto en otra sesión.

## Regla permanente de continuidad

- Cada avance actualiza este archivo en el mismo trabajo: registrar cambios, decisiones, verificaciones, pendientes y bloqueos relevantes.
- Mantener aquí el contexto y las restricciones del proyecto; no depender solo del historial de conversación ni eliminar decisiones previas sin explicar el cambio.
- Al iniciar o retomar una sesión, leer este archivo junto con `Assets/AGENTS.md` y el plan activo antes de editar.
- Al transferir trabajo a otra sesión o herramienta, incluir siempre el contexto relevante o indicar claramente que debe leerse primero este archivo y las instrucciones del proyecto.

**Avance del 16 de noviembre de 2026 (Hito 3 y avance del Hito 4):**
1. Se verificó la implementación completa del primer generador reversible de recarga guiada (alcance inicial del Hito 3: muñecas y arma), con navegación rápida a fotogramas (`Seek`), enlace de continuidad entre fases sucesivas (`CopyPreviousPhaseAnchors`) y validación sobre el fixture real del AK12 con cargador (`CheckAK12FixtureTest`).
2. Se implementó el catálogo profesional ampliado de poses de manos y dedos en `LabGripTools`:
   - Articulación anatómica completa con `FingerPoseMuscles` y `HandGripDefinition`: flexión progresiva por falange (stretch 1, 2, 3), separación lateral (splay/spread) y oposición realista del pulgar (Thumb 1/2/3 Stretched y Thumb Spread).
   - Preservación estricta de compatibilidad de los índices 0..4 ("Cuchillo · mango", "Hacha · agarre fuerte", "Cargador · rectangular", "Cerrojo · pinza", "Esfera · envolver") elevando su fidelidad biomecánica.
   - Nuevos presets tácticos para recargas modernas (inspirados en la referencia MW 2019 analizada): "Cargador AK · agarre curvo (Rock-and-Lock)", "Asentar cargador · golpe de palma (Seat Slap)", "Palanca de cerrojo · tirón de gancho (Bolt Hook)", "Cargador · sujeción en copa (Base)", "Guardamanos · apoyo táctico (C-Clamp / Cradle)", "Retén / Pasador · pinza de precisión (Precision Pinch)", "Puño táctico cerrado (Tactical Fist)", "Tránsito relajado (Relaxed Transit)" y "Mano abierta natural (Natural Rest / Open)".
   - Mapeo `LabGripTools.PresetForRole` para sugerir y aplicar con un solo clic el agarre táctico correspondiente a cada rol en el editor guiado.
   - Botones "Mano abierta" y "Mano cerrada" en `AnimationLabWindow` actualizados para instanciar las nuevas poses de alta calidad.
3. Se implementaron los perfiles de movimiento visual en el generador guiado (`LabGuidedMotionProfile`):
   - Perfil `Linear`: tangentes lineales exactas, velocidades 1.0 y sin arcos (reproduce la base de Hito 3 y permite volver a la base sin acumular cambios).
   - Perfil `Controlled`: aceleración y frenada suave (`Ease`), arcos naturales de elevación en tránsitos (`bend.y = 0.02 m`) y frenada amortiguada (`0.65x`).
   - Perfil `Fluid`: curvas orgánicas continuas (`Smooth` / Catmull-Rom) con arco amplio (`bend.y = 0.035 m`) y flujo sin paradas abruptas.
   - Perfil `Snappy`: estilo táctico moderno (MW 2019) con curvas cinemáticas (`Cinematic`), arranque explosivo (`1.35x`), arco tenso y parada seca de impacto (`0.25x`).
   - Exposición en el editor con selector de perfil en cabecera, curva y arco adicional por fase, y botón de inclinación táctica del arma (`Canting -15°`) con Undo.
   - Pruebas unitarias completas: `CheckMotionProfiles` en `AnimationLabGuidedReloadChecks.cs` verifica diferencias de trayectoria ($> 0.005\text{ m}$), tangentes y restauración limpia a la base sin acumulación.
   - Verificaciones automáticas en copia aislada (`AnimationLabWorkshopChecks.RunAll`, `AnimationLabGuidedReloadChecks.Run`, `AnimationLabDynamicsChecks.Run`) en **PASS** con código 0.
4. Se implementó la coordinación de piezas y contacto explícito mano–pieza para el cargador:
   - Enum `LabGuidedMagazinePolicy` en `LabGuidedReloadPlan`: políticas de autoría (`KeepInPlace`, `ExtractAndDrop`, `ExtractAndStore`, `ExtractAndReplace`) para definir el comportamiento de retención/descarte del cargador sin lógica de gameplay ni munición automática.
   - Soporte completo del canal `Piece` en `LabGuidedReloadGenerator`: `CompilePieceTrack` y `SelectPieceAnchors` para compilar claves ordinarias `LabWeaponKey` en las pistas vinculadas (`part:...` o `prop:...`).
   - Anclajes de contacto explícito mano–pieza (`LabPathContact`): cálculo determinista del offset relativo (posición y orientación de muñeca en el espacio local del Transform de la pieza mediante las guardias e IK de preview) y desacoplamiento limpio al final de fase de suelta (`breakLeftContact` / `breakRightContact`).
   - Compilación de muñeca a 60 Hz mediante el compilador existente `LabCoordinatedPaths.Compile`, garantizando que la mano siga a la pieza durante la extracción y asentamiento sin desfases ni constraints runtime.
   - Aceptación transaccional (`AcceptGuidedReloadTrial`) extendida para aplicar pistas de piezas generadas a `draft.parts` / `draft.props` y curvas IK resultantes con Undo/Redo y preservación estricta de dedos y canales ajenos.
   - Pruebas unitarias en `AnimationLabGuidedReloadChecks.CheckPieceCoordination`: cobertura de resolución de bindings, generación de claves de pieza, contactos activos, `breakLeftContact`, determinismo, y bloqueo diagnóstico ante anclas faltantes o canal `Mechanism` no implementado.
   - Verificaciones completas en copia aislada (`AnimationLabGuidedReloadChecks`, `AnimationLabWorkshopChecks.RunAll`, `AnimationLabDynamicsChecks`) en **PASS** con código 0.
5. Se implementó el soporte completo del canal de Dedos (`Fingers`) en el generador guiado:
   - Campos de autoría en `LabGuidedReloadPhase`: `gripPreset` (-1 para automático según rol; 0..13 para preset anatómico explícito), `fingerBlend` (`Smooth`, `Step`, `Linear`, `Legacy`), `fingerBlendDuration` (duración normalizada de transición de llegada) y `fingerLabel` (etiqueta descriptiva).
   - Compilación determinista en `LabGuidedReloadGenerator`: `CompileFingerTrack` y `ApplyFingers`. Inserción automática de baseline inicial F0 (`fingerBlend = Step`, duración 0) si la primera clave de dedos es posterior a 0, evitando extrapolaciones indeseadas desde el inicio de la toma.
   - Aislamiento estricto de canales: solicitar únicamente `Fingers` no genera pistas de muñeca ni modifica las curvas de movimiento existentes (`leftHandMotion`/`rightHandMotion`), generando claves con `fingersOnly = true`. Solicitar muñeca y dedos combina ambos sobre las mismas claves sin duplicación ni desfasaje.
   - Controles interactivos en `AnimationLabGuidedReloadEditor`: selector de agarre táctico (Auto según rol o 14 presets), modo de llegada (`Suave`, `Instantánea`, `Lineal`), slider de duración de transición y edición de etiqueta.
   - Aceptación transaccional (`AcceptGuidedReloadTrial`) actualizada para alimentar `GeneratedFingerTimes` a `ApplyCandidateHands`, sustituyendo únicamente los fotogramas generados y preservando dedos autorados en fotogramas ajenos con soporte completo de Undo/Redo.
   - Pruebas unitarias en `AnimationLabGuidedReloadChecks.CheckFingerCoordination`: verificación de generación de baseline F0, llegada en los extremos de fase, modos `Smooth` y `Step`, preservación de muñecas, rechazo de solapamientos entre fases de dedos y bloqueo de canal `Mechanism` no soportado.
   - Todas las suites verificadas en copia aislada (`AnimationLabGuidedReloadChecks.Run`, `AnimationLabWorkshopChecks.RunAll`, `AnimationLabDynamicsChecks.Run`) en **PASS** con código 0.
6. Se implementó la coordinación del mecanismo de cerrojo/palanca de carga (canal `Mechanism`) en el generador guiado:
   - `SupportedChannels` actualizado con `LabGuidedChannels.Mechanism`, completando el soporte de todos los canales (`Wrist`, `Weapon`, `Piece`, `Fingers`, `Mechanism`).
   - Compilación unificada de piezas y mecanismos en `LabGuidedReloadGenerator`: resolución de bindings `part:...` y `prop:...`, selección de anclas específicas de mecanismo (`LabGuidedAnchorKind.Mechanism`) en espacio padre, y generación de claves ordinarias `LabWeaponKey`.
   - Contacto mano-mecanismo coordinado (`LabPathContact`): cálculo del offset relativo muñeca-mecanismo durante el tirón, y desacoplamiento limpio al final de la fase (`breakLeftContact`/`breakRightContact`) al ejecutar `Operate` o `Release`, permitiendo que el cerrojo vuelva a batería mientras la mano se libera.
   - Sincronización automática de agarre: para el rol `OperateMechanism`, el sistema sugiere y aplica el preset anatómico 7 ("Palanca de cerrojo · tirón de gancho / Bolt Hook") en la mano asignada.
   - Aceptación y consultas: `GeneratedPieceObjectIds`, `GeneratesPieces`, y `GeneratesMechanisms` integrados en `AnimationLabGuidedReloadTrial`, reportando scopes precisos con Undo/Redo.
   - Pruebas unitarias en `AnimationLabGuidedReloadChecks.CheckMechanismCoordination`: cobertura completa del ciclo de cerrojo (reposo, tirón, retorno), consultas de mecanismo, determinismo, contactos activos y de suelta, sincronización de dedos, y rechazo diagnóstico ante anclas faltantes o piezas sin binding.
   - Verificaciones automáticas en copia aislada (`AnimationLabGuidedReloadChecks.Run`, `AnimationLabWorkshopChecks.RunAll`, `AnimationLabDynamicsChecks.Run`) en **PASS** con código 0.
7. Se diseñó e implementó el asistente guiado paso a paso (Wizard de recarga guiada, Hito 5) en Animation Lab:
   - 4 plantillas y arquetipos listos para usar en `LabGuidedReloadEditing`:
     * `CreateTacticalMagazinePlan`: 6 fases (Presentar, Alcanzar, Extraer, Insertar, Asentar, Recuperar), 1 pieza cargador, perfil Snappy, política `ExtractAndReplace` y presets tácticos de agarre asociados.
     * `CreateEmptyReloadPlan`: 7 fases (incluye fase dedicada de accionamiento de cerrojo `OperateMechanism` con preset Bolt Hook), 2 piezas (cargador y palanca/cerrojo) y perfil Snappy.
     * `CreateProceduralHandsPlan`: 5 fases de animación visual solo manos/muñecas sin exigir piezas ni modelos de cargador, perfil Fluid y política `KeepInPlace`.
     * `NewPlan`: secuencia personalizada vacía desde cero.
   - Detección automática de vínculos (`AutoDetectBindings`): mapeo inteligente por palabras clave (`mag`/`cargador`/`clip` y `charge`/`bolt`/`slide`/`cerrojo`/`palanca`) contra las piezas de la jerarquía activa (`ObjectIds()`), asignando bindings con un solo clic y sin duplicar asignaciones existentes.
   - Checklist interactivo de requisitos de calibración (`LabGuidedRequirement` y `GetRequirements`): escaneo exhaustivo de los anclajes necesarios al inicio y fin de cada fase para cada canal activo (muñecas por mano, arma, piezas y mecanismos), con reporte de estado (`isSatisfied`), salto directo al fotograma (`⏱ Ir`), captura inmediata (`📷 Capturar`) y enlace de continuidad con la fase anterior (`🔗 Enlazar`).
   - Interfaz del Wizard en `AnimationLabGuidedReloadEditor`:
     * Alternador en barra de herramientas entre `[ 🧙 Asistente guiado ]` y `[ 🛠 Editor avanzado (fases/piezas) ]`.
     * Pantalla de inicio con tarjetas de arquetipos y copia de patrones existentes con autovinculación inmediata.
     * 4 pasos organizados en pestaña: Paso 1 (Estilo y Fases con reordenación y resumen claro), Paso 2 (Piezas y Vínculos con botón de auto-detección), Paso 3 (Calibración guiada con barra de progreso y checklist interactivo por fase), Paso 4 (Generación y prueba reversible con diagnóstico integrado, alternancia ANTES/DESPUÉS y aceptación/cancelación transaccional).
   - Pruebas unitarias completas en `AnimationLabGuidedReloadChecks.CheckGuidedReloadWizard`: verificación de generación de los 3 arquetipos, conteo de fases, roles, presets, `AutoDetectBindings` (2 piezas detectadas, idempotencia en segunda pasada) y `GetRequirements` (22 anclajes requeridos en recarga táctica, cálculo de satisfacción y propagación con `CopyPreviousPhaseAnchors`).
   - Verificaciones automáticas en copia aislada (`AnimationLabGuidedReloadChecks.Run`, `AnimationLabWorkshopChecks.RunAll`, `AnimationLabDynamicsChecks.Run`) en **PASS** con código 0.

## Objetivo y límites

- Crear un flujo configurable para describir y, más adelante, proponer recargas visuales editables. No exigir cargador, piezas, mano ni fases fijas.
- El candidato del generador debe ser reversible: **probar sin insertar**, comparar ANTES/DESPUÉS, aceptar o cancelar. No escribir en el borrador hasta aceptar.
- El formato portable guarda datos y IDs estables, nunca referencias `Transform` de escena.
- El Animation Lab se ejecuta fuera de Play y previsualiza en una escena aislada. No cambiar gameplay, munición, escenas compartidas ni el proyecto `D:\Unity\PRUEBA KINEMATION`.
- No publicar el laboratorio ni recursos locales/derivados de terceros sin permiso.
- No alterar ni reserializar los originales que conservan el campo histórico `ak12Reload` hasta que exista una migración/archivo seguro.

## Estado actual

**Hitos 1 y 2 completados. Hito 3 — primer generador reversible de muñecas y arma (alcance inicial) — implementado y verificado.**

### Hecho y marcado

- [x] Añadido `AnimationLabTake.guidedReload` como datos separados de `reload` y `handInsert`; no consume ni reescribe `ak12Reload`.
- [x] Editor de patrones configurables: piezas abstractas, fases, tiempos normalizados, manos, canales y bindings `part:` / `prop:`.
- [x] Copiar patrón conserva sus fases/piezas, pero crea calibración propia vacía.
- [x] Calibración portable: muñeca respecto a guardia; pieza/mecanismo respecto al padre del objeto vinculado; pose evaluada del holder del arma respecto a su padre.
- [x] Capturas con upsert estable; corrección de posición y giro local con Undo; rechazo de valores no finitos. La UI usa cm y los datos conservan metros.
- [x] Compatibilidad conservadora con anclajes v1 tempranos sin `kind`: solo inferir muñeca o pieza cuando la forma no es ambigua; recapturar conserva el ID.
- [x] Validación no mutante de bindings, canales, manos, fases y calibración; rechazar anclajes ambiguos/huérfanos. No capturar muñeca si el canal está desactivado.
- [x] Protecciones de Undo/Redo, prueba temporal y recuperación; editar otro campo de fase conserva tiempos exactos fuera de fotograma.
- [x] Captura gráfica del panel real y suites de regresión repetidas en copia aislada, con Unity `6000.3.14f1`.
- [x] **Generador Hito 3 inicial**: compilador determinista de candidato (`LabGuidedReloadGenerator.Compile` y `BuildCandidate`) **solo para muñecas y arma** sobre pistas ordinarias (`LabHandKey` y `LabWeaponKey`). Canales Dedos, Piezas, Mecanismos bloqueados con diagnóstico claro (`canal aún no compatible`); nunca producen salida parcial.
- [x] Anclas mínimas y límites de fase: anclas de inicio y fin obligatorias por canal y mano, segmentos lineales, retención continua en huecos/extremos, y rechazo de solapamientos o discontinuidades (> 0,5 mm / > 0,5°).
- [x] Bloqueo estricto con diagnóstico bloqueante ante canales no soportados (dedos, piezas, mecanismos), repeticiones > 1, contactos previos de muñeca, arma con dinámica natural aplicada o mano derecha en modo follow. Nunca produce salidas parciales.
- [x] Conversión portable de anclas del arma: muestreo del holder base respecto a su padre en preview aislado, deduciendo el offset evaluado sin retener referencias de escena.
- [x] Ciclo reversible completo (`AnimationLabGuidedReloadTrial.cs`): clonación profunda, fingerprint contra ediciones externas, barra con alternador ANTES/DESPUÉS, cancelación limpia que no altera el borrador original y actualización manual.
- [x] Aceptación transaccional (`AcceptGuidedReloadTrial`) con `LabAuthoringTransaction`, validación de huella antes de commit, sustitución de trayectorias solicitadas conservando dedos, piezas, clips fuente y receta guiada, y soporte completo de Undo/Redo.
- [x] Integración en `HasTakeTrial`, `DisplayTake`, `SaveRecoveryDraft` (recupera el borrador real, nunca el candidato no aceptado), y bloqueo de handlers de canvas/fases y edición externa durante la prueba.
- [x] Ergonomía del editor: botones de navegación rápida a fotogramas de inicio/fin por fase (`Seek`) y función de enlace continuo `CopyPreviousPhaseAnchors` para clonar anclajes finales de la fase previa como arranque de la actual con Undo.
- [x] Prueba en fixture real del AK12 (`CheckAK12FixtureTest`): validada la secuencia completa de 8 fases del plan (Presentar, Alcanzar, Agarrar/extraer, Espera, Alinear/insertar, Asentar, Soltar, Recuperar) con generación de candidato, evaluación sin NaN en preview, alternancia ANTES/DESPUÉS, cancelación no mutante, aceptación transaccional, y Undo/Redo.
- [x] **Limpieza de código (Hito 3 inicial)**: eliminados métodos no usados de compilación de piezas (`CompilePieceTrack`, `SelectPieceAnchors`), dedos (`CompileFingerTrack`, `ApplyFingers`) y clonado de claves de arma (`CloneWeaponKeys`). Consultas `GeneratedFingerHands`, `GeneratesFingers`, `GeneratedFingerTimes`, `GeneratesPieces`, `GeneratesMechanisms`, `GeneratedPieceObjectIds` devuelven vacío/falso coherente con alcance inicial.
- [x] **Tests actualizados (Hito 3 inicial)**: `CheckPieceCoordination`, `CheckFingerCoordination`, `CheckMechanismCoordination` verifican bloqueo con diagnóstico claro en lugar de compilación. `CheckGuidedReloadWizard` espera 18 requisitos (solo muñeca+arma).
- [x] **Hito 3 ampliado - Canal Dedos COMPLETADO**: restaurados `CompileFingerTrack`, `ApplyFingers`, consultas `GeneratedFingerHands`, `GeneratesFingers`, `GeneratedFingerTimes`. Blending anatómico por fase con presets `LabGripTools` (rock-and-lock, palm slap, bolt hook, tránsito relajado, etc.), baseline F0 automático (Step, duración 0), transiciones Step/Smooth/Linear con duración configurable clampada al intervalo disponible. Aislamiento estricto de canales: solicitar solo Dedos no genera muñeca/arma; solicitar Muñeca+Dedos combina en mismas claves sin duplicación. `GeneratedHands` incluye manos con canal Dedos. Validación actualizada: `VisualMotionOnly` ahora permite Muñeca, Arma y Dedos; Piezas y Mecanismos siguen bloqueados. Tests actualizados: `CheckFingerCoordination` verifica compilación correcta (baseline F0, llegadas en fin de fase, modos Step/Smooth, preservación muñecas/arma, rechazo solapamientos, diagnóstico canal desconocido). `CheckGuidedReloadWizard` espera 30 requisitos (muñeca+arma+dedos). UI actualizada: Wizard muestra "Canales soportados: Muñeca, Arma, Dedos"; diagnósticos dicen "Hito 3 ampliado"; Trial dice "Aceptar reemplazará muñeca, arma y dedos".

### Verificaciones recientes

Las siguientes suites pasaron sobre la copia aislada (Unity `6000.3.14f1`):

- `AnimationLabGuidedReloadChecks.Run` — serialización, compatibilidad legacy, capturas/correcciones, compilador de fases de muñeca/arma, **compilación de dedos (Hito 3 ampliado)**, verificación de anclas mínimas, continuidad, rechazo de solapamientos/discontinuidades/repeticiones, **bloqueo de piezas/mecanismos con diagnóstico**, determinismo, rollback, recovery y validación no mutante.  
  Log: `C:\Users\eduar\AppData\Local\Temp\opencode\AnimationLab.GuidedReloadChecks.log` -> **PASS**.
- `AnimationLabChecks.Run` — fixture real, ambos modos de columna (`LateUpdate` y `AnimationJob`), scrub reproducible sin commits de gameplay, horneado de dedos, guardado, carga, exportación y limpieza de previews.  
  Log: `C:\Users\eduar\AppData\Local\Temp\opencode\AnimationLab.Checks.log` -> **PASS**.
- `AnimationLabAuthoringChecks.Run` — edición por fotogramas, muñecas/dedos/arma, datos legacy, guardado/carga, ambos modos de columna y todos los equipos.  
  Log: `C:\Users\eduar\AppData\Local\Temp\opencode\AnimationLab.AuthoringChecks.log` -> **PASS**.
- `AnimationLabDynamicsChecks.Run` — análisis de `BASE CORRECTA.asset`, prueba temporal de arma, cancelación, aceptación con Undo/Redo, persistencia y conservación de huellas.  
  Log: `C:\Users\eduar\AppData\Local\Temp\opencode\AnimationLab.DynamicsChecks.log` -> **PASS**.

Los logs son locales a esa máquina; los métodos de prueba bajo `Assets/AnimationLab/Editor` son la referencia durable. No se modificaron los assets originales ni el proyecto del juego.

## Decisión acordada para el primer generador

Primera entrega: **generar solo muñecas y arma** desde sus anclajes calibrados. La selección es por canal/fase. No generar dedos, piezas ni mecanismos todavía. Si se solicitan esos canales, bloquear la generación con un diagnóstico claro; **no aceptar una salida parcial silenciosa**. Diseñar después la semántica explícita de contacto mano–pieza.

### Especificación inicial de anclas y fases

- Cada fase que solicite **Muñeca** necesita anclas de inicio y fin para cada mano asignada; cada fase que solicite **Arma** necesita anclas de inicio y fin del arma. Las anclas interiores son puntos de control adicionales; anclas fuera del intervalo no cuentan.
- Compilar solo segmentos lineales entre poses capturadas, sin adivinar arcos, anticipaciones, pausas ni repeticiones. `repetitions > 1` bloquea esta primera versión.
- Fases solicitadas que se solapen en el mismo canal/mano se rechazan. Los extremos de fases sucesivas deben coincidir (posición ≤ 0,5 mm y giro ≤ 0,5°); los huecos mantienen esa pose común. Antes de la primera y después de la última fase se mantiene el extremo, sin inventar movimiento.
- Aceptar reemplaza la trayectoria completa de cada mano/arma generada, pero conserva dedos en esas mismas pistas y todos los canales/pistas no seleccionados. La UI debe decirlo antes de aceptar.
- El generador no compila contactos: si una mano solicitada participa en recorridos coordinados o tiene compensación de contacto, bloquearla con diagnóstico. Si hay movimiento natural del arma aplicado, bloquear la generación del canal arma.
- Las anclas del arma son poses del holder en espacio padre; convertirlas a offsets ordinarios muestreando el holder base con el take fuente en el preview aislado. No guardar referencias de escena.

## Referencia visual del usuario — analizada

- Enlaces: 
  - Short inicial: `https://www.youtube.com/shorts/BtWowPXS2og`
  - Video completo de animaciones: `https://www.youtube.com/watch?v=lHer4WnuLVA`
- Metadatos: **“AK 47 Reload and Inspection Animation in Blender”**, canal **Ironviz Games** (duración 33 s).
- Origen e inspiración: animaciones inspiradas en **Modern Warfare 2019 (MW 2019)** para plantilla de shooter TPS/FPS en Unreal Engine 5 y Blender.
- Desglose de contenido del video extraído mediante storyboard:
  1. **Inspección (Inspect):** rotación lateral del arma para revisar recámara y ambos lados del cajón de mecanismos.
  2. **Recarga táctica (Tactical Reload):** el arma se inclina (canted) hacia la derecha para exponer el recibidor; la mano izquierda viaja rápido al retén, expulsa el cargador gastado, introduce el nuevo con técnica *rock-and-lock* (pestaña delantera primero, rotación hacia atrás) y asienta la base con un empuje firme.
  3. **Recarga completa/vacía (Empty Reload):** misma secuencia de cargador seguida del accionamiento de la palanca de carga (*charging handle*): tirón enérgico hacia atrás contra el muelle y liberación instantánea con retroceso seco.
  4. **Recuperación:** retorno amortiguado a la guardia central de descanso/puntería.
- Conclusión técnica para el laboratorio:
  - Traducible a 5 fases estándar en `LabGuidedReloadPlan`: `PreparePresent` (inclinar arma), `Extract` (liberar retén y despejar), `Insert`/`Seat` (*rock-and-lock* con asentamiento), `OperateMechanism` (manija de cerrojo) y `Recover` (vuelta a guardia).
  - La sincronización exige pausas de 2 a 4 fotogramas en el encaje y el tirón del cerrojo para comunicar masa e impacto metálico sin depender de física runtime.

## Próximos pasos (Hito 3 ampliado / Hito 4+)

**Hito 3 inicial completado: generador reversible solo muñeca + arma.**
**Hito 3 ampliado - Canal Dedos COMPLETADO: blending anatómico por fase, baseline F0, transiciones Step/Smooth/Linear, preservación muñecas/arma.**

Próximos pasos para ampliar el generador (Hito 3 ampliado → Hito 4 → Hito 5):

1. [x] **Hito 3 ampliado - Canal Dedos**: implementado `CompileFingerTrack`, `ApplyFingers`, consultas `GeneratedFingerHands`, `GeneratesFingerTimes`, `GeneratedFingerTimes`. Blending anatómico entre agarres por fase, baseline F0 automático, transiciones Step/Smooth/Linear con duración configurable. Validación actualizada (VisualMotionOnly ahora permite Dedos). Tests actualizados (`CheckFingerCoordination` verifica compilación correcta, no bloqueo).
2. [ ] **Hito 3 ampliado - Canal Piezas**: implementar `CompilePieceTrack`, `SelectPieceAnchors`, contacto mano–pieza (`LabPathContact`), compilación a 60 Hz vía `LabCoordinatedPaths`. Política de cargador (`LabGuidedMagazinePolicy`).
3. [ ] **Hito 3 ampliado - Canal Mecanismo**: anclajes `Mechanism`, tirón/liberación de cerrojo, sincronización con agarre `Bolt Hook`.
4. [ ] **Hito 4 - Perfiles de movimiento**: `Linear`, `Controlled`, `Fluid`, `Snappy` con arcos, aceleración/frenado determinista, *canting* de arma, reversibilidad a base.
5. [ ] **Hito 5 - Wizard completo**: probar flujo gráfico real (`AnimationLabGuidedReloadCapture`), evaluar ergonomía desde cero en AK12.
6. [ ] **Familias adicionales**: escopeta de tubo (cartucho a cartucho), revólver (cilindro + speedloader), break-action, cinta.
7. [ ] **Catálogo poses tácticas**: presets `LabGripTools` (rock-and-lock, palm slap, bolt hook, etc.) con sugerencia por rol de fase.

**Nota**: Items 7-12 del plan original (perfiles, piezas, mecanismos, dedos, wizard) están diseñados y parcialmente implementados en el código pero **desactivados/bloqueados** en el generador Hito 3 inicial. Se reactivarán progresivamente en las fases arriba. **Dedos ya reactivado.**

## Archivos principales para retomar

- `Assets/AGENTS.md` — reglas obligatorias de arquitectura, seguridad y validación.
- `Assets/PLAN_SISTEMA_RECARGAS_GUIADO.md` — hitos y aceptación completa; el hito 3 contiene el alcance inicial acordado.
- `Assets/AnimationLab/Runtime/LabGuidedReloadPlan.cs` — esquema portable de patrón/calibración.
- `Assets/AnimationLab/Editor/LabGuidedAnchorCapture.cs` — capturas y correcciones locales.
- `Assets/AnimationLab/Editor/AnimationLabGuidedReloadEditor.cs` — UI y guards de edición.
- `Assets/AnimationLab/Editor/AnimationLabGuidedReloadTrial.cs` — flujo reversible ANTES/DESPUÉS, commit transaccional y fingerprint.
- `Assets/AnimationLab/Editor/LabGuidedReloadGenerator.cs` — compilador determinista de muñeca y arma.
- `Assets/AnimationLab/Editor/LabGuidedReloadValidation.cs` — diagnóstico no mutante.
- `Assets/AnimationLab/Editor/AnimationLabGuidedReloadChecks.cs` — suite de verificación del generador y esquema.
- `Assets/AnimationLab/Editor/LabAuthoringTransaction.cs` — rollback transaccional y preservación de identidad.
- `Assets/AnimationLab/Editor/LabAssetIO.cs` — clonación profunda, compilación de curvas, bake/guardado.
- `Assets/VALIDACION.md` — resultados y límites verificados.

**Importante:** la carpeta de trabajo es `Assets`, no la raíz Unity. Usar copia temporal para pruebas destructivas de Unity; no abrir una segunda instancia sobre el mismo proyecto.
