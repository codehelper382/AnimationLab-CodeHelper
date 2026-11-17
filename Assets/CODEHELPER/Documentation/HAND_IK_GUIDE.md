# Manos sobre un arma (IK propio e independiente)

`HandIKSolver.cs` usa **HandIKMath.cs**, nuestro solucionador analítico de dos huesos basado en
la ley de cosenos y un plano de flexión. Solo usa Unity; NO necesita KINEMATION, KAnimationCore,
Animation Rigging ni otro framework. No crea un PlayableGraph ni copia componentes de terceros.
Se mantienen los mismos campos del componente anterior, sin tener que reasignar huesos/grips.
La solución matemática y el plano del codo pueden verse algo distintos: revisar los hints en tu rig.

Para usar SOLO las manos en otro proyecto basta con copiar `HandIKSolver.cs`, `HandIKMath.cs`
y `ShooterExecutionOrder.cs`. La columna y la cámara son opcionales; no hace falta ningún paquete
adicional para el IK. Si copias también la columna, esta usa el paquete oficial Unity Collections;
no es una dependencia de KINEMATION.

## Configuración rápida

1. Añade **Hand IK Solver** al personaje (el objeto con ProceduralSpine es una buena ubicación).
2. Para Humanoid, asigna el Animator y usa el menú del componente
   **Auto-Configure Humanoid (missing bones only)**.
   Para Generic asigna Upper Arm / Forearm / Hand manualmente en cada lado.
   Se permiten huesos intermedios, pero el brazo debe ser una cadena jerárquica válida.
3. En el arma crea dos GameObjects vacíos: **RightGrip** y **LeftGrip**.
4. Colócalos donde deben estar las muñecas y orienta sus ejes como los huesos Hand de tu rig.
   Asigna los grips como Target de cada mano. Usa Rotation Offset Euler para corregir diferencias
   de ejes del modelo; una rotación cero universal no funciona para todos los rigs.
5. Crea opcionalmente RightElbowHint/LeftElbowHint fuera de los brazos y colócalos hacia donde
   deben doblarse los codos. Asigna Hint. Sin hints se aprovecha la flexión de la pose animada;
   brazos completamente rectos pueden necesitar hints para elegir un lado estable.
6. Ejecuta **Validate Setup** y comprueba los gizmos. Empieza con Alpha, Weight,
   Position Weight y Rotation Weight en 1.

Ejemplo de jerarquía sin bucles de dependencia:

```text
Personaje                    <- ProceduralSpine + HandIKSolver
  Esqueleto
    ... Cabeza
        IK WeaponBone        <- ancla independiente de los brazos; puede seguir la cabeza
          Arma
            RightGrip
            LeftGrip
        CameraRig            <- CameraRigController
          Holder
            Camera
    ... UpperArm -> Forearm -> Hand   (cada lado)
  RightElbowHint
  LeftElbowHint
```

El arma también puede estar bajo Holder: el solver corre DESPUÉS de CameraRigController para leer
los grips actualizados del mismo frame. Ningún brazo que se esté resolviendo debe ser ancestro del arma/grips/hints.

**No pongas el arma en la mano derecha y a la vez hagas que esa mano siga un grip de esa arma.**
Eso es un bucle. Para arma dirigida por la mano animada: deja **Right Target vacío** (sin IK derecha),
usa solo el grip izquierdo y deja la animación dueña del brazo derecho. Se permiten targets bajo
un brazo INACTIVO (Target vacío), pero no bajo un brazo declarado activo aunque su peso actual
sea cero, porque ese peso puede cambiar en runtime. La opción preferible es el arma sobre un ancla independiente.

## Peso, animaciones y cambio de arma

```csharp
// Reducir solo la izquierda para recargar; la animación controla esa mano cuando el peso es cero.
hands.leftHand.weight = 0f;

// Después de equipar otra arma: asignar primero ambos grips, luego validar una vez.
hands.rightHand.target = newRightGrip;
hands.leftHand.target = newLeftGrip;
hands.ValidateSetup();
```

Un Target vacío desactiva esa mano. La sustitución de referencias se detecta automáticamente.
Si REORDENAS padres sin cambiar referencias o cambias la escala, llama ValidateSetup.
No se estiran huesos: un grip fuera del alcance no puede alcanzarse exactamente.
Se requiere escala de esqueleto uniforme y positiva. Evita longitudes de segmento cero.
No asignar huesos de dedos a los campos Upper Arm/Forearm/Hand: Hand debe ser la muñeca real.

## Orden y compatibilidad

```text
Evaluación Animator / SpineLookJob
LateUpdate: ProceduralSpine 200 -> arma existente 400 -> CameraRigController 500 -> HandIKSolver 600
```

El nuevo orden 600 se llama `ShooterExecutionOrder.FinalHandIK`. La constante HandIK=300 queda intacta
para componentes anteriores. El objetivo es que cámara/holder terminen antes de resolver brazos.
No debe haber otro script moviendo el arma, la cabeza, el holder o los brazos después de este solver.
Si un script de attachment 400 lee una mano que se resolverá a 600, revisa la dependencia: no uses
ese attachment para dirigir simultáneamente el arma y el IK de esa misma mano.

Funciona después del modo LateUpdate O AnimationJob de tu columna, sin crear/modificar grafos.
No apiles el IkLayer de KINEMATION, Animation Rigging u otro IK sobre los mismos brazos.
Al iniciar cada Update se restaura la compensación local anterior que no haya sido sobrescrita
por otro sistema. Al desactivar se hace lo mismo, evitando poses residuales sin Animator.

## Qué incluye y qué NO

- IK de posición y rotación de ambas muñecas con pesos separados y guías de codo opcionales.
- Los grips como hijos del arma definen los objetivos relativos al arma sin ninguna capa adicional.
- No incluye poses de dedos ni muestreo de clips. Usa tus clips para dedos/recarga y baja pesos durante esas acciones.
- No incluye lógica de equipar, disparar, ADS ni posicionamiento automático de IK WeaponBone.
- Sin listas/arrays nuevos ni búsquedas de huesos por frame en la ruta normal; aun así hay que
  medir antes de comparar rendimiento con el framework original.

## Pruebas

Menú `Tools > CodeHelper > Run Hand IK Checks`. Usa brazos sintéticos para probar agarres,
pesos, cambios de objetivo, restauración sin Animator, objetivos inalcanzables, continuidad del
codo sin hint y conservación de una pose ya completamente plegada. El conjunto se ha ejecutado
en un proyecto temporal de Unity sin ensamblados de KINEMATION.
Después verifica Play Mode con tu personaje real: idle, caminar, mirar, lean, FPS/TPS, recargas
y cambios de arma. Las pruebas sintéticas no garantizan una postura estética para todos los rigs.

No se ha eliminado KINEMATION del proyecto original: sus propias demos y assets pueden seguir
necesitándolo. La independencia comprobada corresponde a estos scripts propios; no implica que
puedas borrar carpetas de terceros sin revisar primero las referencias de sus escenas/demos.
