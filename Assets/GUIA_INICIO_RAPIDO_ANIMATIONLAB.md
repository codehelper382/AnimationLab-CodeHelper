# Animation Lab: crear una animación sin conocer el sistema

**Distribución:** [Guía de Studio visual](GUIA_STUDIO_VISUAL.md). Inicio para crear/abrir/recuperar, Recargas para puntos/fases y pruebas temporales, Animar para claves manuales, Proyecto para equipo/archivos y Revisar para avisos. La línea de tiempo está abajo.

## La idea en una frase

**Elige un fotograma, coloca una mano o el arma y guarda un punto. Haz lo mismo en otro fotograma: el laboratorio interpola el movimiento.**

Trabaja fuera de Play de Unity. El botón **Reproducir** pertenece al laboratorio.

**Recargas fáciles:** en **Recargas**, elige la recarga existente normal/vacía del AK12 y prueba una mano antes de aceptar. Puedes corregirla con puntos sin perder su movimiento original. Los [guiones procedurales por arma](GUIA_RECARGAS_POR_FOTOGRAMAS.md) siguen disponibles como alternativa orientativa. Insertar una mano no mueve el arma ni piezas; **Abrir recarga completa como copia** es una operación aparte.

## 1. Abrir

1. Abre `D:\Unity\CODEHELPER AnimationLab` con Unity `6000.3.14f1`.
2. Menú **Tools → CodeHelper → Animation Lab → Abrir laboratorio**.
3. En Inicio, pulsa **Preparar personaje y crear toma**. También puedes duplicar una acción o abrir una toma/recuperación de la lista.
4. En **Proyecto**, selecciona **Puños** para el primer ejercicio.
5. Pon nombre `MiPrimerGolpe`. En **Animar → Duración**, escribe `0.6` segundos o `36` fotogramas.

## 2. Entender la pantalla

**Sencillo · empezar** es la vista inicial: oculta curvas técnicas, calibración y capas secundarias, pero conserva las pistas, poses y pruebas. En **Completo · todas las herramientas** vuelven a aparecer esos controles. Cambiar de vista no convierte ni borra la animación.

La **Guía interactiva** recorre **Elegir → Poses → Ritmo → Probar → Guardar** con botones para ir al paso correspondiente. Se puede plegar o recorrer sin obligación de completar pasos.

- **Abajo: fotograma y línea de tiempo.** Escribe un número, mueve el deslizador o haz clic en una pista.
- **Seis pistas visibles:** posición y dedos de cada mano, arma completa y pieza/diseño seleccionado. Las demás piezas guardadas también se reproducen aunque su pista no esté seleccionada.
- **Rombos ◆:** puntos que ya has guardado. Haz clic para volver al punto exacto.
- **Línea blanca:** dónde estás ahora.
- **Izquierda:** elige qué editar y cambia sus valores.
- **Derecha:** observa el personaje y arrastra el punto de mano o arma.
- **Guía interactiva:** orienta en la vista sencilla. **Siguiente paso**, en la vista completa, propone una acción según el borrador.
- **◀ Punto / Punto ▶:** navegan por los puntos de la pista que se ve, también durante una prueba temporal.
- **Zoom:** amplía las pistas alrededor del cursor temporal; **Ctrl + rueda** sobre las pistas hace lo mismo. **Ajustar a ventana** muestra toda la toma.
- **Revisar:** lista avisos con accesos al fotograma/pista y un informe copiable. No considera vacía una receta solo por no tener puntos manuales.

Los fotogramas son una referencia de **60 fps**. Una toma de `0.6 s` termina en **F 36**. No tienes que calcular porcentajes.

## 3. Colocar la mano en un fotograma

1. Elige la pista **Derecha · posición**.
2. Ve a **F 0**.
3. En **Posición y giro**, pulsa **Volver al reposo**.
4. Pulsa **Guardar posición en F 0**.
5. Ve a **F 18**.
6. Arrastra el punto amarillo en el plano de la cámara, los ejes **X rojo / Y verde / Z azul** para restringir el movimiento, o escribe un desplazamiento pequeño en **centímetros**. **Esc** cancela el arrastre; cambiar de vista de cámara ayuda cuando un eje apunta hacia ti.
7. Revisa el brazo desde otra vista. Pulsa **Guardar posición en F 18**.
8. Ve a **F 36**, pulsa **Volver al reposo** y **Guardar posición en F 36**.
9. Pulsa **Reproducir**.

Ejemplo de prueba en F 18: `Z = 12 cm`, **solo si ese eje local avanza en la dirección que buscas**. No es una calibración universal. Si el brazo se estira demasiado, reduce el alcance.

