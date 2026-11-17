# SampleScene: AK12, puños y cuchillo

Preparación directa de `Assets/Scenes/SampleScene.unity`, conservando la jerarquía de cámara,
el esqueleto, los grips/hints y el perfil `Assets/AK 12.asset` existentes.

## Uso

1. Si Unity tenía la escena abierta durante los cambios externos, **recarga la versión del disco**.
   No sobrescribas los cambios externos guardando una copia antigua de la escena desde el editor.
   Si tienes ajustes sin guardar que necesitas conservar, guarda antes esa versión con otro nombre.
2. Espera a que Unity termine de importar/compilar y entra en Play.
3. Haz clic en Game para capturar el cursor si no se captura automáticamente.
4. **1** selecciona AK12; **2**, puños; **3**, cuchillo. Se debe completar la acción actual antes de cambiar.
5. **Botón derecho** apunta con AK12. **Botón izquierdo** prueba su disparo/munición o el golpe de puños.
6. Dispara algunas balas y pulsa **R**: ejecuta la recarga solo del brazo izquierdo. No necesitas activar
   casillas ni configurar fases. El panel muestra **Recargando** y su porcentaje; al terminar cambia la munición.
7. **Escape** libera el cursor. El panel superior muestra equipo y munición; se puede ocultar con
   `Show Controls` en el nuevo componente de equipamiento.

No se incluyen daño, proyectiles, impactos, sonido ni fogonazo. El disparo es una prueba de estado
de munición y movimiento del arma. La recarga está conectada, pero el ajuste fino de contactos
entre manos y cargador sigue necesitando comprobación visual en Play.

## Referencias

- `EquipmentSceneController`, en Player: tres slots con definiciones e instancias independientes.
- `PlayerLookController.Input Enabled`: activado para poder probar mirada/Aim y controles en Play;
  antes estaba desactivado en la escena guardada. El resto de sus ajustes se conserva.
- Animator del personaje: override de **Culling Mode = Always Animate**. Antes usaba Cull Update
  Transforms; eso omite brazos/dedos cuando los renderers no están visibles. En FPS necesitamos que
  sigan evaluándose. No se cambia el Animator Controller ni el importador del personaje.
- Slot AK12: modelo ya existente, ambas muñecas y ambos hints ya configurados. No se clona el rifle.
- Slot Fists: sin modelo físico ni grips; las manos del mismo personaje quedan libres del IK del arma.
- Slot Knife: visual independiente con malla y movimiento original horneado para arma/manos/codos,
  usando los mismos IK y grafo propios. Instrucciones: [AUTHORED_KNIFE_GUIDE.md](AUTHORED_KNIFE_GUIDE.md).
- `EquipmentMotionPass`, en Player: aplica curvas del ancla en LateUpdate 575, después de Idle/Aim
  550 y antes del IK final 600. El offset se retira antes del siguiente frame, sin acumularlo.
- `ProceduralSpine.enableEquipmentAnimation`: canales de pose y acción dentro de su **único grafo**.
  En LateUpdate, el grafo evalúa Controller → canales de equipo y luego se aplica la columna.
  En AnimationJob, los canales entran antes del job de columna. No se añade otro dueño del Animator.
- `EquipmentArms.mask`: solo brazos y dedos; no sustituye cabeza, raíz, piernas ni locomoción.

Los recursos están en `CODEHELPER/EquipmentAssets/Firearms/AK12`, `Melee/Fists`, `Melee/Knife`
y `Shared/Masks`. Puños y cuchillo comparten la categoría **Melee**. Las acciones son
subassets: expande cada definición en Project para inspeccionarlas. Los clips apuntan a los FBX
existentes, no a componentes ni ScriptableObjects de KINEMATION.

AK12 usa su pose de rifle y descenso/ascenso procedural durante guardar/equipar. Puños usa los clips
Humanoid `Fists_Idle`, `Fists_Equip`, `Fists_UnEquip` y `Fists_Punch`. Son copias propias en
`HumanoidFists`, convertidas desde los originales Generic sin modificar sus importadores. Solo se ha asignado el golpe
real disponible: no se simulan dos variantes diferentes con el mismo clip.

## Límites y ajustes

