---
key: fleet
title: Flota
roles: [Master, Tug Master, Chief Engineer, Shore Operations, Tenant Admin]
permissions: [directory.read, directory.write]
related: [today, companies, certificate-command-center, data-trust-center, departure-signoff]
screenshots: [1, 2, 3, 4, 5, 6, 7]
reviewed: false
---
## Para qué sirve esta página
«Flota» es la **lista de buques y remolcadores de su ámbito**. Responde a la pregunta: «¿A qué buques puedo acceder y dónde están el registro de un buque, su perfil normativo, su pasaporte de cumplimiento y sus adjuntos?». La página muestra el registro del buque y la información registrada; la preparación operativa se evalúa por separado.

## Quién la usa
- **Capitán / Patrón del remolcador / Jefe de máquinas:** para seleccionar su propio buque e ir a sus espacios de trabajo (equipos y mantenimiento, lista de tripulantes) (13.03 §2, §3, §5).
- **Operaciones en tierra:** para encontrar buques de la flota y revisar sus registros.
- **Tenant Admin:** para crear y editar registros de buques (13.03 §9).

## Antes de empezar
- En la barra de contexto superior, compruebe que esté seleccionada la **empresa correcta** (13.03 §1). La lista solo muestra buques de su ámbito.
- Necesita acceso de lectura para ver la lista. «Nuevo registro» y «Editar» solo aparecen para los buques sobre los que tiene acceso de escritura.

## Paso a paso
1. En las tarjetas de resumen de la parte superior, vea el número de buques listados, buques, remolcadores y registros editables de esta página. **Los recuentos cubren solo esta página**.
2. Escriba en **Buscar por código o nombre**, elija «Buque» o «Remolcador» en **Tipo de buque** si es necesario y pulse **Buscar**.
3. Pulse el nombre de un buque en la tabla. **Detalles** muestra su código, nombre y tipo; el buque seleccionado aparece como «Buque actual».
4. Abra el perfil del buque con **Perfil normativo**, o sus datos de incorporación con **Incorporación de buque piloto**.
5. Use **Pasaporte digital de cumplimiento** para revisar los requisitos registrados, las fuentes, la revisión de aplicabilidad y los bloqueos sin resolver. Use **Volver al buque** para regresar.
6. Vaya a los espacios de trabajo del buque con **Importación controlada**, **Equipos y mantenimiento** o **Lista de tripulantes**.
7. En la sección inferior, cambie entre **Vessel attachments** y **Borradores de documentos**.
8. Si tiene acceso, añada un buque con **Nuevo registro** o modifíquelo con **Editar** y guarde. Use **Cambiar buque** para otro buque.

## Qué significan los estados
- **Solo lectura / Registros editables:** su acceso actual en esta página. «Editar» no se muestra en un registro de solo lectura.
- **Solo esta página. La preparación operativa se evalúa por separado.:** el gráfico de composición de buques no ofrece información sobre la preparación.
- **Revisión requerida — vigencia sin verificar:** la información del pasaporte se lee de fuentes registradas y no está verificada. El pasaporte no es una aprobación de cumplimiento.
- **No se devolvieron filas de requisitos vigentes:** esto no establece que el conjunto de obligaciones esté vacío.
- **Desconocido / No disponible:** el valor falta en la fuente o no se pudo leer. Esto no significa «Conforme» ni «Listo».

## Lo que no puede hacer / precaución
- Esta página nunca dice que un buque esté listo para operar o para salir. La decisión de salida corresponde al capitán y la decisión de remolque al patrón del remolcador.
- Un administrador genérico no puede sustituir la admisión cualificada de fuentes ni una decisión humana autorizada.
- Si sale con cambios sin guardar, se le pide que lo confirme; los cambios se conservan hasta que guarde o salga explícitamente.
- Los datos mostrados sin conexión no están verificados; vuelva a conectarse y reintente.

## Si algo sale mal
- **No tiene acceso a este espacio de trabajo:** compruebe su rol y el contexto del buque; contacte con el administrador del tenant si es necesario (13.03 §10).
- **No se encontraron registros:** cambie la búsqueda o el filtro de tipo de buque.
- **Inicie sesión para continuar:** su sesión ha terminado; use de nuevo «Iniciar sesión».
- **El servicio no está disponible:** use «Reintentar»; si persiste, comuníquelo al soporte con una captura de pantalla, la hora y los pasos realizados.

## Páginas relacionadas
