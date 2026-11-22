---
key: certificate-command-center
title: Centro de certificados
roles: [Master, Chief Engineer / Technical Superintendent, Regulatory / Assurance]
permissions: [certificate.read, certificate.write, certificate.extract, certificate.verify.record, file.read, directory.write]
related: [renewal-workflow, pre-survey-checklist, survey-planner, audit]
screenshots: []
reviewed: false
---
## Para qué sirve esta página
El Centro de certificados lista las **declaraciones** de certificados del buque seleccionado con sus estados de calendario declarados. Responde a la pregunta: «¿Qué certificados de este buque están cerca de su vencimiento o de una ventana de inspección, y qué registros siguen esperando una revisión humana?». Las fechas mostradas son declaraciones. La página nunca decide que un certificado sea válido o esté verificado (13.03 §1, §8).

## Quién la usa
- **Capitán, Jefe de máquinas / Superintendente técnico:** siguen el calendario de certificados del buque, introducen nuevos borradores de certificados e inician la preparación de renovaciones.
- **Usuario de Normativa / Aseguramiento:** comprueba que las declaraciones se apoyen en una fuente oficial y versionada y registra la declaración de un **Auditor** asignado (13.03 §8).
- Los usuarios con acceso de solo lectura solo ven la lista y no pueden modificarla.

## Antes de empezar
- Para ver la página se necesita lectura de certificados. Para introducir borradores se necesita escritura de certificados y ámbito de edición sobre ese buque; para extraer sugerencias de documentos se necesitan lectura de archivos y extracción; para registrar una declaración humana se necesitan escritura de directorio, el permiso de registro de verificación y un nombramiento vigente de **Auditor** para este buque y certificado. Las declaraciones protegidas de tripulación requieren además lectura de tripulación, lectura de certificados de tripulación, lectura de identidad de tripulación y lectura de archivos.
- Elija primero el buque correcto: púlselo en la lista **Seleccione un buque** (13.03 §1).
- La página necesita una lectura en línea en vivo. Sin conexión verá «Current certificate sources unavailable offline or hidden.» y no se infiere ninguna autorización sin conexión.

## Paso a paso
1. Abra el buque desde la lista **Seleccione un buque**.
2. Use **Ámbito de certificados** para elegir **Certificados del buque** o, si tiene el permiso, **Declaraciones protegidas de tripulación**. Acote la lista con **Filtro de calendario declarado**.
3. Abra un registro pulsando su fila. Las filas llevan la etiqueta **Declaración de certificado sin verificar**.
4. Para un registro nuevo pulse **Nuevo borrador de certificado**. Rellene **Nombre del documento**, **Base normativa declarada**, **Bandera declarada**, la ventana de inspección y **Disponibilidad declarada** a partir del documento fuente y pulse **Guardar borrador sin verificar**.
5. Para obtener sugerencias del documento vinculado pulse **Extraer del documento vinculado** en **Revisar datos del documento**. Compare cada sugerencia con la fuente, marque **He comprobado este dato con la fuente** y **He comprobado que este documento pertenece a este buque y certificado** y pulse **Confirmar datos seleccionados y guardar borrador**.
6. Como Auditor asignado, en **Registro de declaración humana** rellene **Seleccionar asignación real** y **Seleccionar afirmación registrada**, introduzca el motivo y pulse **Registrar declaración sin evaluar**.
7. Para iniciar una renovación use **Preparar renovación** en **Trabajo relacionado con certificados** del registro. Esto le lleva al flujo de [Renovación de certificados](/app/renewal-workflow).

## Qué significan los estados
- **Vencimiento declarado <90 días / <60 días / <30 días / Vencimiento declarado superado:** una observación de calendario a partir de la fecha de vencimiento declarada. No es una decisión sobre la validez.
- **Ventana de inspección declarada abierta / Fuera del plazo de vencimiento:** posición respecto a la ventana de inspección declarada.
- **Revisión del calendario requerida:** una fecha falta, es ambigua o es contradictoria. El registro no está listo hasta que una persona lo compruebe con el documento fuente.
- **Desconocido:** el valor no se declaró. Un valor desconocido nunca significa «no falta» ni «conforme»; necesita revisión.
- **Confianza baja — revisar / Ambiguo · revisar / Revisión requerida:** una sugerencia del documento que todavía no ha confirmado una persona. Una puntuación de confianza no es una verificación.
- «Solo esta página. Las observaciones de calendario están separadas de la cualificación; no son totales de disponibilidad de toda la flota.»

## Lo que no puede hacer / precaución
- Guardar un borrador no verifica el certificado. Escribir el registro, confirmar las sugerencias del documento y la declaración del Auditor son pasos separados; Completado ≠ Verificado.
- La extracción automática solo sugiere. Los valores que faltan siguen siendo desconocidos y solo los datos que usted selecciona y corrige actualizan el borrador.
- Un rol de administrador por sí solo no permite registrar una declaración humana; se requieren un nombramiento vigente y el archivo fuente.
- Un certificado guardado nunca se modifica en silencio. Cada guardado crea una nueva revisión y las revisiones anteriores permanecen en **Historial de revisiones registradas** (13.03 §1).
- La página no concede ninguna aprobación de la sociedad de clasificación, de la bandera ni de la autoridad, y no decide la aptitud para el viaje. Esa decisión corresponde a las personas autorizadas.

## Si algo sale mal
- **Fuente de certificados o acceso no disponible. No se infiere inventario válido, vacío ni verificado.** Pulse **Reintentar lectura de fuente**. Si persiste, compruebe su rol, el buque y el ámbito del objeto; contacte con el administrador del tenant si es necesario (13.03 §10).
- **Resultado desconocido.** Conserve el comando original y reenvíe la misma solicitud con **Reintentar comando exacto de certificado**.
- **La fuente actual ha cambiado.** Sus campos locales se conservan; use **Volver a registros**, vuelva a leer el registro actual y rehaga su cambio.
- **La lectura automática no está disponible para este documento:** introduzca los valores manualmente a partir del documento fuente.
- Si un registro no se puede verificar, revise su evidencia y el linaje de su fuente y remítalo al verificador (13.03 §10).

## Páginas relacionadas
