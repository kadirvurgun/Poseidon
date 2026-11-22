---
key: pre-survey-checklist
title: Lista de verificación previa al reconocimiento
roles: [Master, Chief Engineer / Technical Superintendent, Regulatory / Assurance]
permissions: [certificate.read, certificate.survey.plan, certificate.survey.review]
related: [survey-planner, certificate-command-center, renewal-workflow]
screenshots: []
reviewed: false
---
## Para qué sirve esta página
La lista de verificación previa al reconocimiento sigue las tareas de preparación del reconocimiento de un certificado con responsable, fecha límite, evidencia y verificación humana independiente. Responde a: "¿Qué elementos de preparación de este reconocimiento están abiertos, cuáles tienen evidencia y cuáles ha verificado otra persona?" La preparación es consultiva y nunca es una aprobación del reconocimiento (13.03 §1, §8).

## Quién la usa
- **Capitán, Jefe de máquinas / Superintendente técnico:** inician una lista, responden los elementos y adjuntan evidencia.
- **Verificador:** compara los elementos respondidos por otra persona con su evidencia y los verifica o rechaza (13.03 §1).
- Los usuarios con acceso solo de lectura ven "Read-only for your role."

## Antes de empezar
- Ver la página requiere lectura de certificados. Iniciar y responder una lista requiere planificación de reconocimientos en ese buque; verificar requiere revisión de reconocimientos.
- Elija el buque correcto con **Elija un buque** (13.03 §1).
- La ventana de reconocimiento del certificado debe estar declarada en el [Centro de mando de certificados](/app/certificate-command-center).
- La página necesita una lectura en vivo. Sin conexión verá "Offline: a current online read is required."

## Paso a paso
1. Vea las listas del buque en **Listas de verificación**, o en el [Planificador de reconocimientos](/app/survey-planner) pulse **Abrir lista de verificación** o **Iniciar lista de verificación** en una fila.
2. Para una lista nueva, use **Elija un certificado**, elija una **Plantilla** y pulse **Crear lista de verificación**. La plantilla es una plantilla inicial, no un catálogo aprobado.
3. En **Datos del plan de reconocimiento**, complete **Inicio previsto**, **Fin previsto**, **Lugar previsto (texto)** y, si está disponible, **Escala**.
4. En **Elementos**, introduzca para cada elemento el **Rol responsable**, la **Fecha límite**, la **Respuesta** y, si es necesario, **Hallazgo** y **Comentario**. Vincule evidencia con **Añadir evidencia** y luego pulse **Guardar**. Cada guardado crea una nueva revisión.
5. Como verificador, en los elementos respondidos por otra persona elija **Verificar** o **Rechazar** en **Decisión**, introduzca el motivo y pulse **Registrar verificación**.
6. Vigile **Preparación** y **Bloqueos**.

## Qué significan los estados
- Respuesta del elemento: **Abierto**, **Listo**, **Requiere atención**, **Bloqueado**, **No aplicable**. Un elemento cuya respuesta es desconocida sigue **Abierto**; nunca cuenta como "No aplicable" ni como "Listo".
- Verificación: **Pendiente**, **Verificado (humano)**, **Rechazado**. Un elemento respondido sigue **Pendiente** hasta que se verifica.
- Preparación: **Bloqueado**, **Incompleto**, **Preparado, pendiente de confirmación humana**. Incluso el mejor estado espera una confirmación humana; el sistema nunca dice "reconocimiento aprobado".
- **Crítico / Obligatorio / Opcional:** la importancia del elemento. Los elementos críticos y obligatorios no resueltos cuentan en contra de la preparación y aparecen en **Bloqueos**.
- **Viaje: desconocido (no hay módulo de viajes):** la adecuación al viaje aún no se puede evaluar y sigue desconocida.

## Lo que no puede hacer / precaución
- No puede verificar un elemento que usted respondió: "You answered this item; another person must verify it."
- Responder **Listo** a un elemento no lo verifica; Completado ≠ Verificado. Cada decisión de verificación está vinculada a la respuesta y la evidencia exactas que usted ve.
- La preparación no es una aprobación del reconocimiento, una reserva ni la validez del certificado. La coordinación con la clase, la bandera y la autoridad es externa y aquí no se simula.
- Los elementos de la plantilla no son un catálogo aprobado; una persona autorizada juzga si se ajustan al buque.

## Si algo sale mal
- **Esta lista cambió desde que la abrió. Recargue antes de editar.** Pulse **Recargar la versión actual**.
- **El resultado aún no se conoce. Reintentar envía la misma solicitud.** Use **Reintentar la misma solicitud**.
- **Elementos no registrados:** corrija los elementos indicados y vuelva a guardar.
- **No tiene acceso a esta vista.** Compruebe su rol, el buque y el ámbito del objeto; si es necesario, contacte con el Tenant Admin (13.03 §10).
- Si un elemento no se puede verificar, compruebe su evidencia y su fuente y remítalo al verificador (13.03 §10).

## Páginas relacionadas
