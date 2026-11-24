# Envíos masivos mediante Evolution

El módulo web independiente está en `/massmessages`. Lo usan Gerencia, Gestor y
Vendedor con una empresa asignada. Las campañas y sus resultados se guardan en
la misma base de datos del sistema, separados por empresa y vendedor.

## Configuración en Easypanel

Configurar estas variables **en el servicio web OldSchoolLab**, reemplazando
`ID_EMPRESA` por el identificador numérico de la empresa:

```text
Evolution__Companies__ID_EMPRESA__BaseUrl=https://ventas-api-evolution-v2.wv6a0u.easypanel.host
Evolution__Companies__ID_EMPRESA__Instance=OldSchool
Evolution__Companies__ID_EMPRESA__ApiKey=<clave de la instancia>
```

Para la empresa existente **Old School Lab**, el identificador verificado es
**1**; sus variables son `Evolution__Companies__1__BaseUrl`,
`Evolution__Companies__1__Instance` y `Evolution__Companies__1__ApiKey`.

La clave queda únicamente en el servidor. No se incorpora al repositorio ni se
envía al navegador. Cada empresa tiene su propia configuración; no se usa una
instancia global por defecto. Para desarrollo se pueden usar secretos de .NET
con las mismas claves separadas por `:`. El host debe permanecer encendido para
procesar campañas. Se utiliza HTTPS sin seguir redirecciones para estas solicitudes.

Al desplegar el web, el arranque crea de forma aditiva las tablas
`MassMessageCampaigns`, `MassMessageRecipients` y `MassMessageSenders`. Se necesita
permiso para crear tablas e índices, como en el esquema existente del sistema.
No hace falta otro backend ni modificar el webhook de mensajes recibidos.

## Flujo

1. Escribir un nombre, el mensaje y el intervalo en segundos (1–86400; inicial 30).
2. Pegar números separados por líneas, comas o punto y coma; también seleccionar
   prospectos de la base filtrados por nombre, celular o fecha de registro.
   Cambiar los filtros conserva el mensaje y la selección. Se muestran hasta
   500 prospectos por consulta; se pueden combinar selecciones de varias consultas.
3. Pulsar **Revisar destinatarios**. Se crea un borrador con una copia del texto
   y los números. Se normalizan teléfonos (móviles peruanos locales reciben 51),
   se eliminan duplicados y se excluyen inválidos, “No contactar” y autorizaciones
   explícitas vencidas. Máximo 1000 destinatarios únicos por campaña.
4. Revisar y pulsar **Iniciar envíos**. Se llama a
   `POST /message/sendText/{instance}` con `number` y `text`. Las solicitudes son
   secuenciales. El siguiente envío espera al menos el intervalo configurado
   desde que termina la solicitud anterior.
5. Consultar progreso, pendientes y resultados; pausar, continuar o cancelar.
   Un mensaje que ya esté en curso puede terminar después de pausar o cancelar.

Cerrar el navegador no detiene una campaña iniciada. Tras reiniciar el servidor,
los pendientes de campañas en curso continúan; las pausadas permanecen pausadas.
El cambio de instancia requiere revisión y crear otra campaña: los borradores
conservan la instancia con la que se prepararon.

## Resultados e interrupciones

- **Aceptado por Evolution:** la API devolvió éxito e identificador; no confirma
  entrega ni lectura. Se conserva ese identificador en el resultado.
- **Rechazado:** la API rechazó la solicitud. Se pausa para revisar la conexión.
- **Sin confirmar:** timeout, respuesta incierta o interrupción durante el envío.
  Se pausa y se debe comprobar el chat antes de continuar. No se reenvía automáticamente.
- **Omitido:** el contacto quedó excluido por su autorización antes de enviar.

Continuar procesa únicamente pendientes. No reintenta rechazados o inciertos.
Se vuelve a revisar la autorización inmediatamente antes de cada solicitud.
Un bloqueo de sesión de PostgreSQL por instancia impide que dos réplicas o
campañas envíen simultáneamente. El siguiente momento disponible se persiste
para compartir el intervalo entre campañas de una misma instancia. El contexto
del procesador no reconecta automáticamente mientras mantiene ese bloqueo.

El intervalo no garantiza evitar restricciones de WhatsApp. Usar contactos que
esperen el mensaje y respetar las solicitudes de no contacto.

## Verificación

```powershell
dotnet build OldSchoolLab/OldSchoolLab.csproj
dotnet run --project tests/MassMessageChecks/MassMessageChecks.csproj -- <ruta-configuracion-con-conexion>
```

Las pruebas crean y eliminan un esquema temporal aislado de PostgreSQL y usan
un transporte HTTP simulado que rechaza hosts reales. Comprueban aislamiento
de cuentas, borradores sin envíos, deduplicación, exclusiones, procesamiento
concurrente, intervalo compartido, persistencia, errores y recuperación sin
reenvíos automáticos. Las credenciales y la instancia reales no se usan para enviar.
