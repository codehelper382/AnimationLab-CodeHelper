# Recargas fáciles: insertar manos y seguir un guion

> **Nota de disponibilidad:** la coreografía AK12 de cinco etapas descrita en la entrega histórica ya no está en el código activo; su validación quedó registrada en `../VALIDACION.md`. `../Asistente recarga AK12/LEEME.md` es una guía histórica. La reserialización actual elimina su campo legacy, así que no guardes el asset original ni las recuperaciones. El flujo SOLO MANOS y las demás herramientas activas se describen a continuación.

**Interfaz actual:** [Animation Lab Studio](GUIA_STUDIO_VISUAL.md) añade puntos visuales, fases, Probar/Antes/Después/Aceptar/Cancelar y variantes. El botón de inserción directa de las primeras versiones se sustituye por una prueba temporal; los guiones inferiores siguen siendo bases de autoría.

## La idea

**Elige una recarga existente → indica los fotogramas → prueba, compara y acepta la mano.**

No hay que elegir piezas ni hacer tres capturas. El resultado es una base de **muñeca + dedos de una mano**, editable en la toma actual. La otra mano conserva su pista. No se anima automáticamente el arma, cargador, tambor, corredera, cuerda, palas ni flecha.

Los guiones de abajo son **propuestas de autoría**, no animaciones terminadas ni calibraciones universales. El fixture del laboratorio sigue siendo AK12, puños y cuchillo: no se añaden los otros modelos por elegir una plantilla.

## 1. Probarlo sin complicaciones

1. Fuera de Play, abre **Tools → CodeHelper → Animation Lab → Abrir laboratorio** y prepara personaje/equipos.
2. Para empezar, crea una toma nueva con AK12: propone su recarga normal y duración original. Puedes cambiarla en **Animar → Duración**, en segundos o fotogramas.
3. En la pestaña **Recargas**, abre **Recargas rápidas**.
4. En **Base del movimiento**, elige **Recarga existente · normal** o **vacía**. Para los guiones genéricos inferiores, cambia explícitamente a **Gesto procedural · orientativo**.
5. Indica **Insertar desde F** y **Hasta F**. El guion visible se ajusta a ese tramo; no cambia la duración de la toma.
6. Pulsa **Probar sin insertar**, en la barra fija sobre el visor. **Crear puntos visuales** es opcional con una recarga existente: permite añadir correcciones sin perder el recorrido y dedos originales.
7. Si el contacto no está donde necesitas, deshaz o vuelve a insertar usando un contacto marcado:
   - Puedes mover directamente el punto de contacto o ajustar su orientación en Recargas.
   - Si todavía no creaste puntos, **Colocar mano** abre Animar. Ajusta la muñeca y pulsa **Usar esta pose para la recarga · sin crear clave** antes de cambiar de pestaña.
   - Pulsa **Actualizar prueba** y compara Antes/Después.
8. Revisa dedos y primera persona; pulsa **Aceptar manos** y guarda una copia de la toma.

Para editar la receta completa en vez de insertar una mano, usa **Abrir recarga completa como copia**. Conserva los clips y movimientos originales en otro borrador; no modifica el asset fuente. **Usar duración original** solo ajusta el tiempo total. La inserción de una mano no copia automáticamente el movimiento del arma de esa referencia.

**Recorrido del personaje (cm):** X negativo = izquierda; X positivo = derecha; Y negativo = abajo; Z negativo = hacia atrás. Son ejes del personaje, no los ejes importados del cargador ni los del visor. El preset de arco recomienda derecha y recorrido hacia atrás; puedes elegir otra mano explícitamente, pero no se hace espejo de agarres automáticamente.

**Arco libre** curva el viaje entre contacto y lateral. No añade balanceos cuando la mano está agarrando. No se aplica a la guía de cerrojo ni a la tensión del arco.

## 2. Qué inserta y qué conserva

