# Equipos: definiciones y estado propio

Para colocar brazos y crear recorridos por mano desde Unity, consulta
[EDITABLE_HAND_TARGETS.md](EDITABLE_HAND_TARGETS.md). SampleScene incluye guardia y golpe derecho
recto de ejemplo; la misma base de poses puede reutilizarse con otros equipos sin implementar aún
su lógica de combate específica.

## Qué está implementado

SampleScene incluye además un cuchillo seleccionable con **3**, usando movimiento original
extraído para arma/manos/codos y dedos Humanoid, sin componentes KINEMATION. Véase
[AUTHORED_KNIFE_GUIDE.md](AUTHORED_KNIFE_GUIDE.md).

La base está en `CODEHELPER/Runtime/Equipment`, sin referencias a componentes de KINEMATION.
No sustituye automáticamente el AK12 de la escena ni modifica sus poses, cámara, Animator o IK.

| Recurso | Responsabilidad |
|---|---|
| `EquipmentDefinition` | Identidad, prefab opcional, pose de brazos/dedos, máscara, perfil Idle/Aim, pesos de agarre, equipar/guardar |
| `FirearmDefinition` | Mecanismo, modo de disparo, recarga, capacidad, condiciones iniciales y acciones de arma de fuego |
| `MeleeDefinition` | Cuchillo o puños, ataques ordenados, alcance/daño y pausa entre contactos |
| `EquipmentActionDefinition` | Clips, duración, hito de juego, cancelación, curvas de manos y movimiento del arma |
| `EquipmentInstance` | Munición, recámara, mecanismo pendiente, acción/reloj y combinación de ataques de **un solo objeto** |
| `EquipmentRuntime` | Guardar → cambiar selección → equipar → listo; bloquea uso y Aim durante el cambio |

Los ScriptableObjects son datos compartidos. Nunca guardan munición actual ni progreso de una acción.
Dos instancias del mismo asset tienen estados independientes. Conserva la instancia al cambiar de
equipo: crear una nueva al seleccionarla reiniciaría su munición.

## Crear equipos

En Project, usa **Create → CodeHelper → Equipment → Templates**:

- **Pistol**: autocarga, cargador de 15.
- **Rifle**: autocarga, cargador de 30; modo automático como dato para el futuro controlador de entrada.
- **Bolt Action Rifle**: cerrojo manual, cargador de 5.
- **Pump Shotgun**: bombeo, depósito de 6 y recarga de un cartucho por acción.
- **Knife**: un ataque melee.
- **Fists**: golpes izquierdo/derecho alternos, sin prefab ni grips obligatorios.

Se solicita una ruta y se guarda un asset con sus acciones como **subassets propios**. Expándelo
en Project para seleccionar y editar cada acción. Los tiempos son ejemplos de partida: no se
asignan clips, máscaras, prefabs ni poses arbitrarios. También puedes crear definiciones y acciones
vacías mediante los menús **Firearm**, **Melee** y **Action**. Puños y cuchillo pertenecen a Melee;
`Is Unarmed` distingue los puños del arma sostenida, no otra categoría.

Configura por equipo:

1. Un `Equipment Id` único dentro de tu futuro inventario y un nombre visible.
2. `Visual Prefab`, si existe, con sus grips/piezas fuera de los brazos resueltos por IK.
3. `Character Pose`: clip compatible que aporte postura de brazos y dedos. Para puños, dedos cerrados.
4. `Character Mask`: máscara para no sustituir involuntariamente locomoción/piernas.
5. `Weapon Poses`: puedes referenciar el `AK 12.asset` existente **sin modificarlo**.
6. Pesos base de cada agarre y `Can Aim`. En puños sin ancla de arma: agarres en cero, Aim desactivado.
7. Recursos y tiempos de cada acción. Las plantillas melee ya desactivan agarres/Aim.

Un campo de clip es una referencia, **no reproduce el clip por sí solo**. La primera conexión
con el personaje se documenta en [SAMPLE_SCENE_EQUIPMENT.md](SAMPLE_SCENE_EQUIPMENT.md): usa
EquipmentSceneController y canales en el único grafo de ProceduralSpine, no un segundo propietario.

