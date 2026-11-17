# Mirada, cámara y jerarquía

`PlayerLookController` controla entrada y yaw del Player. `CameraRigController` coloca la cámara
después de animación. `ProceduralSpine` mueve el cuerpo; `HandIKSolver` resuelve los brazos al final.
No requiere KINEMATION ni un InputManager propio. El proyecto usa el Input System oficial de Unity.

## Jerarquía recomendada

```text
Player                              <- PlayerLookController, ProceduralSpine, HandIKSolver
  [PelvisTilt opcional]
  Character                         <- Animator; modelo local sin yaw adicional
    Esqueleto (conservar jerarquía original)
      ... Pelvis / Columna / Cuello / Head
        CameraHolder                <- CameraRigController
          MainCamera                <- Camera + un solo AudioListener
          WeaponAnchor              <- GameObject vacío, no necesita script
            Arma
              RightGrip
              LeftGrip
      ... brazos y manos originales
  RightElbowHint
  LeftElbowHint
```

No poner PlayerLookController/CameraRigController en Head ni rotar Character por segunda vez.
Player debe ser una raíz vertical (yaw), fuera de la jerarquía animada. Otro controlador que
rote Player debe coordinarse con `blockBodyRotation`; no tener dos dueños del yaw.
Esto NO incorpora desplazamiento WASD: conserva/añade por separado tu controlador de movimiento.

## Referencias del Inspector

### PlayerLookController en Player

- Body Root: **Player**.
- Spine Controller: **ProceduralSpine de Player**.
- Camera Rig: **CameraRigController de CameraHolder**.
- Input Mode: **Mouse And Keyboard**.
- Mouse Sensitivity: comenzar con **0.1** (grados por píxel con Input System).
  Si el componente ya tenía 2 del script anterior, Unity conserva ese valor: ajustarlo manualmente.
- Input Smoothness: **0** para respuesta directa; por ejemplo 0.03 para suavizar.
- Lock Cursor On Enable: activo. Soft Limit: 50, Hard Limit: 80 como punto de partida.

### ProceduralSpine en Player

- Animator: **Character / Animator**.
- Animation Root: **Player**.
- Profile: perfil propio o vacío para Humanoid. Mode: empezar en **LateUpdate**.
- Input Smooth Time: empezar en 0 para no añadir doble suavizado.
- El pitch y lean de prueba quedan desactivados para que mande el ratón.

### CameraRigController en CameraHolder

- Look Controller: **PlayerLookController de Player**.
- Character Root: **Player**.
- Head Bone: **Head real del esqueleto**.
- Real Camera: **MainCamera**.
- Camera Holder: el propio **CameraHolder**.
- Stabilization Bone: vacío si Head está asignado.
- Yaw Relative To Root: desactivado; Use Spine Lean: desactivado. Connect Camera lo configura.
- Position Source: **HeadStabilized** para seguir la flexión/altura de cabeza compensando el yaw del cuerpo.
- Procedural Spine: asignar explícitamente tu componente, aunque esté en Character y no en Player.
- Capture Head Position / Root Anchor Offset: solo afectan a **PlayerRoot**, no a HeadStabilized.
- Offset: **(0,0,0)** si no quieres un desplazamiento orbital al mirar con el ratón.
- Smooth Head Position: afecta a Head y HeadStabilized; empezar desactivado para comprobar el seguimiento.
  PlayerRoot sigue los desplazamientos reales de Player sin ese suavizado.
- Collision Layers: entorno, EXCLUYENDO los colliders del Player; solo se usan en TPS.

El CameraHolder puede seguir siendo hijo de Head: su pose mundial final se corrige en LateUpdate.
No hace falta cambiar la jerarquía. El arma bajo el holder y sus grips se estabilizan con él.

**Alternativa fija:** PlayerRoot captura una vez la separación inicial de ojos si Capture Head Position
está activado. No sigue la cabeza al mirar arriba/abajo, ni copia agacharse, respiración o lean
de posición. Para ajustar altura de forma intencional, desactivar Capture Head Position y modificar
Root Anchor Offset (por ejemplo, Y=1.65 de pie / Y=1.1 agachado, según el personaje).
`Recalibrate Camera Anchor` vuelve a tomar la separación de ojos actual; usarlo al cambiar de avatar
o redefinir la pose inicial, NO cada frame ni al hacer turn-in-place. Para teletransporte normal basta
ResetPositionSmoothing: no recaptura una cabeza que pueda estar desplazada por una animación.

El modo **Head** conserva el comportamiento anterior de seguimiento de cabeza, con suavizado opcional.
El modo **HeadStabilized** evalúa una cabeza virtual sin modificar huesos: conserva segmentos y postura,
pero sustituye el rumbo del cuerpo por el de la vista y separa el pitch/roll procedural del giro.
Por tanto NO coincide exactamente con la cabeza física mientras torso y mirada tienen distinto yaw.
En FPS puede ser necesario ocultar la malla de cabeza para esa cámara (manteniéndola visible en TPS).
No elimina las traslaciones arbitrarias que traiga un clip de giro; tampoco es un sistema anticolisión FPS.
Para aislamiento de giro, mantener Yaw Relative To Root desactivado y usar el yaw mundial de PlayerLook.
Root motion que traslade físicamente Player se seguirá viendo: desactiva Apply Root Motion si tu
animación de giro debe ser realmente in-place. Ningún otro script debe reescribir cámara/holder después.

