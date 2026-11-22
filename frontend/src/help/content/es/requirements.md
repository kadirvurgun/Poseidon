---
key: requirements
title: Borradores de requisitos
roles: [Regulatory / Assurance, DPA / HSEQ]
permissions: [regulatory.draft.read, regulatory.draft.write]
related: [sources, audit-ready, audit]
screenshots: []
reviewed: false
---
## Para qué sirve esta página
La página de borradores de requisitos conserva los **requisitos** derivados de una revisión de fuente como borradores versionados y vincula cada requisito a una revisión exacta de la fuente. Responde a: "¿En qué revisión de qué fuente se basa este requisito y cuál es su ciclo de vida registrado?" Un requisito es el primer eslabón de la cadena Requisito → Control → Evidencia → Verificación. Ningún registro de esa cadena debe quedar sin vincular (huérfano) (13.03 §8).

## Quién la usa
- **Usuario de Regulatory / Assurance:** vincula el requisito a su fuente, evalúa la aplicabilidad con los datos de contexto y mantiene completa la cadena (13.03 §8).
- **DPA / HSEQ:** lee la base de un requisito durante el trabajo de hallazgos y auditoría (13.03 §4).

## Antes de empezar
- Ver la página requiere lectura en toda la organización y lectura de borradores regulatorios; editar requiere además escritura de borradores regulatorios.
- Las subvistas necesitan sus propios permisos, siempre con ámbito de lectura en toda la organización. **Ciclo de vida y aplicabilidad** requiere lectura de aplicabilidad; editar reglas requiere escritura de reglas, solicitar revisión requiere revisión de reglas y evaluar requiere evaluación de aplicabilidad; la cualificación y la atestación de hechos requieren cualificación regulatoria o atestación de hechos más una designación registrada en el servidor. **Controles y evidencias** requiere lectura de controles, lectura de evidencias, lectura de archivos y lectura de aplicabilidad; vincular requiere escritura de controles o escritura de evidencias con derechos de edición sobre el buque, y la aprobación requiere aprobación de controles.
- Registre primero la fuente en la página **Borradores de fuentes**. Un requisito solo se puede vincular a una revisión de fuente existente.
- Guardar requiere una conexión activa con el servidor; sin conexión, las ediciones se conservan en la pestaña.

## Paso a paso
1. Vea los borradores de requisitos en la lista; busque con **Search drafts** y **Buscar**.
2. Para un requisito nuevo, pulse **Nuevo borrador**.
3. En **Draft fields**, elija un **Estado** para cada campo (**Ausente**, **Desconocido**, **Known (declared)**) e introduzca los valores conocidos: **Instrumento**, **Declaración de versión / enmienda**, **Fecha de entrada en vigor declarada**, **Declaración / procedencia**, **Fuente observada el (con desfase UTC)**.
4. En **Immutable source citation**, escriba en **Find a source** y pulse **Search sources**. Elija la fuente en **Source draft** y luego la revisión exacta en **Exact revision**. Compruebe el contenido de la revisión elegida con **Selected immutable source content**.
5. Pulse **Guardar borrador**. Cuando el registro está abierto, aparecen las secciones **Ciclo de vida registrado** e **Historial inmutable**.
6. Use **Ciclo de vida y aplicabilidad** para la aplicabilidad y **Controles y evidencias** para los vínculos de control y evidencia. El **Informe de elementos críticos sin vínculo** dentro de **Controles y evidencias** enumera las declaraciones críticas sin vínculos de requisito; vincule esos registros a un requisito.

## Qué significan los estados
- **Ciclo de vida registrado:** el estado que el servidor deriva de las reglas vinculadas: **Borrador**, **Adoptado**, **Efectivo en el futuro**, **En vigor**, **Sustituido**. Si no se puede leer, se muestra **No disponible**; el sistema no supone borrador ni en vigor.
- "Bound rules record different lifecycle states. The shown value is a software precedence; expert review is required." (las reglas vinculadas registran estados de ciclo de vida distintos; el valor mostrado es una precedencia del software; se requiere revisión experta). En ese caso el valor mostrado no es una decisión; se necesita una revisión experta.
- **Desconocido:** la información no se conoce. Cuando un dato crítico es desconocido, el resultado de aplicabilidad sigue en **Revisión requerida** (REVIEW_REQUIRED); nunca se cuenta como "no aplicable" ni como "no" (13.03 §8).
- **Revisión de aplicabilidad requerida / Vínculo de requisito ausente / Verificación no realizada:** elementos abiertos en la cadena control-evidencia.
- **Finalización desconocida — Sin verificar — Eficacia desconocida:** la finalización, la verificación y la eficacia son estados separados. Un registro completado no está verificado, y un registro verificado no es eficaz.

## Lo que no puede hacer / precaución
- No puede vincular un requisito a "la última" fuente; siempre se elige una **Exact revision**. Cuando la fuente recibe una nueva revisión, el vínculo antiguo se conserva en el historial.
- Una vista previa de aplicabilidad no establece cumplimiento, preparación, permiso ni aprobación legal: "PREVIEW / DRAFT. Predictions do not establish compliance, readiness, permits or legal approval." (VISTA PREVIA / BORRADOR; las predicciones no establecen cumplimiento, preparación, permisos ni aprobación legal).
- Un cambio de ciclo de vida pasa por **Solicitar revisión humana**; deciden la admisión de la fuente y un revisor independiente cualificado. El sistema nunca lo cierra por sí solo.
- No suponga que las fuentes de reglas de clase, bandera, empresa y locales se sustituyen entre sí (13.03 §8).

## Si algo sale mal
- **Source selection unavailable.** Pulse **Retry sources**.
- **Conflict. Your edits are retained. Reload only after discarding them.** (conflicto; sus ediciones se conservan; recargue solo después de descartarlas). Abra la revisión actual con **Volver a cargar el borrador guardado**.
- **Check field states, bounds, dates and source revision.** Si eligió una fuente, elija también su revisión exacta.
- **History unavailable. Editor retained.** Pulse **Retry history**.
- Si el ciclo de vida es inesperado, revise las reglas vinculadas y las revisiones de fuente; si es necesario, contacte con el responsable del control o con el experto en la materia (SME) (13.03 §10).
- Si no tiene permiso, compruebe su rol y el ámbito de la organización y consulte al Tenant Admin (13.03 §10).

## Páginas relacionadas