Al cambiar el fotograma o la pista, los campos recuperan la pose correspondiente. No se quedan con los valores de la otra mano.

## 4. Poner dedos en ese fotograma

1. Pausa y elige **Derecha · dedos**, o selecciona mano derecha y **Solo dedos**.
2. Ve al fotograma deseado.
3. En **Agarre de llegada**, elige la forma deseada. Los sliders y articulaciones están en **Ajustar dedos / crear variantes**.
4. Escribe **Mantener hasta F** y comprueba **Llegar al agarre en F**.
5. Pulsa **Ver proceso completo** o usa Base / Inicio cambio / 50% / Llegada para verlo sin guardar.
6. Pulsa **3 · Guardar agarre de llegada en F …**.

**Tu caso F 0 / F 40:** guarda la mano normal en F 0; en F 40 elige **Cargador · rectangular**, **Suave**, **Mantener hasta F 20**. La barra y el visor muestran base hasta F 20, adaptación de F 20 a F 40 y cargador al llegar a F 40. No hace falta guardar otro punto en F 20. Si no había base, se conserva automáticamente la pose inicial en F 0.

**Guardar dedos no cambia la trayectoria de muñeca. Guardar posición no cambia los dedos.** Para mantener un puño durante todo el golpe, puedes guardar la mano cerrada al principio y al final, y comprobar el resultado con las mezclas de la acción.

La base de edición se obtiene sin los fundidos para no recapturar una forma atenuada. Mientras ajustas se muestra la pose sin fundidos; al reproducir vuelven las mezclas finales.

Si después de un punto parece cambiar el agarre, revisa **Siguiente pose de dedos**, el **contacto mano–pieza** y el fundido final de la acción. El estado “sin clave aquí” no crea una transición nueva. Para conservar una forma, usa **Mantener este agarre hasta el final…**; para recuperar tu base, **Volver al agarre guardado en F0**. Si la muñeca deja de seguir la pieza, el panel avisa y ofrece **Mantener mano y pieza hasta el inicio del fundido…**.

## 5. Animar el arma

1. Selecciona un equipo con objeto visible, como **AK12** o **Cuchillo**.
2. Elige **Arma** o la pista **Arma completa**.
3. Ve al fotograma y cambia centímetros/grados o arrastra el punto naranja.
4. Pulsa **Guardar posición en F …**.
5. Repite en otro fotograma.

Los puntos controlan desplazamiento/giro del soporte del arma, no las piezas internas ni un clip completo de manipulación. Las manos que sigan sus grips se desplazan con ellos; una mano liberada o gobernada por otro canal puede no acompañarla.

**Movimiento menos rígido:** en vista **Completo → Arma** pulsa **Probar movimiento natural del arma**. Ajusta intensidad, compara Antes/Después y acepta solo si mejora el gesto. Cancelar no cambia la toma. Puedes quitar la capa para recuperar el recorrido base mientras no lo hayas editado manualmente después.

Para mover el cargador o la palanca de carga, usa **Piezas / diseños → Pieza / objeto**. En AK12 son `AK12Rig/Root/Mag` y `AK12Rig/Root/Charge`. El punto cian mueve la pieza seleccionada; **Mover junto con** permite guardar también una o ambas manos en ese fotograma.

Con dos puntos de conjunto, mano y pieza comparten el recorrido. Curva la **pieza** en **Recorrido hasta el siguiente punto → CURVA COMÚN**: la mano se recalcula también. Para puntos antiguos usa **Unir mano y pieza en este tramo**; no vuelvas a dibujar a mano la misma curva.

Si la receta ya tiene movimiento, la herramienta pide confirmación antes de sustituir ese canal. Guarda una versión previa. **Solo dedos** permite editar la forma de una mano sin sustituir la trayectoria de una recarga.

## 6. Saber si un cambio está guardado

| Lo que ves | Qué significa |
|---|---|
| Aviso **POSE/DEDOS SIN CONFIRMAR** | Has ajustado una pose, pero aún no has guardado su punto. |
| Un rombo en la pista | El punto está en el borrador de la toma. |
| Asset en `Assets/AnimationLab/Authored` | La toma está guardada en disco. |

Al cambiar de fotograma con una pose pendiente puedes elegir:

- **Guardar punto:** conserva el ajuste antes de ir al nuevo fotograma.
- **Seguir editando:** no cambia de fotograma.
- **Descartar ajuste:** recupera lo que estaba grabado.

**Guardar toma como copia**, en la barra superior, confirma los ajustes pendientes y guarda un asset nuevo. No sobrescribe la versión anterior.

