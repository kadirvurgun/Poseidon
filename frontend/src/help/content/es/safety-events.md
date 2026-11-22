---
key: safety-events
title: Eventos de seguridad y casi accidentes
roles: [DPA / HSEQ, Master, Crew / OOW / Engineer]
permissions: [hse.safety_event.read, hse.safety_event.report, hse.safety_event.triage, hse.safety_event.identity.read, hse.safety_event.investigate, hse.safety_event.review, hse.safety_action.write, hse.safety_action.verify, hse.safety_action.effectiveness]
related: [voice-event-draft, risk-assessment, high-risk-work, industry-assurance]
screenshots: [1]
reviewed: false
---
## Para qué sirve esta página
La página de eventos de seguridad y casi accidentes le permite notificar rápidamente un evento de seguridad en un buque y seguir los pasos humanos que vienen después: clasificación inicial, investigación, revisión independiente, acciones correctivas y preventivas, verificación, eficacia y cierre. Responde a: "¿Qué ocurrió, quién lo está gestionando y funcionó realmente la acción?" Aquí nada decide automáticamente la gravedad, la causa raíz ni el cierre; cada uno es un registro humano separado. La página sigue la cadena Hallazgo → Acción → Verificación, y **cerrado no es lo mismo que eficaz** (13.03 §4).

## Quién la usa
- **Tripulación / OOW / Ingeniero y Capitán:** notifican casi accidentes, incidentes, accidentes, fatiga y eventos ambientales o cibernéticos (13.03 §2, §6).
- **DPA / HSEQ:** como enlace de seguridad, vigilan los eventos críticos y las acciones vencidas y gestionan la investigación y la eficacia de las acciones (13.03 §4).
- La clasificación, la investigación, la revisión, la lectura de identidad, la escritura de acciones, la verificación y la revisión de eficacia son permisos separados; cada usuario solo ve las partes que tiene.

## Antes de empezar
- Elija el buque correcto (**Elija un buque**). Sin acceso verá "No tiene acceso a los eventos de seguridad de este buque."
- Notificar requiere el permiso de notificación; leer la lista requiere el permiso de lectura.
- Un informe **Confidencial** y **Enviar informe** necesitan conexión. Sin conexión solo puede guardar un borrador con nombre, y solo en un dispositivo preparado de antemano.
- La hora se introduce en UTC.

## Paso a paso
1. Vea los eventos del buque en la lista **Eventos**. Use **Solo mis informes** para ver solo los suyos.
2. Para un registro nuevo, pulse **Notificar un evento**. Elija **Con nombre** o **Confidencial** como **Modo de notificación**. Para un informe confidencial que necesite seguimiento, elija **Darme un código de seguimiento privado**.
3. Complete **Tipo de evento**, **¿Cuándo ocurrió?**, **Operación**, **Ubicación a bordo**, **Resumen**, **Qué ocurrió**, **¿Se tomó una acción inmediata?** y, opcionalmente, **Su estimación de gravedad**. Si no se conoce la hora, elija **Desconocido** y complete **¿Por qué se desconoce la hora?**.
4. Puede usar **Guardar borrador** mientras esté incompleto. Cuando **Aún necesario antes del envío** esté vacío, pulse **Enviar informe**. Un borrador no enviado puede retirarse con **Retirar borrador** y un motivo.
5. Si se emitió un código de seguimiento, guarde de inmediato y en privado el código de **Su código de seguimiento confidencial** y pulse **Lo he guardado**. Solo se muestra una vez. Más tarde, introdúzcalo como **Código de seguimiento** en **Seguimiento confidencial** y pulse **Abrir** para leer las respuestas del equipo.
6. Un clasificador registra la gravedad y **Investigación requerida** con un motivo en **Clasificación inicial** y pulsa **Registrar clasificación**, o lo devuelve con **Devolver al notificante**.
7. Un investigador pulsa **Abrir investigación**, completa **Factores contribuyentes**, **Causas raíz**, **Barreras** e **Incertidumbres**, y luego pulsa **Guardar investigación** y **Enviar a revisión**. En **Revisión independiente**, otra persona elige **Aprobar**, **Devolver** o **Rechazar**.
8. En **Acciones correctivas y preventivas**, use **Añadir acción** con un responsable, una fecha límite y el hallazgo que aborda. El responsable registra **Informar finalización**, otra persona registra **Verificar** y una tercera registra **Revisar eficacia**.
9. Cuando **Impedimentos para el cierre** esté vacío, un revisor (con permiso de revisión) que no sea ni el notificante ni un autor de la investigación pulsa **Cerrar el evento**. En **Mensajes** puede enviar mensajes con visibilidad **Solo el equipo** o **Compartido con el notificante**.

