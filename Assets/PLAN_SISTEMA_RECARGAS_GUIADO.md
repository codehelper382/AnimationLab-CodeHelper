# Plan de trabajo: recargas guiadas y reutilizables

**Estado:** Hitos 1 y 2 implementados y verificados; primer generador reversible de muñeca + arma (alcance inicial del Hito 3) implementado y verificado.  
**Fecha de inicio:** 15 de noviembre de 2026.  
**Última actualización:** 16 de noviembre de 2026.  
**Proyecto:** Animation Lab, Unity `6000.3.14f1`.  
**Alcance:** herramientas de autoría de `Assets/AnimationLab`; no modificar ni sincronizar el juego de `D:\Unity\PRUEBA KINEMATION`.

> **Objetivo:** partir de una toma vacía, calibrar los anclajes del arma y del rig, elegir su mecanismo y recibir una propuesta editable de recarga: movimiento, rotación, contactos, transiciones y dedos. El sistema debe ahorrar trabajo repetitivo, no prometer que una animación generada sea automáticamente profesional ni que se adapte sin calibración a cualquier modelo.

Este documento nuevo reemplaza como lista activa de organización al plan anterior `PLAN_MEJORAS_RECARGAS_AUTOMATICAS.md`, que se conserva intacto como antecedente. Las casillas solo se marcan cuando se cumple el criterio indicado; una compilación o prueba de datos no certifica por sí sola el aspecto artístico.

---

## 1. Reglas de producto y seguridad

- [ ] **Empezar desde cero:** poder preparar una recarga desde una toma vacía, sin exigir una acción de ejemplo. También permitir abrir una receta existente como copia.
- [ ] **Dejar que el usuario defina la recarga:** elegir una secuencia nueva o partir de una receta como copia; añadir, quitar y ordenar fases, seleccionar las piezas y decidir qué acciones se ejecutan.
- [ ] **Guiar sin ocultar los límites:** preguntar qué mecanismo/piezas tiene el arma; explicar anclajes requeridos, opcionales y ausentes. No inferir mecanismos por el nombre del arma ni asumir que toda arma usa cargador.
- [ ] **Separar patrón de calibración:** reutilizar fases y estilo cuando sean compatibles, pero guardar por separado las referencias específicas del arma, sus piezas, el rig y la mano elegida.
- [ ] **Elegir manos por fase:** el usuario asigna izquierda/derecha a cada tarea. Puede usar la misma mano para varias fases, repartir cargador y corredera/cerrojo entre ambas, o coordinar las dos cuando sea necesario.
- [ ] **Ofrecer alcances de generación independientes:** movimiento procedural visual como opción propia; animación de arma/piezas, mecánica y dedos como canales opcionales. No exigir que el generador cree “todo” ni sobrescribir animaciones que el usuario quiera hacer aparte.
- [ ] **Todo resultado es candidato:** Probar/Antes/Después/Aceptar/Cancelar. Cancelar no inserta claves; aceptar valida que la toma base no haya cambiado.
- [ ] **Generación transaccional e idempotente:** generar en una copia, validar antes de aplicar, admitir Undo/Redo y no acumular offsets al volver a generar.
- [ ] **Preservar lo no seleccionado:** no sustituir clips fuente, la otra mano, otras piezas ni pistas fuera del alcance confirmado en la interfaz.
- [ ] **Sin constraints permanentes:** compilar contactos a curvas reproducibles por el preview y el bake existentes. No añadir un grafo, un solver de recargas ni dependencias runtime de autoría.
- [ ] **Sin cambios implícitos al juego:** Guardar crea una copia portable; exportar e importar siguen siendo explícitos y asignar la acción en el juego requiere una decisión aparte.
- [ ] **Proteger originales y recuperación:** no editar automáticamente `BASE CORRECTA.asset`, recetas originales ni `Recovery_*`; conservar copias profundas y datos de recuperación.
- [ ] **No eliminar sistemas ni renumerar enums en las primeras fases.** Una retirada futura requiere migración, pruebas de compatibilidad y un reemplazo validado.

### Unidades y convenciones

- La interfaz expresa desplazamientos en centímetros; los datos internos de posición usan metros.
- Los tiempos de las claves son normalizados `[0,1]`; los ejemplos de este plan usan fotogramas a 60 fps.
- Anclajes y desplazamientos son locales a la guardia, pieza o padre correspondiente, no posiciones mundiales persistentes.
- Las referencias guardadas deben ser datos portables (identificadores/rutas y valores), nunca `Transform` de una escena.
- No asumir que los ejes locales de flexión, el espejo izquierda/derecha o las proporciones son universales.

---

## 2. Inventario inicial que debe guiar el diseño

### Capacidades reutilizables encontradas

