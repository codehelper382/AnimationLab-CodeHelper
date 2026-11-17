# Animation Lab Studio: recargas visuales sin perder la toma

> **Nota de disponibilidad:** la coreografía AK12 de cinco etapas documentada en la entrega histórica fue retirada del código activo durante la consolidación. `../VALIDACION.md` registra sus pruebas en una copia aislada; `../Asistente recarga AK12/LEEME.md` describe esa versión histórica, no la interfaz actual. En el Studio activo siguen disponibles los flujos descritos en esta guía. La reserialización con el esquema actual elimina el campo legacy del asset AK12; no guardar los originales hasta definir su recuperación/migración.

## 1. Nueva distribución

- **Cabecera:** nombre/estado del borrador y Guardar copia.
- **Recargas:** crear y probar gestos de una mano, puntos visuales, fases y variantes.
- **Recarga guiada (en desarrollo):** editor de patrones propios con piezas, fases, manos/canales, bindings y captura de poses locales de muñeca/pieza. Aún no genera ni inserta movimiento; no reemplaza las plantillas o el flujo avanzado.
- **Animar:** duración en segundos/fotogramas, claves manuales de muñeca, dedos, arma y piezas; controles de curva por tramo.
- **Proyecto:** equipo, duración/tipo, abrir/guardar/exportar y generador avanzado mano + pieza.
- **Inicio:** crear, duplicar una acción o abrir tomas/recuperaciones sin buscar assets manualmente.
- **Revisar:** avisos navegables, resumen y copia de informe; visor sin manipuladores de edición.
- **Visor grande:** Personaje o Primera persona, cámara y mirada/Aim.
- **Barra fija sobre el visor:** Probar/Actualizar, Antes/Después, Aceptar y Cancelar; no se oculta al desplazar el inspector.
- **Línea de tiempo inferior:** fotogramas, seis fases de color, pistas y reproducción.
- **Copiar / Pegar junto a la línea de tiempo:** reutilizar la pose de una pista o de una mano completa en otro fotograma, con Undo y sin reescribir posiciones.

Se usa fuera de Play. Mantiene la escena aislada, el único grafo y el solver propio.

## 2. Flujo recomendado

1. Prepara personaje/equipos. **Animar → Duración** permite escribir segundos o fotogramas a 60 fps; por ejemplo `2 s` para F 0–120. También está en Proyecto. Cambia la escala temporal, no las posiciones de las claves.
2. En Recargas, elige **Base del movimiento**: para AK12 se propone **Recarga existente · normal**; también está la vacía. Conserva su muñeca y dedos como referencia. Elige mano e intervalo.
3. Puedes **Probar sin insertar** directamente. Pulsa **Crear puntos visuales** si quieres corregir la referencia.
4. En el visor, selecciona y arrastra el contacto, suministro/extremo e inserción. El panel permite ajustar orientación y dedos del punto seleccionado.
5. Pulsa **Probar sin insertar**.
6. Compara **Antes · original** y **Después · prueba**; reproduce con mirada/Aim y primera persona.
7. Ajusta puntos, fases o estilo. Al soltar un punto/división se recalcula la prueba; con campos/sliders usa **Actualizar prueba**. Si hay cambios pendientes, un aviso explica que el personaje todavía muestra el último cálculo.
8. **Aceptar manos** inserta el resultado y la receta en la toma con Undo. **Cancelar** elimina el candidato sin modificar las claves originales.
9. Guarda una copia normal de la toma para conservar el resultado final.

Durante la prueba no se editan claves manuales ni se cambia equipo/toma desde Proyecto. Si la toma original cambia externamente —por ejemplo, un Undo anterior—, Aceptar se bloquea para no sobrescribir cambios nuevos. Cancela y vuelve a probar.

**Usar duración original** ajusta solo la duración del borrador. **Abrir recarga completa como copia** abre otra receta independiente con sus clips, objetivos y movimientos originales; confirma antes de descartar un borrador. Es distinto de la prueba, que inserta **solo la mano elegida**.

**Gesto procedural · orientativo** conserva los ocho guiones genéricos como alternativa manual. No son la base recomendada para reproducir la recarga existente del AK12.

