# Animation Lab: piezas, objetos diseñados y agarres

Esta ampliación permite trabajar con piezas separadas del arma, diseñar referencias con primitivas y grabar poses coordinadas de objeto/manos. Se usa **fuera de Play**, desde **Tools → CodeHelper → Animation Lab → Abrir laboratorio**.

Para recorridos curvos entre claves y generar cargador, revólver, cartuchos o cerrojo con tres referencias, consulta [Recorridos y recargas prefabricadas](GUIA_RECORRIDOS_Y_RECARGAS.md). El asistente muestrea la muñeca para acompañar la pieza; los conjuntos manuales descritos aquí siguen siendo solo poses coordinadas.

## 1. Qué puedes hacer y qué no hace automáticamente

| Necesidad | Herramienta |
|---|---|
| Mover toda el arma | **Arma**: offsets del soporte completo. |
| Mover cargador, palanca de carga u otra pieza | **Piezas / diseños** y selector **Pieza / objeto**. |
| Mover una mano con ejes del escenario | **Posición y giro → Global (escenario)**. |
| Mover mano y pieza en un fotograma | **Mover junto con** y **Guardar conjunto mano + pieza**. |
| Reutilizar formas de mano | **Solo dedos → Agarres preparados y personalizados**. |
| Crear un objeto rectangular, redondo o compuesto | **Piezas / diseños → Nuevo objeto**, añadiendo cubos, esferas y cilindros. |
| Obtener una primera forma de dedos alrededor del objeto | **Objeto para adaptar dedos → Aproximar dedos a la superficie**. |

Límites importantes:

- Una pieza debe existir como **Transform/hueso separado**. No se separan automáticamente vértices de una malla única.
- Un conjunto guarda pistas en el mismo tiempo; **no crea una unión física o un parenting permanente de la mano al objeto**. Revisa los intervalos y añade puntos si el contacto se separa.
- Global es un **espacio para editar el fotograma**. Los datos se convierten a la receta local; no bloquea una mano en coordenadas mundiales para todas las miradas o movimientos posteriores.
- Los agarres preparados son **formas iniciales de dedos**, no calibraciones universales de muñeca ni agarres terminados.
- La adaptación procedural aproxima el cierre de dedos. **No coloca la muñeca, no garantiza ausencia de penetraciones y no calcula estabilidad física del agarre.**
- Los diseños son referencias de autoría. Crear un hacha con formas no añade un cuarto equipo ni gameplay de hacha al runtime.

## 2. Moverse por la vista sin mover el personaje

En **Personaje**, la cámara externa admite:

| Gesto | Resultado |
|---|---|
| Arrastrar fondo con botón izquierdo | Orbitar. |
| Rueda | Acercar/alejar. |
| Arrastrar con botón central | Desplazar lateral/verticalmente la vista. |
| Shift + arrastrar con botón izquierdo | Otra forma de desplazar la vista. |
| Mantener botón derecho y mover ratón | Orientar la cámara de navegación. |
| Mantener botón derecho + W/A/S/D | Avanzar, retroceder y desplazarse lateralmente. |
| Mantener botón derecho + Q/E | Bajar/subir en el escenario. |
| Shift durante la navegación | Moverse más rápido. |
| **Encuadrar selección** | Centrar y acercarse a la mano/pieza actual. |
| **Restablecer vista** | Volver al encuadre inicial. |

Haz clic derecho dentro de la vista para darle el control de navegación, no dentro de un campo de texto. Al soltar el botón o perder el foco se termina la navegación. Estos desplazamientos no animan al personaje ni cambian la cámara del juego.

La navegación externa no se aplica en **Primera persona**. Cambia a Personaje para inspeccionar un cargador de cerca; vuelve luego a Primera persona para comprobar el encuadre real de la toma.

## 3. Elegir el cargador o la palanca de carga del AK12

1. Prepara el laboratorio y elige **AK12**.
2. Si vas a modificar una recarga, copia primero la receta y guarda una versión independiente.
3. Selecciona **Piezas / diseños**, no Arma.
4. En **Pieza / objeto**, elige la ruta de la pieza. En el modelo actual:
   - **Cargador**: `AK12Rig/Root/Mag`.
   - **Palanca de carga / cerrojo**: `AK12Rig/Root/Charge`.
   - También existe `AK12Rig/Root/Safety`.
5. Pulsa **Encuadrar selección** si la pieza queda pequeña o fuera de vista.
6. Elige un fotograma.
7. Ajusta posición/giro o arrastra el **punto cian**.
8. Pulsa **Guardar posición en F …**.

`Charge` identifica la pieza de carga del modelo; no implica que el laboratorio exponga cualquier componente mecánico interno que no esté modelado por separado.

### Antes de sustituir movimiento que ya existe

