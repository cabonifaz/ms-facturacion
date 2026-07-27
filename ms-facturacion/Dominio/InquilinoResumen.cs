namespace ms_facturacion.Dominio;

/// Proyección liviana para listados — SP_Inquilino_Listar solo devuelve estas columnas, no el detalle completo.
public sealed record InquilinoResumen(int IdInquilino, string Codigo, string Nombre, bool Activo);
