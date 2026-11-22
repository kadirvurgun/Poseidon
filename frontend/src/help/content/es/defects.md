---
key: defects
title: Defectos y salvaguardas
roles: [Chief Engineer, Technical Superintendent, Master, Crew / OOW / Engineer]
permissions: [defect.read, equipment.read, defect.write, defect.verify, defect.effectiveness, defect.close]
related: [pms-command-center, work-order-center, operational-downtime, service-engineer-report, drydock-planning]
screenshots: []
reviewed: false
---
## Para qué sirve esta página
La página de defectos y medidas temporales contiene los **defectos, restricciones y medidas temporales declaradas registrados** del buque seleccionado. Responde a la pregunta: «¿Qué gravedad tiene este defecto, qué salvaguarda o restricción operativa está registrada, se verificó la acción correctiva y qué sigue bloqueando el cierre?». Cada paso es un registro humano; la página no concede ninguna autoridad de seguridad ni de operación.

## Quién la usa
- **Jefe de máquinas / Superintendente técnico:** para registrar la salvaguarda o la restricción y el impacto en la disponibilidad de un defecto crítico, y para seguir la acción correctiva (13.03 §5).
- **Capitán:** para ver el impacto de un defecto en las restricciones operativas (13.03 §2).
- **Ingeniero / tripulación:** para declarar un defecto que hayan encontrado (13.03 §6).

## Antes de empezar
- La lectura requiere `defect.read` y `equipment.read`. Las declaraciones y los registros del ciclo de vida requieren `defect.write`.
- La verificación requiere `defect.verify`, la eficacia `defect.effectiveness` y el cierre `defect.close`, cada uno con un usuario humano con sesión iniciada.
- Elija el buque correcto en **Seleccione un buque o remolcador**.

## Paso a paso
1. Elija el buque. Si es necesario, filtre **Declaraciones registradas** con **Filtrar observaciones de vencimiento devueltas** y abra una declaración.
2. Para un defecto nuevo, pulse **Nueva declaración de defecto**, rellene los campos y pulse **Guardar declaración sin evaluar**. Se pueden guardar borradores incompletos.
3. En una declaración guardada, pulse **Iniciar ciclo de vida** en la sección **Ciclo de vida del defecto**.
4. Use **Registrar evaluación** para elegir la criticidad y escribir la declaración (al menos 10 caracteres).
5. Use **Registrar salvaguarda o restricción** y elija **Salvaguarda** o **Restricción operativa** como **Tipo de medida**. Para una medida temporal, introduzca el vencimiento (UTC).
6. Use **Registrar acción correctiva** para describir el trabajo realizado.
7. Una persona autorizada que no haya registrado ninguna acción correctiva registra **Registrar verificación** y después **Registrar eficacia**.
8. Cuando no quede ningún **Bloqueos de cierre**, una persona autorizada que tampoco haya registrado ninguna acción correctiva registra **Cerrar y devolver al servicio**. Los registros aparecen en **Registros del ciclo de vida**.

## Qué significan los estados
- **Detección / Evaluación / Salvaguarda / Acción / Verificación / Eficacia:** la etapa del ciclo de vida.
- **Cerrado (decisión humana registrada):** el cierre es una decisión humana; no es un resultado verde de «reparado».
- **Se aplica la regla crítica:** un defecto crítico no puede cerrarse sin una salvaguarda.
- **Activa / Vencida — bloqueante / Levantada / Finalizada al cierre:** el estado de una salvaguarda o restricción. Una medida temporal vencida debe renovarse o levantarse.
- **Defecto crítico sin salvaguarda activa / Criticidad desconocida y sin salvaguarda:** alertas. Estos defectos también aparecen en **Alertas de condición** en el [centro de mando PMS](/app/pms-command-center).
- **Desconocido:** la criticidad no se ha evaluado. Desconocido no significa «Baja» ni «No crítico».

## Lo que no puede hacer / precaución
- Si usted registró la acción correctiva, no puede registrar la verificación, la eficacia ni la devolución al servicio; requieren su propia autoridad y otra persona.
- Verificado, Eficaz y Cerrado son registros humanos separados. Una etapa o medida declarada no autoriza el servicio, no verifica la eficacia ni permite el cierre.
- Si la declaración cambia después de la verificación, el cierre queda bloqueado y el registro vuelve a revisión.
- La criticidad del activo no se copia como evaluación de este defecto.
- Sin conexión, las fuentes actuales de defectos no están disponibles; no hay almacenamiento sin conexión.

## Si algo sale mal
- **Otra persona cambió este registro:** use **Recargar** para leer el registro actual y vuelva a registrar.
- **El resultado del guardado es desconocido:** **Reintentar el mismo comando** envía el mismo comando.
- **No tiene el permiso del servidor para esta acción o registro:** compruebe su rol y el contexto del buque; contacte con el administrador del tenant si es necesario (13.03 §10).
- **Bloqueos de cierre:** complete el registro que falta en la lista. Si un registro no se puede verificar, revise la evidencia con el verificador (13.03 §10).

## Páginas relacionadas
