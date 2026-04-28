using System;
using System.Collections.Generic;

namespace SuplementosApp.Web.Data;

public class Venta
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; }
    public string NombreCliente { get; set; } = string.Empty;
    public string? Region { get; set; }
    public string? DireccionEnvio { get; set; }
    public string? Celular { get; set; }
    public string? DNI { get; set; }
    public int EstadoVentaId { get; set; }
    public EstadoVenta? EstadoVenta { get; set; }
    public string? Observacion { get; set; }
    public string? NroOrden { get; set; }
    public string? Cod { get; set; }
    public string? Clave { get; set; }
    public decimal CostoEnvio { get; set; }
    public int MetodoEnvioId { get; set; }
    public MetodoEnvio? MetodoEnvio { get; set; }
    public string? Productos { get; set; }
    public int MetodoPagoId { get; set; }
    public MetodoPago? MetodoPago { get; set; }
    public int CanalVentaId { get; set; }
    public CanalVenta? CanalVenta { get; set; }
    public decimal PrecioVenta { get; set; }
    public decimal Pagado { get; set; }
    public decimal Debe => PrecioVenta - Pagado;
    public int VendedorId { get; set; }
    public Vendedor? Vendedor { get; set; }
    public decimal Comision { get; set; }
    public string TipoContacto { get; set; } = "Lead";
}

public class Vendedor
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Email { get; set; }
    public decimal ComisionBase { get; set; }
    public DateTime? FechaIngreso { get; set; }
    public bool Activo { get; set; } = true;
    public string? Observacion { get; set; }

    public ICollection<Venta> Ventas { get; set; } = new List<Venta>();
    public ICollection<ReporteDiarioDetalle> ReportesDiariosDetalle { get; set; } = new List<ReporteDiarioDetalle>();
}

public class Gasto
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; }
    public string? Detalle { get; set; }
    public int CategoriaGastoId { get; set; }
    public CategoriaGasto? CategoriaGasto { get; set; }
    public decimal Monto { get; set; }
}

public class ReporteDiario
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; }
    public decimal GastoPublicidadFB { get; set; }
    public int TotalMensajes { get; set; }
    public decimal CostoPorMensaje { get; set; }
    public int TotalPedidos { get; set; }
    public decimal FacturacionTotal { get; set; }
    public decimal ROAS { get; set; }
    public decimal AOV { get; set; }
    public decimal CPA { get; set; }
    public decimal TasaCierre { get; set; }
    public string? Observacion { get; set; }

    public ICollection<ReporteDiarioDetalle> Detalles { get; set; } = new List<ReporteDiarioDetalle>();
}

public class ReporteDiarioDetalle
{
    public int Id { get; set; }
    public int ReporteDiarioId { get; set; }
    public ReporteDiario? ReporteDiario { get; set; }
    public int VendedorId { get; set; }
    public Vendedor? Vendedor { get; set; }
    public decimal Facturado { get; set; }
    public decimal Publicidad { get; set; }
    public int Mensajes { get; set; }
    public int Pedidos { get; set; }
    public decimal TasaCierre { get; set; }
    public string? Observacion { get; set; }
}

public class EstadoVenta
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;

    public ICollection<Venta> Ventas { get; set; } = new List<Venta>();
}

public class MetodoEnvio
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;

    public ICollection<Venta> Ventas { get; set; } = new List<Venta>();
}

public class MetodoPago
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;

    public ICollection<Venta> Ventas { get; set; } = new List<Venta>();
}

public class CanalVenta
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;

    public ICollection<Venta> Ventas { get; set; } = new List<Venta>();
}

public class CategoriaGasto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;

    public ICollection<Gasto> Gastos { get; set; } = new List<Gasto>();
}

public class SeguimientoCliente
{
    public int Id { get; set; }
    public string? Nombre { get; set; }
    public string? Numero { get; set; }
    public string? ProductoInteres { get; set; }
    public bool LlamadaRealizada { get; set; }
    public bool Rechazado { get; set; }
    public string? Observacion { get; set; }
    public DateTime? FechaContacto { get; set; }
}
