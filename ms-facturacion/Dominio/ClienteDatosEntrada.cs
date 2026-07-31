namespace ms_facturacion.Dominio;

/// Datos del cliente tal como los envía el llamador — no hay tabla CLIENTES, este snapshot va directo a
/// las columnas Cliente* de DOCUMENTOS_ELECTRONICOS.
public sealed record ClienteDatosEntrada(
    string TipoDocumentoCodigo, string NumeroDocumento, string? Nombre, string? Correo, string? Direccion);
