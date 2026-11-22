---
key: audit-ready
title: Trazabilidad lista para auditoría
roles: [Regulatory / Assurance, DPA / HSEQ]
permissions: [audit.read, human.trace.read]
related: [requirements, sources, audit]
screenshots: []
reviewed: false
---
## Para qué sirve esta página
La página Trazabilidad lista para auditoría muestra, etapa por etapa, las cadenas de trazabilidad que tiene asignadas para un buque: Requisito → control → evidencia → verificación → decisión, con estados, marcas de tiempo y localizadores de fuente declarados. Responde a la pregunta: «¿En qué requisito se apoya esta evidencia, quién la verificó y qué decisión se registró?». Una cadena registrada es un registro humano, no una aprobación ni una declaración de disponibilidad (13.03 §4, §8).

## Quién la usa
- **Usuario de Normativa / Aseguramiento:** comprueba que no quede ningún registro huérfano en la cadena Requisito→Control→Evidencia→Verificación (13.03 §8).
- **DPA / HSEQ:** muestra el recorrido Hallazgo→Acción→Verificación con su evidencia al preparar una auditoría, una inspección PSC u otra inspección (13.03 §4).

## Antes de empezar
- La página requiere lectura de auditoría y lectura de trazabilidad humana, además de un nombramiento vigente como lector de trazabilidad para ese buque. El permiso por sí solo no basta; sin el nombramiento no se lista ninguna cadena.
- Elija el buque correcto. La lista muestra solo las cadenas que tiene asignadas.
- La página se carga a través de una conexión en vivo. Sin conexión, los datos mostrados pueden estar desactualizados.

## Paso a paso
1. Elija un buque en **Buque** (**Seleccione un buque**).
2. Abra una cadena desde **Cadenas asignadas**.
3. En **Etapas de la trazabilidad**, despliegue cada etapa (Requisito, Control, Evidencia, Verificación, Decisión) y revise **Estado**, **Registrado el**, **Revisión**, **Hash del contenido** y **Localizador de fuente (declarado, no verificado)**.
4. Si se listan **Puntos abiertos**, transmita los códigos al responsable del registro afectado.
5. Cuando existan cadenas asignadas legibles, compile un paquete en **Paquete de auditoría** con **Compilar paquete nuevo**, ábralo desde **Paquetes compilados** y use **Exportar JSON** si lo necesita.
6. Pulse **Reintentar** en la cabecera para actualizar los datos.

## Qué significan los estados
- **Cadena registrada: solo registro humano, no una aprobación:** todas las etapas están registradas. Esto no declara que el buque o la operación estén listos ni que cumplan.
- **Revisión requerida:** al menos una etapa falta, es desconocida o no está disponible. Una entrada desconocida o ilegible nunca se muestra como completa.
- **No registrado:** no existe ningún registro para esa etapa; por ejemplo, todavía no se ha registrado ninguna verificación ni decisión.
- **No disponible / UNKNOWN:** el registro no se puede leer ahora; no se presupone su contenido.
- **Desactualizado: una entrada fue sustituida o caducó; revisión requerida.** Una evidencia o una fuente cambió después; revise de nuevo la cadena.
- **Localizador no indicado:** no se introdujo ningún localizador de fuente; un localizador es una declaración, no un hecho verificado.
- Una evidencia verificada no significa que la acción haya sido eficaz: Verificado ≠ Eficaz (13.03 §4).
- **Paquete de auditoría:** «Compilación del sistema; no es un registro oficial». Los elementos fuera de sus nombramientos solo se cuentan, no se muestran; los elementos **Sustituido (se conserva para la trazabilidad)** nunca se tratan como vigentes.

## Lo que no puede hacer / precaución
- En esta página no puede editar registros, verificar ni decidir; es de solo lectura. La verificación y las decisiones las registra la persona nombrada en sus propias páginas.
- No puede ver cadenas que no tenga asignadas; las cadenas ocultas nunca se nombran y las cadenas asignadas no disponibles solo se muestran como un recuento.
- Los hashes de integridad y de contexto no son autoridad ni aprobación.

## Si algo sale mal
- **No está autorizado para esta vista.** Compruebe su rol, el buque y el contexto de nombramiento; contacte con el administrador del tenant (13.03 §10).
- **No tiene cadenas asignadas para este buque.** Compruebe que seleccionó el buque correcto; pregunte a la persona responsable si se necesita un nombramiento.
- **No se pudo cargar la trazabilidad.** Pulse **Reintentar**.
- **Cadena no encontrada o ya no visible para usted.** Puede que su nombramiento haya terminado o que el registro haya salido de su ámbito.
- Si un registro no se puede verificar, revise su evidencia y su procedencia y, si es necesario, contacte con el verificador (13.03 §10).

## Páginas relacionadas
