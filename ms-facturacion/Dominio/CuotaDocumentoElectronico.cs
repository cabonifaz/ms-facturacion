namespace ms_facturacion.Dominio;

/// Misma forma de entrada y salida — SP_DocumentoElectronico_Insertar y _Obtener manejan las mismas columnas.
/// IdCuotaDocumentoElectronico solo existe del lado de salida (Obtener); en entrada (Insertar/GuardarCambios)
/// no hay PK todavía, se deja en su default. EstadoCuotaCodigo/FechaPago son solo de salida — toda cuota
/// nueva arranca en 'Pendiente' (resuelto por el propio SP, nunca recibido como input); se cambian solo
/// via SP_CuotaDocumentoElectronico_ActualizarEstado.
public sealed record CuotaDocumentoElectronico(
    int NumeroCuota, DateOnly FechaVencimiento, decimal Monto, int IdCuotaDocumentoElectronico = 0,
    string EstadoCuotaCodigo = "", DateTime? FechaPago = null);
