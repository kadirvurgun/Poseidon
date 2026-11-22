---
key: voice-event-draft
title: Borrador de evento por voz
roles: [Crew / OOW / Engineer, Master, DPA / HSEQ]
permissions: [voice.draft.read, voice.draft.write, hse.safety_event.report]
related: [safety-events]
screenshots: []
reviewed: false
---
## Para qué sirve esta página
La página Borrador de evento por voz convierte una nota breve hablada o escrita en **sugerencias de borrador** para un informe de evento de seguridad. Responde a la pregunta: "¿Se tomaron correctamente de lo que dije o escribí el tipo de evento, el lugar, la hora, lo que ocurrió y la acción inmediata?" Las sugerencias proceden de reglas locales fijas, cada una vinculada a sus propias palabras con un grado de confianza. Una persona edita y confirma el borrador; nada se envía, se cierra ni se notifica automáticamente (13.03 §1, §6).

## Quién la usa
- **Tripulación / OOW / Ingeniero y Capitán:** toman una nota rápida sobre el terreno e inician un informe de evento de seguridad (13.03 §2, §6).
- **DPA / HSEQ:** vigilan **Usabilidad medida (este buque)**. Estas cifras son una observación operativa, no una declaración de precisión.

## Antes de empezar
- Elija un buque de su ámbito. Crear un borrador requiere el permiso de escritura de borradores de voz y ámbito de edición en ese buque.
- Pasar un borrador confirmado a un borrador de evento de seguridad requiere además el permiso de informe de eventos de seguridad.
- Privacidad: el audio no se almacena. El reconocimiento de voz solo usa un motor local de esta instalación; si no hay ninguno instalado, escriba o pegue la nota. Evite nombres y datos personales de salud.
- Sin conexión no se envía nada.

## Paso a paso
1. Pulse **Nueva nota de voz o escrita**.
2. Si hay un motor local configurado, pulse **Grabar con motor local** y después **Detener y transcribir**. En caso contrario, escriba una nota breve en **Texto de la nota**: qué, dónde, cuándo, qué hizo usted.
3. Compruebe **Fecha de la nota**. Solo se usa para interpretar palabras como "hoy" o "ayer".
4. Pulse **Crear sugerencias de borrador**.
5. Compruebe cada campo: **Tipo de evento**, **Cuándo ocurrió (hora local)**, **Ubicación a bordo**, **Detalle del lugar**, **Qué ocurrió**, **Acción inmediata tomada**. Bajo cada campo verá la confianza de la sugerencia, la regla y la parte correspondiente de su nota. Lea también **Notas de las reglas**.
6. Corrija las sugerencias erróneas y pulse **Guardar cambios**. Cuando **Aún necesario antes de confirmar:** esté vacío, pulse **Confirmar borrador**. Confirmar registra que usted comprobó cada campo; no envía ni cierra ningún evento.
7. Tras la confirmación, si se creó un borrador de evento de seguridad, siga **Abrir el borrador del evento de seguridad** hasta la página de eventos de seguridad y cuasi accidentes y complételo y envíelo allí. Si aún no se ha creado ninguno y tiene el permiso, use **Crear ahora el borrador del evento de seguridad**.
8. Si la nota no es un evento o es errónea, elija un **Motivo**, añada **Nota (obligatoria para Otro)** si es necesario y pulse **Rechazar borrador**.

## Qué significan los estados
- **Borrador (sin confirmar):** ninguna persona ha confirmado todavía las sugerencias.
- **Confirmado por una persona:** los campos se comprobaron; esto no es un informe de evento de seguridad y no se envió nada.
- **Rechazado:** el motivo del rechazo se registra para su evaluación.
- **Sugerido, sin cambios / Cambiado por usted / Introducido por usted / Vacío:** el origen de un campo. Sugerido, sin cambios es una sugerencia que una persona aún debe comprobar.
- **Escrito o pegado / Motor de voz local:** cómo se introdujo la nota. Editar el texto del motor local lo convierte en una nota escrita.
- Las notas de las reglas como **Hora sin día; no se supuso fecha** indican información que falta; el sistema no adivina el valor que falta.
- Una nota como **2 faltantes**: cuántos campos obligatorios del borrador siguen vacíos en la lista.

## Lo que no puede hacer / precaución
- Las sugerencias son solo un borrador; una confianza no es una garantía de exactitud. Compruebe usted mismo cada campo antes de confirmar.
- Esta página no envía, cierra ni notifica ningún evento de seguridad. El informe se envía en la página de eventos de seguridad y cuasi accidentes.
- Si se mencionan evidencias, adjúntelas usted mismo; la página no adjunta ninguna evidencia.
- **Confirmar borrador** no está disponible mientras haya cambios sin guardar; guárdelos primero.
- Sin el permiso de informe de eventos de seguridad no se crea ningún borrador de evento de seguridad a partir de un borrador confirmado.

## Si algo sale mal
- **Speech capability unavailable: no local engine is configured.** o **Transcription was not completed.** Escriba o pegue la nota.
- **No se concedió acceso al micrófono.** Compruebe el permiso del navegador o escriba la nota.
- **The record changed meanwhile.** Pulse **Recargar** y vuelva a hacer su cambio.
- **You are offline. Nothing was sent.** Cuando tenga conexión, use **Reintentar el mismo comando**.
- **No tiene permiso para esta acción.** Compruebe su rol y el contexto del buque; si es necesario, contacte con el administrador del inquilino (13.03 §10).

## Páginas relacionadas
