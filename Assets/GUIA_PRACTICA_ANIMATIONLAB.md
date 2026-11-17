# Animation Lab: guía práctica para crear tus propias animaciones

**Para este proyecto:** `D:\Unity\CODEHELPER AnimationLab` · **Unity 6000.3.14f1**.

Esta guía explica qué pulsar y, además, cómo trabajar el movimiento para que tenga intención, peso y buenos contactos. Los controles se han contrastado con el código actual; los valores de los ejercicios son propuestas de práctica, **no animaciones ya creadas ni calibraciones validadas visualmente**.

El documento de contexto sigue siendo [CONTEXTO_Y_USO_ANIMATIONLAB.md](CONTEXTO_Y_USO_ANIMATIONLAB.md). Aquí profundizamos en la autoría, con ejercicios y una rutina de revisión.

**Interfaz actual: edición por fotogramas y pistas independientes.** Si es tu primera vez, empieza por [la guía rápida con pasos y referencias](GUIA_INICIO_RAPIDO_ANIMATIONLAB.md). En la ventana eliges F 0, F 18, etc.; los porcentajes de esta guía solo explican cómo se guardan los datos internamente.

La vista **Sencillo** incluye la guía Elegir/Poses/Ritmo/Probar/Guardar. Para los ejercicios que mencionan curvas avanzadas, calibración, movimiento natural del arma o el asistente de tres referencias, cambia a **Completo · todas las herramientas**; no altera tu toma.

Para cargador/cerrojo, navegación de cámara, coordenadas globales, movimiento coordinado y diseños/agarres, consulta [la guía de piezas, objetos y agarres](GUIA_PIEZAS_OBJETOS_Y_AGARRES.md).

Para curvar los tramos y generar ejemplos con tres referencias y dedos, consulta [Recorridos y recargas prefabricadas](GUIA_RECORRIDOS_Y_RECARGAS.md).

Para empezar de forma más simple, usa [Solo manos y guiones por fotogramas](GUIA_RECARGAS_POR_FOTOGRAMAS.md): insertar un gesto en un tramo sin cambiar arma, piezas ni duración.

## Índice

