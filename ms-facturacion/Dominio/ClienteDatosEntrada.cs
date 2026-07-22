namespace ms_facturacion.Dominio;

/// Datos del cliente tal como los envía el llamador — el SP hace upsert contra CLIENTES (match por Tipo+Numero).
public sealed record ClienteDatosEntrada(
    string TipoDocumentoCodigo, string NumeroDocumento, string? Nombre, string? Correo, string? Direccion);
