# Plan de progreso: armas, poses y recargas

Este documento será nuestra lista de seguimiento. Avanzaremos con **una sola arma** y marcaremos
las tareas conforme estén implementadas y comprobadas. Después repetiremos el proceso con otras armas.

## Reglas de trabajo

- Usar componentes propios dentro de `Assets/CODEHELPER`, sin dependencias de KINEMATION.
- Se permite utilizar recursos de animación de KINEMATION como prueba por decisión del usuario:
  no se añaden sus componentes. Esos recursos siguen siendo referencias externas del contenido,
  aunque los scripts propios no dependan de sus ensamblados.
- Los componentes oficiales de Unity (Animator, AnimationClip, ScriptableObject, etc.) sí se pueden usar.
- No cambiar la cámara que ya funciona salvo que una prueba demuestre que es necesario.
- Completar y validar una etapa antes de iniciar la siguiente.
- Una compilación correcta no confirma por sí sola que el resultado visual sea correcto.
- Marcar `[x]` solo después de cumplir el criterio de finalización. Registrar lo que falte o falle.
- Al completar una tarea, actualizar este archivo y su registro de avances.

## Estado actual

- **Etapa activa:** 2 — Guardar y mezclar las poses Idle y Aim.
- **Etapa 1:** completada por confirmación del usuario el 2026-11-09.
- **Arma de prueba:** AK12, instancia del modelo `W_AK12_ReloadEmpty.fbx` bajo `Weapon Holder`.
- **Personaje / escena:** `Player/Character` en `Assets/Scenes/SampleScene.unity`.
- **Base existente:** `HandIKSolver` resuelve brazos y muñecas; la cámara funciona aparentemente
  bien según la última comprobación del usuario. El agarre estático inicial ya tiene evidencia visual.
- **Última validación visual del arma:** captura proporcionada por el usuario con vista externa y FPS.
  El usuario confirma completadas las comprobaciones de agarre de la etapa 1. Los contactos finos de
  dedos siguen en la etapa 3 y la calibración/captura de Aim forma parte de la etapa 2.
- **Clip de prueba:** `Rifle_Idle_Humanoid`, nuevo estado predeterminado del Animator del personaje.
  `ProceduralSpine.bindPoseClip` todavía referencia `C_Pistol_Idle_Humanoid`: es su referencia de
  calibración, no el estado activo del Animator. Revisar esa elección si afecta a la postura; no se ha cambiado.

> El IK no cierra los dedos, no evita automáticamente colisiones con el pecho y no genera recargas.
> Esas funciones se comprobarán o incorporarán por separado.

---

## 1. Agarrar correctamente el arma — Completada

**Objetivo:** conseguir una sujeción estática estable de brazos y muñecas antes de guardar poses.

- [x] Elegir una sola arma y confirmar el personaje y la escena de prueba.
- [x] Colocar el arma bajo un ancla independiente de los brazos, por ejemplo `Weapon Holder`.
- [x] Ajustar su posición base a una distancia alcanzable y sin penetrar el pecho.
- [x] Colocar y orientar inicialmente el grip derecho para la muñeca de la empuñadura.
- [x] Colocar y orientar inicialmente el grip izquierdo en el agarre de apoyo.
- [x] Asignar huesos y targets en `HandIKSolver`.
- [x] Ejecutar `Validate Setup` y confirmar que no hay advertencias.
- [x] Separar los hints de codo y mantenerlos fuera de ambas cadenas de brazos.
- [x] Confirmar que los codos se doblan correctamente durante todo el rango de prueba.
- [x] Revisar la sujeción estática inicial desde FPS y una vista externa mediante captura del usuario.
- [x] Probar mirar arriba, abajo, girar y hacer lean: sin saltos, muñecas torcidas ni brazos inalcanzables.
- [x] Conservar los ajustes iniciales de arma, grips y hints en el archivo de escena.
- [x] Confirmar que la configuración se conserva al volver a ejecutar.

**Para darla por terminada:** ambas muñecas mantienen el contacto y la orientación del agarre;
los codos se doblan de forma aceptable y el arma no entra en el pecho dentro del rango probado.
El cierre detallado de dedos se resolverá en la etapa 3.

**Validación / bloqueos:** etapa cerrada por confirmación del usuario. La revisión del archivo y la
captura respaldan la configuración y la colocación estática; las comprobaciones restantes se dan
por realizadas conforme a esa confirmación, no por una ejecución en vivo del asistente. No se
reportan bloqueos de esta etapa. Los padres `Hands Grips` y `Elbows Grips` en cero son correctos.

## 2. Guardar y mezclar las poses Idle y Aim

**Objetivo:** conservar dos poses del arma por perfil y cambiar entre ellas sin recolocarla manualmente.

