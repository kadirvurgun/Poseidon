---
key: departure-signoff
title: Preparación de salida y firma del capitán
roles: [Master, Shore Operations, Technical Superintendent]
permissions: [departure.readiness.read, departure.decide]
related: [today, fleet, defects, safety-events, certificate-command-center, data-trust-center]
screenshots: []
reviewed: false
---
## Para qué sirve esta página
Esta página muestra una **evaluación orientativa de la preparación** para el buque seleccionado y el periodo de salida previsto, y registra la decisión del capitán nombrado. Responde a la pregunta: «¿Qué dicen las fuentes de tripulación, técnicas, reglamentarias, de emergencia y de eventos de seguridad para este periodo, y por qué?». La decisión de autorizar, retener o detener la salida corresponde al **Capitán** (13.03 §2). El sistema nunca autoriza una salida; una firma registrada no es un despacho de salida.

## Quién la usa
- **Capitán:** para revisar la evaluación y la lista «Por qué» y, después, registrar Autorizar, Retener o Denegar con un motivo (13.03 §2).
- **Operaciones en tierra / Superintendente técnico:** para seguir en modo de solo lectura la evaluación y las decisiones registradas del capitán. El personal de tierra no puede registrar una decisión en lugar del capitán.

## Antes de empezar
- En la barra de contexto superior, compruebe que esté seleccionada la **empresa correcta**. La página solo lista buques de su ámbito (13.03 §1).
- Necesita acceso de lectura a la preparación de salida para ver la página.
- El permiso de decisión por sí solo no basta para registrar una decisión: también necesita un **nombramiento vigente de capitán** para este buque. Sin él la página indica «No tiene una designación vigente de capitán para este buque. Las decisiones son de solo lectura.».

## Paso a paso
1. Elija el buque en la lista «Elija un buque».
2. En **Periodo de salida previsto**, rellene «Fecha de salida» y «Fin del viaje» y pulse **Evaluar preparación**.
3. En la línea **Evaluación**, lea la insignia de estado, el modo «Actual» o «Previsión», «Evaluado el» y «Válido hasta».
4. En la tabla de categorías, revise los resultados de Tripulación, Técnico, Reglamentario, Emergencia y Eventos de seguridad.
5. Lea la lista **Por qué**. Cada fila muestra una observación que no superó la comprobación, su código de motivo y su fuente. Revise una fila inesperada en la propia página de la fuente.
6. Si la evaluación no es actual y Listo, lea **Por qué Autorizar no está disponible**. Indica cuándo el periodo es una previsión, lista las categorías que todavía no son correctas con sus resultados y explica qué registro cerraría las carencias de fuente de Emergencia (todavía no hay ninguna fuente de simulacros 61.02 conectada) y de Reglamentario (no hay definido ningún conjunto de certificados exigidos). Solo explica las categorías del servidor; no modifica la compuerta.
7. En **Decisiones del capitán**, vea las decisiones registradas anteriormente y su validez.
8. Si usted es el capitán, en **Registrar decisión del capitán** elija Autorizar, Retener o Denegar, seleccione su nombramiento en «Designación», escriba un «Motivo» (10–2000 caracteres) y pulse **Registrar**.
9. Use **Actualizar** para volver a leer la evaluación.

## Qué significan los estados
- **Listo:** ninguna fuente registrada tiene una observación que no haya superado la comprobación. Es solo información; la decisión de salida sigue siendo del capitán.
- **Condicional:** al menos una fuente informa de una condición. Autorizar no se ofrece para esta evaluación.
- **No listo:** al menos una fuente devuelve «Fallo». Que otras categorías sean correctas no lo compensa.
- **Revisión requerida:** al menos una entrada es «Desconocido». Una persona autorizada debe examinarla.
- **Correcto / Condición / Fallo / Desconocido:** el resultado de la categoría en la tabla de categorías. **Desconocido** significa que la fuente no se pudo leer o no tiene datos; no significa «Correcto» ni «Listo». En esta versión no hay ninguna fuente de preparación para emergencias conectada para la categoría Emergencia, por lo que esa fila muestra Desconocido. Reglamentario puede seguir en Desconocido aunque todos los certificados registrados sean válidos, porque ninguna fuente define el conjunto de certificados exigidos para el buque. Reglamentario incluye también las señales de confianza de datos (DATA_TRUST_*); sin acceso de lectura a la confianza de datos, Reglamentario muestra Desconocido.
- **Previsión:** el periodo no incluye el día de hoy. Es solo una previsión de planificación y no se puede autorizar.
- **Actual / Obsoleta — entradas cambiadas:** la validez de una decisión registrada. Cuando cambian las entradas, la decisión anterior pasa a obsoleta y no se elimina.

## Lo que no puede hacer / precaución
- El sistema nunca autoriza una salida y no muestra ningún estado como despacho de salida. Una decisión registrada es el propio registro humano del capitán nombrado.
- Autorizar solo puede registrarse para una evaluación READY actual; todas las categorías deben ser correctas. Como la categoría Emergencia permanece en Desconocido en esta versión, la evaluación no puede llegar a Listo y Autorizar no se ofrece; **Por qué Autorizar no está disponible** muestra el motivo. Incluso con Listo, la decisión de autorizar es del capitán; el sistema no aprueba nada. «Retener y Denegar siempre pueden registrarse con un motivo.»
- Cada decisión requiere un motivo. Las decisiones registradas no se modifican; una nueva decisión es un nuevo registro.
- No puede registrar una decisión mientras la evaluación esté obsoleta o usted esté sin conexión; actualice primero.

## Si algo sale mal
- **Está sin conexión:** la evaluación puede estar desactualizada y la firma está desactivada. Actualice cuando vuelva a estar en línea (13.03 §10).
- **No tiene acceso a la preparación de salida de este buque:** compruebe su rol y el contexto del buque; contacte con el administrador del tenant si es necesario.
- **Preparación inesperada:** abra la lista «Por qué»; si una fuente parece incorrecta, plantéelo al responsable del control o a un experto en la materia.
- **El resultado es incierto:** «Reintentar» envía el mismo comando. No escriba una nueva decisión.
- **El historial de decisiones cambió / La evaluación cambió:** use «Recargar», revise la evaluación actual y después decida.
- **El comando no fue aceptado:** comuníquelo al soporte con una captura de pantalla, la hora y los pasos realizados.

## Páginas relacionadas
