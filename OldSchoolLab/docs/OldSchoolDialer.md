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

## Flujo simplificado y sesion (1.7)

Resultados disponibles: Contesto, No contesto (N veces), Competencia y No
interesado. La API agrega cada resultado a CallActivity; el contador de no
contestadas cuenta solo esos intentos, con el mismo ID idempotente por llamada.
Se retiran Ocupado, Numero incorrecto y Volver a llamar de los nuevos resultados.
El historial anterior se conserva.

Se quitan Pausar, Solo guardar, Cancelar intento y Cargar siguiente tanda de la
pantalla. Guardar y llamar al siguiente sigue disponible. Al terminar una tanda
carga automaticamente la siguiente usando los mismos filtros, sin llamar si no
hay mas registros. Cambiar filtros permite guardar el resultado sin avanzar o
descartar explicitamente el intento pendiente.

Filtros, rango de fechas y preferencia de avance se guardan al modificarse.
La sesion se conserva en SecureStorage, sin almacenar la contrasena. Al abrir se
recupera si el token sigue vigente. Cerrar sesion elimina el token, no los filtros.
No se inicia una llamada automaticamente al abrir o recuperar la app.

El login movil envia rememberSession=true. Desplegar AuthService y OldSchoolApi
para habilitar la duracion movil y los resultados nuevos. Jwt:MobileExpiryMinutes
es opcional (por defecto 10080, siete dias; limitado entre 60 y 10080). Los otros
clientes conservan Jwt:ExpiryMinutes. Un token ya emitido no amplia su vencimiento;
es necesario iniciar sesion de nuevo despues de desplegar AuthService.

Pruebas aisladas de resultados y duracion JWT:
dotnet run --project tests/DialerChecks -p:UseAppHost=false


## Preferencia visual anotada (06/11/2026)

Referencia enviada por el usuario: ficha compacta sobrepuesta con numero y nombre
arriba, resultado/feedback destacado y acciones inferiores claras. Se conserva
como referencia de un futuro rediseno; no se implementan las opciones SMS, email
ni reprogramacion de la imagen por esta anotacion.

En 1.8 se retiran el encabezado promocional y la descripcion superior. El borde
del formulario de acceso se oculta completo al iniciar sesion, evitando el
recuadro vacio que ocupaba espacio encima de los filtros.

## Pantalla dedicada de llamada (1.9)

Filtros y preparacion permanecen en MainPage. Iniciar llamadas abre CallPage,
una pantalla nativa separada que permanece al volver de la llamada telefonica.
Muestra el prospecto, los cuatro resultados y notas. Debajo quedan solo Guardar
y llamar al siguiente y Omitir y seguir. Omitir avanza sin enviar un resultado
ni incrementar intentos. La accion explicita Omitir abre el siguiente numero.
Volver a filtros conserva el resultado pendiente; Continuar llamada actual
permite regresar a la ficha sin volver a marcar ni perder las notas.

## Progreso de llamadas (2.0)

Se retiran Preparar mi tanda, el selector Sin llamar, busqueda e intentos minimo/
maximo de la pantalla. Se conservan fechas y tamano de carga. La consulta movil
trabaja con prospectos sin llamadas guardadas dentro del rango elegido; los
filtros antiguos ocultos ya no se envian ni se restauran.

Arriba, tanto en filtros como en la ficha de llamada, aparecen Total, Llamados,
Faltan y Omitidos con una barra de progreso. Llamados aumenta solo tras guardar
un resultado confirmado por la API. Omitir reduce pendientes sin sumar llamadas.
El conteo no se reinicia al cargar automaticamente otra tanda ni al volver a los
filtros. Aplicar un rango nuevo comienza un recorrido nuevo. Reiniciar la app
consulta los prospectos todavia sin llamar del rango guardado.

Desplegar OldSchoolApi para el encabezado X-Total-Count, que devuelve el total de
prospectos validos coincidentes antes de paginar. En una API anterior se muestra
el total conocido de contactos cargados, ampliandolo al recibir otras tandas.

## WhatsApp Business asistido (2.1)

Canal de llamada permite elegir WhatsApp Business (predeterminado) o linea
telefonica. La eleccion se conserva junto con los filtros. El chat se abre con
el numero del prospecto actual de la base de datos, usando explicitamente el
paquete Android com.whatsapp.w4b. No se usa un numero de prueba fijo ni se cambia
al WhatsApp personal o a telefonia si Business no esta instalado.

En Business el operador toca el icono de telefono para iniciar la voz, vuelve al
marcador y declara el resultado. Guardar y abrir siguiente chat conserva el mismo
historial de resultados. Abrir el chat no guarda por si solo un resultado ni un
intento completado; si no se llego a llamar se usa Omitir. No se automatizan
pulsaciones de WhatsApp. Se requiere WhatsApp Business instalado y con sesion.

El simulador de pruebas no tiene WhatsApp Business instalado: se comprobo que
el intento explicito no se resuelve. Se verificaron compilacion Android y
normalizacion de numeros, sin realizar llamadas ni enviar mensajes reales.
