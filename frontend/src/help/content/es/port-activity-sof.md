---
key: port-activity-sof
title: Actividad portuaria y SOF
roles: [Master, Chief Engineer]
permissions: [operations.port.read, operations.port.write, operations.port.finalize]
related: [noon-report, deck-daily-log, fuel-bunker-log, today]
screenshots: []
reviewed: false
---
## Para qué sirve esta página
La página de actividad portuaria y SOF conserva los **eventos portuarios** del buque seleccionado como un registro operativo con referencias de evidencia, y permite que una persona designada finalice internamente el **Statement of Facts (SOF)**. Responde a: "¿Qué eventos portuarios declaramos, en qué orden e intervalo UTC, con qué referencias de carga, calado, práctico, remolcador, retraso y protesta, y ha finalizado una persona designada esta revisión del SOF?" La página no produce ninguna firma, laytime ni demurrage, y los eventos registrados no son instrucciones ni ventanas planificadas aprobadas.

## Quién la usa
- **Capitán:** para conservar la llegada y la actividad portuaria como un registro operativo con evidencia y para finalizar el SOF (13.03 §2).
- **Primer oficial / Jefe de máquinas:** para registrar eventos portuarios y, con una designación vigente, finalizar el SOF.

## Antes de empezar
- En la barra de contexto superior, seleccione la **empresa y el buque correctos**. Si no hay ningún buque seleccionado, la página muestra "Select a ship".
- Leer requiere `operations.port.read`; guardar borradores requiere `operations.port.write` y derechos de edición en este buque.
- Finalizar requiere `operations.port.write`, derechos de edición en este buque y `operations.port.finalize`, además de una designación de finalización vigente (capitán, primer oficial o jefe de máquinas) para este buque.

## Paso a paso
1. Elija el buque. **Cronología de eventos portuarios registrados** enumera los eventos devueltos en el orden UTC del servidor. **Filtrar página devuelta** acota solo esta página.
2. Lea la **Cronología del Statement of Facts** debajo de la lista: cada evento con **Intervalo registrado**, **Motivo del retraso** y **Observaciones**.
3. Pulse **Nuevo registro en borrador**. Complete **Contexto de escala portuaria**, **Actividad y tiempos registrados**, **Declaraciones de carga y calado** y **Apoyo, excepciones y evidencias**. Las horas de los eventos requieren Z o un desfase UTC explícito.
4. Pulse **Guardar borrador sin verificar**. Cada guardado añade una revisión al **Historial de revisiones registradas**.
5. En un registro guardado, abra **Finalización del Statement of Facts**. Si aparece **Required facts missing**, complete primero esos campos.
6. Marque cada referencia no resuelta de la lista para reconocerla, elija su **Designación** y pulse **Finalizar SOF**. El sistema registra quién finalizó, cuándo, la función y el motivo.

## Qué significan los estados
- **BORRADOR:** el SOF no está finalizado.
- **FINAL (interno):** una persona designada finalizó esta revisión. Es interno: no es una firma ni un acuerdo externo.
- **Firma no registrada:** se muestra siempre; en esta página no existe ninguna firma.
- **Modificado tras FINAL: se requiere nueva revisión y finalización.:** el registro se volvió a guardar después de la finalización y ha vuelto a BORRADOR.
- **UNKNOWN (unresolved references):** no existe ninguna búsqueda propietaria para estas referencias obligatorias. Reconocerlas no las convierte en conocidas.
- **Revisión requerida / Registro en borrador sin verificar:** los eventos portuarios son declaraciones; nada está verificado.
- **Borrador (no finalizado) / Final (interno; no es una firma):** el estado del SOF que el servidor registró para esta revisión, mostrado en el resumen del registro. Final solo procede de una finalización humana registrada.

## Lo que no puede hacer / precaución
- El sistema nunca finaliza el SOF. La finalización es un acto humano de un capitán, primer oficial o jefe de máquinas designado (13.03 §2).
- **Finalizar SOF** permanece desactivado mientras falten datos obligatorios, no exista ninguna designación o no se hayan reconocido las referencias no resueltas.
- La finalización no es una firma, no es un acuerdo externo y no es laytime ni demurrage.
- El pie de la página indica: "SOF finalization is an internal human act recorded in the finalization panel by an appointed Master, Chief Officer or Chief Engineer. Signatures, operational/legal approval and offline capture remain unavailable." (la finalización del SOF es un acto humano interno registrado en el panel de finalización por un capitán, primer oficial o jefe de máquinas designado; las firmas, la aprobación operativa/legal y la captura sin conexión siguen sin estar disponibles).
- Guardar borradores y finalizar requieren una sesión en línea actual; esta página no tiene captura sin conexión.

## Si algo sale mal
- **"No current finalization appointment for this vessel.":** compruebe su rol y el contexto del buque con el administrador de su organización (13.03 §10).
- **"Outcome unknown: retry sends the same command once.":** use **Reintentar**; no inicie una nueva finalización.
- **"Command not accepted":** lea el código mostrado, recargue el registro con **Leer registro guardado actual** e inténtelo de nuevo.
- **"The saved source changed.":** recargue o descarte explícitamente antes de volver a guardar.

## Páginas relacionadas