- [x] Definir un único espacio local de referencia para las dos poses: Weapon Holder respecto a Camera Holder.
- [x] Crear un perfil propio con posición, rotación y duración de transición.
- [x] Crear herramientas para capturar y previsualizar Idle y Aim, guardando los datos en el perfil.
- [x] Crear un controlador propio que aplique la pose sin acumular transformaciones.
- [x] Definir un orden de ejecución compatible con cámara, arma y resolución final de manos: 500 -> 550 -> 600.
- [x] Ejecutar comprobaciones aisladas de captura, persistencia, previsualización/Undo y transiciones.
- [x] Añadir el componente a Weapon Holder y crear el perfil capturando primero el Aim actual.
- [ ] Ajustar Idle y Aim visualmente; comprobar la alineación de la mira con la cámara.
- [ ] Probar entrar/salir de Aim y cambiar de dirección a mitad de una transición, sin saltos.
- [ ] Confirmar que ambas manos siguen los grips durante toda la transición.
- [ ] Guardar, salir y volver a ejecutar para comprobar la persistencia del perfil.

**Para darla por terminada:** Idle y Aim se recuperan desde datos guardados, la transición es estable
y la mira queda alineada en las condiciones de prueba. Un Animator nuevo no es necesario solo para estas dos poses.
Si la calibración manual no basta, se incorporará una referencia `AimPoint`.

**Notas / bloqueos:** la colocación actual es la candidata a **Aim**, según el usuario, no a Idle.
En el archivo guardado, `Weapon Holder` está en posición local `(0, -0.05, 0.3)`, rotación identidad,
respecto a `Camera Holder`. Es la referencia inicial que debe conservarse, no un Aim definitivo
calibrado automáticamente. Ya existen `WeaponPoseProfile`, `WeaponPoseController` y su editor;
la escena ya referencia `Assets/AK 12.asset`, con Idle `(0.03, -0.1, 0.4)` y Aim `(0, -0.05, 0.3)`
capturados. Falta la validación visual completa. Ver [WEAPON_POSES_GUIDE.md](WEAPON_POSES_GUIDE.md). No recolocar
el arma para preparar Idle sin conservar antes esta candidata. No cambiar los grips entre Idle y Aim:
deben acompañar al arma.

### 2.1. Sensación procedural al apuntar — ampliación solicitada

Se conserva la cámara actual y las poses Idle/Aim; el movimiento adicional afecta solo al arma.

- [x] Incorporar ajustes de impulso, frecuencia, amortiguación y límites por perfil.
- [x] Añadir un resorte temporal de posición y rotación, independiente de las poses guardadas.
- [x] Excluir el rebote propio de las capturas y retirar su residuo al desactivar el controlador.
- [x] Comprobar en pruebas aisladas estabilidad, reversión rápida, retorno a cero y ausencia de movimiento de cámara.
- [x] Activar Aim Motion en el perfil real AK 12 con un preset de diagnóstico claramente visible.
- [x] Medir ambos presets y una copia de los datos reales del AK12: cámara/FOV intactos, poses preservadas y retorno a Idle/Aim.
- [ ] Ajustar visualmente la intensidad final con el arma real (hay botón para volver al preset suave).
- [ ] Confirmar que Aim termina alineado y las manos mantienen el agarre durante el movimiento.

**Notas:** los perfiles nuevos siguen teniendo el efecto desactivado por defecto. Por solicitud del
usuario se ha activado explícitamente un preset exagerado en `Assets/AK 12.asset`, sin cambiar sus
poses ni la escena. Se añadieron presets suave/diagnóstico y lectura de offsets en vivo al Inspector.

### 2.2. Efectos de cámara Aim — ampliación solicitada

Canal independiente del rebote de arma. No modifica el ancla/posición de cámara ni la entrada del ratón.

- [x] Añadir ajustes de FOV y resorte angular de cámara por perfil.
- [x] Integrar el canal en CameraRigController, sin un segundo escritor de transforms.
- [x] Configurar AK12 con FOV adicional +3, temblor breve y rebote de arma suave; conservar Idle/Aim.
- [x] Ejecutar comprobaciones aisladas de FOV, rotación, cámara/holder, limpieza y coexistencia de efectos.
- [ ] Validar en Play que el FOV pasa de 60 a 63, vuelve a la base y el temblor es cómodo.
- [ ] Confirmar que el agarre y la alineación final de Aim siguen siendo correctos.

**Notas:** Aim Effects Source se busca entre los hijos del rig al iniciar si está vacío. En esta
fase no se añaden objetos ni se cambia la jerarquía. Los perfiles nuevos dejan Aim Camera desactivado.
El preset exagerado de la etapa 2.1 fue temporal: el AK12 vuelve ahora al preset suave de arma.
Después, el usuario solicitó un rebote **de cámara** muy visible: se configuró frecuencia 2,
amortiguación 0.15, impulso Aim `(-2, 0.5, 1)` y límite 2 grados. Se conservaron sus nuevos ajustes
de FOV `-30` y aplicación en TPS; el arma sigue con su rebote suave. Ver la tabla de restauración
de cámara en [WEAPON_POSES_GUIDE.md](WEAPON_POSES_GUIDE.md).

