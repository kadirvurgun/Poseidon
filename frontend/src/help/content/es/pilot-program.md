---
key: pilot-program
title: Programa piloto: semanas e informe de valor
roles: [Product Owner, Tenant Admin]
permissions: [pilot.program.read, pilot.program.manage, pilot.program.record, pilot.program.triage, pilot.program.decide]
related: [pilot-gate, role-uat, audit-ready]
screenshots: []
reviewed: false
---
## Para qué sirve esta página
La página del programa piloto reúne en un solo lugar las semanas piloto de un buque (y opcionalmente de un remolcador), los puntos de control de cada semana, las incidencias del piloto, los comentarios diarios y el informe de valor. Responde a: "¿Se han cumplido con registros reales los puntos de cada semana piloto, y cómo cambiaron los valores medidos respecto a la línea base?" Se aplica el aviso de la parte superior: "Software cannot complete a pilot. A checklist item is satisfied only by a link you make to a real record that the server verifies; a missing baseline stays Unknown; only the appointed Product Owner closes a week or records the program decision." (el software no puede completar un piloto; un punto de control solo se cumple mediante un vínculo que usted crea a un registro real verificado por el servidor; una línea base ausente sigue como Desconocida; solo el Product Owner designado cierra una semana o registra la decisión del programa).

## Quién la usa
- **Product Owner:** cierra las semanas y registra la decisión del programa (Continue, Fix, Stop, Expand). Esto requiere una designación vigente de Product Owner.
- **Equipo piloto:** vincula registros a los puntos de control, notifica incidencias y registra comentarios y mediciones.
- **Responsable del triaje:** clasifica y resuelve las incidencias.
- 13.03 no define un rol separado para esta página; se aplican los principios de administración de la organización (13.03 §9).

## Antes de empezar
- Ver la página requiere lectura en toda la organización y lectura del programa piloto. Crear programas, registrar, clasificar y decidir requieren cada uno su propio permiso; los cierres de semana y las decisiones también requieren una designación de Product Owner.
- Los registros que vincule (por ejemplo, un registro de certificado, de tripulación o de orden de trabajo) deben existir ya en sus propias páginas.
- En los comentarios nunca introduzca nombres, direcciones de correo electrónico, números de teléfono ni de identificación; use solo un seudónimo y el rol.
- Registrar requiere una conexión activa.

## Paso a paso
1. Abra un programa desde **Programas piloto**. Para un programa nuevo, complete **Nombre del programa**, **Buque**, **Remolcador (opcional)**, **Fecha de inicio de la semana 1** y **Zona horaria del calendario (IANA)** en **Crear programa piloto** y pulse **Registrar**.
2. En cada sección **Semana**, revise las columnas **Punto de control**, **Criterio (fuente)**, **Ámbito**, **Estado** y **Registros vinculados**.
3. Para cumplir un punto, introduzca el **Punto de control**, el **Tipo de registro** y el **Identificador del registro** en **Vincular un registro** y pulse **Vincular un registro**. Elimine un vínculo erróneo con **Retirar vínculo** después de escribir un **Motivo del retiro**.
4. En **Incidencias del piloto**, use **Notificar incidencia** para introducir el ámbito, la **Categoría**, la gravedad y el **Resumen de la incidencia**. El responsable del triaje usa **Triaje** con una **Nota de triaje** y después resuelve la incidencia.
5. En **Comentarios diarios**, use **Registrar comentario** para introducir un **Resumen** y una **Valoración**.
6. En **Informe de valor**, use **Registrar medición** para introducir la **Métrica**, la **Fase** (Baseline o Pilot period), el **Valor**, el **Tamaño de la muestra**, **Periodo desde**, **Periodo hasta**, la **Referencia de evidencia** y **Método y muestra**.
7. Si es el Product Owner, registre el resultado y la justificación en **Cerrar semana**. Al final del programa, complete la decisión, la **Designación** y la **Justificación** en **Registrar la decisión del programa** y pulse **Registrar**.

## Qué significan los estados
- **Vinculado y verificado:** hay un registro real verificado por el servidor vinculado al punto. Los puntos con **Vínculo ya no válido** y **Ausente** no se cumplen.
- **Completion required:** el registro vinculado debe estar en su estado completado. "Audit trace proves that a person wrote the record; it does not prove the record or flow is completed." (la traza de auditoría prueba que una persona escribió el registro; no prueba que el registro o el flujo estén completados).
- **Lista para cierre humano / No lista:** una recomendación para la semana; solo el Product Owner la cierra como **Aceptada** o **No aceptada**.
- **Triada el mismo día / Triada con retraso / Triaje pendiente hoy / Vencida:** el momento del triaje de las incidencias P0 respecto al día del calendario del programa. Para las incidencias **Sin objetivo de mismo día en la fuente**, el sistema no inventa ningún plazo.
- **Medido / Desconocido:** cuando falta un valor de línea base o del piloto, el informe sigue como **Desconocido**; nunca se cuenta como cero ni como una mejora. Los valores **No comparable** no se comparan.
- **Telemetría (declarada, no verificada):** un valor de telemetría declarado.
- **Ampliación posible; decisión humana pendiente / Se recomienda corregir:** una recomendación del programa, no una decisión.

## Lo que no puede hacer / precaución
- "Accepted is possible only when every item is verified and no P0 triage is open, pending or late." (Aceptada solo es posible cuando todos los puntos están verificados y ningún triaje P0 está abierto, pendiente o con retraso).
- "Expand is possible only when every week is accepted and values, UAT, defects and P0 are clear." (Expand solo es posible cuando todas las semanas están aceptadas y los valores, UAT, averías y P0 están despejados).
- Si usted fue autor de la evidencia en la que se basa una semana o una decisión (vínculos, comentarios, mediciones), no puede cerrar esa semana ni registrar esa decisión.
- No se puede registrar ningún cierre de semana ni decisión para un programa ficticio de prueba.
- El sistema nunca completa un piloto, nunca cierra una semana y nunca inventa decisiones ni firmas.

## Si algo sale mal
- **No tiene un nombramiento vigente de Product Owner; cierres y decisiones son de solo lectura.** Contacte con el Tenant Admin para obtener la designación (13.03 §10).
- **La captura de comentarios no está disponible hasta que un operador configure la política de conservación.** Pida al administrador del sistema que configure la política de conservación.
- **El registro cambió o ya se usó. Recargue.** Abra el estado actual con **Recargar**.
- **El resultado es incierto. Reintentar envía el mismo comando.** Use **Reintentar** para reenviar el mismo comando de forma segura.
- **Rechazado por las reglas.** Por ejemplo, el registro vinculado puede no existir, estar fuera de ámbito o no estar completado.
- **No permitido.** Usted fue autor de vínculos, comentarios o mediciones en los que se basa este cierre de semana o esta decisión; debe registrarlo otra persona.
- Una semana que no ha empezado no se puede cerrar (se muestra como "El registro cambió o ya se usó. Recargue.").
- **No tiene acceso a estos registros.** Compruebe su rol y el ámbito de la organización (13.03 §10).

## Páginas relacionadas