## Qué significan los estados
- **Borrador / Enviado / Devuelto / Clasificado / En investigación / Seguimiento de acciones / Cerrado / Retirado:** el ciclo de vida del evento.
- **Gravedad — Not triaged / Sin evaluar:** aún no hay clasificación humana. La **Estimación del notificante** es solo una sugerencia. **Severity policy: not configured – human classification only** indica que el sistema no deriva ninguna gravedad.
- **La necesidad de investigación es UNKNOWN:** no se ha tomado ninguna decisión; no significa "no" y bloquea el cierre.
- **Acción — Abierta / Completada / Verificada:** Completada no es Verificada, y Verificada no es Eficaz.
- **Eficacia — Pendiente / Eficaz / No eficaz / No concluyente / Sin acciones / No visible:** la eficacia es una revisión humana por acción y es independiente del cierre. **Sin acciones** no significa eficaz.
- Resumen **Eventos de seguridad abiertos**: **Eventos de gravedad alta abiertos**, **Revisión requerida** o **Ningún evento de gravedad alta abierto**. Es solo una advertencia; no da ninguna decisión de preparación.
- **Confidencial (protegido):** la identidad del notificante está en un almacén restringido y no se muestra a los lectores.
- **Registrado sin conexión / Pendiente de sincronizar / Guardado en este dispositivo. Aún no enviado.** En cola no significa enviado.

## Lo que no puede hacer / precaución
- Un notificante no puede clasificar su propio informe. Un autor de la investigación, quien la envió o el notificante no pueden realizar la revisión independiente.
- Solo el responsable de la acción informa de su finalización. Ni el responsable ni quien la completó pueden verificarla; ni el responsable, ni quien la completó, ni el verificador pueden revisar la eficacia. Son registros separados de personas distintas.
- Las causas raíz son declaraciones humanas; el sistema nunca propone ninguna.
- Su autor nunca puede retirar ni eliminar un informe enviado.
- En un informe confidencial, la identidad solo se abre según la necesidad de conocerla, por alguien que tenga el permiso de lectura de identidad y elija una **Finalidad**; cada lectura se registra con su finalidad. La notificación anónima necesita el canal público de recepción, que no está configurado.
- Una revisión No concluyente puede repetirse; Eficaz y No eficaz son definitivas; una acción que no funcionó necesita una nueva acción de seguimiento.

## Si algo sale mal
- **El servidor rechazó el comando:** complete los elementos que faltan o que bloquean, enumerados debajo.
- **El registro cambió mientras tanto. Recargue y revise antes de volver a intentarlo.** Pulse **Recargar**.
- **El resultado es desconocido (conexión perdida).** Use **Reintentar el mismo comando**; no se aplicará dos veces.
- **No tiene permiso para realizar esta acción.** Compruebe su rol, el buque y el contexto del objeto; si es necesario, contacte con el administrador de la organización (13.03 §10).
- **La clave del dispositivo no está disponible** o **Online re-authorisation is needed:** los datos registrados se conservan; vuelva a autorizar cuando esté en línea. Ante un error de la aplicación, contacte con el soporte indicando el ID de correlación, la pantalla, la hora y los pasos (13.03 §10).

## Páginas relacionadas
