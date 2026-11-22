---
key: tug-assignment
title: Asignación del remolcador
roles: [Tug Operations Coordinator, Shore Operations, Tug Master]
permissions: [mode.tug, tug.read, tug.write, tug.plan.write]
related: [tug-job-board, tug-readiness, tug-crew-readiness, tug-equipment-readiness, towage-plan]
screenshots: []
reviewed: false
---
## Para qué sirve esta página
Esta página muestra las **propuestas de asignación de remolcadores** de un trabajo de remolcador enviado y sus **conflictos** con otros trabajos. Responde a la pregunta: "¿Qué remolcador se propuso para este trabajo, con qué función y franja horaria, entra en conflicto con otro trabajo y ha aceptado la propuesta el Patrón del remolcador?" Tierra propone, el Patrón del remolcador confirma; la página no toma ninguna decisión de despacho ni de liberación. El aviso de la parte superior lo indica: "The system never issues GO."

## Quién la usa
- **Coordinador de operaciones de remolcador / Operaciones en tierra:** para hacer una propuesta de asignación tras revisar los conflictos y la preparación en el tablero de trabajos (13.03 §7).
- **Patrón del remolcador:** para confirmar en contexto una propuesta para su propio remolcador y aceptarla o rechazarla (13.03 §3).

## Antes de empezar
- Abra el remolcador desde **Operaciones de remolcador**; si no hay ningún remolcador seleccionado, la página le remite allí.
- Proponer requiere acceso de escritura de remolcador y de escritura del plan; el trabajo debe estar **Enviado**.
- Responder requiere una **Designación** registrada en el servidor (Patrón del remolcador o Capitán) para ese remolcador. La persona que hizo una propuesta no puede responder a ella.

## Paso a paso
1. Abra el número de trabajo en **Seleccione un trabajo de remolcador enviado**. El enlace **Asignaciones y preparación** del detalle del trabajo también abre esta página.
2. En las propuestas existentes, lea **Estado**, **Acuse del capitán de remolcador**, **Función**, **Inicio previsto** y **Fin previsto**.
3. En **Conflictos**, consulte los conflictos de tiempo y de recursos con otros trabajos, su distintivo de resultado y su código de motivo.
4. Para una nueva propuesta, elija **Remolcador**, **Inicio previsto**, **Fin previsto** y **Función** (principal, apoyo, escolta) en el formulario **Proponer asignación** y pulse **Proponer asignación**.
5. Como Patrón del remolcador, elija su **Designación**, añada un **Motivo** si es necesario y pulse **Aceptar** o **Rechazar**. Para rechazar se requiere un motivo.
6. Para retirar una propuesta, introduzca un motivo y pulse **Retirar la propuesta**.
7. Use los enlaces **Control de preparación operativa** y **Tripulación y competencia de remolque** de cada propuesta para pasar a las pantallas de preparación.

## Qué significan los estados
- **Propuesto / Retirado:** el ciclo de vida de la propuesta. Propuesto no significa que el remolcador haya sido despachado.
- **Pendiente / Aceptado / Rechazado:** la respuesta del Patrón del remolcador a la propuesta. Aceptado confirma el alcance de la propuesta; no es una decisión GO.
- **Superado / Fallido / Desconocido:** el resultado de una comprobación de conflictos. Desconocido indica que no había datos de origen suficientes para la comprobación; nunca significa "sin conflicto".
- **No se observan conflictos; los datos pueden estar incompletos.:** no se observó ningún conflicto, pero esto no es un resultado completo de idoneidad.
- **La solicitud cambió después de esta propuesta; se requiere una nueva propuesta.:** la solicitud del trabajo cambió después de la revisión en la que se basaba la propuesta.

## Lo que no puede hacer / precaución
- Una propuesta de asignación desde tierra nunca prevalece sobre la decisión de seguridad a bordo del Patrón del remolcador (13.03 §3, §7).
- Quien propone no puede aceptar su propia propuesta; la respuesta corresponde únicamente a la persona designada para ese remolcador.
- No se puede hacer ninguna propuesta para un trabajo en borrador o cancelado.
- Los comandos están desactivados sin conexión.

## Si algo sale mal
- **Mensaje "The outcome is uncertain":** **Reintentar** envía el mismo comando; no cree una propuesta nueva.
- **Mensaje "The record changed":** use **Recargar** para revisar el registro actual y vuelva a actuar.
- **"You are not authorized for this action." / "You do not have access to this record.":** compruebe su rol, el remolcador y el contexto de designación; si es necesario, contacte con el administrador de su inquilino (13.03 §10).
- **Conflicto inesperado:** anote el código de motivo y revíselo con el responsable del otro trabajo.

## Páginas relacionadas
