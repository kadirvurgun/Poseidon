---
key: industry-assurance
title: Aseguramiento sectorial (SIRE 2.0)
roles: [DPA / HSEQ, Regulatory / Assurance, Master]
permissions: [assurance.inspection.read, assurance.inspection.import, assurance.import.accept, assurance.evidence.write, assurance.finding.link, assurance.mapping.write, file.read, hse.safety_event.read]
related: [safety-events, audit-ready, regulatory-review]
screenshots: []
reviewed: false
---
## Para qué sirve esta página
La página Aseguramiento sectorial importa desde un libro de cálculo los resultados de inspección SIRE 2.0 de un buque tanque, somete los cambios a revisión humana, vincula los hallazgos a acciones de seguridad y registra evidencias de campo. Responde a la pregunta: «¿Qué hallazgos de esta inspección están abiertos, cuáles tienen una acción vinculada y dónde está la evidencia?». Las importaciones son borradores: una persona distinta acepta cada elemento modificado. Los hallazgos permanecen abiertos y sin verificar; esta página nunca los cierra ni los verifica. La cadena es Hallazgo → Acción → Verificación, y la verificación y la eficacia son registros humanos separados sobre la acción en Eventos de seguridad y cuasiaccidentes (13.03 §4, §8).

## Quién la usa
- **DPA / HSEQ:** vincula los hallazgos de inspección a acciones y sigue la cadena Hallazgo → Acción → Verificación (13.03 §4).
- **Usuario de Normativa / Aseguramiento:** gestiona las correspondencias de columnas y prepara o revisa importaciones (13.03 §8).
- **Capitán y personal del buque:** registran evidencias de campo (fotos) para un hallazgo.

## Antes de empezar
- Elija un buque. SIRE 2.0 solo se aplica a buques tanque. Si el tipo de buque no está declarado en el perfil del buque, las importaciones permanecen en **revisión requerida**.
- La lectura requiere el permiso de lectura de inspecciones. Importar, aceptar una importación, escribir evidencias, vincular hallazgos y escribir correspondencias son permisos separados. Vincular una acción requiere además el permiso de lectura de eventos de seguridad; registrar evidencias requiere además lectura de archivos.
- El esquema de columnas es sintético; no se usa el formato con licencia de OCIMF.
- Sin conexión, solo las capturas de fotos se ponen en cola en este dispositivo; las importaciones y las decisiones requieren conexión.

## Paso a paso
1. **Elija un buque** y compruebe la línea **Aplicabilidad**.
2. Si es necesario, en **Correspondencias de columnas** introduzca una **Clave de correspondencia**, escriba una línea por columna (encabezado=campo) y pulse **Guardar como versión nueva**.
3. En **Preparar importación** elija la **Versión de correspondencia**, la **Referencia de inspección**, la **Fecha de inspección** y el **Libro (.xlsx)** y pulse **Enviar a revisión**.
4. Otra persona, en **Revisión de la importación**, elige **Aceptar** o **Mantener el actual** para cada elemento modificado, escribe un **Motivo** de al menos 10 caracteres y pulsa **Aceptar importación** o **Rechazar importación**.
5. Abra una inspección desde la lista **Inspecciones**. Revise **Elementos** y **Hallazgos**.
6. Para vincular un hallazgo a una acción, introduzca el **Id. de la acción de seguridad** y pulse **Vincular acción**.
7. Para una evidencia de campo, introduzca **Medio**, **Id. del archivo limpio**, **Revisión del archivo** y **Suma de comprobación (SHA-256)** y pulse **Registrar evidencia de campo**. Sin conexión, el botón pasa a ser **Guardar en este dispositivo**; cuando esté en línea, envíela con **Enviar ahora**.

## Qué significan los estados
- **Tanque: aplicable / No aplicable / Tipo de buque desconocido: requiere revisión:** con un tipo de buque desconocido el resultado permanece en **Revisión requerida**; nunca se presupone que no sea aplicable.
- **Pendiente de revisión / Aceptada / Rechazada:** el estado de la importación.
- **Nuevo / Modificado / Sin cambios / No incluido en esta importación:** el elemento comparado con la importación aceptada anteriormente. Un elemento no incluido en esta importación queda como **Se conserva (nunca se elimina)**.
- **Observación negativa / Observación positiva / Sin observación / Desconocido:** la observación sobre un elemento.
- **Abierto / No verificado:** aquí los hallazgos siempre se muestran como abiertos y sin verificar.
- **Sin acción vinculada / Acción vinculada:** si el hallazgo está vinculado a una acción de seguridad. Una acción vinculada no significa que el hallazgo esté verificado ni que la acción sea eficaz.
- **Capturado en línea / Capturado sin conexión:** cómo se capturó la evidencia.

## Lo que no puede hacer / precaución
- La persona que preparó una importación no puede revisarla: «Usted preparó esta importación; otra persona debe revisarla.».
- Esta página no cierra ni verifica hallazgos. Verificado no es Eficaz; la eficacia es una revisión humana separada sobre la acción.
- Las evidencias en vídeo no están disponibles hasta que el operador habilite un tipo de archivo de vídeo analizado.
- Un elemento que falta en una importación posterior nunca se elimina; el historial se conserva.

## Si algo sale mal
- **La solicitud fue rechazada:** resuelva los motivos que se indican debajo.
- **El registro cambió entretanto. Recargue y revise de nuevo.** Pulse **Recargar**.
- **El resultado es desconocido (conexión perdida).** Use **Reintentar la misma solicitud**.
- **No tiene autorización para leer registros de aseguramiento sectorial.** o **No tiene permiso para hacer esto.** Compruebe su rol y el contexto del buque; si es necesario, contacte con el administrador del tenant (13.03 §10).
- Si una evidencia no se puede verificar, compruebe el id. del archivo, la revisión y la suma de comprobación SHA-256; si es necesario, contacte con el verificador (13.03 §10).

## Páginas relacionadas
