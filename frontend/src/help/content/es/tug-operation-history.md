---
key: tug-operation-history
title: Historial de remolcadores
roles: [Tug Operations Coordinator, Shore Operations, Tug Master]
permissions: [mode.tug, tug.read]
related: [towage-execution, tug-daily-log, tug-job-board, tug-operations-offline]
screenshots: []
reviewed: false
---
## Para qué sirve esta página
El historial de operaciones muestra el **historial de solo lectura** de los trabajos de remolcador, las ejecuciones, los diarios y las evidencias. Responde a la pregunta: "¿Qué trabajos terminaron con qué resultado, se verificaron las ejecuciones, existe un registro de evidencias y qué diarios pertenecen a estos trabajos?" Los registros cancelados y abortados siguen visibles; no se elimina ni se reetiqueta nada.

## Quién la usa
- **Coordinador de operaciones de remolcador / Operaciones en tierra:** para seguir la completitud de las evidencias, los eventos y los registros históricos de los trabajos completados (13.03 §7).
- **Patrón del remolcador:** para ver los trabajos pasados y las cronologías de ejecución de su propio remolcador (13.03 §3).

## Antes de empezar
- Abra el remolcador desde **Operaciones de remolcador**; sin un remolcador seleccionado, la página muestra "Select a tug vessel to continue."
- Basta con el acceso de lectura de remolcador; la página no modifica ningún dato.
- El historial se lee del servidor; no se puede actualizar sin conexión.

## Paso a paso
1. En **Filtros**, indique opcionalmente **ID del remolcador**, **Desde (fecha UTC)**, **Hasta (fecha UTC)** y **Resultado**, y pulse **Aplicar filtros**. El intervalo de fechas es como máximo de 400 días.
2. En cada fila de la tabla, lea **Trabajo** (número de trabajo, buque asistido, tipo de trabajo, estado del plan y del briefing), **Hora de actividad**, **Resultado** y **Evidencia**.
3. En **Ejecuciones**, consulte el estado de cada ejecución, el estado de verificación, la duración, el registro del Capitán y el número de entradas de evidencia; use **Abrir cronología de ejecución** para ir a la consola de ejecución.
4. Use los enlaces de **Registros diarios** para abrir el diario correspondiente. La nota "not linked by the log" indica que el diario no enlaza esta ejecución.
5. Use **Cargar más** para registros más antiguos. **Leído a las** muestra cuándo se leyeron los datos.

## Qué significan los estados
- **No iniciado / En curso / Completado (registrado) / Abortado / Cancelado / Mixto:** el resultado global del trabajo. Completado (registrado) no significa que el trabajo se haya verificado.
- **Verificado, evidencia registrada / Verificado, sin entrada de evidencia / Verificación rechazada / Evidencia sin verificar / Desconocido:** el estado de las evidencias y de la verificación.
- **verificación pendiente / verificado / verificación rechazada:** el estado de verificación de una ejecución individual.
- **Ninguno:** no existe ningún registro vinculado para este trabajo. Esto no significa que no se necesitara ningún registro.

## Lo que no puede hacer / precaución
- En esta página no se edita, elimina ni verifica ningún registro.
- Sin eliminaciones silenciosas: los trabajos cancelados y las ejecuciones abortadas o rechazadas permanecen en el historial.
- Completado ≠ Verificado: la verificación es el registro de otra persona.
- Los datos mostrados reflejan el estado en **Leído a las**; no se actualizan sin conexión.

## Si algo sale mal
- **Mensaje "The filter is not valid":** compruebe que el ID del remolcador es un UUID y que el intervalo de fechas no supera los 400 días.
- **Mensaje "The history could not be loaded.":** compruebe su conexión y pulse **Reintentar**.
- **"You do not have access to this tug scope.":** compruebe su rol y el contexto del remolcador; si es necesario, contacte con el administrador de su inquilino (13.03 §10).
- **El registro no se puede verificar:** compruebe la referencia de la evidencia y su fuente en la cronología de ejecución; contacte con el verificador (13.03 §10).

## Páginas relacionadas
