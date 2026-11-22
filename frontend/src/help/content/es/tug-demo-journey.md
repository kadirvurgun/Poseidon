---
key: tug-demo-journey
title: Recorrido demo del remolcador
roles: [Tug Operations Coordinator, Shore Operations, Tug Master]
permissions: [mode.tug, tug.read]
related: [tug-job-intake, tug-assignment, tug-readiness, towage-plan, towage-risk-briefing, towage-execution]
screenshots: []
reviewed: false
---
## Para qué sirve esta página
El recorrido demo es un paseo guiado por las pantallas reales de remolcador con **datos sintéticos, sin firmar y sin verificar**. Responde a la pregunta: "¿Por qué pantallas pasa un trabajo de remolcador desde la solicitud hasta el historial, y qué control aplica el servidor en cada paso?" La demo no crea ninguna decisión, liberación ni verificación; solo enlaza con las pantallas reales y las explica.

## Quién la usa
- **Coordinador de operaciones de remolcador / Operaciones en tierra:** para aprender el flujo de trabajo y el control de cada paso (13.03 §7).
- **Patrón del remolcador:** para ver dónde se sitúan, de principio a fin, los pasos de decisión a bordo (GO de preparación, ejecución) (13.03 §3).

## Antes de empezar
- Seleccione el remolcador; sin un remolcador seleccionado, la página muestra "Select a tug vessel to continue."
- Si abre la página con un trabajo, los enlaces de los pasos llevan ese trabajo.
- Cada enlace abre la pantalla real; se aplican las reglas de autorización propias de esa pantalla.

## Paso a paso
1. Siga los pasos en orden: **Recepción de trabajos**, **Asignación del remolcador**, **Disponibilidad de tracción a punto fijo, tripulación y equipo**, **Plan de remolque**, **Evaluación de riesgos y briefing**, **Master release (registro humano GO)**, **Ejecución**, **Evidencia y verificación**, **Historial de operaciones**, **Continuidad sin conexión**.
2. En cada paso, use **Abrir pantalla** para ir a la pantalla real.
3. Abra **Por qué** para leer el control que aplica el servidor en ese paso; por ejemplo, el plan lo aprueba un Patrón del remolcador o un Capitán que no lo redactó, y cada participante acusa recibo de la revisión exacta emitida.

## Qué significan los estados
- Esta página no tiene ningún distintivo de estado propio. El significado de los estados de las pantallas que abra se describe en la guía de cada pantalla.
- Como explica el paso de preparación, UNKNOWN sigue siendo REVIEW_REQUIRED, nunca READY.
- GO es un registro humano vinculado a la evaluación de preparación actual; el sistema nunca lo crea.

## Lo que no puede hacer / precaución
- Los datos de la demo son ficticios y no están verificados. Las liberaciones, las decisiones GO y las verificaciones siempre las registran personas autorizadas, nunca la demo.
- Un resultado que vea en las pantallas de la demo nunca sustituye a una decisión de seguridad para una operación real; la decisión sigue correspondiendo al Patrón del remolcador (13.03 §3).
- Una asignación desde tierra nunca prevalece sobre la decisión de seguridad a bordo del Patrón del remolcador (13.03 §7).

## Si algo sale mal
- **Mensaje "This page address is not valid.":** vuelva a abrir la página desde el menú.
- **La pantalla de un paso no se abre o falta el acceso:** compruebe su rol y el contexto del remolcador para esa pantalla; si es necesario, contacte con el administrador de su inquilino (13.03 §10).
- **Resultado de preparación inesperado:** abra la vista Por qué de la pantalla correspondiente y revísela con el responsable del control (13.03 §10).

## Páginas relacionadas
