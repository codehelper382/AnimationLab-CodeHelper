# Columna y cámara: configuración

Estos scripts son independientes de KINEMATION. No colocar dos controladores de mirada/cámara
sobre los mismos objetos. Las optimizaciones no implican una ventaja de rendimiento medida.

## Cámara hija de la cabeza

Jerarquía recomendada (no poner CameraRigController en el root ni en un hueso):

```text
PlayerRoot                 <- Character Root; referencia de orientación sin animación de cabeza
  Model / Animator
    ... Head               <- Head Bone
        CameraRig          <- CameraRigController
          Holder           <- Camera Holder (opcional, puede contener objetos visuales FPS)
            MainCamera     <- Real Camera
```

También admite el componente directamente en MainCamera, con Camera Holder vacío y Real Camera
apuntando a sí misma. Para separar efectos de cámara de armas, usar CameraRig/Holder/Camera distintos.
No colocar huesos del personaje dentro del rig; evitar escalas no uniformes en sus ancestros.
El script no cambia padres ni crea objetos en la escena.

1. Asignar Head Bone, Character Root y Real Camera. Asignar Holder si existe.
2. Ejecutar `Validate Setup` desde el menú del componente. No seguir hasta corregir las advertencias.
3. Alimentar la mirada desde el código del jugador ANTES del LateUpdate de cámara.
4. Para seguir la cabeza al mirar arriba/abajo compensando el turn-in-place, usar
   `Position Source = HeadStabilized`, asignar `Procedural Spine`, `Character Root = Player`,
   `Yaw Relative To Root = false`, `Use Spine Lean = false` y `Offset = (0,0,0)`.
   Empezar con `Smooth Head Position` desactivado; el suavizado opcional retrasa la posición, no el ratón.
   Se evalúa una cabeza virtual que conserva flexión/postura pero sustituye el yaw corporal por el visual.
   No coincide exactamente con el hueso físico cuando el torso mira en otra dirección. Ocultar la malla
   de cabeza en FPS si entra en la vista; no corrige penetración ni elimina traslación arbitraria de clips.
   Alternativas: `Head` copia la cabeza física (incluye desplazamiento del giro); `PlayerRoot` mantiene
   una separación fija de ojos y NO sigue cabeza/pitch/postura. En PlayerRoot, `Capture Head Position`
   captura esa separación una vez; este ajuste no afecta a HeadStabilized.
5. En TPS, excluir del Collision Layers todos los colliders del propio personaje. Se ignoran triggers.
   Un origen solapado bloquea el brazo en el origen: resolver el solapamiento/capas del escenario.

### Entradas y migración de PlayerLookController

`PlayerLookController.cs` ahora implementa `ICameraLookSource`, procesa el ratón/teclado sin
InputManager externo y alimenta también ProceduralSpine. Asignarlo en CameraRig > Look Controller,
o usar `Connect Camera` en el componente de mirada. Este enlace desactiva Yaw Relative To Root y
Use Spine Lean porque el controlador publica yaw mundial y lean de cámara sin convertir.
Para la jerarquía completa y controles ver [PLAYER_LOOK_GUIDE.md](PLAYER_LOOK_GUIDE.md).

Opción sencilla: dejar Look Controller vacío y llamar desde el controlador de jugador:

```csharp
// pitchUp: grados positivos hacia arriba. yawWorld: rumbo MUNDIAL, no delta del ratón.
cameraRig.SetLookInputs(pitchUp, yawWorld, leanDegrees, thirdPerson);
// Por separado, enviar al cuerpo su yaw RELATIVO al root, no necesariamente yawWorld.
// El solver actual usa +X hacia abajo y +Z opuesto al lean de cámara:
spine.SetLookInputs(-pitchUp, bodyYawOffset, -leanDegrees, turnOffset);
```

Alternativa: implementar `ICameraLookSource` en el controlador existente y asignarlo a Look Controller.
Las propiedades requeridas son CameraPitch (positivo arriba), CameraYaw (rumbo, grados), CameraLean
e IsThirdPerson. No leer la cámara final para calcular su propio input: produciría realimentación.
La cámara no captura ratón directamente ni envía inputs automáticamente a la columna.

`Yaw Relative To Root` permite usar yaw relativo a Character Root (root.rotation × mirada local).
No activarlo si el yaw enviado ya contiene el giro mundial del personaje: se duplicaría.
`Use Spine Lean` conserva el comportamiento anterior de lean; desactivarlo para usar el lean de
SetLookInputs/ICameraLookSource. La cámara nunca usa el pitch suavizado de la columna.

El pivote ya no sigue el pecho para luego cancelar ese movimiento al recolocar el holder:
ahora se coloca directamente en el ancla final. Revisar cualquier otro script que use ese pivote.
Stabilization Bone queda como posición alternativa si no se asigna Head Bone.

### Efectos explícitos, sin acumular transformaciones