- `AnimationLab/Runtime/AnimationLabTake.cs` ya contiene pistas separadas de manos, arma, piezas y objetos de referencia, anclajes, contactos y recetas de inserción.
- `LabPathCurves` y `LabCoordinatedPaths` proporcionan recorridos y compilación de contacto mano–pieza; los puntos coordinados se muestrean a 60 Hz.
- Hay una inserción SOLO MANOS, un asistente avanzado de tres referencias para mano + una pieza, edición de fases/transiciones, dedos por separado, candidatos temporales, validación y operaciones de guardado/bake/exportación.
- `LabProp` representa primitivas de referencia editables. No equivale a una nueva pieza de cargador con rig/clip exportable.
- El fixture documentado es AK12, puños y cuchillo. Elegir una plantilla no incorpora modelos ni rigs de pistola, escopeta, revólver u otras armas.

### Resultado de la auditoría estática: coreografía AK12 histórica, retirada del árbol activo

- `VALIDACION.md` registra que la coreografía de cinco etapas se probó el 15 de noviembre de 2026 en una copia aislada: catorce suites PASS, prueba gráfica y ejemplo entregado. El SHA256 del asset actual coincide con el documentado: `8A6AC8EA37FFD127C414F863BBB12237F6E90A2722F9B689124BA910A84EABC1`.
- `LIMPIEZA_Y_CONSOLIDACION.md` marca como completado el borrado del código AK12 específico y de sus referencias. Esto coincide con el árbol activo: no contiene `LabAK12Reload`, `AnimationLabAK12Reload`, `LabAK12ReloadRecipe` ni `AnimationLabAK12Checks`; `AnimationLabTake` y `LabAssetIO.Clone` tampoco declaran/copían `ak12Reload`.
- Se encontró una copia temporal anterior (13 de noviembre) con una versión de esas clases. No es la copia exacta cuya validación del día 15 describe `VALIDACION.md`; la carpeta de entrega actual conserva documentación, imágenes y video, pero no el código fuente final.
- El asset entregado y dos assets `Recovery_*` todavía serializan `ak12Reload` como receta configurada/aplicada. La definición histórica de ese campo está en la copia temporal; el esquema activo no la conoce.
- En una copia del asset, Unity conservó el campo al copiar/importar, pero lo eliminó al ejecutar `AssetDatabase.ForceReserializeAssets`; el `AnimationLabTake` cargado y `LabAssetIO.Clone` tampoco lo exponen. El original no fue abierto ni guardado. Hasta definir una recuperación/migración, no volver a guardar ni reserializar los tres originales con el esquema activo.

**Conclusión:** no fue solo documentación adelantada: la función existió, se verificó en otra copia y el plan de consolidación registra su retirada. No se restaurará código automáticamente desde la copia temporal antigua ni se eliminarán los datos serializados. La decisión de recuperarla o reemplazarla por el sistema configurable de este plan queda abierta.

---

## 3. Alcance incremental por hitos

### Hito 0 — Alinear documentación, código y assets (completado en copia aislada)

- [x] Localizar evidencia de la implementación y registrar su estado: entrega/validación histórica del 15 de noviembre y código pre-final en `C:\Users\eduar\AppData\Local\Temp\opencode\AnimationLab.UXVerification.20261113-233916`; no se halló la copia fuente final.
- [x] Comparar estáticamente esa versión con `AnimationLabTake`, `LabAssetIO.Clone`, recuperación, validación y código activo: el esquema/clonación actuales no incluyen `ak12Reload`.
- [x] Identificar `ak12Reload` como `LabAK12ReloadRecipe` en la versión histórica; el asset entregado y dos respaldos mantienen valores serializados que el esquema activo no declara.
- [x] Fijar la fuente de verdad para la capacidad actual: código del árbol activo. La validación histórica no implica que la función siga instalada.
- [x] Actualizar las guías/instrucciones que aún presentaban la coreografía como función activa; conservar `VALIDACION.md` como historial y añadir una nota de estado actual.
- [x] Probar en una copia la carga, clonación y reserialización forzada: se confirma que Unity descarta `ak12Reload` al reserializar y que el clon actual no lo incluye.
- [x] Verificar por separado copias de ambos `Recovery_*`: al copiar/importar el YAML conserva el campo; `LabAssetIO.Clone` lo omite y la reserialización forzada lo elimina.
- [x] Registrar baseline del árbol de código activo en copia temporal: `AnimationLabChecks.Run`, ambos modos de columna, `[AnimationLabChecks] PASS`, salida 0. Log: `C:\Users\eduar\AppData\Local\Temp\opencode\AnimationLab.LegacyAudit.Baseline.log`. El proyecto no tiene Git, así que se identifica por la copia `AnimationLab.AK12LegacyAudit.20261115` y sus logs.
- [x] Adoptar estrategia provisional: conservar intactos asset y recuperaciones históricas; no reserializarlos ni depender de su campo en la nueva herramienta. Si se decide migrar/restaurar, hacerlo desde copias con conversión explícita y aprobación.

