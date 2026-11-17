# CODEHELPER Animation Lab

Proyecto Unity independiente para autoría de poses, agarres y acciones. **El proyecto del juego
continúa en `D:\Unity\PRUEBA KINEMATION` y no se sincroniza ni modifica automáticamente.**

El laboratorio tiene nombre/identificador propios y no conserva el vínculo cloud del juego.

## Abrir

1. En Unity Hub, **Add / Añadir proyecto desde disco** → `D:\Unity\CODEHELPER AnimationLab`.
2. Usar **Unity 6000.3.14f1**. Esperar la importación inicial y la restauración de paquetes.
3. Menú **Tools → CodeHelper → Animation Lab → Abrir laboratorio**.
4. En Inicio, pulsar **Preparar personaje y crear toma**, duplicar una acción o abrir una toma/recuperación de la lista. **No entrar en Play**.

También puedes usar `Abrir laboratorio.cmd` con la instalación actual de Unity.

## Flujo rápido: crear un golpe

1. Elegir **Puños**, poner nombre y duración `0.6 s`.
2. Elegir **Derecha · posición** en la línea de tiempo y **Fotograma 0**. Pulsar **Volver al reposo** y **Guardar posición**.
3. Ir a **Fotograma 18**, arrastrar el punto amarillo o editar centímetros/grados y **Guardar posición**.
4. Ir a **Fotograma 36**, **Volver al reposo** y **Guardar posición**. En **Derecha · dedos**, elegir la forma y **Guardar dedos** por separado.
5. **Reproducir**, probar mirada vertical/horizontal y vista de primera persona.
6. **Guardar como nueva copia**. Las tomas quedan en `Assets/AnimationLab/Authored`.

Hay pistas independientes de posición/dedos de cada mano y **Arma / objeto**, con rombos seleccionables y campos sincronizados al fotograma. La interfaz muestra centímetros; internamente las posiciones siguen en metros y ejes **locales a la guardia**, con giro Euler en grados. Para giros grandes añade puntos intermedios.
Los Blend In/Out del panel avanzado también atenúan el movimiento, igual que en el juego.

**Primera vez:** [guía rápida](Assets/GUIA_INICIO_RAPIDO_ANIMATIONLAB.md). Para ejercicios y fundamentos: [guía detallada](Assets/GUIA_PRACTICA_ANIMATIONLAB.md).

La vista inicial **Sencillo** incluye guía **Elegir → Poses → Ritmo → Probar → Guardar** y oculta controles técnicos. **Completo** conserva todas las herramientas. El visor permite mover por ejes X/Y/Z (Esc cancela); el asistente ofrece adaptación de dedos relativa a la llegada y errores junto a sus campos.

**Studio visual:** [Inicio / Recargas / Animar / Proyecto / Revisar](Assets/GUIA_STUDIO_VISUAL.md). Inicio permite buscar tomas y recuperaciones; Siguiente paso orienta la edición y Revisar muestra avisos navegables sin arrastrar poses. La línea de tiempo incluye zoom y navegación entre puntos; el visor, presets de cámara. Arrastra puntos y fases en Animar/Recargas, compara Antes/Después y acepta o cancela sin insertar a ciegas. Conserva el solver propio.

