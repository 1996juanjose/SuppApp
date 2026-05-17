# Flujo de pagos por voucher

## 1. Resumen
Este documento resume la implementación del flujo de pagos por voucher entre `OldSchoolApi` y `OldSchoolLab`.

El objetivo es registrar pagos desde vouchers de `Yape` o `Plin`, guardar el comprobante, actualizar el estado del registro y mostrar la información en la interfaz web.

## 2. Componentes involucrados

### `OldSchoolApi`
Responsable de:
- recibir vouchers desde automatizaciones o integraciones
- extraer datos con `OpenAI`
- guardar el comprobante en disco
- registrar el pago en la base de datos
- actualizar el estado del registro

### `OldSchoolLab`
Responsable de:
- mostrar registros y pagos
- permitir carga manual de comprobantes
- visualizar comprobantes en modal
- mostrar la hora de registro y la hora real del voucher

## 3. Endpoints relacionados

### `POST /api/payments/process-voucher`
Procesa un voucher enviado desde `n8n` u otro cliente.

Funciones principales:
- valida `Celular` e `ImageBase64`
- busca el registro por celular
- analiza la imagen con `OpenAI Vision`
- detecta:
  - monto
  - tipo de voucher
  - número de operación
  - fecha y hora del voucher
- valida duplicados por número de operación
- guarda el comprobante
- registra el pago
- actualiza el estado del registro

### `POST /api/records/process-payment`
Procesa un comprobante de pago desde la API de registros.

Funciones principales:
- valida el header `X-Api-Key`
- analiza la imagen con `OpenAI`
- registra el pago
- actualiza el saldo del registro
- actualiza el estado según el saldo total

### `POST /api/records/n8n`
Crea registros automáticos desde `n8n`.

## 4. Reglas de estado de pago

La lógica actual usa estas reglas:

- si el pago es parcial, el estado debe quedar en `Por Pagar`
- si el total pagado alcanza o supera el monto del producto, el estado debe quedar en `Cliente` o `Clientes`

Esto aplica en:
- pago manual desde la web
- pago por voucher desde la API
- reversión de pago

## 5. Timestamps

### `PaymentDate`
Se usa para guardar la fecha/hora real del voucher.

Comportamiento actual:
- en pagos por voucher, `PaymentDate` se toma desde la fecha/hora detectada en la imagen
- en pagos manuales, puede seguir usándose la fecha de registro si no hay voucher

### `CreatedAt`
Se usa para registrar la hora real en que se guardó el pago en el sistema.

Esto permite diferenciar claramente:
- `PaymentDate` = hora del voucher
- `CreatedAt` = hora de registro en el sistema

## 6. Comprobantes de pago

### Guardado
Los comprobantes se almacenan en una carpeta compartida configurada en `Storage:PaymentProofsPath`.

### Exposición pública
El API publica esa carpeta en:
- `/payment-proofs`

### Visualización en web
En `OldSchoolLab` se muestra un botón `Ver` para abrir el comprobante en modal.

## 7. Configuración importante

### Variables de entorno recomendadas
- `Jwt__Key`
- `OpenAI__ApiKey`
- `N8n__ApiKey`

### Notas
- no se deben commitear secretos en `appsettings.json`
- si `Storage:PaymentProofsPath` no es válido en el entorno actual, el API usa una ruta alternativa basada en `ContentRootPath`
- `JwtKeyProvider` permite que el API arranque incluso si no hay `Jwt:Key` definido en configuración

## 8. Visualización en la web

En la vista de registros se muestran:
- `Fecha pago`
- `Registrado`
- `Cod. operación`
- `Monto`
- `Estado`
- `Usuario`
- `Comprobante`

En la edición del registro se muestra además el historial completo de pagos y el modal para ver el comprobante.

## 9. Validaciones implementadas

- validación de celular no vacío y con formato correcto
- validación de imagen base64
- detección de duplicados por número de operación
- validación de monto detectado mayor a `0`
- manejo de errores por ausencia o invalidez de la API key de `OpenAI`
- almacenamiento consistente de comprobantes para web y API

## 10. Despliegue

Puertos definidos:
- `OldSchoolApi`: `8080`
- `OldSchoolLab`: `8085`

Recomendación:
- mantener ambas apps apuntando al mismo almacenamiento compartido de comprobantes
- configurar las claves sensibles como variables de entorno en lugar de dejarlas en el repositorio

## 11. Observaciones finales

- `OpenAI` se usa para OCR de vouchers y extracción de datos
- la UI web ya está preparada para mostrar comprobantes, número de operación y hora de registro
- el flujo de pagos quedó preparado para distinguir entre `Por Pagar` y `Cliente/Clientes`