- Cambia solo posición/giro, dedos e influencia IK de la **mano elegida**. Dentro del gesto toma control del IK.
- Conserva tipo, duración, fundidos, clips fuente, arma completa, pistas de piezas, diseños y la otra mano.
- Para insertar sobre una receta anterior, convierte el canal de esa mano a puntos a 60 Hz; fuera del tramo conserva su resultado aproximadamente a esa resolución. **No conserva la estructura original Motion Clip/Reach de esa mano**. Si era la izquierda, su objetivo heredado de recarga se convierte también al nuevo recorrido.
- Los dedos se muestrean sin fundidos para no aplicar la mezcla dos veces al exportar; la conservación externa sigue siendo aproximada entre muestras y según el avatar.
- Es sustituir el tramo elegido, **no sumar encima dos movimientos completos de la misma muñeca**. Usa Undo/Redo para comparar, o guarda una copia antes.
- El contacto se guarda local a la guardia. Si quieres seguir una pieza que se mueve independientemente, corrige puntos o utiliza conscientemente **Avanzado · generar mano + pieza (3 referencias)**; ese otro flujo sí sustituye la pieza y tiene sus propias advertencias.
- Se admiten tomas de hasta 30 s y tramos de al menos 6 fotogramas en este modo (usa muchos más para un gesto legible). Si una guardia, un fundido o el modo Follow Animated Right Hand impiden conservar la mano sin alterar el arma, se explica el motivo en vez de modificar otros canales a escondidas.

## 3. Lista de guiones por arma

Referencia de **60 fps**: F 60 = 1 s; F 120 = 2 s. Los fotogramas siguientes son una base; al elegir otro intervalo, la ventana escala las fases a tu tramo.

**Leyenda:** “arma/pieza opcional” significa que debes editar su pista aparte. El botón de insertar no hace ese movimiento.

### A. Pistola — F 0–80

| Fotogramas | Qué construir |
|---|---|
| 0–20 | Anticipación corta. **Opcional:** girar ligeramente el arma en F 20 para mostrar el alojamiento; evitar tapar la vista con el cañón. |
| 20–25 | La mano de apoyo llega al cargador; la palma se orienta antes de cerrar dedos. |
| 25–40 | La mano viaja hacia la izquierda del personaje; arco pequeño y codo relajado. |
| 40–60 | Regresa al alojamiento, alineando antes de llegar y frenando al contacto. |
| 60–70 | Asentar y soltar. La pausa debe ser legible, no un martillazo exagerado. |
| 70–80 | Retirar dedos y volver al apoyo con una curva limpia. |

Para una corredera abierta, añade **otra fase** de corredera/retén y su gesto de mano; el ejemplo no detecta si la pistola está vacía.

### B. Metralleta / subfusil — F 0–90

| Fotogramas | Qué construir |
|---|---|
| 0–20 | Presentación compacta. **Opcional:** inclinar el arma sin perder su silueta. |
| 20–25 | Mano al cargador y cierre de dedos. |
| 25–42 | Recorrido lateral corto; una metralleta compacta no necesita un barrido enorme. |
| 42–65 | Volver al alojamiento con menos velocidad cerca de la boca. |
| 65–78 | Asentar, pausa breve y abrir dedos. |
| 78–90 | Volver al agarre delantero. El arma recupera su orientación en su pista aparte. |

### C. Fusil de asalto — F 0–100

| Fotogramas | Qué construir |
|---|---|
| 0–20 | Anticipación y presentación del alojamiento; giro del arma opcional. |
| 20–28 | Mano al cargador, sin atravesar el guardamonte. |
| 28–50 | Mano al lateral/suministro; arco moderado para librar el arma. |
| 50–75 | Presentar y regresar al punto de inserción. |
| 75–90 | Asentar y soltar; muñeca estable durante el contacto. |
| 90–100 | Recuperar apoyo con menos energía que la extracción. |

En AK12, el cargador real es `AK12Rig/Root/Mag`, pero esta plantilla **no lo mueve**. Anímalo por separado si quieres que acompañe la mano.

### D. Francotirador: ciclo de cerrojo — F 0–100

| Fotogramas | Qué construir |
|---|---|
| 0–18 | Mano dominante al pomo. |
| 18–26 | Pinzar y gesto de levantar/desbloquear. |
| 26–40 | Tirar hacia atrás con guía recta. |
| 40–55 | Empujar hacia delante, sin un arco lateral decorativo. |
| 55–64 | Gesto de bajar/bloquear y pausa corta. |
| 64–100 | Retirar dedos y recuperar empuñadura. |

La pequeña rotación preparada es una aproximación local de muñeca, no un eje mecánico universal. Corrígela para tu modelo. El cerrojo y el arma permanecen sin cambios hasta que edites sus pistas.

### E. Francotirador: carga bala a bala — F 0–120

| Fotogramas | Qué construir |
|---|---|
| 0–25 | Preparar la mano. Abrir el mecanismo, si corresponde, en una pista separada. |
| 25–35 | Mano en pinza cerca del punto de carga. |
| 35–90 | Repetir mano al suministro → al puerto. Ajusta **Gestos / cartuchos**. |
| 90–105 | Soltar y despejar la abertura. |
| 105–120 | Recuperar apoyo; cierre/ciclo de cerrojo por separado. |