- Las duraciones de las acciones con clip se ajustan inicialmente a la longitud del clip.
- La pose de dedos procede del clip; el sistema no genera agarres a partir de la geometría.
- La mezcla de acción usa Blend In/Out. Equipar puños entra desde el inicio del clip; guardar sale
  desde su final, para acompañar la retirada antes del cambio de equipo.
- El controlador resuelve IK contra grips o contra el objetivo de recarga asignado por slot;
  no genera automáticamente posiciones de contacto con el cargador.
- En la recarga actual no se usa `Weapon Clip` ni seguimiento de muñeca. El arma conserva su ancla
  y la derecha conserva el agarre. El soporte opcional anterior no está activado en estas acciones.
- No captures Idle/Aim mientras corre una acción: la captura del controlador de poses excluye su
  rebote de Aim, pero no el movimiento adicional de equipamiento.
- El controlador conserva las instancias al desactivar/reactivar para no rellenar munición. Al
  desactivarlo devuelve los targets, pesos, visibilidad y modo de entrada de poses que encontró.

El movimiento y los contactos finales deben revisarse con tu personaje en Play Mode. Una prueba
de grafo o de referencias no sustituye esa comprobación visual.

## Puños y dirección de cámara

**Configuración actual:** los puños tienen guardia y destino editables por mano en
`Camera Holder → Fists Hand Targets`. Mueve los puntos `Left/Right Guard` para colocar las muñecas
y `Right Action Target` para ajustar el golpe recto; guarda fuera de Play. La izquierda conserva
su guardia mientras golpea la derecha. Instrucciones y reutilización con otros equipos:
[EDITABLE_HAND_TARGETS.md](EDITABLE_HAND_TARGETS.md).

El seguimiento FK descrito debajo se conserva como alternativa para manos sin objetivos asignados.

`Fists_Definition → Follow Camera` activa la orientación de la guardia y los golpes hacia la vista.
No activa Aim de rifle ni bloquea las manos con grips: los clips siguen animando brazos y dedos.

Antes los puños heredaban solo el giro limitado de la columna, mientras la cámara podía mirar
hasta ±90°. El seguimiento completa esa diferencia después de actualizar la cámara, sin sumar
dos veces el giro del torso. Solo se aplica a equipos `Is Unarmed` con `Follow Camera` activo.

Los hombros permanecen en el cuerpo, por lo que el encuadre no es el de unos brazos flotantes
pegados a la pantalla. No se cambia la posición de la cámara para acomodar la guardia.
La corrección se retira antes de evaluar el siguiente frame y al desactivar el equipo.

Validación: `FistsCameraBeforeFix.log` reproduce 11° de desfase mirando +45°.
`FistsCameraFullValidation.log` registra compilación y suite completa: guardia y golpe a
0/±45/±90 de pitch, ±60 de yaw y cuerpo girado, en LateUpdate y AnimationJob. El error angular
medido es 0.000° y el de recorrido relativo al hombro 0.002 mm. Se comprobaron restauración,
escrituras externas, ausencia de acumulación/deriva y regreso a AK12. La escena, cámara, perfil
Idle/Aim y definición AK12 se conservan idénticos al respaldo. Falta revisar apariencia en Play.

## Recarga izquierda relativa al arma — configuración actual

Se usa directamente **`C_Rifle_Reload_Humanoid`** para los dedos. El recorrido de muñeca es el
clip local independiente `AK12_LeftReloadTarget.anim`, editable y opcional.

| Parte | Durante la recarga |
|---|---|
| Mano derecha | IK y agarre en **1**; sigue su grip habitual |
| Mano izquierda | IK sigue el objetivo de recarga ligado al arma; mezcla desde/hacia su grip |
| Dedos izquierdos | Character Clip con `LeftReloadFingers.mask` |
| Arma | Sigue en Weapon Holder; no sigue la muñeca animada ni recibe offsets de recarga |
| Piezas | Sin clip de piezas por ahora; no se mezcla el antiguo clip AK12 de otra duración |

- La máscara de recarga incluye **solo Left Fingers**. Brazo y muñeca pertenecen al IK,
  no al clip Humanoid. `LeftArmReload.mask` se conserva para configuraciones FK anteriores.
