---
key: tug-daily-log
title: Diario del remolcador
roles: [Tug Master, Master, Crew]
permissions: [mode.tug, tug.read, tug.write]
related: [towage-execution, tug-operation-history, tug-operations-offline, offline-conflict-center]
screenshots: []
reviewed: false
---
## Para qué sirve esta página
El diario del remolcador registra, para una fecha y una guardia, los **trabajos, los contadores de horas de los motores, el combustible y los defectos**, y los compara con los remolques ejecutados ese día para su **conciliación**. Responde a la pregunta: "¿Cuántos trabajos se completaron en este día/guardia, coinciden las horas de los contadores con los tiempos de ejecución, cuánto combustible se consumió y qué defectos se notificaron?" La conciliación es orientativa; la revisión del Capitán es un registro humano independiente.

## Quién la usa
- **Tripulación / oficial de guardia:** para llevar el registro de la guardia en tiempo real y en orden (13.03 §6).
- **Patrón del remolcador / Capitán:** para revisar un diario enviado y verificarlo o devolverlo (13.03 §3).

## Antes de empezar
- Abra el remolcador desde **Operaciones de remolcador**.
- Crear y editar un diario requiere acceso de escritura de remolcador.
- La revisión requiere una **Designación** registrada en el servidor (Patrón del remolcador o Capitán) para ese remolcador. Quien redactó o envió el diario no puede revisarlo.

## Paso a paso
1. Abra un diario existente de la lista, o elija una **Fecha del diario** y una **Guardia** y pulse **Crear diario**.
2. Introduzca la **Zona horaria (IANA)**.
3. En **Contadores de horas de los motores**, introduzca la **Lectura inicial**, la **Lectura final** y, si es necesario, la **Evidencia de puesta a cero o desbordamiento** de cada **Contador**.
4. En **Combustible consumido**, introduzca el valor, la unidad y la **Referencia de la fuente de combustible**.
5. Marque en **Remolques ejecutados en el día** las ejecuciones que pertenecen a este diario; complete los **Linked defect identifiers** y el **Resumen diario** (resumen de al menos 20 caracteres).
6. Pulse **Guardar**. En la línea **Conciliación**, compruebe los trabajos completados, las horas de ejecución, las horas de los contadores y los defectos notificados.
7. Cuando la lista **Bloqueos** esté vacía, pulse **Enviar a revisión del capitán**.
8. Como Capitán, elija su **Designación**, escriba un motivo y pulse **Capitán: verificar diario** o **Capitán: devolver diario**.

## Qué significan los estados
- **Borrador / Enviado / Devuelto / Verificado:** el estado del diario.
- **Revisión del Capitán — Pendiente / Atención / Revisado:** Pendiente = a la espera de revisión; Atención = devuelto, o enviado mientras la conciliación no está en Conciliado; Revisado = verificado por el Capitán. Un diario enviado no es un diario verificado.
- **Conciliado / Revisión requerida / Incompleto:** el resultado orientativo de la comparación. Debajo se enumeran los códigos de motivo.
- **intervalo abierto:** una ejecución sigue en curso; las horas de ejecución aún no son definitivas.
- **Eventos de seguridad — Desconocido:** esta página no cuenta los eventos de seguridad; no se extrae ninguna conclusión de "sin eventos".
- **Sincronizado / Pendiente de sincronizar / Conflicto / Fallido:** el estado de sincronización. Pendiente de sincronizar indica que el cambio aún no ha llegado al servidor.

## Lo que no puede hacer / precaución
- Quien redactó el diario o lo envió a revisión del Capitán no puede verificarlo.
- El resultado de la conciliación no es una aprobación; la revisión la hace el Capitán.
- Completar un registro y verificarlo no son la misma acción (13.03 §1).
- Sin conexión, se conserva un cambio no guardado en la memoria de esta página y se envía con su clave original cuando vuelve la conexión; mantenga la página abierta. El envío y la revisión se realizan con conexión.
- Si aparece un conflicto, no dé por hecho que su propio registro prevalece automáticamente (13.03 §6).

## Si algo sale mal
- **Mensaje "The server listed missing or blocking items.":** resuelva los códigos de **Bloqueos**.
- **Mensaje "The record changed or the command conflicts.":** use **Cargar la versión más reciente** para revisar el diario actual; el cambio local conservado se descarta.
- **Mensaje "The outcome is unknown.":** **Reintentar** vuelve a enviar el mismo comando idéntico.
- **Conflicto:** un cambio conservado en esta página nunca se fusiona; use **Cargar la versión más reciente**. Las entradas diarias capturadas en **Operaciones sin conexión del remolcador** que entran en conflicto se resuelven allí; también aparecen en el **Centro de conflictos materiales** (13.03 §10).

## Páginas relacionadas