El panel **Recarga guiada · fases, piezas y canales** permite crear una secuencia vacía o copiar solo el patrón de otra toma. Las fases son datos configurables y sus tiempos se editan en fotogramas; puede capturarse la muñeca respecto a guardia, una pieza/mecanismo vinculado respecto a su padre y la pose evaluada del holder del arma en ese fotograma respecto a su padre. Los valores locales (posición en cm en la interfaz, metros en el formato) se pueden corregir con Undo. Esto guarda referencias numéricas: no anima ni hornea la raíz visual calibrada o el holder. El panel muestra errores/avisos, pero **todavía no genera un candidato**, no anima automáticamente piezas y no modifica munición ni lógica del juego; consulta el plan enlazado desde `CONTEXTO_Y_USO_ANIMATIONLAB.md`.

Cerrar/recompilar/entrar en Play recupera la **receta editable y canales actuales del borrador**, no una prueba sin aceptar ni una instantánea antigua si el original cambió externamente. Al reabrir esa recuperación, vuelve a Probar y Aceptar.

## 3. Puntos visuales

Los puntos representan posiciones de la **muñeca en el espacio local de su guardia**, no vértices del cargador ni anclajes mundiales permanentes.

- **Clic:** seleccionar un punto.
- **Arrastrar:** moverlo en el plano de la cámara, conservando su profundidad inicial.
- **Ctrl+clic en el visor:** añadir un punto en el fotograma actual. Usa la profundidad del punto seleccionado; no es un raycast sobre la superficie del arma.
- **En este fotograma:** añadir un punto sobre la curva existente y después moverlo.
- **Ir al punto:** saltar a su tiempo, teniendo en cuenta las fases y el estilo.
- **Quitar:** retirar ese control sin borrar las demás fases (quedan al menos dos).
- **Muñeca local (cm) / Orientación:** ajuste preciso, con conversión de escala del rig.
- **Dedos −1:** seguir los dedos de referencia o la plantilla/fase. De `0` a `1`: abrir/cerrar alrededor de ese control; los vecinos automáticos delimitan la corrección.
- **Salida hacia el siguiente:** interpolación del control; Cinematic permite frenar o pasar por el siguiente.
- **Ancla de contacto:** el círculo marca el punto sobre el que se amplía/reduce el gesto. Puedes elegir otro con el botón del inspector. Los puntos coincidentes se separan solo en pantalla con una pequeña línea, para poder seleccionarlos sin cambiar sus coordenadas.

Si prefieres colocar primero la muñeca con los controles manuales, usa **Colocar mano** antes de crear puntos. En Animar, pulsa **Usar esta pose para la recarga · sin crear clave** antes de cambiar de pestaña: captura el ajuste temporal y vuelve a Recargas sin crear un punto de muñeca.

Los ejes dibujados son los ejes de esa muñeca, **no una orientación de palma universal para todos los avatares**. Revisa desde otra vista y corrige el giro. No hay ajuste universal de penetraciones ni reconocimiento automático de la mecánica del arma.

La línea es una referencia local con sus padres congelados en el fotograma actual. La mezcla con el movimiento original, los fundidos y el movimiento de los padres pueden hacer que la trayectoria mundial del personaje no coincida exactamente con esa línea. Los extremos del intervalo regresan a la toma original, no fuerzan el control local a peso completo.

En una recarga existente, los controles conservan la pose original muestreada: mover uno añade una corrección sobre el movimiento denso, no reemplaza todo el gesto por una curva entre pocos puntos. La línea dibuja esa referencia muestreada más sus correcciones. Los campos de curvatura/Bézier modifican la corrección, no las tangentes del clip fuente.

Si recalibras el objetivo de recarga, el dibujo, marcadores y campos se actualizan con la referencia recalibrada, conservando las correcciones editadas. La línea sigue siendo una aproximación muestreada; revisa también la reproducción.

El modo sencillo sigue animando **solo la mano elegida**. Arma, cargador, tambor, corredera, cuerda, palas y flecha se animan aparte. Usa conscientemente el generador avanzado de Proyecto si quieres sustituir también una pieza.

## 4. Fases: menos puntos técnicos y más intención

La línea de tiempo muestra:

**Preparar → Agarrar → Extraer/viajar → Insertar/volver → Soltar → Recuperar**.

Los nombres describen un esquema común; en arco significan aproximación, enganche, tensión, anclaje, liberación y recuperación. En cerrojo representan aproximación, pinza/desbloqueo, tirar, empujar, bloqueo/salida y recuperación. No ejecutan mecanismos de juego.

