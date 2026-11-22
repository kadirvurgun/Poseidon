---
key: regulatory-review
title: Revisión regulatoria por experto
roles: [Regulatory / Assurance, DPA / HSEQ]
permissions: [regulatory.review.read, regulatory.review.manage, regulatory.review.verdict]
related: [sources, requirements, audit-ready, audit]
screenshots: []
reviewed: false
---
## Para qué sirve esta página
La revisión regulatoria por experto registra campañas en las que un experto humano revisa los elementos de fuente y de aplicabilidad de un buque o remolcador. Responde a: "¿Qué elementos de fuente y de requisito de este buque ha confirmado un revisor autorizado, y cuáles siguen abiertos y en seguimiento?" Es solo un registro de revisión humana: el sistema no aprueba ningún requisito, no activa ninguna regla ni permiso y no afirma ninguna aprobación de clase ni de tipo (13.03 §8).

## Quién la usa
- **Usuario de Regulatory / Assurance:** abre campañas de revisión, registra veredictos sobre los elementos y cierra la campaña (13.03 §8).
- **Revisor designado (SME):** confirma o sigue elementos dentro de su propia autoridad de fuente.
- **DPA / HSEQ:** vigila los elementos abiertos y sin seguimiento (13.03 §4).

## Antes de empezar
- Ver la página requiere lectura de la revisión regulatoria. Abrir y cerrar campañas requiere gestión; registrar veredictos requiere el permiso de veredicto y una designación de revisor vigente para este ámbito.
- Elija **Buque** o **Remolcador** en las pestañas **Alcance de la revisión** y luego abra el buque correcto en **Elija un buque** (13.03 §1).
- Las fuentes deben estar registradas como oficiales y versionadas en la página [Borradores de fuentes](/app/sources) (13.03 §8).
- Sin conexión, los datos pueden estar desactualizados y los comandos están desactivados.

## Paso a paso
1. Elija el alcance y el buque. Si no hay ninguna campaña, pulse **Abrir campaña de revisión**.
2. En **Matriz de propiedad de fuentes**, revise para cada autoridad de fuente la categoría, los recuentos de abiertos y confirmados y los roles responsables.
3. Seleccione un elemento en **Elementos de revisión**.
4. En el formulario **Registrar veredicto**, complete **Designación de revisor**, **Veredicto**, **Autoridad de la fuente** y **Rol responsable**. Si el elemento no tiene ninguna fuente registrada, introduzca **Referencia de fuente autorizada** y **Versión / edición de la fuente**. Para un elemento que sigue abierto, introduzca un **ID de la incidencia**.
5. Escriba un motivo de 10–2000 caracteres en **Nota del revisor** y pulse **Registrar**.
6. Cuando todos los elementos estén resueltos, elija un **Resultado del cierre** en **Cerrar campaña**, escriba el **Motivo del cierre** y pulse **Cerrar campaña**.

## Qué significan los estados
- Campaña: **Abierta**, **En revisión**, **Cerrada con elementos abiertos**, **Cerrada**.
- Veredicto: **Confirmado**, **No aplicable (confirmado)**, **Revisión requerida**, **Ambiguo**. Cuando un dato crítico es desconocido, el elemento sigue en **Revisión requerida**; un elemento desconocido nunca se cuenta como "No aplicable".
- **Candidato (no verificado):** la fuente aún no está vinculada a una fuente oficial verificada.
- **Desactualizado — referencia cambiada** y **Perfil del buque modificado:** la fuente o el perfil del buque cambiaron después del veredicto; el elemento debe revisarse de nuevo.
- **Elementos abiertos sin seguimiento:** elementos abiertos sin una incidencia en seguimiento. Mientras su número sea mayor que cero, la campaña no se puede cerrar.
- Las categorías **Ley reglamentaria**, **Regla de clase**, **SMS de la empresa** y **Guía — no es ley** nunca se sustituyen entre sí (13.03 §8).

## Lo que no puede hacer / precaución
- La persona que abrió una campaña no puede registrar veredictos en ella; sin una designación de revisor, los veredictos son de solo lectura.
- Un revisor cuya cualificación no está verificada puede seguir elementos abiertos, pero no puede confirmar. Un elemento desactualizado no se puede confirmar.
- Confirmar requiere una fuente registrada o una referencia de fuente autorizada con su edición. No deje ningún registro huérfano ni sin fuente en la cadena Requisito → Control → Evidencia → Verificación (13.03 §8).
- **Cerrada** requiere que todos los elementos estén confirmados; en caso contrario, cierre con elementos abiertos, cada uno en seguimiento.
- No indique en la nota ninguna aprobación de clase ni de tipo. En el ámbito del remolcador, ningún límite de remolque sin una fuente controlada constituye un valor legal por defecto.
- Los veredictos solo los registran usuarios humanos; las cuentas de IA y de automatización no pueden registrarlos.

## Si algo sale mal
- **El registro cambió. Recargue y revise antes de continuar.** Pulse **Recargar**.
- **El resultado es incierto. Reintentar envía el mismo comando.** Use **Reintentar**.
- **Las reglas de revisión rechazaron este comando.** Compruebe si el elemento está desactualizado, la referencia de la fuente y la incidencia en seguimiento.
- **No tiene acceso a la revisión regulatoria de este buque.** o **No está autorizado para esta acción.** Compruebe su rol y su ámbito; si es necesario, contacte con el Tenant Admin (13.03 §10).
- Si la aplicabilidad parece inesperada, compruebe la fuente y los datos de contexto y hable con el responsable del control o con el experto (13.03 §10).

## Páginas relacionadas
