namespace ms_facturacion.Dominio;

/// Misma forma de entrada y salida — SP_DocumentoElectronico_Insertar y _Obtener manejan las mismas 3 columnas.
/// IdCuotaDocumentoElectronico solo existe del lado de salida (Obtener); en entrada (Insertar/Agregar) no hay
/// PK todavía, se deja en su default.
public sealed record CuotaDocumentoElectronico(
    int NumeroCuota, DateOnly FechaVencimiento, decimal Monto, int IdCuotaDocumentoElectronico = 0);
