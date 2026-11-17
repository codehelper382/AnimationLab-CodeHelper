# Animation Lab: recorridos curvos y recargas en tres pasos

Usa el laboratorio **fuera de Play**. Son herramientas de animación: no transfieren munición ni disparan durante el preview.

**Modo recomendado para empezar:** [Recargas fáciles: solo manos y guiones por fotogramas](GUIA_RECARGAS_POR_FOTOGRAMAS.md). El panel **Recargas rápidas · insertar SOLO MANOS** inserta un gesto en un tramo sin seleccionar piezas ni hacer tres capturas. Esta guía conserva debajo el flujo avanzado mano + pieza.

## 1. Curvar el movimiento entre dos fotogramas

1. Guarda dos puntos de **posición** de una mano, arma completa o pieza/diseño.
2. En **Animar**, sitúa el cursor entre esos puntos o pulsa uno de sus rombos. Desde el último punto se edita el tramo anterior.
3. Abre **Recorrido hasta el siguiente punto → Forma del tramo**:

| Forma | Resultado |
|---|---|
| Original (compatible) | Conserva el comportamiento anterior: manos/arma con tangentes cero; piezas rectas. No cambia tomas antiguas por sí solo. |
| Recto · velocidad constante | Segmento recto sin acelerar ni frenar. |
| Curva suave · entre vecinos | Curva espacial que pasa por los puntos y toma dirección de sus vecinos. Necesita al menos tres puntos para apartarse de la recta; puede sobrepasar los extremos. |
| Arco · desvío editable | Desvía el centro del segmento respecto a la recta, incluso con **solo dos puntos**. |
| Recto · acelerar / frenar | Mantiene la recta y cambia la velocidad, llegando con velocidad cero. No es una curva espacial. |
| Cinemático · arco con llegada suave | Dos cúbicas con centro editable y velocidades de salida/llegada. |
| Bézier · dos controles libres | Dos controles independientes, incluso con dos puntos; permite curvas en S. |

4. Para **Arco**, ajusta **Desvío central local (cm)**. Por ejemplo Y = `5` separa el centro del tramo 5 cm sobre su recta en el eje Y local. Los extremos no se mueven.
5. Reproduce, revisa el arco y guarda la toma.

Con **Libre/Bézier**, arrastra los controles de salida y entrada; **Alt+arrastrar** cambia su profundidad. También puedes escribir sus offsets locales en centímetros. **Duración** está directamente en Animar, en segundos/fotogramas.

Si solo hay un clip, **Capturar inicio y final como dos puntos…** exige confirmación: conserva dedos y fuentes, pero reemplaza la forma interior de esa muñeca por una trayectoria entre sus extremos. Para corregir una recarga conservando el gesto denso, usa **Recargas → Base del movimiento → Recarga existente**.

La configuración se guarda en el punto de **salida**, hasta el siguiente punto. El último punto no tiene tramo propio de salida: seleccionarlo permite editar el anterior. Los dedos siguen siendo una pista independiente; este control cambia posición/giro, no la forma de los dedos.

El desvío usa los ejes locales de la guardia/padre de la pieza incluso si el editor de posición está en Global. Las unidades visibles son centímetros físicos; se convierten para huesos importados con escala.

La línea de la vista muestra la curva seleccionada sin fundidos y con sus padres congelados en el fotograma actual. Sirve para editar; no es una predicción mundial de todos los movimientos del arma o la hoja.

## 2. Asistente de recarga: elegir pieza y plantilla

Después de **Preparar personaje y equipos**, debajo de **Equipo y acción**, despliega:

**Avanzado · generar mano + pieza (3 referencias)**.

1. Selecciona **Pieza de la plantilla**. En AK12:
   - Cargador: `AK12Rig/Root/Mag`.
   - Palanca: `AK12Rig/Root/Charge`.
2. Selecciona **Ejemplo prefabricado** y **Mano que manipula**.
3. Ajusta duración, repeticiones cuando proceda y **Arco del recorrido (cm)**.
4. Completa las tres referencias siguientes.

Para un arma cuyo cargador/tambor no existe como Transform/hueso separado, no puedes separar automáticamente su malla. Puedes crear un diseño de referencia con primitivas para ensayar la animación.

## 3. Colocar las tres referencias

### Paso 1: dónde está la pieza en reposo

1. Pulsa **Colocar pieza** en el paso 1.
2. Ajusta el cargador encajado, el tambor cerrado, el cerrojo cerrado o el cartucho en posición de inserción.
3. Usa Local/Global, campos en cm o arrastra el punto cian de la vista.
4. Pulsa **Capturar 1**.

### Paso 2: dónde contacta la mano

1. Pulsa **Colocar mano**.
2. La referencia de pieza en reposo se muestra mientras colocas la muñeca.
3. Sitúa y orienta la muñeca donde debe agarrar. Comprueba palma, pulgar y alcance.
4. Pulsa **Capturar 2**.

Se guarda la relación muñeca–pieza, no una referencia a un objeto de la escena. La plantilla genera después una forma aproximada de dedos Humanoid; este paso define la muñeca, no garantiza que el agarre encaje sin correcciones.

### Paso 3: hasta dónde sale la pieza