### HandIKSolver en Player

- Animator: Character. Auto-Configure Humanoid o huesos manuales.
- Targets: RightGrip y LeftGrip del arma, con rotación de muñecas correcta.
- Hints: objetos de codo fuera de los brazos. Ver [HAND_IK_GUIDE.md](HAND_IK_GUIDE.md).
- Arma bajo WeaponAnchor/CameraHolder, no bajo la mano que intenta seguirla mediante IK.

## Controles incorporados

| Entrada | Acción |
|---|---|
| Ratón | Mirada horizontal y vertical |
| Q / E sostenidos | Inclinar izquierda / derecha |
| Botón derecho | Publica IsAiming y acelera el seguimiento del cuerpo |
| V | Cambiar FPS/TPS |
| Escape | Liberar cursor |
| Clic izquierdo con cursor libre | Capturar cursor de nuevo (sin aplicar ese frame de ratón) |

El botón derecho NO implementa ADS del arma ni cambia automáticamente FOV. Eso corresponde a tu
sistema de armas; puedes llamar CameraRig.UpdateTargetFOV. Esta clase tampoco dispara ni mueve al Player.
Haz clic en Game y prueba Play Mode. Con el cursor libre no se procesa el ratón.

## Signos, giro e integración

- CameraPitch positivo = arriba; CameraYaw = rumbo mundial; lean negativo Q, positivo E.
- La cámara consume estos datos a través de la interfaz, sin leer su rotación final como input.
- La columna recibe yaw relativo al Player, pitch negativo y lean negativo respecto a la entrada
  de cámara: sus ejes de aplicación actuales son distintos. Esta conversión evita mirar en
  direcciones contrarias. También se suministra TurnOffset para ambos modos de yaw de columna.
  En el perfil de columna, el límite de pitch Y corresponde a arriba y X a abajo.
- Se quitó el suavizado extra oculto del pitch de columna; usar Input Smooth Time en ProceduralSpine.
- El root empieza a seguir la mirada al superar el límite suave; el duro impide seguir retorciendo
  el torso. `blockBodyRotation=true` evita que esta clase gire Player.
- `TurnAngle` es opcional. Si el Animator no tiene un parámetro float con ese nombre se omite.
  En modo AnimationJob se envía mediante ProceduralSpine.SetFloat para llegar al controlador correcto.

Orden: PlayerLook.Update (-50) -> datos de columna (200) -> Animator -> columna LateUpdate (200)
-> arma (400) -> cámara (500) -> manos (600). No modificar cabeza/cámara después del paso final.

## Entrada externa, mando o tu sistema de input

Seleccionar **External** y llamar ANTES del Update de PlayerLook (-50), por ejemplo desde un
componente con DefaultExecutionOrder(-100):

```csharp
look.SetInput(mouseDelta, leanAxis, aiming);
```

El delta se consume una sola vez. El lean y aiming conservan el último valor recibido.
Para un stick, convertir velocidad angular a delta según deltaTime antes de enviarlo y ajustar
sensibilidad; no interpretar un stick sostenido como píxeles sin tener en cuenta el tiempo.
Para fijar mirada absoluta tras spawn/teletransporte: `SetLookAngles(pitchUp, worldYaw, alignBody)`.
Llamar también CameraRig.ResetPositionSmoothing si cambias posición.
`isTPS` se puede cambiar por código; `inputEnabled=false` bloquea controles sin eliminar el seguimiento.

La API anterior `AddRecoilInput(vertical,horizontal)` conserva su signo: vertical positivo REDUCE
CameraPitch (mira abajo); para empujar hacia arriba usar vertical negativo. No sumar también el
mismo impulso en CameraRig si no quieres duplicar el retroceso.

Si no hay Input System, existe fallback para el Input Manager clásico (ejes Mouse X/Mouse Y;
la sensibilidad se debe reajustar porque esas unidades no son píxeles). Sin ninguno de los dos,
compila y admite External pero no leerá teclado/ratón por sí solo.

## Verificación

`Tools > CodeHelper > Run Player Look Checks` valida interfaz, signos, yaw relativo, límites,
TPS override, recoil y consumo de entrada. No simula ratón físico ni prueba las transiciones de tu rig.
`Tools > CodeHelper > Run All Character Checks` ejecuta también las comprobaciones de columna/cámara y manos.
Incluye seguimiento vertical y postura del ancla estabilizada y aislamiento del giro con pelvis inclinada.
Comprobar en Play Mode la sensibilidad, foco/cursor, inclinación, V, límites del torso y recargas.
