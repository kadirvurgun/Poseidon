---
key: role-uat
title: Registros UAT por rol
roles: [Product Owner, Tenant Admin]
permissions: [uat.read, uat.record, uat.threshold.record, uat.attest]
related: [pilot-gate, pilot-program]
screenshots: []
reviewed: false
---
## Para qué sirve esta página
La página de registros UAT por rol registra las sesiones de pruebas de aceptación de usuario (UAT) con roles de buque y de remolcador, sus resultados de escenario y sus hallazgos como **evidencia humana**, y los compara con el umbral acordado. Responde a: "¿Cumple el umbral acordado por las personas la evidencia UAT atestiguada de cada rol y escenario?" Se aplica el aviso de la parte superior: "Records human UAT evidence only. The system never accepts UAT and never decides a pilot GO; automated test results are attachments, not a PASS." (solo registra evidencia UAT humana; el sistema nunca acepta la UAT ni decide un GO del piloto; los resultados de pruebas automatizadas son adjuntos, no un PASS).

## Quién la usa
- **Facilitador de UAT:** abre una sesión, registra los resultados de escenario observados del participante y los hallazgos, y envía la sesión a atestiguación. Los roles de participante del buque incluyen capitán, DPA, jefe de máquinas y tripulación (13.03 §2, §4, §5, §6).
- **Atestiguador:** una persona distinta del facilitador atestigua la sesión.
- **Responsables de UAT / Product Owner:** registran el umbral acordado y usan el resultado en la página **Revisión go/no-go del piloto**.

## Antes de empezar
- Ver la página requiere lectura en toda la organización y lectura de UAT. Registrar sesiones y resultados, registrar el umbral y atestiguar requieren cada uno su propio permiso.
- Los responsables de UAT deben haber acordado el umbral previamente; el sistema nunca lo establece.
- Elija un seudónimo para el participante. Nunca use un nombre, un correo electrónico ni un número de tripulante.
- Registrar requiere una conexión activa; está desactivado sin conexión.

## Paso a paso
1. Elija **Buque** o **Remolcador** con los botones de **Modo de operación**.
2. En **Evidencia UAT frente al umbral acordado**, revise el estado, los recuentos de **Bloqueantes abiertos**, **Hallazgos mayores abiertos**, **Sesiones atestiguadas** y **Pendiente de atestiguación**, y la tabla de escenarios.
3. Si no existe ningún umbral, complete **Tasa mínima de aprobación por escenario**, **Máximo de hallazgos mayores abiertos**, **Conformidad con el diseño**, **Referencia del acuerdo** y **Justificación** en **Registrar el umbral acordado** y pulse **Registrar**.
4. En **Sesiones UAT**, use el formulario **Nueva sesión UAT** para introducir **Rol**, **Entorno**, **Commit de compilación** y **Seudónimo del participante**, y luego pulse **Registrar**.
5. Abra la sesión. Con **Registrar resultado**, elija el **Escenario** y el **Resultado**; añada **Se prestó ayuda** y una **Referencia de evidencia automatizada** si procede. Para un problema, use **Registrar hallazgo** con **Gravedad** y **Resumen del hallazgo**.
6. Cuando haya registrado los escenarios, pulse **Enviar la sesión a atestiguación** (solo el facilitador). Los escenarios sin resultado cuentan como **No ejecutado**. Una persona distinta del facilitador escribe una **Declaración de atestiguación** en **Atestiguar la sesión** y pulsa **Registrar**.
7. En **Hallazgos abiertos**, cierre un hallazgo con una **Resolución** y una **Nota de resolución** y luego pulse **Resolver**.

## Qué significan los estados
- **Umbral no definido:** "No human-agreed threshold is recorded. UAT cannot pass until one is recorded." (no hay registrado ningún umbral acordado por personas; la UAT no puede superarse hasta que se registre uno).
- **Criterios no cumplidos / Evidencia incompleta:** la evidencia atestiguada no cumple el umbral, o no están cubiertos todos los roles y escenarios.
- **Criterios cumplidos; decisión humana pendiente:** el mejor estado posible. No es una aceptación de la UAT ni un GO del piloto; la decisión es humana.
- **No ejecutado:** no existe ningún resultado para el escenario. Un escenario que no se ejecutó nunca cuenta como superado.
- **Abierta / Enviada / Atestiguada:** el estado de la sesión. Solo las sesiones atestiguadas cuentan para el umbral.
- **Aprobado / Fallido / Bloqueado / No ejecutado:** el resultado de escenario observado del participante.
- **Bloqueante / Mayor / Menor:** la gravedad del hallazgo. Un bloqueante abierto hace fallar la puerta.
- **Datos ficticios:** un registro de muestra sintético; no es evidencia UAT real.

## Lo que no puede hacer / precaución
- No puede atestiguar una sesión que usted facilitó: "Attestation must be recorded by a different human than the facilitator." (la atestiguación debe registrarla una persona distinta del facilitador).
- No puede resolver un hallazgo que usted notificó. Un bloqueante solo se cierra como **Resuelto (corregido y reprobado)**; **Riesgo aceptado** no se ofrece.
- Un resultado automatizado (Playwright) es solo evidencia adjunta; el resultado es el observado por el participante.
- El sistema nunca acepta la UAT, nunca establece un umbral y nunca inventa atestiguaciones ni firmas.

## Si algo sale mal
- **No tiene acceso a estos registros.** Compruebe su rol y el ámbito de la organización y contacte con el Tenant Admin (13.03 §10).
- **El registro no se encontró en su inquilino.** Compruebe el enlace de la sesión y el modo seleccionado.
- **El registro cambió o ya se usó. Recargue.** Abra el estado actual con **Recargar**.
- **El resultado es incierto. Reintentar envía el mismo comando.** Use **Reintentar** para reenviar el mismo comando de forma segura.
- **Rechazado por las reglas.** Por ejemplo, el escenario no pertenece al rol de la sesión, o un bloqueante no se puede cerrar como riesgo aceptado.
- **No permitido.** Por ejemplo, usted facilitó esta sesión y no puede atestiguarla, o notificó este hallazgo y no puede resolverlo.
- **Está sin conexión. El registro está desactivado hasta que vuelva la conexión.** Cuando vuelva, pulse **Actualizar**.

## Páginas relacionadas
