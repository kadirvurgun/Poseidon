---
key: towage-risk-briefing
title: Riesgos y briefing de remolque
roles: [Tug Master, Master, Crew, Tug Operations Coordinator]
permissions: [mode.tug, tug.read, tug.write, hse.risk.read]
related: [towage-plan, tug-readiness, towage-execution, tug-operations-offline]
screenshots: []
reviewed: false
---
## Para qué sirve esta página
Esta página prepara y emite el **registro de riesgos y briefing de remolque** de un trabajo de remolcador enviado y recoge los **acuses de recibo** de los participantes. Responde a la pregunta: "¿Qué condiciones, peligros, barreras preventivas y de mitigación, autoridad para detener el trabajo y disposiciones de emergencia se explicaron en el briefing, en qué estado está la evaluación de riesgos vinculada y han acusado recibo todos los participantes de la revisión emitida?" Emitir el briefing es un registro humano del Capitán o del Patrón del remolcador designado; no es una decisión GO ni un permiso. Un briefing incompleto bloquea READY.

## Quién la usa
- **Patrón del remolcador / Capitán:** para preparar y emitir el briefing y asegurarse de que no se toma ninguna decisión READY antes de que el riesgo y el briefing estén completos (13.03 §3).
- **Tripulación y otros participantes (práctico, oficial, marineros de cubierta, maquinista):** para acusar recibo del briefing emitido o impugnarlo en el contexto de función correcto (13.03 §6).
- **Coordinador de operaciones de remolcador:** para seguir el estado del briefing y de los acuses de recibo (13.03 §7).

## Antes de empezar
- Abra el remolcador desde **Operaciones de remolcador** y elija el trabajo en **Seleccione un trabajo de remolcador enviado**.
- El **Plan de remolque** del trabajo debe estar aprobado y la evaluación de riesgos vinculada también; de lo contrario, el briefing no se puede emitir.
- La emisión requiere una **Designación** registrada en el servidor (Patrón del remolcador o Capitán) para el remolcador coordinador. La emisión también requiere acceso de lectura a la evaluación de riesgos y acceso de escritura de remolcador en el remolcador coordinador. El acuse de recibo es solo para las personas de la lista de participantes.

## Paso a paso
1. En la línea superior, lea **Estado**, **Revisión**, **Acuse de recibo**, **Estado del riesgo** y **Estado del plan**.
2. En **Dato de preparación (un briefing incompleto bloquea READY)**, consulte los resultados y los códigos de motivo enviados al control de preparación.
3. Complete **Condiciones confirmadas en el briefing** (meteorología, corriente, visibilidad), **Peligros**, **Barreras preventivas**, **Barreras de mitigación**, **Autoridad para detener el trabajo** y **Disposiciones de emergencia**.
4. Introduzca el **ID de la evaluación de riesgos vinculada**; en **Participantes**, use **Añadir** para introducir el **ID de persona** y la **Función** de cada participante.
5. Guarde con **Crear borrador** o **Guardar borrador**; cuando **Pendiente antes del envío** esté vacío, elija una **Designación** y pulse **Emitir briefing**.
6. Como participante, lea la revisión emitida y pulse **Acusar recibo**. Si no está de acuerdo, escriba un **Motivo** de al menos 10 caracteres y pulse **Impugnar**.
7. Si el briefing debe cambiar, escriba un motivo y use **Modificar**. Las listas **Acuses de recibo** y **Revisiones** muestran el historial.

## Qué significan los estados
- **Borrador / Emitido:** el estado del briefing. Emitido significa que pueden empezar los acuses de recibo; por sí solo no es un resultado de preparación.
- **No emitido / Pendiente / Completo / Impugnado:** el estado de los acuses de recibo de los participantes. Solo pasa a **Completo** cuando todos los participantes han acusado recibo de la revisión emitida.
- **Aprobado y reconocido / Atención / Bloqueado / Revisión requerida:** el estado de la evaluación de riesgos vinculada.
- **Superado / Fallido / Desconocido:** el resultado de la comprobación enviado al control de preparación. Un briefing ausente o no emitido y los acuses de recibo pendientes son un **Fallido** que bloquea READY.

## Lo que no puede hacer / precaución
- No se toma ninguna decisión READY antes de que el riesgo y el briefing estén completos y se hayan registrado los acuses de recibo de los participantes (13.03 §3).
- Una impugnación pone el estado de los acuses de recibo en **Impugnado** y envía un Fallido al control de preparación, de modo que READY y GO quedan bloqueados. El motivo aparece en **Acuses de recibo**. Un acuse de recibo no se puede cambiar para la misma revisión; para resolver una impugnación, el briefing se modifica (nueva revisión), se vuelve a emitir y todos los participantes vuelven a acusar recibo.
- Si el plan o la evaluación de riesgos cambian después del briefing, este debe revisarse de nuevo; los acuses de recibo anteriores no cuentan para una nueva revisión.
- Nadie acusa recibo en nombre de otra persona; cada participante registra su propio acuse de recibo (13.03 §6).
- Sin conexión no se envía ningún comando desde esta página. Un acuse de recibo puede ponerse en cola en **Operaciones sin conexión del remolcador**.

## Si algo sale mal
- **Mensaje "Required sections are missing.":** complete los códigos que faltan; compruebe que el plan y la evaluación de riesgos están aprobados.
- **Mensaje "The record changed":** use **Recargar** para revisar el briefing actual.
- **Mensaje "The outcome is unknown":** **Reintentar** vuelve a enviar la misma solicitud.
- **Sin autorización:** compruebe su rol, el remolcador y el contexto de designación; si es necesario, contacte con el administrador de su inquilino (13.03 §10).

## Páginas relacionadas