**Aceptación del hito:** las guías distinguen la entrega histórica del código activo; las tres copias confirman el riesgo de pérdida del campo; el baseline del código activo pasó sin modificar los originales. El Hito 0 queda cerrado.

### Hito 1 — Definir datos y vocabulario (sin generar movimiento)

- [x] Definir el vocabulario y el modelo lógico v0.1 en esta sección: patrón editable, piezas abstractas, fases temporizadas, asignación explícita de manos, canales y calibración separada.
- [x] Decidir que la secuencia es una lista ordenada definida por el usuario; quitar/reordenar fases es válido y ninguna familia obliga a tener cargador, extracción o suministro.
- [x] Separar fase/rol semántico de su etiqueta e ID estable: los roles comunes ayudan a orientar, pero se permite `Personalizada`.
- [x] Separar el patrón reutilizable de las referencias calibradas para equipo/rig; la toma portable guardará snapshots por valor para que siga siendo reproducible sin el asset de origen.
- [x] Definir que las piezas abstractas pueden referirse a piezas reales del equipo o a diseños de referencia del laboratorio, indicando que un diseño `prop:` no es un objeto del juego ni implica bake exportable.
- [x] Definir validación pura de datos y diagnósticos, sin corregir, importar, evaluar preview ni mutar assets.
- [x] Implementar las clases serializables/versionadas, clonación profunda, rollback de transacciones y persistencia de recuperación JSON, sin cambiar `reload`, `handInsert` ni enums existentes.
- [x] Implementar validación estructural y diagnósticos de compatibilidad: IDs, fases, intervalos/solapamientos, manos, canales, bindings/rutas, referencias reutilizadas y valores finitos; conectar errores estructurales con Revisar/Guardar.
- [x] Probar serialización JSON y de asset, clonación, rollback, recuperación y validación no mutante en copia aislada. Suite `AnimationLabGuidedReloadChecks.Run`.
- [x] Repetir `AnimationLabChecks.Run` y `AnimationLabAuthoringChecks.Run`: PASS, ambos modos de columna. Las pruebas ejecutaron solo en la copia temporal.

**Aceptación del hito:** una ficha real se serializa, clona, guarda y recupera sin referencias de escena; el validador distingue patrón, calibración y compatibilidad sin mutar datos. Hito 1 completado. No se genera movimiento todavía.

#### Esquema de autoría v0.1

El sistema nuevo se llamará **Recarga guiada** en la interfaz y no reutilizará `AnimationLabTake.reload` ni `LabReloadRecipe`: ese objeto conserva el flujo avanzado existente de tres referencias y su semántica actual. Se añadirá un bloque independiente versionado al take únicamente cuando se implemente este esquema. No restaurar ni intentar inferir la receta histórica `ak12Reload`.

**1. Patrón de secuencia (independiente del rig)**

- Identidad estable de patrón (`patternId`) y nombre editable. Los IDs de fase y pieza son cadenas estables generadas al crear cada elemento; renombrar no cambia identidad.
- Lista ordenada de piezas abstractas, cada una con `id`, nombre, clase semántica y cantidad/repeticiones sugeridas. Clases v0.1: cargador extraíble, tambor/cilindro, cartucho/unidad, corredera, cerrojo, palanca/manija y personalizada.
- Lista ordenada de fases. Cada fase contiene `id`, rol semántico, nombre visible, inicio/fin normalizados, repetición, pieza objetivo opcional, acción visual sobre esa pieza, asignación de mano y canales habilitados.
- Roles orientativos: preparar/presentar, alcanzar, agarrar, liberar, extraer, suministrar, alinear, insertar, asentar, accionar, esperar, recuperar y personalizada. La lista no prescribe una coreografía; ausencias y reordenamiento son datos válidos.
- Acción visual de pieza por fase: sin cambio, tomar, sostener, liberar, extraer, transferir, insertar, asentar, accionar o personalizada. Es una instrucción de autoría, no una orden de gameplay ni una transición de munición. Cada pieza (por ejemplo, cargador usado y cargador de reemplazo) tiene identidad propia; el sistema no duplica ni instancia una segunda pieza.
- Asignación de mano explícita: ninguna, izquierda, derecha o ambas. No hay asignación automática ni espejo. `Ninguna` permite pausas/fases solo de pieza o arma; los roles que requieran contacto se validarán aparte.
- Canales por fase: muñeca procedural de la(s) mano(s) asignada(s), dedos de esa(s) mano(s), arma, pieza y mecanismo. Desmarcar un canal significa **no escribirlo**; no significa borrar su animación existente.
- Alcance general de generación: **Movimiento visual solamente** permite muñecas, arma y dedos; **Canales elegidos** permite además piezas y mecanismos. Un plan visual-only con fases que soliciten piezas/mecanismos se marca como inválido; el validador no elimina esos canales automáticamente.
- Solapamiento: los intervalos pueden solaparse solo si las fases involucradas lo declaran expresamente. La repetición no crea objetos ni munición por sí sola; solo describe una acción visual repetida sobre referencias disponibles.

