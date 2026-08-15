namespace ms_facturacion.Dominio;

/// Cuota tal como queda persistida — usada en la salida de Obtener/GuardarCambios/ActualizarEstadoCuota.
/// EstadoCuotaCodigo ya viene resuelto (JOIN contra TABLA_MAESTRA IdMaestro=7 dentro del SP).
public sealed record CuotaDocumentoElectronico(
    int IdCuotaDocumentoElectronico, int NumeroCuota, DateOnly FechaVencimiento, decimal Monto,
    string EstadoCuotaCodigo, DateTime? FechaPago);

/// Cuota tal como la envía el llamador (Insertar y "Guardar cambios" en lote) — mismo criterio que
/// LineaDocumentoElectronicoEntrada. IdCuotaDocumentoElectronico solo se usa en Guardar cambios: 0 = cuota
/// nueva, >0 = cuota existente a actualizar. IdEstadoCuotaMaestro es Num1 de TABLA_MAESTRA IdMaestro=7
/// (1=Pendiente, 2=Pagado) y FechaPago debe ser coherente con él (NULL si Pendiente, obligatoria si Pagado)
/// — el llamador los decide explícitamente (p.ej. Pagado directo con su fecha real al registrar un
/// documento histórico ya cobrado); ninguno de los dos tiene un default implícito.
public sealed record CuotaDocumentoElectronicoEntrada(
    int NumeroCuota, DateOnly FechaVencimiento, decimal Monto, int IdEstadoCuotaMaestro, DateTime? FechaPago,
    int IdCuotaDocumentoElectronico = 0);