Si el clip original anima esa pieza, la herramienta pide confirmar la sustitución de sus canales de posición/giro. Las otras piezas y sus curvas se conservan.

Empieza creando un punto de la posición inicial y otro de la posición final. Un único punto se mantiene durante toda la pista: no significa “cambiar solo un instante y dejar intacto el resto del clip”.

El último renglón de la línea de tiempo muestra **la pieza/diseño seleccionada**. Cambiar de pieza cambia los rombos visibles en ese renglón, pero no elimina las pistas grabadas de las demás piezas; todas se reproducen juntas.

## 4. Local frente a Global

En los editores de posición hay dos opciones:

- **Local:** sigue los ejes de la guardia de la mano o del padre de la pieza/soporte.
- **Global (escenario):** posición absoluta en los ejes del escenario y giro mundial.

Los campos visibles usan centímetros. En huesos importados con escala, la herramienta convierte las unidades para editar el tamaño físico en vez de obligarte a manejar las unidades internas del FBX.

**Ejemplo:** si Y global muestra `135 cm` y quieres bajar la pieza `5 cm`, cambia Y a `130 cm`. Escribir `-5` la llevaría a Y global = −5 cm; no significa “bajar cinco”.

El arrastre del punto sigue trabajando en el plano de la cámara. Para cambiar profundidad con precisión, usa los campos y otra vista. Alternar Local/Global no añade puntos ni cambia por sí solo la pose.

## 5. Guardar un conjunto de cargador y mano

Ejemplo didáctico de una toma de `1 s`, con inicio F 0 y final F 60:

1. Selecciona el cargador `Mag`.
2. En **Mover junto con**, elige **Mano izquierda**.
3. Decide si quieres **Incluir dedos en el conjunto**. Si está activado, también guarda su forma en ese tiempo.
4. Ve a F 0 y guarda el conjunto inicial.
5. Ve a F 30.
6. Desplaza o gira el cargador. La mano elegida acompaña el ajuste conservando su relación inicial con esa pieza en ese fotograma.
7. Pulsa **Guardar conjunto mano + pieza en F 30**.
8. Ve a F 60, establece la pose final y guarda otro conjunto.
9. Reproduce a velocidad normal y comprueba también los intervalos entre puntos.

**Ahora el recorrido también es común:** dos puntos guardados como conjunto conservan el contacto mano–pieza. Selecciona la pieza y abre **Recorrido hasta el siguiente punto**: verás **CURVA COMÚN · pieza + mano izquierda**. Cambiar Directo/Arco/Libre o arrastrar el control de curva recalcula también la muñeca; no tienes que repetir la curva en la pista de la mano. Al arrastrar se recalcula al soltar.

Para conjuntos guardados antes de esta mejora, elige la mano en **Mover junto con** y pulsa **Unir mano y pieza en este tramo** una vez. Se capturan los contactos de los dos extremos, sin cambiar dedos. No se enlazan tomas antiguas silenciosamente.

Desde la pista de la mano, **Editar la curva común** lleva al control de la pieza. **Separar las manos de este tramo** permite volver a editar la mano por separado; no separa el tramo vecino y conserva las curvas actuales.

Puedes escoger **Ambas manos**, o **Solo la pieza** si no debe arrastrar una mano. En una recarga heredada, tomar control de la muñeca puede sustituir su ruta anterior: lee la confirmación y conserva una versión de la receta original.

### Qué significa combinar aquí

La toma puede contener simultáneamente:

- Clip corporal/de dedos de la receta fuente.
- Clip de las piezas del arma, con los canales que sustituyas.
- Trayectorias de muñeca izquierda y derecha.
- Puntos independientes de dedos.
- Curvas del soporte completo del arma.
- Varias pistas de piezas y referencias diseñadas.

El recorrido combinado guarda contactos locales por datos y compila la muñeca a curvas normales a 60 Hz sobre la pieza curvada/girada, compensando los fundidos. No añade constraints ni parenting dinámico al runtime. Los dedos conservan su pista/transición. Si los contactos de ambos extremos son diferentes, se interpolan: no se fuerza un agarre rígido inventado.

Revisa alcance, giros y contacto entre muestras. Se rechazan dos piezas simultáneas para una mano, pesos de solver incompletos o contactos incompatibles con un fundido de peso cero. Un rechazo al guardar/unir conserva el borrador anterior. Para la derecha con Follow Animated Right Hand hay que desactivarlo explícitamente antes de combinarla.

## 6. Diseñar un objeto con cubos, esferas y cilindros