### 2.3. Tiempo compartido de Aim — ampliación solicitada

- [x] Publicar un único reloj de transición desde WeaponPoseController.Update.
- [x] Usar el mismo progreso para arma y FOV, antes del IK final de manos.
- [x] Hacer que los rebotes de arma y cámara terminen en cero con la pose.
- [x] Conservar el modo independiente como alternativa configurable.
- [x] Activar la sincronización en AK12 sin cambiar Idle/Aim ni el zoom actual del usuario.
- [x] Completar la comprobación final de distintas duraciones, tasas de frame e inversiones rápidas.
- [ ] Validar en Play si 0.18 segundos resulta cómodo o conviene una transición de 0.25/0.3 segundos.

**Notas:** con Synchronize Aim Effects, Transition Duration gobierna los efectos de Aim y
Transition Oscillations su número de oscilaciones. Frequency/Fov Blend Speed solo se utilizan
en el modo independiente. Se conserva el FOV actual -10 y la aplicación solo en FPS.

### 2.4. Definiciones de equipo y estado — ampliación autorizada

Esta base de datos/lógica no da por finalizadas las etapas visuales de agarre y recarga.
Guía: [EQUIPMENT_GUIDE.md](EQUIPMENT_GUIDE.md).

- [x] Separar EquipmentDefinition, FirearmDefinition y MeleeDefinition (incluidos puños sin modelo).
- [x] Separar mecanismo, modo de disparo y método de recarga.
- [x] Referenciar pose de brazos/dedos, máscara, perfil Idle/Aim y acciones por equipo.
- [x] Definir acciones con clips, reloj, hito, cancelación y canales distintos de agarre/IK.
- [x] Mantener munición, recámara, mecanismo y progreso en instancias, no en los assets.
- [x] Implementar secuencia lógica guardar → sustituir selección → equipar.
- [x] Añadir plantillas de pistola, rifle, cerrojo, escopeta de bombeo, cuchillo y puños.
- [x] Completar compilación y pruebas aisladas de reglas de munición, cancelación y cambios.
- [x] Elegir clips/prefabs compatibles y conectar la presentación al grafo existente y al IK.
- [ ] Validar el primer cambio animado y las acciones con el personaje real en Play Mode.

**Al finalizar 2.4:** solo existía la base lógica, sin presentación ni cambios en la escena.
La conexión solicitada después se registra en 2.5; debe respetar que ProceduralSpine controla
su propio grafo, sin añadir otro propietario de Animator. El daño sigue fuera de esta prueba.

### 2.5. Conexión de AK12 y puños en SampleScene — autorizada

- [x] Guardar respaldo externo antes de modificar la escena original.
- [x] Crear enlace de escena con slots, referencias de grips y liberación para puños.
- [x] Añadir canales de pose/acción al grafo de ProceduralSpine, antes de la columna y sin otro dueño de Animator.
- [x] Crear pase procedural posterior a Idle/Aim y anterior al IK de manos.
- [x] Crear/asignar los assets AK12/puños con los clips compatibles disponibles.
- [x] Guardar las referencias en SampleScene preservando sus transforms y poses.
- [x] Completar regresiones y pruebas aisladas del grafo con el personaje real en ambos modos.
- [ ] Verificar en Play los contactos, comodidad y entrada real.

Guía específica: [SAMPLE_SCENE_EQUIPMENT.md](SAMPLE_SCENE_EQUIPMENT.md). No incluye todavía una
recarga visual terminada ni daño; esos avances no se marcan por preparar el cambio de equipo.

### 2.6. Recarga simple adaptada a los clips existentes — solicitada

- [x] Reutilizar la secuencia AK12 existente sin exigir configuración manual de fases.
- [x] Hornear el clip Generic de brazos a Humanoid sobre un esqueleto fuente compatible, sin modificar originales.
- [x] Asignar su clip de piezas y el mismo reloj a la acción del personaje.
- [x] Liberar y recuperar IK de agarre; mover el arma desde la muñeca animada con ancla independiente.
- [x] Activar R en SampleScene y mostrar Recargando/progreso en el panel.
- [x] Confirmar la transferencia de munición una vez al terminar, sin concederla al cancelar antes.
- [x] Comprobar recarga con munición, desde vacío, cancelación, desactivar/reactivar y restauración en ambos modos de columna.
- [x] Compilar Runtime/Editor y pasar todas las regresiones previas.
- [ ] Ajustar/verificar visualmente contactos de mano-cargador-arma en Play con FPS/TPS y distintos ángulos.

**Alcance:** una sola secuencia completa de 4.5 s para ambos casos; no es todavía una variante táctica
abreviada. Mantiene el modelo, grips, cámara y poses actuales. El seguimiento de muñeca no parenta el
arma bajo una mano resuelta por IK. Las etapas visuales pendientes no se dan por aprobadas por pruebas de código.

