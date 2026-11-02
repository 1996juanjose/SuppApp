# Comprobantes de pago en RustFS

La web Razor Pages y los endpoints `POST /api/payments/process-voucher` y
`POST /api/records/process-payment` pueden guardar los comprobantes nuevos en
el RustFS de balistica. Los datos del pago permanecen en PostgreSQL.

- Endpoint S3: `https://forense9-rustfs-s3.wv6a0u.easypanel.host`
- Bucket existente: `forense-media`
- Prefijo exclusivo, fijo en codigo: `oldschool/payment-proofs/`
- La carpeta ya fue creada con un objeto `.keep` durante la prueba real.

## Activacion en Easypanel

Configurar **tanto el servicio web como OldSchoolApi**, y volver a desplegarlos:

```dotenv
ObjectStorage__Enabled=true
OpenBao__Enabled=true
OpenBao__Address=https://forense9-openbao.wv6a0u.easypanel.host
OpenBao__Mount=forense
OpenBao__AuthMount=approle
OpenBao__SecretPath=<mismo valor del servicio de balistica>
OpenBao__RoleId=<mismo valor del servicio de balistica>
OpenBao__SecretId=<mismo valor del servicio de balistica>
```

El cargador importa exclusivamente `ObjectStorage` del secreto KV v2 de OpenBao:
`Endpoint`, `Bucket`, `Region`, `AccessKey` y `SecretKey`. No importa la conexion
de base de datos ni la autenticacion de los cursos. Si OpenBao esta habilitado
y no responde, el arranque falla en lugar de usar credenciales de relleno.
El token se revoca cuando la politica lo permite; en otro caso expira por su TTL.

Alternativamente, sin OpenBao, configurar `ObjectStorage__Enabled=true`,
`OpenBao__Enabled=false`, `ObjectStorage__Endpoint`, `ObjectStorage__Bucket`,
`ObjectStorage__Region=us-east-1`, `ObjectStorage__AccessKey` y
`ObjectStorage__SecretKey`. No guardar las claves en el repositorio.

Sin `ObjectStorage__Enabled=true`, se conserva el almacenamiento anterior.

## Lectura y compatibilidad

Se guarda una ruta estable `/rustfs-payment-proofs/{identificador}.jpg` (o png/webp)
en `ProofImagePath`. Ambos servicios resuelven esa misma ruta con autenticacion
y redirigen a una URL S3 firmada que dura cinco minutos. Las URLs firmadas no se
guardan en PostgreSQL. No se necesita hacer publico el bucket.

Los archivos nuevos tienen nombres UUID, limite de 1 MB y extensiones JPG/JPEG,
PNG o WEBP. Si falla la subida a RustFS, no se registra ese pago. Los comprobantes
anteriores permanecen en disco o en su URL original; esta implementacion no los
migra. Mantener los volumenes existentes para poder seguir consultandolos.

Los dos proyectos incluyen copias identicas de `RustFsPaymentProofs.cs` y
`RustFsConfiguration.cs` para conservar sus contextos Docker independientes.
Actualizar ambas copias cuando cambie la integracion.

## Verificacion realizada

- Compilacion correcta de la web y la API con .NET 10.
- Autenticacion real en OpenBao con la configuracion de balistica.
- Subida de un PNG temporal al prefijo exclusivo y descarga mediante URL firmada.
- Comparacion exacta de los bytes descargados y eliminacion del objeto temporal.
- Rechazo de nombres de objeto invalidos, archivos mayores a 1 MB y extensiones no admitidas.
- Creacion de `oldschool/payment-proofs/.keep` en el bucket existente.

Tambien se verifico un pago completo por HTTP contra una instancia local de la
API, con OCR real, persistencia en PostgreSQL, actualizacion del total pagado y
descarga exacta del comprobante desde RustFS. El despliegue de produccion queda
separado de esta verificacion local.