1. Pulsa **Colocar pieza** en el paso 3.
2. Lleva el cargador fuera, el tambor a su posición abierta o el cerrojo hacia atrás. La mano acompaña la pieza según el contacto capturado.
3. Ajusta también el giro. Para un cerrojo, el giro capturado sirve como referencia de levantar/desbloquear antes del desplazamiento.
4. Pulsa **Capturar 3**.

**Capturar** guarda la referencia y termina el ajuste temporal; no necesitas pulsar Guardar posición para estos pasos. No crea claves de animación hasta generar. Si vuelves a capturar el reposo o cambias de pieza, debes repetir contacto y extremo. Si cambias de mano, debes repetir el contacto.

Las referencias se guardan dentro de la toma y su recuperación. Puedes volver a los pasos y capturarlas de nuevo.

## 4. Generar y corregir

Pulsa **Generar ejemplo editable con dedos** y lee la confirmación. Reemplaza:

- Toda la posición/giro y dedos de **la mano elegida**.
- Su curva de influencia IK, que pasa a 100% para que la muñeca siga realmente el recorrido (no solo el marcador).
- Posición/giro de **la pieza elegida**.
- Tipo y duración de la toma (cambiar duración afecta a todas las pistas).
- El objetivo de recarga heredado de la izquierda cuando esa es la mano generada.
- Los fundidos Blend In/Out demasiado largos, que se limitan al 15% de la duración cada uno para no atenuar el contacto mientras se manipula la pieza.

Conserva las pistas de la otra mano, las otras piezas, las curvas del soporte del arma y los clips fuente. La duración y los fundidos son globales, por lo que también cambian su timing/mezcla. Trabaja sobre una copia y no sustituye nada si falla la generación. Guarda una versión antes de reemplazar una toma que te guste.

Si cambiar de tipo activaría o desactivaría el objetivo heredado Reload de la izquierda al generar con la derecha, el asistente bloquea la operación en vez de alterar silenciosamente esa otra mano. Ocurre tanto de Reload a Cycle/Fire como a la inversa. Parte de una toma del mismo tipo o convierte antes el recorrido completo izquierdo a puntos de muñeca. La plantilla también necesita pesos completos en el binding/solver IK; no modifica automáticamente esos componentes.

La pieza tiene unos pocos puntos de fases. La muñeca tiene puntos a **60 Hz** para acompañar el contacto durante traslaciones y giros, sin añadir un constraint al runtime. Los dedos tienen puntos de abrir, agarrar, soltar y volver a la pose inicial.

Después de generar:

1. Reproduce a velocidad reducida y normal.
2. Revisa dedos, alcance, penetraciones y primera persona con mirada/Aim.
3. Puedes corregir puntos y dedos con las herramientas normales.
4. Si cambias pieza, referencias, arco o plantilla, pulsa **Generar** otra vez para recalcular la muñeca. Cambiar solo la curva de la pieza **no recalcula automáticamente** la mano.
5. Guarda la toma como nueva copia. El bake de piezas conserva exactamente las curvas cúbicas del preview en el clip Generic exportado.

## 5. Qué representa cada ejemplo

| Plantilla | Fases y límites |
|---|---|
| Cargador | Acercar mano, agarrar, extraer con arco, pausa, insertar y soltar. Usa el mismo cargador, no crea un cargador nuevo ni lo deja caer. |
| Revólver | Abre la pieza seleccionada, repite gestos de mano/cierre de dedos junto al tambor abierto y cierra. El tambor debe existir separado; no detecta bisagras, cámaras, eyección ni munición. El punto de suministro es una aproximación derivada de la apertura, ajustable luego por puntos. |
| Cartucho a cartucho | Repite el viaje del cartucho/diseño de referencia desde fuera hasta la inserción. Reutiliza una sola pieza que vuelve al origen: no crea, oculta ni deja cartuchos dentro del arma. Es una base de timing/gesto, no una recarga mecánica terminada. |
| Cerrojo | Contacto, giro de desbloqueo, tirar atrás, pausa, empujar y bloquear. Si la referencia exterior no tiene giro diferente, no inventa un eje de palanca. El arco no se aplica a las guías rectas del cerrojo. |
| Disparo gestual + cerrojo | Flexión inicial del índice de la mano elegida, luego manipulación del cerrojo. No reconoce el gatillo, no genera recoil del soporte, sonido, proyectiles ni disparos de gameplay. |

AK12 es el fixture disponible; **no se añaden modelos de revólver, escopeta o sniper**. Debes seleccionar piezas/referencias compatibles. Ninguna plantilla reconoce automáticamente la mecánica de cualquier arma.

El arco solo modifica tramos configurados como Arco. No garantiza colisiones ni movimientos orgánicos por sí solo. Los fundidos y los movimientos fuente del cuerpo/padres pueden alterar contactos: revisa siempre el resultado final.

## 6. Verificación técnica

Menú **Tools → CodeHelper → Animation Lab → Verificar recorridos y plantillas**.

Para ejecutar también todas las suites anteriores por batch: `CodeHelper.AnimationLab.Editor.AnimationLabProceduralChecks.RunAll`.

Estas pruebas comprueban datos, interpolación, contactos muestreados, persistencia y equivalencia del bake; no certifican calidad artística ni gameplay. El juego no se modifica ni se sincroniza automáticamente.
