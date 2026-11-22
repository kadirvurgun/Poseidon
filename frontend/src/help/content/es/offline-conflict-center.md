---
key: offline-conflict-center
title: Centro de conflictos materiales
roles: [Crew, Tug Master, Master, Chief Engineer]
permissions: [directory.read, crew.read, mode.tug, tug.read, daily.noon.read, daily.deck.read, daily.engine.read, workorder.read, certificate.read]
related: [tug-operations-offline, tug-daily-log, today]
screenshots: []
reviewed: false
---
## Para qué sirve esta página
El centro de conflictos materiales enumera, para el buque o remolcador seleccionado, los **conflictos materiales entre sus cambios sin conexión y el registro actual del servidor**, y permite que una persona los resuelva con una decisión motivada. Responde a: "¿Cuáles de mis cambios sin conexión chocaron con un registro más reciente del servidor, y cómo, quién y con qué motivo se resolverán?" Aquí nada se sobrescribe ni se resuelve automáticamente.

## Quién la usa
- **Tripulación / Patrón del remolcador / Capitán / Jefe de máquinas:** para ver y resolver sus propios registros en conflicto después de trabajar sin conexión. Cuando aparece un conflicto, no suponga que su propio registro prevalece automáticamente (13.03 §6).
- Un conflicto material necesita una resolución humana; esta página es el destino del flujo "Conflicto material → Centro de conflictos" de 13.03 §10.

## Antes de empezar
- La página solo enumera los conflictos que usted registró y requiere acceso de lectura a al menos una fuente legible (por ejemplo, asignación de tripulación, operaciones del remolcador, registro diario, evidencia de orden de trabajo (con lectura de equipos y mantenimiento), borrador de renovación de certificado). Cualquier fuente legible abre la página; los permisos enumerados son alternativas.
- La lista y las decisiones necesitan una conexión actual con el servidor. Mientras está sin conexión, sus cambios locales se conservan en el dispositivo.
- Elija el buque o remolcador en la lista **Buque**.

## Paso a paso
1. Elija un buque en **Buque**. La tabla se abre con las columnas **Fuente**, **Registro**, **Versiones**, **Capturado**, **Recibido por el servidor** y **Estado**.
2. Si aparece una nota "Not included:", las fuentes para las que no tiene permiso de lectura no están en la lista.
3. En una fila con **Abierto — se requiere decisión**, pulse **Revisar**.
4. En **Revisión del conflicto**, compare para cada campo los valores **Antes de capturar**, **Capturado** y **Servidor**. No hay ninguna opción preseleccionada.
5. Para cada valor, elija explícitamente **Mantener valor del servidor** o **Usar valor capturado**, complete **Motivo de resolución (obligatorio)** y pulse **Enviar resolución**.
6. Para eliminar solo la copia local de este dispositivo, marque la confirmación en **Descartar mi cambio local** y pulse **Descartar cambio local**.
7. Use **Cerrar revisión** para volver a la lista.

## Qué significan los estados
- **Abierto — se requiere decisión:** el conflicto espera una decisión humana.
- **Sustituido por un conflicto más reciente:** existe un conflicto más reciente para el mismo registro; revise el nuevo.
- **Resuelto por decisión explícita:** alguien lo resolvió con una decisión motivada. Esto no es una aprobación operativa.
- **Reservado / No proporcionado / Desconocido:** el valor está oculto por autorización, no se registró o no se pudo leer. Un valor desconocido nunca se toma como vacío ni como "No".
- **Server receipt recorded for your explicit decision:** el servidor recibió su decisión. Este acuse de recibo no es una aprobación operativa.

## Lo que no puede hacer / precaución
- Los conflictos nunca se fusionan ni se resuelven automáticamente; cada valor necesita una elección humana explícita y un motivo (13.03 §1).
- El centro resuelve directamente los conflictos de asignación de tripulación. Para otras fuentes muestra "Resolve this conflict from the owning record's offline panel."; por ejemplo, los registros del remolcador se resuelven en **Operaciones sin conexión del remolcador**.
- Descartar un cambio local elimina solo la copia del dispositivo; el registro del servidor no cambia y el conflicto sigue registrado hasta una decisión motivada.
- Un cambio capturado en otro dispositivo debe resolverse en ese dispositivo; no se descarta nada.
- El envío vuelve a comprobar su acceso y las versiones del registro; no concede aprobación operativa.

## Si algo sale mal
- **Mensaje "The submission outcome is unknown":** antes de enviar de nuevo, use **Comprobar resultado del envío**.
- **Mensaje "The server record changed after your review" / "The server record changed again":** su decisión no se aplicó; revise el conflicto de nuevo.
- **"Material conflicts could not be loaded. Nothing was resolved.":** pulse **Reintentar**.
- **"You do not have access to the conflict center." / "Conflict access is unavailable for this vessel.":** compruebe su rol y el contexto del buque; si es necesario, contacte con el administrador de su organización (13.03 §10).

## Páginas relacionadas