## Acciones y manos

`Duration` es el reloj en segundos; `Commit Time` es el hito normalizado entre 0 y 1:

- Fire: consume la bala y registra el disparo.
- Cycle: termina la operación de mecanismo y alimenta la recámara.
- Reload: transfiere la munición.
- Melee: registra contacto potencial; todavía no aplica daño a nadie.
- Equip/Unequip: la selección cambia **al terminar Unequip**, no en su hito intermedio.

Un paso que atraviesa el hito y el final lo confirma **una sola vez**. Duración cero o una acción
no asignada en armas/transiciones es inmediata. Para melee debe existir un ataque asignado.
`Duration`, `Commit Time` y `Can Cancel` se capturan al iniciar: editarlos no retima la acción activa.
No edites las definiciones compartidas durante el juego; los clips y curvas siguen siendo referencias.

Antes del hito, cancelar no cambia munición/contactos. Después, cancelar no revierte lo confirmado.
`Can Cancel = false` impide cancelación normal; `Interrupt()` permite limpieza obligatoria al destruir
o desactivar el propietario, sin inventar hitos ni devolver balas. En un cambio de equipo, usa `Clear()`
para desmontar el coordinador; no canceles directamente sus acciones de transición.

Canales de mano distintos:

- `Left/Right Grip`: adherencia al grip del arma, multiplicada por el peso base del equipo.
- `Left/Right IK`: influencia del IK. No es lo mismo que adherencia: una mano puede soltar el arma
  y seguir otro objetivo de recarga con IK activo.

`SampleHands(progress)` devuelve ambos canales. El futuro presentador decidirá objetivos y mezclas.
La capa de datos no altera `HandIKSolver`; EquipmentSceneController consume estos pesos. En puños animados sin objetivos, el presentador deberá
dejar libre el IK de agarre, aunque una curva tenga valor 1.

`Weapon Position` expresa offset aditivo en ejes locales del **padre** de Weapon Holder, en metros;
`Weapon Rotation`, en ejes locales del ancla, en grados. Solo se evalúan con `Evaluate(progress)`;
no se aplican por la capa de datos; EquipmentMotionPass las aplica en la conexión de escena.
Prepara entradas/salidas suaves hacia cero.

## Reglas de munición

Modelo inicial de arma de fuego de recámara única, sin atascos, cargador físicamente retirado ni
estados intermedios de cerrojo abierto. `Magazine Capacity` **excluye** la bala de la recámara:
un cargador de 30 más una bala en recámara puede contener 31. Los valores iniciales son explícitos.

- **Autocarga**: disparar consume la recámara y, si hay munición en el cargador, alimenta la siguiente.
- **Cerrojo/bombeo**: disparar deja `NeedsCycle = true`. No permite otro disparo hasta confirmar Cycle.
- **Cycle** también sirve para cargar una recámara vacía antes del primer disparo. No expulsa balas
  vivas ni permite ciclar una recámara ya cargada. Puede limpiar el mecanismo tras el último disparo
  incluso sin más munición.
- **Cargador extraíble**: Reload repone hasta la capacidad, conservando las balas existentes y la
  recámara; no simula tirar un cargador parcialmente lleno.
- **Cartuchos individuales**: cada Reload inserta **uno**. El futuro controlador podrá repetir la
  acción mientras se solicite recarga, con posibilidad de detenerse entre inserciones.
- **Reload Empty** se elige si no hay bala en recámara. `Insert Round`, si existe y es recarga
  individual, tiene prioridad sobre esa alternativa.
- **Reload Chambers Round** se activa solamente si esa acción incluye operar el mecanismo. Confirma
  reposición y alimentación juntas en su hito. En las plantillas de cerrojo/bombeo está desactivado:
  cargar munición **no** significa accionar el mecanismo. Usa Cycle aparte.
- La cadencia se mide desde el disparo/contacto confirmado; cancelar después no permite saltársela.

La reposición completa y el cierre se modelan con un solo hito en esta primera base. Una recarga con
extracción, pérdida de cargador e inserción por separado requerirá ampliar las fases, no simularlo
solo con el clip. Tampoco incluye revólveres, doble recámara, reservas compartidas ni munición infinita.

