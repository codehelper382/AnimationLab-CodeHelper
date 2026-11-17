# Cuchillo con movimiento original, sin componentes de KINEMATION

## Probar

1. Si la escena estaba abierta antes de estos cambios, conserva aparte lo que tengas sin guardar y
   recarga `Scenes/SampleScene.unity` desde disco. Espera a que Unity importe los nuevos recursos.
2. Entra en Play: **1** AK12, **2** puños, **3** cuchillo.
3. Botón izquierdo ejecuta el ataque. Mirar arriba/abajo orienta el conjunto hacia la vista.
4. El cambio de equipo espera a que termine la acción actual.

Esto reproduce presentación y un hito de contacto de prueba. **Todavía no aplica daño, impactos,
sonido ni colisiones de la hoja.** Equipar/guardar duran 0.25 s, sin un nuevo clip de entrada/salida.

## Qué se reutilizó

- Movimiento fuente: `kindemo/Demo 1/Meshes/Weapons/Knife/C_Knife_Pose.anim` y `C_Knife_Attack.anim`.
- Pose de arma original del clip (`Skeleton/WeaponBone`), manos y codos muestreados en edición.
- Malla/materiales del cuchillo que acompaña al FBX original del ataque.
- Copia propia de `C_Knife_Attack.fbx`, convertida a Humanoid para reproducir los dedos en nuestro grafo.

El ataque conserva sus **1.4333 segundos**. Se hornea a **60 Hz** un clip Generic con arma, manos
y polos de codo en una jerarquía coordinada; no se inventa un tajo con dos puntos de interpolación.
La posición es relativa a los ojos neutros del rig fuente, con escala uniforme según la longitud
del brazo del personaje. El IK adapta las muñecas al esqueleto real.

La pose de dedos en reposo se congela del primer frame del mismo ataque. No se usa una pose de
locomoción con otro avatar para decidir la posición de las manos: esa mezcla puede colocar el arma
detrás de la cámara.

## Recursos y configuración

Configuración del cuchillo en `CODEHELPER/EquipmentAssets/Melee/Knife`:

| Recurso | Función |
|---|---|
| `Knife_Definition.asset` | Equipo melee y acciones propias |
| `Knife_AttackRig.anim` | Movimiento coordinado de arma, manos y codos |
| `Knife_Pose.anim` | Pose Humanoid de dedos congelada |
| `Humanoid/Knife_Attack.fbx` | Copia propia de la animación para los dedos |
| `Knife_Presentation.prefab` | Malla y puntos nativos de Unity; ningún MonoBehaviour de terceros |
| `Knife_Bake.json` | Fuente, tiempo, frecuencia, escala y poses de referencia del horneado |

En **Knife Attack**:

- **Character Clip** reproduce los dedos; los brazos controlados por IK se excluyen en máscaras privadas.
- **Weapon Clip** reproduce `Knife_AttackRig` sobre el visual independiente del cuchillo.
- **Blend Weapon Clip**, en su slot de escena, permite entrar/salir con el mismo Blend In/Out de
  la acción: 0.15 s. El AK12 conserva el comportamiento anterior, sin activar esta opción.
- **Commit Time = 0.5** es el hito de contacto de prueba, no una detección automática de impacto.
- **Attack Interval = 1** limita el uso según el reloj de contactos del sistema propio; también
  se debe completar la acción antes de empezar otra.

Todos los canales se evalúan con el mismo progreso. Primero se mueve el rig visual y luego se
resuelven las manos contra sus puntos. La jerarquía conserva el agarre durante la mezcla: no se
interpolan arma y mano por separado en coordenadas del mundo.

## Ajustar la posición

```text
Camera Holder
└── Knife_Presentation                ← coloca todo el conjunto respecto a la vista
    └── Weapon Motion                ← animado: no usar como ajuste permanente
        ├── Blade                    ← malla, pose local fija respecto al arma
        ├── Left Motion              ← animado
        │   └── Left Guard           ← ajuste local de la muñeca izquierda
        ├── Right Motion             ← animado
        │   └── Right Guard          ← ajuste local de la muñeca derecha
        ├── Left Elbow               ← animado
        │   └── Left Elbow Hint      ← ajuste de orientación del codo
        └── Right Elbow              ← animado
            └── Right Elbow Hint
```

