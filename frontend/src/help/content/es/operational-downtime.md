---
key: operational-downtime
title: Inactividad operativa
roles: [Master, Chief Engineer, Technical Superintendent, DPA]
permissions: [operations.downtime.read, operations.downtime.write, operations.downtime.review]
related: [defects, work-order-center, engine-daily-log, today]
screenshots: []
reviewed: false
---
## Para qué sirve esta página
Esta página registra las **interrupciones operativas declaradas** de un buque (inactividad) junto con su trazabilidad de origen. Responde a: "¿Qué interrupción se declaró en este buque, cuándo, por qué causa y vinculada a qué avería u orden de trabajo, y dónde están su revisión y su cierre?" La página no decide el derecho a loss of hire, la aptitud para el servicio ni ninguna liberación de seguridad.

## Quién la usa
- **Jefe de máquinas / Superintendente técnico:** para registrar la inactividad causada por un fallo técnico o por mantenimiento y vincularla al historial de la avería o de la orden de trabajo (13.03 §5).
- **Capitán:** para seguir el impacto operativo de la inactividad y revisarla cuando esté designado (13.03 §2).
- **Revisor designado (capitán, primer oficial, jefe de máquinas, Fleet Manager, DPA):** para verificar, devolver para corrección o cerrar un registro de inactividad.

## Antes de empezar
- En la barra de contexto superior, compruebe que esté seleccionada la **empresa correcta**; la página solo enumera los buques de su ámbito.
- Leer requiere acceso de lectura a la inactividad; abrir un borrador requiere acceso de escritura. Las acciones de revisión también requieren acceso de revisión y una designación de función vigente para este buque.
- Para abrir una inactividad vinculada, puede usar **Abrir borrador de inactividad vinculado** en la página de una avería o de una orden de trabajo.

## Paso a paso
1. Elija el buque en la lista "Select a ship". Se abre la lista **Interrupciones operativas registradas**.
2. Use **Buscar causas en esta página** para acotar los registros de esta página.
3. Para un registro nuevo, pulse **Nuevo borrador de inactividad**.
4. Para cada campo, elija primero el estado de su valor: **Ausente**, **Desconocido** o **Valor declarado**. Solo introduce un valor mientras "Valor declarado" está seleccionado.
5. En "Interruption start" e "Interruption end", escriba una marca de tiempo completa con segundos y un desfase UTC explícito.
6. Pulse **Guardar borrador sin evaluar**. Se puede guardar un borrador incompleto.
7. En **Revisión y cierre de la inactividad**, vincule la evidencia de reclamación con **Seleccionar evidencia** si es necesario y luego pulse **Enviar a revisión**.
8. Si es un revisor designado, elija su "Appointment" y use **Verificar**, **Devolver para corrección** o **Cerrar**.
9. Revise las revisiones anteriores en **Historial inmutable de borradores**.

## Qué significan los estados
- **Borrador / Revisión requerida:** el contenido registrado no está evaluado. Los motivos aparecen en "Why this draft remains unassessed".
- **No enviado / Abierto / En revisión:** el estado del ciclo de vida del registro.
- **Verificado (humano):** una persona designada distinta de quien lo registró lo verificó. Completado no es Verificado (13.03 §1).
- **Cerrado:** la inactividad se cerró. Un cierre verificado no es una liberación de seguridad.
- **Desconocido:** el valor no se declaró o no se pudo determinar. Desconocido no significa "No" ni "No aplicable". Por eso el loss of hire y el cierre siguen como "Desconocido".
- **Candidato a Loss of Hire:** solo un candidato; no establece ningún derecho ni importe.
- **Intervalo registrado:** una observación técnica sin evaluar; no sustituye a los minutos declarados.

## Lo que no puede hacer / precaución
- Quien registró la inactividad no puede verificarla ni cerrarla; debe hacerlo otra persona designada.
- No es posible enviar, verificar ni cerrar mientras falten datos obligatorios.
- Vincular evidencia de reclamación no es una verificación.
- No se copia ninguna descripción, hora ni decisión de cierre de la fuente propietaria (avería, orden de trabajo); solo se conserva el vínculo a la revisión exacta.
- Sin conexión no se pueden leer las fuentes actuales y no hay almacenamiento sin conexión; sus entradas permanecen solo en la página abierta.

## Si algo sale mal
- **El resultado del guardado es desconocido:** su comando original está protegido. Con la misma cuenta, use "Retry exact downtime command".
- **El borrador guardado cambió:** otra persona modificó el registro. Sus entradas se conservan; use "Read current draft" para recargar, o descártelas.
- **Los registros de inactividad o el acceso actual no están disponibles:** compruebe su rol y el contexto del buque; si es necesario, contacte con el administrador de su organización (13.03 §10).
- **La fuente cambió:** use la revisión actual de la fuente propietaria para un nuevo borrador.
- **Comando no aceptado:** comuníquelo al soporte con una captura de pantalla, la hora y sus pasos.

## Páginas relacionadas