- Pulsa un bloque para seleccionar la fase y saltar a su inicio.
- Arrastra una división para alargar una fase a costa de la vecina. No cambia la duración total de la toma.
- En **Ritmo / dedos**, puedes escribir el fotograma de fin de la fase.
- **Repetir tramo** limita la reproducción al intervalo; **Solo fase seleccionada** permite revisar una pequeña acción repetidamente.
- **Restaurar ajustes de esta fase** restaura su división de salida, elimina su override de dedos y controles libres internos; no es deshacer toda la inserción ni recuperar automáticamente el clip antiguo.

Las muestras a 60 Hz permanecen en el resultado para reproducirlo/exportarlo. Se ocultan por defecto para no llenar la interfaz de cientos de rombos; en Animar puedes desplegar **Muestras técnicas** y editar las cercanas al cursor. No se han eliminado esos datos.

## 5. Estilo y dedos

En **Ritmo / dedos**:

- **Compacta ↔ amplia:** tamaño del recorrido alrededor del contacto.
- **Suave ↔ enérgica:** distribución de velocidad dentro de las fases.
- **Recuperación amortiguada:** ritmo de la última fase.
- **Curvatura del viaje:** intensidad de los arcos de los puntos, sin añadir balanceos al contacto.
- **Pausa de asentamiento (F):** mantener el contacto al principio de la fase de soltar; se toma tiempo de esa misma fase, sin alargar la toma.
- **Cierre del pulgar:** aproximación del cierre; revisa la técnica/rig.
- **Forma inicial de dedos:** automática o uno de los perfiles preparados.
- **Dedos propios de esta fase:** ajustar apertura/cierre sin mover la muñeca ni sustituir los dedos de referencia de las demás fases.
- **Suave / Táctica / Enérgica:** puntos de partida de estilo, no animaciones finales diferentes certificadas.

No se añade ruido para fingir naturalidad ni se rompe deliberadamente un contacto estable. Para pausas más claras, ajusta las fases y usa contactos con velocidad de llegada cero. Los dedos siguen siendo aproximaciones Humanoid, no contacto físico resuelto contra una malla.

Los avisos de **alcance**, **mano fuera del encuadre** y **recorrido amplio** son ayudas geométricas sencillas. No son detección precisa de colisiones ni garantías de seguridad/estabilidad del agarre.

## 6. Curvas manuales con mejor llegada

En **Animar**, selecciona una pista de posición y abre **Recorrido hasta el siguiente punto**. Basta situar el cursor dentro del tramo; desde el último punto se edita el tramo anterior. **Ir al inicio/final** ayuda a inspeccionar ambos extremos.

Si la receta solo tiene clips y aún no hay dos claves de posición, **Capturar inicio y final como dos puntos…** muestrea sus extremos con confirmación. Conserva dedos, clip de personaje y otras pistas, pero **sustituye el recorrido de esa muñeca por dos puntos**: no conserva la forma interior del clip. Usa la referencia de Recargas para corregirla sin esa simplificación.

| Botón / modo | Uso |
|---|---|
| Directo | Recta con velocidad constante. |
| Arco suave | Arco con frenada en salida/llegada y desvío central. |
| Contacto | Llegada estable sin arco extra. |
| Continuo | Suaviza este tramo y el siguiente; exige un vecino posterior. |
| Cinemático | Control explícito de desvío, velocidad de salida y velocidad de llegada. |
| Libre / Bézier | Dos controles independientes de salida/entrada; permite curvas en S sin mover los extremos. |

El punto **violeta** en el centro del tramo se puede arrastrar para cambiar la curvatura. En Libre aparecen dos controles. Arrastra en el plano de cámara o usa **Alt+arrastrar** para profundidad. Sus campos usan centímetros locales, incluso con el editor de posición en Global.

La curva cinematográfica usa dos tramos cúbicos con un centro editable; Bézier usa sus dos controles independientes. Preview, curvas de la acción y clip Generic de piezas comparten la misma compilación. No introduce un nuevo grafo. Las tomas antiguas mantienen su modo Original y no se suavizan automáticamente.

Los **conjuntos mano + pieza** nuevos comparten recorrido: la pieza controla la curva y la muñeca se recompila conservando contacto a 60 Hz. Para conjuntos antiguos, **Unir mano y pieza en este tramo** captura sus referencias. Las pistas que no hayas unido siguen siendo independientes.

## 7. Variantes y comparación

