---
key: renewal-workflow
title: Renovación de certificado
roles: [Master, Chief Engineer / Technical Superintendent, Regulatory / Assurance]
permissions: [certificate.read, certificate.write, certificate.renewal.write, certificate.renewal.verify, certificate.renewal.complete, certificate.renewal.owner, file.read]
related: [certificate-command-center, pre-survey-checklist, survey-planner, audit]
screenshots: []
reviewed: false
---
## Para qué sirve esta página
La página de renovación de certificado registra paso a paso la preparación de la renovación de un certificado y su flujo interno: preparación, solicitud, espera de la autoridad, el reemplazo emitido, la verificación y la finalización. Responde a: "¿En qué punto está la renovación de este certificado y quién da el siguiente paso?" Es solo un registro de flujo interno; no establece la validez legal ni la aprobación de la autoridad (13.03 §1, §8).

## Quién la usa
- **Capitán, Jefe de máquinas / Superintendente técnico:** preparan el borrador de renovación y registran los requisitos y la solicitud.
- **Responsable de renovación asignado:** hace avanzar el flujo. Es un rol que tienen los miembros con el permiso de responsable de renovación; sin él, el flujo no se puede abrir ni validar.
- **Verificador y finalizador:** comparan el reemplazo emitido con su fuente y lo verifican y, como persona distinta, completan la renovación (13.03 §1, §8).

## Antes de empezar
- Ver la página requiere lectura de certificados. Preparar un borrador requiere escritura de certificados y escritura de renovaciones en ese buque; verificar requiere verificación de renovaciones; completar requiere finalización de renovaciones.
- Elija primero el buque correcto en **Seleccione un buque** (13.03 §1).
- El certificado que se va a renovar debe tener una revisión guardada vigente. Un certificado sustituido no se puede renovar.
- Los pasos del flujo necesitan una conexión activa. Sin conexión verá "You are offline. Workflow steps are unavailable until the connection returns."

## Paso a paso
1. Abra un **Registro de renovación** desde **Borradores de renovación registrados**. Para una renovación nueva, use **Elegir certificado para renovación** para ir al [Centro de mando de certificados](/app/certificate-command-center) y pulse **Preparar renovación** en el registro.
2. Complete el **Formulario de renovación de certificado**: **Fecha declarada de vencimiento de renovación**, la ventana de reconocimiento, **Lugar previsto**, **Rol asignado del responsable de renovación** y **Declaración de inspección requerida**, **Declaración de documentos requeridos**, **Declaración de acciones requeridas**. Deje como **Desconocido** un valor que no conozca.
3. Guarde el borrador con **Guardar borrador sin verificar**.
4. En **Flujo de renovación**, elija en **Pasos disponibles** el paso abierto para su rol, por ejemplo **Confirmar requisitos completos**, **Registrar solicitud** o **Esperar decisión de la autoridad**. Complete los campos obligatorios y pulse **Registrar paso**.
5. Cuando la autoridad emita el nuevo certificado, use **Registrar reemplazo emitido** y selecciónelo. Su propio documento se usa como evidencia.
6. El verificador usa **Verificar reemplazo** y establece **El emisor coincide**, **El número de certificado coincide**, **El objeto coincide**, **Las fechas coinciden** y **El alcance coincide** comparando con la fuente.
7. Una persona distinta completa la renovación con **Completar y sustituir**. Si la autoridad la rechazó, use **Registrar rechazo de la autoridad**; si se abandona la preparación, use **Cancelar preparación**.

## Qué significan los estados
- **Borrador / En preparación:** la renovación se está preparando; el formulario se puede editar.
- **Listo para solicitar / Solicitud registrada / En espera de la autoridad:** la fase de solicitud. La decisión de la autoridad se produce fuera del sistema.
- **Emitido, sin verificar:** el reemplazo está registrado, pero ninguna persona lo ha comparado aún con su fuente.
- **Verificado por revisor:** el verificador registró la comparación; la renovación aún no está completada.
- **Completado:** el flujo interno está cerrado y el reemplazo sustituye al original. No es una decisión sobre la validez legal del certificado.
- **Cancelado / Rechazado:** el flujo se detuvo.
- Un requisito puede ser **Completo / No requerido / Desconocido**. **Desconocido** no significa "No requerido"; el servidor no hace avanzar el flujo con un requisito desconocido.

## Lo que no puede hacer / precaución
- La persona que registró el reemplazo emitido, o que introdujo ese certificado de reemplazo, no puede verificarlo. El finalizador debe ser distinto del verificador, de la persona que registró el reemplazo emitido y del autor del certificado de reemplazo.
- La verificación y la finalización son exclusivamente decisiones humanas; Completado ≠ Verificado.
- Las declaraciones de planificación no establecen reconocimientos completados, solicitudes aprobadas, aptitud para el viaje ni verificación.
- Los vínculos de reemplazo en borrador no sustituyen al original. El certificado anterior solo se cierra mediante el paso de finalización con un vínculo de sustitución trazable (13.03 §1).
- Cada paso se registra como una revisión inmutable; el historial permanece en **Historial de revisiones registradas**.
- Sin un responsable asignado explícitamente, el sistema nunca asigna por defecto a un administrador.

## Si algo sale mal
- **No hay pasos disponibles para su rol en este estado.** El siguiente paso corresponde a otro rol; compruebe su permiso y su designación y, si es necesario, contacte con el Tenant Admin (13.03 §10).
- **The renewal changed after it was loaded.** o **Esta no es la revisión actual.** Pulse **Recargar** y revise el estado actual.
- **El resultado es desconocido. Reintentar envía la misma solicitud.** Use **Reintentar la misma solicitud**.
- **Rechazado por segregación de funciones: otra persona debe realizar este paso. Usted registró, redactó o verificó un paso anterior de esta renovación, o todavía no consta ninguna verificación hecha por otra persona.**. El registro no cambió; recargar no ayuda. Deje el paso a otra persona autorizada.
- **El paso no se registró. Revise los datos e inténtelo de nuevo.** Compruebe los campos obligatorios y las selecciones de coincidencia.
- Si el reemplazo no se puede verificar, compruebe el documento y la trazabilidad de la fuente y remítalo al verificador (13.03 §10).

## Páginas relacionadas
