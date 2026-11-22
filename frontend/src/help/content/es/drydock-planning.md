---
key: drydock-planning
title: Planificación de dique seco
roles: [Technical Superintendent, Chief Engineer]
permissions: [drydock.read, drydock.write, drydock.verify]
related: [work-order-center, defects, certificate-command-center, survey-planner, pre-survey-checklist]
screenshots: []
reviewed: false
---
## Para qué sirve esta página
La planificación de dique seco contiene los **paquetes de dique seco** del buque seleccionado (proyecto, astillero, fechas previstas y elementos del alcance). Responde a la pregunta: «¿A qué orden de trabajo, defecto, certificado o lista de comprobación de inspección está vinculado cada elemento del alcance del dique seco, y esos vínculos están actualizados?». El alcance está vinculado a estos registros (13.03 §5). **Completitud de vínculos** indica solo la completitud de los vínculos; no es un resultado de inspección de la sociedad de clasificación ni de la bandera, ni una declaración de navegabilidad.

## Quién la usa
- **Superintendente técnico:** para preparar un paquete de dique seco, vincular el alcance a registros, planificarlo y completarlo (13.03 §5).
- **Jefe de máquinas:** para seguir cómo se vincula el alcance a los registros de defectos y órdenes de trabajo y, con permiso, verificar un paquete que haya completado otra persona (13.03 §5).

## Antes de empezar
- Seleccione el buque correcto. Si no hay ningún buque seleccionado, la página indica **Seleccione un buque en el espacio de trabajo para continuar.**
- Necesita acceso de lectura de dique seco para ver los paquetes. Crear, editar y cambiar el estado requieren acceso de escritura; verificar requiere acceso de verificación.
- Tenga a mano los identificadores y las revisiones de los registros de órdenes de trabajo, defectos, certificados y listas de comprobación de inspección que vaya a vincular.

## Paso a paso
1. Abra un paquete de la lista o cree un borrador vacío con **Crear paquete de dique seco**.
2. Rellene **Proyecto de dique seco**, **Astillero**, **Inicio previsto** y **Fin previsto**.
3. Añada elementos con **Añadir elemento del alcance**; introduzca el tipo de **Elemento del alcance**, **Título**, **Paquete de trabajo**, **Requisito de clasificación**, **Requisito reglamentario** y **Prioridad**.
4. En cada elemento, use **Añadir vínculo** e introduzca el **Tipo de registro**, el **Identificador del registro** y la **Revisión fijada**.
5. Para la finalización, añada una referencia de evidencia en **Evidencia de finalización** con **Añadir**. Después pulse **Guardar**.
6. Revise la lista **Bloqueos de planificación** o **Bloqueos para completar**. En **Cambiar estado**, elija una **Acción** (**Planificar**, **Volver a borrador**, **Iniciar dique seco**, **Completar dique seco**, **Cancelar paquete**) y confírmela. Cancelar requiere una nota. Solo se ofrecen las acciones válidas para el estado actual; un paquete solo se puede cancelar mientras está en Borrador o Planificado.
7. En un paquete completado que usted no haya completado, escriba un **Motivo** de al menos 10 caracteres y use **Verificar registro** o **Rechazar verificación**.
8. Siga las fechas previstas y reales y cada revisión en la **Cronología**.

## Qué significan los estados
- **Borrador / Planificado / En curso / Completado / Verificado / Cancelado:** el ciclo de vida del paquete. Completado y Verificado no son lo mismo.
- **Vínculos actualizados / Atención / Revisión requerida / Incompleto:** la completitud de vínculos. Incluso con los vínculos actualizados, esto no es un resultado de inspección.
- **Vinculado / Sin vincular / Vencido / Revisión requerida:** el estado de vínculo de un elemento del alcance (Revisión requerida cuando algún vínculo está Desactualizado o No disponible).
- **Vigente / Desactualizado / No disponible:** un registro vinculado comparado con su revisión fijada. Revise un vínculo desactualizado y use **Volver a fijar en** para fijarlo a la nueva revisión.
- **Desconocido / sin establecer:** no se introdujo ninguna prioridad. Desconocido no significa «Normal» ni «Baja».

## Lo que no puede hacer / precaución
- La página no decide que un dique seco sea seguro ni que el buque esté en condiciones de navegar.
- No puede verificar un paquete que usted haya completado. Rechazar la verificación devuelve el paquete a **En curso**.
- El coste, la moneda, el proveedor y el presupuesto son solo referencias externas; aquí no se suma, aprueba ni encarga nada.
- Un paquete completado, verificado o cancelado no se puede editar.
- Los comandos no se envían sin conexión.

## Si algo sale mal
- **El servidor indicó elementos faltantes o bloqueantes:** resuelva la lista de bloqueos y vuelva a intentarlo.
- **El registro cambió o el comando entra en conflicto:** use **Cargar la versión más reciente** y revise.
- **El resultado es desconocido:** **Reintentar** vuelve a enviar el comando idéntico.
- **No tiene acceso a este registro:** compruebe su rol y el contexto del buque; contacte con el administrador del tenant si es necesario (13.03 §10).
- **No puede leer estos tipos de registros vinculados:** compruebe el permiso con el responsable del registro o con el administrador del tenant.

## Páginas relacionadas