En **Variantes**, guarda una receta de puntos, fases y estilo con un nombre propio. Se guarda como asset `LabHandVariant`, separado de la toma final. Para cargarla se exige el mismo `equipmentId`; conserva el intervalo actual, pero recupera la mano y puntos de la variante. Revisa también que el avatar/guardia sean compatibles.

No es una asignación automática de acciones al juego. Para obtener una acción exportable: prueba, acepta y guarda la toma normalmente.

## 8. Comprobación y límites

En **Animar → Arma**, **Probar movimiento natural del arma** prepara una capa secundaria pequeña con Antes/Después/Intensidad/Aceptar/Cancelar. Mantiene las poses principales, conserva la base para restaurar y recompila los contactos; no añade cámara ni física. Una edición posterior de curvas impide restaurar silenciosamente una base antigua. [Análisis de BASE CORRECTA](ANALISIS_BASE_CORRECTA.md).

En dedos se distingue **clave guardada**, **sin clave aquí** y **destino sin guardar**. Sin clave no aparece un modo Suave nuevo por navegar; el selector es una orden para preparar otra pose, no la identidad de un tipo guardado. **Volver al agarre guardado en F0** permite recuperar esa pose y **Mantener este agarre hasta el final…** quita futuras poses solo de dedos, con Undo. El fundido global de salida sigue activo.

El panel también muestra el **contacto mano–pieza**: si el último enlace termina antes del final, avisa de que la muñeca dejará de seguir la pieza. **Mantener mano y pieza hasta el inicio del fundido…** añade un punto de la pieza con el último contacto y recalcula solo esa muñeca; no cambia dedos, arma ni la otra mano. El solver exige peso completo, así que el contacto se detiene al empezar el fundido y los últimos fotogramas se atenúan con él.

Para agarres manuales, **Animar → Solo dedos** ofrece un flujo explícito: **Agarre de llegada**, **Mantener hasta F**, **Llegar al agarre en F**, barra mantener/adaptar y vista previa sin guardar. Para base hasta F 20 y cargador en F 40, elige Suave y Mantener hasta F 20. El visor permite probar F 0/20/30/40 o reproducir el proceso sin mover el destino F 40. [Paso a paso](GUIA_PIEZAS_OBJETOS_Y_AGARRES.md).

**Tools → CodeHelper → Animation Lab → Verificar estudio visual y pruebas temporales** comprueba transacciones, puntos, fases, curvas y persistencia. `AnimationLabStudioChecks.RunAll` ejecuta también las cinco suites anteriores.

**Verificar recargas existentes y curvas libres** añade duración/Undo, referencias normal/vacía, dedos originales y overrides locales, controles aditivos, copia completa, edición de dos puntos y bake Bézier. `AnimationLabReferenceChecks.RunAll` incluye las siete suites.

**Verificar agarres, transiciones y copiar fotogramas** comprueba el agarre inicial, variantes, transiciones de dedos, copiar/pegar por pista/mano, Undo/Redo y bake. `AnimationLabGripFrameChecks.RunAll` incluye las ocho suites.

**Verificar recorrido combinado y transición visual** añade candidato F 0/20/40, contactos al curvar/girar piezas, fundidos/recaptura, Undo, persistencia y bake sin constraints runtime. `AnimationLabCombinedChecks.RunAll` incluye las nueve suites.

Son comprobaciones técnicas, no una certificación de acabado artístico ni una prueba de facilidad de uso con principiantes. Para ritmo por arma, consulta [los guiones por fotogramas](GUIA_RECARGAS_POR_FOTOGRAMAS.md). Antes de exportar revisa contactos, dedos, alcance, mirada/Aim y primera persona a velocidad normal.

## 9. Inicio, revisión y navegación

- Inicio permite preparar una toma, elegir una acción existente para copiar o buscar tomas y recuperaciones por nombre, equipo o tipo. Las recuperaciones no se borran al abrirlas; se abren como borrador y necesitan un guardado normal para obtener el resultado final.
- El botón **Inicio** de la cabecera vuelve al navegador. Con trabajo pendiente pregunta si guardar una recuperación, cancelar o descartar.
- **Siguiente paso** propone guardar un ajuste pendiente, preparar el primer punto, completar una pista o guardar una copia. No sustituye silenciosamente una trayectoria basada en clips, Reach o curvas existentes.
- **Revisar** comprueba duración, fundidos, identidad de equipo, puntos duplicados, fin de contacto y pistas sin pieza. Un aviso puede abrir Animar en el objeto y fotograma correspondientes. Los contactos, dedos y fundidos siguen siendo canales distintos.
- Revisar no permite arrastrar posiciones ni pegar claves; sí reproducir, orbitar, navegar en el tiempo y usar los presets de cámara. Guardar copia sigue siendo una acción explícita.
- **◀ Punto / Punto ▶** navegan por la misma toma mostrada en los rombos, incluido un candidato temporal. El **Zoom 1×–8×**, **Ctrl + rueda** y **Ajustar a ventana** facilitan encontrar puntos densos sin cambiar los tiempos guardados.
- **Frontal / Lateral / Tres cuartos / Superior** cambian solo la cámara externa. No modifican el personaje ni la toma.
- Cada guardado normal conserva la política de **copia con ruta única**; los respaldos periódicos locales descritos abajo no sustituyen ese guardado ni aceptan propuestas.