**2. Calibración (dependiente del equipo/rig)**

- Se guarda separada del patrón: `equipmentId`, identificador de rig/avatar si el autor lo proporciona, bindings `pieceId → objectId` y anclajes/poses de referencia identificados por tipo, rol, fase, mano y tiempo.
- Un binding puede apuntar a una pieza de equipo (`part:<ruta>`) o a un diseño de laboratorio (`prop:<id>`). La ruta/ID se resuelve en la escena de preview; nunca se persiste un `Transform`.
- Posición y rotación de cada referencia se expresan en el espacio local declarado: muñeca respecto a guardia; pieza/mecanismo respecto al padre de la referencia vinculada; arma respecto al padre de su holder evaluado en el fotograma. La calibración no anima ni hornea la raíz visual/holder. No convertir una captura local en anclaje mundial.
- La ficha distingue “referencia resuelta”, “referencia ausente” y “diseño de laboratorio solamente”. Una `prop:` sirve para probar forma/recorrido visual, pero no se presenta como pieza real exportable ni como munición instanciada.
- Mano disponible, peso de binding/IK, guardia, objetivo, pieza y compatibilidad se diagnostican para el equipo/rig activo; la validación del patrón por sí sola no certifica esos requisitos.

**3. Persistencia y separación**

- Los patrones reutilizables y las calibraciones pueden vivir como assets de perfil independientes, pero el take guarda un snapshot de los valores usados (IDs, nombres, fases, bindings, referencias locales y versión). Así, cambiar el perfil después no cambia una toma guardada.
- En el take no se guardan referencias a objetos de escena, componentes, estados de gameplay ni dependencias editor-only. Los perfiles son sugerencias de autoría; el Action/runtime sigue recibiendo solo los canales ordinarios ya soportados.
- Incorporación aditiva a `AnimationLabTake`; no renombrar/eliminar enums/campos viejos ni reconstruir por defecto `reload` o `handInsert`. La clonación, recuperación JSON, guardado y exportación deben copiar el nuevo bloque explícitamente.
- Versión inicial `1`. Migrar versiones solo en una copia temporal y con conversión explícita; no ejecutar `ForceReserializeAssets` sobre assets de autoría existentes como parte de una migración.

**4. Reglas del validador v0.1**

- Comprobar IDs no vacíos/únicos, nombres no vacíos, versión conocida, listas no nulas y números finitos.
- Para cada fase: `0 ≤ start < end ≤ 1`, repetición dentro del rango configurado, mano/canales coherentes y pieza objetivo existente cuando el canal de pieza/mecanismo la requiera. El modo visual-only no puede combinarse con canales de piezas/mecanismos.
- Rechazar solapamientos no autorizados. Permitir fases omitidas y `Custom`; no exigir cargador, cilindro, cartucho, corredera ni cerrojo por nombre de equipo.
- Comprobar resolución de bindings y espacios de anclaje por separado de la validez estructural; informar rutas ausentes/incompatibles sin modificarlas.
- Advertir si una fase pide dedos/muñeca sin mano, si hay contacto sin objetivo calibrado o si usa `prop:` para un resultado que el usuario pretende hornear como pieza real.
- La inspección recibe datos y contexto de compatibilidad de solo lectura, produce errores/avisos localizables por ID y no ordena listas, rellena defaults, captura poses ni guarda assets.

### Hito 2 — Flujo guiado y definido por el usuario

