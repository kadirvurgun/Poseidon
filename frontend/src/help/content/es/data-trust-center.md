---
key: data-trust-center
title: Centro de confianza de datos
roles: [Regulatory / Assurance, DPA, Technical Superintendent, Master]
permissions: [control.read, evidence.read, file.read, applicability.read, regulatory.draft.read]
related: [departure-signoff, fleet, industry-assurance, audit-ready, sources]
screenshots: []
reviewed: false
---
## Para qué sirve esta página
El centro de confianza de datos muestra **señales de calidad de los datos** de los controles, las evidencias y los campos del perfil del buque seleccionado. Responde a la pregunta: «¿Qué registros de este buque faltan, están obsoletos, son contradictorios, no están verificados o están huérfanos, y qué información de fuente y de actualidad lo indica?». Una señal de confianza no es una verificación del registro subyacente. La página nunca marca nada como listo.

## Quién la usa
- **Usuario de Normativa / Aseguramiento:** para no dejar registros huérfanos o incompletos en la cadena Requisito → Control → Evidencia → Verificación (13.03 §8).
- **DPA:** para seguir las carencias de evidencias y verificaciones (13.03 §4).
- **Superintendente técnico / Capitán:** para ver las señales que mantienen una evaluación de disponibilidad en revisión requerida (13.03 §2, §5).

## Antes de empezar
- En la barra de contexto superior, compruebe que esté seleccionada la **empresa correcta** (13.03 §1).
- La página necesita acceso de lectura a controles, evidencias, archivos, aplicabilidad y borradores normativos del tenant, todos a la vez, con ámbito de lectura en todo el tenant.
- Si no hay ningún buque seleccionado, la página muestra primero la lista «Elija un buque».

## Paso a paso
1. Elija el buque en la lista «Elija un buque».
2. En **Resumen**, lea el estado general («Revisión requerida» o «Sin señal relevante») y los recuentos de **Objetos por señal**.
3. En la tabla **Controles y evidencias**, revise las columnas «Fuente / procedencia», «Actualidad», «Verificación» y «Señales» de cada objeto.
4. Anote el código de motivo junto a la insignia de una señal y revise el registro en su propia página.
5. Vea las señales de los campos del perfil en la tabla **Campos del perfil del buque**.
6. Use **Anterior** / **Siguiente** para otros objetos.
7. Si aparece «Es posible que estas señales estén desactualizadas.», pulse **Recargar**.

## Qué significan los estados
- **Faltante / Obsoleto / Contradictorio / No verificado / Huérfano:** el tipo de señal sobre un objeto. Una señal es un marcador de calidad; no demuestra que el registro sea correcto o incorrecto.
- **Sin señal:** no se encontró ninguna señal sobre este objeto. Esto no significa que el registro esté verificado.
- **Revisión requerida:** existe una señal relevante. La disponibilidad se mantiene en revisión requerida mientras existan señales relevantes.
- **Sin señal relevante:** esto no es una declaración de disponibilidad.
- **Verificado por una persona:** una persona autorizada verificó la evidencia. Esto procede del propio registro de la evidencia; el centro no verifica nada.
- **No verificado / Verificación caducada / Verificación rechazada:** el estado de verificación de la evidencia.
- **No aplica a controles:** se muestra en las filas de controles; el estado de verificación corresponde a las filas de evidencias.
- **Hora de observación desconocida / Sin política de actualidad / Fechado en el futuro:** no se pudo establecer la actualidad o la fecha está en el futuro. Ninguno de estos estados significa «actual».

## Lo que no puede hacer / precaución
- Los registros no se corrigen, verifican ni cierran desde esta página; eso lo hace una persona autorizada en la propia página del registro.
- Si solo se evaluaron los primeros objetos, el resumen sigue en revisión requerida.
- Los objetos que no puede leer se cuentan pero no se muestran, y el resumen permanece entonces en Revisión requerida. Un registro que no puede ver no significa que no haya ningún problema.
- Sin conexión, las señales de confianza de datos no se pueden actualizar.

## Si algo sale mal
- **No tiene autorización para leer señales de confianza de datos:** compruebe su rol y el contexto del buque; contacte con el administrador del tenant si es necesario (13.03 §10).
- **El registro no se puede verificar:** revise la evidencia y su procedencia; plantéelo al verificador autorizado.
- **Señal inesperada:** plantee el código de motivo al responsable del control o a un experto en la materia.
- **Las señales de confianza de datos no están disponibles:** use «Reintentar»; si persiste, comuníquelo al soporte con una captura de pantalla, la hora y los pasos realizados.

## Páginas relacionadas