- Se usa el mismo clip en Reload y Reload Empty; duración actual **3.3 segundos**, igual al clip existente.
- La munición se transfiere **al terminar**; cancelar antes no la concede. Solo se confirma una vez.
- R durante el último disparo espera a que termine ese disparo. Volver a pulsar R durante la
  recarga no encola otra recarga. Disparo, Aim y selección quedan bloqueados mientras recarga.
- El contador incluye la bala de recámara: un cargador lleno con una bala ya cargada puede mostrar
  31; una recarga desde vacío carga 30 en total y alimenta la recámara.
- Al desactivar el componente se recupera el agarre sin rellenar munición.

### Asignar/ajustar la posición

```text
Weapon Holder
├── Hands Grips                    ← agarres existentes, sin cambios
├── AK12                           ← modelo existente
└── AK12 Left Reload Target        ← posición y orientación ajustables de esta recarga
```

En **Player → Equipment Scene Controller → Slots → AK12 → Left Reload Target** está asignado ese
objeto. Mueve/rota **AK12 Left Reload Target** en Scene para ajustar la recarga; no muevas el grip
Idle/Aim ni los huesos. Guarda la escena fuera de Play para conservar el ajuste.

El objetivo es hijo del ancla del arma, no del cuerpo: al mirar arriba/abajo, su posición y su
trayectoria acompañan al arma. Durante la manipulación la izquierda mantiene IK activo contra ese
objetivo; **soltar el grip no significa apagar todo el IK**. La derecha sigue en su grip con peso 1.

La acción referencia `AK12_LeftReloadTarget.anim`, un ejemplo editable de posición/rotación local
obtenido del clip actual en mirada neutra. Se reproduce en un hijo temporal del objetivo, sin mover
el objeto de ajuste guardado. **Left Hand Target Motion = ninguno** deja la mano en el punto que
asignes, sin trayectoria adicional. Los dedos siguen usando Character Clip.

Para tus propias trayectorias, anima un Empty con posición local cero y rotación identidad bajo
el objetivo y guarda solo sus curvas locales de posición/rotación (clip Generic con ruta raíz).
Asigna ese clip en **Left Hand Target Motion** y tu clip de dedos en **Character Clip**. Conserva
la máscara solo de dedos izquierdos y el IK derecho en 1; ajusta Duration al tiempo deseado. No animes directamente
Weapon Holder ni la cámara para corregir la mano.

Un objetivo fuera del alcance físico del brazo seguirá limitado por el IK. La referencia relativa
al arma evita la deriva por mirada; no genera automáticamente contactos perfectos con el cargador.
El objetivo inicial quedó en Z local `-0.1`, más cerca del cuerpo que el grip delantero; es un punto
de partida para tu calibración, no una posición de contacto garantizada.

### Por qué temblaba al recargar

La máscara anterior dejaba que el clip Humanoid animara también el brazo, aunque el IK ya colocaba
la muñeca. En LateUpdate esto añadía desplazamiento de cadera/cabeza que la cámara seguía.
El diagnóstico a 60 Hz midió pasos máximos de cabeza/cámara de **1.346 mm** frente a **0.129 mm**
en Idle. Con objetivo fijo seguía ocurriendo; con clip solo de dedos volvía al nivel de Idle.

`LeftReloadFingers.mask` separa las responsabilidades: clip para dedos y objetivo IK para brazo y
muñeca. No se cambian suavizado, root motion, cámara, poses ni trayectoria para ocultar el problema.
La extracción opcional de trayectoria todavía usa el brazo completo, pero solo en su preview aislado.

Validación: `ReloadFingerOnlyFullValidation.log` contiene la suite completa y 36 variantes continuas
a 30/60/144 Hz en ambos modos. La recarga corregida iguala la estabilidad corporal de Idle; el
control con brazo completo reproduce el fallo anterior. Sin deriva con reloj fijo ni efectos del
muestreo sobre ancestros. Revisión independiente sin hallazgos. Solo se cambiaron las dos referencias
de máscara en la definición; escena, cámara y poses permanecen intactas. En la configuración nueva
2 de 35 muestras aún exceden el alcance del brazo: el contacto final requiere calibración visual.

Los recursos de la adaptación anterior, dentro de `AK12Reload`, se conservan sin borrar, pero **ya
no están asignados a las recargas**. No necesitas editarlos ni configurar sus fases.

