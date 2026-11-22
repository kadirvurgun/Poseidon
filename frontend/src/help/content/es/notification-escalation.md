---
key: notification-escalation
title: Notificaciones
roles: [Chief Engineer, Tug Master, Master, Crew]
permissions: [notification.read, maintenance.read, equipment.read, notification.acknowledge, notification.preference.write]
related: [today, offline-conflict-center]
screenshots: []
reviewed: false
---
## Para qué sirve esta página
La página de notificaciones lista las **excepciones de fuentes configuradas en la aplicación** (mantenimiento vencido, revisión de mantenimiento requerida, fuente de mantenimiento modificada o no disponible) del buque o remolcador seleccionado. Responde a la pregunta: «¿Qué notificaciones de mantenimiento han llegado a mi rol, qué gravedad tienen, cuáles se escalaron porque nadie acusó recibo a tiempo y en qué registro de origen se basan?». El acuse de recibo no verifica evidencias ni aprueba operaciones.

## Quién la usa
- **Jefe de máquinas / Superintendente técnico:** para seguir los vencimientos de mantenimiento y las solicitudes de revisión e ir al plan de origen (13.03 §5).
- **Patrón del remolcador / Capitán / Tripulación:** para ver las notificaciones dirigidas a sus roles y acusar recibo (13.03 §6).

## Antes de empezar
- Necesita acceso de lectura a notificaciones, mantenimiento y equipos. Acusar recibo y guardar preferencias requieren permisos separados; sin ellos no se muestran los botones correspondientes.
- Elija un buque o remolcador autorizado en **Buque de la notificación**.
- Sin conexión no se puede enviar ningún comando de acuse de recibo ni de preferencias.

## Paso a paso
1. Elija un buque en **Buque de la notificación**.
2. Lea la línea de política: «Configurada · política de fuente sin verificar · entrega solo en la aplicación».
3. En cada notificación, lea el título, la insignia de gravedad (**Información**, **Advertencia**, **Crítico**) y la insignia de estado; compruebe **Fuente / revisión**, **Fecha de vencimiento** y **Creado el**. **Regla de enrutamiento** muestra el código de la regla.
4. Use **Abrir plan de origen** para ir al plan de mantenimiento relacionado.
5. Para registrar que ha visto una notificación, pulse **Acusar recibo** (solo en estado **Abierto** o **Escalado**).
6. Si tiene acceso a las preferencias, edite **Silenciar notificaciones orientativas no obligatorias** y **Horas de silencio** (inicio, fin, zona horaria IANA) y pulse **Guardar preferencia orientativa**.
7. Use **Notificaciones anteriores** / **Más notificaciones** para otras páginas y **Volver a cargar bandeja** para volver a leer.

## Qué significan los estados
- **Abierto:** la notificación espera un acuse de recibo.
- **Acuse registrado:** una persona registró que la vio. Esto no significa que el mantenimiento se haya hecho ni que la evidencia esté verificada.
- **Escalado:** no se acusó recibo de la notificación a tiempo y se dirigió al rol de escalado de su regla.
- **Fuente modificada / Fuente no disponible:** el registro de base cambió o no se puede confirmar. La base registrada se conserva; esto no anula ni vuelve a activar la notificación.
- **Ruta obligatoria — no puede silenciarse:** las preferencias no pueden silenciar esta notificación.
- **Desconocido (Fecha de vencimiento):** la fuente no tiene fecha de vencimiento. Esto no significa que no haya nada vencido.

## Lo que no puede hacer / precaución
- El acuse de recibo no verifica evidencias ni aprueba operaciones.
- Las preferencias orientativas no pueden silenciar las rutas obligatorias y críticas. Las horas de silencio solo retrasan los avisos opcionales.
- Una página vacía no significa que todo esté en orden. Si la política o las fuentes no están disponibles, la página lo indica por separado.
- No hay reintento automático; un comando incierto se conserva en esta pestaña y los cambios de contexto permanecen bloqueados.

## Si algo sale mal
- **Mensaje «Resultado desconocido»:** use **Reintentar comando exacto de notificación** para volver a enviar el comando conservado.
- **Mensaje «La versión de la notificación ha cambiado»:** use **Volver a cargar bandeja** para cargar la lista actual.
- **Mensaje «Fuentes de notificaciones no disponibles o acceso denegado.»:** compruebe su rol y el contexto del buque; contacte con el administrador del tenant si es necesario (13.03 §10).
- **Mensaje «No se pudieron aplicar las horas de silencio guardadas»:** se muestran todos los avisos; revise la configuración de sus horas de silencio.

## Páginas relacionadas
