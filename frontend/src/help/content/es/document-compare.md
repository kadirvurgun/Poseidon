---
key: document-compare
title: Comparación de revisiones de documento
roles: [Master, DPA, Regulatory / Assurance, Chief Engineer, Crew / OOW]
permissions: [file.read, document.acknowledge, document.restricted.download, document.isps.read]
related: [data-trust-center, audit, sources, today]
screenshots: []
reviewed: false
---
## Para qué sirve esta página
Esta página muestra **dos revisiones de un documento controlado una al lado de la otra, en solo lectura**, para el buque seleccionado. Responde a la pregunta: «¿Qué diferencias hay entre estas dos revisiones a nivel de archivo y de metadatos, y cuál es el historial de publicación, verificación y custodia de cada una?». La página muestra diferencias; no aprueba, publica ni juzga cambios. No pone en vigor una revisión ni hace que sustituya a una anterior. Los originales de las evidencias nunca se modifican en silencio; cuando una revisión nueva reemplaza a una anterior, el estado de publicación de la anterior pasa a Sustituida y el evento aparece en su historial de custodia (13.03 §1).

## Quién la usa
- **Capitán / Jefe de máquinas / Tripulación:** para comparar la revisión vigente con la anterior y registrar un acuse de lectura (13.03 §2, §5, §6).
- **DPA / Usuario de Normativa y aseguramiento:** para revisar el historial de revisiones y los registros de verificación (13.03 §4, §8).

## Antes de empezar
- Si no hay ningún buque seleccionado, la página indica **Elija un buque de su ámbito.** y lista los buques. Seleccione el buque correcto.
- Necesita acceso de lectura de archivos para ver la página. El contenido restringido y los documentos restringidos ISPS necesitan además su propio permiso.
- Cargar contenido y registrar un acuse de lectura solo funcionan en línea.

## Paso a paso
1. Abra un documento desde la lista **Documentos controlados**. Las etiquetas de clasificación y de **Restringido ISPS** se muestran en la fila.
2. En **Elija dos revisiones**, seleccione las revisiones **Izquierda (base)** y **Derecha (revisada)**. La página propone primero la revisión más reciente a la derecha y la anterior a la izquierda.
3. Pulse **Comparar**. Use **Intercambiar** para cambiarlas de lado.
4. Lea la declaración de bytes y la tabla **Comparación de metadatos**: **Tipo de archivo**, **Tamaño (bytes)**, **SHA-256 del contenido**, **Recepción**, **Publicación**, **Verificación** y los campos de hora.
5. Revise el **Historial de aprobación y custodia** de cada lado.
6. Para ver el contenido, pulse **Cargar contenido (acceso registrado)**. El contenido se muestra con una marca de agua de atribución.
7. Después de **Cargar contenido (acceso registrado)**, si ha leído la revisión vigente, marque **He leído esta revisión y pude acceder a su contenido.** y pulse **Registrar acuse de lectura**.

## Qué significan los estados
- **Sin publicar / Vigente / Sustituida:** el estado de publicación de la revisión. Una revisión que no está vigente muestra **No vigente operativamente**.
- **No verificado / Verificada válida / Verificada no válida / Verificación no concluyente:** el registro de verificación de la revisión. No verificado no significa «válido».
- **Igual / Cambiado / No comparable:** el resultado de una fila de metadatos. No comparable no significa «Igual».
- **Archivos idénticos byte a byte:** solo bytes idénticos; no es una revisión del contenido.
- **Los archivos difieren a nivel de bytes:** qué regla cambió y su importancia requieren la revisión de una persona.
- **Contenido restringido para su rol; solo metadatos.** el contenido no se abre para este rol.

## Lo que no puede hacer / precaución
- Desde esta página no puede publicar, aprobar, poner en vigor ni sustituir un documento.
- La página no interpreta automáticamente las diferencias de contenido; una persona autorizada decide su importancia.
- Un acuse de lectura solo se registra para la revisión vigente, una vez por persona y revisión. No es una aprobación ni una verificación.
- Una copia impresa no está controlada. El contenido no se almacena en caché ni se conserva sin conexión.
- Sin conexión, la comparación está desactualizada y no se puede cargar contenido nuevo.

## Si algo sale mal
- **Este documento no está disponible para usted:** compruebe el contexto del buque y su permiso; contacte con el administrador del tenant si es necesario (13.03 §10).
- **No tiene permiso para esta acción:** compruebe su rol y el contexto del buque (13.03 §10).
- **El registro cambió mientras tanto:** use **Recargar** para abrir la versión más reciente.
- **Una revisión no se puede verificar:** revise el historial de evidencias y de procedencia; hable con un verificador autorizado (13.03 §10).
- **La solicitud falló:** envíe al soporte la pantalla, la hora y los pasos realizados (13.03 §10).

## Páginas relacionadas
