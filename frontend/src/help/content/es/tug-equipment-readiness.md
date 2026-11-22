---
key: tug-equipment-readiness
title: Preparación del equipo de remolque
roles: [Tug Master, Chief Engineer]
permissions: [mode.tug, tug.read, tug.write, equipment.write]
related: [tug-readiness, tug-crew-readiness, tug-assignment, tug-command]
screenshots: []
reviewed: false
---
## Para qué sirve esta página
Esta página muestra las **declaraciones del equipo de remolque** del remolcador seleccionado (chigre de remolque, gancho de remolque, cabo de remolque, liberación de emergencia, contraincendios FiFi) y sus **verificaciones del jefe de máquinas**. Responde a la pregunta: "¿Cuáles son la CMU, las fechas de prueba, la condición y la criticidad de cada elemento, quién lo verificó y qué dice la comprobación de preparación?" La evaluación es solo orientativa y alimenta el control de preparación; no es una decisión GO.

## Quién la usa
- **Patrón del remolcador:** para comprobar el chigre, el cabo de remolque, el gancho de remolque / estopor de cadena, la liberación de emergencia, el FiFi y las evidencias de defectos críticos (13.03 §3).
- **Jefe de máquinas:** para verificar o no verificar las declaraciones y registrar el efecto de un defecto crítico en la preparación (13.03 §5).

## Antes de empezar
- Abra el remolcador desde **Operaciones de remolcador**; esta pantalla no necesita ninguna asignación.
- Añadir o editar una declaración requiere acceso de escritura de remolcador y de escritura de equipos.
- Registrar una verificación requiere una designación de **Jefe de máquinas** registrada en el servidor para ese remolcador.

## Paso a paso
1. Lea el distintivo global en la línea **Evaluación**.
2. En cada tarjeta de equipo, compruebe el distintivo de resultado, el código de motivo, **CMU (t)**, **Próxima prueba**, **Condición** y **Crítico**.
3. Para una nueva declaración, pulse **Añadir equipo**; complete **Tipo de equipo**, **Etiqueta**, **CMU (t)**, **Última prueba**, **Próxima prueba**, **Condición**, **Crítico**, **ID de activo PMS**, **ID del certificado** y **Notas**, y pulse **Guardar declaración**.
4. Para cambiar una declaración existente, use **Editar** en su tarjeta.
5. Como jefe de máquinas, elija su **Designación**, escriba un motivo y use **Verificado** o **No verificado** para **Registrar verificación**. Para "No verificado" se requiere un motivo.
6. Consulte el historial de verificaciones con revisión y hora en **Verificaciones del jefe de máquinas**, y la tabla **Comprobaciones de fuentes** en la parte inferior.

## Qué significan los estados
- **Listo / Condicional / No listo / Revisión requerida:** la evaluación orientativa. Listo no es una decisión GO.
- **Superado / Fallido / Desconocido:** el resultado de la comprobación de un elemento.
- **Operativo / Degradado / Averiado / Desconocido:** la condición declarada. Si no se elige ninguna condición, sigue siendo "Desconocido"; no se cuenta como operativo.
- **No evaluado:** la criticidad no se ha evaluado. Esto no significa "No".
- **Verificado / No verificado:** el registro humano del jefe de máquinas.
- **sustituido por una revisión posterior:** la verificación pertenecía a una revisión anterior de la declaración; la revisión actual necesita una nueva verificación.

## Lo que no puede hacer / precaución
- Guardar registra una declaración sin verificar. Cualquier cambio necesita una nueva verificación del jefe de máquinas.
- Completar un registro y verificarlo no son la misma acción; la verificación espera a la persona autorizada (13.03 §1).
- El tipo de equipo no se puede cambiar después de guardar; añada una nueva declaración para otro tipo.
- La página nunca declara seguro el equipo de remolque; la decisión sigue correspondiendo al Patrón del remolcador (13.03 §3).
- Los comandos están desactivados sin conexión.

## Si algo sale mal
- **Mensaje "The record changed":** use **Recargar** para revisar la declaración actual.
- **Mensaje "The outcome is uncertain":** **Reintentar** envía el mismo comando.
- **El registro no se puede verificar:** compruebe las evidencias y la fuente PMS/certificado; contacte con el verificador (13.03 §10).
- **Sin autorización:** compruebe su rol, el remolcador y el contexto de designación; si es necesario, contacte con el administrador de su inquilino (13.03 §10).

## Páginas relacionadas
