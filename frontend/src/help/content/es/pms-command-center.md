---
key: pms-command-center
title: Centro de mando PMS
roles: [Chief Engineer, Technical Superintendent, Master]
permissions: [equipment.read]
related: [work-order-center, defects, critical-spares, departure-signoff, today]
screenshots: []
reviewed: false
---
## Para qué sirve esta página
El centro de mando PMS reúne en una sola pantalla las **excepciones** de mantenimiento del buque seleccionado. Responde a: "¿Tiene este buque trabajos críticos vencidos, órdenes de trabajo abiertas, alertas de averías o faltas de repuestos críticos, y por qué?" La página es una vista consultiva de excepciones; no es una decisión de aptitud, preparación ni seguridad. Los elementos en buen estado no se enumeran, solo se cuentan.

## Quién la usa
- **Jefe de máquinas / Superintendente técnico:** para seguir los trabajos vencidos, las órdenes de trabajo abiertas, las alertas de averías y las faltas de repuestos críticos (13.03 §5).
- **Capitán:** para ver el impacto en restricciones operativas de los registros PMS y de averías (13.03 §2).

## Antes de empezar
- Necesita el permiso de lectura de equipos (`equipment.read`) para ver la página.
- Cada sección depende del permiso de lectura de su propio módulo. Por ejemplo, sin permiso de lectura de averías, la sección **Alertas de condición** permanece como "Se requiere acceso: no se muestra".
- Elija primero un buque. Sin buque, la página solo muestra la lista **Elija un buque**.

## Paso a paso
1. Elija el buque en la lista. Si ninguno está en su ámbito, la página indica "No vessel is in your scope."
2. Lea la insignia **Resumen de excepciones** y la hora de **Última actualización** en la parte superior.
3. Revise las secciones: **Trabajos críticos vencidos**, **Trabajos pendientes, vencidos y por revisar**, **Órdenes de trabajo abiertas**, **Alertas de condición** y **Faltas de repuestos críticos**.
4. Abra **Por qué** en un elemento para ver los códigos de motivo registrados y, si existe, la hora **Observado**.
5. Use **Abrir registro de origen** para ir a la fuente. Un elemento de avería abre [Averías y medidas temporales](/app/defects); un elemento de orden de trabajo abre [Centro de órdenes de trabajo](/app/work-order-center).
6. Pulse **Actualizar** para volver a leer los datos.

## Qué significan los estados
- **Bloqueado / Revisión requerida / Atención:** el estado del resumen de excepciones. Los detalles están en los elementos de cada sección.
- **Sin excepciones abiertas (consultivo):** las fuentes leídas no devolvieron ninguna excepción abierta. Esto no es un resultado de preparación ni de aptitud.
- **Crítico / Atención / Revisión requerida:** la gravedad de un elemento.
- **Fuente no disponible: desconocido, no cero:** no se pudo leer la fuente de la sección. Desconocido no significa "cero", "sin problemas" ni "Listo".
- **Se requiere acceso: no se muestra:** no tiene permiso para esta sección; no se muestra ni el contenido ni el recuento.
- **Sin excepciones de esta fuente:** la sección se leyó y no devolvió ninguna excepción. El número de elementos en buen estado se muestra por separado.
- **Stale data / Actualidad desconocida:** la lectura en la que se basa el elemento no es actual, o se desconoce su actualidad.

## Lo que no puede hacer / precaución
- La página no modifica ningún registro; solo lee. Las órdenes de trabajo, las averías y los repuestos se gestionan en sus propias páginas.
- La página nunca declara un buque "listo" ni "seguro". La decisión de salida corresponde al capitán (13.03 §2).
- Una avería solo cuenta como cerrada mediante un registro de cierre humano, y solo para la revisión de la declaración que se cerró. Si la declaración cambia después, el elemento vuelve a revisión.
- Los datos mostrados sin conexión pueden ser antiguos, y **Actualizar** no está disponible.

## Si algo sale mal
- **No se pudo cargar el registro:** compruebe su conexión y use **Recargar**.
- **No tiene el permiso del servidor para esta acción o registro:** compruebe su rol y el contexto del buque; si es necesario, contacte con el administrador de su organización (13.03 §10).
- **Excepción inesperada:** abra **Por qué** y revise el registro de origen con el responsable o el experto en la materia (13.03 §10).
- **Error de la aplicación:** comunique al soporte el ID de correlación, la pantalla, la hora y los pasos (13.03 §10).

## Páginas relacionadas