### 2.7. Simplificar recarga a brazo izquierdo — corrección del usuario

- [x] Cambiar la preparación a C_Rifle_Reload_Humanoid existente, sin hornear/convertir animación.
- [x] Limitar la máscara a brazo y dedos izquierdos; mantener agarre/IK derecho constante en 1.
- [x] Desactivar clip de piezas, seguimiento de muñeca y offsets de recarga en estas acciones.
- [x] Asignar los recursos simplificados al AK12 preservando escena, cámara, poses y grips.
- [x] Comprobar derecha en 1 durante toda la acción, izquierda animada, arma fija y regresiones.
- [ ] Usuario ajusta sus clips izquierdos y contactos visuales en Play.

La adaptación anterior 2.6 queda como historial: sus recursos no se borran, pero dejan de utilizarse.
No se amplía el framework: se simplifica la configuración de las dos acciones de recarga existentes.

### 2.8. Prueba de animación y agarres por arma — solicitada

- [x] Mantener el ejemplo AK12 listo para Play: 1, disparar y R, con derecha fija e izquierda animada.
- [x] Añadir prueba A → B → A solo en preview, con otra definición, clip y grips temporales.
- [x] Verificar que el mismo controlador selecciona cada clip/grip y conserva munición independiente.
- [x] Ejecutar la prueba nativa en ambos modos de columna y las regresiones.
- [ ] Usuario comprueba el contacto visual del ejemplo en Game.

La definición B es un diagnóstico temporal con un clip modificado para distinguir el recorrido;
no se presenta como otra arma o recarga de producción ni se añade a la escena guardada.

### 2.9. Objetivo de recarga relativo al arma — corrección por mirada extrema

- [x] Separar la posición de muñeca del clip corporal: ancla Left Reload Target por arma.
- [x] Mantener IK izquierdo activo contra ese objetivo y derecho en 1; conservar clip para dedos.
- [x] Evaluar trayectoria local tras cámara/poses y antes del IK final, mezclando desde/hacia el grip.
- [x] Añadir AK12 Left Reload Target a SampleScene sin recolocar cámara, modelo, grips ni poses.
- [x] Asignar ejemplo de trayectoria local obtenido del clip actual, editable y opcional.
- [x] Validar posición/rotación relativa al arma al mirar 0, ±45 y ±90 grados con cámara real.
- [x] Completar regresiones, controles por arma, objetivos fijos sin clip y limpieza al cancelar/desactivar.
- [ ] Usuario calibra posición y contactos dentro del alcance del brazo en Play.

El modo anterior de FK corporal solo queda como alternativa para slots sin objetivo asignado.
No se parenta el arma a una mano. El nuevo objetivo está bajo Weapon Holder y es un ajuste por arma.

### 2.10. Temblor al recargar — propiedad exclusiva de brazo y dedos

- [x] Reproducir frame a frame y aislar clip corporal, objetivo fijo, ausencia de clip y solo dedos.
- [x] Identificar exceso de movimiento de cadera/cabeza/cámara con máscara de brazo en LateUpdate.
- [x] Preparar máscara solo Left Fingers; conservar extracción del brazo únicamente en preview.
- [x] Asignar máscara a Reload/Reload Empty sin cambiar clips, tiempos, curvas ni trayectoria.
- [x] Validar estabilidad frente a Idle a 30/60/144 Hz en ambos modos y conservar control de reproducción del fallo.
- [x] Completar regresiones y revisión; verificar escena, cámara y poses intactas.
- [ ] Usuario comprueba visualmente la recarga en su editor.

El clip anima dedos y el IK coloca brazo/muñeca. No se corrige aumentando suavizado de cámara.

### 2.11. Puños orientados a la cámara

- [x] Reproducir desalineación de guardia/golpe al mirar arriba, abajo y a los lados.
- [x] Añadir seguimiento opcional de cámara exclusivo de equipos sin arma; conservar clips de brazos/dedos.
- [x] Corregir la rotación heredada del torso después de cámara y antes de IK, sin duplicar pitch/yaw.
- [x] Validar guardia/golpe en ambos modos, cambios a AK12 y restauración sin deriva.
- [x] Completar regresiones y revisión; conservar escena, cámara y configuración del rifle.
- [ ] Usuario comprueba encuadre y apariencia de hombros/brazos en Play.

Los puños siguen el ángulo de vista, pero los hombros permanecen en el cuerpo: no se parenta el
personaje a la cámara ni se convierten los golpes en agarres fijos de arma.

### 2.12. Guardia y recorridos editables por mano/equipo