Respaldo anterior a esta adaptación:
`C:\Users\eduar\AppData\Local\Temp\opencode\before-ak12-animated-reload-20261110-094757`.
Respaldo anterior a la simplificación izquierda:
`C:\Users\eduar\AppData\Local\Temp\opencode\before-left-arm-reload-20261110-101144`.
Respaldo anterior al objetivo relativo al arma:
`C:\Users\eduar\AppData\Local\Temp\opencode\before-weapon-relative-left-reload-20261110-113055`.

Respaldo previo fuera de Assets:
`C:\Users\eduar\AppData\Local\Temp\opencode\before-sample-equipment-20261109-221108`.
Se verificó por secciones que los transforms originales, overrides del modelo, cámara y grips
no cambiaron, salvo el nuevo override de culling del Animator; el perfil AK12 se conserva idéntico al respaldo.

## Validación realizada

Compilación Runtime/Editor y todas las suites previas pasan. La escena guardada se abrió en preview
aislado y se evaluó el grafo con el personaje real en **LateUpdate y AnimationJob**. Se verificaron
referencias sin scripts ausentes, un único output, descenso del arma antes del cambio, reproducción
de pose y golpe, cambios de dedos, liberación/restauración de grips, Aim sostenido durante disparo,
holster, cambios repetidos sin offsets acumulados y restauración al desactivar el enlace.

Registros externos: `SampleEquipmentFinalValidation.log` y `SampleEquipmentADSSceneValidation.log`,
en `C:\Users\eduar\AppData\Local\Temp\opencode`. No se ha comprobado físicamente el ratón ni juzgado
la estética de la animación en tu Unity abierto.

Ampliación 2026-11-10: `AK12AnimatedReloadFinalValidation.log` registra compilación y regresiones
completas con la recarga adaptada, en ambos modos. Comprueba movimiento de manos/cargador, ancla
alineada con muñeca, munición al final, cancelación, vacío y desactivar/reactivar sin residuos.

Simplificación anterior 2026-11-10: `LeftArmReloadAllChecks.log` contiene la suite completa de la
configuración anterior en ambos modos. Comprueba máscara izquierda, derecha/IK en 1 durante toda
la acción, ausencia de influencia sobre dedos derechos comparando la pose base al mismo tiempo,
arma/piezas fijas, munición al final, cancelación, vacío y desactivar/reactivar. No se modificó la escena.

## Prueba de asignación por arma

**Tools → CodeHelper → Check SampleScene Equipment (Isolated)** también prueba A → B → A en una
copia preview de la escena guardada. B es una definición temporal con otro clip diagnóstico y
otros grips: no es una pistola terminada ni una nueva arma añadida a SampleScene. Se comprueban
el clip que realmente recibe el grafo, distintos recorridos izquierdos, derecha en 1, arma fija,
referencias de cada agarre y munición independiente. El recorrido se contrasta con el clip sin
modificar al mismo tiempo de pose/acción y en coordenadas del personaje; los dedos izquierdos se
contrastan con una muestra sin acción al mismo tiempo. Se recuperan las instancias originales y
los objetos temporales se destruyen incluso si falla una comprobación.

Para la prueba manual visible no necesitas ese menú: entra en Play, selecciona AK12 con **1**,
dispara y pulsa **R**. Comprueba que solo la izquierda se separa del agarre y vuelve al terminar.

Validación 2026-11-10: compilación y suite completa pasan en ambos modos. El control mostró
27.99/28.79 cm de diferencia en recorrido izquierdo y 46.02 grados en el dedo izquierdo, sin
influencia sobre los dedos derechos. Revisión independiente completada. Registro externo:
`C:\Users\eduar\AppData\Local\Temp\opencode\PerWeaponLeftReloadControlledValidation.log`.
Estas medidas pertenecen a la variante diagnóstica temporal, no a una calibración de contactos.

Corrección relativa al arma: `WeaponRelativeReloadAdjustedValidation.log` registra compilación y
suite completa en ambos modos, con cámara real y 7 muestras de recarga para cada mirada 0/±45/±90.
La trayectoria/rotación local permanecen invariantes. En 4 de 35 muestras el objetivo del ejemplo
excede el alcance del brazo; el IK llega al límite físico sin error adicional. Esas poses requieren
ajustar tu objetivo/recorrido, no volver a usar una trayectoria de muñeca relativa al cuerpo.