**Proyecto → Guardar recuperación ahora** conserva el borrador sin hornear. Las recuperaciones se pueden buscar en Inicio o en **Proyecto → Buscar… → Recuperaciones**. **Inicio** en la cabecera permite volver al navegador: si hay cambios, ofrece guardar recuperación, cancelar o descartar. Durante una prueba sin aceptar se recupera el borrador, no el candidato.

Además, **Proyecto → Respaldo automático sin horneado** conserva cada 120 segundos por defecto los cambios del borrador y ajustes pendientes, sin confirmar puntos. Se abre desde **Inicio → Respaldos automáticos y de emergencia**. Estos JSON son locales al proyecto, viven en `Library/AnimationLabRecovery` y no se exportan al juego; no borres Library si necesitas recuperarlos. Si falla esa ubicación se intenta una carpeta de respaldo en `%LOCALAPPDATA%/Temp/opencode/AnimationLabRecovery`. Si tampoco se puede escribir, se avisa y se retiene un último respaldo **solo durante la sesión de Unity**: guárdalo cuanto antes.

Si el respaldo contiene datos inválidos, se abre una **revisión segura sin reproducirlo**, no se elimina. En **Editar datos recuperados (avanzado)** puedes corregir tiempos/valores, conservar otro respaldo antes de tocar nada y volver al estudio cuando pase la validación. Ese editor técnico muestra tiempos `[0,1]` y metros del formato interno; no es el panel habitual en centímetros. También permite descartar explícitamente solo los ajustes sin confirmar, sin borrar los puntos.

Los guardados normales continúan siendo copias nuevas. Inicio agrupa las versiones creadas por esta herramienta en este equipo; **Mostrar todas las versiones guardadas** las despliega. La agrupación local se pierde al borrar Library, pero los assets permanecen.

**Revisar** permite reproducir y mover la cámara sin arrastrar poses ni pegar claves. Sus avisos son comprobaciones de datos: no certifican acabado artístico, penetraciones ni comportamiento en el juego.

## 7. Corregir un punto

1. Haz clic en su rombo o en su botón **F …** de la lista.
2. Ajusta la pose.
3. Pulsa Guardar posición o Guardar dedos.

**Quitar** solo elimina el canal de la pista seleccionada. Si una toma antigua tenía posición y dedos juntos, quitar la posición conserva sus dedos, y quitar dedos conserva la posición.

Los puntos antiguos que no caigan exactamente en un fotograma se conservan: pulsa el rombo para editar su tiempo exacto, en vez de aproximarlo con el número de fotograma.

### Mover un punto a otro fotograma

- **Arrastra su rombo horizontalmente** en la línea de tiempo. El marcador cian muestra el destino; el cambio se aplica al soltar. **Esc** o perder el foco cancela sin mover nada.
- Para precisión: selecciona el rombo y abre **Animar → Mover este punto en el tiempo → Mover a F**. Escribe el número y pulsa **Mover**.
- Por defecto solo cambia el canal seleccionado. En una mano puedes activar **También posición + dedos**: mueve ambos canales si existen en el origen; no inventa una clave de muñeca.
- Si el destino ya tiene un punto del canal que mueves, se pide confirmación. Reemplazar posición conserva los dedos de destino, y reemplazar dedos conserva su posición.
- En una pieza, la muñeca enlazada se recompila para seguirla. **Dedos enlazados en este punto** mueve también las poses de dedos guardadas exactamente en ese origen; si no lo marcas, conservan sus tiempos.
- Una muñeca compilada que sigue una pieza no se mueve por separado: mueve el punto de la **pieza**, o separa el recorrido conscientemente.
- No cambia la duración total ni el clip fuente. Admite **Undo/Redo**. Un contacto que entre en un fundido de peso cero se rechaza sin aplicar cambios.

### Crear una transición fácil

1. Guarda dos poses de **posición** o dos **agarres** en una misma pista.
2. Selecciona un fotograma de su tramo y despliega **Crear transición fácil · poses, ritmo y dedos**.
3. Pulsa **Usar los dos puntos del tramo actual**.
4. En posición, elige intención: **Acercarse**, **Pasar por aquí**, **Llegar/agarrar**, **Soltar** o **Volver a la pose inicial**. En Solo dedos, configura directamente mantener/adaptar y el agarre.
5. Ajusta **Empezar**, **Llegar** y **Asentar**. Puedes arrastrar las divisiones de **Mantener → Viajar/Adaptar → Asentar**; son ajustes de una propuesta, no cambios todavía guardados.
6. Deja **Conservar todos los dedos** si solo quieres movimiento. Si eliges un agarre, define cuándo empezar a adaptarlo.
7. Pulsa **Probar transición**. En el visor compara **Antes/Después**, reproduce el tramo y elige **Aceptar transición** o **Cancelar/ajustes**.
8. Guarda la toma como copia.