Mueve **Knife_Presentation** fuera de Play para desplazar arma y recorrido juntos. Los `Guard`
añaden un ajuste sobre el movimiento original: no quedan sobrescritos por la animación. Cambiar
solo un Guard no mueve la hoja, por lo que puede separar el agarre; úsalo para calibraciones pequeñas.
Para cambiar la relación de la malla con la mano, ajusta **Blade** o edita el movimiento coordinado.

Guarda los ajustes fuera de Play. No coloques el arma ni sus objetivos bajo los huesos resueltos
por IK: el rig visual ya está en un ancla independiente. Los huesos no se estiran; alejar mucho el
conjunto puede superar el alcance del brazo.

### Calibración de agarre indicada por el usuario

Se conservó la pose de la captura: posición **(-0.0222, -0.0171, -0.0773)** y rotación
**(-90, 0, 0)** respecto a `Weapon Motion`, con escala 1. Se convirtió a un offset en **Right Guard**
en vez de modificar `Right Motion`, porque este último está animado y sobrescribiría el ajuste.
Por esa conversión, los números locales de Right Guard son distintos: la composición de ambos
Transforms da exactamente la pose indicada en reposo y mantiene el ajuste durante el ataque.

Si todavía conservas en el editor el ajuste manual sin guardar de **Right Motion**, sal de Play
y recarga la escena guardada sin guardar ese override. Conserva aparte otros cambios que necesites.
Así no se aplica dos veces la corrección. Para retoques posteriores utiliza **Right Guard**.

`KnifeUserGripCalibrationValidation.log` registra la suite completa con el offset: ambos modos,
30/60/144 Hz, regreso/cancelación y ninguna muestra limitada por alcance. El control del clip fuente
incluye ahora el offset artístico. Se comprobó por composición que la pose final coincide con la
captura y por secciones que solo cambió Right Guard; escena, clip y runtime intactos. Revisión
independiente sin hallazgos. La confirmación estética en el editor del usuario sigue siendo externa.

## Independencia y validación

El runtime sigue siendo exclusivamente `CODEHELPER`. Los recursos originales se leen en edición;
no se añadieron `FPSAnimator`, `MeleeWeapon` ni capas de KINEMATION al personaje o al cuchillo.
Se conserva un solo grafo de personaje. El bake es explícito, no se regenera automáticamente al
importar y se niega a sobrescribir una carpeta existente. `KnifeAnimationSetup.Configure` es una
utilidad de autoría para el proyecto aislado, no un botón para reemplazar tu escena de trabajo.

`AuthoredKnifeFullValidation.log`, en `C:\Users\eduar\AppData\Local\Temp\opencode`, registra
compilación Runtime/Editor y todas las regresiones sobre la escena desplegada:

- Arma/manos/codos con el mismo reloj y mezcla, pose de dedos igual a reproducción nativa.
- 35 muestras de ataque a 0/±45/±90 en LateUpdate y AnimationJob; ninguna limitada por alcance.
- Regreso a guardia, cancelación, desactivar/reactivar y cambios a los equipos anteriores.
- Evaluación continua a 30/60/144 Hz, sin deriva con reloj fijo y sin sobrescribir ajustes de usuario.
- El root independiente se conserva; el visual no contiene componentes de terceros.
- Regresiones de cámara, poses, puños, munición y estabilidad de la recarga del AK12.

Error medido contra el rig horneado: 0.000 mm; diferencia máxima de agarre frente a interpolación
del original entre sus keys: 0.568 mm; residuo IK adicional al límite físico: 0.003 mm. La revisión
independiente no encontró bloqueos. Estas medidas no sustituyen comprobar la apariencia y el
contacto visual en tu Unity abierto.

Respaldo anterior: `C:\Users\eduar\AppData\Local\Temp\opencode\before-authored-knife-20261110-181138`.
Se verificó por secciones que solo se añadieron el tercer slot y el nuevo visual bajo Camera Holder;
los recursos AK12/puños y sus poses conservan exactamente su contenido anterior.
