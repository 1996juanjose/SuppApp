# PRD - OldSchoolLab / OldSchoolApi

## 1. Resumen
`OldSchoolLab` es una aplicación web en `ASP.NET Core Razor Pages` para gestionar registros comerciales, pagos, estados, productos, inventario y auditoría. `OldSchoolApi` es una API separada para autenticación e integración con automatizaciones externas como `n8n`.

## 2. Objetivo
Centralizar el registro y seguimiento operativo de contactos comerciales en una plataforma web simple, con control de pagos, stock e inventario, y una API complementaria para integraciones externas.

## 3. Problema que resuelve
El proyecto busca:

- registrar contactos y su estado comercial
- evitar duplicados por `Celular`
- manejar productos con precios por cantidad
- registrar movimientos de inventario y calcular stock por producto
- crear y revertir automáticamente salidas de stock cuando un registro queda o deja de quedar totalmente pagado
- calcular ganancias por rango de fechas con gastos opcionales
- auditar acciones relevantes
- permitir inserciones automáticas desde `n8n`

## 4. Alcance actual

### 4.1 `OldSchoolLab`
Módulos observados:

- autenticación web
- gestión de registros
- edición de registros
- catálogo de productos
- inventario y movimientos de stock
- ganancias y gastos por rango de fechas
- auditoría
- autorización por roles

### 4.2 `OldSchoolApi`
Capacidades observadas:

- login con `JWT`
- creación de registros protegida por token
- endpoint para `n8n`
- regla de no modificar si ya existe el `Celular`

## 5. Usuarios y roles
Roles identificados:

- `SuperAdmin`
- `AdminEmpresa`
- `Gerencia`
- `Gestor`
- `Monitoreo`
- `Vendedor`

Usuario de prueba configurado:

- usuario: `ADMIN`
- contraseña: `ADMIN`

## 6. Requerimientos funcionales

### RF-01. Autenticación web
El sistema debe permitir inicio y cierre de sesión.

**Criterios de aceptación**
- acceso con credenciales válidas
- páginas internas protegidas
- cierre de sesión disponible

### RF-02. Control por roles
El sistema debe restringir acceso según rol.

**Criterios de aceptación**
- `Gerencia` y `Gestor` pueden crear y editar registros
- las áreas administrativas requieren autorización

### RF-03. Gestión de registros
El sistema debe permitir crear y editar registros comerciales.

Campos funcionales observados:
- `Estado`
- `Fecha`
- `Celular`
- `Nombre / Ref WA`
- `Actividad de la llamada`
- `DNI`
- `Producto`
- `Cantidad`
- `Pagado`
- `Ruta carpeta`

**Criterios de aceptación**
- `Celular`, `Estado` y `Fecha` son obligatorios
- si se selecciona producto, debe existir precio para la cantidad
- el sistema calcula el saldo pendiente

### RF-04. Catálogo de estados
El sistema debe manejar estados como catálogo fijo.

Estados base:
- `Clientes`
- `Rechazo`
- `Interesado`
- `Por Pagar`
- `Prospecto`

**Criterios de aceptación**
- solo se muestran estados activos
- el estado por defecto es `Prospecto`

### RF-05. Catálogo de productos con precios por cantidad
El sistema debe permitir precios escalonados por cantidad.

Ejemplo:
- `Creatina`
  - 1 unidad: `89`
  - 2 unidades: `149`
  - 3 unidades: `189`

**Criterios de aceptación**
- un producto puede tener varios precios
- si no existe precio para una cantidad, no debe guardarse

### RF-06. Inventario y movimientos de stock
El sistema debe permitir registrar movimientos manuales de stock y mostrar el cálculo de inventario.

**Criterios de aceptación**
- se puede registrar ingreso o egreso manual por producto
- el stock visible se calcula como `Ingresos - Egresos`
- se muestran los movimientos recientes
- la salida automática por pago completo queda asociada al registro comercial

### RF-07. Ganancias y gastos opcionales
El sistema debe permitir calcular la ganancia neta en un rango de fechas.

**Criterios de aceptación**
- se filtra por rango de fechas
- la ganancia base se calcula como `Pagado - Comisiones - Costo del producto`
- se pueden agregar gastos opcionales con `Nombre` y `Costo`
- la ganancia neta descuenta esos gastos extra

### RF-08. Auditoría
El sistema debe registrar acciones relevantes.

**Criterios de aceptación**
- al crear un registro se genera auditoría
- se almacena usuario, fecha y detalle

### RF-09. API de autenticación
La API debe permitir obtener un `JWT` usando las credenciales del sistema web.

