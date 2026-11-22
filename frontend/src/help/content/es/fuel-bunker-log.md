---
key: fuel-bunker-log
title: Combustible, suministro y ROB
roles: [Chief Engineer, Master]
permissions: [operations.fuel.read, operations.fuel.write]
related: [noon-report, engine-daily-log, port-activity-sof, operational-downtime]
screenshots: []
reviewed: false
---
## Para qué sirve esta página
La página de combustible, suministro y ROB conserva las **declaraciones de combustible y de suministro de combustible** del buque seleccionado como borradores sin verificar con su historial de fuente. Responde a la pregunta: «¿Qué combustible se recibió o consumió, qué ROB declaramos antes y después, qué referencias de BDN, proveedor, muestra y tanque le corresponden y la cadena de ROB declarada continúa de un registro al siguiente?». La página no corrige cantidades, no calcula emisiones y no establece cumplimiento ni verificación.

## Quién la usa
- **Jefe de máquinas / oficiales de máquinas:** para registrar declaraciones de suministro, consumo y ROB con sus referencias de evidencia (13.03 §5).
- **Capitán:** para seguir las declaraciones de combustible junto a los resúmenes de mediodía revisados por el capitán (13.03 §2).

## Antes de empezar
- En la barra de contexto superior, seleccione la **empresa y el buque correctos**. Si no hay ningún buque seleccionado, la página muestra «Seleccione un buque».
- La lectura requiere `operations.fuel.read`. Crear y guardar requiere `operations.fuel.write` y derechos de edición sobre este buque.
- Las horas deben incluir fecha, segundos y Z o un desfase UTC explícito; no se acepta una hora local sin zona horaria.

## Paso a paso
1. Elija el buque. **Registros de combustible y ROB** lista los registros devueltos. Use **Filtrar página devuelta** para acotar solo esta página.
2. Lea **Revisión de continuidad ROB** debajo de la lista. Muestra el balance (antes + recibido − consumido − después), **Continuidad con el registro anterior** y **Marcas de revisión** por tipo de combustible declarado.
3. Lea **Resúmenes de combustible y ROB de Noon revisados por el capitán** como contexto. Se muestran como texto declarado y no se copian en el registro contable.
4. Pulse **Nuevo registro en borrador**. Rellene **Contexto de registro y entrega**, **Declaraciones de cantidades y ROB** y **Propiedades del combustible y registro de muestras**. Para cada campo mantenga un valor declarado, **Ausente** o **Desconocido**.
5. Pulse **Guardar borrador sin verificar**. Cada guardado añade una revisión a **Historial de revisiones registradas**.
6. Para partir de un informe de mediodía o un diario de máquinas guardado, use **Abrir registro de combustible desde fuente guardada** en esa página. La **Declaración diaria vinculada** permanece aquí en solo lectura.

## Qué significan los estados
- **Declaración en borrador / Registro en borrador sin verificar:** una persona declaró los valores; nada está verificado.
- **Revisión requerida:** el estado de revisión de cumplimiento de cada registro; el cumplimiento no se ha evaluado.
- **Pendiente; verificación no establecida:** el estado de la revisión de verificación; no existe ninguna verificación.
- **Sin discrepancia detectada:** las cifras declaradas cuadran. Esto no es una verificación.
- **Desconocido:** no se puede valorar la continuidad, por ejemplo cuando falta un valor o el historial más antiguo queda fuera del periodo evaluado. Desconocido no significa «Sin discrepancia» ni «Listo».
- **Ausente / Desconocido:** distintos de cero.

## Lo que no puede hacer / precaución
- La revisión humana, la aprobación operativa o legal, la finalización y la captura sin conexión no están disponibles en esta página.
- El sistema señala las diferencias para su revisión humana; nunca corrige una cantidad ni decide sobre el cumplimiento en materia de combustible.
- Solo se evalúan los 100 registros más recientes. Sin referencia de tanque, una discrepancia en un buque con varios tanques es una cuestión de datos de tanque que faltan, no un error de datos.
- Las cantidades del informe de mediodía o del diario de máquinas con una unidad sin resolver nunca se convierten en valores recibidos, consumidos ni de ROB.

## Si algo sale mal
- **«La fuente guardada ha cambiado.»:** sus entradas se conservan; recargue o descarte explícitamente antes de volver a guardar.
- **«Resultado desconocido.»:** use **Reintentar comando operativo exacto**; no vuelva a escribir los datos.
- **«Compruebe los valores declarados, los desfases horarios explícitos y el permiso vigente sobre la fuente.»:** corrija el campo marcado o déjelo como Ausente o Desconocido.
- **Fuente operativa o acceso no disponible:** use **Reintentar lectura de fuente**; si el acceso sigue denegado, compruebe su rol y el contexto del buque con el administrador del tenant (13.03 §10).

## Páginas relacionadas
