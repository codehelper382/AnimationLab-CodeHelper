# BASE CORRECTA: movimiento del arma y agarres

## Qué contiene la toma

Análisis de `AnimationLab/Authored/BASE CORRECTA.asset`, sin modificar el original:

- **3,33 s**, final de referencia **F200**.
- **4 controles del arma:** F0, F40, F56 y final. Ya existe movimiento principal: retracción de unos 10–12 cm, elevación de 5 cm y giros de hasta −25°/−15°.
- El tramo F56–final dura unos **2,4 s**. No falta toda la animación: faltan pequeñas acciones secundarias que eviten una vuelta tan uniforme.
- **206 muestras de muñeca izquierda**, **2 tramos** de contacto con `Mag`.
- **7 poses de dedos:** F0, F40, F56, F100, F150, F198 y final.

## Qué ocurre en F150

La pose guardada en F150 no coincide con F0: una de las articulaciones difiere aproximadamente **55,9°**. Coincide con las poses de F56/F100/F198. La pose inicial regresa al final.

Además, hay un **fundido global de salida de 0,08 s, unos 4,8 fotogramas**. Ese fundido afecta al resultado final aunque las poses de dedos se mantengan entre sus claves.

La causa principal de que la mano “se salga” después de F150 es otra: el **contacto con el cargador termina en F150**. Los dos tramos enlazados son F56→F100 y F100→F150; a partir de F150 no hay otro punto de la pieza con contacto, así que la muñeca deja de seguir el cargador y vuelve a la guardia durante los ~50 fotogramas restantes. El cargador y el arma también vuelven, pero por curvas distintas; por eso se percibe un deslizamiento suave. Los dedos se mantienen hasta F198 y vuelven a la base en F198–F200. El fundido de 0,08 s termina de suavizar la salida.

Había otra confusión de interfaz: en un fotograma sin clave, el editor mostraba `Suave` como configuración por defecto y el selector podía conservar el nombre de un tipo elegido antes. Eso no demostraba qué pose estaba guardada.

Ahora se distingue **clave guardada / sin clave / destino sin guardar**. Se indican las poses futuras y la diferencia respecto de F0; al navegar sin clave no aparece una transición nueva.

### Para volver realmente a la base desde F150

1. Abre la toma y elige la pista de dedos correspondiente.
2. Ve a F150 y pulsa **Volver al agarre guardado en F0**.
3. Configura cómo quieres llegar a esa base en F150.
4. Pulsa **Mantener este agarre hasta el final…**. Guarda la pose y quita solo las poses posteriores de dedos, con Undo; no borra muñeca ni piezas.
5. Si además quieres que la **mano siga el cargador** después de F150, pulsa **Mantener mano y pieza hasta F 195 (inicio del fundido)…**. Añade un punto de la pieza con el último contacto y recalcula la muñeca a 60 Hz; no cambia dedos, arma ni la otra mano.
6. Guarda como nueva copia.

El fundido global de la acción se conserva. Mantener un agarre arbitrario no desactiva ese fundido ni modifica otras pistas. El botón de contacto se detiene al inicio del fundido porque el solver de contacto necesita peso completo; los últimos fotogramas siguen atenuándose con el fundido global.

## Botón de movimiento natural

**Animar → Arma → Probar movimiento natural del arma**:

- Respiración leve y reacciones amortiguadas según los puntos de arma/piezas.
- Movimiento determinista, sin ruido aleatorio ni sacudidas de cámara.
- Adición limitada a **0,6 cm y 1,8° como máximo**, escalada por intensidad.
- Respeta las posiciones/giros de los controles originales; conserva la base para volver atrás.
- Recalcula los contactos mano–pieza con el solver existente y compila curvas normales.
- **Antes / Después / Intensidad / Aceptar / Cancelar**, sin modificar la toma hasta aceptar.
- **Quitar capa natural** restaura el recorrido base. Si después editaste manualmente las curvas, se rechaza la restauración para no sobrescribir esos cambios.

Es una ayuda de autoría, no simulación física ni garantía de acabado realista. Revisa contactos, alcance, encuadre y primera persona; el fixture conserva artefactos de malla.

## Mejoras recomendadas para simplificar el manejo

Estas son propuestas, no funciones adicionales ya implementadas:

1. **Modo sencillo:** mostrar solo pose actual, siguiente objetivo y mantener/adaptar; esconder controles técnicos hasta necesitarlos.
2. **Una pista visual de conjunto mano + pieza**, con los agarres como bloques, en lugar de editar mentalmente varias pistas.
3. **Asistente de recarga en tres pasos:** colocar contactos, definir recorrido común y ajustar agarres/tiempos.
4. **Marcadores con intención:** “agarrar”, “extraer”, “insertar”, “soltar”, con avisos si una pose futura o un fundido contradice un mantenimiento.
5. **Comparación y diagnóstico en un clic:** diferencias frente a la base, contactos fuera de alcance y cambios pendientes claramente destacados.
6. **Un solo “mantener agarre completo”:** unificar el mantenimiento de dedos y el de contacto mano–pieza en una acción con una vista previa de qué canales cambia.
7. **Aviso de final de contacto:** mostrar en la barra de tiempo dónde termina cada enlace mano–pieza, no solo en el panel de dedos.
8. **Presets de salida:** elegir “sostener hasta el final”, “soltar en F…”, “volver a la guardia” sin tener que combinar varios botones.