**Ejemplo orientativo:** pose inicial F0, destino F40; empezar F20, llegar F40 y adaptar dedos desde F30. Añadir 2–4 F de asentamiento puede ayudar a leer la llegada, pero no es una animación validada para todos los equipos.

**Límites importantes:** el asistente trabaja entre puntos consecutivos, no simplifica automáticamente un clip de recarga a dos poses. **Pasar por aquí** necesita un punto posterior y suaviza también el siguiente tramo, conservando las poses. **Volver** usa el primer punto de posición de esa pista, que no tiene por qué ser una guardia universal. **Contacto** frena, pero no enlaza una mano a una pieza por sí solo. Cambiar dedos crea poses manuales y puede sustituir la dinámica de dedos del clip; conservarlos no la sustituye.

### Copiar y pegar un fotograma

1. Selecciona la pista y el fotograma que quieres repetir.
2. Junto a la línea de tiempo, elige **Pista seleccionada** y pulsa **Copiar F …**.
3. Ve al destino y pulsa **Pegar en F …**. También funcionan **Ctrl+C / Ctrl+V** si no estás escribiendo en un campo.
4. Para repetir muñeca y dedos juntos, elige **Mano: posición + dedos**.

La copia contiene la pose del fotograma, incluso si no había rombo, y conserva ajustes de curva cuando copia una clave exacta. No copia un intervalo completo. Se pega en la misma mano/pista y equipo; una pieza requiere la misma pieza. Los dedos pegados son independientes y la transición conserva su duración en fotogramas. Pegar admite Undo/Redo y no modifica las otras pistas. El portapapeles es interno a la ventana y se vacía al cerrar/recompilar.

**Curvar con solo dos puntos:** en Animar, sitúa el cursor entre ambos y abre **Recorrido hasta el siguiente punto → Libre**. Mueve los controles de salida/entrada para dibujar una curva en S; Alt+arrastrar ajusta profundidad. Desde el último punto también puedes editar el tramo anterior. Si aún solo hay un clip, capturar sus dos extremos requiere confirmación y simplifica el movimiento: guarda una copia antes.

## 8. Ayudas visuales y navegación

- **Amarillo:** mano seleccionada y referencias de sus puntos locales de posición, con etiquetas F.
- **Naranja:** punto de edición del arma.
- **Cian:** pieza u objeto diseñado.
- **Verde en la línea de tiempo:** puntos de dedos.

La referencia de puntos es una guía local, no una simulación de contacto ni la trayectoria física de la hoja. Arrastrar un punto mueve en el plano de la cámara; usa los números y otra vista para ajustar profundidad. Arrastrar el fondo orbita y la rueda hace zoom en vista externa.

Para desplazar la vista usa botón central o Shift+arrastrar. Mantén botón derecho y W/A/S/D para navegar, Q/E para bajar/subir. **Encuadrar selección** acerca la mano/pieza; **Restablecer vista** recupera el encuadre inicial. Los campos **Local / Global (escenario)** cambian los ejes de edición, no la cámara.

## 9. Antes de terminar

1. Reproduce a velocidad `1`.
2. Comprueba **Primera persona** y **Personaje**.
3. Prueba mirar arriba/abajo y Aim si corresponde.
4. Guarda la toma como copia.
5. Si quieres llevarla al juego, exporta la copia guardada y asigna su **Action** manualmente allí.

El laboratorio no ejecuta daño ni consume munición al previsualizar. Tampoco sincroniza automáticamente con el juego.

Para profundizar en ritmo, dedos, cuchillo, recargas y exportación, consulta [la guía detallada](GUIA_PRACTICA_ANIMATIONLAB.md).

Para piezas individuales, diseños de cubos/esferas/cilindros y agarres preparados o procedurales, consulta [Piezas, objetos y agarres](GUIA_PIEZAS_OBJETOS_Y_AGARRES.md). El ajuste de dedos es aproximado y necesita revisión visual; el movimiento conjunto no crea una unión permanente entre mano y objeto.

## Prueba corta con una persona nueva

Para saber si la mejora realmente ayuda, pide a alguien que haga esto leyendo solo las ayudas de la ventana y esta hoja:

1. Crear una toma de puños de `0.6 s`.
2. Poner la mano derecha en una posición distinta en F 18, volviendo al reposo en F 36.
3. Cerrar sus dedos sin cambiar ese recorrido.
4. Reproducir, guardar, reabrir y corregir el punto de F 18.

Anota dónde duda, qué botón esperaba encontrar y si distingue **ajuste temporal**, **punto del borrador** y **toma guardada**. Las pruebas técnicas comprueban datos y reproducción, pero no sustituyen esta prueba de comprensión con usuarios.
