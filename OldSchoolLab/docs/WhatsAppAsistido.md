# WhatsApp asistido en Registros

La función se integra en `/Records`, sin nuevas pantallas ni servicios de envío.
Disponible para Gerencia, Gestor y Vendedor con empresa asignada.

## Uso

1. Para registros existentes, abrir **Editar → WhatsApp / reactivación**. Guardar
   el estado comercial del número: Sin verificar, Autorizado o No contactar.
   Para autorizar, indicar fecha y referencia. La vigencia es opcional.
   Si cambia el celular, guardar primero el registro y luego el estado del nuevo
   número. La autorización nunca se hereda automáticamente a otro número.
2. Seleccionar filas o todos los números habilitados visibles. Los números
   peruanos móviles de nueve dígitos reciben prefijo 51; se deduplican por empresa.
3. Usar **⚙️** para configurar y guardar el mensaje común. Por defecto se excluyen
   los números con envíos declarados anteriores. El panel informa exclusiones.
4. **WhatsApp masivo → Iniciar con los habilitados** abre un enlace `wa.me`.
   **Generar enlace** en una fila permite hacerlo para un solo registro.
5. Enviar manualmente en WhatsApp. Al volver, pulsar **Enviado y siguiente** para
   declarar el envío y generar el enlace del siguiente pendiente.
6. Omitir o pausar según corresponda. La selección y su mensaje quedan guardados;
   cerrar sesión o recargar no elimina el progreso. Una recarga no abre enlaces.
   Al volver, abrir el pendiente o pulsar Continuar si la campaña estaba pausada.

## Estados y filtros

- **Enlace generado:** se registró la generación del enlace; no acredita envío.
- **Enviado / ya contactados:** el usuario declaró manualmente el envío.
- **Sin envío masivo:** el número nunca tuvo una declaración de envío, aunque
  tenga enlaces generados o selecciones pendientes.
- **Pendientes de campaña:** pendientes de la selección activa del usuario.
- **Contactados hoy:** declaraciones dentro del día de Perú.
- **Reactivación:** números con un enlace generado desde esta función, incluyendo
  sus registros anteriores/repetidos. No significa que una venta se haya concretado.

Los filtros se combinan con estado, búsqueda y fechas existentes. Si se elige un
filtro de WhatsApp o consentimiento sin fechas, se consulta todo el historial
(en lugar del valor predeterminado de hoy del listado).

El indicador de cada fila muestra reactivación y el último envío declarado.
**Historial** abre el detalle dentro del listado, con mensaje, fechas, número,
usuario y campaña. El historial está asociado al número y a su registro original.
No se cambia el estado comercial de compra/venta del registro.

## Persistencia y despliegue

Desplegar/reiniciar **OldSchoolLab (web MVC/Razor)**. La inicialización existente
crea de forma aditiva cinco tablas PostgreSQL: WhatsAppContacts,
WhatsAppConsentChanges, WhatsAppMessageSettings, WhatsAppCampaigns y
WhatsAppCampaignRecipients. No se necesita modificar AuthService, la API ni la APK.

Los registros existentes conservan sus datos. Sin consentimiento guardado, el
número se considera Sin verificar y no se incluye en selecciones comerciales.
Los contactos revocados/vencidos se comprueban de nuevo al generar enlaces y
al confirmar envíos. Historial y campañas están separados por empresa; solo el
usuario creador puede operar su campaña. Una selección pendiente por usuario.

Se guardan snapshots del número, mensaje y cliente; borrar el registro original
no elimina el historial. Los comandos repetidos son idempotentes. En PostgreSQL,
las mutaciones usan una transacción con bloqueo por empresa dentro de la
estrategia de reintento. Las operaciones POST requieren antiforgery.

No se comprueba entrega, lectura ni conversión de venta. No se usa Cloud API,
Evolution, RPA ni automatización del botón Enviar. Si el navegador bloquea una
pestaña, se muestra el enlace para abrirlo manualmente.

## Validación

Pruebas de negocio (sin tocar base real):

```powershell
dotnet run --project tests/WhatsAppChecks -p:UseAppHost=false
```

Pruebas PostgreSQL opcionales, en esquema temporal generado y eliminado:

```powershell
dotnet run --project tests/WhatsAppChecks -p:UseAppHost=false -- --postgres-isolated D:/OldSchool/OldSchoolLab/OldSchoolLab/appsettings.json
```

La ruta debe ser absoluta. Nunca se imprimen credenciales. Se verifica la creación
aditiva de tablas sobre una base existente, reintentos, historial y traducción SQL.

Prueba de interfaz con datos ficticios, autenticación exclusiva del proceso de
pruebas y base en memoria, en `127.0.0.1:5187`:

```powershell
dotnet run --project tests/WhatsAppChecks -p:UseAppHost=false -- --serve-fixture
```

En otra consola, con `playwright-core` disponible y Edge instalado:

```powershell
node tests/WhatsAppChecks/browser.cjs
```

Se puede indicar la ruta de playwright-core con PLAYWRIGHT_CORE y el archivo de
captura con WA_SCREENSHOT. El navegador intercepta window.open y no accede a
WhatsApp. Verifica selección sin duplicados, CSRF, codificación del mensaje,
confirmación manual, continuación, recarga, omisión, filtros e historial, además
del guardado del estado comercial desde Editar.
