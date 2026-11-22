---
key: towage-execution
title: Ejecución del remolque
roles: [Tug Master, Master, Tug Operations Coordinator, Shore Operations]
permissions: [mode.tug, tug.read, tug.write, tug.execution.verify]
related: [tug-readiness, towage-risk-briefing, towage-plan, tug-daily-log, tug-operation-history]
screenshots: []
reviewed: false
---
## Para qué sirve esta página
La consola de ejecución registra la **ejecución real** de un trabajo de remolcador enviado como una cronología: el inicio, las observaciones, las acciones **Suspender / Reanudar / Abortar / Completar** y la verificación posterior. Responde a la pregunta: "¿Cuándo empezó la operación, cuándo se conectó y se soltó el cabo de remolque, qué observaciones se registraron, en qué estado está la operación y se ha verificado el registro?" El inicio y las transiciones son registros humanos del Patrón del remolcador o del Capitán a bordo del remolcador que ejecuta; la consola nunca emite un GO.

## Quién la usa
- **Patrón del remolcador / Capitán:** para iniciar la operación y registrar la cronología de inicio/suspensión/reanudación/aborto/finalización y sus evidencias (13.03 §3).
- **Verificador (una persona con autoridad de verificación de la ejecución):** para verificar un registro completado o abortado, o rechazar su verificación (13.03 §1).
- **Coordinador de operaciones de remolcador / Operaciones en tierra:** para seguir la completitud de las evidencias y el historial de los trabajos completados; no pueden iniciar ni abortar una operación (13.03 §7).

## Antes de empezar
- Abra el remolcador desde **Operaciones de remolcador** y elija el trabajo en **Seleccione un trabajo de remolcador enviado**.
- Para iniciar, todo lo siguiente debe estar vigente: la asignación activa del remolcador, un **Plan de remolque** aprobado (el remolcador debe figurar en el plan), un **Riesgos y briefing de remolque** emitido y confirmado por todos los participantes, y un registro **GO** en el **Control de preparación operativa** vinculado a la evaluación actual.
- El inicio y las transiciones requieren una **Designación** registrada en el servidor (Patrón del remolcador o Capitán) para ese remolcador.

## Paso a paso
1. En la sección **Control de inicio**, lea la **Decisión de disponibilidad** y los **Bloqueos** de cada remolcador.
2. Si no hay bloqueos, lea el texto de confirmación como persona designada a bordo, elija su **Designación**, introduzca opcionalmente una **Posición de inicio** y pulse **Iniciar operación**.
3. Una vez abierta la ejecución, siga el **Estado**, la **Verificación** y los campos del registro (cabo de remolque conectado/soltado, carga de remolque máxima observada, comunicación, chigre, prueba de liberación de emergencia, minutos activos).
4. En **Registrar observación**, elija el tipo de observación (por ejemplo cabo de remolque conectado, carga de remolque, prueba de comunicación, meteorología, estado del chigre, prueba de liberación de emergencia, posición, evidencia, nota), introduzca los valores y pulse **Registrar observación**.
5. En **Acciones del patrón del remolcador**, elija la **Acción** (Suspender, Reanudar, Abortar, Completar) e introduzca su **Designación** y un **Motivo**. Abortar y completar requieren un motivo de al menos 20 caracteres y abren el campo **Posición final**; la finalización no se acepta sin posición final.
6. Si se muestran **Bloqueos para completar**, resuélvalos; por ejemplo, si el cabo de remolque sigue conectado, registre primero que se ha soltado.
7. Como verificador, escriba un motivo y pulse **Verificar registro** o **Rechazar verificación**.
8. Consulte cada evento en orden en la **Cronología**.

## Qué significan los estados
- **Activo / Suspendido / Completado / Abortado:** el estado de la operación. Completado no significa que el registro esté verificado.
- **Pendiente / Verificado / Rechazado:** el estado de la verificación. Completado ≠ Verificado; la verificación es el registro de otra persona.
- **Buena / Perdida / Superada / No superada / Condicional / Desconocido:** el resultado de las observaciones de comunicación, chigre y liberación de emergencia. Desconocido indica que no se registró nada; nunca sustituye a "Buena" ni a "Superada".
- **Eventos de seguridad — Desconocido:** esta consola no cuenta los eventos de seguridad; no se extrae ninguna conclusión de "sin eventos".
- **GO (registro humano) / NO GO (registro humano):** la decisión registrada por el Patrón del remolcador o el Capitán en el control de preparación.

## Lo que no puede hacer / precaución
- Los usuarios en tierra, la IA y las identidades del sistema no pueden iniciar ni abortar una operación. Una asignación desde tierra nunca prevalece sobre la decisión de seguridad a bordo del Patrón del remolcador (13.03 §7).
- Solo el Patrón del remolcador o el Capitán designado puede suspender, reanudar, abortar o completar. Abortar está siempre disponible mientras la operación está en curso.
- La persona que inició o terminó la operación no puede verificar el mismo registro; la verificación se realiza después de que la operación haya terminado.
- El registro solo admite añadidos; los eventos registrados nunca se eliminan ni se modifican.
- Sin conexión no se envía ningún comando. Las observaciones pueden ponerse en cola en **Operaciones sin conexión del remolcador**; el inicio, las transiciones y la verificación siguen requiriendo conexión.

## Si algo sale mal
- **Mensaje "The server listed missing or blocking items.":** resuelva los códigos de **Bloqueos** o **Bloqueos para completar** (por ejemplo plan no aprobado, confirmación del briefing pendiente, registro GO ausente o no vigente).
- **Mensaje "The record changed or the command conflicts.":** use **Cargar la versión más reciente** para revisar el registro.
- **Mensaje "The outcome is unknown.":** **Reintentar** vuelve a enviar el mismo comando idéntico.
- **Sin autorización:** compruebe su rol, el remolcador y el contexto de designación; si es necesario, contacte con el administrador de su inquilino (13.03 §10).

## Páginas relacionadas
