---
key: high-risk-work
title: Trabajos de alto riesgo
roles: [DPA / HSEQ, Master, Tug Master]
permissions: [hse.risk.read, hse.high_risk_work.read]
related: [risk-assessment, risk-template-library, safety-events]
screenshots: []
reviewed: false
---
## Para qué sirve esta página
La página Trabajos de alto riesgo muestra en una lista de **solo lectura** las excepciones de los trabajos de la flota como trabajos en caliente, espacios cerrados, trabajos en altura, aislamiento eléctrico, mantenimiento crítico, remolque y suministro de combustible. Responde a la pregunta: «¿Qué trabajo de alto riesgo tiene un problema abierto en riesgo, permiso, barrera crítica, tripulación o equipo, y quién tiene la siguiente acción?». Cada dimensión se muestra por separado; no hay una puntuación agregada. Las acciones abren el registro responsable; esta página no modifica ningún registro ni toma ninguna decisión de inicio de trabajo (13.03 §4).

## Quién la usa
- **DPA / HSEQ:** como parte de la gobernanza del riesgo, sigue las excepciones de trabajos de alto riesgo en toda la flota (13.03 §4).
- **Capitán y Patrón del remolcador:** ven los bloqueos de los trabajos de alto riesgo en su propio buque y van al registro responsable. La decisión de iniciar o detener el trabajo sigue siendo suya (13.03 §2, §3).

## Antes de empezar
- Para ver la página se necesitan los permisos de lectura de riesgos y de lectura de trabajos de alto riesgo. Sin ellos verá «No tiene acceso a la vista de trabajos de alto riesgo.».
- La lista solo abarca las flotas y los buques de su ámbito.
- Sin conexión, la lista mostrada puede estar desactualizada.

## Paso a paso
1. Elija los filtros **Flota**, **Buque** y **Tipo de trabajo**. **Todos** quita un filtro.
2. En la tabla, lea por separado los estados de **Riesgo**, **Permiso**, **Barrera crítica**, **Tripulación** y **Equipo** de cada trabajo. Las insignias **Bloqueos críticos** y **Elementos por revisar** muestran recuentos.
3. Use las columnas **Siguiente acción** y **Responsable / vencimiento** para ver quién debe hacer qué.
4. Para ver por qué una fila está en su estado, pulse **Por qué**. Se abre una tabla con **Dimensión**, **Estado** y **Motivo**; ciérrela con **Ocultar**.
5. Para cambiar algo, siga el enlace del trabajo o **Abrir registro**; la acción se realiza en la propia página del registro responsable.
6. Pase de página con **Anterior** y **Siguiente**. **Evaluado el** y **Cobertura de fuentes** muestran cuándo y a partir de qué fuentes se calculó la lista.

## Qué significan los estados
- **Listo / Condicional / Atención / No listo / Bloqueado:** el estado de la dimensión en su fuente. Una dimensión Listo no significa que el trabajo pueda empezar; la decisión sigue correspondiendo a la persona autorizada.
- **Revisión requerida:** una persona debe revisarla.
- **Desconocido:** la fuente no está disponible; nunca está lista. POSEIDON todavía no tiene ninguna fuente de permisos de trabajo, por lo que la columna **Permiso** siempre muestra **Desconocido**.
- **Correcto / Degradado / Fallido:** el estado de la barrera crítica.
- **Sin clasificar:** el tipo de trabajo no se conoce o su criticidad no está determinada; no se excluye de la lista.
- **Esperando la fuente / Resolver parada o impugnación / Renovar elemento vencido / Completar elemento requerido / Reevaluar / Abrir registro del responsable:** la siguiente acción, mostrada en un orden de prioridad fijo.
- **Vencido sin resolver / No planificado:** la fecha de vencimiento o el inicio previsto no se conocen.

## Lo que no puede hacer / precaución
- Esta página es de solo lectura; no modifica, aprueba ni permite nada.
- No hay una puntuación agregada; un trabajo con un bloqueo crítico siempre se ordena arriba y el buen estado de otras dimensiones nunca lo oculta.
- No interprete **Desconocido** como listo. Un permiso activo histórico nunca es un permiso válido vigente.
- La decisión de iniciar o detener el trabajo sigue correspondiendo al capitán o al patrón del remolcador.

## Si algo sale mal
- **El servicio no está disponible. Inténtelo de nuevo.** Pulse **Reintentar**.
- **Está sin conexión. La lista mostrada puede estar desactualizada.** Cuando esté en línea, pulse **Recargar**.
- Si un estado parece inesperado, abra la tabla **Por qué** y consulte al responsable del registro o al responsable del control (13.03 §10).
- Sin acceso, compruebe su rol y el contexto del buque; si es necesario, contacte con el administrador del tenant (13.03 §10).

## Páginas relacionadas
