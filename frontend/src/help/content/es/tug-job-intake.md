---
key: tug-job-intake
title: Recepción de trabajos
roles: [Tug Operations Coordinator, Shore Operations]
permissions: [mode.tug, tug.read, tug.write]
related: [tug-job-board, tug-command, tug-assignment, towage-plan]
screenshots: []
reviewed: false
---
## Para qué sirve esta página
La recepción de trabajos estructura una nueva **solicitud de trabajo de remolcador como borrador** en el contexto de remolcador seleccionado. Responde a la pregunta: "Para este trabajo, ¿cuáles son el buque asistido, el tipo de trabajo, la franja horaria, la ubicación y los recursos necesarios (número de remolcadores, tiro a punto fijo)?" La solicitud guardada es una declaración en borrador; no es una asignación operativa ni una aprobación de idoneidad, permiso o seguridad.

## Quién la usa
- **Coordinador de operaciones de remolcador / Operaciones en tierra:** para estructurar la solicitud e introducir el buque asistido, el tipo de trabajo, la franja horaria y la capacidad necesaria (13.03 §7).

## Antes de empezar
- En la barra de contexto superior, seleccione la **empresa y el remolcador correctos**.
- Necesita acceso de escritura de remolcador. Sin él, el formulario no se abre y se muestra "El acceso no está disponible."
- Los borradores incompletos se pueden guardar. Los datos operativos obligatorios y la revisión autorizada deben resolverse antes del envío.

## Paso a paso
1. En **Solicitud y buque asistido**, complete **Fuente de la solicitud**, **Referencia del cliente**, **Buque asistido**, **IMO del buque asistido**, **Tipo de trabajo**, **Prioridad** y **Responsable de la solicitud**. Las listas sin elección quedan en "Desconocido".
2. En **Hora y ubicación solicitadas**, introduzca **Inicio solicitado (UTC)**, **Fin solicitado (UTC, condicional)**, **Zona de operación**, **Punto de recogida** y **Punto de destino**. Las horas están en UTC.
3. En **Necesidades de recursos y notas**, introduzca **BP requerida (toneladas, decimal exacto)**, **Número de remolcadores requerido**, **Requisitos especiales** y **Notas**.
4. Pulse **Guardar borrador**. Si se guarda correctamente, se abre el detalle del trabajo con "Draft saved. Operational approval remains unavailable."
5. Para abandonar sus cambios, use **Descartar cambios**.
6. En el detalle del trabajo, use **Enviar solicitud** para registrar la solicitud para su planificación. Si faltan datos obligatorios, el envío se bloquea y se enumeran los campos que faltan.

## Qué significan los estados
- **Borrador:** la solicitud está guardada y se puede editar; todavía no se ha enviado.
- **Enviado:** la solicitud está registrada para su planificación. No es una aprobación, una asignación ni una decisión de preparación.
- **Cancelado:** la solicitud está cerrada; no se puede reiniciar.
- **Desconocido:** no se eligió ningún valor para el campo. Desconocido no es un valor por defecto y nunca sustituye a "No" ni a "No aplicable".
- **Compruebe este valor.:** el servidor rechazó este campo; corríjalo y vuelva a guardar.

## Lo que no puede hacer / precaución
- Esta página no asigna ningún remolcador y nunca declara el trabajo listo. La propuesta de asignación es un paso aparte y nunca prevalece sobre la decisión de seguridad a bordo del Patrón del remolcador (13.03 §7).
- Una solicitud no se puede enviar mientras falten datos obligatorios; un borrador incompleto sí se puede guardar.
- Los campos de una solicitud enviada no se pueden modificar desde este formulario; una solicitud cancelada no se puede reiniciar, cree una nueva en su lugar.
- Sin conexión, el borrador se conserva solo en esta página y no se puede guardar.

## Si algo sale mal
- **Mensaje "Save outcome uncertain":** se conserva el intento exacto; tras volver a conectarse, use **Reintentar** para enviar el mismo comando. Descartar los datos locales no cancela un comando ya enviado.
- **Mensaje "Conflict":** su borrador se conserva; descártelo y recargue antes de un nuevo intento.
- **Comprobar campos:** corrija los campos marcados.
- **El acceso no está disponible:** compruebe su rol y el contexto del remolcador; si es necesario, contacte con el administrador de su inquilino (13.03 §10).

## Páginas relacionadas