- [x] Añadir un panel **Recarga guiada** dentro de Recargas, sin quitar el flujo avanzado ni las plantillas actuales.
- [x] Paso 1: crear una secuencia nueva o copiar el patrón de otra toma; la copia conserva fases/piezas, pero empieza con calibración vacía para el equipo activo.
- [x] Paso 2: añadir, nombrar, clasificar y quitar piezas abstractas; asociar opcionalmente cada pieza a una ruta `part:` o referencia `prop:`.
- [x] Paso 3: añadir/quitar/reordenar fases; editar nombre/rol, intervalo por fotogramas, pieza objetivo, acción visual y solapamiento explícito.
- [x] Paso 4: asignar ninguna, izquierda, derecha o ambas por fase; no se espejan poses automáticamente.
- [x] Paso 5: elegir **Movimiento visual solamente** o **Canales elegidos**. Conflictos de modo visual-only se diagnostican y no se eliminan automáticamente.
- [x] Añadir captura de muñeca, pieza/mecanismo vinculado y pose evaluada del holder del arma en la fase/fotograma actual; cada anclaje usa upsert estable, guarda valores en su espacio local y no serializa `Transform`.
- [x] Permitir corregir posición local (cm en UI, metros en datos) y giro Euler local de cada anclaje, con Undo y rechazo de números no finitos.
- [x] Diagnosticar manos no disponibles, bindings ausentes/reutilizados, referencias `prop:` no exportables y fases/canales sin anclaje correspondiente.
- [x] Mantener compatibilidad de calibraciones v1 tempranas sin el campo `kind`: inferir solo muñeca en guardia con mano o pieza local a padre con binding; el validador no muta los datos y recapturar actualiza el ID anterior.
- [x] Rechazar capturas de muñeca con ese canal inactivo y formas legacy ambiguas o anclajes huérfanos; conservar tiempos normalizados fuera de fotograma al editar otros campos. Cubierto también Undo/Redo y bloqueo de mutaciones durante pruebas/recuperación.
- [ ] Revisar visualmente los valores capturados por equipo/rig y determinar las transformaciones del generador antes de habilitar bake.
- [ ] Paso 7: editar trayectorias y pausas visuales en preview, con transiciones coherentes entre canales seleccionados.
- [ ] Paso 8: generar candidato, recorrerlo, editarlo y aceptar/cancelar; Guardar como nueva copia.
- [x] Mantener accesibles los flujos avanzados mano + pieza e inserción SOLO MANOS.

**Aceptación del hito:** desde una toma vacía se puede crear una secuencia propia sin abrir una toma modelo; también se puede comenzar desde una receta en copia. La UI deja ver la asignación de manos y el alcance elegido; si faltan datos, informa qué fase/canal no puede generar y no produce una animación parcialmente aplicada.

### Hito 3 — Generador mínimo de fases (una familia primero)

**Alcance inicial acordado:** la primera prueba reversible generará únicamente muñecas y arma a partir de sus anclajes calibrados. Dedos, piezas y mecanismos quedan fuera de este primer generador; si se solicitan en una fase, deben producir un diagnóstico bloqueante y no un resultado parcial. La coordinación mano–pieza se diseñará después con contactos explícitos. Esta primera pasada no completa por sí sola la aceptación final del hito 3.

- [x] Decidir el alcance inicial: muñeca + arma en candidato reversible; otros canales se bloquean si están seleccionados.
- [x] Especificar los límites de fase y las anclas mínimas: inicio/fin por canal y mano solicitados; anclas interiores opcionales; segmentos lineales; retener la pose en huecos y extremos; rechazar solapamientos, discontinuidades y repeticiones no soportadas.
- [x] Definir preservación/límites: se reemplaza la trayectoria completa de las pistas seleccionadas conservando dedos; los canales no seleccionados permanecen intactos. Bloquear muñecas con contactos/compensación y arma con dinámica aplicada hasta que el generador pueda recompilarlos.
- [x] Definir conversión del arma: muestrear el holder base con el take fuente y convertir anclas padre-locales a offsets ordinarios; no persistir referencias de escena.
- [x] Diseñar el ciclo candidato: snapshot profundo del borrador, fingerprint contra ediciones externas, vista ANTES/DESPUÉS, regeneración determinista, cancelar sin insertar y aceptación solo si el borrador base sigue idéntico.
- [x] Aceptar transaccionalmente solo las pistas de muñeca/arma solicitadas, conservando las demás pistas, dedos, piezas, clips fuente y receta guiada.
- [x] Usar el fixture real que tenga cargador extraíble (AK12 `Mag`) para revisar la primera prueba; el candidato inicial no animará esa pieza.
- [ ] En una ampliación posterior, dejar elegir entre movimiento procedural de manos/arma y generación opcional de piezas/mecánica/dedos; no obligar a crear las animaciones restantes.
- [ ] En esas ampliaciones, compilar solo los canales seleccionados a pistas ordinarias y preservar los demás; no añadir una lista de fases que el juego/runtime deba ejecutar.
- [ ] Cuando se incorpore movimiento de piezas, coordinar la mano asignada mientras haya contacto y cortarlo en el punto definido, antes de perder peso por fundido.
- [ ] Preguntar qué ocurre con el cargador usado: se queda, se extrae y conserva, se suelta, se guarda o se reemplaza. Cada opción visual requiere una representación compatible.
- [ ] Preguntar si se usará la misma pieza, una segunda pieza existente o solo el movimiento procedural de manos. No simular un cargador nuevo distinto moviendo la misma pieza de ida y vuelta.
- [ ] Permitir asignar la manipulación de la corredera/cerrojo a la misma mano que el cargador o a la otra; generarla solo si el usuario selecciona esa fase y existe su referencia.
- [ ] Soportar fases de soltar, insertar, asentar y recuperar solo en los canales elegidos; conservar la otra mano cuando no se haya asignado.
- [x] Hacer que re-generar con los mismos datos produzca el mismo candidato y que cambiar una fase no destruya canales ajenos.

