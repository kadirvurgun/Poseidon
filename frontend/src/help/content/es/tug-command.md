---
key: tug-command
title: Mando de remolcadores
roles: [Tug Master, Tug Operations Coordinator, Shore Operations, Crew]
permissions: [mode.tug, tug.read]
related: [tug-job-board, tug-job-intake, tug-assignment, tug-readiness, today]
screenshots: [1, 2, 3, 4, 5, 6, 7]
reviewed: false
---
## Para qué sirve esta página
Esta página, que aparece en el menú como **Mando de remolcadores**, muestra el **contexto de origen** del remolcador seleccionado. Responde a la pregunta: "¿Qué declaraciones de función, tipo, tiro a punto fijo (BP) y equipos están registradas para este remolcador, de dónde proceden y qué vigencia tienen?" La página solo muestra las declaraciones y sus fuentes; no otorga ninguna aprobación de preparación operativa, idoneidad, permiso ni seguridad. El distintivo de la parte superior lo indica: "Source declarations · operational readiness unknown".

## Quién la usa
- **Patrón del remolcador:** para confirmar el contexto de función, BP y equipos de su propio remolcador antes de empezar el trabajo (13.03 §3).
- **Coordinador de operaciones de remolcador / Operaciones en tierra:** para asegurarse de estar en el contexto de remolcador correcto antes de pasar al tablero de trabajos y a la recepción de trabajos (13.03 §7).
- **Tripulación:** para ver su vista de rol y las pantallas del modo buque que tiene abiertas (13.03 §6).

## Antes de empezar
- Necesita el modo remolcador y acceso de lectura de remolcador. Sin ellos, la página muestra "El acceso no está disponible."
- Si no hay ningún contexto seleccionado, la página enumera los remolcadores autorizados en **Seleccionar remolcador**; abra uno.
- La lista de equipos también requiere acceso de lectura de equipos; sin él, la fuente de equipos aparece como "unavailable or not granted".

## Paso a paso
1. Abra el remolcador desde la lista **Seleccionar remolcador**. La línea **Contexto del remolcador** bajo el encabezado muestra el código, el nombre y el modo del remolcador.
2. En **Rol (asignación del servidor)** y **Vista de menú en modo remolcador**, lea el rol y la vista de menú que el servidor le asignó.
3. En **Visibilidad de modo**, compruebe si las pantallas del modo buque están abiertas. La sección **Pantallas del modo buque** solo enlaza con las pantallas que tiene concedidas.
4. Compruebe en **Tipo de remolcador**, **Fuente / revisión del tipo** y **Observación / política de fuente del tipo** de qué fuente y revisión procede el tipo.
5. Lea **BP del perfil (sin verificar)**, **Fuente / versión / procedencia de BP** y **Vigencia / observación de BP**. Es un valor declarado, no medido.
6. En **Contexto del equipo**, consulte las declaraciones de equipos, sus niveles, revisiones y procedencia.
7. Use las pestañas de la página para pasar a **Tablero de trabajos** o (con acceso de escritura) a **Recepción de trabajos**, o (con acceso de escritura) pulse **Nueva solicitud**.

## Qué significan los estados
- **Desconocido — sin asignación de rol:** el servidor no tiene ninguna asignación de rol para usted en este remolcador. Esto no es "sin rol, sin problema"; se necesita una asignación por parte de un administrador.
- **Etiquetas de rol no disponibles / Vista de menú no disponible:** la fuente no se puede leer ahora. La información ausente no se interpreta como vacía.
- **No hay vista configurada para el modo remolcador:** no hay ninguna vista de menú definida para usted en este modo.
- **Ocultas en modo remolcador. Se requiere un rol multimodo.:** las pantallas del modo buque requieren un rol multimodo autorizado expresamente.
- **BP del perfil (sin verificar):** el tiro a punto fijo declarado. No es un resultado de prueba verificado.
- **No se devolvieron declaraciones de equipos. La aptitud operativa verificada sigue siendo desconocida.:** una lista vacía nunca significa que el equipo sea apto.
- **Desconocido / No disponible:** el valor de origen falta o no se pudo leer. Desconocido nunca sustituye a "Listo" ni a "No".

## Lo que no puede hacer / precaución
- En esta página no se edita ningún campo ni se produce ningún resultado de preparación. La decisión operativa y la autoridad de seguridad siguen correspondiendo al Patrón del remolcador a bordo (13.03 §3).
- Una propuesta de asignación desde tierra nunca prevalece sobre la decisión de seguridad a bordo del Patrón del remolcador (13.03 §7).
- Incluso con un permiso multimodo declarado, cada pantalla del modo buque sigue necesitando su propia autorización del servidor.
- Sin conexión no se muestran las fuentes actuales del perfil; vuelva a conectarse para comprobar de nuevo el acceso y las versiones de las fuentes.

## Si algo sale mal
- **El acceso no está disponible:** compruebe su rol, el remolcador y el contexto del objeto; si es necesario, contacte con el administrador de su inquilino (13.03 §10).
- **Alerta "Current identity and vessel access must be checked":** pulse **Volver a comprobar acceso**.
- **Datos no disponibles:** compruebe su conexión y pulse **Reintentar**.
- **Valores de BP o de equipos inesperados:** anote los campos de fuente y versión y revíselos con el responsable de la fuente; si un registro no se puede verificar, compruebe sus evidencias y su procedencia (13.03 §10).

## Páginas relacionadas
