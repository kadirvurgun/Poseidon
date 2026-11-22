---
key: noon-report
title: Informe de mediodía
roles: [Master, Crew / OOW]
permissions: [daily.noon.read, daily.noon.write, daily.noon.review]
related: [deck-daily-log, engine-daily-log, fuel-bunker-log, port-activity-sof, offline-conflict-center, today]
screenshots: []
reviewed: false
---
## Para qué sirve esta página
La página del informe de mediodía conserva las **declaraciones diarias de mediodía** del buque seleccionado como borradores con la versión de fuente registrada. Responde a la pregunta: «¿Qué declaramos sobre la hora del informe, el viaje, la posición, la distancia, el tiempo, la carga, las horas de maquinaria, el combustible y el ROB, y ha registrado el capitán una revisión de esta revisión exacta?». Un borrador guardado es una declaración sin verificar. Guardarlo no establece la aprobación del capitán ni del jefe de máquinas, y la página no extrae de él ninguna conclusión sobre emisiones, aseguramiento del combustible ni disponibilidad.

## Quién la usa
- **Oficial que prepara el informe (Crew / OOW):** para rellenar y guardar el borrador de mediodía para el buque y la fecha correctos (13.03 §6).
- **Capitán:** para revisar el informe guardado y registrar esa revisión como un acto humano (13.03 §2). Solo un nombramiento con la función de capitán puede registrar una revisión del informe de mediodía.

## Antes de empezar
- En la barra de contexto superior, seleccione la **empresa y el buque correctos**. Si no hay ningún buque seleccionado, la página muestra «Seleccione un buque».
- La lectura requiere `daily.noon.read`. Crear y guardar borradores requiere `daily.noon.write` y derechos de edición sobre este buque; sin ellos verá «Acceso de solo lectura. No se conceden la edición ni el guardado.».
- Registrar una revisión requiere `daily.noon.write` con derechos de edición sobre este buque, `daily.noon.review` y, además, un nombramiento vigente de capitán para este registro exacto.

## Paso a paso
1. Elija el buque. La lista **Borradores registrados** muestra cada informe por fecha y revisión con la insignia «Borrador · revisión requerida». Use **Borradores anteriores** / **Más borradores** para cambiar de página.
2. Pulse **Nuevo borrador** para abrir **Nuevo borrador diario**, o abra un informe de la lista para **Editar borrador registrado**.
3. Rellene los grupos (**Informe y viaje**, **Posición y actividad**, **Tiempo y carga**, **Maquinaria, combustible y ROB**). Para cada campo elija un **Estado de datos**: **Conocido**, **Desconocido** o **Ausente**. Se pueden guardar borradores incompletos.
4. Pulse **Guardar borrador**. La página confirma «Borrador guardado. La revisión humana y la aprobación operativa siguen siendo independientes.». Cada guardado añade una revisión en **Historial de revisiones registradas**.
5. En un informe guardado, el capitán abre **Registro de revisión humana**, elige un **Nombramiento humano autorizado** y una **Entrada de revisión** («Registrar declaración revisada (sin evaluar)» o «Devolver para corrección»), escribe una **Justificación de revisión** y pulsa **Registrar entrada de revisión**.
6. Si hay que registrar combustible o un evento portuario, use **Usar fuente operativa guardada** y **Abrir registro de combustible desde fuente guardada** o **Abrir evento portuario desde fuente guardada**. Las cantidades, las unidades y las aprobaciones no se copian.

## Qué significan los estados
- **Borrador — declaraciones sin verificar / Declaración sin verificar:** los valores los declara una persona; nadie los ha verificado.
- **Conocido / Desconocido / Ausente:** el estado de los datos de un campo. Desconocido no significa «No», «Listo» ni «No aplicable», y Ausente no es cero.
- **Pendiente — revisión no registrada:** no se ha registrado ninguna revisión humana para esta revisión.
- **Metadatos humanos registrados — sin evaluar / Registrado — sin evaluar:** existe una entrada de revisión del capitán para la revisión mostrada. El registro de la revisión no establece verificación, envío ni aprobación de seguridad del capitán.
- **Revisión histórica:** la entrada de revisión pertenece a una revisión anterior y no revisa el borrador guardado actual.

## Lo que no puede hacer / precaución
- El sistema nunca revisa ni finaliza el informe. Registrar una revisión es un acto humano del capitán nombrado (13.03 §2).
- Quien haya redactado cualquier revisión del diario no puede revisarlo. No se ofrece ningún nombramiento a un autor y la página indica «Usted redactó esta revisión del diario; un revisor independiente debe registrar la revisión.»; el servidor también rechaza esa revisión por separación de funciones. Si el capitán redactó el informe, un revisor independiente con autoridad de revisión debe registrar igualmente la revisión.
- Un nuevo guardado crea una nueva revisión; la revisión humana anterior permanece en su propia revisión. Vuelva a revisar después de los cambios.
- Una referencia escrita en texto libre no puede sustituir una búsqueda autorizada; deje el campo como Ausente o Desconocido.
- Sin conexión, solo los campos clasificados explícitamente pueden ir a la cola cifrada. Los campos de revisión, aprobación y verificación están excluidos.

## Si algo sale mal
- **«El registro ha cambiado. Sus entradas se conservan.»:** alguien guardó una versión más reciente. Descarte y recargue explícitamente antes de un nuevo intento.
- **«Resultado del guardado desconocido»:** los campos quedan bloqueados; use **Reintentar comando guardado exacto** y no vuelva a escribir los datos.
- **«No hay ningún nombramiento humano vigente autorizado por la fuente disponible para este registro.»:** compruebe su rol y el contexto del buque con el administrador del tenant (13.03 §10).
- **«Se requiere resolución humana del conflicto»:** una captura sin conexión entra en conflicto con el servidor. Abra **Revisar conflicto** o el Centro de conflictos y elija explícitamente; nada se fusiona automáticamente (13.03 §10).
- **Fuentes diarias no disponibles o acceso denegado:** use **Reintentar lectura de fuentes**; si persiste, comunique al soporte la hora y los pasos realizados.

## Páginas relacionadas
