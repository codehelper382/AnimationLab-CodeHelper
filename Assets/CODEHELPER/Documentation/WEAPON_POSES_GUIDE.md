# Poses propias de arma: Idle y Aim

`WeaponPoseProfile` guarda datos locales por arma. `WeaponPoseController` mezcla las poses y lee
`PlayerLookController.IsAiming` (botón derecho en el controlador actual). No necesitan KINEMATION,
otro Animator ni un grafo de animación. No animan dedos, piezas del arma ni recargas.

## 1. Conservar primero tu Aim actual

Fuera de Play Mode, sin mover todavía el arma:

1. Selecciona **Weapon Holder**, el objeto que contiene el arma, `Hands Grips` y `Elbows Grips`.
2. Añade **CodeHelper > Shooter Framework > Weapons > Weapon Pose Controller**.
3. **Pose Root:** el propio `Weapon Holder`.
4. Activa **Use Look Controller** y asigna **Player / PlayerLookController**.
5. Pulsa **Crear perfil y guardar Aim actual...**.
6. Guarda el asset, por ejemplo como `AK12_Poses.asset`, dentro de una carpeta propia del proyecto.
7. Comprueba que el Inspector indica **Aim: Guardado**, **Idle: Sin capturar**.
8. Guarda la escena para conservar el componente y sus referencias.

El botón guarda la posición y rotación **locales** actuales del ancla, sin moverla. No usa valores
fijos del ejemplo ni modifica los grips. El perfil creado tiene Aim y deja Idle pendiente a propósito.
Una pose sin capturar no mueve el arma: no se interpreta como `(0,0,0)`.

No pongas el componente en `Camera Holder`, Player, Head ni en un hueso de brazo. No debe controlar
la cámara o el esqueleto. Mantén la escala y el mismo padre de referencia para ambas capturas.
El perfil no guarda escala, posiciones de cámara ni referencias a objetos de escena.

## 2. Preparar Idle

1. Continúa fuera de Play Mode.
2. Mueve y rota **Weapon Holder** hasta obtener la postura de reposo deseada.
3. No ajustes las manos/grips por separado: deben acompañar al conjunto y conservar el agarre.
4. Pulsa **Guardar posición actual como Idle**.
5. Comprueba que ambas poses figuran como guardadas.
6. Usa **Previsualizar Aim** y **Previsualizar Idle** para alternar las colocaciones.
7. Ajusta **Transition Duration** en el asset; el punto de partida es `0.18` segundos.
8. Guarda la escena en la postura de inicio que prefieras.

La previsualización mueve el ancla en edición; admite **Undo** y registra overrides de prefab.
No reproduce automáticamente Animator/IK en edición: verifica la pose completa de brazos y dedos
en Play Mode. Guardar una pose existente pide confirmación porque el asset puede estar compartido.

## 3. Probar las transiciones

- En Play, sin apuntar se selecciona Idle; al mantener el botón derecho se selecciona Aim.
- Captura el cursor en Game si está libre; el controlador de mirada debe tener entrada habilitada.
- Comprueba la transición en ambos sentidos, incluyendo soltar/apuntar antes de que termine.
- Comprueba que arma, grips e hints se mueven juntos y que el IK mantiene las manos.
- `WeaponPoseController` no mueve la cámara. Opcionalmente `CameraRigController` lee **Aim Camera**
  del mismo perfil para añadir FOV y un pequeño efecto angular independiente (ver más abajo).
- La pose Aim guardada manualmente no garantiza alineación automática de cualquier mira:
  calibra la colocación actual visualmente. Un sistema `AimPoint` sería una ampliación posterior.

Orden: **cámara 500 -> poses de arma 550 -> manos 600**. Evita otro Animator/script escribiendo
el mismo `Weapon Holder` después. Un Animator de piezas móviles del arma no necesita mover esa ancla.

Las transiciones usan posición interpolada y rotación de cuaterniones, con entrada/salida suave.
Al invertir el estado parten de la transformación que se está mostrando, sin volver al extremo previo.
No suman offsets sobre el resultado anterior. Cero segundos aplica la pose inmediatamente.

## Sincronizar arma, zoom y rebote

En el perfil, **Synchronize Aim Effects** activa un reloj compartido. Ya está activado en
`Assets/AK 12.asset`, conservando **Transition Duration = 0.18 segundos**.

- El arma y el FOV usan exactamente el mismo progreso y suavizado, no velocidades independientes.
- El rebote del arma y de la cámara ocurre dentro de ese intervalo y termina en cero en el mismo
  frame en que la pose alcanza el destino. Duración cero significa cambio inmediato, sin rebote.