- [x] Añadir guardia/destino y polo de codo opcionales por mano y slot, sin referencias de escena en los assets.
- [x] Añadir Reach, posición/rotación y clip local opcional independientes por mano/acción.
- [x] Usar el reloj de la acción para trayectoria, mezcla y contacto; mantener mano no atacante en guardia.
- [x] Reutilizar IK y único grafo; máscaras privadas excluyen brazos solo con IK completo, conservando dedos y FK parcial/cero.
- [x] Configurar puntos Fists en SampleScene con golpe derecho recto +16 cm, sin cambiar transforms originales, cámara, AK12 ni poses.
- [x] Añadir creación/selección de puntos y etiquetas de Scene; documentar flujo reutilizable para otras herramientas.
- [x] Validar curvas/clips, destino rotado, cancelación, referencias inválidas, modos, cámara extrema y 30/60/144 Hz.
- [x] Completar regresiones y revisión independiente; documentar alcance y continuidad de codo sin hint al variar pesos.
- [ ] Usuario ajusta guardia, recorrido y encuadre en Play y conserva los ajustes fuera de Play.

Base de presentación, no combate completo de arcos/escudos/armas blancas. No mueve automáticamente
un modelo junto a la mano: su ancla/clips deben coordinarse. Los huesos conservan su longitud física.

### 2.13. Cuchillo con animación original coordinada

- [x] Inspeccionar rig/clip original y extraer arma, manos y codos, sin depender de componentes de terceros.
- [x] Conservar tiempo original, hornear rig Generic a 60 Hz y reproducir dedos desde copia Humanoid propia.
- [x] Añadir mezcla opcional del rig visual desde/hacia su pose base, preservando ajustes no animados.
- [x] Crear prefab nativo, definición y tercer slot con tecla 3; preservar AK12, puños, cámara y poses.
- [x] Añadir ajustes locales de manos/codos y root independiente para calibrar el conjunto.
- [x] Incorporar calibración del usuario (-0.0222, -0.0171, -0.0773), Euler (-90, 0, 0), como offset de Right Guard sin sobrescribir el nodo animado.
- [x] Validar fuente de agarre, reloj/mezclas, dedos, vista extrema, retorno/cancelación/limpieza y 30/60/144 Hz en ambos modos.
- [x] Completar compilación, todas las regresiones y revisión independiente.
- [ ] Usuario confirma contacto visual, materiales y comodidad en Play; daño/impactos/sonido todavía pendientes.

El clip coordinado conduce objetivos y arma bajo un ancla independiente; no se parenta el cuchillo
a una mano resuelta. Los recursos de origen y las definiciones anteriores no se sobrescriben.

### 2.14. Organización de CODEHELPER y copia privada

- [x] Agrupar Firearms/AK12 y Melee/Fists/Knife; puños y cuchillo siguen siendo MeleeDefinition.
- [x] Conservar namespaces, GUID, datos de equipos y agarres; no modificar la escena ni los perfiles externos.
- [x] Centralizar rutas de autoría y conservar clips/modelos derivados solo localmente.
- [x] Preparar repositorio privado limitado a CODEHELPER, sin escenas ni configuración del proyecto Unity.
- [x] Verificar compilación y equivalencia de assets movidos. La regresión de escena conserva su fallo previo ante el Weapon Clip personalizado del AK12; no se modifica como parte de ordenar carpetas.

La copia de Git excluye recursos comerciales y derivados. No sustituye un respaldo autorizado de
los recursos externos; el alcance es únicamente esta carpeta, como solicitó el usuario.

## 3. Preparar la pose de dedos

**Objetivo:** que las manos envuelvan el arma, no solo que las muñecas alcancen los grips.

- [ ] Crear o preparar una pose compatible con los dedos del personaje para esta arma.
- [x] Asignar un clip de prueba de rifle en el Animator: `Rifle_Idle_Humanoid`.
- [ ] Definir su aplicación mediante un clip con máscara o un sistema propio de rotaciones locales.
- [ ] Evitar que la pose de dedos sobrescriba brazos, columna o cámara.
- [ ] Ajustar pulgares, índice y resto de dedos en ambas manos.
- [ ] Comprobar Idle, Aim y las transiciones sin penetraciones visuales importantes.
- [ ] Guardar la referencia de pose y comprobar que se restaura al volver a ejecutar.

**Para darla por terminada:** la pose de dedos es reproducible, se adapta al arma y no pelea con el IK.

**Notas / bloqueos:** pendiente.

## 4. Preparar una primera recarga animada

**Objetivo:** tener una acción base coherente antes de añadir variaciones procedurales.

- [x] Elegir un solo tipo de recarga inicial: táctica o desde vacío.
- [x] Crear o preparar un clip compatible con el personaje para brazos, muñecas y dedos.
- [x] Preparar la animación de las piezas necesarias del arma: cargador, palanca, cerrojo, etc.
- [x] Definir cómo se moverá el ancla del arma durante la acción; no dejarla fija si el clip necesita moverla.
- [x] Previsualizar la secuencia sin que el IK de agarre sobrescriba los brazos que deben moverse.
- [ ] Ajustar contactos y sincronización entre manos, cargador y arma.
- [ ] Identificar los momentos de liberar agarre, retirar/insertar cargador y recuperar agarre.

