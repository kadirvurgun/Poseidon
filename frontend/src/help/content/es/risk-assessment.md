---
key: risk-assessment
title: Evaluación de riesgos
roles: [Master, Tug Master, DPA / HSEQ, Crew / OOW / Engineer]
permissions: [hse.risk.read, hse.risk.write, hse.risk.approve, hse.risk.acknowledge]
related: [risk-template-library, high-risk-work, safety-events]
screenshots: []
reviewed: false
---
## Para qué sirve esta página
La página de evaluación de riesgos registra los peligros, las consecuencias potenciales, las medidas de control, el riesgo inicial y residual y las condiciones de detención del trabajo para un trabajo u operación en un buque. Responde a: "¿Se han considerado, revisado y confirmado por los participantes los peligros y controles de este trabajo?" La página es **solo un objetivo de desarrollo**: una aprobación aquí es una revisión humana registrada, no un permiso, ni una aceptación experta del riesgo, ni la superación de una puerta HSE (13.03 §2, §4).

## Quién la usa
- **Capitán y Patrón del remolcador:** hacen preparar la evaluación para el trabajo en el buque o remolcador y toman la decisión de detener el trabajo. La autoridad de decisión sigue siendo suya (13.03 §2, §3).
- **DPA / HSEQ:** supervisan la gobernanza del riesgo y registran la decisión de revisión (13.03 §4).
- **Tripulación / OOW / Ingeniero:** como participantes, confirman o disputan la evaluación aprobada por sí mismos (13.03 §6).

## Antes de empezar
- Elija un buque para abrir sus evaluaciones de riesgos. Leer requiere el permiso de lectura de riesgos; escribir un borrador requiere el permiso de escritura de riesgos y ámbito de edición sobre ese buque. La decisión de revisión requiere el permiso de aprobación de riesgos; la confirmación requiere el permiso de confirmación de riesgos.
- Las valoraciones requieren un método de riesgo publicado (matriz) en la organización. Sin él, las valoraciones siguen como **No evaluado**. Un responsable HSEQ de la organización configura la matriz y otra persona la publica (página Biblioteca de plantillas de riesgo).
- Los comandos de riesgo necesitan una conexión en línea actual; sin conexión no se pone nada en cola ni se aprueba nada.

## Paso a paso
1. Elija el buque y abra una evaluación de la lista, o pulse **Nueva evaluación**.
2. Introduzca un **Número de evaluación**. Opcionalmente, introduzca un **Tipo de trabajo** y use **Partir de una plantilla publicada (solo copia)** para elegir una plantilla adecuada; la plantilla solo se copia en el nuevo borrador.
3. Complete **Título**, **Contexto de operación / trabajo**, **Tipo de trabajo**, **Alcance del trabajo** y **Método de riesgo**. Para **Se requiere detener el trabajo**, elija **Sí**, **No** o **Desconocido** y, si es necesario, escriba las **Stop-work conditions**.
4. Con **Añadir peligro**, introduzca para cada peligro el **Paso del trabajo**, las **Potential consequences**, el **Riesgo inicial** y el **Riesgo residual** (Likelihood, Severity, Rationale). Con **Añadir control**, añada las **Medidas de control**, el **Responsable del control** y las **Barrier references**.
5. En **Participantes**, escriba un ID de actor y un rol por línea. Vincule registros relacionados con **Añadir vínculo** si es necesario.
6. Pulse **Guardar borrador** y luego **Enviar a revisión**. **Elementos bloqueantes** enumera lo que falta.
7. Un revisor escribe un **Motivo** de al menos 10 caracteres y pulsa **Aprobar (registro de revisión humana)** o **Devolver**.
8. Tras la aprobación, cada participante pulsa **Confirmar** o, con un motivo, **Disputar** por sí mismo. Si se necesita un cambio, use **Enmendar (abre un nuevo borrador)**.

## Qué significan los estados
- **Borrador / En revisión / Devuelto / Aprobado (registro humano):** el ciclo de vida de la evaluación. Aprobado es un registro de revisión humana, no un permiso.
- **No evaluado / Método no configurado:** no hay valoración. El sistema no usa ninguna matriz por defecto, multiplicación ni umbral bajo/medio/alto.
- **Se requiere detener el trabajo — Desconocido:** aún no se ha tomado ninguna decisión; nunca se interpreta como "No".
- **Confirmación no aplicable antes de la aprobación / Confirmaciones pendientes / Todos los participantes confirmaron / Disputado – sin resolver:** el estado de confirmación de los participantes. Una disputa nunca se cierra con la confirmación de otra persona.
- **Cambiado desde la fijación / No disponible:** un registro vinculado cambió después de la revisión fijada o ya no se puede leer; revíselo de nuevo.

## Lo que no puede hacer / precaución
- Un autor o quien envió el ciclo de revisión no puede aprobar la misma evaluación (separación de funciones).
- Cada persona confirma solo por sí misma y solo la revisión aprobada exacta.
- Una aprobación aquí no es un permiso, ni una aceptación experta del riesgo, ni una puerta HSE. La decisión de iniciar o detener el trabajo sigue correspondiendo al capitán o al patrón del remolcador.
- Un borrador iniciado desde una plantilla no incluye ninguna valoración, firma, confirmación de participantes ni aprobación.
- Sin conexión, una evaluación no se puede enviar, aprobar ni confirmar.

## Si algo sale mal
- **Bloqueado hasta completar los elementos indicados.** Complete los elementos bloqueantes y envíe de nuevo.
- **This record changed elsewhere.** (este registro cambió en otro lugar). Pulse **Recargar** para ver la revisión actual y repita la acción.
- **No tiene permiso para esta acción (segregación de funciones o alcance).** Pida a otra persona autorizada que revise, o compruebe su rol y el contexto del buque; si es necesario, contacte con el administrador de la organización (13.03 §10).
- **El espacio de riesgos no está disponible con su acceso actual.** Compruebe sus permisos.
- Si una valoración parece inesperada, compruebe el método de riesgo y su revisión; consulte al responsable del control o al responsable HSEQ (13.03 §10).

## Páginas relacionadas
