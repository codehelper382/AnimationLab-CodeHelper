# Animation Lab — contexto y guía de uso

**Actualizado:** 15 de noviembre de 2026.  
**Proyecto:** `D:\Unity\CODEHELPER AnimationLab`  
**Unity:** `6000.3.14f1`  
**Proyecto del juego:** `D:\Unity\PRUEBA KINEMATION`  
**Contexto del juego:** `D:\Unity\PRUEBA KINEMATION\CONTEXTO_PROYECTO_JUEGO.md`.

**Tutorial paso a paso:** [Guía práctica para crear tus propias animaciones](GUIA_PRACTICA_ANIMATIONLAB.md), con ejercicios, uso de las skills instaladas y resolución de problemas de autoría.

**Para empezar sin experiencia:** [Guía rápida de la interfaz por fotogramas](GUIA_INICIO_RAPIDO_ANIMATIONLAB.md). La ventana actual tiene pistas separadas de posición/dedos por mano y arma; los desplazamientos animados se muestran en centímetros. Los tiempos normalizados y metros de este contexto describen el formato interno.

**Ampliación de objetos y agarres:** [Piezas, diseños y agarres](GUIA_PIEZAS_OBJETOS_Y_AGARRES.md). Incluye piezas del arma, cámara desplazable/navegable, edición local/global, conjuntos mano-pieza, referencias compuestas de primitivas y aproximación de dedos. No añade constraints físicos permanentes ni garantiza agarres perfectos.

**Recorridos y plantillas:** [Recorridos curvos y recargas en tres pasos](GUIA_RECORRIDOS_Y_RECARGAS.md). Configuración por tramo compatible con tomas anteriores y generador visual de cargador, tambor, cartucho y cerrojo; referencias portables, muñeca muestreada y dedos editables. No reconoce mecánicas ni instancia munición.

**Entrada sencilla actual:** [Recargas rápidas, solo manos y guiones por fotogramas](GUIA_RECARGAS_POR_FOTOGRAMAS.md). Propone las recargas existentes normal/vacía del AK12 como referencia de muñeca y dedos, con controles aditivos opcionales. Inserta una mano en un intervalo; arma/piezas/tipo/duración no cambian. Conserva los ocho gestos procedurales como alternativa orientativa. La conversión a 60 Hz conserva aproximadamente el movimiento fuera del tramo.

**Studio visual:** [Interfaz por tareas, puntos visuales, fases y prueba temporal](GUIA_STUDIO_VISUAL.md). Antes/Después/Aceptar/Cancelar separa el candidato de la toma, conserva recetas de variantes y curvas cinematográficas exportables; no incorpora componentes ni animaciones KINEMATION.

**Recarga guiada configurable — en desarrollo:** en Recargas se puede crear/copiar un patrón, definir piezas y fases, asignar manos/canales, vincular piezas y capturar/corregir muñecas (locales a guardia), piezas/mecanismos (locales a su padre) y la pose evaluada del holder del arma en el fotograma (local a su padre). Son referencias portables, no animan ni hornean raíces calibradas. Es una ficha de autoría, no un generador todavía: no aplica fases a gameplay ni crea movimiento automáticamente. El estado y límites están en [`PLAN_SISTEMA_RECARGAS_GUIADO.md`](PLAN_SISTEMA_RECARGAS_GUIADO.md).