Para un rifle de cargador extraíble usa el guion de cargador, no este de munición individual. No se crean cartuchos ni se confirma su inserción en gameplay.

### F. Revólver — F 0–120

| Fotogramas | Qué construir |
|---|---|
| 0–20 | Presentar. Abrir tambor por separado si el modelo lo permite. |
| 20–30 | Mano en pinza junto al tambor. |
| 30–90 | Repetir gestos de suministro y carga; puedes corregir el contacto para distintas cámaras. |
| 90–105 | Abrir dedos y retirarlos del recorrido del tambor. |
| 105–120 | Recuperar apoyo. El cierre del tambor se anima aparte. |

El ejemplo no expulsa vainas ni rota cámaras; tampoco simula un speedloader. No sacudas el arma para “vender” cada cartucho: la claridad del contacto importa más.

### G. Escopeta: carga de cartuchos — F 0–120

| Fotogramas | Qué construir |
|---|---|
| 0–25 | Preparar acceso al puerto de carga; giro del arma opcional. |
| 25–35 | Mano junto al puerto con pinza inicial. |
| 35–90 | Repetir suministro → puerto según el número de gestos. |
| 90–105 | Soltar y salir sin atravesar guardamonte/cajón. |
| 105–120 | Volver al apoyo. Corredera y ciclo se animan aparte. |

La pinza preparada no reproduce por sí sola la presión exacta del pulgar sobre la compuerta; edita esos dedos en los puntos necesarios.

### H. Arco: colocar, tensar y soltar — F 0–120

| Fotogramas | Qué construir |
|---|---|
| 0–20 | Mano de tiro a la referencia de cuerda/culatín. La mano del arco conserva su pista. |
| 20–35 | Gesto de enganchar dedos; flecha y nock se colocan por separado. |
| 35–50 | Tirar hacia el cuerpo con aceleración progresiva, dentro del alcance del brazo. |
| 50–85 | Anclaje legible. Mantener la mano estable; no añadir ruido para fingir tensión. |
| 85–100 | Abrir dedos y seguimiento corto de la mano; evitar una sacudida grande. |
| 100–120 | Recuperar suavemente. |

La plantilla recomienda mano derecha y desplazamiento hacia atrás; adáptalo para arqueros zurdos. **No deforma cuerda/palas ni lanza flechas**. Su preset de pinza es una base, no un agarre de cuerda definitivo de dos o tres dedos: corrige índice, medio, anular y pulgar según la técnica de referencia.

## 4. Cómo hacer que se vea estético

1. **Silueta antes que detalle.** En primera persona deben leerse arma, cargador/puerto y mano sin cubrir toda la pantalla.
2. **Palma primero, dedos después.** Alinea la muñeca antes de cerrar. No uses dedos para ocultar un contacto mal colocado.
3. **Arcos al viajar, precisión al contactar.** Curva el tramo libre, no el encaje ni las guías del cerrojo.
4. **Ritmo desigual.** Preparar algo más lento, recorrido decidido y frenada corta al llegar. No repartir todos los puntos con la misma velocidad.
5. **Pausas de 2–5 fotogramas** para asentar o bloquear si la duración lo permite. Una pausa técnica es más clara que muchos rebotes.
6. **Asimetría intencional.** Una mano trabaja y la otra sostiene. No copies el mismo movimiento a ambas por defecto.
7. **Nada de ruido durante un contacto.** La respiración/cuerpo pueden dar vida alrededor; no hagas resbalar la mano sobre cargador, cerrojo o cuerda.
8. **Recuperación amortiguada.** El regreso debe perder energía; el último punto no debería sentirse como un frenazo.
9. **Revisa alcance y tres vistas.** Frontal, lateral y primera persona con mirada/Aim; reduce recorrido si el codo se estira al límite.
10. **Usa referencia real.** Graba tu gesto o estudia vídeo de la familia concreta de arma. El guion no sustituye mecánica ni calibración.

Estas pautas están inspiradas en arcos, anticipación, variación de ritmo y acciones secundarias de la skill `naturalistic-motion`, respetando los contactos estables del solver propio.

## 5. Guardar y comprobar

Guarda como nueva copia para conservar tanto las claves como los parámetros/contacto del ejemplo. La exportación sigue siendo explícita; el juego no se modifica automáticamente.

Chequeo técnico: **Tools → CodeHelper → Animation Lab → Verificar inserción solo manos**. `AnimationLabHandTemplateChecks.RunAll` incluye también las cuatro suites anteriores. No certifica calidad artística ni sustituye la revisión visual.