**Aceptación del hito:** resultado determinista, editable, cancelable y reversible; contacto y bake coinciden; una segunda generación no duplica claves ni offsets.

### Modo de movimiento procedural visual

Debe poder usarse como resultado completo por sí mismo, aunque el usuario quiera animar piezas o mecanismos por separado:

- [ ] Crear movimiento de muñeca/arma con anticipación, orientación, trayectoria, frenada, pausas, asentamiento y recuperación según fases elegidas.
- [ ] Permitir seleccionar qué canales se generan (por ejemplo muñecas solamente, muñecas + arma, o incluir dedos); las pistas desactivadas no se sustituyen.
- [ ] Buscar un acabado visual cuidado mediante perfiles y controles editables, sin afirmar que el algoritmo garantiza calidad profesional en todo rig.
- [ ] Dejar que el usuario añada después la caída/cambio de cargador, cartuchos, corredera, cerrojo u otras animaciones manuales sin perder el movimiento procedural aceptado.
- [ ] Ofrecer como extensión la coreografía completa, coordinando las piezas únicamente cuando existan y el usuario lo solicite.

### Hito 4 — Perfiles de movimiento y ajustes artísticos

- [ ] Añadir perfiles descriptivos y editables (por ejemplo, controlado, fluido y rápido), sin atribuirles calidad profesional garantizada ni imitar una obra concreta.
- [ ] Exponer intensidad/tiempo de anticipación, duración por fase, arco, giro, frenada, asentamiento y recuperación, con límites seguros y valores iniciales moderados.
- [ ] Permitir ajustar inicio/fin y pausas en fotogramas, incluido un control para presentar/esperar antes de insertar.
- [ ] Asociar presets de dedos por fase y mano; mantener edición manual y transiciones suave/lineal/instantánea donde sean compatibles.
- [ ] Usar comportamiento determinista. Sin micro-jitter por defecto; no añadir ruido durante un contacto estable.
- [ ] Mantener previsualización Antes/Después, misma interpolación en preview y bake, y sin aplicar fundidos dos veces.
- [ ] Revisar silueta y contactos en primera persona y vista externa. Las pruebas automáticas no certifican arte, ausencia de penetraciones ni naturalidad.

**Aceptación del hito:** dos perfiles producen ritmos/curvas claramente distintos y editables sobre el mismo patrón; el usuario puede volver a valores base sin acumular cambios.

### Hito 5 — Ampliar mecanismos y cobertura real

Priorizar por mecanismos y rigs disponibles, no por el número de nombres en un menú:

1. [ ] Cargador extraíble: pistola, rifle o SMG según la calibración y las piezas disponibles; incluir política para cargador usado y nuevo.
2. [ ] Corredera/cerrojo: fase independiente, con mano seleccionable y guía recta cuando la mecánica lo requiera.
3. [ ] Escopeta de tubo: insertar cartuchos uno por uno, definir cantidad/repeticiones y decidir si se anima la corredera/bombeo.
4. [ ] Revólver: abrir/cerrar tambor y cargar cartuchos individualmente o con speedloader; no etiquetar el tambor como si fuera un cargador desmontable.
5. [ ] Break-action, alimentación por cinta u otros mecanismos solo cuando existan rig, piezas, contactos y caso de prueba.
6. [ ] Perfil personalizado: fases y piezas que el usuario pueda añadir, quitar, reordenar, nombrar y asignar a una mano.

**Aceptación de cada familia:** al menos un equipo real con Transform/piezas compatibles, calibración documentada, prueba de guardado/bake, revisión visual y suites de regresión. No marcar soporte por mostrar el tipo en un desplegable.

---

## 4. Secuencia de ejemplo para validar el flujo

Ejemplo ilustrativo a 60 fps para una recarga de cargador extraíble. Los tiempos son ajustables; no son una toma validada ni una plantilla universal.