**Para darla por terminada:** la acción base encaja visualmente con el personaje y el arma.
Todavía no se considera terminada su integración con el sistema de juego.

**Notas / bloqueos:** pendiente.

## 5. Integrar la recarga con componentes propios

**Objetivo:** ejecutar la acción completa sin depender de KINEMATION ni perder el agarre al terminar.

- [x] Crear un perfil de recarga propio con recursos, tiempos y curvas de control.
- [x] Crear un controlador de recarga con un reloj común para personaje, arma y fases.
- [x] Definir por mano cuándo sigue el grip y cuándo la animación u otro objetivo de recarga.
- [x] Distinguir la adherencia al grip del peso del IK: liberarlo no obliga siempre a apagar todo el IK.
- [x] Mezclar suavemente la liberación y recuperación de cada mano, sin ciclos de dependencia.
- [x] Evitar que Idle/Aim y la recarga sobrescriban simultáneamente el mismo transform.
- [x] Definir cuándo se actualiza la munición y cómo se evita aplicarla dos veces.
- [x] Gestionar interrupción, desactivación y regreso seguro a la pose normal.
- [ ] Probar la acción con distintos ángulos de mirada y en FPS/TPS.
- [x] Confirmar en pruebas aisladas que varias recargas seguidas no acumulan offsets ni dejan manos o piezas fuera de lugar.

**Para darla por terminada:** la recarga puede iniciarse, completarse o cancelarse según las reglas
definidas, manteniendo sincronización y recuperando correctamente la sujeción.

**Notas / bloqueos:** pendiente.

## 6. Añadir movimiento procedural de recarga

**Objetivo:** ampliar la recarga funcional, no reemplazar todo lo anterior de golpe.

- [ ] Elegir una mejora concreta: inclinación del arma, trayectoria de una mano o ajuste de contacto.
- [ ] Guardar curvas de posición/rotación y declarar su espacio de referencia.
- [ ] Evaluar esas curvas con el mismo reloj de la recarga, antes de resolver los brazos.
- [ ] Combinar el movimiento con las poses de dedos y las fases del cargador.
- [ ] Comprobar continuidad al entrar, salir o cancelar la acción, sin duplicar movimientos del clip.
- [ ] Comparar con la recarga base y conservar solo los cambios que mejoren el resultado.

**Para darla por terminada:** la mejora procedural elegida funciona de forma estable y puede
desactivarse sin romper la recarga base. Una recarga sin clips completos sería un objetivo posterior,
con trayectorias, contactos y poses preparados explícitamente, no una generación automática.

**Notas / bloqueos:** pendiente.

---

## Fuera del alcance inicial

- Integrar componentes o ensamblados de KINEMATION.
- Construir varias armas a la vez antes de validar la primera.
- Generar automáticamente agarres de dedos a partir de la geometría del arma.
- Mezclar este trabajo con una reestructuración de cámara o del IK de pies.
- Añadir sway, recoil, colisión avanzada o múltiples accesorios antes de completar la base.

## Registro de avances

