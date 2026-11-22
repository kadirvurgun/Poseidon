---
key: survey-planner
title: Planificador de reconocimientos
roles: [Master, Chief Engineer / Technical Superintendent, Regulatory / Assurance]
permissions: [certificate.read]
related: [pre-survey-checklist, certificate-command-center, renewal-workflow, drydock-planning]
screenshots: []
reviewed: false
---
## Para qué sirve esta página
El planificador de reconocimientos muestra las ventanas de reconocimiento declaradas en un calendario de la flota, junto con la idoneidad respecto al viaje y al puerto. Responde a: "En el rango que elegí, ¿qué certificados de qué buques tienen una ventana de reconocimiento que se abre o que ya pasó, y existe una lista de verificación de preparación?" Es solo una ayuda de planificación: no es un calendario de la autoridad, una reserva ni un resultado de reconocimiento (13.03 §8).

## Quién la usa
- **Capitán, Jefe de máquinas / Superintendente técnico:** ven los próximos reconocimientos e inician a tiempo la lista de verificación de preparación (13.03 §5).
- **Usuario de Regulatory / Assurance:** vigila las ventanas declaradas y las declaraciones que faltan en toda la flota (13.03 §8).

## Antes de empezar
- Ver la página requiere lectura de certificados. Solo se enumeran los buques a los que tiene acceso.
- Las ventanas proceden de las declaraciones del registro del certificado. Si no se ha declarado ninguna ventana, la fila aparece en **Ventana no declarada**; introduzca las fechas del documento de origen en el [Centro de mando de certificados](/app/certificate-command-center).
- La página necesita una lectura en vivo. Sin conexión verá "Offline: a current online read is required."

## Paso a paso
1. En **Rango del calendario**, elija **Desde** y **Hasta** y pulse **Mostrar**.
2. Lea en la línea de resumen los recuentos de abiertas, próximas, vencidas y desconocidas.
3. En cada fila, revise **Certificado**, **Emisor**, **Bandera declarada**, **Base normativa**, **Vencimiento declarado**, **Ventana de reconocimiento** e **Idoneidad viaje/puerto**.
4. Pulse **Abrir certificado** para ver el certificado.
5. Use **Abrir lista de verificación** si existe una lista de preparación, o **Iniciar lista de verificación** si no existe, para ir a la [Lista de verificación previa al reconocimiento](/app/pre-survey-checklist).

## Qué significan los estados
- **Próxima / Ventana abierta:** la posición respecto a la ventana declarada.
- **Ventana vencida, cumplimiento no registrado:** la ventana ha terminado y ningún registro muestra que se haya realizado el reconocimiento. Tampoco demuestra que se haya omitido el reconocimiento; compruebe los registros.
- **Ventana desconocida:** la fecha falta o no se puede leer. Una ventana desconocida nunca significa "sin problemas"; necesita revisión.
- Idoneidad viaje/puerto: **Condicional**, **No adecuado**, **Desconocido**. La página nunca da una aprobación de "adecuado".
- Preparación de la lista de verificación: **Bloqueado**, **Incompleto**, **Preparado, pendiente de confirmación humana**.

## Lo que no puede hacer / precaución
- El planificador no reserva nada ni presenta nada ante una autoridad. La coordinación con la clase, la bandera y la autoridad es externa y aquí no se simula.
- Lo que muestra el calendario no es un resultado de reconocimiento ni la validez del certificado. Las decisiones de viaje y de reconocimiento corresponden a las personas autorizadas.
- La página es de solo lectura; los cambios se hacen en las páginas de certificados y de listas de verificación.

## Si algo sale mal
- **Solo se muestra parte de la flota; reduzca el rango.** Elija un rango de calendario más corto.
- **No hay certificados con ventanas de reconocimiento en este rango.** Amplíe el rango o compruebe las declaraciones de los certificados.
- **No se pudo cargar. Inténtelo de nuevo.** Pulse **Reintentar**. Si persiste, comuníquelo al soporte con la pantalla, la hora y los pasos (13.03 §10).
- **No tiene acceso a esta vista.** Compruebe su rol y el ámbito de buques; si es necesario, contacte con el Tenant Admin (13.03 §10).

## Páginas relacionadas
