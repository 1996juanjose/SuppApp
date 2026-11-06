# OldSchool Ventas 1.4: cola nativa de llamadas

Se reutiliza el proyecto Android .NET MAUI `OldSchoolDialer`, con el mismo
identificador de app `com.oldschool.ventas`.

## Flujo de uso

1. Abrir la app: carga automaticamente las empresas, sin campos de enlaces.
   Los servicios se configuran en `OldSchoolDialer/ServiceEndpoints.cs`.
2. Seleccionar la empresa e iniciar sesion. AuthService valida
   la pertenencia y emite el JWT; la API toma la empresa exclusivamente del JWT.
3. Elegir Sin llamar, Ya llamados, Volver a llamar pendientes o Todos; filtrar
   por nombre/celular, fecha de creacion y numero minimo/maximo de intentos.
4. Cargar una tanda de 50, 100, 250 o 500 prospectos.
5. Pulsar **Iniciar llamadas masivas** y conceder el permiso Telefono de Android.
6. Terminar la llamada en el telefono y volver a la app para elegir resultado:
   Contesto, No contesto, Ocupado, Numero incorrecto o Volver a llamar.
7. Elegir uno de los botones visibles de resultado y pulsar **Guardar y llamar
   al siguiente**. Esta accion explicita llama al siguiente tras la confirmacion
   del servidor, tambien si el candado estaba desactivado o la primera llamada
   se inicio individualmente. **Solo guardar, sin llamar** permite detenerse.
   Escribir notas no sustituye la eleccion del resultado; los botones de guardado
   se habilitan solo tras elegir una opcion.
8. Pausar detiene el avance incluso si se esta guardando. Un intento que no
   llego a realizarse puede cancelarse sin contarlo; esto pausa la tanda.

El candado empieza desactivado en cada sesion. No se marcan llamadas en segundo
plano ni al colgar sin intervencion del usuario. El resultado lo confirma la
persona: abrir el telefono no demuestra que el prospecto haya contestado.
Se usa ACTION_CALL y solo CALL_PHONE; no se lee el registro de llamadas ni se
graba audio. No se guarda la contrasena ni el token en preferencias.

Los intentos confirmados se guardan en PostgreSQL con resultado, notas, hora,
usuario y proxima llamada. El contador es independiente del estado comercial
Prospecto. La web muestra "Se llamo 1 vez", "Se llamo 2 veces", etc. junto al celular.
Las notas antiguas se conservan, pero no se inventan contadores a partir de
texto libre: el historial estructurado comienza con esta version.

Cada llamada pendiente tiene un RequestId estable. Si falla la red, el intento
se conserva en memoria y se puede reintentar sin duplicarlo. La app no avanza
antes de recibir confirmacion del servidor. Cerrar el proceso pierde la tanda
local pendiente; los intentos ya guardados permanecen en el servidor.

## API

- GET `/api/auth/companies` y POST `/api/auth/login` con CompanyId.
- GET `/api/records/prospect-calls/queue`: JWT obligatorio con company_id.
  Parametros: callFilter=all|uncalled|called|callback, search, fromDate, toDate,
  minAttempts, maxAttempts, take (1..500), afterId. Devuelve un array compatible
  con la app anterior y el encabezado X-Next-Cursor. Orden ascendente por Id.
  Devuelve callCount, lastCallAt, lastOutcome, nextCallAt y callLabel.
- POST `/api/records/{id}/prospect-call-result`: requestId (UUID), outcome,
  notes (maximo 250) y nextCallAt opcional. Solo escribe prospectos de la empresa
  del JWT. Los reintentos con el mismo UUID no agregan otro intento.
- GET `/api/records/{id}/prospect-calls`: ultimos 100 intentos del prospecto.

Volver a llamar deja IsCallConcrete=false y fija la fecha elegida; si no se
envia fecha, se usa el dia siguiente. El filtro callback incluye solo las
llamadas pendientes cuyo horario ya llego. Las fechas de callback se envian
sin offset, expresadas en hora de Peru.

## Despliegue y validacion

La version 1.4 usa produccion directamente:
- AuthService: https://ventas-authservice.wv6a0u.easypanel.host/
- OldSchoolApi: https://ventas-oldschool-api.wv6a0u.easypanel.host/

No requiere Visual Studio ni adb reverse. El cliente usa validacion TLS normal,
sin excepciones para certificados de desarrollo. Los servicios siguen definidos
en ServiceEndpoints.cs. La APK entregada es una compilacion Debug para pruebas;
no es una publicacion en Google Play.

Los ensamblados se incluyen en la APK para permitir instalarla sin Visual Studio.

Reiniciar/desplegar API y web antes de usar la nueva APK. Ambos crean de forma
idempotente la tabla LeadCallAttempts y su indice. No se modifican pagos ni
registros anteriores. AuthService y OldSchoolApi deben compartir la configuracion
de validacion JWT correspondiente.

Compilacion Android de prueba:

```powershell
dotnet build OldSchoolDialer/OldSchoolDialer.csproj -f net10.0-android -c Debug
```

Se verificaron compilacion Android, API y web; pruebas de candado/pausa/cancelar,
aislamiento por empresa, historial, filtros, callbacks e idempotencia. Tambien
se probo el esquema, guardado y consulta de la cola en PostgreSQL real dentro
de un esquema temporal, que se elimino al terminar. No se realizaron llamadas
reales. Falta probar permisos, llamada y regreso a la app en un telefono con SIM.

Referencias: [ACTION_CALL de Android](https://developer.android.com/guide/components/intents-common#Phone),
[permisos de .NET MAUI](https://learn.microsoft.com/en-us/dotnet/maui/platform-integration/appmodel/permissions).

## Volver a llamar al mismo prospecto (1.5)

Despues de una llamada, el boton queda disponible como "Guardar resultado y
volver a llamar a este prospecto". Selecciona primero el resultado del intento
anterior. Se guarda con su identificador y luego se marca el mismo numero,
creando un intento nuevo sin avanzar la tanda. Si falla el guardado no se llama;
si falla el marcado, el resultado anterior permanece guardado y puedes reintentar.

## Filtros y valores iniciales (1.6)

La fecha de creacion y el avance automatico se activan por defecto. El rango
inicial sigue siendo el ultimo mes; puede cambiarse antes de cargar.
Aplicar filtros permanece disponible despues de iniciar una llamada. Si hay un
resultado pendiente, se puede guardar el resultado seleccionado sin llamar al
siguiente, descartar explicitamente el intento sin contarlo, o volver conservandolo.
Si falla el guardado no se cambia la cola. Cambiar filtros pausa la tanda; para
reanudar llamadas se pulsa Iniciar llamadas masivas. No se llama al cargar filtros.