**Tools → CodeHelper → Animation Lab → Verificar inicio, revisión y navegación** ejecuta `AnimationLabUsabilityChecks.Run`. `RunAll` añade las diez suites anteriores. La captura `AnimationLabUsabilityCapture.Run` necesita una copia temporal con editor gráfico, **sin batchmode**; cierra ese proceso de Unity al terminar.

## 10. Transiciones fáciles y cambio de fotogramas

En **Animar**, después de elegir pista, aparecen **Mover este punto en el tiempo** (si hay una clave exacta) y **Crear transición fácil**. El asistente usa dos puntos consecutivos: mantiene sus poses, propone un inicio de movimiento, una llegada y un asentamiento. Si eliges **Destino = primer punto de pista**, reemplaza explícitamente la pose de llegada por esa primera pose.

Los presets **Acercarse / Pasar / Llegar-agarrar / Soltar / Volver** preparan ajustes; no son animaciones acabadas. **Pasar** usa interpolación suave entre vecinos en el tramo y su continuación para compartir tangente de llegada/salida; requiere un punto posterior, no admite asentamiento y puede modificar la curva siguiente sin cambiar sus poses. Los demás permiten arco local y frenada.

La barra **Mantener → Viajar/Adaptar → Asentar** edita el ritmo de la propuesta arrastrando divisiones sobre un rango temporal estable, hasta antes del vecino posterior o el final de toma. El extremo derecho permite añadir asentamiento incluso desde cero: si coincide con la llegada, su tirador **cian está abajo** y el de llegada **blanco arriba**. Mover la llegada conserva la duración del asentamiento. Esc o perder foco cancela el ajuste de la barra. **Probar** genera una copia independiente: Antes/Después, reproducción del tramo, Aceptar con Undo y Cancelar sin insertar. Mientras hay prueba se bloquean edición manual, otras pruebas, pegado y guardado normal. Aceptar comprueba que el borrador no haya cambiado. La recuperación conserva el borrador sin aceptar el candidato. Los últimos ajustes del asistente se recuerdan localmente por toma; al volver a elegir el mismo tramo se recuperan si sus extremos siguen coincidiendo. No forman parte del paquete portable.

El asentamiento mantiene también los dedos que hayas elegido cambiar; si encuentra otro agarre dentro de ese intervalo, se rechaza para no borrarlo. Las copias de variantes se normalizan al avatar antes de la prueba para corresponder al horneado Humanoid. Los fundidos globales siguen activos y pueden atenuar las poses al principio/final.

**Dedos** es opcional: conservarlos, usar el agarre guardado en la llegada, abrir, base F0, tipos preparados o variante compatible. Se conservan otras manos y canales. Un cambio explícito de dedos crea poses manuales (base F0 si falta), no conserva necesariamente la animación dinámica de dedos del clip anterior. En una pista Solo dedos el primer límite del bloque es el inicio de adaptación; asentamiento añade una pose mantenida. En piezas, configura dedos por separado o con los conjuntos existentes.

**Los dedos terminan junto con la muñeca → Últimos fotogramas** expresa la duración relativa a la llegada; mover Llegar en F desplaza también el inicio de dedos. Desactívalo para indicar un inicio independiente. En Solo dedos, **Conservar forma · editar solo el ritmo** conserva la forma de destino, no su mezcla anterior: el propósito de esa pista es retimar la adaptación.

**Recto con frenada** unifica las antiguas opciones equivalentes Directo/Contacto del asistente; no crea contacto físico. En los recorridos avanzados, **Continuo** suaviza este tramo y el siguiente y requiere un vecino posterior; **Frenar** se detiene. Los mensajes junto a los campos impiden probar ritmos fuera de rango, cruces del vecino o adaptaciones que borrarían otro agarre.

