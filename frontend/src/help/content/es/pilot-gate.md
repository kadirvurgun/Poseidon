---
key: pilot-gate
title: Revisión go/no-go del piloto
roles: [Product Owner, Tenant Admin]
permissions: [pilot.gate.read, pilot.gate.support.record, pilot.gate.decide]
related: [role-uat, pilot-program, audit-ready]
screenshots: []
reviewed: false
---
## Para qué sirve esta página
La página de revisión go/no-go del piloto muestra en un solo lugar el estado de las puertas necesarias para un piloto, el número de P0 abiertos y la preparación del soporte, y registra la decisión go/no-go del Product Owner designado. Responde a: "¿Se ha cumplido cada puerta del piloto con evidencia y se ha registrado una decisión humana?" Se aplica el aviso de la parte superior: "Advisory review only. Unknown or deferred gates and an unknown or open P0 recommend NO-GO. Only the appointed Product Owner records a decision." (solo revisión consultiva; las puertas desconocidas o aplazadas y un P0 desconocido o abierto recomiendan NO-GO; solo el Product Owner designado registra una decisión).

## Quién la usa
- **Product Owner:** lee el resultado de la revisión y registra la decisión go/no-go con una justificación. Decidir requiere una designación vigente de Product Owner.
- **Soporte / Tenant Admin:** marca los elementos de preparación de soporte y guardia con una referencia de evidencia.
- 13.03 no define un rol separado para esta página; se aplican los principios de administración de la organización (mínimo privilegio, publicación controlada) (13.03 §9).

## Antes de empezar
- Ver la página requiere lectura en toda la organización y lectura de la puerta del piloto. Marcar un elemento de soporte requiere el permiso de registro de soporte; decidir requiere el permiso de decisión más una designación vigente de Product Owner aprovisionada en el servidor. El permiso por sí solo no basta.
- Solo una cuenta humana puede marcar soporte o decidir; la designación debe coincidir con su rol activo y estar dentro de su periodo de validez.
- Las entradas de las puertas proceden de otras páginas; por ejemplo, las puertas Ship UAT y Tug UAT se calculan a partir de las ejecuciones atestiguadas en la página **Registros UAT por rol**.
- Registrar requiere una conexión activa; está desactivado sin conexión.

## Paso a paso
1. En **Revisión de la puerta del piloto**, lea la recomendación (**Recomendación consultiva**), el número de **P0 abiertos** y la tabla de puertas: **Puerta**, **Estado**, **Fuente**, **Motivos**.
2. En **Preparación de soporte y guardia**, elija un **Estado** para cada elemento, introduzca una **Referencia de evidencia** para **Listo**, escriba una **Nota** y pulse **Marcar**.
3. Si es el Product Owner, elija la decisión en **Registrar la decisión go/no-go** (**NO-GO**, **GO condicional**, **GO**), elija su designación de Product Owner en **Designación** y escriba la **Justificación**. Para **GO condicional**, complete **Condiciones (una por línea)**.
4. Pulse **Registrar**. La decisión aparece en **Decisiones del Product Owner** con su número de secuencia.
5. Pulse **Actualizar** para recargar los datos.

## Qué significan los estados
- **Evidence threshold met (PASS):** la fuente de la puerta cumple el umbral de evidencia. No es una aprobación; decide una persona.
- **Fail (FAIL)**, **Unknown (UNKNOWN)**, **Deferred (DEFERRED):** la puerta no se ha cumplido. Una puerta desconocida o aplazada nunca cuenta como superada y recomienda NO-GO.
- **Se recomienda NO-GO:** al menos una puerta no está en PASS, o el número de P0 abiertos es desconocido o mayor que cero.
- **Apto para GO; decisión humana pendiente:** todas las puertas están en PASS y el número de P0 abiertos es cero. El sistema nunca decide GO; solo el Product Owner lo registra.
- **Sin marcar / Listo / No listo:** el último estado marcado de un elemento de soporte.
- **Vigente / Obsoleta: la revisión cambió:** una decisión está vinculada a la revisión del momento en que se tomó. Si la revisión cambió después, la decisión aparece como obsoleta y debe reconsiderarse.

## Lo que no puede hacer / precaución
- "GO is possible only when every gate is PASS and open P0 is zero." (GO solo es posible cuando todas las puertas están en PASS y el número de P0 abiertos es cero). En caso contrario, la opción GO está desactivada.
- GO condicional requiere el mismo estado que GO (todas las puertas en PASS, P0 abiertos con valor conocido de cero) y al menos una condición. No se puede registrar mientras alguna puerta esté fallida, desconocida o aplazada, ni mientras haya P0 abiertos o desconocidos. NO-GO siempre se puede registrar, sin condiciones.
- El sistema, la IA o la automatización nunca toman la decisión del piloto y nunca inventan puertas ni firmas. Las puertas y decisiones del piloto son registros humanos.
- No puede cambiar una decisión registrada; se añade una nueva decisión con un nuevo número de secuencia.

## Si algo sale mal
- **No tiene una designación vigente de Product Owner. Las decisiones son de solo lectura.** Las designaciones se aprovisionan en el servidor; contacte con el Tenant Admin (13.03 §10).
- **No tiene acceso a estos registros.** Compruebe su rol y el ámbito de la organización.
- **El registro cambió o ya se usó. Recargue.** La revisión cambió mientras usted decidía; abra la revisión actual con **Recargar**. Este mensaje también aparece cuando los estados de las puertas cambiaron y la decisión ya no está permitida.
- **El resultado es incierto. Reintentar envía el mismo comando.** Use **Reintentar** para reenviar el mismo comando de forma segura.
- **Rechazado por las reglas.** Una puerta está aplazada.
- **No permitido.** Su designación no está vigente o su cuenta no es una cuenta humana.
- **El servicio no está disponible. Inténtelo de nuevo.** Si persiste, comunique al soporte el ID de correlación, la hora y los pasos (13.03 §10).

## Páginas relacionadas
