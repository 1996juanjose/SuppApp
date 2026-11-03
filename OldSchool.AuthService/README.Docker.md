# AuthService en Docker / Easypanel

Este servicio autentica usuarios y devuelve empresas/tokens JWT; no realiza
llamadas. El Dockerfile usa .NET 10, puerto HTTP interno 8080 y usuario app.

## Construccion

Desde la raiz del repositorio (D:/OldSchool):

```powershell
docker build -t oldschool-authservice ./OldSchool.AuthService
docker run --name oldschool-authservice --env-file auth.production.env -p 8086:8080 oldschool-authservice
```

El contexto de build es la carpeta OldSchool.AuthService, no la raiz del repo.
En Easypanel, usar esa subcarpeta como ruta de build y Dockerfile como archivo.
Puerto del servicio: 8080. Configurar un dominio HTTPS apuntando a ese puerto;
Easypanel termina TLS y reenvia HTTP al contenedor. Comprobacion: GET /health.

## Variables requeridas (sin comillas alrededor del valor en Easypanel)

```dotenv
ConnectionStrings__DefaultConnection=Host=HOST_POSTGRES;Port=5432;Database=BD;Username=USUARIO;Password=CLAVE
Jwt__Key=MISMA_CLAVE_SEGURA_CONFIGURADA_EN_OLDSCHOOLAPI
Jwt__Issuer=MISMO_ISSUER_DE_OLDSCHOOLAPI
Jwt__Audience=MISMA_AUDIENCE_DE_OLDSCHOOLAPI
Jwt__ExpiryMinutes=120
```

Usar la misma base de datos que contiene Companies, AspNetUsers, AspNetRoles y
AspNetUserRoles. Dentro de Docker, localhost apunta al propio contenedor: usar
el nombre de servicio de PostgreSQL en la red de Easypanel o su host accesible.
La base debe estar provisionada por la aplicacion existente; AuthService no
crea usuarios ni tablas durante el arranque.

Los archivos appsettings*.json, .env y certificados se excluyen del contexto
para no incluir claves locales en la imagen. Toda la configuracion de produccion
se inyecta con variables de entorno. No subir auth.production.env a Git.

## Verificacion tras desplegar

- GET /health: confirma que el proceso responde; no comprueba PostgreSQL.
- GET /api/auth/companies: debe devolver las empresas activas. El endpoint
  existente devuelve [] si ocurre un error de base de datos, asi que un HTTP 200
  por si solo no confirma conectividad a PostgreSQL.
- POST /api/auth/login: con credenciales reales y CompanyId devuelve el token.
  Verificar luego el token en OldSchoolApi usando /api/records/prospect-calls/queue.

La app Android actual esta configurada para el emulador y localhost:64491/7254.
Publicar este contenedor no cambia automaticamente la APK: para conectarla fuera
del PC, sustituir Auth y Api por sus dominios HTTPS en ServiceEndpoints.cs y
generar una nueva APK. No es necesario exponer PostgreSQL al telefono.
