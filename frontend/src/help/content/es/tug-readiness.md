---
key: tug-readiness
title: Control de preparación del remolcador
roles: [Tug Master, Master, Tug Operations Coordinator, Shore Operations]
permissions: [mode.tug, tug.read, tug.write]
related: [tug-assignment, tug-crew-readiness, tug-equipment-readiness, towage-risk-briefing, towage-execution]
screenshots: []
reviewed: false
---
## Para qué sirve esta página
El control de preparación evalúa la preparación operativa de una asignación de remolcador a partir de comprobaciones de fuentes **solo como orientación**, y permite al Patrón del remolcador o al Capitán designado registrar una **decisión GO/NO-GO** motivada. Responde a la pregunta: "¿Qué dicen las comprobaciones de tripulación, equipos, PMS, certificados, comunicaciones, meteorología, margen de tiro a punto fijo, asignación y briefing, y qué bloqueos existen?" La evaluación no es una decisión: "The system never issues GO; only the appointed Master or Tug Master records a GO/NO-GO decision with a reason."

## Quién la usa
- **Patrón del remolcador / Capitán:** para revisar la evaluación y los bloqueos y registrar una decisión GO o NO-GO (13.03 §3).
- **Coordinador de operaciones de remolcador / Operaciones en tierra:** para seguir la preparación y los bloqueos; no deciden (13.03 §7).

## Antes de empezar
- Abra la página desde una asignación: en **Asignación y conflictos**, use el enlace **Control de preparación operativa** de una propuesta. Sin una asignación seleccionada, la página muestra "Select an assignment from the job's assignment list."
- Registrar una decisión requiere acceso de escritura de remolcador y una **Designación** registrada en el servidor (Patrón del remolcador o Capitán) para ese remolcador, y la asignación debe seguir en estado Propuesto.
- No hay READY sin el briefing: el **Riesgos y briefing de remolque** debe estar emitido y confirmado por todos los participantes (13.03 §3).

## Paso a paso
1. Lea el distintivo de estado en la línea **Evaluación**. La nota junto a él indica "Advisory, not a GO decision."
2. Compruebe **Tiro a punto fijo requerido (t)**, **Tiro a punto fijo disponible verificado (t)** y **Margen de tiro a punto fijo (t)**. Un valor ausente muestra "Desconocido".
3. Revise los códigos de **Bloqueos críticos**, **Motivos de revisión** y **Condiciones**.
4. En la tabla de categorías, consulte el resultado de cada categoría (Tripulación, Competencia de remolque, Equipos, Equipo de remolque, PMS y defectos, Certificados, Comunicaciones, Meteorología, Margen de tiro a punto fijo, Asignación, Riesgos y briefing de remolque). Para más detalle, lea los códigos de motivo de la tabla **Comprobaciones de fuentes**.
5. Si tiene autoridad de decisión, elija **NO-GO** o **GO** en el formulario **Registrar decisión del capitán**, introduzca su **Designación** y un **Motivo**, y pulse **Registrar**.
6. Consulte las decisiones registradas en la lista **Decisiones del capitán**.

## Qué significan los estados
- **Listo (READY):** todas las comprobaciones de fuentes se superan (sin bloqueos, desconocidos ni condiciones). Es información, no una decisión; la decisión GO la sigue registrando el Patrón del remolcador o el Capitán.
- **Condicional (CONDITIONAL):** se aplican las condiciones de "Condiciones". GO está desactivado y la página explica: "This assessment is CONDITIONAL. GO needs a READY assessment: no source defines which conditions would permit GO, so the server refuses it. Resolve the conditions and reassess, or record NO-GO."
- **No listo (NOT_READY):** existe al menos un bloqueo crítico.
- **Revisión requerida (REVIEW_REQUIRED):** algunas fuentes son desconocidas o esperan revisión. Una comprobación desconocida nunca se cuenta como "Listo".
- **Superado / Fallido / Desconocido:** el resultado de una categoría o comprobación individual.
- **GO / NO-GO:** la decisión humana y el motivo registrados por el Patrón del remolcador o el Capitán.

## Lo que no puede hacer / precaución
- GO solo puede registrarse sobre una evaluación READY; el servidor rechaza GO en CONDITIONAL, NOT_READY o REVIEW_REQUIRED. La página solo activa GO con una evaluación READY; en otro caso indica "GO can only be recorded against a READY assessment." NO-GO siempre puede registrarse con un motivo.
- No hay READY mientras el riesgo y el briefing estén incompletos o falten acuses de recibo de los participantes; la opción GO queda entonces desactivada (13.03 §3).
- La propuesta de asignación de un usuario en tierra nunca prevalece sobre la decisión de seguridad a bordo del Patrón del remolcador (13.03 §7).
- Si la evaluación cambia antes de su decisión, la decisión se rechaza; vuelva a revisar la evaluación actual.
- Los comandos están desactivados sin conexión.

## Si algo sale mal
- **Mensaje "The assessment changed":** use **Recargar**, revise la evaluación actual y vuelva a decidir.
- **Mensaje "The outcome is uncertain":** **Reintentar** envía el mismo comando.
- **Preparación inesperada:** abra la tabla de categorías y los motivos de las comprobaciones de fuentes; revíselos con el responsable del control o un experto en la materia (13.03 §10).
- **Sin autorización:** compruebe su rol, el remolcador y el contexto de designación; si es necesario, contacte con el administrador de su inquilino (13.03 §10).

## Páginas relacionadas