| Fotogramas | Fase | Resultado editable |
|---|---|---|
| F0 | Base | Guardia, arma y dedos en estado inicial. |
| F1–F12 | Presentar | Anticipar y orientar el arma de forma acotada si el perfil lo permite. |
| F13–F25 | Alcanzar | La mano manipuladora viaja al punto de agarre. |
| F26–F42 | Agarrar / extraer | Cerrar dedos, mantener contacto y despejar el cargador usado según la política escogida. |
| F43–F73 | Presentar suministro / espera | Sostener la referencia de suministro durante **30 fotogramas**; la pausa debe poder cambiarse o quitarse. |
| F74–F92 | Alinear / insertar | Alinear antes del contacto; coordinar muñeca y cargador nuevo si existe como pieza independiente. |
| F93–F99 | Asentar | Pausa breve, giro y presión configurables; no introducir rebotes obligatorios. |
| F100–F108 | Soltar | Abrir dedos y cortar el contacto en el momento indicado. |
| F109–F120 | Recuperar | Volver al apoyo/guardia con una transición editable. |

El asistente debe poder omitir extracción, suministro, asentamiento o recuperación cuando el patrón no las use; no debe rellenar fases incompatibles con movimiento inventado.

---

## 5. Qué añadir, reutilizar y qué no quitar todavía

| Área | Propuesta | Decisión |
|---|---|---|
| Perfiles de mecanismo | Datos de fases, orden, opcionalidad, roles y requisitos | Añadir tras resolver versión/esquema y elegir dónde viven los datos. |
| Calibración del rig | Rutas/IDs de piezas y anclajes locales por equipo | Añadir; nunca guardar referencias de escena. |
| Asistente | Flujo guiado desde toma vacía y resumen de compatibilidad | Añadir dentro de Recargas o integrado al Studio; no crear otra ventana sin necesidad. |
| Generación | Candidato transaccional sobre pistas actuales | Añadir incrementalmente; reutilizar `LabPathCurves`, `LabCoordinatedPaths`, `LabTimeline`, `LabTakeValidation` y transacciones existentes cuando encajen. |
| Perfiles visuales | Ajustes deterministas de curva, tiempo, giro y dedos | Añadir como presets editables; documentar que son bases, no garantía artística. |
| Alcance de generación | Movimiento procedural visual o coreografía con canales adicionales | Selector explícito; los canales no elegidos permanecen intactos. |
| Asignación de manos | Mano izquierda/derecha por fase, con soporte para una o ambas manos | Decisión del usuario; sin inferencia ni espejo automático. |
| Piezas faltantes | Duplicado visual separado o mensaje de limitación | Requiere asset/rig real; una primitiva de referencia no resuelve exportación. |
| `LabReloadGenerator`, SOLO MANOS, asistente de transiciones, modos de curva/dedos | Conservar mientras haya tomas, documentación o pruebas que dependan de ellos | **No eliminar ni fusionar ahora.** Primero demostrar sustitución, migración y compatibilidad. |
| Enums y formato portable | Mantener valores y serialización existentes | No renumerar ni borrar modos legacy; añadir valores al final si se justifica y probar assets antiguos. |
| Acción/runtime del juego | Ningún cambio automático | Fuera del alcance de este plan. |

### Condición para retirar una herramienta en el futuro

- [ ] Identificar llamadas, assets, recetas guardadas, recuperación, documentación y suites que la usan.
- [ ] Diseñar migración no destructiva y probarla sobre copias de recetas antiguas.
- [ ] Mantener lectura de valores legacy o convertirlos explícitamente sin alterar su resultado.
- [ ] Sustituir primero la función y pasar todas las suites relacionadas.
- [ ] Obtener aprobación antes de borrar archivos o cambiar el flujo por defecto.

---

## 6. Cobertura técnica y visual

### Pruebas de datos y transacciones

- [ ] Anclajes faltantes/incompatibles, equipo cambiado, fase inválida, NaN/Infinity y duración fuera de rango.
- [ ] Generar → cancelar sin cambios; generar → aceptar → Undo/Redo; rechazar candidato obsoleto si cambió la toma base.
- [ ] Repetir generación con datos iguales y confirmar determinismo e idempotencia.
- [ ] Confirmar que se preservan mano/piezas/canales/clips no seleccionados y que solo se reemplaza el alcance anunciado.
- [ ] Clonar, recuperar, guardar y exportar; confirmar que perfiles y receta no contienen objetos de escena ni dependencias editor-only en el formato runtime.
- [ ] Mantener resultados de tomas legacy y el orden de evaluación/limpieza de `LabPreview`.

### Pruebas de interpolación, contactos y bake

- [ ] Probar contacto mano–pieza durante traslación y giro, con fundidos y puntos fuera de la cuadrícula.
- [ ] Comparar preview con clip bakeado; los contactos deben terminar antes del inicio del fundido que reduzca el peso requerido.
- [ ] Comprobar dedos por fase, poses iniciales, transiciones Step/suaves y ausencia de mezcla duplicada.
- [ ] Comprobar que editar una pieza requiere recompilar su mano cuando corresponda, y que el asistente no presenta una constraint runtime inexistente.

