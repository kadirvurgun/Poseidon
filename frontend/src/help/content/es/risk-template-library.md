---
key: risk-template-library
title: Biblioteca de plantillas de riesgo
roles: [DPA / HSEQ, Master, Tug Master, Crew / OOW / Engineer]
permissions: [hse.risk_template.read, hse.risk_template.write, hse.risk_template.publish, hse.risk_template.suggest, hse.risk_method.write, hse.risk_method.publish]
related: [risk-assessment, high-risk-work]
screenshots: []
reviewed: false
---
## Para qué sirve esta página
La biblioteca de plantillas de riesgo gestiona, con revisiones, las plantillas de evaluación de riesgos usadas en toda la organización (tipo de trabajo, peligros, controles por defecto, barreras requeridas, fuentes SMS y de requisitos) y los métodos de riesgo (matriz). Responde a: "¿Qué plantilla publicada se aplica a este tipo de trabajo y qué revisión está vigente?" La página es **solo un objetivo de desarrollo**: una publicación aquí es un registro humano HSEQ registrado, no un permiso, ni una aceptación experta del riesgo, ni la superación de una puerta HSE (13.03 §4).

## Quién la usa
- **DPA / HSEQ:** como parte de la gobernanza del riesgo, preparan, revisan y publican las revisiones de plantillas, las barreras y la matriz de riesgo (13.03 §4).
- **Capitán, Patrón del remolcador y tripulación:** leen las plantillas publicadas y envían **Sugerencias de la tripulación** para una plantilla.

## Antes de empezar
- Leer requiere el permiso de lectura de plantillas. Escribir y publicar plantillas requieren ámbito en toda la organización y el permiso de escritura o publicación de plantillas. Enviar una sugerencia solo requiere el permiso de sugerencia de plantillas.
- Crear y publicar un método de riesgo son permisos separados y requieren ámbito en toda la organización.
- Los comandos de riesgo necesitan una conexión en línea actual; sin conexión no se pone nada en cola ni se aprueba nada.

## Paso a paso
1. Vea las plantillas en la lista. Cada fila muestra el estado, la revisión **Vigente** y el recuento de **Suggestions**. Abra una plantilla.
2. Para una plantilla nueva, pulse **Nueva plantilla**. Complete **Código de plantilla**, **Título**, **Tipo de trabajo**, **Ámbito buque / remolcador**, **Responsable de aprobación**, **Motivo del cambio**, **Required barriers**, **SMS / requirement sources** y **Peligros**, y luego pulse **Guardar borrador**.
3. En una revisión en borrador, pulse **Enviar a revisión**. Los elementos que faltan se enumeran como elementos bloqueantes.
4. Un publicador introduce **Vigente desde** y un **Motivo** de al menos 10 caracteres y pulsa **Publicar (registro humano HSEQ)** o **Devolver**.
5. Para cambiar una plantilla publicada, escriba un motivo y pulse **Iniciar nueva revisión**; la revisión anterior no cambia.
6. La tripulación escribe una sugerencia en **Enviar sugerencia** y la envía. Una persona autorizada la revisa con **Aceptar** o **Rechazar** y un motivo.
7. En **Métodos de riesgo (matriz)**, use **Nuevo método** para introducir **Código del método**, **Likelihood codes**, **Severity codes** y **Matrix cells**; otra persona lo publica con **Publicar método**.

## Qué significan los estados
- **Borrador / En revisión / Devuelto / Publicado / Sustituido:** el ciclo de vida de la revisión de la plantilla.
- **Vigente / Programado / No eficaz:** la vista de vigencia. La última revisión publicada cuya fecha de entrada en vigor ha llegado es **Vigente**; una con fecha futura es **Programado**. Una revisión futura nunca sustituye antes de tiempo a la anterior.
- **Desconocido:** el campo no está completado; un valor vacío nunca se rellena con un valor por defecto.
- **No published risk method: ratings stay not assessed.** (no hay método de riesgo publicado: las valoraciones siguen sin evaluar). El sistema no ofrece ninguna matriz ni umbral por defecto.

## Lo que no puede hacer / precaución
- Un autor de una revisión o la persona que la envió no puede publicar esa misma revisión; la publicación es un registro humano separado (separación de funciones).
- La publicación no es un permiso, ni una aceptación experta del riesgo, ni un catálogo de producción aprobado.
- Una plantilla se aplica a una evaluación de riesgos solo como **copia**; no incluye ninguna valoración, firma, confirmación de participantes, aprobación ni acuse. Cuando una plantilla cambia, las evaluaciones existentes no cambian por sí solas.
- Una revisión publicada nunca se edita; inicie una nueva revisión para cambiarla.

## Si algo sale mal
- **Bloqueado hasta completar los elementos indicados.** Complete los elementos bloqueantes y envíe de nuevo.
- **This record changed elsewhere.** (este registro cambió en otro lugar). Pulse **Recargar** para ver la revisión actual.
- **No tiene permiso para esta acción (segregación de funciones o alcance).** La publicación debe hacerla otra persona autorizada que no haya sido autora de la revisión; si falta un permiso, contacte con el administrador de la organización (13.03 §10).
- **Esta acción ya no corresponde al estado actual. Recargue.** El estado de la revisión ha cambiado.

## Páginas relacionadas
