---
key: work-order-center
title: Órdenes de trabajo
roles: [Chief Engineer, Technical Superintendent, Crew / OOW / Engineer]
permissions: [workorder.read, equipment.read, maintenance.read, workorder.write, workorder.release, workorder.execute, workorder.verify, workorder.close]
related: [pms-command-center, defects, critical-spares, service-engineer-report, operational-downtime, offline-conflict-center]
screenshots: []
reviewed: false
---
## Para qué sirve esta página
El centro de órdenes de trabajo contiene las **órdenes de trabajo en borrador y los registros de ejecución declarados** del buque seleccionado. Responde a la pregunta: "¿Qué órdenes de trabajo existen en este buque y cuáles se han liberado, ejecutado, han tenido evidencias enviadas y han sido verificadas por una persona?" Cada revisión guardada se conserva en un historial inmutable. La verificación y el cierre no son una vuelta al servicio, un permiso ni una aprobación de riesgos.

## Quién la usa
- **Jefe de máquinas / Superintendente técnico:** para abrir, liberar, verificar y cerrar órdenes de trabajo planificadas y no planificadas (13.03 §5).
- **Ingeniero / tripulación:** para iniciar el trabajo asignado y registrar y enviar lecturas, repuestos y evidencias fotográficas y documentales (13.03 §6).

## Antes de empezar
- La lectura requiere `workorder.read`, `equipment.read` y `maintenance.read`. Los borradores requieren `workorder.write`, la liberación `workorder.release`, la ejecución `workorder.execute`, la verificación `workorder.verify` y el cierre `workorder.close`.
- La liberación, la verificación y el cierre requieren un usuario humano con sesión iniciada.
- Elija el buque correcto en **Seleccione un buque o remolcador**.

## Paso a paso
1. Elija el buque. En **Órdenes de trabajo registradas**, filtre esta página con **Buscar números de órdenes de trabajo devueltos** y abra un registro.
2. Para un trabajo nuevo, pulse **Nuevo borrador de orden de trabajo**, complete los campos y pulse **Guardar borrador sin evaluar**. Los borradores incompletos se pueden guardar.
3. En **Ejecución y evidencias**, si tiene permiso de liberación, elija **Declarar evidencias requeridas** en **Política de evidencias requeridas**, marque los tipos de evidencia y pulse **Liberar orden de trabajo**. Si libera sin declarar, la verificación queda bloqueada hasta que alguien con permiso de liberación use **Cambiar evidencias requeridas** → **Guardar evidencias requeridas**.
4. El ejecutor pulsa **Iniciar trabajo**, completa **Lecturas**, **Repuestos consumidos**, **Evidencias fotográficas y documentales**, **Trabajo realizado** y **Hallazgos**, pulsa **Guardar evidencias** y después **Enviar evidencias**.
5. Un verificador autorizado que no ejecutó el trabajo revisa las evidencias. Pulsa **Registrar verificación**, o elige un **Motivo de la devolución** y pulsa **Devolver al ejecutor**.
6. Para una orden de trabajo verificada, una persona autorizada pulsa **Registrar cierre**.
7. Consulte las revisiones anteriores en **Historial inmutable de órdenes de trabajo**. Si se necesita un registro de inactividad, use **Abrir borrador de inactividad vinculado**.

## Qué significan los estados
- **No liberada para su ejecución. / Liberada / En curso:** el trabajo aún no está liberado, está liberado o se está ejecutando.
- **Evidencias enviadas:** el ejecutor declaró el trabajo y las evidencias. No significa verificado.
- **Verificada (registro humano):** registrado por un verificador autorizado que no ejecutó el trabajo. **Cerrada (registro humano)** es un paso aparte.
- **Faltan evidencias requeridas / Presente, pendiente de verificación humana / Verificada por una persona:** el estado del control de evidencias requeridas.
- **Revisión requerida: política no configurada:** no se declaró ninguna evidencia requerida. No configurada nunca significa que no se requiera nada; la verificación queda bloqueada.
- **Devuelta por el verificador:** el trabajo volvió al ejecutor con el motivo indicado.

## Lo que no puede hacer / precaución
- Si usted ejecutó el trabajo, no puede verificarlo ni cerrarlo. La página indica "You executed this work, so you cannot verify or close it."
- Evidencias enviadas, Verificada y Cerrada son registros humanos independientes. La página nunca declara un equipo listo para el servicio.
- El resumen "Saved work order source" describe los campos del borrador guardado e indica: "Release, evidence recording, independent verification and closure are recorded in the execution panel. Submitted evidence is accepted only by independent human verification, never by the person who did the work; there is no separate evidence-acceptance step, and verification is not a return-to-service decision." Estas acciones se realizan en **Ejecución y evidencias**.
- Las lecturas se conservan tal como se declararon; no se aplica ninguna conversión de unidades ni umbral.
- La cola de evidencias sin conexión solo funciona si existe una política sin conexión aprobada y mientras el trabajo está en curso. Los conflictos nunca se fusionan automáticamente; use **Abrir centro de conflictos** y decide una persona.

## Si algo sale mal
- **El registro cambió o esta acción no está permitida en su estado actual:** use **Recargar** para leer el registro actual e inténtelo de nuevo.
- **Resultado desconocido:** **Reintentar el mismo comando** envía el mismo comando; no crea un registro nuevo.
- **Falta el permiso o la autoridad humana:** compruebe su rol y el contexto del buque; si es necesario, contacte con el administrador de su inquilino (13.03 §10).
- **La compuerta de evidencias requeridas rechazó esta acción:** complete los tipos de evidencia que faltan. Si un registro no se puede verificar, compruebe las evidencias y su procedencia con el verificador (13.03 §10).

## Páginas relacionadas
