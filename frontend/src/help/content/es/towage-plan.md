---
key: towage-plan
title: Plan de remolque
roles: [Tug Master, Master, Tug Operations Coordinator, Shore Operations]
permissions: [mode.tug, tug.read, tug.write, tug.plan.write]
related: [towage-risk-briefing, tug-assignment, tug-readiness, towage-execution]
screenshots: []
reviewed: false
---
## Para qué sirve esta página
El plan de remolque registra, para un trabajo de remolcador enviado, las **funciones y la configuración de los remolcadores, el intercambio de información, los límites operativos, las comunicaciones, la liberación de emergencia, la contingencia y los controles de la zona de retroceso (snap-back)**. Responde a la pregunta: "¿Cómo se realizará este trabajo, dentro de qué límites y con qué disposiciones de emergencia?" La aprobación del plan es un registro humano del Capitán o del Patrón del remolcador designado; no es una decisión GO ni un permiso. Los límites son datos de entrada del plan indicados con su fuente; el sistema no proporciona valores por defecto.

## Quién la usa
- **Coordinador / planificador de operaciones de remolcador:** para preparar el plan y enviarlo para aprobación (13.03 §7).
- **Patrón del remolcador / Capitán:** para confirmar la configuración, el cabo, el BP/capacidad, las comunicaciones, los límites, la contingencia y las condiciones de detención del trabajo, y aprobar o devolver el plan (13.03 §3).

## Antes de empezar
- Abra el remolcador desde **Operaciones de remolcador** y elija el trabajo en **Seleccione un trabajo de remolcador enviado**.
- Editar el plan requiere acceso de escritura de remolcador y de escritura del plan. Los remolcadores del plan se eligen entre los que tienen una propuesta de asignación vigente para este trabajo.
- Aprobar o devolver requiere una **Designación** registrada en el servidor (Patrón del remolcador o Capitán) para el remolcador coordinador.

## Paso a paso
1. Lea el **Estado** y la **Revisión** y, a continuación, la lista **Pendiente antes del envío**.
2. En **Funciones y configuración de los remolcadores**, use **Añadir** para cada remolcador; introduzca **Posición**, **Método de remolque**, **Remolcador coordinador (principal)**, **Origen del cabo de remolque**, **Punto de conexión**, **ID del equipo del cabo de remolque** e **ID del equipo de suelta de emergencia**.
3. Escriba el texto de **Intercambio de información Capitán / práctico / Patrón del remolcador**.
4. En **Límites operativos (datos de entrada del plan)**, introduzca un **Valor** y la **Fuente del límite** para cada **Parámetro**. Un límite nuevo empieza vacío.
5. Complete **Plan de comunicaciones** (canal principal y de reserva, idioma de trabajo, procedimiento ante pérdida de comunicaciones), **Liberación de emergencia**, **Contingencia** y **Controles de la zona de retroceso (snap-back)**.
6. Guarde con **Crear borrador** o **Guardar borrador**; cuando no falte nada, pulse **Enviar para aprobación**.
7. El aprobador elige una **Designación**, escribe un **Motivo** de al menos 10 caracteres y pulsa **Aprobar** o **Devolver**.
8. Si un plan aprobado debe cambiar, escriba un motivo y use **Modificar**. Las listas **Registros de aprobación** y **Revisiones** muestran el historial.

## Qué significan los estados
- **Borrador:** el plan se puede editar.
- **En revisión:** el plan se envió para aprobación; se espera una decisión.
- **Devuelto:** el aprobador devolvió el plan con un motivo; corríjalo y vuelva a enviarlo.
- **Aprobado:** el Capitán o el Patrón del remolcador aprobó esta revisión del plan. No es una decisión GO; el control de preparación y el briefing siguen siendo necesarios.
- **Desconocido:** no se eligió ningún valor para el campo. "Desconocido" para las zonas de retroceso nunca sustituye a "No".
- **La solicitud de remolcador cambió después de guardar este plan.:** el plan se basa en una revisión anterior de la solicitud y debe revisarse.

## Lo que no puede hacer / precaución
- La persona que redactó o envió el plan no puede aprobarlo.
- Un plan al que le faltan secciones obligatorias no se puede enviar; un borrador incompleto sí se puede guardar.
- El sistema no propone ningún valor de límite, canal ni umbral; cada valor lo aporta el planificador con su fuente.
- Un plan aprobado no es un permiso para iniciar; el inicio requiere confirmaciones vigentes del briefing y el registro GO del Patrón del remolcador (13.03 §3).
- Los comandos están desactivados sin conexión.

## Si algo sale mal
- **Mensaje "Required sections are missing.":** complete los códigos de **Pendiente antes del envío**.
- **Mensaje "The record changed":** use **Recargar** para revisar el plan actual.
- **Mensaje "The outcome is unknown":** **Reintentar** vuelve a enviar la misma solicitud.
- **Sin autorización:** compruebe su rol, el remolcador y el contexto de designación; si es necesario, contacte con el administrador de su inquilino (13.03 §10).

## Páginas relacionadas