- **Transition Oscillations**, inicialmente 1, controla las oscilaciones dentro del intervalo.
- **Kick**, **Damping** y los límites siguen ajustando dirección, intensidad y forma del movimiento.
- En este modo **Frequency** y **Fov Blend Speed** no gobiernan los efectos de Aim. Se conservan
  en el asset para el modo independiente, pero no hace falta reajustarlos al cambiar la duración.
- Al invertir la entrada a mitad de transición, se parte del resultado actual sin saltar al extremo
  anterior. La nueva transición utiliza de nuevo la duración completa del perfil.

El modo sincronizado usa una oscilación amortiguada de duración finita y una envolvente suave:
no es el resorte físico libre cortado bruscamente al final. Es útil para un ADS coherente y fácil
de ajustar. Una duración muy corta puede sentirse demasiado rápida: prueba 0.18 y, si resulta
brusca, compara con 0.25 o 0.3 segundos sin modificar las poses ni las intensidades.

Si prefieres que una oscilación física dure más que la entrada a Aim, desactiva **Synchronize Aim
Effects**: vuelven a funcionar los resortes independientes con Frequency y el FOV exponencial con
Fov Blend Speed. Los perfiles nuevos conservan este modo anterior hasta que se active la sincronización.

El reloj se prepara en **WeaponPoseController.Update**, después de la entrada del jugador. La cámara
lo lee en LateUpdate 500 y el arma aplica su resultado en 550; no se avanza dos veces por frame.
**Use Unscaled Time** del controlador del arma decide el reloj compartido. El FOV base, recoil y
otros efectos siguen teniendo su propio comportamiento. Al desactivar/ocultar el arma o cambiar
de perspectiva, la retirada del canal de cámara puede usar su suavizado de limpieza independiente.

En la configuración actual se conservó el zoom elegido por el usuario: **Aim Fov Offset = -10**
y el canal de cámara solo en FPS. Con base 60, el destino es 50. La sincronización no cambia esos
valores, la posición de cámara, la jerarquía ni las poses capturadas.

## Rebote procedural al apuntar (opcional)

El perfil incluye **Aim Motion**, inicialmente desactivado para conservar el comportamiento anterior.
No requiere cambiar la jerarquía ni añadir otro componente.

1. Selecciona el asset de poses y despliega **Aim Motion**.
2. Activa **Enabled**.
3. Mantén inicialmente **Frequency = 4.5**, **Damping = 0.6** y **Transition Duration = 0.18**.
4. Entra y sal de Aim en Play. No hace falta recapturar Idle/Aim.

En modo independiente, el efecto es un resorte amortiguado que recibe un impulso al cambiar entre
dos estados válidos. Con sincronización, estos ajustes alimentan la oscilación de duración finita:

- **Aim Position Kick / Idle Position Kick:** intensidad y dirección del impulso, en metros como
  escala de ajuste; los ejes son los del padre del ancla. No son desplazamientos permanentes.
- **Aim Rotation Kick / Idle Rotation Kick:** escala angular en grados, en los ejes locales del arma.
- **Frequency:** rapidez de oscilación solo en modo independiente.
- **Damping:** amortiguación. Más cerca de 1 vuelve sin tanta oscilación; menos valor produce más rebote.
- **Max Position Offset / Max Rotation Offset:** límites de magnitud para evitar acumulación excesiva.

Los valores iniciales son suaves. Para reducir el efecto, disminuye los Kick o aumenta Damping.
No aumentes todos los parámetros a la vez. Los límites no sustituyen una comprobación visual de alcance
de brazos o penetración del arma con el cuerpo.

El resultado es **pose base interpolada + rebote temporal**, aplicado antes del IK de manos.
Este canal de arma no mueve cámara ni FOV, no altera los grips locales ni escribe en las poses guardadas.
Al mantener el estado el resorte vuelve a cero y queda exactamente la pose de destino.
No se añade impulso al aparecer/re-activar el componente; solo al cambiar de estado dentro del mismo contexto.
Un cambio rápido de Aim conserva el desplazamiento del resorte, cambia su impulso y limita su energía.
Se eliminan los offsets propios antes de tomar una nueva base, evitando incorporarlos a una transición.
Desactivar el efecto o el componente elimina su residuo; el componente conserva la pose base actual.
No se restaura sobre una transformación distinta escrita posteriormente por otro sistema.
Si necesitas cambiar el padre del ancla durante el juego, desactiva primero el controlador para
retirar el rebote en su espacio original; después cambia el padre y revisa las poses locales.

### Diagnóstico visible del AK12 y regreso al ajuste suave

