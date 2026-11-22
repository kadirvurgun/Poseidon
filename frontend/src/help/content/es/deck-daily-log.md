---
key: deck-daily-log
title: Diario de cubierta
roles: [Crew / OOW, Master]
permissions: [daily.deck.read, daily.deck.write, daily.deck.review]
related: [noon-report, engine-daily-log, port-activity-sof, offline-conflict-center, today]
screenshots: []
reviewed: false
---
## Para qué sirve esta página
El diario de cubierta conserva los **registros de guardia de cubierta** del buque seleccionado como borradores con la versión de fuente registrada. Responde a la pregunta: «¿Qué declaró la guardia para esta fecha y período sobre posición, rumbo, velocidad, tiempo, visibilidad, navegación, actividades portuarias y de carga, lastre, trabajos de cubierta, observaciones de seguridad y relevo, y se ha registrado una revisión para esta revisión exacta?». Una entrada guardada es una declaración sin verificar, no un diario aprobado.

## Quién la usa
- **Oficial de guardia (Crew / OOW):** para llevar el registro de guardia en tiempo real y en orden cronológico (13.03 §6).
- **Capitán o primer oficial:** para revisar el diario guardado y registrar esa revisión como un acto humano (13.03 §2). Solo un nombramiento con la función de capitán o de primer oficial puede registrar una revisión del diario de cubierta.

## Antes de empezar
- En la barra de contexto superior, seleccione la **empresa y el buque correctos**. Si no hay ningún buque seleccionado, la página muestra «Seleccione un buque».
- La lectura requiere `daily.deck.read`. Crear y guardar requiere `daily.deck.write` y derechos de edición sobre este buque; de lo contrario verá «Acceso de solo lectura. No se conceden la edición ni el guardado.».
- Registrar una revisión requiere `daily.deck.write` con derechos de edición sobre este buque, `daily.deck.review` y, además, un nombramiento vigente de capitán o de primer oficial para este registro exacto.

## Paso a paso
1. Elija el buque. **Borradores registrados** lista cada diario por fecha y revisión con «Borrador · revisión requerida».
2. Pulse **Nuevo borrador** para un **Nuevo borrador diario**, o abra un diario de la lista para **Editar borrador registrado**.
3. Rellene primero **Contexto de guardia** (**Fecha del diario**, **Guardia / período**, **Referencia del oficial de guardia**). La lista de oficiales solo ofrece personas disponibles para la fecha del diario declarada.
4. Rellene **Navegación y tiempo**, **Actividades portuarias y de carga** y **Trabajos de cubierta y relevo**. Para cada campo elija **Conocido**, **Desconocido** o **Ausente**.
5. Pulse **Guardar borrador** durante la guardia o justo después. Cada guardado añade una revisión a **Historial de revisiones registradas**; las revisiones anteriores no cambian.
6. El capitán o el primer oficial abre **Registro de revisión humana**, elige un **Nombramiento humano autorizado** y una **Entrada de revisión**, escribe una **Justificación de revisión** y pulsa **Registrar entrada de revisión**.

## Qué significan los estados
- **Borrador — declaraciones sin verificar / Declaración sin verificar:** declarado por una persona; no verificado.
- **Conocido / Desconocido / Ausente:** el estado de los datos del campo. Desconocido no significa «No», «Listo» ni «No aplicable».
- **Pendiente — revisión no registrada:** no existe ninguna revisión humana para esta revisión.
- **Metadatos humanos registrados — sin evaluar:** existe una entrada de revisión para esta revisión. No establece verificación, envío ni aprobación de seguridad del capitán.
- **Registrar declaración revisada (sin evaluar) / Devolver para corrección:** los dos tipos de entrada de revisión que puede registrar un revisor.
- **Revisión histórica:** la revisión pertenece a una revisión anterior, no al borrador actual.

## Lo que no puede hacer / precaución
- El sistema nunca revisa ni finaliza el diario. La revisión es un acto humano del capitán o del primer oficial nombrado (13.03 §2).
- Quien haya redactado cualquier revisión del diario no puede revisarlo. No se ofrece ningún nombramiento a un autor y la página indica «Usted redactó esta revisión del diario; un revisor independiente debe registrar la revisión.»; el servidor también rechaza esa revisión por separación de funciones.
- Mantenga las entradas en tiempo real y en orden cronológico; no reescriba una guardia antigua para que parezca nueva (13.03 §6).
- No modifique el registro revisado de otra persona; un guardado posterior es una nueva revisión y necesita una nueva revisión humana (13.03 §6).
- La captura sin conexión solo cubre los campos clasificados explícitamente. Un conflicto sin conexión nunca se fusiona automáticamente; no dé por hecho que su registro prevalece (13.03 §6).

## Si algo sale mal
- **«El registro ha cambiado. Sus entradas se conservan.»:** descarte y recargue explícitamente antes de un nuevo intento.
- **«Resultado del guardado desconocido»:** use **Reintentar comando guardado exacto**; no vuelva a escribir los campos.
- **Referencia del oficial no disponible:** deje el campo como Ausente o Desconocido; un nombre escrito a mano no puede sustituir la búsqueda.
- **«Se requiere resolución humana del conflicto»:** abra **Revisar conflicto** o el Centro de conflictos y elija explícitamente (13.03 §10).
- **No hay ningún nombramiento disponible:** compruebe su rol y el contexto del buque con el administrador del tenant (13.03 §10).

## Páginas relacionadas
