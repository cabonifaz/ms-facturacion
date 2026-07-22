namespace ms_facturacion.Dominio;

/// Misma forma de entrada y salida — SP_DocumentoElectronico_Insertar y _Obtener manejan exactamente estas 3 columnas.
public sealed record CuotaDocumentoElectronico(int NumeroCuota, DateOnly FechaVencimiento, decimal Monto);