1. [Qué puedes crear y qué resultado obtienes](#1-qué-puedes-crear-y-qué-resultado-obtienes)
2. [Skills instaladas y cómo pedir ayuda a la IA](#2-skills-instaladas-y-cómo-pedir-ayuda-a-la-ia)
3. [Abrir y preparar el laboratorio](#3-abrir-y-preparar-el-laboratorio)
4. [Entender la ventana y los datos](#4-entender-la-ventana-y-los-datos)
5. [Planificar una animación antes de tocar controles](#5-planificar-una-animación-antes-de-tocar-controles)
6. [Primera animación: golpe derecho sencillo](#6-primera-animación-golpe-derecho-sencillo)
7. [Mover y orientar muñecas con precisión](#7-mover-y-orientar-muñecas-con-precisión)
8. [Crear y animar poses de dedos](#8-crear-y-animar-poses-de-dedos)
9. [Editar, reemplazar y quitar puntos](#9-editar-reemplazar-y-quitar-puntos)
10. [Dar calidad al movimiento: poses, ritmo, arcos y peso](#10-dar-calidad-al-movimiento-poses-ritmo-arcos-y-peso)
11. [Ejercicio de pulido: golpe con anticipación y recuperación](#11-ejercicio-de-pulido-golpe-con-anticipación-y-recuperación)
12. [Trabajar con las dos manos](#12-trabajar-con-las-dos-manos)
13. [Cuchillo: modificar un ataque sin desarmar el sistema](#13-cuchillo-modificar-un-ataque-sin-desarmar-el-sistema)
14. [AK12: ajustar una recarga y sus contactos](#14-ak12-ajustar-una-recarga-y-sus-contactos)
15. [Calibrar agarres y guardias](#15-calibrar-agarres-y-guardias)
16. [Panel avanzado explicado](#16-panel-avanzado-explicado)
17. [Guardar, reabrir, recuperar y versionar](#17-guardar-reabrir-recuperar-y-versionar)
18. [Exportar e integrar en el juego](#18-exportar-e-integrar-en-el-juego)
19. [Revisión final y verificación técnica](#19-revisión-final-y-verificación-técnica)
20. [Problemas habituales y solución](#20-problemas-habituales-y-solución)
21. [Plan de práctica y ficha reutilizable](#21-plan-de-práctica-y-ficha-reutilizable)

## 1. Qué puedes crear y qué resultado obtienes

El laboratorio sirve para trabajar con **AK12, puños y cuchillo**, usando el personaje y los sistemas propios de mirada, cámara, columna e IK.

Puedes:

- Dibujar recorridos de muñeca y offsets de arma mediante puntos de posición y giro.
- Grabar poses de dedos en distintos momentos y mezclarlas durante la acción.
- Ajustar grips, guardias y objetivos disponibles en la copia de preview.
- Partir de una acción existente y crear una variante sin cambiar el original.
- Combinar estos datos con clips y curvas existentes en el panel avanzado.
- Previsualizar desde fuera y desde primera persona, con mirada y Aim.

**No es un editor general de todas las articulaciones del cuerpo.** Los controles principales son muñecas, dedos y calibraciones. Para una actuación corporal completa necesitarás un clip de personaje apropiado y, posiblemente, autoría externa. Las skills no añaden por sí solas controles nuevos a la ventana.

El resultado principal tampoco es necesariamente un único archivo `.anim`. Guardas una **toma `.asset`** que incluye:

| Parte | Para qué sirve |
|---|---|
| Toma `AnimationLabTake` | Volver a editar puntos, dedos, calibraciones y fuentes. |
| Subasset **Action** | Receta `EquipmentActionDefinition` que consume el juego. |
| Poses de dedos | Datos de cada punto de cada mano. |
| Clip de dedos horneado, cuando corresponde | Canales Humanoid reproducibles por el runtime, conservando otros canales del clip fuente. |
| Calibraciones | Posiciones/rotaciones de referencias; se aplican aparte al integrar. |

El laboratorio **no ejecuta daño, proyectiles, transferencia de munición ni eventos de combate al mover el tiempo**. Posar un lanzamiento no crea un sistema de lanzar objetos.

## 2. Skills instaladas y cómo pedir ayuda a la IA

Están instaladas **solo para este proyecto**, fuera de `Assets`:

| Skill | Ubicación relativa a la raíz Unity | Uso recomendado |
|---|---|---|
| `naturalistic-motion` | `.agents/skills/naturalistic-motion/` | Revisar arcos, anticipación, ritmo, solapamiento y movimiento demasiado robótico. |
| `unity-animation` | `.agents/skills/unity-animation/` | Consultar Humanoid, clips, máscaras y problemas técnicos de animación de Unity. |

La procedencia queda registrada en `../skills-lock.json`. No se han instalado Blender, paquetes adicionales de Unity ni un nuevo sistema de IK.

### Cómo utilizarlas en OpenCode

Puedes mencionar la skill expresamente en tu mensaje:

```text
Usa @naturalistic-motion y lee @GUIA_PRACTICA_ANIMATIONLAB.md.
Quiero crear un golpe derecho rápido con puños de 0.6 segundos.
Propón poses clave y tiempos normalizados, con anticipación breve,
extensión rápida y recuperación controlada. No cambies scripts ni assets todavía.
```

Para diagnóstico técnico:

```text
Usa @unity-animation, pero respeta los sistemas propios de Animation Lab.
Mi toma se ve bien en preview y los dedos cambian al exportar.
Revisa clip fuente, máscara, bake y mezclas antes de proponer cambios.
No sustituyas HandIKSolver ni añadas otro grafo al personaje.
```

Para una crítica útil, adjunta capturas o una grabación y explica:

1. Equipo, mano, duración y tipo de acción.
2. Qué querías transmitir: rapidez, peso, precisión, agresividad, cuidado, etc.
3. Qué te parece mal y en qué tiempo ocurre.
4. Vista externa o primera persona; mirada/Aim usados.
5. Si partiste de una receta existente o grabaste una trayectoria nueva.

**No basta con que la IA diga que una animación “debería verse bien”.** Debe distinguir lo que ha observado de lo que solo propone.

### Cómo interpretar sus consejos

- `naturalistic-motion` es una referencia artística, no una ley física. Un agarre puede y debe permanecer estable durante un contacto; no añadas ruido a los dedos para que “parezcan vivos”.
- `unity-animation` describe principalmente Mecanim y su IK integrado. Aquí ya existe `HandIKSolver`: que una skill hable de `OnAnimatorIK` o de otro controlador no significa que haya que cambiar el laboratorio.
- Las skills no garantizan ejecución automática dentro del editor. Sin una conexión de automatización disponible, pueden analizar archivos y guiarte, pero no asumir que han manipulado la ventana.

## 3. Abrir y preparar el laboratorio

1. En Unity Hub, añade el proyecto **`D:\Unity\CODEHELPER AnimationLab`**, no su subcarpeta `Assets` ni el proyecto del juego.
2. Ábrelo con **Unity 6000.3.14f1**.
3. Espera a que termine la importación y compilación. Si hay errores rojos en Console, resuélvelos antes de empezar.
4. Abre **Tools → CodeHelper → Animation Lab → Abrir laboratorio**.
5. En **Modo de columna**, empieza con `LateUpdate`. Más adelante compararás con `AnimationJob`.
6. Pulsa **1 · Preparar personaje y equipos**.
7. Amplía la ventana para tener visibles controles y personaje; la columna izquierda tiene desplazamiento vertical.

También puedes ejecutar **`Abrir laboratorio.cmd`**, situado en la raíz Unity. Usa la instalación local indicada en ese archivo.

> **No pulses Play de Unity.** El laboratorio funciona en modo edición y abre internamente una copia aislada de `Assets/Scenes/AnimationLab.unity`. No necesitas convertir la escena abierta en tu zona de trabajo.

Para comparar otro modo de columna, guarda primero, cierra la ventana, vuelve a abrirla, elige el modo antes de preparar y reabre la toma guardada.

## 4. Entender la ventana y los datos

### Panel 1: Equipo y acción

- **Equipo:** el recurso sobre el que trabajas.
- **Copiar acción existente:** inicia un borrador a partir de una receta del equipo.
- **Nombre de la toma:** identifica la copia que guardarás.
- **Tipo:** categoría de acción del runtime.
- **Duración (segundos):** tiempo real de la acción, no el porcentaje del slider.

Los tipos actuales son `None`, `Equip`, `Unequip`, `Fire`, `Cycle`, `Reload` y `MeleeAttack`. Para los ejercicios de golpes usa `MeleeAttack`; para recargas, `Reload`. No existe un tipo `Throw` implementado por esta ventana.

Al copiar una acción se selecciona su `Tipo` según la referencia del equipo (Equip, Unequip, Fire, Cycle, Reload o MeleeAttack). Revísalo si después cambias la intención de la receta. El borrador inicial de arma de fuego parte de `Reload`; el de melee, de `MeleeAttack`.

### Panel 2: Manos y dedos

Aquí eliges **Izquierda**, **Derecha**, **Arma** o **Piezas / diseños**. Para cada mano puedes elegir **Posición y giro** o **Solo dedos**. Los puntos de posición y dedos se guardan por separado; calibración de reposo y controles avanzados quedan plegados.

### Vista y panel 3: Previsualizar

| Control | Qué hace |
|---|---|
| **Personaje · arrastrar para orbitar** | Vista externa. Arrastra el fondo para girar alrededor del personaje. |
| **Primera persona** | Vista desde la cámara del personaje. |
| Rueda en vista externa | Acerca/aleja la cámara externa. |
| Punto amarillo | Arrastra la muñeca seleccionada en el plano de la vista. |
| **Mirar arriba / abajo** | Cambia la mirada vertical de prueba. |
| **Mirar a los lados** | Cambia la mirada horizontal de prueba. |
| **Probar Aim** | Prueba apuntado cuando el equipo lo admite. |
| **Fotograma** y deslizador superior | Elige el fotograma de referencia de 60 fps. También puedes hacer clic en una pista. |
| Seis pistas y rombos **◆** | Posición y dedos de cada mano, arma completa y pieza/diseño seleccionado. El rombo abre un punto exacto; la línea blanca marca el tiempo actual. |
| **\|◀** | Vuelve al inicio. |
| **−1 fotograma / +1 fotograma** | Pasos de referencia de `1/60` de segundo. |
| **▶ Reproducir / Pausar** | Reproduce la toma sin entrar en Play. |
| **Repetir** | Reinicia al terminar. No convierte automáticamente la toma en un loop bien construido. |
| **Velocidad** | Solo cambia la velocidad de preview, de `0.1` a `2`; no la duración guardada. |

### Panel 4: Guardar / llevar al juego

- **Abrir toma:** carga un asset guardado en un borrador editable.
- **Guardar toma como copia** (barra superior) / **Guardar como nueva copia:** confirma ajustes pendientes y guarda otra toma, no sobrescribe la anterior.
- **Exportar copia guardada al juego (.unitypackage)…:** exporta la copia persistente. Hay que guardar después de modificarla.

### Tiempo normalizado: referencia opcional

```text
segundos = tiempo_normalizado × duración
tiempo_normalizado = segundos / duración
fotograma_de_referencia ≈ segundos × 60
```

No necesitas estas cuentas para editar: la ventana muestra fotogramas y segundos. Si una toma dura `0.6 s`:

| Tiempo | Segundos | Fotograma de referencia |
|---|---:|---:|
| `0` | `0` | `0` |
| `0.25` | `0.15` | `9` |
| `0.5` | `0.30` | `18` |
| `0.75` | `0.45` | `27` |
| `1` | `0.60` | `36` |

Cambiar Duración mantiene las posiciones normalizadas de los puntos: cambia cuánto tarda la acción completa. Para alargar solo la preparación o acortar solo el ataque, debes redistribuir sus puntos, no solo cambiar la duración.

## 5. Planificar una animación antes de tocar controles

Empieza por una frase concreta: **“Un golpe derecho corto, preciso, con regreso rápido a guardia”** es más útil que “quiero una animación buena”.

### Reúne una referencia

- Usa una grabación propia o material que tengas permiso para utilizar.
- Para golpes, puedes ensayar lentamente sin golpear a nadie ni usar objetos peligrosos.
- Estudia qué inicia el movimiento, cuándo acelera, dónde está el máximo alcance y cómo vuelve.
- Observa tanto el recorrido de la muñeca como el cambio de orientación de la mano.

No copies ciegamente el tiempo de un vídeo a cámara lenta: úsalo para estudiar las fases y revisa luego a velocidad normal.

### Divide la acción en fases

| Fase | Pregunta que resuelve |
|---|---|
| Guardia | ¿De dónde parte la mano? |
| Preparación | ¿Qué indica la intención de actuar? |
| Desplazamiento principal | ¿Dónde está la aceleración? |
| Contacto o extremo | ¿Qué pose vende el objetivo de la acción? |
| Continuación | ¿Cómo se disipa el movimiento? |
| Recuperación | ¿Cómo vuelve sin parecer que el vídeo se reproduce al revés? |

El “contacto” de esta planificación es **visual**. No significa que el laboratorio haya detectado una colisión.

### Elige la ruta de trabajo

- **Acción nueva de puños:** comienza con un borrador nuevo y pocos puntos.
- **Variante pequeña de cuchillo o recarga:** copia la acción existente; observa sus canales antes de grabar puntos.
- **Cambio del agarre de reposo:** calibra la guardia/grip; no intentes corregirlo añadiendo el mismo offset a decenas de puntos.
- **Cambio de actuación corporal completa:** considera un clip de personaje; esta ventana no sustituye un DCC como Blender.

## 6. Primera animación: golpe derecho sencillo

Objetivo: aprender el ciclo **fotograma → pose → guardar punto → reproducir → guardar toma** con tres puntos de posición. No buscamos todavía un golpe final.

### A. Configura el borrador

1. Selecciona **Puños**.
2. Nombre: `Fists_Derecha_Prueba_v01`.
3. Tipo: **MeleeAttack**.
4. Duración: **`0.6` segundos**.
5. Selecciona **Mano derecha → Posición y giro**, o la pista **Derecha · posición**.
6. Pon mirada vertical y horizontal a cero; empieza sin Aim.

### B. Graba el inicio

1. Lleva **Fotograma** a `0`.
2. Pulsa **Volver al reposo**, o escribe Desplazamiento/Giro a cero.
3. Pulsa **Guardar posición en F 0**.
4. Comprueba el rombo de la pista derecha de posición.
5. Elige **Solo dedos**, pulsa **Mano cerrada**, ajusta el pulgar y pulsa **Guardar dedos en F 0**.
6. Vuelve a **Posición y giro**. Las dos pistas son independientes.

### C. Graba la extensión

1. Lleva **Fotograma** a `18` (mitad de esta toma de `0.6 s`).
2. Introduce un pequeño desplazamiento. Como prueba inicial, usa **Z = 12 cm** **solo si Z local avanza en la dirección adecuada**.
3. Si Z mueve la muñeca hacia otro lado, vuelve a cero y prueba los ejes como se explica en la sección 7.
4. Mantén el giro en cero al principio.
5. La pose se previsualiza al editar. Revisa desde delante y de lado.
6. Si el brazo queda bloqueado o demasiado extendido, reduce el alcance; no fuerces el rig para conseguir más distancia.
7. Pulsa **Guardar posición en F 18**.

Los dedos de su propia pista se mantienen/interpolan sin que tengas que volver a capturarlos al guardar posiciones. Revisa las mezclas de entrada/salida de la acción.

### D. Graba la vuelta

1. Lleva **Fotograma** a `36` (final).
2. Pulsa **Volver al reposo**: pone Desplazamiento y Giro a cero.
3. Pulsa **Guardar posición en F 36**.
4. Comprueba los tres rombos de posición derecha. Si quieres fijar también el puño final, usa Solo dedos y guarda su forma en F 36.

### E. Reproduce y guarda

1. Pulsa **▶ Reproducir**.
2. Mira primero a **Velocidad `1`**. Después usa `0.5` o `0.25` para inspeccionar.
3. Prueba **Primera persona**.
4. Pulsa **Guardar como nueva copia**.
5. Busca la toma en **`Assets/AnimationLab/Authored`**.

Con esto has creado tu primera toma editable. Si el golpe parece un pistón, es normal: todavía faltan preparación, ritmo y una recuperación diferenciada.

## 7. Mover y orientar muñecas con precisión

### Los ejes pertenecen a la guardia

**Desplazamiento (cm)** se muestra en centímetros y **Giro** en grados, ambos relativos a la guardia de la mano. El asset conserva metros. No presupongas que X es siempre izquierda/derecha en pantalla ni que Z es siempre hacia delante del personaje.

Para entenderlos:

1. Trabaja en el centro de una toma, donde la mezcla tenga influencia suficiente.
2. Parte de cero y añade `1` a X: es un centímetro local en la interfaz.
3. Observa la dirección y vuelve a cero.
4. Repite con Y y Z.
5. Prueba pequeños giros, por ejemplo `5°`, de uno en uno.

Si giras la guardia, cambias también sus direcciones locales. Es preferible decidir la calibración de reposo antes de afinar toda la trayectoria.

### Usar el punto amarillo

1. Selecciona la mano correcta.
2. Pausa y elige un fotograma.
3. Pulsa directamente sobre el punto amarillo y arrastra.
4. Suelta y revisa el resultado desde otro ángulo.
5. Corrige profundidad con los campos numéricos.
6. Pulsa **Guardar posición en F …** si quieres conservarlo.

El arrastre trabaja a una profundidad fija respecto a la cámara: **no es un manipulador XYZ ni hace snap a la superficie de un objeto**. Arrastrar el fondo en vista externa orbita la cámara, no la mano.

### Pose temporal frente a trayectoria

- Cambiar Desplazamiento/Giro o arrastrar crea una pose temporal.
- El preview muestra el ajuste inmediatamente, sin añadir una clave.
- **Guardar posición** lo incorpora al borrador; Guardar toma crea el asset.
- Al cambiar tiempo/pista o reproducir, un diálogo permite guardar el punto, seguir editando o descartar. No hace falta perder el ajuste para navegar.

### Por qué no se mueve al inicio o al final

Con **Blend In / Blend Out**, la influencia final puede ser nula en los extremos. Ahora puedes editar esos fotogramas: mientras ajustas se muestra la pose sin fundidos y un aviso lo indica. Al guardar/reproducir vuelven las mezclas finales.

Si el resultado final pierde alcance en los extremos, revisa la mezcla; no compenses con desplazamientos enormes.

### Giros grandes

Evita pedir una vuelta grande con un único salto Euler. Añade orientaciones intermedias, comprueba la ruta y revisa cada eje. Dos números distintos no garantizan que la interpolación tome la vuelta que imaginabas.

## 8. Crear y animar poses de dedos

### Qué controla cada herramienta

| Herramienta | Uso |
|---|---|
| **Usar pose de este fotograma como base** | Toma la forma fuente sin fundidos o conserva el ajuste de dedos actual como base; pausa. |
| **Mano abierta / Mano cerrada** | Crea una base usando músculos Humanoid del avatar. |
| **Cargar pose de dedos** | Usa un `HandPoseData` existente de la misma mano. |
| **Eje local de flexión X/Y/Z** | El eje de las rotaciones añadidas por los cinco sliders. |
| Sliders de dedos | Ajustan cada dedo respecto a la base capturada/cargada. |
| **Ajuste fino de articulaciones** | Ajusta las articulaciones disponibles individualmente. |
| **Guardar pose de dedos independiente…** | Guarda una pose reutilizable, no una animación temporal completa. |

### Orden seguro para editar un punto completo

1. Pausa y elige el tiempo o pulsa un punto existente.
2. Elige **Solo dedos**. La base se prepara automáticamente sin fundidos.
3. Si necesitas otra forma, aplica un preset, carga una pose o usa el botón para tomar una nueva base.
4. Ajusta los sliders.
5. Realiza el ajuste fino de articulaciones.
6. Revisa la forma temporal desde varias vistas.
7. Si solo cambiabas muñeca, usa Posición y giro en lugar de este flujo: los dedos no cambian.
8. Pulsa **Guardar dedos en F …**.
9. Reproduce para comprobar los datos grabados, no solo la forma temporal.

### Edición sin degradar la pose por los fundidos

La herramienta prepara la base sin Blend In/Out y restaura esos valores después. Ya no necesitas ponerlos manualmente a cero para editar dedos cerca del inicio/final. Durante un ajuste pendiente ves la forma fuente; Reproducir vuelve a mostrar la mezcla final.

Una pose importada/capturada con otra herramienta puede tener ya una forma atenuada: este sistema no puede adivinar una pose original que no contiene.

**Haz los sliders antes del ajuste fino.** Al mover un slider se recalculan las rotaciones a partir de la base; puedes perder ajustes finos que hayas hecho después de capturarla. Si quieres continuar desde esa forma, captura una base nueva con la pose visible antes de seguir.

Para cambiar entre herramientas con una pose pendiente, el diálogo permite confirmarla antes de continuar. La captura de dedos no sustituye una trayectoria de muñeca.

### Elegir un eje de flexión

1. Captura una base adecuada.
2. Elige un eje y mueve poco el slider del índice.
3. Si dobla hacia el lado equivocado, vuelve al valor anterior y prueba otro eje.
4. Si se retuerce en lugar de flexionar, usa otro eje o el ajuste individual.
5. Después de cambiar el eje, mueve un slider para que se recalcule la forma.

No todos los huesos de un rig comparten un eje de flexión útil. Los presets Humanoid suelen ser una base más segura que intentar resolver toda una mano con un eje adivinado.

### Hacer un agarre convincente

- La palma debe estar situada correctamente antes de forzar dedos.
- Ajusta el pulgar por separado: es decisivo para que el objeto parezca sujeto.
- Evita que todas las articulaciones tengan exactamente la misma curvatura.
- Revisa las puntas desde una vista lateral: una pose puede parecer correcta de frente y atravesar el objeto.
- Mantén los contactos que sostienen el objeto; reserva el movimiento para los dedos que realmente cambian de función.
- No cierres al máximo un dedo solo porque el slider lo permita.

### Animar abrir y cerrar

Ejemplo conceptual para una toma de `1 s`:

| Fotograma (toma de 1 s) | Tiempo normalizado | Forma deseada |
|---:|---|---|
| 0 | `0` | Mano relajada inicial. |
| 15 | `0.25` | Mano abierta antes de alcanzar. |
| 33 | `0.55` | Dedos cerrándose sobre el objeto. |
| 45 | `0.75` | Contacto mantenido. |
| 60 | `1` | Forma final o regreso, según la acción. |

En cada tiempo debes **Guardar dedos** después de editar la forma. Para retener una forma durante un intervalo, grábala en sus dos límites, además de revisar las mezclas y el clip base.

**Guardar dedos no reconstruye muñeca, Motion Clip, Reach ni sus bindings.** Esta separación permite cambiar dedos en una recarga sin sustituir su recorrido. Las tomas antiguas con puntos combinados siguen siendo compatibles.

### Bake y fidelidad

Al grabar, la forma se normaliza a lo que el avatar Humanoid puede reproducir. Una torsión local arbitraria puede corregirse. Al guardar normalmente se hornean los canales de dedos, con muestreo a 60 Hz y sin eventos de animación.

Esto no implica retargeting universal. Una pose izquierda no es automáticamente una derecha, ni una pose local es necesariamente válida en un avatar distinto.

## 9. Editar, reemplazar y quitar puntos

### Reemplazar un punto

1. Selecciona la mano.
2. Pulsa el botón del punto en la lista para volver a su tiempo y recuperar sus campos de muñeca.
3. Cambia la posición/giro o los dedos en la pista correspondiente.
4. Pulsa **Guardar posición** o **Guardar dedos** en ese mismo fotograma. No modifica el otro canal.

Es más seguro seleccionar el punto que volver a escribir un porcentaje aproximado: grabar en un tiempo ligeramente distinto puede crear otro punto muy cercano.

### Añadir un punto entre dos existentes

1. Elige el tiempo intermedio.
2. Los campos recuperan la posición/orientación del tiempo seleccionado. Si existe un punto, se recuperan sus valores; si no, se evalúa la receta.
3. Introduce valores intencionales o arrastra el punto amarillo para establecer una nueva posición.
4. Graba y reproduce ambos intervalos vecinos.

### Quitar un punto

Usa **Quitar** en su fila. Solo elimina el canal de la pista elegida: quitar muñeca conserva los dedos de un punto combinado antiguo y viceversa. Si eliminas el último punto de posición, su canal se reinicia; **no se recupera automáticamente la trayectoria original de una acción copiada**.

### Cambiar el tiempo de un punto

La lista no tiene un control dedicado para arrastrar/retimar claves. Guarda una copia de seguridad, anota sus valores y forma de dedos, recrea el punto en el tiempo deseado y quita el anterior. Puedes guardar la pose de dedos por separado para reutilizarla.

### Cambiar de mano

Izquierda, derecha y arma tienen pistas independientes. Cambiar de mano/fotograma sincroniza los campos y pide confirmación si había un ajuste pendiente. Los rombos son la forma más directa de recuperar puntos exactos.

### Deshacer

Los puntos y la calibración tienen operaciones Undo. No trates Undo como un historial completo de todo el panel, de los sliders temporales o de archivos exportados. Guarda versiones antes de cambios importantes.

## 10. Dar calidad al movimiento: poses, ritmo, arcos y peso

### Primero poses legibles; después más puntos

Detén la toma en preparación, extremo y recuperación. Si no puedes reconocer la intención en esas poses, más interpolación no resolverá el problema.

Para un golpe, revisa orientación del puño, alineación de muñeca, distancia al cuerpo y brazo de apoyo. Para una manipulación, revisa qué parte de la mano sostiene y cuál actúa.

### Timing y spacing no son lo mismo

- **Timing:** cuándo llegas a cada pose.
- **Spacing:** cuánto se desplaza la mano entre instantes sucesivos.

Si recorres distancias iguales en tiempos iguales, el movimiento tiende a parecer mecánico. Un golpe puede preparar poco, acelerar rápido y dedicar más tiempo a recuperarse. Una recarga necesita pausas de contacto claras, no velocidad uniforme en todo el recorrido.

Prueba primero cambiando una sola variable: duración total, tiempo de contacto o amplitud. Si cambias las tres a la vez no sabrás cuál mejoró el resultado.

### Construye arcos con sentido

Una muñeca depende de codo y hombro: suele describir curvas. Añade un punto intermedio cuando defina un arco, evite una penetración o aclare un cambio de dirección.

No añadas puntos en todos los fotogramas por defecto. Pocos puntos bien elegidos facilitan corregir la trayectoria. Revisa que las curvas entre ellos no introduzcan picos u oscilaciones.

### Anticipación sin exagerar todos los gestos

Una pequeña preparación puede hacer legible un ataque. No todos los movimientos necesitan un gran retroceso: una manipulación precisa puede anticiparse solo con orientación de muñeca y apertura de dedos.

### Recuperación distinta del ataque

Volver a la guardia por el mismo camino y a la misma velocidad puede parecer una grabación invertida. Cambia ligeramente el arco o el reparto temporal si la referencia lo justifica, manteniendo un final estable.

### Solapamiento entre partes

La muñeca puede iniciar el alcance y los dedos terminar de cerrar al llegar; al soltar, los dedos pueden abrir antes de que la mano se retire. Usa ese desfase para aclarar la acción, no para hacer que el objeto resbale.

### El peso se comunica, no se arregla con ruido

Un arma pesada no debería cambiar de dirección instantáneamente. Pero añadir temblor, rebotes o sacudidas a todo tampoco la hace más pesada. Busca una preparación coherente, una aceleración creíble y una recuperación controlada.

No escales huesos o el arma solo porque una referencia general mencione squash/stretch. Aquí interesa conservar volumen, agarres y compatibilidad del avatar.

### Primera persona manda sobre el encuadre

Una animación bonita desde fuera puede tapar la vista, cortar una mano o acercar demasiado el arma a la cámara. Alterna las dos vistas desde el primer borrador; no dejes el encuadre para el final.

## 11. Ejercicio de pulido: golpe con anticipación y recuperación

Parte de tu prueba anterior y guarda una nueva versión: **`Fists_Derecha_Recto_v02`**. Conserva la anterior como comparación.

Una distribución didáctica para `0.6 s` es:

| Fotograma de referencia | Segundos | Tiempo normalizado aproximado | Intención |
|---:|---:|---:|---|
| 0 | 0.000 | 0.000 | Guardia. |
| 6 | 0.100 | 0.167 | Preparación breve. |
| 12 | 0.200 | 0.333 | Inicio de extensión. |
| 16 | 0.267 | 0.444 | Máximo alcance o contacto visual. |
| 22 | 0.367 | 0.611 | Retirada inicial. |
| 30 | 0.500 | 0.833 | Cerca de guardia. |
| 36 | 0.600 | 1.000 | Guardia final. |

Los números están redondeados. Usa el contador y los botones de fotograma para colocar tiempos precisos; al reemplazar, pulsa el punto existente.

### Cómo construirlo

1. Conserva inicio/final a cero si quieres regresar a la guardia.
2. Reubica o elimina el antiguo punto central si ya no corresponde al nuevo contacto. No dejes dos máximos por accidente.
3. En preparación, mueve poco la muñeca hacia atrás/lateral según la referencia. Empieza con centímetros, no con desplazamientos enormes.
4. En el inicio de extensión, define el arco: no necesitas aún todo el alcance.
5. En contacto visual, utiliza una extensión alcanzable y una muñeca alineada; no bloquees el codo para ganar distancia.
6. En retirada, recoge la mano sin atravesar el cuerpo.
7. Cerca del final, vuelve progresivamente a cero y evita un rebote no intencionado.
8. Mantén el puño mientras golpea. Si cambias dedos, hazlo por una razón visible.
9. Reproduce a velocidad normal y luego lenta.
10. Guarda otra versión solo cuando puedas describir qué mejoró.

La tabla es una **hipótesis de ritmo**, no frame data obligatorio ni una prueba de contacto del juego. Ajusta los tiempos a la referencia y a la sensación buscada.

## 12. Trabajar con las dos manos

### Una mano actúa y otra sostiene

En un golpe derecho, puedes dejar la izquierda en su guardia al principio. En un arma, la derecha puede conservar su grip mientras la izquierda manipula.

No grabes puntos de la segunda mano “por si acaso”: cada punto puede activar o reconstruir su canal procedural. Añádela cuando realmente necesites cambiar su recorrido o dedos.

### Ambas manos cambian

1. Termina un primer recorrido de la mano principal.
2. Cambia de mano y revisa los campos antes de grabar.
3. Añade los puntos que necesita la mano de apoyo; no tienen que compartir todos los tiempos.
4. Compara los momentos de contacto y de liberación.
5. Inspecciona si se cruzan los brazos, se pisan las manos o aparece tensión extrema en codos.

No uses el espejo automático como garantía de una pose correcta. Revisa cada lado y su orientación local.

## 13. Cuchillo: modificar un ataque sin desarmar el sistema

### Ruta A: partir de un ataque existente

1. Guarda el borrador anterior y elige **Cuchillo**.
2. En **Copiar acción existente**, selecciona el ataque que quieres estudiar; el listado usa nombres de assets, no nombres universales de botones.
3. Verifica **Tipo `MeleeAttack`**.
4. Pon un nombre como `Knife_Corte_Diagonal_v01` y guarda una primera copia.
5. Reproduce **sin grabar puntos nuevos todavía**.
6. Observa qué mueve la muñeca, qué mueve el arma y qué controla la forma de los dedos.
7. Si solo falla la posición del agarre, empieza por calibración. Si falla el gesto completo, revisa los clips y canales del panel avanzado.

### Ruta B: sustituir intencionalmente una trayectoria de mano

1. Guarda otra versión antes de empezar.
2. Define guardia → preparación → recorrido de corte → continuación → recuperación.
3. Graba pocos puntos y comprueba la **trayectoria de la hoja**, no solo la muñeca.
4. Añade orientaciones intermedias si el giro no sigue el recorrido deseado.
5. Revisa hombro, codo, agarre y encuadre en primera persona.

**Límite importante:** desplazar una muñeca no significa que el objeto la siga automáticamente. Algunas acciones mueven el arma con `Weapon Clip`, curvas del arma o `Follow Animated Right Hand`. No actives este último sin un clip de personaje adecuado y sin entender qué pasa con el IK.

Si mano y cuchillo se separan, no ocultes el problema exagerando dedos: primero identifica qué canal gobierna el arma y cuál gobierna la mano. No emparentes el arma a la mano ya resuelta por IK.

El recorrido visual no genera daño ni barrido de colisión. Eso se valida al integrar en el juego.

## 14. AK12: ajustar una recarga y sus contactos

Una recarga es un buen ejercicio avanzado porque puede combinar varios sistemas a la vez.

### Identifica los canales antes de editar

| Elemento | Qué puede controlar |
|---|---|
| **Character Clip** | Animación corporal y dedos. |
| **Weapon Clip** | Piezas o esqueleto independiente del arma. |
| **Left Hand Target Motion** | Movimiento del objetivo izquierdo de recarga. |
| **Left/Right Hand Motion** | Trayectorias editables de mano, con curvas, Reach y/o Motion Clip. |
| Grips, guardias y targets del slot | Referencias espaciales. |
| Curvas Grip / IK | Influencia de las sujeciones y del solver. |

No todas las recetas usan todos estos elementos de la misma forma.

### Para un ajuste pequeño de contacto

1. Selecciona **AK12**.
2. Copia la receta de recarga correspondiente; puede haber varias, según las referencias del equipo.
3. Comprueba **Tipo `Reload`** y guarda como `AK12_Reload_AjusteContacto_v01`.
4. Reproduce una vez entera.
5. Recorre lentamente el acercamiento, la manipulación y el regreso.
6. Si la mano llega cerca pero no coincide, revisa **Calibrar agarre / punto** y el objetivo adecuado.
7. Cambia poco, vuelve a reproducir toda la acción y comprueba que no has arreglado un contacto estropeando otro.
8. Mantén la mano que sostiene el arma estable si no hay una razón para moverla.

### Antes de guardar posición o dedos

**Guardar posición sustituye el canal procedural de esa muñeca:** reconstruye sus curvas, quita su Motion Clip y pone Reach a cero. La herramienta pide confirmación al empezar una trayectoria que reemplaza una receta existente. **Solo dedos → Guardar dedos** conserva esos canales y bindings.

No sustituye automáticamente:

- La trayectoria de la otra mano.
- El clip del arma.
- `Left Hand Target Motion`.
- Todos los bindings y objetivos del slot.

Por eso, “he grabado una muñeca nueva” no significa “he reemplazado toda la recarga”. Para cambiar únicamente la forma de la mano, usa su pista de dedos; revisa después el clip horneado y los contactos, sin reconstruir la trayectoria.

### Si quieres rediseñar la recarga completa

1. Guarda una versión independiente.
2. Anota qué canales mantendrás y cuáles reemplazarás.
3. Planifica los contactos antes de dibujar el recorrido.
4. Coordina dedos, muñeca, cargador/piezas y arma.
5. Revisa entradas/salidas de IK y grip, sin asumir que bajar Grip equivale a liberar todo IK.
6. Comprueba mirada/Aim y encuadre en varias fases.
7. Integra como variante distinta y prueba las reglas reales en el juego.

El laboratorio no consume ni repone munición durante estas pruebas; tampoco confirma que haya contacto físico con el cargador.

## 15. Calibrar agarres y guardias

Calibrar significa cambiar una **referencia espacial**, no añadir un punto temporal.

1. Selecciona la mano.
2. Despliega **Ajustar agarres de reposo (no es una animación)** y en **Calibrar agarre / punto** elige una referencia por su nombre.
3. Anota los valores iniciales o guarda una toma antes de cambiar.
4. Ajusta **Posición local** y **Giro local** en incrementos pequeños.
5. Inspecciona reposo, mitad y final de la acción.
6. Guarda la toma para conservar las calibraciones.

Los valores locales de calibración siguen siendo metros/grados y son relativos al **padre de ese punto**. Los offsets animados de muñeca se muestran en centímetros respecto a la guardia. No son necesariamente el mismo espacio ni la misma unidad visible.

El selector muestra las referencias disponibles de la mano: guardia/grip utilizado, hint y objetivos cuando existen. No es un editor libre de toda la jerarquía ni un detector automático de agarres.

Cambiar la guardia puede cambiar el origen y orientación de toda la trayectoria de esa mano. Haz las calibraciones grandes antes del pulido final.

**Solo cambia la copia de preview.** Guardar la toma conserva los datos, pero aplicarlos a una escena del juego es una operación posterior y explícita con Undo.

## 16. Panel avanzado explicado

Abre **Avanzado: clips, curvas, pesos y contacto** solo cuando sepas qué capa quieres modificar. Se muestra el inspector de la receta; algunos rótulos siguen los nombres ingleses de sus campos.

| Campo | Significado y precaución |
|---|---|
| **Character Clip** | Clip del personaje. Puede aportar cuerpo/brazos/dedos; el IK final puede dominar brazos según sus pesos. |
| **Character Mask** | Decide qué partes del clip influyen. Si faltan dedos, revisa la máscara además del clip. |
| **Weapon Clip** | Clip Generic del arma separada. No debe animar el holder común ni el personaje. |
| **Left Hand Target Motion** | Clip Generic opcional del objetivo izquierdo de recarga; no sustituye la animación de dedos. |
| **Left / Right Hand Motion → Enabled** | Activa la receta de trayectoria correspondiente; el resultado también depende del binding y pesos de IK. |
| **Reach** | Mezcla desde guardia hacia el action target disponible. `0` es guardia y `1` el objetivo, antes de considerar la mezcla global. |
| **Position / Rotation** | Curvas de offsets locales de muñeca. Guardar posición las reconstruye para esa mano; guardar dedos no las modifica. |
| **Motion Clip** | Movimiento Generic opcional de un Empty propio, no un clip Humanoid de dedos. Guardar posición lo elimina en ese canal. |
| **Follow Animated Right Hand** | Puede conducir el arma desde la muñeca animada antes del IK final. No es un interruptor universal de “pegar arma a mano”. |
| **Duration** | Duración runtime en segundos; la ventana principal la limita entre `0.01` y `300`. |
| **Blend In / Blend Out** | Segundos de entrada/salida de influencia de la acción. No son porcentajes ni curvas de velocidad del recorrido. |
| **Commit Time** | Hito normalizado del runtime. En preview no produce daño ni transferencia de munición. |
| **Can Cancel** | Dato de cancelación; el scrub no demuestra cómo se cancelará realmente en gameplay. |
| **Left / Right Grip** | Multiplicadores de sujeción del equipo. Su efecto depende de la ruta de presentación activa. |
| **Left / Right IK** | Multiplicadores de IK por mano; reducirlos puede hacer reaparecer la pose del clip. |
| **Weapon Position / Rotation** | Curvas de offset del arma en sus espacios locales documentados, no los de la guardia de la mano. |

### Entender Blend In / Blend Out con números

En una toma de `0.6 s`, un Blend In de `0.08 s` ocupa aproximadamente el primer `13.3%` del tiempo. El Blend Out equivalente ocupa el último `13.3%`.

Esto afecta a la influencia de la acción: un punto de offset grande exactamente en `0` puede quedar oculto por peso cero. Si las dos mezclas son demasiado largas respecto a la duración, puede no haber una zona de influencia completa.

Para diagnosticar, guarda una copia y prueba valores menores o cero de forma temporal. No confundas “se ve más fuerte sin blend” con “ya no necesita una transición al juego”.

### Curvas y puntos no son dos capas independientes

Puedes editar curvas avanzadas, pero al volver a **Guardar posición** se reconstruyen Position/Rotation del canal de esa mano con su lista de puntos. La pista de arma reconstruye sus curvas de offsets de la misma forma. Guarda una variante antes de mezclar ambos métodos; guardar dedos no reconstruye estas curvas.

Los tiempos de las curvas de receta se evalúan con progreso normalizado. Los clips tienen su propia duración y el preview los recorre según ese progreso; no confundas sus segundos con el eje `[0,1]` de los puntos.

## 17. Guardar, reabrir, recuperar y versionar

### Guardar correctamente

1. Termina de grabar los cambios de muñeca/dedos que quieras conservar.
2. Reproduce para comprobar que no dependes de una pose temporal.
3. Escribe un nombre reconocible.
4. Pulsa **Guardar como nueva copia**.
5. Comprueba el asset en **`Assets/AnimationLab/Authored`**.

Unity genera una ruta única; repetir nombre puede producir sufijos. Para comparar mejor, usa nombres deliberados:

```text
Fists_Derecha_Recto_v01_Blocking
Fists_Derecha_Recto_v02_Timing
Fists_Derecha_Recto_v03_Dedos
AK12_Reload_AjusteContacto_v01
Knife_Corte_Diagonal_v02
```

Guardar una pose independiente de dedos no sustituye guardar la toma. Guardar la escena de Unity tampoco convierte una pose temporal en puntos de la toma.

### Reabrir

1. Prepara el laboratorio.
2. En **Abrir toma**, selecciona o arrastra el asset guardado.
3. Debe corresponder al mismo slot compatible y `equipmentId`; no se busca libremente otro equipo ni se retargetea el rig.
4. Reproduce antes de continuar editando.

Al reabrir se recuperan el clip/máscara fuente para editar; no se toma el bake anterior como si fuera la animación original. El clip horneado exportado sigue siendo un resultado de esa versión guardada.

### Qué hacer ante una recuperación

Al cerrar, recompilar o intentar entrar en Play se intenta guardar el borrador modificado como **`Recovery_FECHA_HORA`** dentro de Authored, sin bake de dedos.

1. No borres las recuperaciones como caché.
2. Abre la que corresponda desde **Abrir toma**.
3. Comprueba puntos, dedos y referencias.
4. Pon un nombre normal y pulsa **Guardar como nueva copia** para producir el bake final.

La recuperación intenta confirmar también la pose pendiente antes de crear el asset, y depende de que el borrador/preview sean válidos y no haya errores. No es un guardado continuo ni sustituye guardar versiones explícitas.

### Cuándo guardar una versión

- Antes de cambiar equipo o copiar otra acción.
- Antes de sustituir una trayectoria existente.
- Después de tener el primer recorrido legible.
- Antes y después de retimar o cambiar calibraciones importantes.
- Antes de cerrar, compilar cambios de código o comparar modos de columna.
- Siempre después del último cambio y antes de exportar.

## 18. Exportar e integrar en el juego

No hay sincronización automática entre el laboratorio y **`D:\Unity\PRUEBA KINEMATION`**.

### En el laboratorio

1. Graba todos los puntos pendientes.
2. Guarda una copia normal, no solo una recuperación.
3. Pulsa **Exportar copia guardada al juego (.unitypackage)…**.
4. Elige un nombre que identifique la versión.

El paquete incluye toma y código portable seleccionado; no incluye la ventana completa, la escena del laboratorio ni todos los modelos/clips externos. Esos recursos deben existir con sus GUID originales en el destino.

### En el proyecto del juego

1. Importa el `.unitypackage` y revisa la lista de archivos.
2. Si tienes versiones más recientes de scripts compartidos, no las sobrescribas a ciegas.
3. Conserva los `.meta`/GUID; no los regeneres para “arreglar” referencias.
4. Expande el asset de toma y localiza su subasset **Action**.
5. Asigna **Action**, no el contenedor de toma, al campo runtime apropiado:
   - Recarga: Reload o la variante correspondiente de `FirearmDefinition`.
   - Ataque melee: entrada de Attacks de `MeleeDefinition`.
   - Equipar/guardar: campo de acción correspondiente.
6. Mantén la acción anterior disponible para comparar o deshacer la integración.

### Aplicar calibración, si la necesitas

1. Fuera de Play, selecciona la toma importada.
2. En **Equipo de destino**, asigna el `EquipmentSceneController` correcto de la escena.
3. Pulsa **Aplicar agarres a la escena (Undo)** y confirma.
4. Revisa el resultado antes de guardar la escena.

Se exige un `equipmentId` que identifique exactamente un slot y referencias compatibles del personaje de destino. No se deben resolver agarres dentro de las cadenas de brazo. Si falla, revisa la identidad y jerarquía, no asignes otro equipo al azar.

**Importar no asigna la acción ni aplica los grips automáticamente.** Tampoco añade un sistema de combate o lanzamiento. Después de integrar, prueba en Play del juego contactos, cancelación, transiciones y reglas reales de munición.

No publiques paquetes que incluyan recursos o curvas derivados de terceros sin revisar su licencia y tener autorización.

## 19. Revisión final y verificación técnica

### Revisión artística: primero velocidad normal

1. Mira una reproducción completa a Velocidad `1` sin tocar controles.
2. Describe en una frase qué se ve. ¿Coincide con tu intención?
3. Repite en cámara lenta para localizar la causa de un fallo, no para decidir por sí sola si tiene buen ritmo.
4. Recorre después los fotogramas cercanos al contacto y los cambios de dirección.

Lista de calidad:

- [ ] Se entiende qué está haciendo cada mano.
- [ ] La muñeca no se dobla de forma extraña para compensar un target incorrecto.
- [ ] Los dedos no atraviesan palma, arma ni cargador de forma evidente.
- [ ] Los contactos importantes se mantienen sin deslizamiento.
- [ ] No hay saltos, giros inesperados ni cambios bruscos de codo.
- [ ] El brazo no queda forzado al máximo de alcance.
- [ ] Preparación, acción principal y recuperación tienen un ritmo intencional.
- [ ] La mano vuelve a una pose compatible con el reposo.
- [ ] No depende de un slider temporal sin grabar.

### Matriz de vistas

Prueba como mínimo inicio, preparación, contacto visual, recuperación y final en:

| Prueba | Qué buscar |
|---|---|
| Vista externa frontal | Silueta, alineación y contacto. |
| Vista externa lateral | Profundidad, penetraciones y alcance. |
| Primera persona, mirada neutra | Encuadre y lectura de manos/arma. |
| Primera persona mirando arriba/abajo | Separaciones, codos extraños y recortes. |
| Mirada lateral | Cambios de orientación y relación con la cámara. |
| Aim activado/desactivado, si corresponde | Diferencias de encuadre y agarres. |
| Ambos modos de columna | Compatibilidad del resultado con las dos rutas. |

### Verificación automática

Ejecuta **Tools → CodeHelper → Animation Lab → Verificar laboratorio (aislado)** cuando necesites comprobar el sistema, especialmente tras cambios técnicos.

La prueba adicional **Verificar edición por fotogramas** comprueba sincronización de campos, independencia de muñecas/dedos/arma, compatibilidad con puntos antiguos y guardado/reapertura.

La prueba revisa equipos/modos, scrub reproducible, ausencia de consumo/commits al mover tiempo, guardado/bake/exportación y limpieza de previews. Genera o reemplaza artefactos de validación en la raíz del laboratorio; no es un análisis artístico de tu toma concreta.

Consulta [VALIDACION.md](../VALIDACION.md) para las validaciones históricas. Que una prueba técnica pase no certifica el contacto de tu nueva animación ni su calidad visual; la integración en otro proyecto y la compilación sin editor son comprobaciones adicionales.

## 20. Problemas habituales y solución

| Síntoma | Qué revisar primero |
|---|---|
| No aparece la ventana | Proyecto correcto, compilación terminada y menú Tools → CodeHelper → Animation Lab. |
| Pide salir de Play | Sal de Play de Unity; usa Reproducir de la herramienta. |
| Arrastrar gira la vista | Has arrastrado el fondo, no el punto amarillo. |
| La mano no avanza hacia donde esperabas | Los ejes son locales a la guardia, no a la pantalla. |
| Al reproducir la muñeca no se desplaza en un extremo | El ajuste sin fundidos y el resultado final no son lo mismo; revisa Blend In/Out sin compensar con offsets enormes. |
| No cambia el fotograma y aparece un diálogo | Hay una pose pendiente: elige Guardar punto, Seguir editando o Descartar ajuste. |
| Necesito recuperar un punto exacto | Pulsa su rombo en la pista; los puntos antiguos fuera de rejilla conservan su tiempo exacto. |
| Quiero editar dedos sin afectar la muñeca | Selecciona Solo dedos / pista de dedos, no Posición y giro. |
| El ajuste fino de dedos desaparece | Un slider recalculó desde la base; haz ajuste fino al final o captura otra base. |
| No aparecen sliders de dedos | Captura dedos, carga una pose o aplica Mano abierta/cerrada. |
| Un dedo se retuerce | Eje local incorrecto o limitación del rig; usa preset Humanoid y ajuste individual. |
| Cambiar eje tuerce los dedos | Se recalcula la flexión con el eje elegido; vuelve al anterior o usa un preset/ajuste fino. |
| No puedo cargar una pose de dedos | Comprueba que pertenece a la mano seleccionada. |
| La forma cambia un poco al grabar | Normalización a los grados de libertad Humanoid del avatar. |
| Los dedos difieren al reproducir respecto al ajuste | Editas la pose fuente sin fundidos; reproducir aplica las mezclas finales y límites Humanoid. |
| Se rompió una recarga al guardar posición | Aceptaste reemplazar Motion Clip/Reach y curvas; vuelve a una copia. Para cambiar solo forma, utiliza Guardar dedos. |
| Quitar todos los puntos no devuelve el movimiento original | No se conserva como una capa debajo; reabre la versión anterior o vuelve a copiar la receta. |
| Mano y arma se separan | No necesariamente comparten el mismo controlador de movimiento; revisa arma, muñeca, clips y bindings. |
| Parece correcto desde fuera pero mal en primera persona | Corrige encuadre/alcance desde primera persona; no solo la vista externa. |
| El principio o final pierde fuerza | Revisa duración de mezclas y pose de guardia/base. |
| Cambié la curva avanzada y luego se perdió | Guardar posición reconstruye las curvas de esa pista; elige una fuente de autoría. |
| La copia tiene Tipo inesperado | Se deduce de su referencia en el equipo; revisa Tipo si cambias la finalidad de la acción. |
| No se habilita exportar | Guarda una copia y vuelve a guardar tras cambios del borrador. |
| No abre una toma | Slot, `equipmentId` o recursos del rig incompatibles. |
| La recuperación no tiene resultado final de dedos | Se guarda sin bake; ábrela y guarda una copia normal. |
| Importé y el juego sigue igual | Falta asignar Action y, si corresponde, aplicar calibración por separado. |
| Faltan referencias tras importar | El paquete no incluye todos los recursos externos; deben existir con los mismos GUID. |
| No se gasta munición ni hay daño | Es intencional en preview; compruébalo en el juego al integrar. |

## 21. Plan de práctica y ficha reutilizable

### Sesión 1: dominar el ciclo de trabajo

Crea el golpe de tres puntos de la sección 6, guárdalo, cierra la ventana y vuelve a abrirlo. Objetivo: comprobar que sabes distinguir preview temporal, punto grabado y asset guardado.

### Sesión 2: mejorar solo el ritmo

Usa la misma forma de puño y modifica cuándo prepara, extiende y vuelve. Compara dos versiones a velocidad normal. Objetivo: notar que la sensación puede cambiar sin aumentar el alcance.

### Sesión 3: dedos y contacto

Practica una mano relajada que se cierra y vuelve a abrirse. Después conserva un agarre con pulgar bien situado. Objetivo: que cada dedo tenga una función, sin penetraciones ni movimiento gratuito.

### Sesión 4: cuchillo

Copia un ataque, identifica sus canales y cambia una sola cosa: duración, contacto o trayectoria, según lo que controle la receta. Objetivo: entender antes de sustituir.

### Sesión 5: recarga

Copia una recarga y ajusta un contacto por calibración sin grabar trayectorias nuevas. Objetivo: distinguir referencias espaciales de animación temporal.

### Sesión 6: revisión e integración

Elige una toma, revisa la matriz de vistas, guarda y exporta. Integra manualmente como variante. Objetivo: verificar que el resultado guardado funciona fuera del preview.

### Ficha de una nueva toma

```text
Nombre / versión:
Equipo y mano(s):
Tipo runtime:
Acción nueva o receta fuente:
Referencia e intención:
Duración:

Tiempo normalizado | fase | muñeca: posición/giro | dedos | contacto deseado
0.00               |      |                       |       |
                   |      |                       |       |
                   |      |                       |       |
1.00               |      |                       |       |

Canales que mantengo:
Canales que sustituyo:
Calibraciones cambiadas:
Resultado en primera persona y con mirada/Aim:
Qué mejoró respecto a la versión anterior:
Problemas pendientes:
Asset guardado / paquete exportado:
Probado en el juego: sí / no, y qué se comprobó realmente.
```

**Rutina corta para recordar:** referencia → intención → poses clave → grabar → ritmo → arcos → dedos/contactos → primera persona/mirada → guardar → exportar → probar en el juego.
