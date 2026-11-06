# WhatsApp masivo en el modal de Registros

## Uso

1. En el listado existente, marcar los clientes o la casilla de seleccionar todos.
   Los números válidos están habilitados por defecto. Un número repetido cuenta
   una sola vez, aunque tenga varios registros de compra.
2. Pulsar **WhatsApp masivo (N)** en la barra superior. Se abre un modal con la
   lista completa de seleccionados y el mensaje común.
3. Escribir el mensaje y pulsar **Comenzar envíos**. Se guarda la selección en la
   base de datos y se abre el enlace `wa.me` del primer seleccionado.
4. Enviar el mensaje manualmente en WhatsApp y volver al modal.
5. Pulsar **Enviado y siguiente**: se registra **Reactivado: Sí**, con su fecha,
   y se abre el enlace del siguiente contacto. El modal marca el contacto anterior
   como enviado y resalta el contacto actual.
6. Se puede omitir, pausar o cerrar el modal. El progreso y los pendientes no se
   pierden al cerrar sesión o actualizar. El botón superior permite continuar.

El mensaje se reutiliza; se puede guardar sin comenzar todavía. Una selección
pendiente conserva su mensaje original y no permite cambiarlo a mitad del envío.

## Un único estado en el listado

El filtro **Reactivado** tiene únicamente **Todos / Sí / No**.

- **Sí:** el número tiene al menos un envío declarado por el usuario.
- **No:** nunca se confirmó un envío, aunque se haya abierto un enlace.

Los registros anteriores del mismo número comparten ese indicador dentro de la
misma empresa. No se crean pedidos nuevos ni se cambia el estado de compra/venta.
Cada fila conserva el acceso a su historial.

Se retiraron el filtro Estado comercial y el panel de autorización en Editar.
Los registros existentes no requieren actualizarse individualmente para poder
seleccionarlos. Los datos de autorización guardados anteriormente no bloquean
números Sin verificar. Se preservan las solicitudes explícitas de no contacto
que ya estuvieran registradas; también se excluyen números inválidos.

Los filtros habituales de estado, búsqueda y fechas se combinan con Reactivado.
Si se elige Sí o No sin fechas, se consulta todo el historial en lugar del día de
hoy. Los enlaces usan números normalizados y prefijo 51 para móviles peruanos.

## Historial y continuidad

Cada contacto de campaña guarda el ID del registro, número, mensaje, campaña,
usuario, fecha de apertura y fecha de envío declarado. Abrir WhatsApp no marca
el número como reactivado. Las confirmaciones repetidas no duplican el historial.
El historial sobrevive a la eliminación del registro original mediante snapshots.

Solo el usuario creador puede operar su selección, dentro de su empresa.
Hay una selección pendiente por usuario; al abrir el modal se recupera primero.
La lista muestra todos los contactos y su progreso, incluyendo enviados y omitidos.

No se automatiza el botón Enviar ni se usan APIs de WhatsApp o RPA. WhatsApp se
abre en una pestaña y la lista/progreso se mantiene dentro del modal. Si el
navegador bloquea la pestaña, aparece un enlace manual.

## Despliegue y pruebas

Desplegar/reiniciar **OldSchoolLab**. Se reutilizan las tablas del historial ya
implementado. No requiere actualizar AuthService, OldSchoolApi ni la APK.

Pruebas de negocio:

```powershell
dotnet run --project tests/WhatsAppChecks -p:UseAppHost=false
```

PostgreSQL opcional: esquema temporal aislado que se elimina al terminar:

```powershell
dotnet run --project tests/WhatsAppChecks -p:UseAppHost=false -- --postgres-isolated D:/OldSchool/OldSchoolLab/OldSchoolLab/appsettings.json
```

Interfaz real con datos ficticios y autenticación exclusiva del proceso de pruebas,
base en memoria, sin conexiones a la base real:

```powershell
dotnet run --project tests/WhatsAppChecks -p:UseAppHost=false -- --serve-fixture
```

En otra consola, con Edge y playwright-core disponibles:

```powershell
node tests/WhatsAppChecks/browser.cjs
```

PLAYWRIGHT_CORE permite especificar la ruta del paquete y WA_SCREENSHOT la captura.
La prueba intercepta window.open; no se abre WhatsApp ni se envían mensajes.
Comprueba selección habilitada por defecto, deduplicación, filtro sí/no, lista del
modal, mensaje, confirmación, siguiente contacto, cierre/reapertura, recarga,
continuación, omisión, historial y protección antiforgery.
