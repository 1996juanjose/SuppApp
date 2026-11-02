# Recompras y caja

## Recompras por API

`POST /api/records` y `POST /api/records/n8n` permiten otro registro para el mismo
celular y empresa cuando pasaron **mas de 30 dias exactos** desde la creacion
del registro anterior. Hasta ese momento devuelven `skipped=true`, el `id`
existente y `canCreateAfter`. No se modifica el registro anterior.

Se usa `CreatedAt` del registro mas reciente, no la fecha del voucher ni una
fecha `AutoCont` enviada por el cliente. PostgreSQL serializa la comprobacion
y creacion por empresa/celular para evitar duplicados de webhooks simultaneos.

La respuesta incluye `purchaseNumber` e `isRepeatCustomer`. El listado web
muestra `2.ª compra`, `3.ª compra`, etc. junto al celular. El ordinal se calcula
con el historial completo por empresa, aunque el listado filtre fechas.
Registros consecutivos con hasta 30 dias de diferencia se agrupan en el mismo
ciclo; se trata del numero de ciclo de pedido, no de una certificacion de pago.

Pagos (`/api/payments/process-voucher`, `/api/records/process-payment`), guias
(`/api/records/process-shipment`) y actualizacion por texto usan el registro
mas reciente por `CreatedAt` y, en caso de empate, por `Id`. Todos esos pedidos
admiten `CompanyId` opcional; enviarlo desde n8n cuando haya varias empresas.
La empresa del JWT tiene prioridad. La validacion de operacion duplicada sigue
aplicando. Los adelantos `Pagado` al crear un registro generan tambien su fila
de pago para el historial y la caja.

## Caja

Pagina: `/Cash`. Disponible en la navegacion y con resumen de la caja abierta
en `/Records`. Los importes de caja no dependen de los filtros del listado.

- Una caja abierta por empresa; no hay apertura ni cierre automaticos.
- Solo el usuario autenticado con nombre exacto `ADMIN` puede abrir/cerrar,
  con validacion en servidor. El rol Gerencia por si solo no concede ese permiso.
- Se guardan fecha/hora y usuario de apertura y cierre. Hora: America/Lima,
  salvo configuracion explicita de `App:TimeZoneId`.
- ADMIN y Gerencia pueden agregar gastos mientras la caja esta abierta.
  Cada gasto guarda concepto, monto positivo, detalle, usuario y hora de registro.
- Ingresos: pagos registrados dentro del periodo, por `CreatedAt`.
- Extornos: pagos revertidos dentro del periodo, por `ReversedAt`, incluso
  si el pago original era anterior a la apertura.
- Gastos: egresos de la empresa registrados dentro del periodo, incluyendo
  los que se registren desde la pantalla existente de gastos.
- Saldo neto = ingresos - extornos - gastos. No incluye efectivo inicial.
- Periodo: apertura inclusiva, cierre exclusivo. Una caja cerrada no recibe
  movimientos con fecha de registro posterior ni permite agregar nuevos gastos.
  El resumen se calcula sobre los movimientos existentes; no es una fotografia
  contable inmutable frente a ediciones posteriores de movimientos historicos.

`SeedData` crea de forma idempotente `CashRegisters`, su indice unico de caja
abierta y la columna opcional `Expenses.CashRegisterId`. Reiniciar la web al
actualizar para aplicar estos cambios; los gastos anteriores se conservan.
Reiniciar tambien la API para activar la logica de recompras y guias.

## Pruebas aisladas

Desde la carpeta OldSchoolLab:

```powershell
dotnet run --project tests/BusinessRules/BusinessRules.csproj -p:UseAppHost=false
```

Usan EF Core en memoria y OCR simulado, sin conexiones externas ni movimientos
en la base real. Cubren el limite de 30 dias, numeracion historica, empresa,
webhooks repetidos, pagos y guias al pedido nuevo, permisos ADMIN, aperturas
duplicadas, cierre, gastos invalidos y sumatorias por periodo.
