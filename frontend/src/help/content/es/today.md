---
key: today
title: Hoy
roles: [Master, Tug Master, Chief Engineer, Crew / OOW]
permissions: [canRead]
related: [fleet, tug-job-board, offline-conflict-center, sources]
screenshots: [1, 2, 3, 4, 5, 6]
reviewed: false
---
## Para qué sirve esta página
"Hoy" reúne en una sola lista, para el buque o remolcador que seleccione, las **alertas que requieren su atención ahora**. Responde a la pregunta: "¿Qué registro de este buque espera hoy una decisión o una comprobación, y qué fuente lo indica?" La página no decide nada; muestra la alerta, su fuente y el siguiente paso.

## Quién la usa
- **Capitán** y **Patrón del remolcador:** para ver las excepciones del día, su responsable y el siguiente paso (13.03 §2, §3).
- **Jefe de máquinas:** para seguir las alertas del plan de mantenimiento (13.03 §5).
- **Crew / OOW:** para ver el trabajo que les corresponde (13.03 §6).

## Antes de empezar
- En la barra de contexto superior, confirme que están seleccionados la **empresa y el buque correctos** (13.03 §1). Una alerta que vea pertenece al buque seleccionado.
- Necesita acceso de lectura al espacio de trabajo para abrir esta página.
- Si no hay ningún buque seleccionado, la página muestra primero la lista "Seleccione un buque".

## Paso a paso
1. Seleccione el buque o remolcador con el que está trabajando. El buque seleccionado se muestra como "Buque actual"; use "Cambiar de buque" para cambiarlo.
2. Observe la sección **Cobertura de fuentes** en la parte superior. Muestra qué fuentes (normativa, mantenimiento planificado, operaciones de remolcador) se pudieron leer.
3. Lea las tarjetas de alerta de arriba abajo. Cada tarjeta procede de una fuente y sugiere un siguiente paso (por ejemplo "Revisar borrador", "Revisar plan de mantenimiento", "Ver trabajo de remolcador").
4. Pulse **¿Por qué?** para ver el motivo de una tarjeta. La sección muestra la regla, el registro de origen, su revisión, la hora de lectura y cualquier dato de entrada desconocido.
5. Siga el enlace de la tarjeta hasta la página propia del registro y actúe allí, dentro de sus permisos.
6. Use **Actualizar** para volver a leer los datos.

## Qué significan los estados
- **Disponible (sin verificar):** la fuente se leyó. Esto no significa que su contenido se haya verificado.
- **Ausente / No disponible en este contexto:** la fuente no se pudo leer para este buque. Que no vea una alerta no significa que todo esté bien.
- **Vista parcial:** algunas fuentes no se pudieron leer. Un recuento de cero no significa "ningún problema".
- **Todas las fuentes indicadas se leyeron y no devolvieron elementos:** no se devolvió ninguna alerta. Esto no es una autorización de seguridad.
- **Desconocido:** el valor falta en la fuente o no se pudo determinar. Desconocido no significa "No", "Listo" ni "No aplicable".
- **Revisión requerida / Atención de planificación / Aviso orientativo:** la prioridad de la tarjeta. Una tarjeta con revisión requerida espera a que una persona autorizada la examine.
- **Completado ≠ Verificado:** completar un registro no significa que un verificador autorizado lo haya confirmado (13.03 §1).

## Lo que no puede hacer / precaución
- Esta página no toma ninguna decisión de salida, de operación ni de seguridad. Esa decisión corresponde al Capitán o al Patrón del remolcador autorizado.
- Desde esta página no se ejecuta ningún comando; las acciones se realizan en la página propia del registro.
- Los datos mostrados sin conexión son antiguos y no están verificados. Los detalles de "¿Por qué?" no se abren sin conexión.
- Una hora de lectura o de guardado no demuestra que la fuente sea correcta.

## Si algo sale mal
- **Sin conexión:** continúe con su trabajo crítico y vigile el estado de sincronización. Si aparece un conflicto, vaya al Centro de conflictos (13.03 §10).
- **Acceso denegado / Buque no disponible en su ámbito:** compruebe su rol y el contexto del buque; si es necesario, contacte con el administrador de su inquilino.
- **Alerta inesperada:** abra "¿Por qué?"; si la regla o la fuente parecen incorrectas, trasládelo al responsable del control o a un experto en la materia.
- **Fuente no disponible / Demasiadas solicitudes:** espere un momento y use "Reintentar".
- **La respuesta del servidor no superó la validación:** no se muestra nada de ella; notifíquelo a soporte con una captura de pantalla, la hora y sus pasos.

## Páginas relacionadas
