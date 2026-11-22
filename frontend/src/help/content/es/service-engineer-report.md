---
key: service-engineer-report
title: Informes de servicio e ingeniero
roles: [Chief Engineer, Technical Superintendent, Crew / OOW]
permissions: [service.report.read, equipment.read, service.report.write, service.report.verify, file.read]
related: [work-order-center, defects, pms-command-center, today]
screenshots: []
reviewed: false
---
## Para qué sirve esta página
Esta página registra y enumera los **informes de visitas de servicio y de ingenieros** del buque seleccionado. Responde a: "¿Quién hizo qué trabajo, en qué equipo y para qué orden de trabajo, qué repuestos y mediciones se registraron, y está verificado el informe?" Un informe se vincula a un ID de equipo, a una orden de trabajo y a averías, de modo que queda ligado al historial de la orden de trabajo y del equipo (13.03 §5). Un informe verificado es un registro humano; no cierra una avería ni una orden de trabajo.

## Quién la usa
- **Jefe de máquinas / Superintendente técnico:** para vincular informes al historial de equipos y órdenes de trabajo, seguir lo que falta y verificar informes de los que no son autores (13.03 §5).
- **Ingeniero:** para introducir un informe como borrador después de un servicio y enviarlo para verificación (13.03 §6).

## Antes de empezar
- Si no hay ningún buque seleccionado, la página indica **Elija un buque** y enumera los buques de su ámbito. Seleccione el buque correcto.
- Necesita acceso de lectura a informes de servicio y a equipos para ver los informes. El botón **Nuevo informe** solo se muestra con acceso de escritura y mientras está en línea.
- Tenga a mano los ID del equipo, de la orden de trabajo, de la avería y de los archivos de evidencia. Los campos de orden de trabajo, avería y evidencia solo se muestran si puede leer esos registros.

## Paso a paso
1. Abra un informe de la lista **Informes**, o inicie un nuevo borrador con **Nuevo informe**.
2. Complete **N.º de informe**, **Proveedor de servicio**, **Ingeniero**, **Fecha del servicio**, **Motivo y alcance** y **Trabajo realizado**.
3. Si es necesario, use **Añadir repuesto** y **Añadir medición** para introducir los repuestos utilizados y las mediciones. Si no conoce el resultado de una medición, déjelo como **Desconocido**.
4. En **Vínculos**, introduzca el **ID del equipo**, el **ID de la orden de trabajo** y los ID de las averías y archivos de evidencia vinculados (uno por línea).
5. Guarde con **Guardar borrador**. Si el informe muestra una lista **Falta antes del envío**, complete primero esos elementos.
6. Envíe el informe con **Enviar para verificación**.
7. En un informe enviado del que no sea autor, escriba un **Motivo** de al menos 10 caracteres y use **Registrar verificación** o **Devolver al autor**.
8. Para cambiar un informe verificado, escriba un motivo y use **Corregir (nueva revisión)**. Cada revisión permanece en **Historial inmutable**.

## Qué significan los estados
- **Borrador / Enviado / Devuelto:** el ciclo de vida del informe. Enviar no es verificar.
- **Verificado (decisión humana registrada) / Verificado por un no autor:** una persona autorizada que no fue autora del informe ni lo envió registró una verificación. Completado y Verificado no son lo mismo.
- **Pendiente de verificación:** el informe se envió y aún no está verificado.
- **Se verificó una revisión anterior; esta no:** después de una corrección, la nueva revisión necesita verificarse de nuevo.
- **revisión actual / cambiado desde el vínculo / no disponible:** el estado de un equipo, orden de trabajo o avería vinculados comparado con la revisión que se vinculó.
- **Transferencia aceptada, no evidencia verificada:** un archivo adjunto es solo una transferencia aceptada.
- Un resultado de medición **Desconocido** no significa "Within limits" (dentro de los límites).

## Lo que no puede hacer / precaución
- No puede verificar un informe del que sea autor o que haya enviado; el servidor lo rechaza.
- Un informe verificado no cierra una avería ni una orden de trabajo, y no demuestra que el equipo sea seguro.
- Los originales de evidencia no se modifican; una corrección abre una nueva revisión y la revisión anterior permanece en el historial (13.03 §1).
- Sin conexión no se registra nada. Cuando vuelve la conexión, **Reintentar el mismo comando** envía de nuevo el mismo comando.

## Si algo sale mal
- **Faltan datos o registros obligatorios:** complete la lista de faltantes y vuelva a guardar.
- **Otra persona modificó este registro:** use **Recargar** para abrir la última versión y luego vuelva a guardar.
- **No tiene el permiso del servidor para esta acción o registro:** compruebe su rol y el contexto del buque; si es necesario, contacte con el administrador de su organización (13.03 §10).
- **Un informe no se puede verificar:** compruebe los registros vinculados y los archivos de evidencia; hable con un verificador autorizado (13.03 §10).
- **No se pudo cargar el registro:** envíe al soporte la pantalla, la hora y los pasos (13.03 §10).

## Páginas relacionadas