Endpoint:
- `POST /api/auth/login`

### RF-10. API de registros autenticada
La API debe permitir crear registros usando `JWT`.

Endpoint:
- `POST /api/records`

**Criterios de aceptación**
- requiere token válido
- crea registro si no existe el `Celular`
- no modifica si el `Celular` ya existe

### RF-11. Integración con `n8n`
La API debe permitir recibir registros desde automatizaciones.

Endpoint:
- `POST /api/records/n8n`

Header:
- `X-Api-Key`

Payload esperado:
- `Celular`
- `Estado`
- `AutoCont`
- `Nombre`

**Criterios de aceptación**
- requiere `X-Api-Key` válida
- si el `Celular` ya existe, responde `skipped = true`
- si no existe, crea el registro

## 7. Reglas de negocio

- no duplicar por `Celular` desde la API
- usar `Prospecto` como estado por defecto si no se envía uno
- calcular saldo pendiente como `ProductAmount - PaidAmount`, nunca menor a `0`
- resolver precios por cantidad del producto
- sincronizar stock con el estado de pago del registro

## 8. Modelo funcional
Entidades principales:

- `CustomerRecord`
- `StatusCatalog`
- `Product`
- `ProductPrice`
- `ProductStockMovement`
- `CustomerRecordPayment`
- `Expense`
- `AuditLog`
- usuarios y roles de `Identity`

## 9. Arquitectura actual

### 9.1 Componentes
- `OldSchoolLab`: frontend web en `Razor Pages`
- `OldSchoolApi`: API separada
- `PostgreSQL`: persistencia compartida
- `n8n`: integrador externo

### 9.2 Autenticación
- web: `ASP.NET Core Identity`
- api: `JWT`
- integración `n8n`: `X-Api-Key`

### 9.3 Despliegue
Puertos de contenedor definidos:
- `OldSchoolApi`: `8080`
- `OldSchoolLab`: `8085`

## 10. Flujos principales

### Flujo A. Registro manual desde web
1. iniciar sesión
2. abrir creación de registro
3. ingresar datos y producto opcional
4. guardar registro
5. registrar auditoría

### Flujo B. Registro automático desde `n8n`
1. llamar `POST /api/records/n8n`
2. enviar `X-Api-Key`
3. enviar datos del contacto
4. validar existencia por `Celular`
5. crear o responder `skipped = true`

### Flujo C. Consumo API con `JWT`
1. llamar `POST /api/auth/login`
2. recibir token
3. usar token en `POST /api/records`

### Flujo D. Pago completo y salida autom?tica de stock
1. registrar pagos al `CustomerRecord`
2. cuando `BalanceDue` llega a `0`, crear una salida de stock
3. dejar nota con `Celular` y `Fecha`
4. vincular el movimiento al registro

### Flujo E. Extorno de pago y reversi?n de stock
1. marcar el pago como extornado
2. recalcular `BalanceDue`
3. si el registro deja de estar totalmente pagado, eliminar la salida autom?tica asociada
4. si vuelve a quedar totalmente pagado, recrear la salida autom?tica

## 11. Requerimientos no funcionales

- seguridad con `Identity`, roles, `JWT` y `ApiKey`
- separación entre proyecto web y proyecto API
- despliegue con `Dockerfile`
- base de datos `PostgreSQL`

## 12. Riesgos actuales

- configuración manual de `Jwt:Key` y `N8n:ApiKey`
- desalineación de esquema en base de datos
- dependencia de configuración correcta de puertos y variables en contenedores

## 13. Roadmap sugerido

### Fase 1
- operación web base
- catálogos
- auditoría

### Fase 2
- integración `n8n`
- validación por `Celular`

### Fase 3
- documentación técnica formal
- pruebas automatizadas
- versionado de API
- migraciones controladas

## 14. Referencias internas
Archivos clave:

- `OldSchoolLab/Program.cs`
- `OldSchoolLab/Data/ApplicationDbContext.cs`
- `OldSchoolLab/Data/SeedData.cs`
- `OldSchoolLab/Pages/Records/Create.cshtml.cs`
- `OldSchoolLab/Pages/Admin/Inventory/Index.cshtml.cs`
- `OldSchoolLab/Pages/Admin/Inventory/Index.cshtml`
- `../OldSchoolApi/OldSchoolApi/Program.cs`
- `../OldSchoolApi/OldSchoolApi/Controllers/AuthController.cs`
- `../OldSchoolApi/OldSchoolApi/Controllers/RecordsController.cs`
