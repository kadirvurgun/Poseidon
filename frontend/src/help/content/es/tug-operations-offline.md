---
key: tug-operations-offline
title: Operaciones sin conexión del remolcador
roles: [Tug Master, Crew]
permissions: [mode.tug, tug.read, tug.write]
related: [towage-risk-briefing, towage-execution, tug-daily-log, offline-conflict-center]
screenshots: []
reviewed: false
---
## Para qué sirve esta página
Esta página guarda en el dispositivo una **instantánea precargada** de un trabajo de remolcador y coloca tres tipos de capturas realizadas sin conexión en una **cola duradera y cifrada**: acuses de recibo del briefing, observaciones de la ejecución y entradas del diario. Responde a la pregunta: "¿Qué puedo capturar con seguridad sin conexión, qué espera en la cola y cómo lo recibió el servidor?" La liberación del Capitán, el GO de preparación, el inicio/las transiciones de la ejecución, la verificación y las revisiones siguen siendo registros humanos con conexión.

## Quién la usa
- **Patrón del remolcador:** para continuar con el trabajo crítico donde la conexión es débil y seguir el estado de sincronización (13.03 §10).
- **Tripulación:** para realizar sin conexión acuses de recibo del briefing y entradas de guardia, y para resolver conflictos sin dar por hecho que su propio registro prevalece automáticamente (13.03 §6).

## Antes de empezar
- Abra la página desde un trabajo de remolcador; sin un trabajo, la página muestra "Open this screen from a tug job to prefetch its snapshot."
- La línea **Política sin conexión** debe mostrar una política de almacenamiento configurada (`CONFIGURED_UNVERIFIED`); sin política, la precarga de instantáneas y la captura sin conexión no están disponibles.
- Precargue la instantánea **mientras tenga conexión**. Antes de capturar, compruebe que la concesión de la instantánea no ha vencido.

## Paso a paso
1. Con conexión, pulse **Precargar instantánea del trabajo**. Aparece el mensaje "Snapshot sealed on this device."
2. En **Instantánea del trabajo**, compruebe el número de trabajo, su estado y **Concesión hasta**.
3. Si es participante de un briefing emitido, elija **Confirmar** o **Disputar** (con un motivo de al menos 10 caracteres) en **Confirmar briefing** y pulse **Sellar y poner en cola**.
4. Para una ejecución en curso, elija la ejecución y el tipo de observación (Nota, Cabo de remolque conectado, Cabo de remolque soltado) en **Registrar observación de ejecución**, escriba la nota y pulse **Sellar y poner en cola**.
5. Para un diario en borrador o devuelto, elija el diario en **Entrada del registro diario**, escriba el **Resumen del día** y pulse **Sellar y poner en cola**.
6. Siga el estado de cada captura en **Capturas en cola**. Las capturas se envían en orden cuando vuelve la conexión; para enviarlas manualmente, use **Enviar capturas en cola**.
7. Si una captura muestra **Conflicto material**, lea **Cambiado en el servidor**, elija expresamente **Conservar el registro del servidor** o **Aplicar mi valor capturado sobre el registro actual** y pulse **Resolver conflicto**.

## Qué significan los estados
- **En cola en el dispositivo (no enviado):** la captura está sellada en este dispositivo; en cola no significa enviado.
- **Aceptado por el servidor:** el servidor recibió la captura. Esto no es una aprobación ni una verificación.
- **Conflicto material:** el registro del servidor cambió después de la captura; nunca se fusiona automáticamente y espera la decisión de una persona.
- **Valor del servidor conservado / Rechazado por el servidor:** el resultado del servidor para la captura.
- **UNKNOWN (sin conexión):** el estado de la política no se pudo leer sin conexión; no se presupone ninguna política.

## Lo que no puede hacer / precaución
- Solo con conexión: liberación del Capitán, GO de preparación, inicio y transiciones de la ejecución, emisión del briefing, verificación, envío y revisión del diario.
- Los conflictos materiales nunca se fusionan automáticamente; la resolución es siempre una elección humana explícita (13.03 §1).
- No se puede hacer ninguna captura nueva con una concesión de instantánea vencida; precargue una instantánea nueva.
- En la reproducción, cada captura se procesa una sola vez con su id original; no vuelva a crear la misma captura.

## Si algo sale mal
- **Mensaje "The device key for this queue is unavailable.":** los registros capturados se conservan pero no se pueden leer; no los elimine y contacte con soporte.
- **Mensaje "Outcome unknown":** la misma captura se reintentará con su id original; no cree una nueva.
- **Mensaje "The offline policy does not allow a snapshot.":** contacte con el administrador de su inquilino sobre la configuración de la política (13.03 §10).
- **Conflicto material:** use el formulario **Resolver conflicto** de esta página; los conflictos también aparecen en el **Centro de conflictos materiales** (13.03 §10).

## Páginas relacionadas