## API de lógica, independiente de la conexión visual

```csharp
var rifle = new EquipmentInstance(rifleDefinition);
var fists = new EquipmentInstance(fistsDefinition);
var equipment = new EquipmentRuntime();
equipment.TrySelect(rifle); // Inicia Equip.

// Desde un único propietario: avanzar una vez por frame.
equipment.Tick(Time.deltaTime);

// Consultar el resultado bool; una petición bloqueada devuelve false.
equipment.TryFire();   // Una petición = un disparo, sea cual sea el modo declarado.
equipment.TryCycle();
equipment.TryReload();
equipment.CancelAction();
equipment.TrySelect(fists); // Requiere acción terminada/cancelada.
// Tras equipar puños:
equipment.TryMeleeAttack();
// Al desmontar el propietario:
equipment.Clear();
```

EquipmentRuntime no es un componente para añadir al Inspector. EquipmentSceneController proporciona
ahora un primer enlace de escena para modelos/grips existentes y clips del personaje.
`EquipmentRuntime` avanza solo el objeto actual; no llames además a `Current.Tick` porque duplicarías
su reloj. El coordinador recuerda los objetos seleccionados y avanza también su pausa de cadencia
mientras están guardados, pero no sus acciones. `Clear()` libera esas referencias. Un objeto debe
tener un único coordinador propietario; no compartas la misma instancia entre dos coordinadores.
Los cambios de fase aprovechan el sobrante de tiempo del frame: guardar y equipar conservan su
duración total tanto con pasos grandes como pequeños. Si un frame cubre el cambio entero, un futuro
presentador debe mostrar el estado resultante, no reiniciar visualmente todas las acciones omitidas.

Un presentador podrá observar `Current`, `SelectionVersion`, `Phase`, `Action.IsActive`, `Kind`,
`Definition`, `Progress` y `Version`. `CommitVersion` y `LastCommittedKind` permiten detectar hitos
sin AnimationEvents. Al iniciar una acción con hito cero puede confirmarse inmediatamente: observa
las revisiones también después de las peticiones, no solo después de Tick. No reproduzcas otra vez
una acción terminada solo porque `Kind` conserve su último valor.

**La base pura no incluye presentación.** La conexión de SampleScene añade selección por teclado,
modelos existentes, poses/acciones del personaje, liberación del IK de agarre y entrada de disparo
simple/mantenido/ráfaga. Todavía quedan instanciación general de prefabs, objetivos específicos de
recarga, proyectiles/raycast, daño y persistencia de partidas. Los clips Generic de piezas del arma
sí se pueden muestrear en el modelo separado, pero no se usan en la recarga simplificada actual:
SampleScene conserva C_Rifle_Reload_Humanoid para los dedos izquierdos, con derecha/IK en 1 y arma fija.
La posición izquierda sigue ahora un objetivo asignado bajo el arma, para no perderla al mirar arriba.
Left Reload Target ajusta posición/orientación por slot; Left Hand Target Motion es una trayectoria
local opcional por acción. Sin trayectoria se mantiene el punto asignado; sin objetivo se conserva
el modo FK anterior. Usa R, sin seguimiento del arma desde muñeca ni fases manuales.
Alcance/daño siguen siendo configuración, no detección de impactos. Crear un asset nuevo no lo añade
automáticamente a los slots ni garantiza que sus clips y contactos encajen con cualquier personaje.

## Comprobaciones

**Tools → CodeHelper → Run Equipment Checks** ejecuta pruebas aisladas con definiciones temporales.
**Run All Character Checks** incluye esta suite y las regresiones de cámara, poses, columna y manos.
La prueba de persistencia crea un asset con nombre único y borra exclusivamente ese asset propio;
no sustituye el equipo ni abre/limpia la escena del usuario.

Validación inicial 2026-11-09: compilación separada de Runtime y Editor, suite completa aislada de
Unity y revisión independiente completadas. Se verificaron también la cadencia con el arma guardada
y el mismo plazo de cambio con pasos únicos o a 30/60/144 Hz. Registro externo:
`C:\Users\eduar\AppData\Local\Temp\opencode\EquipmentFinalValidation.log`.
