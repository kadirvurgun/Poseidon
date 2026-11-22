---
key: audit
title: Historial de acciones
roles: [DPA / HSEQ, Regulatory / Assurance, Tenant Admin]
permissions: [audit.read]
related: [audit-ready, user-admin, regulatory-review]
screenshots: [1, 2, 3]
reviewed: false
---
## Para qué sirve esta página
La página Historial de acciones muestra un registro atribuido y de **solo lectura** de los cambios en los registros de negocio dentro de su ámbito autorizado. Responde a la pregunta: «¿Cuándo cambió este registro, con qué acción y bajo qué actor y contexto de rol?». Los eventos de seguridad se restringen por separado y no aparecen aquí (13.03 §4, §9).

## Quién la usa
- **DPA / HSEQ:** traza registros de hallazgos, acciones y verificaciones (13.03 §4).
- **Usuario de Normativa / Aseguramiento:** revisa el historial de un registro antes de una auditoría o revisión (13.03 §8).
- **Tenant Admin:** audita cambios de pertenencia y de permisos y las horas de inicio y fin de las delegaciones (13.03 §9).

## Antes de empezar
- Para ver la página se necesita lectura de auditoría. Sin ella verá «Se requiere permiso de auditoría.».
- Solo se listan los registros del tenant y del ámbito de buques para los que está autorizado.
- Si busca un registro concreto, tenga a mano su **ID del buque** o su **ID del objeto**. Los enlaces de historial de otras páginas abren esta página ya filtrada.

## Paso a paso
1. Rellene los filtros: **ID del buque**, **ID del objeto**, **Tipo de objeto** y **Acción** (**Todos**, **Creado**, **Actualizado**, **Eliminado**).
2. Pulse **Filtrar**. Use **Borrar filtros** para restablecerlos.
3. Revise las columnas **Hora**, **Objeto** y **Acción** en **Registros autorizados**. Cambie de página con **Primera página** y **Siguiente**.
4. Pulse el enlace del objeto en una fila para abrir **Detalles del registro**. Lea **Referencia del actor**, **Referencias de roles asignados**, **Capacidades de confianza**, **Motivo**, **Metadatos**, **Versión del recurso** y **Correlación**.
5. Si existe evidencia de revisión inmutable, pulse **Abrir revisión inmutable exacta** para ver el registro tal como era. Pulse **Cerrar** para cerrar los detalles.

## Qué significan los estados
- **Creado / Actualizado / Eliminado:** el tipo de acción realizada sobre el registro.
- **Registrado:** los metadatos se capturaron por completo.
- **Metadatos históricos incompletos / Identificador histórico no disponible / Contexto histórico no disponible:** los registros más antiguos no conservaron esta información. La información que falta nunca se completa por suposición.
- **Valores reservados:** el valor del campo se oculta por privacidad; solo se muestra el hecho de que cambió.
- **No hay evidencias de revisión inmutable disponibles para este registro.** No existe ninguna instantánea de revisión para este tipo de registro.

## Lo que no puede hacer / precaución
- La página es de solo lectura. Una entrada del historial de acciones no se puede editar ni eliminar.
- Que un registro aparezca en el historial no significa que el trabajo se haya verificado ni que haya sido eficaz; Verificado ≠ Eficaz (13.03 §4).
- Esta página registra cambios de negocio. Por sí sola no constituye una auditoría completa de las acciones de los usuarios ni una prueba de cumplimiento en materia de privacidad; los eventos de seguridad se conservan por separado.
- La lista solo muestra el ámbito para el que está autorizado. Los registros fuera de su ámbito no se listan; no ver un registro no significa que nunca haya cambiado.

## Si algo sale mal
- **Se requiere permiso de auditoría.** Compruebe su rol y su ámbito; contacte con el Tenant Admin si es necesario (13.03 §10).
- **Inicie sesión para continuar.** Vuelva a iniciar sesión.
- **Historial no disponible. Compruebe los filtros y reintente.** Compruebe que los identificadores estén completos y sean correctos y pulse **Reintentar**.
- **Ningún registro autorizado coincide con estos filtros.** Amplíe los filtros.
- Si un error de la aplicación persiste, comuníquelo al soporte con el ID de correlación, la pantalla, la hora y los pasos realizados (13.03 §10).

## Páginas relacionadas
