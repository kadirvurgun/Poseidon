---
key: tug-job-board
title: Tablero de trabajos
roles: [Tug Operations Coordinator, Shore Operations, Tug Master]
permissions: [canRead, canWrite]
related: [tug-command, tug-job-intake, tug-assignment, tug-readiness, today]
screenshots: [1, 2, 3, 4, 5, 7]
reviewed: false
---
## Para qué sirve esta página
El tablero de trabajos enumera las **solicitudes de trabajo de remolcador en borrador** del contexto de remolcador seleccionado para su planificación. Responde a la pregunta: "¿Qué solicitudes existen, qué franja horaria piden, cuántos remolcadores y cuánto tiro a punto fijo (BP) necesitan, y qué falta todavía para la planificación?" El tablero solo muestra declaraciones en borrador; no es una asignación operativa ni una aprobación de idoneidad, permiso o seguridad.

## Quién la usa
- **Coordinador de operaciones de remolcador / Operaciones en tierra:** para ver las solicitudes, seguir la información de conflictos y de preparación y preparar una propuesta de asignación (13.03 §7).
- **Patrón del remolcador:** para ver con antelación el trabajo previsto para su remolcador (13.03 §3).

## Antes de empezar
- En la barra de contexto superior, seleccione la **empresa y el remolcador correctos**. El tablero solo muestra las solicitudes de este contexto.
- Necesita acceso de lectura para ver el tablero. Crear una solicitud requiere acceso de escritura; sin él, no se muestra el botón "Nueva solicitud".

## Paso a paso
1. En el resumen superior, consulte el número de solicitudes de esta página, los trabajos con un bloqueo registrado, los que esperan revisión de preparación y los que tienen la preparación desconocida. **Los recuentos cubren solo esta página.**
2. Si es necesario, escriba un número de trabajo o el nombre de un buque asistido en **Buscar solicitudes**, elija los filtros **Prioridad**, **Estado** y **Ventana de tiempo** y pulse **Aplicar filtros**.
3. En cada fila, lea la franja solicitada (UTC), las asignaciones en borrador, el número de remolcadores requerido, el BP requerido frente a la capacidad de planificación, la preparación y el conflicto operativo.
4. Abra **Motivo / siguiente acción** en una fila para ver las referencias de los remolcadores asignados, el alcance de la comparación y los motivos registrados.
5. Use **Abrir trabajo** para ir a los detalles de la solicitud.
6. Para una nueva solicitud, pulse **Nueva solicitud** para abrir **Recepción de trabajos** (requiere acceso de escritura).
7. Use **Actualizar tablero** para volver a leer los datos y **Anterior** / **Siguiente** para las demás páginas.

## Qué significan los estados
- **Borrador / Enviado / Cancelado:** el estado del ciclo de vida de la solicitud. Enviar registra la solicitud para su planificación; no es una aprobación, una asignación ni una decisión de preparación.
- **Bloqueado:** la solicitud tiene un bloqueo registrado. El motivo figura en "Motivo / siguiente acción".
- **Revisión requerida:** la información de preparación espera la revisión de una persona autorizada.
- **Desconocido:** no hay datos de origen suficientes para la preparación o el conflicto. Desconocido no significa "Listo" ni "sin conflicto", y el tablero no extrae de ello ninguna conclusión de preparación.
- **Revisión humana autorizada requerida / Revisión de contacto de límites requerida:** dos solicitudes pueden solaparse en el tiempo o tocarse de extremo a extremo. Decide una persona.
- **BP del perfil (sin verificar):** el tiro a punto fijo mostrado es un valor declarado, no medido ni verificado.

## Lo que no puede hacer / precaución
- El tablero nunca declara una solicitud "lista" ni "segura". Una propuesta de asignación desde tierra no elimina la autoridad de seguridad a bordo del Patrón del remolcador (13.03 §7).
- Los campos de una solicitud enviada o cancelada no se modifican desde el tablero; una solicitud cancelada no se puede reiniciar, cree una nueva en su lugar.
- El filtro de ventana de tiempo usa la hora del servidor. Las solicitudes sin inicio permanecen en la lista como "Desconocido".
- Los datos mostrados sin conexión pueden ser antiguos y el tablero no se puede actualizar.

## Si algo sale mal
- **Tablero actual no disponible:** compruebe su conexión y use "Actualizar tablero".
- **No hay solicitudes en borrador:** si aplicó filtros, pruebe otra búsqueda u otra prioridad; las solicitudes nuevas aparecen en este contexto.
- **Acceso no disponible:** compruebe su rol y el contexto del remolcador; si es necesario, contacte con el administrador de su inquilino (13.03 §10).
- **Bloqueo o revisión inesperados:** abra "Motivo / siguiente acción" y revise los motivos registrados con el responsable de la solicitud.

## Páginas relacionadas