El perfil real `Assets/AK 12.asset` estaba referenciado correctamente en la escena, pero no tenía
el bloque `aimMotion` guardado: el valor por defecto desactivado impedía ver el efecto. Se ha añadido
un preset deliberadamente exagerado para comprobarlo, conservando las poses, su duración y el GUID.

En el Inspector del perfil y del controlador hay dos botones con confirmación:

- **Rebote MUY visible (prueba):** activa el diagnóstico exagerado.
- **Rebote suave (recomendado):** guarda los valores suaves iniciales, dejando el efecto activado.

Ambos modifican solo Aim Motion, admiten Undo y guardan el perfil. El preset suave **no significa
restaurar el asset original**, que tenía el efecto desactivado: para dejar de usarlo, desmarca Enabled.

| Parámetro | Suave | Diagnóstico exagerado |
|---|---|---|
| Frequency | 4.5 | 2 |
| Damping | 0.6 | 0.2 |
| Aim Position Kick | `(0, -0.006, -0.012)` | `(0, 0.08, -0.05)` |
| Idle Position Kick | `(0, 0.003, 0.006)` | `(0, -0.06, 0.04)` |
| Aim Rotation Kick | `(-1.5, 0, 0.4)` | `(-12, 0, 6)` |
| Idle Rotation Kick | `(0.8, 0, -0.25)` | `(10, 0, -5)` |
| Max Position Offset | 0.025 m | 0.1 m |
| Max Rotation Offset | 4 grados | 15 grados |

En mediciones aisladas a 120 Hz, el preset suave produce aproximadamente **0.67 cm / 0.77 grados**
de pico al entrar en Aim. El diagnóstico produce aproximadamente **7.13 cm / 10.14 grados**.
No son offsets permanentes ni límites alcanzados necesariamente: el resorte regresa a cero.
El diagnóstico puede causar penetraciones o posturas poco naturales: no es una configuración final.

Para probar, sal de Play, espera a que Unity importe los cambios y vuelve a ejecutar. Haz clic en Game
para capturar el cursor si hace falta; mantén el botón derecho un segundo, suelta y repite.
El efecto es transitorio, no un temblor continuo mientras mantienes Aim. En el Inspector del controlador,
**Aim de entrada** y los offsets en vivo permiten comprobar si llegan los cambios de estado y el impulso.
La medición automatizada no verifica por sí sola la sensación visual, los brazos del personaje o el ratón físico.

## FOV y temblor breve de cámara al apuntar

El perfil incluye un canal separado **Aim Camera**. Lo aplica `CameraRigController` a la cámara real,
no `WeaponPoseController` sobre sus transforms. No hace falta un componente extra ni modificar la jerarquía.

- **Aim Effects Source**, en CameraRigController: referencia a tu WeaponPoseController.
  Si está vacío, Awake busca uno entre los hijos del rig. Con varias armas, asigna explícitamente
  el controlador del arma equipada; no se implementa aquí un sistema de equipamiento.
- **Enabled**, en Aim Camera del perfil: activa FOV y los efectos angulares configurados.
- **Aim Fov Offset = 3:** añade 3 grados al FOV base. Con base 60, Aim llega a 63 y vuelve a 60.
  Un valor positivo abre el encuadre; para zoom usa uno negativo, por ejemplo `-5`.
- **Fov Blend Speed = 12:** transición exponencial solo en modo independiente. Cero aplica el destino inmediatamente.
- **Shake Enabled:** activa el resorte angular breve al entrar o salir de Aim; no genera un
  temblor continuo al mantener el botón. No añade impulso al aparecer/reactivar la cámara.
- **Frequency = 8**, **Damping = 0.65**, **Max Rotation Offset = 0.35 grados**.
- **Aim Rotation Kick = (-0.25, 0.08, 0.04)** y **Idle Rotation Kick = (0.12, -0.04, -0.02)**.
- **Apply In Third Person:** desactivado inicialmente; al pasar a TPS, el FOV adicional vuelve a cero.

En el AK12 se activó este canal y se devolvió el rebote del arma al preset suave. Idle, Aim, su
duración y los grips no se recapturan. La cámara conserva su posición/seguimiento existente:
solo el FOV y su orientación visual final reciben el nuevo efecto. Una pequeña oscilación de mira
durante el impulso es esperable; debe estabilizarse al mantener Aim.

Se requiere una **Real Camera distinta del rig/holder** para aplicar el temblor sin arrastrar el arma.
Las jerarquías antiguas con cámara y holder compartidos conservan el FOV pero omiten este temblor.