**Flujo sencillo y seguridad:** vista Sencillo/Completo, guía Elegir/Poses/Ritmo/Probar/Guardar, traslación por ejes, dedos relativos a la llegada y validación junto al asistente. Respaldo periódico local configurable en Proyecto y recuperación alternativa si falla `Recovery_*`; no acepta candidatos ni sustituye Guardar copia. Inicio agrupa versiones nuevas mediante un catálogo local en Library, sin alterar el formato portable. Consulta [protección del trabajo](GUIA_STUDIO_VISUAL.md#11-flujo-sencillo-y-protección-del-trabajo).

**Coreografía AK12 — entrega histórica, no disponible en el código activo:** `../VALIDACION.md` registra su validación en una copia aislada el 15 de noviembre de 2026. `LIMPIEZA_Y_CONSOLIDACION.md` registra la eliminación del código AK12 específico. En el árbol activo no existe el asistente ni su prueba; la interfaz actual conserva los flujos descritos abajo, como SOLO MANOS y el generador avanzado mano + pieza. El asset `AK12_Asistente_Fluida_01.asset` y dos respaldos contienen `ak12Reload`, pero el esquema activo no lo declara: una prueba en copia confirmó que la reserialización forzada lo elimina. No guardar ni reserializar los originales hasta definir su recuperación/migración. La guía `../Asistente recarga AK12/LEEME.md` se conserva como referencia histórica, no como instrucciones de una función instalada.

> Este documento explica para qué existe el laboratorio, cómo funciona y cómo usarlo. Puede
> entregarse como contexto a otra persona o IA. El laboratorio es una herramienta de autoría,
> no una segunda versión jugable del juego final ni un sistema de combate completo.

## 1. Objetivo y relación con el juego

El laboratorio se creó para trabajar cómodamente con:

- Poses y agarres de armas/objetos.
- Forma de los dedos y posición/orientación de muñecas.
- Guardias, golpes y ataques melee.
- Recargas y manipulaciones de armas.
- Gestos de lanzamiento y otras acciones que más adelante puedan necesitar lógica de juego.

Reutiliza los sistemas reales de mirada, cámara, columna e IK para probar el resultado en condiciones
parecidas al juego, en vez de editar poses sin conocer su relación con la vista y el arma.

**Decisión definitiva:** el juego permanece en `PRUEBA KINEMATION`; el laboratorio está en este otro
proyecto. Se creó una copia local de los recursos necesarios, sin copiar la carpeta Git ni las cachés
del proyecto original. Unity genera sus propias cachés al abrirlo. El laboratorio tiene identidad
propia y se desvinculó del vínculo cloud del juego.

No hay sincronización automática. Cambiar aquí un script, una receta o un grip **no modifica el juego**.
Para llevar resultados al juego hay que exportar/importar y revisar explícitamente.

## 2. Qué contiene y cómo funciona

### Base reutilizada

- `PlayerLookController` y `CameraRigController`: mirada, vista y Aim.
- `ProceduralSpine`: columna y único grafo de animación del personaje.
- `HandIKSolver`: brazos y muñecas.
- `WeaponPoseController`: poses del arma.
- `EquipmentSceneController`: presentación de equipos y sus referencias.
- `HandGripToolkit`: poses de dedos, mapa de huesos y puntos de agarre.

Equipos disponibles para las pruebas: **AK12, puños y cuchillo**.

### Funcionamiento de la previsualización

1. Abre una copia aislada de `Assets/Scenes/AnimationLab.unity`.
2. Inicializa los componentes reutilizados y el grafo del personaje.
3. Evalúa la pose en el tiempo que eliges, con mirada/Aim de prueba.
4. Aplica movimientos de arma/manos y el IK final.
5. Aplica las poses de dedos grabadas con su mezcla correspondiente.
6. Renderiza la copia en la ventana del laboratorio.

El scrub no avanza la munición ni confirma contactos de combate. Los eventos del Animator están
desactivados en la previsualización. No se añade un segundo grafo sobre el personaje.

**Se utiliza fuera de Play.** El botón de reproducir de la herramienta reproduce la toma, no inicia
el modo Play de Unity.

## 3. Cómo abrirlo

### Desde Unity Hub

1. Pulsar **Add / Añadir proyecto desde disco**.
2. Elegir `D:\Unity\CODEHELPER AnimationLab`.
3. Abrir con **Unity 6000.3.14f1** y esperar la importación/compilación.
4. Ir a **Tools → CodeHelper → Animation Lab → Abrir laboratorio**.
5. Elegir el modo de columna si necesitas comparar `LateUpdate` / `AnimationJob`.
6. En Inicio, pulsar **Preparar personaje y crear toma** o elegir duplicar/abrir/recuperar.

### Acceso directo local

Ejecutar `Abrir laboratorio.cmd` en la raíz del proyecto. Utiliza la ruta de la instalación actual
de Unity; si se cambia esa instalación, hay que actualizar el comando o abrir desde Hub.

No es necesario transformar la escena abierta en un editor ni modificar el personaje del juego.

## 4. Conceptos básicos de la herramienta

| Concepto | Significado |
|---|---|
| Toma | Documento editable con acción, puntos de manos, dedos y calibraciones. |
| Equipo | AK12, puños o cuchillo sobre el que se trabaja. |
| Acción | Receta compatible con `EquipmentActionDefinition`. |
| Guardia | Referencia de reposo de la muñeca. |
| Desplazamiento | Offset de la mano respecto a la guardia, en metros. |
| Giro | Rotación adicional respecto a la guardia, en grados. |
| Punto grabado | Tiempo normalizado y datos de su pista: posición/giro o dedos. Las tomas antiguas pueden combinar ambos. |
| Calibración | Posición/rotación de un grip, guardia u otro punto de referencia. |
| Bake / horneado | Conversión de las poses de dedos a canales Humanoid que reproduce el juego. |

**Una pose temporal no es un punto guardado.** Pulsa **Guardar posición** o **Guardar dedos** y después guarda la toma como asset. Al navegar con ajustes pendientes puedes guardarlos, descartarlos o seguir editando; Guardar toma también los confirma.

## 5. Panel 1: preparar equipo y acción

1. Elegir el **Equipo**.
2. Escribir un **Nombre de la toma** reconocible, por ejemplo `Fists_Derecha_Recto_01`.
3. Definir la **Duración**, en segundos.
4. Elegir el **Tipo** que corresponda a las categorías existentes del runtime.
5. Para partir de una animación existente, usar **Copiar acción existente**.

La receta original no se modifica: se trabaja sobre una copia. Cambiar de equipo o copiar otra
acción puede descartar el borrador actual después de la confirmación. **Guardar antes de aceptar**
si quieres conservarlo.

Los tipos disponibles son los de `EquipmentActionKind`; el laboratorio no agrega automáticamente
un nuevo tipo de gameplay llamado Throw ni un sistema para lanzar objetos.

En Studio la duración está también directamente en **Animar**, en segundos o fotogramas a 60 fps, con Undo. Retima las claves normalizadas sin borrar canales; durante una prueba temporal hay que Aceptar o Cancelar antes de cambiarla.

## 6. Panel 2: editar manos, agarres y dedos

### A. Mover y orientar una muñeca

1. Elegir **Mano izquierda** o **Mano derecha**, y **Posición y giro**.
2. Elegir el **Fotograma** o hacer clic en la pista/rombo de la línea de tiempo.
3. Ajustar **Desplazamiento (cm)** y **Giro (grados)**, o arrastrar el punto amarillo.
4. Inspeccionar el ajuste temporal sin fundidos; los campos se sincronizan al cambiar de tiempo/mano.
5. Pulsar **Guardar posición en F …**. Solo cambia la muñeca; los dedos son otra pista.

Para **Arma / objeto**, usar su pista y el punto naranja. Sus claves generan las curvas de offsets del arma sin sustituir el clip de piezas.

El punto central mueve la muñeca en el plano de la vista; los ejes X/Y/Z restringen la traslación.
Para profundidad y precisión también puedes usar campos numéricos y otro ángulo. Esc cancela el arrastre. No arrastres el fondo si querías mover el punto amarillo:
arrastrar el fondo orbita la cámara externa.

**Los ejes son locales a la guardia**, no necesariamente los ejes globales de la escena.
Si giras la guardia, sus ejes también cambian. Para giros grandes añade puntos intermedios y evita
depender de un único salto Euler de más de 180°.

### B. Ajustar los dedos

- **Usar pose de este fotograma como base:** prepara la pose fuente sin fundidos o conserva el ajuste actual como base y pausa.
- **Mano abierta / Mano cerrada:** presets basados en músculos Humanoid del avatar.
- **Cargar pose de dedos:** carga un `HandPoseData` de la misma mano.
- **Eje local de flexión X/Y/Z:** eje sobre el que actúan los cinco sliders de dedos.
- **Ajuste fino de articulaciones:** rotación individual de las articulaciones disponibles.
- **Guardar pose de dedos independiente:** crea un `HandPoseData` separado de la toma.

Para animar dedos, elegir **Solo dedos** y pulsar **Guardar dedos en F …**. No modifica Motion Clip, Reach, curvas ni bindings de muñeca. Una forma temporal debe confirmarse como punto antes de considerarse parte de la toma.

**Agarres actuales:** la pose real de F 0 de la animación cargada está disponible como **Sostener arma · animación cargada**, editable y guardable como `LabGripPreset` independiente. Al guardar el primer agarre posterior a F 0 se conserva ese inicio automáticamente. Las transiciones de llegada Suave/Lineal/Instantánea permiten, por ejemplo, mantener el agarre básico hasta F 30 y llegar al cargador en F 40 con 10 F de mezcla; las claves anteriores limitan el intervalo. Las tomas previas conservan `Legacy` hasta editar su transición o introducir un tipo nuevo. Los conjuntos mano + pieza con dedos usan la misma protección.

**Flujo visual recomendado:** en F 40 selecciona **Agarre de llegada → Cargador**, **Suave**, **Mantener hasta F 20**. La barra muestra base F 0–20 y adaptación F 20–40; Base/Inicio cambio/50%/Llegada y **Ver proceso completo** permiten probarlo en el visor antes de guardar. El fotograma de destino permanece en F 40 mientras el visor prueba F 0/20/30/40. Sliders, articulaciones y guardado de tipos están en **Ajustar dedos / crear variantes**.

**Recorrido común mano–pieza:** dos puntos nuevos guardados como conjunto almacenan contactos por datos y compilan la muñeca sobre la misma curva de la pieza a 60 Hz. Cambiar Arco/Libre actualiza ambos; al arrastrar, recalcula al soltar. Para puntos anteriores, usar **Unir mano y pieza en este tramo** una vez. Separar conserva las curvas actuales sin desunir el tramo vecino. No hay constraints runtime: se exportan curvas normales y clip de piezas.

**Estado de agarres:** el selector es una orden para una pose nueva, no la identidad de lo guardado. Sin clave exacta y sin edición pendiente no se muestra una transición Suave por defecto. El panel indica la última/siguiente pose; en una clave compara con F0. **Volver al agarre guardado en F0** recupera esa pose y **Mantener este agarre hasta el final…** quita posteriores poses solo de dedos, con Undo.

**Contacto mano–pieza:** el panel de dedos avisa si el último enlace termina antes del final; a partir de ahí la muñeca deja de seguir la pieza aunque los dedos conserven su pose. **Mantener mano y pieza hasta el inicio del fundido…** añade un punto de la pieza con el último contacto y recalcula esa muñeca a 60 Hz, sin tocar dedos, arma ni la otra mano. El contacto se corta donde empieza el fundido de salida porque el solver exige peso completo; el fundido global (0,08 s en `BASE CORRECTA`) sigue atenuando el final.

**Movimiento natural del arma:** **Animar → Arma → Probar movimiento natural del arma** prepara un candidato con respiración leve y reacciones amortiguadas basadas en los puntos. Antes/Después, intensidad, Aceptar/Cancelar; preserva controles principales y contactos, sin cámara nueva ni física. Guarda base y snapshots numéricos para regenerar sin acumular y restaurar sin sobrescribir cambios manuales posteriores. El análisis de la toma real y las propuestas de facilidad de uso están en [ANALISIS_BASE_CORRECTA.md](ANALISIS_BASE_CORRECTA.md).

Cada rig puede usar ejes locales diferentes. Si un slider tuerce el dedo en vez de cerrarlo,
prueba otro eje o comienza con los presets Humanoid. Al grabar, las poses se ajustan a los grados
de libertad reproducibles por el avatar; una torsión arbitraria puede corregirse ligeramente.

No se asume que reflejar una pose izquierda produce una derecha correcta. El espejo directo del
toolkit requiere marcos de huesos compatibles y no se usa automáticamente en esta ventana.

### C. Calibrar agarres/puntos

En **Calibrar agarre / punto**, elegir el punto disponible y editar su posición/giro local.

Esto recoloca la referencia del grip/guardia/objetivo, no agrega por sí solo una trayectoria temporal.
Los cambios afectan a la copia de previsualización y se conservan al guardar la toma como calibración.
**No recolocan automáticamente el grip de la escena del juego.**

### D. Editar puntos existentes

- Pulsar un punto de la lista para ir a su tiempo y recuperar desplazamiento/giro.
- Volver a **Guardar posición** o **Guardar dedos** en ese mismo tiempo para reemplazar ese canal.
- Usar **Quitar** para eliminarlo.
- Cada mano mantiene su propia lista de puntos.

**Copiar / Pegar** junto a la línea de tiempo reutiliza la pose de la pista seleccionada (muñeca, dedos, arma o pieza), o una mano completa con posición y dedos. Admite Ctrl+C/V fuera de campos de texto y Undo/Redo; los dedos son copias independientes. Exige mismo equipo/mano/pieza, sin espejo implícito. Copia el fotograma evaluado y los ajustes de curva cuando hay una clave exacta; no copia intervalos. El portapapeles vive en la ventana, no en el formato portable.

### E. Panel avanzado

**Avanzado: clips, curvas, pesos y contacto** expone la receta de acción:

- Clips de personaje, arma y movimiento local.
- Máscaras de personaje.
- Blend In / Blend Out.
- Curvas de grip e IK por mano.
- Movimiento de arma y trayectorias por mano.
- Duración, hito y cancelación como datos de la receta.

**Advertencia importante:** guardar puntos de **posición de muñeca** reconstruye su canal procedural y desactiva el `Motion Clip` y `Reach` previos, con confirmación al sustituir una receta. **Guardar dedos** no hace esa sustitución. La otra mano y el clip del arma no se reemplazan al guardar posición de una mano.

## 7. Panel 3: tiempo, reproducción y vistas

- **Fotograma:** número editable y deslizador a 60 fps de referencia, con segundos y pistas/rombos seleccionables. Internamente el progreso sigue siendo `[0,1]`.
- **Volver al inicio:** regresa al primer instante.
- **−1 / +1 fotograma:** avanza en pasos de referencia de 1/60 s.
- **Reproducir / Pausar:** reproduce o detiene la toma sin entrar en Play.
- **Repetir:** reinicia al llegar al final.
- **Velocidad:** cambia la velocidad de previsualización, no la duración guardada de la receta.
- **Personaje:** cámara externa; arrastrar fondo para orbitar y rueda para zoom.
- **Primera persona:** prueba el encuadre desde la cámara del personaje.
- **Mirar arriba/abajo y a los lados:** prueba la relación con columna, cámara y brazos.
- **Probar Aim:** comprueba el comportamiento con apuntado cuando corresponde al equipo.

Con Blend In/Out, el movimiento pierde influencia al principio/final para volver a la guardia.
Al editar se omiten temporalmente los fundidos para poder colocar la pose también en los extremos. Al reproducir vuelven las mezclas; una pose con poca influencia final no es necesariamente un fallo del IK.

## 8. Ejemplo completo: golpe derecho con puños

1. Preparar el personaje y elegir **Puños**.
2. Nombre `Fists_Golpe_Derecho_01`; duración de ejemplo `0.6` segundos.
3. Elegir mano **Derecha**.
4. Fotograma `0`: posición/giro cero y **Guardar posición**; en Solo dedos, **Mano cerrada** y **Guardar dedos**.
5. Fotograma `18` —mitad de `0.6 s`—: aumentar Z local, por ejemplo `12 cm` si ese eje es adecuado, o arrastrar la muñeca; comprobar alcance y **Guardar posición**.
6. Fotograma `36`: **Volver al reposo** y **Guardar posición** para regresar a la guardia.
7. Reproducir. La izquierda puede conservar su guardia sin agregarle la trayectoria derecha.
8. Probar primera persona y mirada vertical; reducir alcance si el brazo queda forzado.
9. Guardar como nueva copia.

Los valores son ejemplos, no una calibración universal. La guardia, las proporciones del personaje
y el Blend In/Out determinan el resultado visible.

## 9. Ejemplo: ajustar una recarga del AK12

**Entrada recomendada en Studio:** en Recargas, elegir **Recarga existente · normal/vacía**, Probar y comparar Antes/Después. **Crear puntos visuales** permite añadir correcciones al recorrido denso conservando los dedos originales por defecto; un override de dedos de fase es local a esa fase. **Usar duración original** cambia solo el tiempo total. **Abrir recarga completa como copia** abre todos los clips/objetivos de la receta en un borrador independiente, en vez de insertar solo una mano.

En Animar, el tramo se edita desde cualquier fotograma entre dos claves y desde el último punto. **Libre/Bézier** añade dos controles independientes con profundidad mediante Alt+arrastrar. Capturar inicio/final de un clip conserva dedos y fuentes, pero sustituye la forma interior de esa muñeca: exige confirmación y admite Undo.

1. Elegir **AK12** y **Copiar acción existente → Reload** o la variante correspondiente.
2. Guardar una primera copia con un nombre propio antes de experimentar.
3. Reproducir la acción para entender qué controla el clip de dedos, qué controla la trayectoria
   izquierda y qué controla un posible clip del arma.
4. Para cambiar contacto sin reemplazar la trayectoria, calibrar grips/objetivo de recarga y
   revisar el panel avanzado.
5. Guardar puntos de posición solo si quieres **sustituir el canal procedural de esa muñeca**; usar Solo dedos para cambiar únicamente su forma.
   No basta con registrar puntos para asumir que se sustituyeron todos los canales antiguos de recarga:
   revisar también `Left Hand Target Motion`, referencias del slot y clips de piezas.
6. Probar varias partes del recorrido y mirar arriba/abajo.
7. Conservar la derecha sobre su grip si no necesita manipular el arma.
8. Guardar, exportar e integrar la variante como acción distinta hasta validarla en el juego.

La munición no cambia durante estas pruebas. El laboratorio no valida por sí solo las reglas de
transferencia de munición ni genera automáticamente contacto con el cargador.

## 10. Panel 4: guardar, recuperar y reabrir

### Guardar una toma

Pulsar **Guardar como nueva copia**. Se guarda en:

```text
Assets/AnimationLab/Authored/<nombre>.asset
```

La toma contiene:

- Identificación del equipo y tipo de acción.
- Receta `EquipmentActionDefinition` como subasset **Action**.
- Puntos de ambas manos y poses de dedos.
- Calibración de puntos, sin referencias directas a objetos de escena.
- Clip/máscara fuente para continuar editando sin reutilizar un bake antiguo como si fuera el original.
- Cuando corresponde, clip Humanoid de dedos horneado a 60 Hz y máscara compatible.

Guardar crea una copia con ruta única; no sobrescribe automáticamente el recurso original.
El horneado conserva los canales del clip de personaje inicial y elimina eventos de animación del
clip resultante. Si partiste de una animación externa, puede contener curvas derivadas de ella.

### Reabrir

Usar **Abrir toma** y seleccionar el asset guardado. La toma debe corresponder a un slot compatible
y al mismo `equipmentId`. No se hace retargeting completo ni búsqueda libre de otro equipo.

### Recuperación

Al cerrar la ventana, recompilar o intentar entrar en Play, la herramienta intenta conservar el
borrador modificado con nombre `Recovery_FECHA_HORA` dentro de Authored, sin hornear dedos.

Si ocurre una recuperación, abrir ese asset con **Abrir toma** y guardar una copia normal para
producir su resultado final. La recuperación es una ayuda, **no sustituye guardar frecuentemente**.
La recuperación intenta confirmar también los ajustes pendientes; depende de que no haya errores y no sustituye guardar una toma normal.

La pantalla **Inicio** y **Proyecto → Buscar… → Recuperaciones** permiten buscar y abrir esas tomas. **Proyecto → Guardar recuperación ahora** ofrece una copia manual sin hornear. El botón **Inicio** de la cabecera pregunta si guardar recuperación, cancelar o descartar cuando hay trabajo pendiente. Proyecto permite además activar/configurar respaldos periódicos locales sin hornear, abiertos desde Inicio. Una prueba temporal se recupera sin aceptar su candidato; esos respaldos no sustituyen el guardado final.

**Revisar** agrega avisos de datos con acceso al fotograma/pista: no exige puntos manuales si la acción ya tiene clips, curvas o movimiento de objetivo, ni obliga a mover la muñeca en una toma solo dedos. El visor de revisión deshabilita el arrastre de poses y el pegado de claves; reproduce y navega sin modificar la toma. Sigue haciendo falta comprobar la apariencia en primera persona y con mirada/Aim.

**Transiciones y ritmo:** Animar incluye un asistente entre dos puntos consecutivos, con presets, bloques mantener/viajar/asentar y dedos opcionales. Probar mantiene fuente/candidato separados; Aceptar valida el borrador y aplica con Undo. No reconoce contactos físicos ni simplifica automáticamente clips existentes. Los rombos se pueden arrastrar a otro fotograma, o mover por campo numérico: se conserva el otro canal de una clave combinada y se confirman colisiones. Una pieza recompila los contactos de sus manos; una muestra de muñeca enlazada no se mueve por separado. [Flujo y límites](GUIA_STUDIO_VISUAL.md#10-transiciones-fáciles-y-cambio-de-fotogramas).

## 11. Exportar e integrar en el juego

1. Guardar la toma normal. Si cambias algo después, guardar otra copia antes de exportar.
2. Pulsar **Exportar copia guardada al juego (.unitypackage)** y elegir dónde guardarlo.
3. Abrir el proyecto del juego e importar el paquete.
4. Revisar la lista de importación. Si ya tienes los scripts compartidos y son más recientes,
   desmarcarlos para no reemplazarlos a ciegas. No cambiar sus GUID.
5. Expandir el asset de toma y localizar su subasset **Action**.
6. Asignar esa acción al campo correcto de la definición del juego:
   - Recarga → campo Reload o variante correspondiente del arma de fuego.
   - Ataque melee → entrada correspondiente de Attacks.
   - Equipar/guardar → campo de acción correspondiente.
7. Si necesitas la calibración, seleccionar la toma, asignar el `EquipmentSceneController`
   de la escena en **Equipo de destino** y pulsar **Aplicar agarres a la escena (Undo)**.
8. Se exige `equipmentId` único y referencias/jerarquía de puntos compatibles. Revisar el resultado,
   deshacer si no corresponde y guardar la escena cuando esté correcto.
9. Probar en **Play del juego** los contactos, transiciones, cancelación y reglas reales.

**Importar no asigna automáticamente la acción, no cambia definiciones y no reemplaza la escena.**
Aplicar calibración es una operación distinta, explícita y deshacible.

El paquete incluye la toma y código portable necesario, no la ventana completa, la escena del
laboratorio ni los modelos/paquetes externos. Las referencias externas deben existir en el juego
con los mismos GUID. No exportar o compartir recursos derivados de terceros sin revisar su licencia.

## 12. Estructura para desarrollo y mantenimiento

```text
Assets/
├── AnimationLab/
│   ├── Runtime/AnimationLabTake.cs              # Formato portable de toma
│   ├── Editor/
│   │   ├── AnimationLabWindow.cs                # Interfaz y flujo de trabajo
│   │   ├── LabPreview.cs                        # Copia aislada y evaluación/render
│   │   ├── LabAssetIO.cs                        # Clonado, guardado, bake y exportación
│   │   └── AnimationLabChecks.cs                # Pruebas nativas
│   ├── PortableEditor/AnimationLabTakeEditor.cs # Aplicar calibración al importar
│   └── Authored/                                # Tomas y recuperaciones
├── CODEHELPER/                                  # Copia local de la base compartida
│   └── HandGripToolkit/Runtime/                  # HandPoseData, mapas, grips
└── Scenes/AnimationLab.unity                     # Fixture local de previsualización
```

`LabPreview` usa reflexión acotada sobre la copia del runtime para inicializar/evaluar los mismos
pases usados por el juego. Si cambian sus métodos privados o el orden de ejecución, hay que revisar
el adaptador y volver a ejecutar pruebas. No resolverlo creando otro grafo sobre el mismo Animator.

`PortableEditor` está protegido con `UNITY_EDITOR` para no romper la compilación de jugador.
El código exportable no debe depender de la ventana ni de clases editor-only del laboratorio.

## 13. Estado de validación y límites

Se comprobaron automáticamente:

- Tres equipos y ambos modos de columna.
- Scrub reproducible hacia delante/atrás con mirada variable.
- Ausencia de consumo de munición/confirmaciones de combate durante scrub.
- Guardado y carga mediante copias independientes.
- Coincidencia del clip de dedos con la pose de prueba del laboratorio: error medido de **0°**
  en el punto comprobado, no una garantía universal para todo avatar o gesto.
- Mezclas de entrada/salida y conservación de fuentes para reeditar.
- Calibración sobre otro personaje sin mover el personaje original.
- Limpieza de escenas de preview y conservación de escenas abiertas.
- Compilación del runtime sin `UNITY_EDITOR`.
- Exportación e importación de toma/acción/dedos/código en un proyecto independiente.

Repetir desde **Tools → CodeHelper → Animation Lab → Verificar laboratorio (aislado)**.
Consultar `VALIDACION.md` para detalle y ubicación de logs.

Lo que no hace:

- No diseña automáticamente animaciones de calidad final.
- No crea un agarre perfecto a partir de una malla.
- No añade daño, proyectiles, física de lanzamiento, sonido o eventos de soltar/coger.
- No hace retargeting universal de poses locales entre avatares diferentes.
- No asegura ausencia de penetraciones, ni comodidad del encuadre sin revisión visual.
- No sincroniza cambios de scripts o escenas con el juego.
- No sustituye las pruebas reales de gameplay.

## 14. Problemas habituales

| Problema | Qué revisar |
|---|---|
| No aparece la ventana | Abrir este proyecto, no el del juego; esperar compilación y usar el menú indicado. |
| La herramienta pide salir de Play | Usar su propio botón Reproducir, fuera de Play de Unity. |
| El resultado reproducido pierde influencia al inicio/final | Revisar Blend In/Out; el ajuste temporal se muestra sin esos fundidos. |
| Un dedo gira hacia un lado | Cambiar eje local, usar presets Humanoid o ajuste fino. |
| Los sliders de dedos ya no aparecen | Capturar dedos, cargar una pose o aplicar Mano abierta/cerrada. |
| Solo veo una pose temporal | Pulsar Guardar posición / Guardar dedos y después Guardar como nueva copia. |
| Cambió el recorrido al grabar posición | Sustituye el canal procedural de esa muñeca; Solo dedos no lo hace. |
| El movimiento no afecta la recarga esperada | Revisar bindings del slot, Left Hand Target Motion y canales realmente usados por esa receta. |
| Una toma no abre | Comprobar slot compatible, `equipmentId` y recursos del avatar. |
| Cambiar algo aquí no afecta el juego | Es correcto: exportar/importar y asignar explícitamente. |
| Faltan recursos después de importar | Restaurar dependencias externas autorizadas con sus `.meta` originales. |
| El paquete no reemplaza la acción actual | Es intencional: asignar manualmente el subasset Action. |
| La calibración no se aplica | Elegir el componente de la escena correcto y comprobar identidad/referencias del equipo. |

## 15. Reglas para continuar con otra persona o IA

- Mantener el laboratorio separado del juego y sin sincronizaciones implícitas.
- Priorizar controles intuitivos y conservar datos al editar, guardar y recuperar.
- No modificar escenas/definiciones compartidas mientras solo se previsualiza.
- No alterar GUID ni sobrescribir scripts del juego automáticamente al exportar.
- No añadir componentes runtime de KINEMATION para sustituir los sistemas propios.
- Conservar un único grafo y el orden cámara/arma/movimiento/IK.
- Distinguir herramienta de animación de futura lógica de combate/lanzamiento.
- Probar técnicamente y revisar visualmente antes de declarar una animación terminada.
- No publicar el laboratorio ni recursos locales en GitHub sin permiso.

## Resumen breve para entregar a otra IA

> Tengo un proyecto independiente D:\Unity\CODEHELPER AnimationLab, Unity 6000.3.14f1, para crear
> poses de dedos, grips y acciones de manos/armas. El juego sigue en D:\Unity\PRUEBA KINEMATION.
> La ventana está en Tools > CodeHelper > Animation Lab > Abrir laboratorio y se usa fuera de Play.
> Reutiliza PlayerLookController, CameraRigController, ProceduralSpine, HandIKSolver,
> WeaponPoseController y EquipmentSceneController sobre una escena de preview aislada, con un solo
> grafo y sin avanzar munición/combate. Integra HandGripToolkit. Hay AK12, puños y cuchillo.
> Puedo grabar puntos por mano, editar dedos, reproducir, calibrar y guardar tomas en
> Assets/AnimationLab/Authored. El bake genera dedos Humanoid compatibles con la acción del juego.
> Exporto un .unitypackage y asigno su Action manualmente en el juego; aplicar agarres es otra
> operación explícita con Undo. No hay sincronización viva, gameplay de lanzamientos ni daño.
> Preservar fuentes, GUID, datos y aislamiento; no publicar recursos de terceros ni cambios sin permiso.
