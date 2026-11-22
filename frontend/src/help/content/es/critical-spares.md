---
key: critical-spares
title: Repuestos críticos y requisiciones
roles: [Chief Engineer, Technical Superintendent]
permissions: [spare.read, spare.write, requisition.propose, requisition.approve]
related: [pms-command-center, work-order-center, defects]
screenshots: []
reviewed: false
---
## Para qué sirve esta página
La página de repuestos críticos y requisiciones muestra las **existencias declaradas, el mínimo y el máximo, las alertas de existencias bajas y las propuestas de requisición técnica** del buque seleccionado. Responde a la pregunta: «¿Qué repuesto crítico alcanzó su mínimo, cuántas órdenes de trabajo abiertas hacen referencia a él y se ha propuesto y decidido una requisición técnica?». Los recuentos son declaraciones humanas y no se verifican contra un almacén.

## Quién la usa
- **Jefe de máquinas / Superintendente técnico:** para seguir los umbrales mínimos de los repuestos críticos e iniciar una requisición técnica (13.03 §5). La persona que propone una requisición y la persona que la aprueba son personas distintas.

## Antes de empezar
- Para ver la página se necesita `spare.read`. Definir repuestos y registrar recuentos requiere `spare.write`, proponer una requisición `requisition.propose` y decidirla `requisition.approve`.
- Las propuestas y las decisiones requieren un usuario humano con sesión iniciada.
- Elija el buque correcto en la lista **Buque**.

## Paso a paso
1. Elija el buque. Se abre la vista **Repuestos críticos**. Lea los recuentos **Existencias bajas** y **Revisión requerida** en la parte superior.
2. En cada fila de la tabla, lea **Criticidad del repuesto**, **Criticidad del equipo**, **Estado de existencias**, **Proyectado**, **Mín. / máx.** y **Órdenes de trabajo abiertas**.
3. Pulse **Nuevo repuesto crítico** para un repuesto nuevo o **Editar** para uno existente. Introduzca **Número de pieza**, **Nombre**, **Unidad**, **Existencias mínimas** y **Existencias máximas**. Deje los umbrales vacíos cuando no estén decididos.
4. Para introducir un recuento, marque **Registrar un nuevo recuento de existencias**, rellene **Cantidad contada** y pulse **Guardar**.
5. En una fila con **Existencias bajas** sin requisición abierta, pulse **Proponer requisición**. El servidor calcula la cantidad propuesta (máximo menos existencias proyectadas). Si no hay un máximo definido, no se propone ninguna cantidad y el aprobador debe introducir la **Cantidad aprobada**.
6. En la vista **Requisiciones técnicas**, una persona autorizada que no haya hecho la propuesta comprueba la **Cantidad aprobada** y pulsa **Aprobar para exportación al ERP**, o elige un **Motivo del rechazo** y pulsa **Rechazar**.
7. Abra **Referencia de exportación al ERP** en una requisición aprobada para ver la referencia.

## Qué significan los estados
- **Existencias bajas:** las existencias proyectadas (último recuento declarado menos los repuestos registrados en órdenes de trabajo enviadas desde ese recuento) están en el mínimo o por debajo. En esta fila se ofrece la propuesta de requisición.
- **Por encima del mínimo (declarado):** las existencias proyectadas están por encima del mínimo; el recuento no está verificado.
- **Mínimo no configurado:** no se introdujo ningún umbral. Un umbral vacío se notifica como revisión requerida, nunca como correcto.
- **Existencias desconocidas / Consumo sin resolver:** no se pudieron calcular las existencias. Desconocido no significa «existencias suficientes».
- **Propuesta / Aprobada para exportación al ERP / Rechazada:** el estado de la requisición técnica.
- **Crítico (declarado) / No crítico (declarado) / Desconocido:** la criticidad del repuesto es una declaración.

## Lo que no puede hacer / precaución
- No puede decidir una requisición que usted mismo propuso. La página indica «Usted propuso esto; otra persona debe decidir.».
- La propuesta y la aprobación son actos humanos separados. La aprobación solo crea una referencia de exportación al ERP; aquí no se gestionan órdenes de compra, proveedores ni precios.
- La página no tiene un campo separado de umbral de reaprovisionamiento; las existencias bajas se evalúan respecto a las existencias mínimas.
- Sin conexión, los datos no se pueden actualizar y no se puede realizar ninguna acción.

## Si algo sale mal
- **El registro ha cambiado:** use **Reintentar** para actualizar la página y vuelva a intentarlo.
- **Resultado desconocido:** **Reintentar el mismo comando** envía el mismo comando.
- **La solicitud fue rechazada por una regla:** compruebe el estado de existencias y la información de la requisición abierta.
- **No tiene permiso:** compruebe su rol y el contexto del buque; contacte con el administrador del tenant si es necesario (13.03 §10).

## Páginas relacionadas
