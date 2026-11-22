---
key: sources
title: Borradores de fuentes
roles: [Regulatory / Assurance, DPA / HSEQ]
permissions: [regulatory.draft.read, regulatory.draft.write]
related: [requirements, audit-ready, audit]
screenshots: []
reviewed: false
---
## Para qué sirve esta página
La página de borradores de fuentes conserva como **registros en borrador versionados** las fuentes normativas y de reglas en las que se basa su organización (convenio, bandera, clase, empresa o regla local). Responde a: "¿En qué revisión de qué fuente oficial se basa este requisito?" Se aplica el aviso de la parte superior: "Tenant-local DRAFT · Declared and unverified. Dates and hashes confer no legal authority. Source references are stored as text." (BORRADOR local de la organización · declarado y sin verificar; las fechas y los hashes no confieren autoridad legal; las referencias de fuente se almacenan como texto) (13.03 §8).

## Quién la usa
- **Usuario de Regulatory / Assurance:** registra la fuente oficial y versionada e indica su fecha de entrada en vigor y su ciclo de vida (13.03 §8).
- **DPA / HSEQ:** la lee para ver a qué revisión de fuente se refiere una auditoría o un hallazgo (13.03 §4).

## Antes de empezar
- Ver la página requiere lectura en toda la organización y lectura de borradores regulatorios. Crear y guardar borradores requiere además escritura de borradores regulatorios; sin ella, el botón **Nuevo borrador** no se muestra.
- Las subvistas necesitan sus propios permisos, siempre con ámbito de lectura en toda la organización. **Ciclo de vida y aplicabilidad** requiere lectura de aplicabilidad; editar reglas requiere escritura de reglas, solicitar revisión requiere revisión de reglas y evaluar requiere evaluación de aplicabilidad; la cualificación y la atestación de hechos requieren cualificación regulatoria o atestación de hechos más una designación registrada en el servidor. **Controles y evidencias** requiere lectura de controles, lectura de evidencias, lectura de archivos y lectura de aplicabilidad; vincular requiere escritura de controles o escritura de evidencias con derechos de edición sobre el buque, y la aprobación requiere aprobación de controles.
- Tenga a mano el texto oficial de la fuente y los datos de su versión/enmienda. POSEIDON no descarga ni verifica el texto de la fuente; usted lo declara.
- Guardar requiere una conexión activa con el servidor. Sin conexión, sus ediciones se conservan en la pestaña, pero no se guardan.

## Paso a paso
1. Vea los borradores de fuentes registrados en la lista. Para buscar, escriba en **Search drafts** y pulse **Buscar**. Desplácese entre páginas con **Anterior** y **Siguiente**.
2. Para una fuente nueva, pulse **Nuevo borrador**.
3. En **Draft fields**, elija primero un **Estado** para cada campo: **Ausente**, **Desconocido** o **Known (declared)**. Solo puede introducir un valor después de elegir **Known (declared)**. Los campos son **Título**, **Autoridad declarada**, **Referencia de fuente (solo texto)**, **Declaración de versión / enmienda**, **Fecha de entrada en vigor declarada**, **Declaración / procedencia** y **Fuente observada el (con desfase UTC)**.
4. Pulse **Guardar borrador**. Si se guarda correctamente verá "Draft saved. Unverified." (borrador guardado; sin verificar); abra el registro con **Open saved draft and history**.
5. Abra un borrador existente de la lista, modifíquelo y vuelva a guardarlo. Cada guardado crea una nueva **Revisión**; las revisiones anteriores permanecen en **Historial inmutable** con su contenido y su **Hash de integridad (no autoridad)**.
6. Para pasar a las reglas de aplicabilidad o a la cadena control-evidencia, use **Ciclo de vida y aplicabilidad** o **Controles y evidencias** encima de la lista. Pulse **Back to register** para volver.

## Qué significan los estados
- **BORRADOR:** el estado almacenado que se muestra en la lista y en el encabezado del registro. Todos los registros de fuente de esta página son borradores; no son una adopción oficial ni una decisión de entrada en vigor.
- **Ausente:** aún no se ha introducido información para el campo. Los pasos posteriores (por ejemplo, una evaluación de aplicabilidad) quedan bloqueados mientras falten datos obligatorios.
- **Desconocido:** la información se buscó, pero no se conoce. El sistema nunca interpreta Desconocido como "no" ni como "no aplicable"; cuando un dato crítico es desconocido, el resultado sigue en revisión (REVIEW_REQUIRED).
- **Known (declared):** usted declaró el valor. Una declaración no es información verificada.
- **Hash de integridad (no autoridad):** muestra que el contenido de una revisión no ha cambiado; no es una prueba de validez legal.

## Lo que no puede hacer / precaución
- No puede cambiar una revisión guardada. Cuando la fuente cambie, guarde una nueva revisión; los requisitos siguen vinculados a la revisión anterior y el historial se conserva (13.03 §1).
- No suponga que las fuentes de reglas de clase, bandera, empresa y locales se sustituyen entre sí; registre cada una como su propia fuente (13.03 §8).
- Esta página no admite oficialmente una fuente y no da a nadie un resultado de cumplimiento, de permiso ni de preparación. La admisión de fuentes y la revisión independiente son autoridad humana.
- Las fechas de entrada en vigor y los hashes no confieren autoridad legal.

## Si algo sale mal
- **Permission denied.** Compruebe su rol y el ámbito de la organización; si es necesario, consulte al Tenant Admin (13.03 §10).
- **Conflict. Your edits are retained. Reload only after discarding them.** (conflicto; sus ediciones se conservan; recargue solo después de descartarlas). Otra persona modificó el registro antes que usted. Abra la revisión actual con **Volver a cargar el borrador guardado** y vuelva a hacer su cambio.
- **Check field states, bounds, dates and source revision.** El valor de un campo o el formato de una fecha no es válido.
- **Service unavailable. Your draft is retained.** Cuando vuelva la conexión, reenvíe el mismo comando con **Retry frozen command**.
- **Sign in to continue; retain your draft.** Su sesión terminó; use el enlace **Iniciar sesión**.
- Si un error de la aplicación persiste, comunique al soporte el ID de correlación, la pantalla, la hora y los pasos (13.03 §10).

## Páginas relacionadas