El FOV Aim es una capa sobre el FOV base. `UpdateTargetFOV` sigue estableciendo el destino de la base
y el canal Aim se suma después, sin acumularse cada frame. Al salir de Aim vuelve a la base actual,
no a un número fijo impuesto por el arma. Recoil y `SetAdditivePose` conservan sus propios canales.
No añadas un segundo script que escriba la cámara después del rig.

Orden: entrada del jugador -> **cámara 500** (lee IsAiming actual) -> **arma 550** -> **manos 600**.
La rotación visual no se realimenta al ratón, columna ni orientación base del holder.
Deshabilitar el rig retira sus offsets Aim si no fueron reemplazados por otro escritor; deshabilitar
el perfil/fuente retira el temblor y mezcla el FOV adicional hacia cero.

`Tools > CodeHelper > Run Camera Aim Checks` ejecuta comprobaciones aisladas; también se incluye
en `Run All Character Checks`. La comodidad visual, sensibilidad y encuadre con el personaje deben
comprobarse en Play Mode. Empieza con intensidades bajas; estos valores son un punto de partida.
Con los ajustes suaves iniciales, la comprobación aislada midió aproximadamente **0.126 grados** de pico
angular de cámara, sin traslación. El perfil del arma permanece intacto durante la ejecución.

### Cámara: rebote de diagnóstico muy visible

Por solicitud del usuario, el AK12 utiliza temporalmente estos valores en **Aim Camera**:

| Parámetro | Suave inicial | Diagnóstico visible |
|---|---|---|
| Frequency | 8 | 2 |
| Damping | 0.65 | 0.15 |
| Aim Rotation Kick | `(-0.25, 0.08, 0.04)` | `(-2, 0.5, 1)` |
| Idle Rotation Kick | `(0.12, -0.04, -0.02)` | `(1.5, -0.4, -0.75)` |
| Max Rotation Offset | 0.35 grados | 2 grados |

Es una oscilación más lenta y menos amortiguada, solo al cambiar el estado. Vuelve a cero al mantener
Aim; no es temblor continuo. Para regresar al ajuste suave cambia únicamente estas cinco propiedades.
Los botones de preset de **Aim Motion** afectan al arma, no a estos valores de cámara.

Se conservaron los ajustes del usuario **Aim Fov Offset = -30** y **Apply In Third Person = true**:
con base 60, ese FOV de apuntado es 30, no 63. No se modificaron las poses ni el rebote suave del arma.
El movimiento de diagnóstico puede resultar incómodo o marear, especialmente con ese zoom.
Úsalo para comprobar el efecto y después reduce la intensidad. No añade traslación de cámara.

## Guardado y Play Mode

- Crear el perfil y previsualizar solo están disponibles **fuera de Play**.
- Los botones de captura sí permiten actualizar un perfil existente **durante Play**, con confirmación.
  Guardan el **asset**, por lo que esa captura sobrevive al salir de Play.
- Esto NO conserva cambios temporales en las referencias del componente ni en la jerarquía/escena.
- Capturar desde Play excluye el rebote propio si el ancla sigue mostrando el resultado del controlador.
  Sigue capturando la base interpolada actual (no necesariamente el extremo Idle/Aim), y puede incluir
  modificaciones de otros sistemas. Para la primera configuración, se recomienda hacerlo fuera de Play.
- El controlador nunca sobrescribe el perfil por sí solo durante el juego.
- Un mismo asset afecta a todas las armas/componentes que lo compartan. Crea un perfil por configuración.
- Si cambias el padre del ancla, las coordenadas anteriores significarán otra cosa: revisa y recaptura
  explícitamente. Si modificas la jerarquía en runtime, llama `ValidateSetup()`.

## Entrada externa

Desactiva **Use Look Controller** y llama:

```csharp
weaponPoseController.SetAiming(true);  // Aim
weaponPoseController.SetAiming(false); // Idle
```

Publica la entrada antes de **WeaponPoseController.Update (550)**, por ejemplo desde un Update
con orden menor. Asignar otro perfil cambia los datos de destino;
una pose no capturada conserva la colocación actual. Al desactivar el componente deja de escribir
y conserva la última pose base, retirando el rebote propio, sin restaurar otra pose ni cambiar el asset.

## Comprobaciones

`Tools > CodeHelper > Run Weapon Pose Checks` ejecuta comprobaciones aisladas del controlador y
del guardado/previsualización. Crea objetos temporales y un asset de prueba con nombre único,
que elimina al terminar. También se incluye en `Run All Character Checks`.

Estas comprobaciones no sustituyen la alineación visual de la mira, el contacto de las manos,
la ausencia de penetraciones, la sensación del rebote ni la validación FPS/TPS con tu personaje real.