### Revisión visual manual

- [ ] Reproducir lento y a velocidad normal; inspeccionar inicio, contactos, pausas y salida.
- [ ] Revisar primera persona, vista externa, mirada/Aim y ambas manos.
- [ ] Comprobar silueta, alcance, penetraciones visibles, orientación de palma/dedos y control del arma.
- [ ] Registrar rig, avatar, referencias, perfil, duración y limitaciones; una suite de datos no reemplaza esta revisión.

Usar las suites específicas del flujo que se toque y las verificaciones globales descritas en `Assets/AGENTS.md`. No abrir otra instancia de Unity sobre el mismo proyecto; para capturas gráficas usar copia temporal. No se ejecutó Unity como parte de la creación de este plan.

---

## 7. Criterios de finalización del primer lanzamiento

- [ ] Usuario nuevo crea una toma de recarga desde vacío siguiendo el asistente, sin depender de un asset ejemplo.
- [ ] El usuario puede construir y ordenar sus fases, especificar piezas, asignar mano por fase y elegir qué ocurre con el cargador/cartuchos usados.
- [ ] El sistema explica los anclajes y piezas necesarios y permite dejar fases opcionales fuera, incluida la fase de cargador en armas que no la usan.
- [ ] El usuario puede generar solo movimiento procedural visual y reservar las animaciones de piezas/mecánica para edición manual.
- [ ] Un mecanismo soportado genera movimiento de arma/mano/pieza/dedos editable, con pausas por fotograma.
- [ ] La generación es determinista, transaccional, compatible con Undo/Redo y no cambia assets originales.
- [ ] Preview, guardado y bake coinciden en curvas y contactos dentro de las tolerancias probadas.
- [ ] Los flujos anteriores siguen funcionando y las recetas existentes pasan sus verificaciones.
- [ ] La documentación distingue perfiles orientativos, equipos realmente probados y limitaciones visuales.
- [ ] El usuario puede refinar movimiento/rotación/dedos; “profesional automático” no se usa como criterio técnico de aceptación.

**No son criterios aceptables:** “funciona para cualquier arma”, “cero penetraciones”, “calidad AAA automática”, “se auto-adapta al avatar” o “el juego ya consume las fases” sin implementación y evidencia específica.

---

## 8. Decisiones pendientes

- [ ] ¿Qué representación separada hay disponible para probar un cargador usado y uno nuevo a la vez? Si no la hay, ¿el primer hito se limita a movimiento procedural o se prepara un segundo modelo?
- [ ] ¿Qué valores iniciales mostrar para manos y fases? La asignación final debe ser decisión del usuario, no una regla fija del patrón.
- [ ] ¿Cómo se diferencia una recarga táctica, vacía y de emergencia: fases, parámetros o recetas independientes?
- [ ] ¿Qué datos pertenecen al perfil compartido y cuáles deben quedar como snapshot en cada toma para reproducibilidad?
- [ ] ¿Qué referencias visuales concretas se usarán para revisión? El enlace `https://www.youtube.com/shorts/BtWowPXS2og` queda registrado, pero la consulta disponible no devolvió fotogramas analizables; no se afirma que ese video ya se haya estudiado.

---

## Registro de avance

- [x] Revisión estática inicial de esquema, wizard de tres referencias, generador, guías y discrepancia AK12.
- [x] Creado este plan sin modificar código, tomas, escena, `BASE CORRECTA.asset` ni el plan anterior.
- [x] Añadidos requisitos del usuario: fases/piezas configurables, mano por fase, mecanismos distintos y modo procedural visual independiente.
- [x] Reconciliada estáticamente la historia de la coreografía AK12: validación histórica y retirada documentada.
- [x] Actualizadas las guías activas para distinguir la entrega histórica de las funciones disponibles.
- [x] Probar el campo legacy sobre una copia y documentar su pérdida al forzar reserialización.
- [x] Verificar en copias el descarte de `ak12Reload` y ejecutar el baseline actual del laboratorio (copia aislada; salida 0).
- [x] Definir e implementar el esquema v0.1; verificarlo y pasar las regresiones actuales en una copia aislada.
- [x] Añadir el editor de patrón/calibración a Recargas y verificar visualmente toma nueva, patrón configurado y calibración en la ventana real; capturas/corrección local cubiertas por prueba aislada.
- [x] Implementar y verificar el generador reversible muñeca + arma (Hito 3 inicial): anclas mínimas inicio/fin, retención en huecos, rechazo de solapamientos/discontinuidades/repeticiones, bloqueo estricto de dedos/piezas/mecanismos, vista ANTES/DESPUÉS, cancelación limpia, aceptación transaccional con Undo y protección de fingerprint; verificado en copia aislada junto a suites de regresión.