1. En **Piezas / diseños**, pulsa **Nuevo objeto**.
2. Aparecerá un diseño de referencia cerca de la mano seleccionada, con una primera caja.
3. Pon un **Nombre del diseño** reconocible.
4. Usa **+ Cubo**, **+ Esfera** o **+ Cilindro** para añadir formas.
5. Elige **Forma a diseñar**.
6. Ajusta **Centro de la forma (cm)**, **Giro de la forma** y **Dimensiones completas (cm)** mirando el resultado.
7. Para mover todo el diseño, usa los controles de posición de la pieza/diseño; para cambiar su silueta, edita las formas.
8. Guarda la toma para conservar el diseño.

Ejemplos de construcción:

- **Cargador rectangular:** una caja estrecha; añade otra más ancha como base si hace falta.
- **Mango de cuchillo:** cilindro o caja alargada; una caja pequeña puede representar una guarda.
- **Hacha de referencia:** cilindro alargado para el mango y caja aplanada para la cabeza.
- **Pomo o tirador:** cilindro corto más esfera.

En cilindros, los campos representan **diámetro / altura / diámetro**, no semialturas. Las esferas con dimensiones diferentes sirven como referencias elipsoidales. Usa **Quitar esta forma** para retirar una parte y **Quitar este diseño de la toma** para eliminar el objeto completo; Undo permite recuperar los cambios del diseño.

Este es un constructor visual por parámetros con resultado en el preview, no una herramienta de escultura de mallas o booleanas. No crea colliders de gameplay: la adaptación consulta la geometría de referencia, sin activar física en el laboratorio.

### Guardar como prefab

**Guardar diseño como prefab…** guarda una copia de la geometría y su material, sin modificar la escena abierta. El prefab:

- Es un objeto estático reutilizable.
- No lleva automáticamente las claves de movimiento de la toma.
- No se convierte por sí solo en un arma o una definición de equipo.
- Es una exportación distinta de guardar/exportar la toma.

Los diseños se guardan también como datos de la toma y se recrean al reabrirla en el laboratorio. El runtime del juego no instancia esas referencias automáticamente al recibir Action.

## 7. Usar agarres preparados y crear tus tipos

En una mano, abre **Solo dedos**. La entrada recomendada es **1 · Elige el agarre de llegada**. Para guardar tipos, articulaciones y herramientas avanzadas, abre **Ajustar dedos / crear variantes → Agarres preparados y personalizados**.

Tipos iniciales:

- **Sostener arma · animación cargada**: captura los dedos reales de F 0 de la toma, sin usar el preset de cargador ni las correcciones manuales de dedos.
- **Cuchillo · mango**.
- **Hacha · agarre fuerte**.
- **Cargador · rectangular**.
- **Cerrojo · pinza**.
- **Esfera · envolver**.

Selecciona uno y pulsa **Aplicar agarre como copia editable**. El agarre inicial se obtiene de la animación cargada; los demás se generan usando músculos Humanoid del avatar actual. No se copian a ciegas rotaciones de otra mano. Después corrige pulgar, separación y articulaciones si lo necesitas, y pulsa **Guardar dedos en F …**.

En **Agarre de llegada → Base inicial · sostener arma** puedes volver al agarre básico o partir de él y construir variaciones, sin mover la muñeca.

Para guardar uno propio:

1. Prepara la forma con presets, sliders, ajuste fino o aproximación a una superficie.
2. Escribe **Nombre de mi agarre** y **Categoría**; por ejemplo `Cargador_AK12_Lateral` y `Rectangular`.
3. Pulsa **Guardar como nuevo tipo / variante…**.
4. Se crea un asset `LabGripPreset` con su pose de dedos propia.
5. Para reutilizarlo, asígnalo a **Mi tipo / variante** y pulsa **Aplicar mi tipo como copia editable** en una mano compatible.

El tipo personalizado y sus dedos son copias independientes. No se aplica automáticamente a la otra mano ni a otro avatar. Cargarlo tampoco coloca la muñeca: eso sigue siendo parte de la pose/recorrido que diseñas.

### Decidir cuándo cambia el agarre

En **Solo dedos → Transición hacia este agarre**:

- **Suave**: conserva el agarre anterior y mezcla con entrada/salida suaves durante los fotogramas indicados.
- **Lineal**: mezcla a velocidad constante durante ese mismo intervalo.
- **Instantánea**: mantiene el anterior hasta el fotograma de destino.
- **Original (compatible)**: conserva la interpolación de tomas antiguas.

### Ejemplo exacto: base F 0–20 y adaptación F 20–40

