---
key: engine-daily-log
title: Diario de máquinas
roles: [Chief Engineer, Crew / OOW]
permissions: [daily.engine.read, daily.engine.write, daily.engine.review]
related: [noon-report, deck-daily-log, fuel-bunker-log, operational-downtime, work-order-center, defects, offline-conflict-center]
screenshots: []
reviewed: false
---
## Para qué sirve esta página
El diario de máquinas conserva los **registros de guardia de máquinas** del buque seleccionado como borradores con la versión de fuente registrada. Responde a la pregunta: «¿Qué declaró la guardia de máquinas sobre el estado y las horas de los motores, el consumo de combustible y aceite lubricante, el agua dulce, las mediciones críticas, las alarmas, los defectos, el abastecimiento de combustible, la sentina y los controles medioambientales, y ha registrado el jefe de máquinas una revisión de esta revisión exacta?». Una entrada guardada es una declaración sin verificar. Los datos del borrador no se convierten en conclusiones sobre emisiones, aseguramiento del combustible ni disponibilidad.

## Quién la usa
- **Maquinista de guardia (Crew / OOW / Engineer):** para llevar el registro de guardia de máquinas en tiempo real y en orden cronológico (13.03 §6).
- **Jefe de máquinas:** para revisar el diario guardado, vincularlo a órdenes de trabajo y defectos y registrar la revisión como un acto humano (13.03 §5). Solo un nombramiento con la función de jefe de máquinas puede registrar una revisión del diario de máquinas.

## Antes de empezar
- En la barra de contexto superior, seleccione la **empresa y el buque correctos**.
- La lectura requiere `daily.engine.read`; guardar requiere `daily.engine.write` y derechos de edición sobre este buque. Registrar una revisión requiere `daily.engine.write` con derechos de edición sobre este buque, `daily.engine.review` y, además, un nombramiento vigente de jefe de máquinas para este registro exacto.
- Vincular órdenes de trabajo o defectos requiere también acceso de lectura a esos registros.

## Paso a paso
1. Elija el buque. **Borradores registrados** lista cada diario por fecha y revisión con «Borrador · revisión requerida».
2. Pulse **Nuevo borrador** o abra un diario de la lista para **Editar borrador registrado**.
3. Rellene **Contexto de guardia de máquinas**, **Estado y horas de motores**, **Consumo y producción**, **Mediciones, alarmas y defectos** y **Contexto de trabajo y medioambiente**. Para cada campo elija **Conocido**, **Desconocido** o **Ausente**.
4. Pulse **Guardar borrador**. Cada guardado añade una revisión a **Historial de revisiones registradas**.
5. En un diario guardado, use **Órdenes de trabajo y defectos**: elija un **Tipo de registro** y un **Registro** y pulse **Vincular**. Un vínculo fija una revisión exacta; **Quitar vínculo** lo elimina.
6. Si es necesario, abra **Abrir registro de combustible desde fuente guardada** o **Abrir registro de inactividad desde fuente guardada**. Las cantidades y las unidades no se copian.
7. El jefe de máquinas abre **Registro de revisión humana**, elige un **Nombramiento humano autorizado** y una **Entrada de revisión**, escribe una **Justificación de revisión** y pulsa **Registrar entrada de revisión**.

## Qué significan los estados
- **Declaración sin verificar / Conocido / Desconocido / Ausente:** los valores declarados y su estado de datos. Desconocido no significa «No», «Listo» ni «No aplicable».
- **Pendiente — revisión no registrada:** no existe ninguna revisión humana para esta revisión.
- **Metadatos humanos registrados — sin evaluar:** existe una entrada de revisión del jefe de máquinas para esta revisión. No establece verificación ni la firma del jefe de máquinas.
- **Revisión actual / Existe una revisión más reciente:** si una orden de trabajo o un defecto vinculado ha cambiado desde que se vinculó. Un vínculo solo indica el linaje; no verifica, cierra ni aprueba ese registro.
- **búsqueda no disponible — DESCONOCIDO:** el tipo de registro vinculado no se puede leer con su acceso.

## Lo que no puede hacer / precaución
- El sistema nunca revisa ni finaliza el diario. La revisión es un acto humano del jefe de máquinas nombrado (13.03 §5).
- Quien haya redactado cualquier revisión del diario no puede revisarlo. No se ofrece ningún nombramiento a un autor y la página indica «Usted redactó esta revisión del diario; un revisor independiente debe registrar la revisión.»; el servidor también rechaza esa revisión por separación de funciones.
- Mantenga las entradas en tiempo real y en orden cronológico; no modifique el registro revisado de otra persona (13.03 §6).
- Vincular aquí un defecto no registra una restricción ni lo cierra; hágalo en la página del defecto o de la orden de trabajo (13.03 §5).
- Los vínculos requieren una conexión en línea vigente; sin conexión no se vincula nada. Los conflictos sin conexión nunca se fusionan automáticamente.

## Si algo sale mal
- **«La entrada del diario ha cambiado. Recárguela antes de cambiar los vínculos.»:** recargue y vuelva a vincular.
- **«Resultado del guardado desconocido»:** use **Reintentar comando guardado exacto**; no vuelva a escribir los datos.
- **«Se requiere resolución humana del conflicto»:** abra **Revisar conflicto** o el Centro de conflictos y elija explícitamente (13.03 §10).
- **No hay ningún nombramiento disponible o se ha denegado el acceso:** compruebe su rol y el contexto del buque con el administrador del tenant (13.03 §10).

## Páginas relacionadas
