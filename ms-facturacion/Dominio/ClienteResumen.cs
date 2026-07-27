namespace ms_facturacion.Dominio;

/// Proyección liviana para listados — SP_Cliente_Listar solo devuelve estas columnas.
public sealed record ClienteResumen(int IdCliente, string NumeroDocumento, string Nombre);