**Mover fotogramas** admite arrastre de rombos o campo numérico. Se muestra el destino sin alterar la toma hasta soltar; Esc/perder foco cancela. Se mueven tiempos normalizados, con destinos a 60 fps de referencia, sin cambiar duración ni clips fuente. Las claves combinadas antiguas se separan/unen por canal conservando el otro. Reemplazar un destino requiere confirmación. Posición + dedos mueve solo los canales que realmente existen allí; no crea muñeca si solo había dedos.

Mover una pieza recompila sus manos enlazadas; mover una muestra de muñeca enlazada se rechaza. Puede optarse por mover las poses de dedos enlazadas que coinciden con ese punto. Los contactos no se convierten en constraints runtime, no se retira el fundido dos veces y una compilación incompatible se rechaza sin aplicar la edición.

Los puntos densos de una plantilla siguen siendo muestras; mover uno no retima automáticamente toda la receta de Recargas. Si regeneras esa receta después, vuelves a generar sus canales. Para un gesto entero, usa las fases de Recargas o construye transiciones sobre controles manuales.

**Verificar transiciones y mover fotogramas** (`AnimationLabTransitionChecks.Run`; `RunAll` incluye las doce suites) comprueba movimientos independientes/conjuntos, colisiones, tiempos off-grid, Undo/Redo, mantener/viajar/asentar, dedos y contactos. `AnimationLabTransitionCapture.Run` verifica en editor gráfico aislado el asistente y eventos reales de arrastre/Escape; cierra solo ese proceso de comprobación.

## 11. Flujo sencillo y protección del trabajo

- **Sencillo** muestra la guía Elegir/Poses/Ritmo/Probar/Guardar y es la vista inicial. **Completo** recupera todas las herramientas avanzadas sin alterar canales. Los avisos distinguen muñeca compilada de una pieza, dedos independientes y pistas densas; no atribuyen automáticamente procedencia manual a una muestra.
- Los ejes del visor permiten traslación restringida; Local sigue la referencia de guardia/pieza y Global los ejes del escenario. No son constraints permanentes. Esc/perder foco restaura la pose previa al arrastre.
- **Revisar** detecta datos no finitos y tiempos fuera de `[0,1]`; guardado final y candidatos rechazan datos inválidos antes de aplicarlos. Las tangentes infinitas de Step son válidas, no se sustituyen.
- Un respaldo inválido se abre en **revisión segura**, sin evaluar su grafo. Conserva valores y pendientes para reparación explícita sobre el borrador; no expone la asignación de Action ni edita su asset fuente. El editor técnico usa tiempos normalizados y metros, permite conservar otro respaldo antes de reparar y exige revalidar antes de volver al estudio.
- La edición de recorrido/calibración restaura los valores previos si falla su compilación. Las claves legacy con posición y dedos separados en el mismo instante se buscan por canal.
- **Proyecto → Respaldo automático** guarda JSON local sin bake (120 s por defecto, configurable entre 30 y 900). Incluye ajustes pendientes; no acepta pruebas ni guarda sus canales candidatos. Inicio muestra el último por toma o su historial. No se borran automáticamente respaldos antiguos.
- Al cerrar se intenta `Recovery_*` normal. Si falla, se conserva un respaldo alternativo del estado anterior al intento, incluidos ajustes sin confirmar. Ubicación principal `Library/AnimationLabRecovery`, alternativa `%LOCALAPPDATA%/Temp/opencode/AnimationLabRecovery`; como último recurso, retención de sesión con aviso explícito, no garantía frente a un cierre de Unity. Los JSON requieren las mismas dependencias/GUID del proyecto y no son un formato de intercambio.
- Inicio agrupa las nuevas copias por linaje local; un buscador o **Mostrar todas las versiones guardadas** permite ver las anteriores. Nunca se sobrescribe el asset original. El catálogo está en Library y no viaja al juego.

**Verificar seguridad y flujo sencillo** ejecuta `AnimationLabImprovementChecks.Run`; `RunAll` incluye trece suites. Añade regresiones de dedos separados candidato/aceptado, recuperación fallida, respaldo periódico, datos no válidos, rollback y continuidad. `AnimationLabImprovementCapture.Run` ejercita la ventana real y termina el editor gráfico aislado. No equivale a una prueba con usuarios principiantes ni a una revisión artística.
