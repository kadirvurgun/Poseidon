---
key: user-admin
title: Users and permissions
roles: [Tenant Admin]
permissions: [membership.read, membership.manage]
related: [audit, companies, fleet]
screenshots: [1, 2, 7]
reviewed: false
---
## Para qué sirve esta página
La página Users and permissions gestiona las **membresías** de su espacio de trabajo. Responde a la pregunta: "¿Qué puede ver y hacer esta persona, y en qué buques?" La cuenta en sí (nombre de usuario, contraseña, MFA) reside en el proveedor de identidad central (IdP), no en POSEIDON. Esta página **invita** a esa cuenta a una membresía y concede paquetes de roles, permisos y ámbito de buques (13.03 §9). Un miembro nuevo es invitado: la invitación no concede nada hasta que la persona inicia sesión y la acepta.

## Quién la usa
- **Tenant administrator:** asigna el ámbito de usuario, rol, buque y objeto con el mínimo privilegio, invita a nuevos miembros y gestiona las delegaciones y la suspensión (13.03 §9).
- Otros usuarios con acceso de lectura de membresías pueden ver la lista, pero no modificarla.
- La **persona invitada** solo abre el enlace de invitación; nunca ve esta página.

## Antes de empezar
- Ver requiere lectura de membresías; modificar requiere gestión de membresías más gestión de todo el inquilino. Con acceso de solo lectura verá "You can view but not change memberships."
- La persona necesita una cuenta en el proveedor de identidad. El campo **Expected IdP subject (optional)** limita la invitación a esa única cuenta; sin él, cualquiera que inicie sesión con el enlace puede aceptarla.
- Prevea un canal de confianza para entregar el enlace de invitación (por ejemplo, la mensajería propia de su organización). POSEIDON no envía correos electrónicos.
- Solo puede conceder permisos que usted mismo tenga. Su propia membresía la edita otro administrador.
- La administración de membresías requiere una conexión activa con el servidor; sin conexión no se puede modificar nada.

## Paso a paso
1. Consulte los miembros en la lista **Members**. Filtre por **Status** (por ejemplo **Invited**) y **Role bundle**, y pulse **Apply**. Para buscar, introduzca un prefijo de id de membresía o de cuenta, o el IdP subject exacto, y pulse **Search**.
2. Abra un miembro con **Open**. Revise **Effective permissions**, **Vessel scope**, **Invitation**, **Grant history** y **Delegations**.
3. Para invitar a un miembro, pulse **Add member**. Compruebe **IdP issuer**, introduzca opcionalmente **Expected IdP subject (optional)**, elija al menos un **Role bundle**, añada **Individual permissions** si es necesario y fije el ámbito (todo el inquilino o al menos un buque). Después pulse **Create membership**.
4. El miembro se abre como **Invited** con el **Invitation link**. Pulse **Copy invitation link** y entregue el enlace. El enlace solo se muestra en este momento; funciona una sola vez y caduca a las 24 horas.
5. Si el enlace se perdió o caducó, pulse **Create a new invitation link**; el enlace anterior deja de funcionar. Para retirar la invitación, pulse **Revoke invitation**.
6. La persona invitada abre el enlace, inicia sesión, comprueba lo que concede la invitación y pulsa **Accept invitation**. Tras volver a iniciar sesión, trabaja con la concesión que usted fijó; el miembro muestra entonces **Active** e **Invitation accepted**.
7. Para cambiar la concesión de un miembro existente, pulse **Edit grant** y después **Save grant**. Cada guardado crea una nueva revisión de la concesión; las revisiones anteriores permanecen en **Grant history**.
8. Para una cesión temporal, use **Delegate from this member** y complete el delegado, **Valid from**, **Valid until**, **Reason** y **Delegated permissions**.
9. Para retirar el acceso, use **Suspend** en **Lifecycle**; **Resume** lo reabre. **Revoke membership** es definitivo.

## Qué significan los estados
- **Invited:** la membresía espera a la persona invitada. No concede nada ni reserva la cuenta en ningún sitio, tampoco en ninguna otra organización.
- **Invitation pending / Invitation expired / Invitation replaced / Invitation revoked / Invitation accepted:** el estado del último enlace de invitación. Un enlace caducado, sustituido o revocado nunca se puede aceptar.
- **Active / Suspended / Revoked:** el ciclo de vida de la membresía. La siguiente solicitud de un miembro suspendido o revocado se deniega.
- **Origen de la concesión — Managed here:** la concesión se otorgó en esta página y puede modificarse aquí.
- **Origen de la concesión — Operator configuration:** la concesión procede de la configuración del servidor (bootstrap/break-glass) y no puede editarse aquí.
- **Origen de la concesión — No grant:** existe un registro, pero no concede nada.
- **Not held by you / You do not hold every permission of this bundle:** no puede conceder este permiso o paquete porque usted no lo tiene.
- **Saved. The change takes effect on the member's next request.** El acceso de un miembro invitado empieza en el siguiente inicio de sesión tras aceptar.

## Lo que no puede hacer / precaución
- No puede conceder un permiso, un buque ni un acceso a todo el inquilino que usted no tenga.
- No puede modificar su propia membresía; el último administrador activo no se puede eliminar.
- Una cuenta solo puede tener una membresía aceptada en una única organización. Una invitación nunca traslada una cuenta que ya pertenece a otra; en ese caso, la persona invitada solo ve que la invitación no se puede aceptar.
- Trate el enlace de invitación como una llave: cualquiera que lo tenga e inicie sesión puede aceptarlo, salvo que usted haya introducido un subject esperado. No lo publique en canales compartidos. Transmita siempre el enlace completo: la parte secreta después de **#** solo la lee el navegador y nunca llega al servidor ni a sus registros.
- Los permisos de decisión, verificación y aprobación nunca forman parte de un paquete. Solo un administrador que los tenga puede concederlos, uno a uno. Las acciones sujetas a designación (por ejemplo, decisiones de remolcador o de salida) también requieren una designación del operador. Conceder un paquete de roles no designa a nadie como Capitán ni como verificador.
- No puede aprobar una delegación en la que usted sea el origen o el delegado. Una delegación nunca supera la autoridad actual del origen y nunca abarca todo el inquilino.
- POSEIDON no almacena contraseñas, solo guarda una huella del enlace de invitación y nunca vuelve a mostrar los IdP subjects en la lista.

## Si algo sale mal
- **This identity is configured by the operator, already a member here or already invited here.** Abra en su lugar el miembro existente o revoque primero la invitación anterior.
- **La persona invitada ve "This invitation cannot be accepted":** el enlace caducó, ya se usó, fue sustituido o revocado, pertenece a otra cuenta, o su cuenta ya pertenece a una organización. Compruebe el estado de **Invitation** y, si es necesario, pulse **Create a new invitation link**.
- **El enlace no se copió:** seleccione el campo del enlace y cópielo manualmente.
- **Exceeds your own authority:** elija un paquete o un ámbito más restringido, o pídaselo a un administrador que tenga el permiso.
- **The record changed meanwhile:** pulse **Reload current state** y vuelva a hacer su cambio.
- **The outcome is unknown. Retry with the same request or reload.** Puede volver a enviar el mismo comando con seguridad.
- **Not the trusted identity provider:** compruebe el emisor. Si la cuenta procede de otro IdP, contacte con el administrador del sistema (13.03 §10).

## Páginas relacionadas