- `AddCameraRecoil(Vector3)`: impulso Euler en grados, amortiguado exponencialmente.
- `SetAdditivePose(position, rotation)`: pose ABSOLUTA de efecto en espacio de vista, no delta acumulativo.
- `ResetCameraEffects()`: limpia efectos.
- `UpdateTargetFOV(float)`: transición de FOV si Real Camera tiene un componente Camera.
- `ResetPositionSmoothing()`: llamar tras teletransporte/respawn.
- `RecalibrateAnchor()`: recapturar la posición de ojos tras cambiar de avatar/pose inicial. No llamar durante cada giro.
- `ValidateSetup()`: llamar después de sustituir referencias o cambiar la jerarquía en runtime.

Los efectos solo afectan Real Camera si esta es distinta del holder. Un arma dentro del holder
no recibe automáticamente esos efectos; eso es intencional para permitir ajustes independientes.
No hay reproductor de clips de cámara ni IK de arma/manos en estos scripts.
El offset de posición aditivo no se comprueba contra colisiones TPS: usar cero o efectos pequeños.

## Columna

- Asignar `ProceduralSpine` al objeto del personaje y su Animator/Animation Root.
- Dejar Profile vacío para el humanoide por defecto o crear un `SpineLookProfile`.
- Los perfiles personalizados conservan sus límites; solo el fallback redistribuye el total entre
  los huesos humanoides que realmente existen. Los cambios de perfil requieren `SetProfile`/`Reinitialize`.
- LateUpdate es el modo inicial recomendado. Se restaura el resultado local del frame anterior
  antes del Animator para evitar acumular mirada cuando no anima esos huesos. Se respeta una
  escritura externa distinta del resultado propio; dos sistemas escribiendo el mismo hueso
  siguen necesitando coordinación. No se puede identificar la autoría de una rotación idéntica.
- AnimationJob mantiene su grafo propio y necesita un Animator con Controller. No combinarlo
  con KINEMATION/Timeline/otro dueño del mismo Animator. Usar los métodos spine.SetFloat/Bool/
  Integer/Trigger para enviar parámetros. Una reconstrucción intenta transferir los parámetros
  float/int/bool y el estado actual, pero NO clona transiciones activas ni triggers pendientes.
  Al desmontar el grafo, no se garantiza continuidad del estado del Animator original.
  No cambiar modo/perfil durante transiciones críticas. Esta limitación preexistente requiere
  una integración de grafo específica para eliminarla; no se promete equivalencia total entre modos.
- La raíz del job usa un scene handle: puede estar por encima del Animator. Los huesos del job
  deben pertenecer al Animator. La pelvis del job actualiza activación/ángulo y referencias en Update.
- HeadStabilized lee las fases completadas del job en LateUpdate. Si el Animator se pausa, conserva
  la última flexión y transporta las fases con la raíz; no puede generar nuevos movimientos de cabeza
  hasta que el grafo vuelva a evaluar. Una escritura externa de huesos invalida las fases afectadas.
- PelvisTilt compensa rotaciones de muslos; NO fija los pies. Para eso hace falta IK posterior.

## Orden de ejecución

```text
Update: input del juego -> datos de columna/job (200)
Evaluación Animator / AnimationStream
LateUpdate: columna (200) -> IK propio (300) -> arma (400) -> cámara (500)
```

300/400 solo afectan a componentes que usen esas constantes; no reordenan jobs del Animator.
Unity permite overrides en Project Settings > Script Execution Order: comprobar que no contradicen
los atributos. Nadie debe escribir la cabeza/cámara después de la estabilización final de cámara.
Si el arma depende del holder, asegurar un flujo sin ciclos: los scripts de arma pueden usar la
mirada como dato, no deben mover la cabeza basándose en la cámara del frame anterior.

## Verificación

Menú `Tools > CodeHelper > Run Spine and Camera Checks`: comprobaciones aisladas de límites,
restauración de pelvis (incluyendo escrituras externas), cámara hija de cabeza, ausencia de acumulación
sin Animator y un AnimationJob evaluado manualmente con raíz por encima del Animator. Crean y
eliminan objetos temporales; no configuran la escena. Se ejecutan manualmente; compilar no es ejecutarlas.

En Play Mode comprobar ambos modos con el rig real: idle/caminar/correr, mirar arriba/abajo,
girar el root, lean, alpha 0/0.5/1, Animator pausado, pelvis on/off, teletransporte, FPS/TPS,
paredes cercanas, colisiones propias y desactivar/reactivar componentes.

Para rendimiento, comparar en un Development Build con el mismo rig y efectos activos, con 1/10/50
personajes. Medir CPU, animación, GC Alloc y picos de cambio de perfil. Las nuevas rutas de frame no
crean listas/diccionarios; las búsquedas, validaciones de jerarquía y arrays se preparan al configurar.

## IK de manos opcional

`HandIKSolver.cs` usa el solucionador propio `HandIKMath.cs`, sin dependencias de KINEMATION.
Consultar [HAND_IK_GUIDE.md](HAND_IK_GUIDE.md). Su orden final es 600, DESPUÉS de cámara/holder (500),
para que los grips del arma bajo el holder se lean en el mismo frame. No sustituye ni cambia
automáticamente otros sistemas de IK existentes; hay que elegir un único dueño de los brazos.
