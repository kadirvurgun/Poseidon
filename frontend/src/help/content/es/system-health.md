---
key: system-health
title: Estado del sistema
roles: [Tenant Admin, Support / Engineering]
permissions: [directory.read, audit.read, system.health.read]
related: [audit, notification-escalation, offline-conflict-center, user-admin]
screenshots: []
reviewed: false
---
## Para qué sirve esta página
"Estado del sistema" muestra a los administradores las **observaciones del servicio** en una sola pantalla: API, escritor de auditoría, sincronización, tareas de notificación, servicio de archivos y vigencia de los datos. Responde a la pregunta: "¿Qué señales del servicio se observaron para el buque seleccionado y este inquilino, y cuáles no?" La configuración y la telemetría ausente no establecen que el servicio esté sano ni que el buque esté listo.

## Quién la usa
- **System / Tenant Admin:** para detectar problemas de servicio y de sincronización y gestionarlos de forma controlada (13.03 §9).
- **Support / Engineering:** para revisar las observaciones cuando se notifica un error de la aplicación (13.03 §10).

## Antes de empezar
- La página solo es visible para usuarios con acceso de lectura a todo el inquilino, de lectura del historial de acciones y de lectura del estado del sistema.
- En la barra de contexto superior, compruebe que está seleccionada la **empresa correcta**.
- Las observaciones se solicitan por buque; primero debe seleccionar un buque.

## Paso a paso
1. Elija el buque en **Ámbito de observación del buque**. Si la lista es larga, use **Buques anteriores** / **Buques siguientes**.
2. Compruebe la hora "Leído a las".
3. En **Disponibilidad del servicio**, lea la hora de inicio del proceso y el tiempo en funcionamiento.
4. En las tarjetas **API** y **Escritor de auditoría**, lea las cifras de solicitudes, errores, registros persistidos y latencia. Abarcan este inquilino desde que se inició el proceso.
5. En **Sincronización**, consulte los resultados confirmados, los conflictos sin resolver y la última sincronización confirmada.
6. En **Tareas de notificación**, consulte las notificaciones abiertas, las escalaciones vencidas y el estado de la tarea de evaluación.
7. Lea las tarjetas **Servicio de archivos** y **Vigencia de los datos**.
8. En **Métricas del piloto**, revise las observaciones de los clientes inscritos en el piloto.
9. Pulse **Actualizar observaciones** para obtener datos actuales.

## Qué significan los estados
- **No observado / No disponible:** no hay ninguna observación para esta señal. Esto no significa "sano" ni "sin problemas".
- **Configurado — sin sondear:** el servicio de archivos parece configurado, pero no se realizó ninguna prueba en vivo del almacenamiento ni del escáner.
- **Dentro del calendario / En ejecución / Retrasado / Programador no habilitado / Ejecución abandonada (bloqueo vencido):** el estado de la tarea de evaluación de notificaciones.
- **Conflictos sin resolver:** conflictos fuera de línea que esperan una resolución humana. Nunca se fusionan automáticamente.
- **Vigente / Desactualizado / Desconocido / No disponible:** recuentos de las evaluaciones de vigencia de los datos. Desconocido no significa "Vigente".
- **Esta instantánea tiene más de dos minutos:** actualice antes de basarse en ella.

## Lo que no puede hacer / precaución
- Esta página es de solo lectura; no se realiza ninguna acción sobre servicios, buques ni registros.
- Los registros de notificación existen solo dentro de la aplicación; no son prueba de entrega ni de ninguna decisión de seguridad.
- La disponibilidad del servicio solo muestra que este proceso de la API respondió; no demuestra que otros servicios estén funcionando.
- Las métricas del piloto son declaradas y no verificadas; no constituyen una declaración de aceptación.
- No corrija datos directamente en producción; publique los cambios de configuración de forma controlada (13.03 §9).

## Si algo sale mal
- **Acceso a estado no disponible en este contexto:** compruebe su rol y el ámbito del inquilino.
- **Observaciones del servicio no disponibles:** no se deduce ningún estado sano; use "Actualizar observaciones" pasado un rato.
- **Hay un conflicto sin resolver:** remita al usuario afectado al Centro de conflictos (13.03 §10).
- **Error de la aplicación:** notifíquelo a soporte/ingeniería con el Correlation ID, la pantalla, la hora y los pasos.

## Páginas relacionadas
