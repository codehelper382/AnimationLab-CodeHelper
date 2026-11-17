# Brazos editables por equipo y acción

## Ejemplo listo: puños

En `SampleScene`:

```text
Camera Holder
├── Weapon Holder                  ← AK12 existente, sin recolocar
└── Fists Hand Targets
    ├── Left Guard                 ← posición/rotación de guardia izquierda
    ├── Left Action Target         ← destino disponible para una acción izquierda
    ├── Right Guard                ← posición/rotación de guardia derecha
    └── Right Action Target        ← destino del golpe derecho de ejemplo
```

1. Recarga la escena del disco si Unity tenía una versión anterior abierta. Conserva por separado
   cualquier cambio tuyo sin guardar antes de recargar.
2. Mueve/rota **Left Guard** y **Right Guard** para colocar las muñecas. No muevas los huesos.
3. Mueve **Right Action Target** para ajustar dónde llega el golpe. En el ejemplo está 16 cm más
   adelante que la guardia, en Z de la vista: el recorrido de la muñeca es recto y vuelve al inicio.
4. Guarda la escena fuera de Play. En Play, pulsa **2** y botón izquierdo para comprobar el resultado.

Los ajustes de Transform hechos durante Play se pierden al salir. Para conservar una prueba,
copia los valores de los puntos y pégalos fuera de Play antes de guardar; no se guardan automáticamente.
El ejemplo conserva la orientación/posición de guardia obtenida del clip original como punto de
partida, no como una guardia de boxeo perfectamente calibrada.

Los campos están en **Player → Equipment Scene Controller → Slots → Fists → Left/Right Hand Pose**:

| Campo | Función |
|---|---|
| Enabled | Activa el control de esa mano |
| Guard | Referencia editable de la muñeca en reposo |
| Action Target | Destino opcional de la acción |
| Elbow Hint | Polo opcional para orientar el codo, siempre fuera de los brazos |
| Weight | Mezcla de IK; el ejemplo usa 1 |

El inspector del controlador también permite elegir un slot, crear los puntos que falten y
seleccionar los de guardia/acción. No sobrescribe referencias existentes. Con el controlador
seleccionado y Gizmos visibles, Scene muestra etiquetas y líneas entre los puntos.

## Recorridos por acción

Expande `Fists_Definition` en Project y selecciona **Punch → Right Hand Motion**:

- **Enabled**: permite que la acción mueva esa mano; desactivado mantiene su guardia.
- **Reach**: 0 = Guard, 1 = Action Target. Eje X = progreso normalizado de la acción.
- **Position**: desplazamiento adicional en metros, en los ejes locales de Guard.
- **Rotation**: rotación adicional en grados, en los ejes locales de Guard.
- **Motion Clip**: opcional, Generic con curvas de posición/rotación en la ruta raíz. Se combina
  con las curvas, muestreado en un Empty temporal que empieza en posición cero/rotación identidad.

El golpe de ejemplo usa Reach `0→0`, `0.4→1`, `1→0`: llega al destino cuando ocurre el contacto
de prueba (`Commit Time = 0.4`). La izquierda mantiene su guardia. No se cambió la duración original.
Las dos manos tienen recetas independientes en cualquier acción, no solo golpes de puños.

La mezcla Blend In/Out de la acción también atenúa el movimiento para entrar/salir. Conserva
Reach y offsets en cero en los extremos cuando quieras regresar sin saltos. Las curvas usan el
reloj de la acción; un clip de mano se evalúa con ese mismo progreso normalizado.

### Dedos e IK/FK

Character Clip conserva los dedos. Con IK completo, el único grafo existente crea máscaras
privadas que excluyen solo los brazos controlados: el clip no compite con la posición de muñeca.
No se modifican las máscaras originales ni se añade otro grafo sobre el personaje.

Si una mano no tiene un Guard válido o está desactivada, conserva el grip/FK anterior.
Con peso parcial/cero, se conserva la animación FK como fuente de mezcla; las curvas Left/Right IK
de la acción siguen funcionando. Para mezclar pesos cerca de 1, asigna **Elbow Hint** si necesitas
un plano de codo estable: sin él, cambiar de fuente animada a IK completo puede cambiar la flexión.

Las muñecas respetan los offsets existentes de Hand IK Solver. En este ejemplo son cero.
Los hombros no se mueven ni los huesos se estiran: un objetivo fuera del alcance físico no se
alcanzará aunque la referencia de trayectoria sea exacta. En las muestras de validación hay una
pose limitada por alcance; ajusta los puntos según tu personaje.

## Reutilizar con otros equipos

El cuchillo ya tiene un ejemplo basado en su animación original, sin dibujar manualmente el tajo:
[AUTHORED_KNIFE_GUIDE.md](AUTHORED_KNIFE_GUIDE.md). Sus ajustes de Guard se suman al rig animado.

Cada slot tiene sus propios puntos y cada acción sus curvas. Los datos compartidos no guardan
progreso de reproducción, munición ni referencias a objetos de la escena.

| Equipo | Ejemplo de autoría de manos |
|---|---|
| Cuchillo/espada | Posición y rotación para un tajo o una estocada |
| Hacha/martillo | Curvas de subida, arco descendente y recuperación |
| Escudo | Mano izquierda en guardia mientras la derecha hace otra acción |
| Arco | Mano de apoyo fija y otra con recorrido de tensado |

Esto es una **base de presentación de manos**, no la implementación de combate de todos esos
equipos. Bloqueo, cuerda, proyectiles, colisiones, impactos y daño necesitan lógica y recursos propios.
Un modelo de arma tampoco pasa a seguir automáticamente una mano: conserva un ancla independiente
y coordina sus curvas Weapon Position/Rotation o Weapon Clip con los puntos. Para agarrar un arma
que se mueve, puedes colocar los puntos bajo su ancla independiente en lugar de bajo la cámara.
Nunca pongas estos objetivos bajo una mano/brazo resuelto por IK: crearía un ciclo.

## Validación

`EditableHandTargetsFullValidation.log`, en `C:\Users\eduar\AppData\Local\Temp\opencode`, registra
compilación Runtime/Editor, todas las regresiones y la escena real en LateUpdate/AnimationJob:

- Guardia editable, rotación y recta de golpe/retorno con el mismo reloj de contacto.
- Mirada 0/±45/±90 y evaluaciones continuas a 30/60/144 Hz con cámara en movimiento.
- Curvas, clip Generic local, rotación con destino rotado y cancelación.
- Mano individual, pesos cero/parciales y máscaras originales intactas.
- Rechazo de referencias bajo brazos, sin deriva con reloj fijo ni escrituras sobre puntos guardados.
- Cambios a AK12 y estabilidad de su recarga, sin cambiar sus datos ni las poses Idle/Aim.

`EditableHandTargetsAuthoringValidation.log` añade las pruebas del inspector: crear solo los puntos
que faltan, conservar referencias/pesos/hints, repetición sin duplicados y Undo/Redo completo.

Los tests usan definiciones/acciones clonadas y una escena aislada. El error medido de muñeca
por encima del límite físico fue 0.002 mm; no sustituye comprobar encuadre y comodidad en Play.
Revisión independiente completada; la continuidad del codo al variar pesos sin hint es un límite
documentado, no una promesa de mezcla perfecta para cualquier rig.