1. En **F 0**, elige **Base inicial · sostener arma** (o coloca tus dedos) y pulsa **Guardar agarre de llegada en F 0**.
2. Ve a **F 40**, sin crear un punto intermedio en F 20.
3. En **Agarre de llegada**, elige **Cargador · rectangular**.
4. En **Cómo llegar**, selecciona **Suave**.
5. Escribe **Mantener hasta F = 20**. **Llegar al agarre en F = 40** es el fotograma de destino seleccionado.
6. La barra muestra **MANTENER hasta F 20 → ADAPTAR hasta F 40**.
7. Pulsa **Base**, **Inicio cambio**, **50%** o **Llegada**: el visor enseña la pose de F 0, F 20, F 30 y F 40. También puedes mover **F de prueba** o pulsar **Ver proceso completo**.
8. Durante la prueba el destino sigue siendo F 40. El visor indica el fotograma probado y el porcentaje de adaptación; no se insertan claves por probar.
9. Pulsa **3 · Guardar agarre de llegada en F 40** y después guarda la toma.

Resultado: **F 0–20 mantiene exactamente el agarre base; F 20–40 lo transforma gradualmente; F 40 alcanza el cargador**. El botón **Mitad mantener / mitad adaptar** configura ese reparto de un clic cuando los puntos son F 0 y F 40.

Si no guardaste la base y este es el primer punto de dedos posterior a F 0, se captura automáticamente la pose inicial. El cambio y ese punto inicial se deshacen juntos. **Volver a editar destino** cancela solo la vista previa; no guarda el candidato.

La duración pertenece al punto de llegada, no al siguiente tramo. Una clave anterior limita el inicio: no se saltan ni borran otras claves de dedos silenciosamente. Para preparar el cambio más tarde, reduce su duración o añade un punto de mantenimiento. Al introducir otro tipo sobre una clave antigua se propone la transición suave nueva; puedes cambiarla antes de guardar.

Esto mantiene la **pose inicial** de los dedos; no preserva una animación dinámica completa de dedos antes del primer cambio. La muñeca y los clips fuente se conservan.

Los conjuntos **mano + pieza** con dedos incluidos usan la misma protección del agarre inicial y permiten indicar **Mantener dedos hasta F** desde el panel de la pieza. **Configurar agarre y ver su transición…** abre la pista de dedos de la mano. El bake incluye los instantes exactos de cambio: **Instantánea** no se convierte en una mezcla anticipada al exportar.

## 8. Aproximar los dedos a un objeto diseñado

1. Diseña la referencia y colócala con un tamaño realista.
2. Sitúa la muñeca alrededor de la zona que quieres agarrar. Comprueba orientación de palma y pulgar.
3. Ve a **Solo dedos** de esa mano.
4. En **Objeto para adaptar dedos**, selecciona la referencia diseñada o una pieza compatible.
5. Pulsa **Aproximar dedos a la superficie**.
6. Revisa desde delante, de lado y cerca del pulgar.
7. Corrige con sliders/ajuste fino.
8. Pulsa **Guardar dedos** para la animación y/o guarda tu tipo de agarre reutilizable.

### Cómo funciona y cómo interpretar el resultado

El ajuste prueba progresivamente la flexión Humanoid de cada dedo y estima si sus segmentos se acercan a la superficie. En diseños de primitivas consulta la forma compuesta; en piezas con renderers usa su caja visible como aproximación.

- El número de dedos que alcanzaron una referencia es una **indicación geométrica**, no una puntuación de calidad ni prueba de estabilidad.
- Si el objeto está lejos o la palma apunta mal, no moverá mágicamente el brazo para resolverlo. Recoloca la muñeca y repite.
- Si una pieza es únicamente un hueso de un SkinnedMesh y no tiene geometría visible propia, no se conoce con precisión qué zona de la malla le corresponde. Usa un diseño de referencia equivalente.
- Si la mano ya atraviesa la referencia antes de empezar, el cierre por sí solo no resolverá la penetración.
- Los dedos siguen sujetos a los límites del avatar Humanoid y pueden necesitar corrección, especialmente el pulgar.

## 9. Guardado, horneado y comprobación

- Las piezas y diseños tienen claves propias; guardar posición de una pieza no sustituye las curvas de otras.
- Al guardar una toma normal, las piezas originales se hornean en un clip **Generic** del arma, conservando canales no reemplazados y quitando eventos.
- La toma conserva el clip de arma fuente para volver a editar sin reutilizar el bake como original.
- Los diseños de referencia y sus puntos quedan en la toma; el prefab es geometría separada y no se exporta automáticamente como parte de Action.
- La integración en el juego sigue siendo explícita. No cambia escenas, munición, daño o reglas de coger/soltar por importar una toma.

Prueba técnica: **Tools → CodeHelper → Animation Lab → Verificar piezas, diseños y agarres**. Para ejecutar también las suites anteriores por batch, usa `AnimationLabWorkshopChecks.RunAll`.

Antes de considerar el trabajo terminado, reproduce la toma, revisa contactos entre claves, prueba primera persona/mirada y guarda una versión. El funcionamiento de los datos no sustituye la revisión visual ni una prueba con otra persona.
