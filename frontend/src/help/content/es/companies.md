---
key: companies
title: Empresas
roles: [Tenant Admin, Shore Operations]
permissions: [canReadCompanies, canCreate]
related: [fleet, audit, today]
screenshots: [1, 2, 3]
reviewed: false
---
## Para qué sirve esta página
La página Empresas lista los **registros de empresas** de su espacio de trabajo que usted está autorizado a ver. Responde a la pregunta: «¿Qué empresas están definidas en este espacio de trabajo y cuáles son su código y su nombre?». Los registros de buques y remolcadores pertenecen a una empresa, por lo que elegir la empresa correcta es el primer paso para seleccionar su contexto.

## Quién la usa
- **Tenant Admin:** para crear y editar registros de empresas con el mínimo privilegio (13.03 §9).
- **Operaciones en tierra:** para encontrar el contexto de empresa correcto.

## Antes de empezar
- Necesita permiso para leer empresas para ver esta página. Sin él se muestra el mensaje «No tiene acceso a este espacio de trabajo.».
- Crear un registro requiere permiso de creación. Que pueda editar un registro depende del permiso que el servidor conceda para ese registro.

## Paso a paso
1. Vea el código y el nombre de cada empresa en la lista. Use **Anterior** / **Siguiente** para otras páginas.
2. Para buscar, escriba en **Buscar por código o nombre** y pulse **Buscar**.
3. Seleccione el nombre de una empresa para abrir la sección **Detalles**.
4. Para una empresa nueva, pulse **Nuevo registro**, introduzca el código y el nombre y pulse **Guardar**. Todos los campos son obligatorios.
5. Para modificar un registro existente, use **Editar** en los detalles.
6. Abra **Mostrar registro de la entidad jurídica** para ver la información de la entidad jurídica; esta sección es de solo lectura.

## Qué significan los estados
- **No se encontraron registros:** no se devolvió ningún registro para su búsqueda o su ámbito de permisos.
- **Entidad jurídica: Borrador – incompleto, no activo / Activa / Inactiva:** el estado del registro de la entidad jurídica. Borrador significa que todavía faltan valores antes de la activación.
- **Desconocido:** el valor no figura en el registro. Desconocido no significa «No» ni «válido».
- **Guardado:** el registro se escribió en el servidor. Este registro no crea ninguna aprobación operativa.

## Lo que no puede hacer / precaución
- Crear o editar un registro de empresa no es una aprobación operativa, de cumplimiento ni de seguridad.
- Los valores de la entidad jurídica no se modifican desde esta página; solo cambian mediante comandos de RR. HH. auditados. Los identificadores de registro mercantil se muestran enmascarados.
- Dos empresas no pueden usar el mismo código.
- Si sale de la página con un borrador sin guardar, se le pregunta si desea descartarlo o seguir editando.

## Si algo sale mal
- **Este registro cambió en otra operación:** su borrador se conserva. Descarte el borrador para cargar el registro actual y vuelva a hacer su cambio.
- **Este código ya está en uso:** introduzca un código diferente.
- **No se pudo confirmar el resultado:** los campos permanecen bloqueados hasta que se conozca el resultado; reintente de forma segura el mismo comando.
- **Es posible que su permiso haya cambiado / Sin acceso:** compruebe su rol y el contexto del espacio de trabajo; contacte con el administrador del tenant si es necesario (13.03 §10).
- **El servicio no está disponible:** use «Reintentar»; si el problema continúa, comuníquelo con la referencia de soporte.

## Páginas relacionadas