**Transiciones fáciles y fotogramas movibles:** en Animar, selecciona dos poses y usa **Crear transición fácil** para mantener/viajar/asentar con presets, dedos opcionales y prueba Antes/Después/Aceptar/Cancelar. Arrastra rombos para mover sus tiempos, o usa **Mover a F**. Colisiones confirmadas, canales independientes, contactos recompilados y Undo. [Paso a paso](Assets/GUIA_INICIO_RAPIDO_ANIMATIONLAB.md#crear-una-transición-fácil).

**Movimiento natural y diagnóstico de agarres:** [análisis de BASE CORRECTA](Assets/ANALISIS_BASE_CORRECTA.md). En **Arma**, **Probar movimiento natural del arma** añade una capa secundaria pequeña y determinista con Antes/Después/Intensidad; en **Dedos** se distingue la pose guardada, el tramo sin clave y el destino pendiente, y se avisa si el contacto mano–pieza termina antes del final.

**Recargas intuitivas:** [solo manos y guiones por fotogramas](Assets/GUIA_RECARGAS_POR_FOTOGRAMAS.md). **Recargas rápidas · insertar SOLO MANOS** añade muñeca/dedos a un tramo de la toma sin mover armas o piezas. Lista de pistola, subfusil, fusil, francotirador, revólver, escopeta y arco; los giros de arma del guion son opcionales y se editan aparte.

**Recorridos y recargas:** [guía de curvas por tramo y asistente de tres referencias](Assets/GUIA_RECORRIDOS_Y_RECARGAS.md). Plantillas de cargador, tambor, cartucho y cerrojo con dedos: bases visuales editables, no detección automática de mecánica/munición.

**Piezas y objetos:** [guía de cargador/cerrojo, cámara, Global, conjuntos y agarres](Assets/GUIA_PIEZAS_OBJETOS_Y_AGARRES.md). La pestaña **Piezas / diseños** permite seleccionar `Mag`/`Charge` del AK12, componer referencias con primitivas y obtener una forma aproximada de dedos. Los conjuntos son claves coordinadas, no un vínculo físico permanente; el prefab es geometría estática aparte.

## Agarres y dedos

- **Calibrar agarre / punto** cambia solo la copia de previsualización. Guarda la toma para
  conservar sus posiciones y rotaciones; no se guardan automáticamente en la escena original.
- **Usar pose de este fotograma como base** toma la pose fuente sin fundidos. Los cinco sliders suman
  flexión a esa base. El eje X/Y/Z es local al hueso: puede variar según el rig; prueba el que
  cierre la mano correctamente. No se presupone que todos los modelos usen los mismos ejes.
- **Mano abierta / Mano cerrada** usa los músculos Humanoid del avatar, sin adivinar ejes locales.
  Puedes cargar un `HandPoseData` de la misma mano y ajustar cada articulación en el desplegable fino.
- **Guardar dedos** crea puntos independientes de la muñeca. Se interpolan entre los puntos de cada mano.
  Al grabar, la pose se ajusta a los grados de libertad que el avatar Humanoid puede reproducir;
  torsiones locales no representables pueden corregirse ligeramente para coincidir con el bake.
- **Guardar pose de dedos independiente** produce un `HandPoseData` reutilizable del toolkit.
- No se usa el espejo directo del toolkit: requiere huesos locales simétricos y no es seguro
  asumirlo para todos los avatares.

## Recargas, cuchillo y otras acciones

Selecciona AK12/cuchillo y **Copiar acción existente** para partir de una copia de su receta.
El original no se modifica. El panel avanzado permite editar clips de personaje/arma,
movimientos por mano, curvas de IK/grip, offsets de arma y tiempo de contacto.

**Guardar posición de muñeca reemplaza su canal procedural**, con confirmación si ya tenía una receta (desactiva Motion Clip y Reach previos). No reemplaza la otra mano ni el clip del arma. **Solo dedos → Guardar dedos** conserva la trayectoria. También puedes ajustar contactos mediante calibración o panel avanzado.

Lanzamientos y manipulaciones se pueden posar/previsualizar como acciones de manos/arma.
Este laboratorio no añade proyectiles, física de objetos arrojados, daño, sonido ni eventos
de soltar/coger objetos. `EquipmentActionKind` conserva las categorías que ya admite el juego.

## Guardar y recuperación

- Guardar crea **otra copia**, nunca sobrescribe una receta/pose original.
- La toma contiene una acción compatible con el juego, poses de dedos y calibración de puntos.
- Al guardar puntos de dedos se hornean músculos Humanoid a 60 Hz, preservando los canales del
  clip de personaje inicial. El resultado queda como subasset de la toma, sin eventos de animación.
  La toma conserva el clip/máscara fuente para que reabrirla y quitar puntos no deje un bake antiguo.
- Al cerrar la ventana, recompilar o entrar en Play se intenta guardar el borrador modificado
  como `Recovery_FECHA_HORA` en Authored, sin horneado. Se puede abrir desde **Abrir toma**.
- **Proyecto → Respaldo automático** conserva además borrador y ajustes pendientes cada 120 s por defecto, sin bake ni aceptación del candidato. Inicio muestra sus respaldos e historial. Los JSON viven en `Library/AnimationLabRecovery` (alternativa en `%LOCALAPPDATA%/Temp/opencode/AnimationLabRecovery` si falla la escritura); no se exportan ni sustituyen Guardar copia. Si falla todo acceso a disco, se avisa de la retención **solo de sesión**.
- Inicio agrupa las nuevas versiones con un catálogo local en Library; **Mostrar todas las versiones guardadas** las despliega. No borra ni sobrescribe assets.
- Los cambios de equipo/toma piden confirmar el descarte del borrador.
- Deshacer soporta puntos grabados y calibración. Guardar/exportar es explícito.

## Llevar una toma al juego

1. Guardar una copia y pulsar **Exportar copia guardada al juego (.unitypackage)**.
2. Importarla en el juego. Unity mostrará los archivos: si ya tienes los scripts compartidos,
   desmárcalos para no reemplazar cambios más recientes; conserva sus GUID.
3. Expandir el asset de la toma en Project y asignar su subasset **Action** al campo apropiado
   de `FirearmDefinition` o `MeleeDefinition` (Reload, Attacks, etc.). Esto no se hace automáticamente.
4. Para aplicar los agarres, seleccionar la toma, asignar el `EquipmentSceneController` de la
   escena a **Equipo de destino** y pulsar **Aplicar agarres a la escena (Undo)**. Se exige un
   `equipmentId` único y la misma jerarquía de puntos. Revisa el resultado y guarda la escena.
5. El inspector de importación no cambia definiciones ni asigna acciones por sí solo.

El paquete incluye toma y código portable, **no** la escena del laboratorio, su ventana ni los
modelos/paquetes externos. Las referencias externas deben existir en el juego con los mismos GUID.
Si se parte de clips de terceros, el bake puede contener curvas derivadas: es para uso local
autorizado, no implica permiso de publicación/redistribución. No se ha subido nada a GitHub.

## Estructura y límites

- `Assets/AnimationLab/Runtime`: formato portable de toma, sin lógica de combate.
- `Assets/AnimationLab/Editor`: ventana, previsualización, guardado y verificaciones.
- `Assets/AnimationLab/PortableEditor`: inspector para aplicar calibración al importar en el juego.
- `Assets/CODEHELPER`: copia local de la base compartida y `HandGripToolkit`.
- `Assets/Scenes/AnimationLab.unity`: copia de la escena de pruebas; no es la escena del juego.
- Recursos necesarios de terceros: copias locales de dependencias, no un nuevo runtime KINEMATION.

La previsualización usa una escena aislada y **un solo grafo del ProceduralSpine existente**, con
PlayerLookController, CameraRigController, WeaponPoseController y HandIKSolver reales. Mover el
tiempo asigna la pose sin avanzar la munición, los contactos de combate ni AnimationEvents.
El adaptador de editor usa reflexión acotada sobre la copia del runtime; si se actualiza esa base,
ejecuta de nuevo las verificaciones. No hay enlace vivo entre ambos proyectos.

**Verificar:** Tools → CodeHelper → Animation Lab → Verificar laboratorio (aislado).
Para autoría: **Verificar edición por fotogramas** comprueba separación de pistas, claves antiguas y guardado/reapertura.
Para el taller: **Verificar piezas, diseños y agarres** comprueba piezas reales, bake Generic con fundidos, conjuntos, coordenadas globales y persistencia de diseños/agarres.
Para movimiento natural: **Verificar movimiento natural y diagnóstico de agarres** usa una copia de `BASE CORRECTA` y cubre F150/F198, mantenimiento, capa determinista, contactos y restauración.
Comprobar visualmente contactos, torsión de dedos y comodidad del flujo con tu avatar antes de
considerar una animación terminada. Las pruebas automáticas no evalúan su calidad artística.