| Fecha | Etapa | Avance / comprobación | Pendiente |
|---|---|---|---|
| 2026-11-09 | Planificación | Creado el seguimiento secuencial. No se han marcado pruebas del arma como completadas. | Elegir arma y comenzar etapa 1. |
| 2026-11-09 | 1 / Preparación de Aim | Revisada la escena guardada: AK12 bajo ancla independiente, IK asignado y clip de prueba en Animator. Usuario identifica la colocación como Aim. | Contrastar ajustes de manos no reflejados en los targets guardados; comprobar agarre y alineación visual. |
| 2026-11-09 | 1 / Agarre estático | Escena actualizada: grips e hints separados, arma desplazada y rifle como estado inicial. Captura externa/FPS confirma la colocación inicial. Los contenedores en cero son correctos. | Validate Setup, contactos finos, pruebas de mirada/giro/lean y persistencia al reiniciar. |
| 2026-11-09 | 1 completada | Usuario confirma finalizada la etapa de agarre. Se marcan sus tareas como completadas con esa fuente de validación. | Iniciar etapa 2, conservando la colocación actual como candidata a Aim. |
| 2026-11-09 | 2 / Implementación | Creados perfil, controlador y editor propios. Compilación Editor/runtime con Input System y comprobaciones aisladas de poses, cámara, columna y manos completadas. | Añadir el componente al arma real, capturar Aim/Idle y comprobar alineación y transiciones en Play Mode. |
| 2026-11-09 | 2.1 / Rebote de Aim | Añadido resorte amortiguado opcional al arma. Compilación Editor/runtime y pruebas aisladas completadas: límites, alternancia rápida, retorno al Aim, captura sin rebote, limpieza al desactivar y cámara intacta. | Activar Aim Motion en el perfil y validar sensación/agarre con el personaje real. |
| 2026-11-09 | 2.1 / Diagnóstico AK12 | Detectado Aim Motion ausente/desactivado en el asset guardado. Activado preset exagerado sin cambiar poses. Prueba con snapshot real: pico Aim 7.13 cm / 10.14 grados; preset suave 0.67 cm / 0.77 grados. Presets con Undo/Redo y persistencia comprobados; todas las suites aisladas pasan. | Verificar visualmente en Play y volver al botón de preset suave después del diagnóstico. |
| 2026-11-09 | 2.2 / Cámara Aim | Activados FOV adicional +3 y temblor breve en AK12; arma devuelta al preset suave. Compilación Editor/runtime y todas las suites aisladas pasan, con snapshot actual del perfil. Pico angular de cámara 0.126 grados, sin traslación; limpieza al desactivar arma/rig y coexistencia con FOV base y efectos previos comprobadas. | Validar comodidad, encuadre y agarre en Play Mode. |
| 2026-11-09 | 2.2 / Cámara visible | A petición del usuario, ampliado solo el rebote angular de cámara. Snapshot real: pico Aim 1.612 grados y salida 1.389 grados; vuelve a cero sin traslación. Conservados el FOV -30, TPS y poses del usuario. Todas las suites aisladas pasan. | Probar visibilidad/comodidad y reducir después los valores de diagnóstico. |
| 2026-11-09 | 2.3 / Tiempo compartido | Activada sincronización en AK12 a 0.18 s y una oscilación. Arma/FOV usan un reloj común y los rebotes terminan en cero. Compilación Editor/runtime y todas las suites aisladas pasan: 0/0.18/0.4 s, 30/60/144 Hz, inversiones y reactivación de efectos. Snapshot actual: FOV Aim 50, pico angular 1.119 grados. Conservados poses y FOV -10 del usuario. | Comprobar sensación en Play; ajustar solo Transition Duration si resulta demasiado rápida. |
| 2026-11-09 | 2.4 / Base de equipos | Definiciones/acciones propias, instancias independientes, recámara/cargador/reserva, autocarga/cerrojo/bombeo, melee/puños y cambios ordenados. Plantillas con acciones como subassets y guía creadas. Compilación Editor/runtime y todas las suites aisladas pasan; revisión independiente y correcciones de cadencia guardada y tiempo sobrante entre fases verificadas. No se modificó la escena ni AK12. | Elegir recursos compatibles y conectar presentación al grafo/IK existente; aún no hay cambio animado real ni daño. |
| 2026-11-09 | 2.5 / SampleScene AK12-puños | Escena original enlazada con dos slots, 1/2 para selección, clips propios Humanoid convertidos desde copias Generic, pose/acción dentro del grafo de ProceduralSpine y descenso/ascenso procedural del AK12. Solo se añadieron dos componentes a Player, se habilitaron entrada/canales de equipo y se fijó Always Animate para evitar culling de brazos. Transforms, grips, cámara, Controller y asset de poses conservados. Compilación, todas las regresiones y pruebas de la escena en preview pasan en LateUpdate y AnimationJob: puños/dedos, golpe, Aim mantenido al disparar, cambios repetidos, holster y limpieza. | Revisar contactos/sensación en Play. Recarga visual, piezas del arma y daño todavía pendientes. |
| 2026-11-10 | 2.6 / Recarga AK12 adaptada | Reutilizados los clips AK12 de brazos/piezas, con horneado Humanoid propio. R activa una sola secuencia de 4.5 s y muestra progreso; agarres se liberan, ancla acompaña muñeca animada y munición se confirma al final. Compilación Runtime/Editor, regresiones y preview real en ambos modos pasan: animación de brazos/cargador, seguimiento sin ciclo, recarga desde vacío, cancelación y desactivar/reactivar sin regalar munición ni acumular offsets. | Validación visual fina en Play; no hay todavía variante táctica corta, daño ni múltiples fases de extracción/inserción. |
| 2026-11-10 | 2.7 / Recarga izquierda simple | A petición del usuario, reemplazada la configuración anterior por C_Rifle_Reload_Humanoid nativo (3.3 s), máscara solo izquierda, derecha/IK en 1 y arma sin seguimiento ni clip de piezas. Solo cambiaron las dos acciones y se añadió una máscara; escena, poses y restantes acciones preservadas. Compilación y suite completa pasan en ambos modos: derecha en 1 durante la recarga, dedos derechos excluidos a igual tiempo de pose base, izquierda animada, ancla fija, cancelación, vacío y limpieza. | Usuario crea/ajusta sus clips izquierdos y contactos visuales. |
| 2026-11-10 | 2.8 / Prueba por arma | Prueba A/B/A en preview con definición, clip y grips B temporales. El grafo recibió cada clip correcto y se recuperaron los grips e instancias originales. Control a igual tiempo: recorrido izquierdo distinto 27.99/28.79 cm y dedo izquierdo 46.02 grados; derecha permanece en 1 y arma fija. Munición independiente, limpieza protegida y datos reales intactos. Compilación, suite completa y revisión independiente pasan. No se modificó runtime ni escena guardada. | Usuario comprueba visualmente AK12 con 1, disparar y R; B no se guardó como arma nueva. |
| 2026-11-10 | 2.9 / Mano relativa al arma | Añadido AK12 Left Reload Target bajo Weapon Holder y trayectoria local editable. IK izquierdo sigue ese objetivo; dedos conservan clip, derecha en 1 y arma independiente. Trayectoria y rotación invariantes con cámara real a 0/±45/±90, 7 muestras por acción, en ambos modos. Compilación, regresiones, controles por arma, objetivo sin clip, fallback y limpieza pasan; revisión sin bloqueos. Objetivo inicial acercado al cuerpo (Z local -0.1) sin alterar grips/poses originales. | En 4 de 35 muestras el ejemplo excede alcance físico; IK llega al punto alcanzable sin residuo adicional. Usuario calibra posición/recorrido y contactos en Play. |
| 2026-11-10 | 2.10 / Temblor de recarga | Aislada la máscara de brazo como origen del exceso corporal en LateUpdate. Asignada LeftReloadFingers a las dos recargas; solo cambian esas referencias. Compilación Runtime/Editor, suite completa y 36 variantes continuas a 30/60/144 Hz pasan: recarga igual a Idle, control de brazo reproduce el fallo, sin deriva ni efectos sobre ancestros. Revisión independiente sin hallazgos; assets desplegados idénticos a los validados y escena/cámara/poses intactas. Registro ReloadFingerOnlyFullValidation.log. | Usuario confirma apariencia y contactos en Play; con máscara nueva 2/35 muestras aún exceden alcance físico del brazo. |
| 2026-11-10 | 2.11 / Puños y cámara | Reproducido desfase de 11 grados y 73.124 mm respecto al hombro mirando +45. Añadido Follow Camera opcional para unarmed: conserva clips y completa giro del torso en brazos, sin mover hombros/cámara. Guardia y golpe pasan a 0/±45/±90 pitch, ±60 yaw y cuerpo girado, en ambos modos: error angular medido 0.000 grados y recorrido relativo al hombro 0.002 mm. Sin acumulación, deriva con reloj fijo ni cambios a dedos/cabeza/cámara; limpieza, escrituras externas y retorno a AK12 pasan. Compilación y suite completa, incluida estabilidad de recarga, pasan. Revisión sin bloqueos; orden de pruebas alineado al real. Escena, perfil y definición AK12 idénticos al respaldo. Registro FistsCameraFullValidation.log. | Usuario confirma encuadre y aspecto corporal en Play; no se garantiza posición fija en pantalla porque los hombros permanecen en el cuerpo. |
| 2026-11-10 | 2.12 / Manos editables | Añadidos puntos de guardia/destino por mano/slot y recetas por acción con Reach, posición, rotación y clip local opcional. SampleScene incluye Fists Hand Targets y derecha recta +16 cm, izquierda en guardia. Máscaras privadas solo con IK completo; FK parcial/cero y dedos se conservan. Validación nativa ambos modos, cámara extrema, curvas/clips, retorno/contacto/cancelación y 30/60/144 Hz pasa; error adicional a alcance físico 0.002 mm, una muestra limitada. Inspector probado con Undo/Redo, reparación sin sobrescritura e idempotencia. Compilación y suite completa pasan. Revisión sin bloqueos para puños; codo sin hint al cruzar peso 1 documentado. Verificación por secciones: solo hijo/referencias nuevos, cinco GameObjects, AK12/poses/transformaciones originales intactos. Logs EditableHandTargetsFullValidation y EditableHandTargetsAuthoringValidation. | Usuario calibra puntos/encuadre fuera de Play y revisa apariencia. Base de presentación, no lógica de combate de todas las herramientas. |
| 2026-11-10 | 2.13 / Cuchillo original | Creado Knife_Definition y prefab nativo como tercer equipo, tecla 3. Fuente Generic original horneada a rig local a 60 Hz (1.4333 s) con arma/manos/codos coordinados; dedos desde copia Humanoid propia y pose congelada del mismo ataque. Mezcla opcional respeta canales no animados y offsets de ajuste. Guardia relativa a ojos fuente y escala por brazo, sin retarget de torso ajeno. Compilación y suite completa pasan en ambos modos, incluyendo ±90, cancelación/desactivar/reactivar, dedos nativos, reloj, agarre original y 30/60/144 Hz. Error de rig 0 mm, agarre entre keys frente a original 0.568 mm, residuo IK 0.003 mm y ninguna muestra limitada. Revisión independiente sin bloqueos. Trasplante por secciones: solo nuevo hijo/slot/prefab; AK12, puños, cámara, grips y poses conservados. Assets desplegados idénticos a los probados. Log AuthoredKnifeFullValidation.log. | Usuario confirma apariencia/materiales/contactos en Play; daño, impactos y sonido no implementados. Guía AUTHORED_KNIFE_GUIDE.md. |
